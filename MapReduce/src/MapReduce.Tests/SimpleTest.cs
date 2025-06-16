using MapReduce.Master;
using MapReduce.Worker;
using MapReduce.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MapReduce.Tests
{
    /// <summary>
    /// 简单的功能验证测试
    /// </summary>
    public class SimpleTest
    {
        private readonly ITestOutputHelper _output;

        public SimpleTest(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// 测试Master的基本功能
        /// </summary>
        [Fact]
        public async Task Master_BasicFunctionality_Works()
        {
            _output.WriteLine("测试Master基本功能...");

            // 创建临时测试文件
            var testFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(testFile, "hello world\nhello test");

            try
            {
                // 创建Master
                var master = new MapReduceMaster(new[] { testFile }, 2);

                // 测试状态获取
                var status = await master.GetMasterStatusAsync();
                Assert.NotNull(status);
                Assert.Equal("Map", status.CurrentPhase);
                Assert.Equal(1, status.TotalMapTasks);
                Assert.Equal(2, status.TotalReduceTasks);

                _output.WriteLine($"Master状态: {status.CurrentPhase}, Map:{status.CompletedMapTasks}/{status.TotalMapTasks}");

                // 测试任务请求
                var task = await master.RequestTaskAsync("test-worker");
                Assert.NotNull(task);
                Assert.Equal(TaskType.Map, task.TaskType);

                _output.WriteLine($"获得任务: {task.TaskType} #{task.TaskId}");

                master.Dispose();
                _output.WriteLine("Master基本功能测试通过");
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        /// <summary>
        /// 测试Worker的基本功能
        /// </summary>
        [Fact]
        public void Worker_Creation_Works()
        {
            _output.WriteLine("测试Worker创建...");

            var worker = new MapReduceWorker("test-endpoint");
            Assert.NotNull(worker);

            worker.Stop();
            _output.WriteLine("Worker创建测试通过");
        }

        /// <summary>
        /// 测试Master-Worker基本通信
        /// </summary>
        [Fact]
        public async Task MasterWorker_Communication_Works()
        {
            _output.WriteLine("测试Master-Worker通信...");

            // 创建临时测试文件
            var testFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(testFile, "test data");

            try
            {
                // 创建Master
                var master = new MapReduceMaster(new[] { testFile }, 1);
                DirectMasterService.SetMasterInstance(master);

                // 测试Worker请求任务
                var service = new DirectMasterService();
                var task = await service.RequestTaskAsync("test-worker");

                Assert.NotNull(task);
                _output.WriteLine($"Worker获得任务: {task.TaskType} #{task.TaskId}");

                // 测试心跳
                await service.SendHeartbeatAsync("test-worker");
                _output.WriteLine("心跳发送成功");

                // 测试状态查询
                var status = await service.GetMasterStatusAsync();
                Assert.NotNull(status);
                _output.WriteLine($"状态查询成功: {status.CurrentPhase}");

                master.Dispose();
                _output.WriteLine("Master-Worker通信测试通过");
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
    }
}