using Microsoft.Extensions.Logging;
using Raft.Common.Interfaces;
using Raft.Common.Models;
using Raft.Server;
using Xunit;
using Xunit.Abstractions;

namespace Raft.Tests;

/// <summary>
/// Raft节点测试类，主要测试Leader选举功能
/// </summary>
public class RaftNodeTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;

    public RaftNodeTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new XunitLoggerProvider(output))
                   .SetMinimumLevel(LogLevel.Debug));
    }

    /// <summary>
    /// 测试单节点自动成为Leader
    /// </summary>
    [Fact]
    public async Task SingleNode_ShouldBecomeLeader()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftNode>();
        var node = new RaftNode("node1", new List<string>(), logger);

        // Act
        await node.StartAsync();

        // 等待选举超时
        await Task.Delay(400);

        // Assert
        Assert.Equal(RaftNodeState.Leader, node.State);
        Assert.True(node.CurrentTerm > 0);

        await node.StopAsync();
    }

    /// <summary>
    /// 测试三节点集群的Leader选举
    /// </summary>
    [Fact]
    public async Task ThreeNodeCluster_ShouldElectOneLeader()
    {
        // Arrange
        var peers = new List<string> { "node2", "node3" };
        var node1 = new RaftNode("node1", peers, _loggerFactory.CreateLogger<RaftNode>());
        var node2 = new RaftNode("node2", new List<string> { "node1", "node3" }, _loggerFactory.CreateLogger<RaftNode>());
        var node3 = new RaftNode("node3", new List<string> { "node1", "node2" }, _loggerFactory.CreateLogger<RaftNode>());

        // 设置节点间的通信
        var nodeMap = new Dictionary<string, RaftNode>
        {
            { "node1", node1 },
            { "node2", node2 },
            { "node3", node3 }
        };

        // 为每个节点设置对等节点引用
        foreach (var node in nodeMap.Values)
        {
            var peerNodes = nodeMap
                .Where(kv => kv.Key != node.NodeId)
                .ToDictionary(kv => kv.Key, kv => (IRaftNode)kv.Value);
            node.SetPeerNodes(peerNodes);
        }

        // Act
        await Task.WhenAll(
            node1.StartAsync(),
            node2.StartAsync(),
            node3.StartAsync()
        );

        // 等待选举完成
        await Task.Delay(1000);

        // Assert
        var leaders = nodeMap.Values.Where(n => n.IsLeader).ToList();
        var followers = nodeMap.Values.Where(n => n.State == RaftNodeState.Follower).ToList();

        Assert.Single(leaders); // 应该只有一个Leader
        Assert.Equal(2, followers.Count); // 应该有两个Follower

        _output.WriteLine($"Leader: {leaders.First().NodeId}");
        _output.WriteLine($"Followers: {string.Join(", ", followers.Select(f => f.NodeId))}");

        // 所有节点应该在同一个任期
        var terms = nodeMap.Values.Select(n => n.CurrentTerm).Distinct().ToList();
        Assert.Single(terms);

        // Clean up
        await Task.WhenAll(
            node1.StopAsync(),
            node2.StopAsync(),
            node3.StopAsync()
        );
    }

    /// <summary>
    /// 测试投票请求处理
    /// </summary>
    [Fact]
    public async Task RequestVote_ShouldGrantVoteForFirstCandidate()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftNode>();
        var node = new RaftNode("node1", new List<string> { "node2" }, logger);

        var voteRequest = new VoteRequest
        {
            Term = 1,
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await node.RequestVoteAsync(voteRequest);

        // Assert
        Assert.True(response.VoteGranted);
        Assert.Equal(1, response.Term);

        // 第二次相同的请求应该也被批准（因为是同一个候选人）
        var response2 = await node.RequestVoteAsync(voteRequest);
        Assert.True(response2.VoteGranted);

        // 不同候选人的请求应该被拒绝
        var voteRequest2 = new VoteRequest
        {
            Term = 1,
            CandidateId = "node3",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        var response3 = await node.RequestVoteAsync(voteRequest2);
        Assert.False(response3.VoteGranted);
    }

    /// <summary>
    /// 测试更高任期的投票请求
    /// </summary>
    [Fact]
    public async Task RequestVote_HigherTerm_ShouldUpdateTermAndGrantVote()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftNode>();
        var node = new RaftNode("node1", new List<string> { "node2" }, logger);

        // 先发送一个任期1的请求
        var voteRequest1 = new VoteRequest
        {
            Term = 1,
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        await node.RequestVoteAsync(voteRequest1);
        Assert.Equal(1, node.CurrentTerm);

        // 再发送一个任期2的请求
        var voteRequest2 = new VoteRequest
        {
            Term = 2,
            CandidateId = "node3",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await node.RequestVoteAsync(voteRequest2);

        // Assert
        Assert.True(response.VoteGranted);
        Assert.Equal(2, response.Term);
        Assert.Equal(2, node.CurrentTerm);
        Assert.Equal(RaftNodeState.Follower, node.State);
    }

    /// <summary>
    /// 测试过期任期的投票请求
    /// </summary>
    [Fact]
    public async Task RequestVote_LowerTerm_ShouldRejectVote()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftNode>();
        var node = new RaftNode("node1", new List<string> { "node2" }, logger);

        // 先发送一个任期2的请求以提高节点的当前任期
        var voteRequest1 = new VoteRequest
        {
            Term = 2,
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        await node.RequestVoteAsync(voteRequest1);

        // 再发送一个任期1的请求（过期任期）
        var voteRequest2 = new VoteRequest
        {
            Term = 1,
            CandidateId = "node3",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await node.RequestVoteAsync(voteRequest2);

        // Assert
        Assert.False(response.VoteGranted);
        Assert.Equal(2, response.Term); // 返回当前任期
        Assert.Equal(2, node.CurrentTerm); // 当前任期不变
    }

    /// <summary>
    /// 测试心跳处理
    /// </summary>
    [Fact]
    public async Task AppendEntries_ValidHeartbeat_ShouldAccept()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftNode>();
        var node = new RaftNode("node1", new List<string> { "node2" }, logger);

        var appendRequest = new AppendEntriesRequest
        {
            Term = 1,
            LeaderId = "node2",
            Entries = new List<LogEntry>(),
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            LeaderCommit = 0
        };

        // Act
        var response = await node.AppendEntriesAsync(appendRequest);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(1, response.Term);
        Assert.Equal(1, node.CurrentTerm);
        Assert.Equal(RaftNodeState.Follower, node.State);
    }

    /// <summary>
    /// 测试过期任期的心跳
    /// </summary>
    [Fact]
    public async Task AppendEntries_LowerTerm_ShouldReject()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftNode>();
        var node = new RaftNode("node1", new List<string> { "node2" }, logger);

        // 先提高节点的当前任期
        await node.RequestVoteAsync(new VoteRequest
        {
            Term = 2,
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        });

        var appendRequest = new AppendEntriesRequest
        {
            Term = 1, // 低于当前任期
            LeaderId = "node3",
            Entries = new List<LogEntry>(),
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            LeaderCommit = 0
        };

        // Act
        var response = await node.AppendEntriesAsync(appendRequest);

        // Assert
        Assert.False(response.Success);
        Assert.Equal(2, response.Term);
        Assert.Equal(2, node.CurrentTerm);
    }
}

/// <summary>
/// 用于测试的日志提供程序
/// </summary>
public class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// 用于测试的日志记录器
/// </summary>
public class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {_categoryName}: {message}");

            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
        catch
        {
            // 忽略日志错误，避免影响测试
        }
    }
}