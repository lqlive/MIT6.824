using Microsoft.Extensions.Logging;
using Raft.Server;
using Raft.Common.Models;
using Xunit;
using Xunit.Abstractions;

namespace Raft.Tests;

public class RaftNodeTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<RaftNode> _logger;

    public RaftNodeTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger<RaftNode>(output);
    }

    [Fact]
    public void Constructor_ShouldInitializeNodeCorrectly()
    {
        // Arrange
        var nodeId = "node1";
        var peers = new List<string> { "node2", "node3" };

        // Act
        var node = new RaftNode(nodeId, peers, _logger);

        // Assert
        Assert.Equal(nodeId, node.NodeId);
        Assert.Equal(RaftNodeState.Follower, node.State);
        Assert.Equal(0, node.CurrentTerm);
        Assert.False(node.IsLeader);
    }

    [Fact]
    public async Task RequestVote_WithLowerTerm_ShouldRejectVote()
    {
        // Arrange
        var nodeId = "node1";
        var peers = new List<string> { "node2", "node3" };
        var node = new RaftNode(nodeId, peers, _logger);

        // 先让节点进入任期2，然后测试任期1的请求应该被拒绝
        var higherTermRequest = new VoteRequest
        {
            Term = 2,
            CandidateId = "node3",
            LastLogIndex = 0,
            LastLogTerm = 0
        };
        await node.RequestVoteAsync(higherTermRequest); // 这会使节点进入任期2

        var request = new VoteRequest
        {
            Term = 1, // 低于当前任期（2）
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await node.RequestVoteAsync(request);

        // Assert
        Assert.False(response.VoteGranted);
        Assert.Equal(2, response.Term); // 当前任期应该是2
    }

    [Fact]
    public async Task RequestVote_WithHigherTerm_ShouldGrantVote()
    {
        // Arrange
        var nodeId = "node1";
        var peers = new List<string> { "node2", "node3" };
        var node = new RaftNode(nodeId, peers, _logger);

        var request = new VoteRequest
        {
            Term = 1, // 高于当前任期（0）
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await node.RequestVoteAsync(request);

        // Assert
        Assert.True(response.VoteGranted);
        Assert.Equal(1, response.Term);
        Assert.Equal(RaftNodeState.Follower, node.State);
    }

    [Fact]
    public async Task AppendEntries_WithLowerTerm_ShouldReject()
    {
        // Arrange
        var nodeId = "node1";
        var peers = new List<string> { "node2", "node3" };
        var node = new RaftNode(nodeId, peers, _logger);

        var request = new AppendEntriesRequest
        {
            Term = -1, // 低于当前任期
            LeaderId = "node2",
            PrevLogIndex = -1,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0
        };

        // Act
        var response = await node.AppendEntriesAsync(request);

        // Assert
        Assert.False(response.Success);
        Assert.Equal(0, response.Term);
    }

    [Fact]
    public async Task AppendEntries_WithValidTerm_ShouldAccept()
    {
        // Arrange
        var nodeId = "node1";
        var peers = new List<string> { "node2", "node3" };
        var node = new RaftNode(nodeId, peers, _logger);

        var request = new AppendEntriesRequest
        {
            Term = 1, // 等于或高于当前任期
            LeaderId = "node2",
            PrevLogIndex = -1,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0
        };

        // Act
        var response = await node.AppendEntriesAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(1, response.Term);
        Assert.Equal(RaftNodeState.Follower, node.State);
    }
}

/// <summary>
/// 测试用的Logger实现
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable BeginScope<TState>(TState state) => null!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}