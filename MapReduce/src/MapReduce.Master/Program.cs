using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MapReduce.Master;
using MapReduce.Common.Contracts;

namespace MapReduce.Master
{
    class Program
    {
        private static System.Net.HttpListener? _httpListener;
        private static MapReduceMaster? _master;

        static void Main(string[] args)
        {
            Console.WriteLine("=== MapReduce Master 节点 ===");

            // 解析命令行参数
            if (args.Length < 2)
            {
                Console.WriteLine("用法: MapReduce.Master.exe <输入文件目录> <Reduce任务数>");
                Console.WriteLine("示例: MapReduce.Master.exe testassets 3");
                Console.WriteLine("注意: 请在MapReduce根目录下运行，testassets文件夹应位于项目根目录");
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
            _master = new MapReduceMaster(inputFiles, reduceCount);

            try
            {
                // 启动WCF服务
                StartWcfService();

                Console.WriteLine("Master已启动，等待Worker连接...");
                Console.WriteLine("WCF服务地址: http://localhost:8080/MapReduceService");
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
                    ShowMasterStatus(_master).Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动Master服务失败: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("正在关闭Master服务...");
                StopWcfService();
                _master?.Dispose();
            }
        }

        /// <summary>
        /// 启动WCF服务
        /// </summary>
        private static void StartWcfService()
        {
            try
            {
                // 创建HTTP监听器
                _httpListener = new System.Net.HttpListener();
                _httpListener.Prefixes.Add("http://localhost:8080/");
                _httpListener.Start();

                Console.WriteLine("✅ HTTP服务已启动!");
                Console.WriteLine("📡 监听地址: http://localhost:8080/");
                Console.WriteLine("💡 注意: 这是简化的HTTP服务，真正的WCF需要额外配置");

                // 在后台线程处理请求
                Task.Run(() => ProcessHttpRequests());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动HTTP服务失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部异常: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// 处理HTTP请求（支持Worker API）
        /// </summary>
        private static async Task ProcessHttpRequests()
        {
            while (_httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    // 设置CORS头部
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    string path = request.Url?.AbsolutePath ?? "/";
                    string method = request.HttpMethod;

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {method} {path}");

                    try
                    {
                        if (method == "GET" && path == "/")
                        {
                            await HandleStatusPage(response);
                        }
                        else if (method == "GET" && path == "/status")
                        {
                            await HandleGetStatus(response);
                        }
                        else if (method == "GET" && path == "/task")
                        {
                            string? workerId = request.QueryString["workerId"];
                            await HandleRequestTask(response, workerId);
                        }
                        else if (method == "POST" && path == "/report")
                        {
                            await HandleReportTask(request, response);
                        }
                        else if (method == "POST" && path == "/heartbeat")
                        {
                            string? workerId = request.QueryString["workerId"];
                            await HandleHeartbeat(response, workerId);
                        }
                        else if (method == "GET" && path == "/completed")
                        {
                            await HandleIsCompleted(response);
                        }
                        else
                        {
                            response.StatusCode = 404;
                            await WriteResponse(response, "Not Found");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理请求 {path} 错误: {ex.Message}");
                        response.StatusCode = 500;
                        await WriteResponse(response, $"Internal Server Error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理HTTP请求错误: {ex.Message}");
                }
            }
        }

        private static async Task HandleStatusPage(System.Net.HttpListenerResponse response)
        {
            var status = await _master!.GetMasterStatusAsync();
            string html = $@"
<!DOCTYPE html>
<html>
<head><title>MapReduce Master</title></head>
<body>
<h1>MapReduce Master Service</h1>
<p><strong>Status:</strong> Running</p>
<p><strong>Phase:</strong> {status.CurrentPhase}</p>
<p><strong>Map Tasks:</strong> {status.CompletedMapTasks}/{status.TotalMapTasks}</p>
<p><strong>Reduce Tasks:</strong> {status.CompletedReduceTasks}/{status.TotalReduceTasks}</p>
<p><strong>Active Workers:</strong> {status.ActiveWorkers.Count}</p>
<p><strong>Completed:</strong> {(status.IsCompleted ? "Yes" : "No")}</p>
</body>
</html>";
            response.ContentType = "text/html";
            await WriteResponse(response, html);
        }

        private static async Task HandleGetStatus(System.Net.HttpListenerResponse response)
        {
            var status = await _master!.GetMasterStatusAsync();
            string json = System.Text.Json.JsonSerializer.Serialize(status);
            response.ContentType = "application/json";
            await WriteResponse(response, json);
        }

        private static async Task HandleRequestTask(System.Net.HttpListenerResponse response, string? workerId)
        {
            if (string.IsNullOrEmpty(workerId))
            {
                response.StatusCode = 400;
                await WriteResponse(response, "WorkerId is required");
                return;
            }

            var task = await _master!.RequestTaskAsync(workerId);
            if (task != null)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(task);
                response.ContentType = "application/json";
                await WriteResponse(response, json);
            }
            else
            {
                response.StatusCode = 204; // No Content
                response.Close();
            }
        }

        private static async Task HandleReportTask(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            try
            {
                using var reader = new System.IO.StreamReader(request.InputStream);
                string json = await reader.ReadToEndAsync();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到报告数据: {json}");

                // 使用JsonDocument来正确处理JSON
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("WorkerId", out var workerIdElement) &&
                    root.TryGetProperty("TaskId", out var taskIdElement) &&
                    root.TryGetProperty("Success", out var successElement) &&
                    root.TryGetProperty("OutputFiles", out var outputFilesElement))
                {
                    string workerId = workerIdElement.GetString() ?? "";
                    int taskId = taskIdElement.GetInt32();
                    bool success = successElement.GetBoolean();

                    // 处理输出文件数组
                    string[] outputFiles = Array.Empty<string>();
                    if (outputFilesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var fileList = new List<string>();
                        foreach (var fileElement in outputFilesElement.EnumerateArray())
                        {
                            var fileName = fileElement.GetString();
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                fileList.Add(fileName);
                            }
                        }
                        outputFiles = fileList.ToArray();
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 解析报告: WorkerId={workerId}, TaskId={taskId}, Success={success}, Files={outputFiles.Length}");

                    await _master!.ReportTaskCompletionAsync(workerId, taskId, success, outputFiles);
                    await WriteResponse(response, "OK");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 报告数据格式错误: 缺少必要字段");
                    response.StatusCode = 400;
                    await WriteResponse(response, "Invalid request data: missing required fields");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 处理报告请求异常: {ex.Message}");
                response.StatusCode = 500;
                await WriteResponse(response, $"Internal Server Error: {ex.Message}");
            }
        }

        private static async Task HandleHeartbeat(System.Net.HttpListenerResponse response, string? workerId)
        {
            if (string.IsNullOrEmpty(workerId))
            {
                response.StatusCode = 400;
                await WriteResponse(response, "WorkerId is required");
                return;
            }

            await _master!.SendHeartbeatAsync(workerId);
            await WriteResponse(response, "OK");
        }

        private static async Task HandleIsCompleted(System.Net.HttpListenerResponse response)
        {
            bool completed = await _master!.IsAllTasksCompletedAsync();
            await WriteResponse(response, completed.ToString().ToLower());
        }

        private static async Task WriteResponse(System.Net.HttpListenerResponse response, string text)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(text);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// 停止WCF服务
        /// </summary>
        private static void StopWcfService()
        {
            try
            {
                if (_httpListener != null)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    _httpListener = null;
                    Console.WriteLine("✅ HTTP服务已关闭");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 关闭HTTP服务时发生错误: {ex.Message}");
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
            Console.WriteLine($"🔗 服务地址: http://localhost:8080/");
            Console.WriteLine($"📊 当前阶段: {status.CurrentPhase}");
            Console.WriteLine($"📋 Map任务: {status.CompletedMapTasks}/{status.TotalMapTasks} 完成");
            Console.WriteLine($"🔄 Reduce任务: {status.CompletedReduceTasks}/{status.TotalReduceTasks} 完成");
            Console.WriteLine($"👥 活跃Worker数: {status.ActiveWorkers.Count}");

            if (status.ActiveWorkers.Any())
            {
                Console.WriteLine("活跃Workers:");
                foreach (var worker in status.ActiveWorkers)
                {
                    Console.WriteLine($"  🟢 {worker}");
                }
            }

            Console.WriteLine($"📈 作业状态: {(status.IsCompleted ? "✅ 已完成" : "🔄 进行中")}");
            Console.WriteLine();
            Console.WriteLine("按任意键刷新状态，输入 'q' 退出");
        }
    }
}