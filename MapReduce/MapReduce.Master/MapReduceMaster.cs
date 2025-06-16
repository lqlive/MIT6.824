using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MapReduce.Common.Contracts;
using MapReduce.Common.Models;
using MapReduce.Common.Utilities;
using TaskStatus = MapReduce.Common.Models.TaskStatus;

namespace MapReduce.Master
{
    /// <summary>
    /// MapReduce Master节点实现
    /// 负责任务调度、状态管理和容错处理
    /// </summary>
    public class MapReduceMaster : IMapReduceService
    {
        private readonly ConcurrentDictionary<int, MapReduceTask> _tasks;
        private readonly ConcurrentDictionary<string, DateTime> _workerHeartbeats;
        private readonly object _phaseLock = new object();

        private readonly int _reduceCount;
        private readonly string[] _inputFiles;
        private readonly Timer _heartbeatTimer;

        private int _currentTaskId = 0;
        private bool _mapPhaseCompleted = false;
        private bool _allTasksCompleted = false;

        // 配置参数
        private readonly TimeSpan _workerTimeout = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _heartbeatCheckInterval = TimeSpan.FromSeconds(5);

        public MapReduceMaster(string[] inputFiles, int reduceCount)
        {
            _inputFiles = inputFiles ?? throw new ArgumentNullException(nameof(inputFiles));
            _reduceCount = reduceCount;

            _tasks = new ConcurrentDictionary<int, MapReduceTask>();
            _workerHeartbeats = new ConcurrentDictionary<string, DateTime>();

            // 初始化Map任务
            InitializeMapTasks();

            // 启动心跳检测定时器
            _heartbeatTimer = new Timer(CheckWorkerTimeouts, null,
                _heartbeatCheckInterval, _heartbeatCheckInterval);

            Console.WriteLine($"Master启动: {_inputFiles.Length}个Map任务, {_reduceCount}个Reduce任务");
        }

        /// <summary>
        /// Worker请求任务
        /// </summary>
        public async Task<MapReduceTask?> RequestTaskAsync(string workerId)
        {
            await Task.CompletedTask; // 模拟异步操作

            // 更新Worker心跳
            _workerHeartbeats.AddOrUpdate(workerId, DateTime.Now, (k, v) => DateTime.Now);

            lock (_phaseLock)
            {
                // 如果所有任务都完成了，返回null
                if (_allTasksCompleted)
                {
                    Console.WriteLine($"Worker {workerId} 请求任务 - 所有任务已完成");
                    return null;
                }

                // 优先分配Map任务
                if (!_mapPhaseCompleted)
                {
                    var mapTask = GetAvailableMapTask(workerId);
                    if (mapTask != null)
                    {
                        Console.WriteLine($"分配Map任务 {mapTask.TaskId} 给 Worker {workerId}");
                        return mapTask;
                    }

                    // 检查Map阶段是否完成
                    CheckMapPhaseCompletion();
                }

                // Map阶段完成后，分配Reduce任务
                if (_mapPhaseCompleted)
                {
                    var reduceTask = GetAvailableReduceTask(workerId);
                    if (reduceTask != null)
                    {
                        Console.WriteLine($"分配Reduce任务 {reduceTask.TaskId} 给 Worker {workerId}");
                        return reduceTask;
                    }

                    // 检查所有任务是否完成
                    CheckAllTasksCompletion();
                }

                Console.WriteLine($"Worker {workerId} 请求任务 - 暂无可用任务");
                return null;
            }
        }

        /// <summary>
        /// Worker报告任务完成
        /// </summary>
        public async Task ReportTaskCompletionAsync(string workerId, int taskId, bool success, string[] outputFiles)
        {
            await Task.CompletedTask;

            Console.WriteLine($"Worker {workerId} 报告任务 {taskId} {(success ? "完成" : "失败")}");

            if (_tasks.TryGetValue(taskId, out var task))
            {
                if (success)
                {
                    task.Status = TaskStatus.Completed;
                    task.IntermediateFiles = outputFiles?.ToList() ?? new List<string>();
                    Console.WriteLine($"任务 {taskId} 标记为完成");
                }
                else
                {
                    task.Status = TaskStatus.Failed;
                    task.AssignedWorkerId = null;
                    task.AssignedTime = default;
                    Console.WriteLine($"任务 {taskId} 标记为失败，将重新分配");
                }
            }
        }

        /// <summary>
        /// Worker心跳检测
        /// </summary>
        public async Task SendHeartbeatAsync(string workerId)
        {
            await Task.CompletedTask;
            _workerHeartbeats.AddOrUpdate(workerId, DateTime.Now, (k, v) => DateTime.Now);
        }

        /// <summary>
        /// 检查所有任务是否完成
        /// </summary>
        public async Task<bool> IsAllTasksCompletedAsync()
        {
            await Task.CompletedTask;
            return _allTasksCompleted;
        }

        /// <summary>
        /// 获取Master状态信息
        /// </summary>
        public async Task<MasterStatus> GetMasterStatusAsync()
        {
            await Task.CompletedTask;

            var mapTasks = _tasks.Values.Where(t => t.TaskType == TaskType.Map).ToList();
            var reduceTasks = _tasks.Values.Where(t => t.TaskType == TaskType.Reduce).ToList();

            return new MasterStatus
            {
                TotalMapTasks = mapTasks.Count,
                CompletedMapTasks = mapTasks.Count(t => t.Status == TaskStatus.Completed),
                TotalReduceTasks = reduceTasks.Count,
                CompletedReduceTasks = reduceTasks.Count(t => t.Status == TaskStatus.Completed),
                CurrentPhase = _mapPhaseCompleted ? "Reduce" : "Map",
                ActiveWorkers = _workerHeartbeats.Keys.ToList(),
                IsCompleted = _allTasksCompleted
            };
        }

        #region 私有方法

        /// <summary>
        /// 初始化Map任务
        /// </summary>
        private void InitializeMapTasks()
        {
            for (int i = 0; i < _inputFiles.Length; i++)
            {
                var task = new MapReduceTask
                {
                    TaskId = ++_currentTaskId,
                    TaskType = TaskType.Map,
                    Status = TaskStatus.Idle,
                    InputFiles = new List<string> { _inputFiles[i] },
                    ReduceCount = _reduceCount
                };

                _tasks.TryAdd(task.TaskId, task);
            }
        }

        /// <summary>
        /// 初始化Reduce任务
        /// </summary>
        private void InitializeReduceTasks()
        {
            for (int i = 0; i < _reduceCount; i++)
            {
                var task = new MapReduceTask
                {
                    TaskId = ++_currentTaskId,
                    TaskType = TaskType.Reduce,
                    Status = TaskStatus.Idle,
                    ReduceIndex = i,
                    IntermediateFiles = GetIntermediateFilesForReduce(i)
                };

                _tasks.TryAdd(task.TaskId, task);
            }
        }

        /// <summary>
        /// 获取可用的Map任务
        /// </summary>
        private MapReduceTask? GetAvailableMapTask(string workerId)
        {
            var availableTask = _tasks.Values
                .Where(t => t.TaskType == TaskType.Map && t.Status == TaskStatus.Idle)
                .FirstOrDefault();

            if (availableTask != null)
            {
                availableTask.Status = TaskStatus.InProgress;
                availableTask.AssignedWorkerId = workerId;
                availableTask.AssignedTime = DateTime.Now;
            }

            return availableTask;
        }

        /// <summary>
        /// 获取可用的Reduce任务
        /// </summary>
        private MapReduceTask? GetAvailableReduceTask(string workerId)
        {
            var availableTask = _tasks.Values
                .Where(t => t.TaskType == TaskType.Reduce && t.Status == TaskStatus.Idle)
                .FirstOrDefault();

            if (availableTask != null)
            {
                availableTask.Status = TaskStatus.InProgress;
                availableTask.AssignedWorkerId = workerId;
                availableTask.AssignedTime = DateTime.Now;
            }

            return availableTask;
        }

        /// <summary>
        /// 检查Map阶段是否完成
        /// </summary>
        private void CheckMapPhaseCompletion()
        {
            var mapTasks = _tasks.Values.Where(t => t.TaskType == TaskType.Map).ToList();
            if (mapTasks.All(t => t.Status == TaskStatus.Completed))
            {
                _mapPhaseCompleted = true;
                Console.WriteLine("Map阶段完成，开始初始化Reduce任务");
                InitializeReduceTasks();
            }
        }

        /// <summary>
        /// 检查所有任务是否完成
        /// </summary>
        private void CheckAllTasksCompletion()
        {
            var reduceTasks = _tasks.Values.Where(t => t.TaskType == TaskType.Reduce).ToList();
            if (reduceTasks.Any() && reduceTasks.All(t => t.Status == TaskStatus.Completed))
            {
                _allTasksCompleted = true;
                Console.WriteLine("所有任务完成！MapReduce作业结束");
            }
        }

        /// <summary>
        /// 获取指定Reduce任务的中间文件列表
        /// </summary>
        private List<string> GetIntermediateFilesForReduce(int reduceIndex)
        {
            var intermediateFiles = new List<string>();
            var mapTasks = _tasks.Values.Where(t => t.TaskType == TaskType.Map).ToList();

            foreach (var mapTask in mapTasks)
            {
                var fileName = HashingHelper.GetIntermediateFileName(mapTask.TaskId, reduceIndex);
                intermediateFiles.Add(fileName);
            }

            return intermediateFiles;
        }

        /// <summary>
        /// 检查Worker超时
        /// </summary>
        private void CheckWorkerTimeouts(object? state)
        {
            var now = DateTime.Now;
            var timeoutWorkers = new List<string>();

            // 找出超时的Worker
            foreach (var kvp in _workerHeartbeats)
            {
                if (now - kvp.Value > _workerTimeout)
                {
                    timeoutWorkers.Add(kvp.Key);
                }
            }

            // 处理超时Worker的任务
            foreach (var workerId in timeoutWorkers)
            {
                Console.WriteLine($"Worker {workerId} 超时，重新分配其任务");
                _workerHeartbeats.TryRemove(workerId, out _);

                // 重新分配该Worker的任务
                var workerTasks = _tasks.Values
                    .Where(t => t.AssignedWorkerId == workerId && t.Status == TaskStatus.InProgress)
                    .ToList();

                foreach (var task in workerTasks)
                {
                    task.Status = TaskStatus.Idle;
                    task.AssignedWorkerId = null;
                    task.AssignedTime = default;
                    Console.WriteLine($"任务 {task.TaskId} 重新标记为可用");
                }
            }
        }

        #endregion

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
        }
    }
}