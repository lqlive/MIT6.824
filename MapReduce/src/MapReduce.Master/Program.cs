using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MapReduce.Master;

namespace MapReduce.Master
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== MapReduce Master 节点 ===");

            // 解析命令行参数
            if (args.Length < 2)
            {
                Console.WriteLine("用法: MapReduce.Master.exe <输入文件目录> <Reduce任务数>");
                Console.WriteLine("示例: MapReduce.Master.exe ./input 3");
                return;
            }

            string inputDirectory = args[0];
            if (!int.TryParse(args[1], out int reduceCount) || reduceCount <= 0)
            {
                Console.WriteLine("错误: Reduce任务数必须是正整数");
                return;
            }

            // 获取输入文件列表
            if (!Directory.Exists(inputDirectory))
            {
                Console.WriteLine($"错误: 输入目录不存在: {inputDirectory}");
                return;
            }

            var inputFiles = Directory.GetFiles(inputDirectory, "*.txt");
            if (inputFiles.Length == 0)
            {
                Console.WriteLine($"错误: 在目录 {inputDirectory} 中没有找到 .txt 文件");
                return;
            }

            Console.WriteLine($"找到 {inputFiles.Length} 个输入文件，{reduceCount} 个Reduce任务");

            // 创建Master实例
            var master = new MapReduceMaster(inputFiles, reduceCount);

            try
            {
                Console.WriteLine("Master已启动，等待Worker连接...");
                Console.WriteLine("注意: 这是简化版本，Worker将通过直接调用Master方法来通信");
                Console.WriteLine("按任意键查看状态，输入 'q' 退出");

                // 控制台交互循环
                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        break;
                    }

                    // 显示当前状态
                    ShowMasterStatus(master).Wait();
                }
            }
            finally
            {
                Console.WriteLine("正在关闭Master服务...");
                master?.Dispose();
            }
        }

        /// <summary>
        /// 显示Master状态
        /// </summary>
        private static async Task ShowMasterStatus(MapReduceMaster master)
        {
            var status = await master.GetMasterStatusAsync();

            Console.Clear();
            Console.WriteLine("=== MapReduce Master 状态 ===");
            Console.WriteLine($"当前阶段: {status.CurrentPhase}");
            Console.WriteLine($"Map任务: {status.CompletedMapTasks}/{status.TotalMapTasks} 完成");
            Console.WriteLine($"Reduce任务: {status.CompletedReduceTasks}/{status.TotalReduceTasks} 完成");
            Console.WriteLine($"活跃Worker数: {status.ActiveWorkers.Count}");

            if (status.ActiveWorkers.Any())
            {
                Console.WriteLine("活跃Workers:");
                foreach (var worker in status.ActiveWorkers)
                {
                    Console.WriteLine($"  - {worker}");
                }
            }

            Console.WriteLine($"作业状态: {(status.IsCompleted ? "已完成" : "进行中")}");
            Console.WriteLine();
            Console.WriteLine("按任意键刷新状态，输入 'q' 退出");
        }
    }
}