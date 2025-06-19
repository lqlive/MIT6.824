using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raft.Server;

namespace Raft.Server;

class Program
{
    static async Task Main(string[] args)
    {
        // 解析命令行参数
        if (args.Length < 2)
        {
            Console.WriteLine("使用方法: Raft.Server <节点ID> <集群节点列表>");
            Console.WriteLine("示例: Raft.Server node1 node1,node2,node3");
            return;
        }

        var nodeId = args[0];
        var allNodes = args[1].Split(',').ToList();
        var peers = allNodes.Where(n => n != nodeId).ToList();

        Console.WriteLine($"启动Raft节点: {nodeId}");
        Console.WriteLine($"集群节点: [{string.Join(", ", allNodes)}]");
        Console.WriteLine($"其他节点: [{string.Join(", ", peers)}]");

        // 创建主机
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddSingleton<RaftNode>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<RaftNode>>();
                    return new RaftNode(nodeId, peers, logger);
                });

                services.AddHostedService<RaftHostedService>();
            })
            .Build();

        // 运行主机
        await host.RunAsync();
    }
}

/// <summary>
/// Raft节点托管服务
/// </summary>
public class RaftHostedService : BackgroundService
{
    private readonly RaftNode _raftNode;
    private readonly ILogger<RaftHostedService> _logger;

    public RaftHostedService(RaftNode raftNode, ILogger<RaftHostedService> logger)
    {
        _raftNode = raftNode;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("启动Raft节点 {NodeId}", _raftNode.NodeId);

        await _raftNode.StartAsync(stoppingToken);

        // 等待取消
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);

            // 定期输出节点状态
            _logger.LogInformation("节点状态: {NodeId} - {State} - 任期 {Term}",
                _raftNode.NodeId, _raftNode.State, _raftNode.CurrentTerm);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止Raft节点 {NodeId}", _raftNode.NodeId);
        await _raftNode.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}