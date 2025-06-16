using MapReduce.Worker;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapReduce.Worker
{
    /// <summary>
    /// MapReduce Worker程序入口点
    /// </summary>
    class Program
    {
        private static MapReduceWorker? _worker;
        private static readonly CancellationTokenSource _cancellationTokenSource = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== MapReduce Worker 节点 ===");
            Console.WriteLine($"启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();

            try
            {
                // 解析命令行参数
                string masterEndpoint = "http://localhost:8080";

                if (args.Length > 0)
                {
                    masterEndpoint = args[0];
                }

                Console.WriteLine($"Master端点: {masterEndpoint}");
                Console.WriteLine();

                // 设置控制台取消处理
                Console.CancelKeyPress += OnCancelKeyPress;

                // 创建并启动Worker
                _worker = new MapReduceWorker(masterEndpoint);

                Console.WriteLine("Worker已启动，按 Ctrl+C 停止...");
                Console.WriteLine("========================================");
                Console.WriteLine();

                // 启动Worker主循环
                await _worker.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker启动失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                Environment.Exit(1);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("Worker已停止");
                Console.WriteLine($"停止时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        /// <summary>
        /// 处理Ctrl+C信号，优雅关闭Worker
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine();
            Console.WriteLine("正在停止Worker...");

            e.Cancel = true; // 防止立即退出

            try
            {
                _worker?.Stop();
                _cancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止Worker时发生错误: {ex.Message}");
            }
        }
    }
}