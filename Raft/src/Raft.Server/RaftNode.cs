using Microsoft.Extensions.Logging;
using Raft.Common.Interfaces;
using Raft.Common.Models;
using System.Collections.Concurrent;

namespace Raft.Server;

/// <summary>
/// Raft 节点实现
/// </summary>
public class RaftNode : IRaftNode
{
    private readonly ILogger<RaftNode> _logger;
    private readonly List<string> _peers;
    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // Raft核心状态
    private RaftNodeState _state = RaftNodeState.Follower;
    private int _currentTerm = 0;
    private string? _votedFor = null;
    private readonly List<LogEntry> _log = new();

    // 选举相关
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    private readonly Random _random = new();
    private Task? _electionTask;
    private Task? _heartbeatTask;

    // Leader状态（重置当选举为Leader时）
    private readonly Dictionary<string, int> _nextIndex = new();
    private readonly Dictionary<string, int> _matchIndex = new();

    // 状态机相关
    private int _commitIndex = 0;
    private int _lastApplied = 0;

    // 网络通信相关（简化版，实际应用中应该使用HTTP客户端或gRPC等）
    private readonly Dictionary<string, IRaftNode> _peerNodes = new();

    public RaftNode(string nodeId, List<string> peers, ILogger<RaftNode> logger)
    {
        NodeId = nodeId;
        _peers = peers ?? throw new ArgumentNullException(nameof(peers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("创建Raft节点 {NodeId}，集群节点: [{Peers}]",
            NodeId, string.Join(", ", _peers));
    }

    public string NodeId { get; }

    public RaftNodeState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public int CurrentTerm
    {
        get
        {
            lock (_stateLock)
            {
                return _currentTerm;
            }
        }
    }

    public bool IsLeader => State == RaftNodeState.Leader;

    /// <summary>
    /// 设置对等节点引用（用于网络通信）
    /// 在实际应用中，这应该通过HTTP客户端、gRPC或其他网络协议实现
    /// </summary>
    public void SetPeerNodes(Dictionary<string, IRaftNode> peerNodes)
    {
        foreach (var peer in peerNodes)
        {
            _peerNodes[peer.Key] = peer.Value;
        }
    }

    public async Task<VoteResponse> RequestVoteAsync(VoteRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("收到来自 {CandidateId} 的投票请求，任期 {Term}",
            request.CandidateId, request.Term);

        lock (_stateLock)
        {
            // 如果请求的任期小于当前任期，拒绝投票
            if (request.Term < _currentTerm)
            {
                _logger.LogDebug("拒绝投票给 {CandidateId}：任期过时 ({RequestTerm} < {CurrentTerm})",
                    request.CandidateId, request.Term, _currentTerm);
                return new VoteResponse { Term = _currentTerm, VoteGranted = false };
            }

            // 如果请求的任期更大，更新当前任期并转为Follower
            if (request.Term > _currentTerm)
            {
                _logger.LogInformation("收到更大任期 {NewTerm}，从任期 {OldTerm} 转为Follower",
                    request.Term, _currentTerm);
                _currentTerm = request.Term;
                _votedFor = null;
                _state = RaftNodeState.Follower;
            }

            // 检查是否可以投票
            bool canVote = _votedFor == null || _votedFor == request.CandidateId;
            bool logUpToDate = IsLogUpToDate(request.LastLogIndex, request.LastLogTerm);

            if (canVote && logUpToDate)
            {
                _votedFor = request.CandidateId;
                _lastHeartbeat = DateTime.UtcNow; // 重置选举超时
                _logger.LogInformation("投票给候选人 {CandidateId}，任期 {Term}",
                    request.CandidateId, request.Term);
                return new VoteResponse { Term = _currentTerm, VoteGranted = true };
            }
            else
            {
                _logger.LogDebug("拒绝投票给 {CandidateId}：已投票={VotedFor}, 日志最新={LogUpToDate}",
                    request.CandidateId, _votedFor, logUpToDate);
                return new VoteResponse { Term = _currentTerm, VoteGranted = false };
            }
        }
    }

    public async Task<AppendEntriesResponse> AppendEntriesAsync(AppendEntriesRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("收到来自Leader {LeaderId} 的AppendEntries，任期 {Term}，条目数量 {EntryCount}",
            request.LeaderId, request.Term, request.Entries.Count);

        lock (_stateLock)
        {
            // 如果请求的任期小于当前任期，拒绝
            if (request.Term < _currentTerm)
            {
                _logger.LogDebug("拒绝AppendEntries：任期过时 ({RequestTerm} < {CurrentTerm})",
                    request.Term, _currentTerm);
                return new AppendEntriesResponse { Term = _currentTerm, Success = false };
            }

            // 更新任期并转为Follower
            if (request.Term >= _currentTerm)
            {
                if (request.Term > _currentTerm)
                {
                    _logger.LogInformation("收到更大任期 {NewTerm}，从任期 {OldTerm} 更新",
                        request.Term, _currentTerm);
                    _currentTerm = request.Term;
                    _votedFor = null;
                }
                _state = RaftNodeState.Follower;
                _lastHeartbeat = DateTime.UtcNow; // 重置选举超时
            }

            // TODO: 实现日志一致性检查和日志追加逻辑
            // 这里暂时简单返回成功，完整实现将在Part 2B中完成
            return new AppendEntriesResponse { Term = _currentTerm, Success = true };
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("启动Raft节点 {NodeId}", NodeId);

        // 启动选举定时器
        _electionTask = Task.Run(ElectionTimeoutLoop, _cancellationTokenSource.Token);

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("停止Raft节点 {NodeId}", NodeId);

        _cancellationTokenSource.Cancel();

        if (_electionTask != null)
            await _electionTask;
        if (_heartbeatTask != null)
            await _heartbeatTask;
    }

    public async Task<bool> SubmitCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsLeader)
        {
            _logger.LogWarning("尝试提交命令到非Leader节点 {NodeId}", NodeId);
            return false;
        }

        // TODO: 实现客户端命令提交逻辑
        // 这将在Part 2B中实现
        _logger.LogInformation("Leader {NodeId} 收到命令: {Command}", NodeId, command);
        return true;
    }

    /// <summary>
    /// 选举超时循环
    /// </summary>
    private async Task ElectionTimeoutLoop()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var electionTimeout = GetElectionTimeout();
                await Task.Delay(electionTimeout, _cancellationTokenSource.Token);

                lock (_stateLock)
                {
                    // 检查是否需要开始选举
                    if (_state != RaftNodeState.Leader &&
                        DateTime.UtcNow - _lastHeartbeat > TimeSpan.FromMilliseconds(electionTimeout))
                    {
                        _logger.LogInformation("选举超时，节点 {NodeId} 开始选举", NodeId);
                        _ = Task.Run(StartElection, _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "选举超时循环发生错误");
            }
        }
    }

    /// <summary>
    /// 开始选举
    /// </summary>
    private async Task StartElection()
    {
        _logger.LogInformation("节点 {NodeId} 开始选举", NodeId);

        int currentTerm;
        int lastLogIndex;
        int lastLogTerm;

        // 更新状态并准备选举
        lock (_stateLock)
        {
            _state = RaftNodeState.Candidate;
            _currentTerm++;
            _votedFor = NodeId; // 给自己投票
            _lastHeartbeat = DateTime.UtcNow;

            currentTerm = _currentTerm;
            lastLogIndex = _log.Count > 0 ? _log.Last().Index : 0;
            lastLogTerm = _log.Count > 0 ? _log.Last().Term : 0;
        }

        _logger.LogInformation("节点 {NodeId} 成为候选人，任期 {Term}，开始请求投票", NodeId, currentTerm);

        var votes = 1; // 给自己投票
        var totalNodes = _peers.Count + 1; // 包括自己
        var majorityVotes = totalNodes / 2 + 1;

        _logger.LogDebug("需要 {MajorityVotes} 票（总共 {TotalNodes} 个节点）", majorityVotes, totalNodes);

        // 特殊情况：如果只有一个节点，直接成为Leader
        if (_peers.Count == 0)
        {
            _logger.LogInformation("单节点集群，节点 {NodeId} 直接成为Leader", NodeId);
            await BecomeLeader();
            return;
        }

        var voteRequest = new VoteRequest
        {
            Term = currentTerm,
            CandidateId = NodeId,
            LastLogIndex = lastLogIndex,
            LastLogTerm = lastLogTerm
        };

        var voteTasks = new List<Task<VoteResponse>>();

        // 并行向所有对等节点发送投票请求
        foreach (var peerId in _peers)
        {
            if (_peerNodes.TryGetValue(peerId, out var peerNode))
            {
                var voteTask = RequestVoteFromPeerAsync(peerNode, voteRequest);
                voteTasks.Add(voteTask);
            }
            else
            {
                _logger.LogWarning("无法找到对等节点 {PeerId} 的引用", peerId);
            }
        }

        // 等待投票结果或超时
        var electionTimeout = TimeSpan.FromMilliseconds(GetElectionTimeout());
        var cancellationTokenSource = new CancellationTokenSource(electionTimeout);

        try
        {
            while (voteTasks.Count > 0 && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                var completedTask = await Task.WhenAny(voteTasks);
                voteTasks.Remove(completedTask);

                try
                {
                    var response = await completedTask;

                    // 检查是否收到更高任期的响应
                    if (response.Term > currentTerm)
                    {
                        _logger.LogInformation("收到更高任期 {NewTerm}，节点 {NodeId} 转为Follower",
                            response.Term, NodeId);

                        lock (_stateLock)
                        {
                            if (response.Term > _currentTerm)
                            {
                                _currentTerm = response.Term;
                                _votedFor = null;
                                _state = RaftNodeState.Follower;
                                _lastHeartbeat = DateTime.UtcNow;
                            }
                        }
                        return;
                    }

                    // 统计投票
                    if (response.VoteGranted)
                    {
                        votes++;
                        _logger.LogDebug("节点 {NodeId} 获得一票，当前票数: {Votes}/{MajorityVotes}",
                            NodeId, votes, majorityVotes);

                        // 检查是否获得多数票
                        if (votes >= majorityVotes)
                        {
                            await BecomeLeader();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取投票响应时发生错误");
                }
            }

            // 选举超时或没有获得多数票
            _logger.LogInformation("节点 {NodeId} 选举失败，获得 {Votes} 票，需要 {MajorityVotes} 票",
                NodeId, votes, majorityVotes);

            lock (_stateLock)
            {
                if (_state == RaftNodeState.Candidate && _currentTerm == currentTerm)
                {
                    _state = RaftNodeState.Follower;
                    _lastHeartbeat = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("节点 {NodeId} 选举超时", NodeId);

            lock (_stateLock)
            {
                if (_state == RaftNodeState.Candidate && _currentTerm == currentTerm)
                {
                    _state = RaftNodeState.Follower;
                }
            }
        }
    }

    /// <summary>
    /// 向对等节点请求投票
    /// </summary>
    private async Task<VoteResponse> RequestVoteFromPeerAsync(IRaftNode peerNode, VoteRequest request)
    {
        try
        {
            var response = await peerNode.RequestVoteAsync(request);
            _logger.LogTrace("收到来自节点的投票响应：Term={Term}, VoteGranted={VoteGranted}",
                response.Term, response.VoteGranted);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "向对等节点请求投票时发生错误");
            // 返回拒绝投票的响应
            return new VoteResponse { Term = request.Term, VoteGranted = false };
        }
    }

    /// <summary>
    /// 成为Leader
    /// </summary>
    private async Task BecomeLeader()
    {
        _logger.LogInformation("🎉 节点 {NodeId} 成为Leader，任期 {Term}", NodeId, CurrentTerm);

        lock (_stateLock)
        {
            _state = RaftNodeState.Leader;
            _lastHeartbeat = DateTime.UtcNow;

            // 初始化Leader状态
            _nextIndex.Clear();
            _matchIndex.Clear();

            var nextIndex = _log.Count > 0 ? _log.Last().Index + 1 : 1;
            foreach (var peerId in _peers)
            {
                _nextIndex[peerId] = nextIndex;
                _matchIndex[peerId] = 0;
            }
        }

        // 启动心跳任务
        _heartbeatTask = Task.Run(SendHeartbeats, _cancellationTokenSource.Token);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 发送心跳（空的AppendEntries）
    /// </summary>
    private async Task SendHeartbeats()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested && IsLeader)
        {
            try
            {
                var heartbeatTasks = new List<Task>();

                foreach (var peerId in _peers)
                {
                    if (_peerNodes.TryGetValue(peerId, out var peerNode))
                    {
                        var heartbeatTask = SendHeartbeatToPeerAsync(peerNode);
                        heartbeatTasks.Add(heartbeatTask);
                    }
                }

                // 并行发送心跳
                await Task.WhenAll(heartbeatTasks);

                // 心跳间隔（通常比选举超时小得多）
                await Task.Delay(50, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送心跳时发生错误");
            }
        }
    }

    /// <summary>
    /// 向对等节点发送心跳
    /// </summary>
    private async Task SendHeartbeatToPeerAsync(IRaftNode peerNode)
    {
        try
        {
            var request = new AppendEntriesRequest
            {
                Term = CurrentTerm,
                LeaderId = NodeId,
                Entries = new List<LogEntry>(), // 空心跳
                PrevLogIndex = 0,
                PrevLogTerm = 0,
                LeaderCommit = _commitIndex
            };

            var response = await peerNode.AppendEntriesAsync(request);

            // 如果收到更高任期的响应，转为Follower
            if (response.Term > CurrentTerm)
            {
                _logger.LogInformation("心跳收到更高任期 {NewTerm}，节点 {NodeId} 转为Follower",
                    response.Term, NodeId);

                lock (_stateLock)
                {
                    if (response.Term > _currentTerm)
                    {
                        _currentTerm = response.Term;
                        _votedFor = null;
                        _state = RaftNodeState.Follower;
                        _lastHeartbeat = DateTime.UtcNow;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "发送心跳到对等节点时发生错误");
        }
    }

    /// <summary>
    /// 检查候选人的日志是否至少和当前节点一样新
    /// </summary>
    private bool IsLogUpToDate(int lastLogIndex, int lastLogTerm)
    {
        if (_log.Count == 0)
            return true;

        var lastEntry = _log.Last();

        // 如果候选人最后日志条目的任期号大于接收者最后日志条目的任期号，则候选人比接收者更新
        if (lastLogTerm > lastEntry.Term)
            return true;

        // 如果任期号相同，则日志比较长的更新
        if (lastLogTerm == lastEntry.Term && lastLogIndex >= lastEntry.Index)
            return true;

        return false;
    }

    /// <summary>
    /// 获取随机选举超时时间（150-300ms）
    /// </summary>
    private int GetElectionTimeout()
    {
        return _random.Next(150, 300);
    }
}