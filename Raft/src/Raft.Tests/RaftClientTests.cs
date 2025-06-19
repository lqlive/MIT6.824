using Microsoft.Extensions.Logging;
using Raft.Client;
using Raft.Common.Models;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Raft.Tests;

public class RaftClientTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;

    public RaftClientTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new XunitLoggerProvider(output))
                   .SetMinimumLevel(LogLevel.Debug));
    }

    [Fact]
    public void Constructor_ShouldInitializeClientCorrectly()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<RaftClient>();
        var serverEndpoints = new List<string> { "http://localhost:8080", "http://localhost:8081" };

        // Act
        var client = new RaftClient(serverEndpoints, logger);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task SubmitCommandAsync_WithNoLeader_ShouldReturnFailure()
    {
        // Arrange
        var clusterNodes = new List<string> { "nonexistent:5001" };
        var httpClient = new HttpClient(new MockHttpMessageHandler());
        var client = new RaftClient(clusterNodes, _loggerFactory.CreateLogger<RaftClient>(), httpClient);

        // Act
        var result = await client.SubmitCommandAsync("test command");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("无法找到集群Leader", result.Error);
    }

    [Fact]
    public async Task GetClusterStatusAsync_WithOfflineNodes_ShouldHandleGracefully()
    {
        // Arrange
        var clusterNodes = new List<string> { "offline1:5001", "offline2:5002" };
        var httpClient = new HttpClient(new MockHttpMessageHandler());
        var client = new RaftClient(clusterNodes, _loggerFactory.CreateLogger<RaftClient>(), httpClient);

        // Act
        var status = await client.GetClusterStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(2, status.Nodes.Count);
        Assert.All(status.Nodes, node => Assert.False(node.IsOnline));
    }
}

/// <summary>
/// 模拟HTTP消息处理器，用于测试
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 模拟所有请求都失败（网络不可达）
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("Service unavailable", Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}