using Microsoft.Extensions.Logging;
using Raft.Common.Models;
using System.Text.Json;

namespace Raft.Client;

/// <summary>
/// Raft 客户端实现
/// 用于与Raft集群进行交互
/// </summary>
public class RaftClient
{
    private readonly List<string> _clusterNodes;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RaftClient> _logger;
    private string? _currentLeader;

    public RaftClient(List<string> clusterNodes, ILogger<RaftClient> logger, HttpClient? httpClient = null)
    {
        _clusterNodes = clusterNodes ?? throw new ArgumentNullException(nameof(clusterNodes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();

        _logger.LogInformation("初始化Raft客户端，集群节点: [{Nodes}]", string.Join(", ", _clusterNodes));
    }

    /// <summary>
    /// 提交命令到Raft集群
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令执行结果</returns>
    public async Task<CommandResult> SubmitCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("提交命令: {Command}", command);

        // 如果不知道当前Leader，尝试发现Leader
        if (_currentLeader == null)
        {
            var leader = await FindLeaderAsync(cancellationToken);
            if (leader == null)
            {
                _logger.LogError("无法找到集群Leader");
                return new CommandResult
                {
                    Success = false,
                    Error = "无法找到集群Leader"
                };
            }
            _currentLeader = leader;
        }

        // 尝试向当前Leader提交命令
        var result = await TrySubmitToNodeAsync(_currentLeader, command, cancellationToken);

        // 如果失败且是因为不是Leader，重新寻找Leader
        if (!result.Success && result.Error?.Contains("not leader") == true)
        {
            _logger.LogWarning("当前节点 {Node} 不再是Leader，重新寻找Leader", _currentLeader);
            _currentLeader = null;

            var newLeader = await FindLeaderAsync(cancellationToken);
            if (newLeader != null)
            {
                _currentLeader = newLeader;
                result = await TrySubmitToNodeAsync(_currentLeader, command, cancellationToken);
            }
        }

        return result;
    }

    /// <summary>
    /// 查询集群状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>集群状态信息</returns>
    public async Task<ClusterStatus> GetClusterStatusAsync(CancellationToken cancellationToken = default)
    {
        var clusterStatus = new ClusterStatus
        {
            Nodes = new List<NodeStatus>()
        };

        foreach (var node in _clusterNodes)
        {
            try
            {
                var nodeStatus = await GetNodeStatusAsync(node, cancellationToken);
                clusterStatus.Nodes.Add(nodeStatus);

                if (nodeStatus.IsLeader)
                {
                    clusterStatus.Leader = node;
                    _currentLeader = node;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法获取节点 {Node} 的状态", node);
                clusterStatus.Nodes.Add(new NodeStatus
                {
                    NodeId = node,
                    IsOnline = false
                });
            }
        }

        return clusterStatus;
    }

    /// <summary>
    /// 发现集群中的Leader节点
    /// </summary>
    private async Task<string?> FindLeaderAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在寻找集群Leader...");

        foreach (var node in _clusterNodes)
        {
            try
            {
                var nodeStatus = await GetNodeStatusAsync(node, cancellationToken);
                if (nodeStatus.IsLeader)
                {
                    _logger.LogInformation("发现Leader节点: {Node}", node);
                    return node;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "无法连接到节点 {Node}", node);
            }
        }

        _logger.LogWarning("未找到Leader节点");
        return null;
    }

    /// <summary>
    /// 尝试向指定节点提交命令
    /// </summary>
    private async Task<CommandResult> TrySubmitToNodeAsync(string nodeId, string command, CancellationToken cancellationToken)
    {
        try
        {
            var request = new SubmitCommandRequest { Command = command };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"http://{nodeId}/raft/command",
                content,
                cancellationToken);

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<CommandResult>(responseJson);
                return result ?? new CommandResult { Success = false, Error = "响应解析失败" };
            }
            else
            {
                return new CommandResult
                {
                    Success = false,
                    Error = $"HTTP {response.StatusCode}: {responseJson}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向节点 {Node} 提交命令失败", nodeId);
            return new CommandResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// 获取节点状态
    /// </summary>
    private async Task<NodeStatus> GetNodeStatusAsync(string nodeId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"http://{nodeId}/raft/status",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = JsonSerializer.Deserialize<NodeStatus>(json);

        return status ?? new NodeStatus { NodeId = nodeId, IsOnline = false };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// 命令提交请求
/// </summary>
public class SubmitCommandRequest
{
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// 命令执行结果
/// </summary>
public class CommandResult
{
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 集群状态
/// </summary>
public class ClusterStatus
{
    public string? Leader { get; set; }
    public List<NodeStatus> Nodes { get; set; } = new();
}

/// <summary>
/// 节点状态
/// </summary>
public class NodeStatus
{
    public string NodeId { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public bool IsOnline { get; set; } = true;
    public int CurrentTerm { get; set; }
    public RaftNodeState State { get; set; }
}