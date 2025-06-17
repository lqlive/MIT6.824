using MapReduce.Common.Contracts;
using MapReduce.Common.Interfaces;
using MapReduce.Common.Models;
using MapReduce.Common.Utilities;
using MapReduce.Examples;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = MapReduce.Common.Models.TaskStatus;

namespace MapReduce.Worker
{
    /// <summary>
    /// MapReduce Worker节点实现
    /// 负责向Master请求任务、执行Map/Reduce操作并报告结果
    /// </summary>
    public class MapReduceWorker
    {
        private readonly string _workerId;
        private readonly string _masterEndpoint;
        private readonly IMapReduceService _masterService;
        private readonly IMapFunction _mapFunction;
        private readonly IReduceFunction _reduceFunction;
        private readonly Timer _heartbeatTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new object();
        private readonly string _outputDirectory;

        private bool _isRunning;
        private MapReduceTask? _currentTask;
        private DateTime _lastHeartbeat;

        // 任务执行统计
        private int _completedMapTasks;
        private int _completedReduceTasks;
        private int _failedTasks;

        public MapReduceWorker(string masterEndpoint = "http://localhost:8080", string outputDirectory = "output")
        {
            _workerId = Environment.MachineName + "-" + Environment.ProcessId + "-" + DateTime.Now.Ticks;
            _masterEndpoint = masterEndpoint;
            _outputDirectory = outputDirectory;
            _cancellationTokenSource = new CancellationTokenSource();

            // 确保输出目录存在
            HashingHelper.EnsureOutputDirectoryExists(_outputDirectory);

            // 使用HTTP客户端连接Master服务
            _masterService = new HttpMasterService(_masterEndpoint);

            // 使用示例的WordCount实现
            _mapFunction = new WordCountMapFunction();
            _reduceFunction = new WordCountReduceFunction();

            // 设置心跳定时器，每3秒发送一次心跳
            _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));

            _lastHeartbeat = DateTime.UtcNow;

            Console.WriteLine($"Worker {_workerId} 已启动，连接Master: {_masterEndpoint}");
            Console.WriteLine($"输出目录: {Path.GetFullPath(_outputDirectory)}");
        }

        /// <summary>
        /// 启动Worker主循环
        /// </summary>
        public async Task StartAsync()
        {
            _isRunning = true;

            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // 向Master请求任务
                        var task = await RequestTaskFromMasterAsync();

                        if (task != null)
                        {
                            _currentTask = task;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到任务: {task.TaskType} #{task.TaskId}");

                            // 执行任务
                            bool success = await ExecuteTaskAsync(task);

                            // 报告任务完成状态
                            await ReportTaskCompletionAsync(task.TaskId, success);

                            if (success)
                            {
                                if (task.TaskType == TaskType.Map)
                                    _completedMapTasks++;
                                else
                                    _completedReduceTasks++;

                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 任务完成: {task.TaskType} #{task.TaskId}");
                            }
                            else
                            {
                                _failedTasks++;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 任务失败: {task.TaskType} #{task.TaskId}");
                            }

                            _currentTask = null;
                        }
                        else
                        {
                            // 没有可用任务，等待一段时间
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Worker执行错误: {ex.Message}");
                        await Task.Delay(2000, _cancellationTokenSource.Token);
                    }
                }
            }
            finally
            {
                Console.WriteLine($"Worker {_workerId} 已停止");
                Console.WriteLine($"统计信息 - Map任务: {_completedMapTasks}, Reduce任务: {_completedReduceTasks}, 失败: {_failedTasks}");
            }
        }

        /// <summary>
        /// 向Master请求任务
        /// </summary>
        private async Task<MapReduceTask?> RequestTaskFromMasterAsync()
        {
            try
            {
                // 这里简化实现，直接调用方法而不是通过网络
                return await _masterService.RequestTaskAsync(_workerId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 请求任务失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 执行任务
        /// </summary>
        private async Task<bool> ExecuteTaskAsync(MapReduceTask task)
        {
            try
            {
                task.Status = TaskStatus.InProgress;
                // 任务开始时间记录在AssignedTime中

                bool success = task.TaskType switch
                {
                    TaskType.Map => await ExecuteMapTaskAsync(task),
                    TaskType.Reduce => await ExecuteReduceTaskAsync(task),
                    _ => false
                };

                task.Status = success ? TaskStatus.Completed : TaskStatus.Failed;

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 执行任务异常: {ex.Message}");
                task.Status = TaskStatus.Failed;
                return false;
            }
        }

        /// <summary>
        /// 执行Map任务
        /// </summary>
        private async Task<bool> ExecuteMapTaskAsync(MapReduceTask task)
        {
            try
            {
                if (task.InputFiles == null || !task.InputFiles.Any())
                {
                    Console.WriteLine("Map任务缺少输入文件");
                    return false;
                }

                string inputFile = task.InputFiles[0];

                // 读取输入文件
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine($"输入文件不存在: {inputFile}");
                    return false;
                }

                string content = await File.ReadAllTextAsync(inputFile);

                // 调用Map函数
                var mapResults = _mapFunction.Map(inputFile, content);

                // 按reduce分区对结果进行分组
                var partitions = new Dictionary<int, List<KeyValuePair<string, string>>>();

                foreach (var kv in mapResults)
                {
                    int partition = HashingHelper.HashKey(kv.Key, task.ReduceCount);

                    if (!partitions.ContainsKey(partition))
                        partitions[partition] = new List<KeyValuePair<string, string>>();

                    partitions[partition].Add(kv);
                }

                // 写入中间文件到输出目录
                var outputFiles = new List<string>();
                foreach (var partition in partitions)
                {
                    string outputFile = HashingHelper.GetIntermediateFileName(task.TaskId, partition.Key, _outputDirectory);
                    outputFiles.Add(outputFile);

                    var lines = partition.Value.Select(kv => $"{kv.Key}\t{kv.Value}");
                    await File.WriteAllLinesAsync(outputFile, lines);
                }

                task.IntermediateFiles = outputFiles;

                Console.WriteLine($"Map任务 #{task.TaskId} 生成了 {outputFiles.Count} 个中间文件");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行Map任务失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行Reduce任务
        /// </summary>
        private async Task<bool> ExecuteReduceTaskAsync(MapReduceTask task)
        {
            try
            {
                if (task.IntermediateFiles == null || !task.IntermediateFiles.Any())
                {
                    Console.WriteLine("Reduce任务缺少中间文件");
                    return false;
                }

                // 读取所有中间文件并合并相同key的值
                var keyValues = new Dictionary<string, List<string>>();

                foreach (string inputFile in task.IntermediateFiles)
                {
                    if (!File.Exists(inputFile))
                    {
                        Console.WriteLine($"中间文件不存在: {inputFile}");
                        continue;
                    }

                    var lines = await File.ReadAllLinesAsync(inputFile);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var parts = line.Split('\t', 2);
                        if (parts.Length != 2)
                            continue;

                        string key = parts[0];
                        string value = parts[1];

                        if (!keyValues.ContainsKey(key))
                            keyValues[key] = new List<string>();

                        keyValues[key].Add(value);
                    }
                }

                // 对每个key调用Reduce函数
                var results = new List<string>();
                foreach (var kvp in keyValues.OrderBy(x => x.Key))
                {
                    string result = _reduceFunction.Reduce(kvp.Key, kvp.Value);
                    if (!string.IsNullOrEmpty(result))
                        results.Add(result);
                }

                // 写入最终输出文件
                string outputFile = HashingHelper.GetOutputFileName(task.ReduceIndex, _outputDirectory);
                await File.WriteAllLinesAsync(outputFile, results);

                task.OutputFile = outputFile;

                Console.WriteLine($"Reduce任务 #{task.TaskId} 处理了 {keyValues.Count} 个唯一键，输出到: {outputFile}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行Reduce任务失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 报告任务完成状态
        /// </summary>
        private async Task ReportTaskCompletionAsync(int taskId, bool success)
        {
            try
            {
                // 这里简化实现，直接调用方法而不是通过网络
                string[] outputFiles = Array.Empty<string>();

                if (_currentTask != null)
                {
                    if (_currentTask.TaskType == TaskType.Map)
                        outputFiles = _currentTask.IntermediateFiles?.ToArray() ?? Array.Empty<string>();
                    else if (_currentTask.TaskType == TaskType.Reduce && !string.IsNullOrEmpty(_currentTask.OutputFile))
                        outputFiles = new[] { _currentTask.OutputFile };
                }

                await _masterService.ReportTaskCompletionAsync(_workerId, taskId, success, outputFiles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 报告任务完成失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        private async void SendHeartbeat(object? state)
        {
            if (!_isRunning)
                return;

            try
            {
                lock (_lockObject)
                {
                    _lastHeartbeat = DateTime.UtcNow;
                }
                // 这里简化实现，直接调用方法而不是通过网络
                await _masterService.SendHeartbeatAsync(_workerId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 心跳发送失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止Worker
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            _heartbeatTimer?.Dispose();
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();

            // 释放HTTP客户端
            if (_masterService is HttpMasterService httpService)
            {
                httpService.Dispose();
            }
        }
    }

    /// <summary>
    /// HTTP客户端实现，通过网络连接Master服务
    /// </summary>
    public class HttpMasterService : IMapReduceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public HttpMasterService(string masterEndpoint)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _baseUrl = masterEndpoint.TrimEnd('/');
        }

        public async Task<MasterStatus> GetMasterStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/status");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<MasterStatus>(json) ?? new MasterStatus();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取Master状态失败: {ex.Message}");
            }

            // 返回默认状态
            return new MasterStatus
            {
                CurrentPhase = "Unknown",
                CompletedMapTasks = 0,
                CompletedReduceTasks = 0,
                ActiveWorkers = new List<string>(),
                TotalMapTasks = 0,
                TotalReduceTasks = 0,
                IsCompleted = false
            };
        }

        public async Task<bool> IsAllTasksCompletedAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/completed");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return bool.Parse(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查任务完成状态失败: {ex.Message}");
            }
            return false;
        }

        public async Task ReportTaskCompletionAsync(string workerId, int taskId, bool success, string[] outputFiles)
        {
            try
            {
                var data = new
                {
                    WorkerId = workerId,
                    TaskId = taskId,
                    Success = success,
                    OutputFiles = outputFiles
                };

                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/report", content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"报告任务完成失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"报告任务完成异常: {ex.Message}");
            }
        }

        public async Task<MapReduceTask?> RequestTaskAsync(string workerId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/task?workerId={Uri.EscapeDataString(workerId)}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json) && json != "null")
                    {
                        return System.Text.Json.JsonSerializer.Deserialize<MapReduceTask>(json);
                    }
                }
                else if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    Console.WriteLine($"请求任务失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"请求任务异常: {ex.Message}");
            }
            return null;
        }

        public async Task SendHeartbeatAsync(string workerId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/heartbeat?workerId={Uri.EscapeDataString(workerId)}", null);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"发送心跳失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送心跳异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}