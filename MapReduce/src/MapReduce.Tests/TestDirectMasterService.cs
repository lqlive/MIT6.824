using MapReduce.Common.Contracts;
using MapReduce.Common.Models;
using MapReduce.Master;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MapReduce.Tests
{
    /// <summary>
    /// 用于测试的直接Master服务实现
    /// 简化版本，用于单元测试而不需要网络通信
    /// </summary>
    public class DirectMasterService : IMapReduceService
    {
        private static MapReduceMaster? _masterInstance;

        public static void SetMasterInstance(MapReduceMaster master)
        {
            _masterInstance = master;
        }

        public async Task<MasterStatus> GetMasterStatusAsync()
        {
            return await (_masterInstance?.GetMasterStatusAsync() ?? Task.FromResult(new MasterStatus
            {
                CurrentPhase = "Waiting",
                CompletedMapTasks = 0,
                CompletedReduceTasks = 0,
                ActiveWorkers = new List<string>(),
                TotalMapTasks = 0,
                TotalReduceTasks = 0,
                IsCompleted = false
            }));
        }

        public async Task<bool> IsAllTasksCompletedAsync()
        {
            return await (_masterInstance?.IsAllTasksCompletedAsync() ?? Task.FromResult(false));
        }

        public async Task ReportTaskCompletionAsync(string workerId, int taskId, bool success, string[] outputFiles)
        {
            if (_masterInstance != null)
                await _masterInstance.ReportTaskCompletionAsync(workerId, taskId, success, outputFiles);
        }

        public async Task<MapReduceTask?> RequestTaskAsync(string workerId)
        {
            return await _masterInstance?.RequestTaskAsync(workerId)!;
        }

        public async Task SendHeartbeatAsync(string workerId)
        {
            if (_masterInstance != null)
                await _masterInstance.SendHeartbeatAsync(workerId);
        }
    }
}