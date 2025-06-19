using Microsoft.Extensions.Logging;
using Raft.Client;

namespace Raft.Client;

class Program
{
    static async Task Main(string[] args)
    {
        // 解析命令行参数
        if (args.Length < 1)
        {
            Console.WriteLine("使用方法: Raft.Client <集群节点列表>");
            Console.WriteLine("示例: Raft.Client localhost:5001,localhost:5002,localhost:5003");
            return;
        }

        var clusterNodes = args[0].Split(',').ToList();

        Console.WriteLine("🚀 启动Raft客户端");
        Console.WriteLine($"集群节点: [{string.Join(", ", clusterNodes)}]");

        // 创建日志记录器
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<RaftClient>();

        // 创建Raft客户端
        var client = new RaftClient(clusterNodes, logger);

        Console.WriteLine("\n📋 可用命令:");
        Console.WriteLine("  submit <命令>  - 提交命令到集群");
        Console.WriteLine("  status         - 查看集群状态");
        Console.WriteLine("  exit           - 退出客户端");
        Console.WriteLine();

        // 交互式命令循环
        while (true)
        {
            Console.Write("raft> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "submit":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("❌ 请提供要提交的命令");
                            break;
                        }
                        await HandleSubmitCommand(client, parts[1]);
                        break;

                    case "status":
                        await HandleStatusCommand(client);
                        break;

                    case "exit":
                        Console.WriteLine("👋 再见！");
                        return;

                    default:
                        Console.WriteLine("❌ 未知命令，请使用 submit、status 或 exit");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static async Task HandleSubmitCommand(RaftClient client, string command)
    {
        Console.WriteLine($"📤 提交命令: {command}");

        var result = await client.SubmitCommandAsync(command);

        if (result.Success)
        {
            Console.WriteLine($"✅ 命令执行成功");
            if (!string.IsNullOrEmpty(result.Result))
            {
                Console.WriteLine($"📋 结果: {result.Result}");
            }
        }
        else
        {
            Console.WriteLine($"❌ 命令执行失败: {result.Error}");
        }
    }

    private static async Task HandleStatusCommand(RaftClient client)
    {
        Console.WriteLine("📊 获取集群状态...");

        var status = await client.GetClusterStatusAsync();

        Console.WriteLine($"🎯 当前Leader: {status.Leader ?? "未知"}");
        Console.WriteLine("📋 节点状态:");

        foreach (var node in status.Nodes)
        {
            var statusIcon = node.IsOnline ? "🟢" : "🔴";
            var roleIcon = node.IsLeader ? "👑" : (node.State == Common.Models.RaftNodeState.Candidate ? "🗳️" : "👥");

            Console.WriteLine($"  {statusIcon} {roleIcon} {node.NodeId} - {node.State} (任期: {node.CurrentTerm})");
        }
    }
}