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

        lock (_stateLock)
        {
            _state = RaftNodeState.Candidate;
            _currentTerm++;
            _votedFor = NodeId; // 给自己投票
            _lastHeartbeat = DateTime.UtcNow;
        }

        // TODO: 实现选举逻辑
        // 1. 向所有其他节点发送RequestVote RPC
        // 2. 统计选票
        // 3. 如果获得多数选票，成为Leader
        // 这将在完整实现中补充

        await Task.CompletedTask;
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