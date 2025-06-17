using MapReduce.Master;
using MapReduce.Worker;
using MapReduce.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MapReduce.Tests
{
    /// <summary>
    /// MapReduce端到端集成测试
    /// </summary>
    public class EndToEndTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDirectory;
        private readonly List<string> _createdFiles;
        private readonly List<string> _createdDirectories;

        public EndToEndTests(ITestOutputHelper output)
        {
            _output = output;
            _testDirectory = Path.Combine(Path.GetTempPath(), "MapReduceTest_" + Guid.NewGuid().ToString("N")[..8]);
            _createdFiles = new List<string>();
            _createdDirectories = new List<string>();

            // 创建测试目录
            Directory.CreateDirectory(_testDirectory);
            _createdDirectories.Add(_testDirectory);

            _output.WriteLine($"测试目录: {_testDirectory}");
        }

        /// <summary>
        /// 测试WordCount的完整端到端流程
        /// </summary>
        [Fact]
        public async Task WordCount_EndToEnd_Success()
        {
            _output.WriteLine("开始WordCount端到端测试...");

            // 1. 准备测试数据
            var inputFiles = await PrepareTestDataAsync();
            _output.WriteLine($"准备了 {inputFiles.Length} 个输入文件");

            // 2. 创建并启动Master
            var master = new MapReduceMaster(inputFiles, 2);
            Tests.DirectMasterService.SetMasterInstance(master);
            _output.WriteLine("Master已启动");

            // 3. 创建并启动多个Worker
            var workers = new List<MapReduceWorker>();
            var workerTasks = new List<Task>();

            try
            {
                // 启动3个Worker（使用测试模式）
                for (int i = 0; i < 3; i++)
                {
                    var worker = CreateTestWorker();
                    workers.Add(worker);

                    var workerTask = Task.Run(async () =>
                    {
                        try
                        {
                            await worker.StartAsync();
                        }
                        catch (OperationCanceledException)
                        {
                            // 正常取消，忽略
                        }
                    });
                    workerTasks.Add(workerTask);
                }

                _output.WriteLine($"启动了 {workers.Count} 个Worker");

                // 4. 等待所有任务完成
                await WaitForJobCompletionAsync(master);
                _output.WriteLine("所有任务已完成");

                // 5. 验证结果
                await VerifyResultsAsync();
                _output.WriteLine("结果验证通过");

            }
            finally
            {
                // 6. 清理资源
                foreach (var worker in workers)
                {
                    worker.Stop();
                }

                // 等待Worker任务结束
                await Task.WhenAll(workerTasks.Select(t => t.ContinueWith(_ => { })));

                master.Dispose();
                _output.WriteLine("资源清理完成");
            }
        }

        /// <summary>
        /// 测试故障恢复机制
        /// </summary>
        [Fact]
        public async Task FaultTolerance_WorkerFailure_Recovery()
        {
            _output.WriteLine("开始故障恢复测试...");

            // 准备测试数据
            var inputFiles = await PrepareTestDataAsync();

            var master = new MapReduceMaster(inputFiles, 2);
            Tests.DirectMasterService.SetMasterInstance(master);

            var workers = new List<MapReduceWorker>();
            var workerTasks = new List<Task>();
            var cancellationTokens = new List<CancellationTokenSource>();

            try
            {
                // 启动2个Worker
                for (int i = 0; i < 2; i++)
                {
                    var worker = CreateTestWorker();
                    var cts = new CancellationTokenSource();

                    workers.Add(worker);
                    cancellationTokens.Add(cts);

                    var workerTask = Task.Run(async () =>
                    {
                        try
                        {
                            await worker.StartAsync();
                        }
                        catch (OperationCanceledException)
                        {
                            // 正常取消
                        }
                    });
                    workerTasks.Add(workerTask);
                }

                // 等待一段时间让一些任务开始执行
                await Task.Delay(2000);

                // 模拟第一个Worker故障
                _output.WriteLine("模拟Worker故障...");
                workers[0].Stop();

                // 等待任务完成（应该由剩余的Worker完成）
                await WaitForJobCompletionAsync(master);

                // 验证结果仍然正确
                await VerifyResultsAsync();
                _output.WriteLine("故障恢复测试通过");
            }
            finally
            {
                foreach (var worker in workers)
                {
                    worker.Stop();
                }

                foreach (var cts in cancellationTokens)
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                await Task.WhenAll(workerTasks.Select(t => t.ContinueWith(_ => { })));
                master.Dispose();
            }
        }

        /// <summary>
        /// 创建测试用的Worker
        /// </summary>
        private MapReduceWorker CreateTestWorker()
        {
            // 创建一个使用DirectMasterService的Worker
            var worker = new TestMapReduceWorker();
            return worker;
        }

        /// <summary>
        /// 准备测试数据
        /// </summary>
        private async Task<string[]> PrepareTestDataAsync()
        {
            var inputFiles = new List<string>();

            // 创建测试文件1 - 包含重复单词
            var file1 = Path.Combine(_testDirectory, "input1.txt");
            var content1 = "hello world\nhello mapreduce\nworld of mapreduce\nhello world again";
            await File.WriteAllTextAsync(file1, content1);
            inputFiles.Add(file1);
            _createdFiles.Add(file1);

            // 创建测试文件2 - 包含不同单词
            var file2 = Path.Combine(_testDirectory, "input2.txt");
            var content2 = "distributed systems\nmapreduce framework\nsystems programming\ndistributed computing";
            await File.WriteAllTextAsync(file2, content2);
            inputFiles.Add(file2);
            _createdFiles.Add(file2);

            // 创建测试文件3 - 空文件
            var file3 = Path.Combine(_testDirectory, "input3.txt");
            await File.WriteAllTextAsync(file3, "");
            inputFiles.Add(file3);
            _createdFiles.Add(file3);

            return inputFiles.ToArray();
        }

        /// <summary>
        /// 等待作业完成
        /// </summary>
        private async Task WaitForJobCompletionAsync(MapReduceMaster master, int timeoutSeconds = 30)
        {
            var timeout = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < timeout)
            {
                var status = await master.GetMasterStatusAsync();

                _output.WriteLine($"当前状态: {status.CurrentPhase}, " +
                                $"Map: {status.CompletedMapTasks}/{status.TotalMapTasks}, " +
                                $"Reduce: {status.CompletedReduceTasks}/{status.TotalReduceTasks}");

                if (status.IsCompleted)
                {
                    _output.WriteLine("作业已完成");
                    return;
                }

                await Task.Delay(1000);
            }

            throw new TimeoutException($"作业在 {timeoutSeconds} 秒内未完成");
        }

        /// <summary>
        /// 验证输出结果
        /// </summary>
        private async Task VerifyResultsAsync()
        {
            // 预期的单词计数（基于测试数据）
            var expectedCounts = new Dictionary<string, int>
            {
                { "hello", 3 },
                { "world", 3 },
                { "mapreduce", 3 },
                { "of", 1 },
                { "again", 1 },
                { "distributed", 2 },
                { "systems", 2 },
                { "framework", 1 },
                { "programming", 1 },
                { "computing", 1 }
            };

            // 等待一下确保文件已写入
            await Task.Delay(500);

            // 读取所有输出文件
            var actualCounts = new Dictionary<string, int>();
            var currentDir = Directory.GetCurrentDirectory();

            _output.WriteLine($"当前工作目录: {currentDir}");

            // 查找所有输出文件
            var outputDir = Path.Combine(currentDir, "test-output");
            var outputFiles = Directory.Exists(outputDir) ? Directory.GetFiles(outputDir, "mr-out-*") : Array.Empty<string>();
            _output.WriteLine($"找到 {outputFiles.Length} 个输出文件");

            for (int i = 0; i < 2; i++) // 2个Reduce任务
            {
                var outputFile = Path.Combine(outputDir, $"mr-out-{i}");

                _output.WriteLine($"检查输出文件: {outputFile}");

                if (File.Exists(outputFile))
                {
                    _createdFiles.Add(outputFile);
                    var lines = await File.ReadAllLinesAsync(outputFile);

                    _output.WriteLine($"文件 {outputFile} 包含 {lines.Length} 行");

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        _output.WriteLine($"处理行: '{line}'");
                        var parts = line.Split(' ', 2);
                        if (parts.Length == 2 && int.TryParse(parts[1], out int count))
                        {
                            actualCounts[parts[0]] = count;
                            _output.WriteLine($"解析单词: {parts[0]} = {count}");
                        }
                    }
                }
                else
                {
                    _output.WriteLine($"输出文件不存在: {outputFile}");
                }
            }

            // 验证单词计数
            foreach (var expected in expectedCounts)
            {
                Assert.True(actualCounts.ContainsKey(expected.Key),
                    $"输出中缺少单词: {expected.Key}");

                Assert.Equal(expected.Value, actualCounts[expected.Key]);

                _output.WriteLine($"验证通过: {expected.Key} = {expected.Value}");
            }

            // 验证没有多余的单词
            foreach (var actual in actualCounts)
            {
                Assert.True(expectedCounts.ContainsKey(actual.Key),
                    $"输出中包含意外的单词: {actual.Key}");
            }

            _output.WriteLine($"总共验证了 {expectedCounts.Count} 个单词的计数");
        }

        /// <summary>
        /// 清理测试资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 删除创建的文件
                foreach (var file in _createdFiles)
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }

                // 删除创建的目录
                foreach (var directory in _createdDirectories)
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }

                // 清理中间文件和输出文件
                CleanupMapReduceFiles();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"清理资源时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理MapReduce生成的中间文件和输出文件
        /// </summary>
        private void CleanupMapReduceFiles()
        {
            try
            {
                var currentDir = Directory.GetCurrentDirectory();

                // 清理中间文件 (mr-*-*)
                var intermediateFiles = Directory.GetFiles(currentDir, "mr-*-*");
                foreach (var file in intermediateFiles)
                {
                    File.Delete(file);
                }

                // 清理输出文件 (mr-out-*)
                var outputFiles = Directory.GetFiles(currentDir, "mr-out-*");
                foreach (var file in outputFiles)
                {
                    File.Delete(file);
                }

                // 清理测试输出目录
                var testOutputDir = Path.Combine(currentDir, "test-output");
                if (Directory.Exists(testOutputDir))
                {
                    var testOutputFiles = Directory.GetFiles(testOutputDir, "mr-out-*");
                    foreach (var file in testOutputFiles)
                    {
                        File.Delete(file);
                    }
                    if (!Directory.EnumerateFileSystemEntries(testOutputDir).Any())
                    {
                        Directory.Delete(testOutputDir);
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"清理MapReduce文件时出错: {ex.Message}");
            }
        }
    }
}