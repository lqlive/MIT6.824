# MIT6.824 MapReduce 核心内容分析

## 1. MapReduce 编程模型概述

MapReduce 是一个用于处理大规模数据集的编程模型和执行框架。它将复杂的并行计算抽象为两个简单的操作：**Map** 和 **Reduce**。

### 核心思想
- **分而治之**：将大问题分解为小问题并行处理
- **数据本地化**：将计算移到数据附近，减少网络传输
- **容错处理**：自动处理节点故障和任务重试

## 2. 架构组件

### 2.1 Master 节点
Master 是 MapReduce 系统的协调者，负责：

```csharp
// Master 核心功能 - 来自 src/MapReduce.Master/MapReduceMaster.cs
public class MapReduceMaster : IMapReduceService
{
    private readonly ConcurrentDictionary<int, MapReduceTask> _tasks;
    private readonly ConcurrentDictionary<string, DateTime> _workerHeartbeats;
    
    // 任务调度核心方法
    public async Task<MapReduceTask?> RequestTaskAsync(string workerId)
    {
        lock (_phaseLock)
        {
            // 优先分配Map任务
            if (!_mapPhaseCompleted)
            {
                var mapTask = GetAvailableMapTask(workerId);
                if (mapTask != null) return mapTask;
                CheckMapPhaseCompletion();
            }
            
            // Map完成后分配Reduce任务
            if (_mapPhaseCompleted)
            {
                var reduceTask = GetAvailableReduceTask(workerId);
                if (reduceTask != null) return reduceTask;
                CheckAllTasksCompletion();
            }
            
            return null; // 无可用任务
        }
    }
}
```

**Master 的核心职责：**
1. **任务分配**：根据当前阶段分配 Map 或 Reduce 任务
2. **阶段管理**：控制从 Map 阶段到 Reduce 阶段的转换
3. **容错处理**：监控 Worker 心跳，处理超时任务
4. **状态跟踪**：维护所有任务的执行状态

### 2.2 Worker 节点
Worker 是任务的实际执行者：

```csharp
// Worker 核心逻辑 - 来自 src/MapReduce.Worker/MapReduceWorker.cs
public class MapReduceWorker
{
    public async Task StartAsync()
    {
        while (_isRunning)
        {
            // 1. 向Master请求任务
            var task = await RequestTaskFromMasterAsync();
            
            if (task != null)
            {
                // 2. 执行任务
                bool success = await ExecuteTaskAsync(task);
                
                // 3. 报告完成状态
                await ReportTaskCompletionAsync(task.TaskId, success);
            }
            else
            {
                await Task.Delay(1000); // 等待新任务
            }
        }
    }
}
```

## 3. 编程接口

### 3.1 Map 函数接口
```csharp
// Map 函数接口定义 - 来自 src/MapReduce.Common/Interfaces/IMapFunction.cs
public interface IMapFunction
{
    /// <summary>
    /// 执行Map操作
    /// </summary>
    /// <param name="filename">输入文件名</param>
    /// <param name="content">文件内容</param>
    /// <returns>键值对列表</returns>
    IEnumerable<KeyValuePair<string, string>> Map(string filename, string content);
}
```

### 3.2 Reduce 函数接口
```csharp
// Reduce 函数接口定义 - 来自 src/MapReduce.Common/Interfaces/IReduceFunction.cs
public interface IReduceFunction
{
    /// <summary>
    /// 执行Reduce操作
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="values">该键对应的所有值</param>
    /// <returns>归约后的值</returns>
    string Reduce(string key, IEnumerable<string> values);
}
```

## 4. 经典示例：WordCount

### 4.1 Map 函数实现
```csharp
// WordCount Map函数 - 来自 src/MapReduce.Examples/WordCountMapFunction.cs
public class WordCountMapFunction : IMapFunction
{
    public IEnumerable<KeyValuePair<string, string>> Map(string filename, string content)
    {
        // 使用正则表达式分割单词
        var words = Regex.Split(content.ToLower(), @"[^a-zA-Z]+")
                        .Where(word => !string.IsNullOrWhiteSpace(word));

        // 为每个单词生成键值对 (word, "1")
        foreach (var word in words)
        {
            yield return new KeyValuePair<string, string>(word.Trim(), "1");
        }
    }
}
```

### 4.2 Reduce 函数实现
```csharp
// WordCount Reduce函数 - 来自 src/MapReduce.Examples/WordCountReduceFunction.cs
public class WordCountReduceFunction : IReduceFunction
{
    public string Reduce(string key, IEnumerable<string> values)
    {
        // 计算该单词出现的总次数
        var count = values.Count(v => v == "1");
        return count.ToString();
    }
}
```

## 5. 执行流程

### 5.1 Map 阶段
1. **输入分片**：Master 将输入文件分配给不同的 Map 任务
2. **Map 执行**：Worker 读取输入文件，执行用户定义的 Map 函数
3. **中间结果**：生成键值对并按 key 进行分区（为 Reduce 阶段准备）
4. **本地存储**：Map 输出存储在本地磁盘，按 Reduce 分区组织

### 5.2 Shuffle 阶段
- **数据重分布**：将 Map 输出按 key 重新分组
- **排序**：对每个 Reduce 分区内的数据按 key 排序
- **合并**：相同 key 的所有 value 组合在一起

### 5.3 Reduce 阶段
1. **数据读取**：Reduce Worker 读取所有相关的中间文件
2. **Reduce 执行**：对每个 key 及其 value 列表执行 Reduce 函数
3. **结果输出**：将最终结果写入输出文件

## 6. 任务状态管理

```csharp
// 任务状态定义 - 来自 src/MapReduce.Common/Models/TaskStatus.cs
public enum TaskStatus
{
    Idle,        // 空闲状态，等待分配
    InProgress,  // 正在执行中
    Completed,   // 已完成
    Failed       // 执行失败
}

// 任务类型定义 - 来自 src/MapReduce.Common/Models/TaskType.cs
public enum TaskType
{
    Map,    // Map任务
    Reduce  // Reduce任务
}
```

## 7. 容错机制

### 7.1 Worker 故障处理
```csharp
// 心跳检测和超时处理 - 来自 src/MapReduce.Master/MapReduceMaster.cs
private void CheckWorkerTimeouts(object? state)
{
    var timeoutWorkers = _workerHeartbeats
        .Where(kvp => DateTime.Now - kvp.Value > _workerTimeout)
        .Select(kvp => kvp.Key)
        .ToList();

    foreach (var workerId in timeoutWorkers)
    {
        // 标记该Worker的任务为失败，重新分配
        var workerTasks = _tasks.Values
            .Where(t => t.AssignedWorkerId == workerId && 
                       t.Status == TaskStatus.InProgress);
        
        foreach (var task in workerTasks)
        {
            task.Status = TaskStatus.Failed;
            task.AssignedWorkerId = null;
        }
    }
}
```

### 7.2 任务重试机制
- **检测**：通过心跳机制检测 Worker 故障
- **重置**：将失败任务状态重置为 Idle
- **重分配**：向其他可用 Worker 重新分配任务

## 8. 通信机制

### 8.1 RPC 服务接口
```csharp
// RPC服务接口 - 来自 src/MapReduce.Common/Contracts/IMapReduceService.cs
[ServiceContract]
public interface IMapReduceService
{
    [OperationContract]
    Task<MapReduceTask?> RequestTaskAsync(string workerId);
    
    [OperationContract]
    Task ReportTaskCompletionAsync(string workerId, int taskId, bool success, string[] outputFiles);
    
    [OperationContract]
    Task SendHeartbeatAsync(string workerId);
    
    [OperationContract]
    Task<bool> IsAllTasksCompletedAsync();
    
    [OperationContract]
    Task<MasterStatus> GetMasterStatusAsync();
}
```

### 8.2 通信协议
- **任务请求**：Worker 定期向 Master 请求新任务
- **状态报告**：Worker 完成任务后向 Master 报告结果
- **心跳检测**：Worker 定期发送心跳保持连接
- **状态查询**：支持查询整体执行状态

## 9. 系统特性

### 9.1 扩展性
- **水平扩展**：可以轻松添加更多 Worker 节点
- **负载均衡**：Master 自动分配任务到空闲 Worker
- **数据分区**：支持任意数量的 Reduce 分区

### 9.2 可靠性
- **自动重试**：失败任务自动重新执行
- **进度跟踪**：实时监控任务执行状态
- **错误隔离**：单个任务失败不影响整体进度

### 9.3 性能优化
- **数据本地化**：尽量将计算调度到数据所在节点
- **并行执行**：Map 和 Reduce 阶段内部高度并行
- **流水线处理**：Map 完成的部分可以立即开始 Reduce

## 10. 设计模式和原则

### 10.1 分离关注点
- **接口分离**：Map 和 Reduce 函数通过接口定义
- **模块化**：Master、Worker、Common 模块清晰分离
- **配置外部化**：任务参数和系统配置分离

### 10.2 异步编程
- **非阻塞操作**：大量使用 async/await 模式
- **并发安全**：使用 ConcurrentDictionary 等线程安全集合
- **定时任务**：使用 Timer 进行定期检查

### 10.3 错误处理
- **分层处理**：不同层次的异常处理策略
- **状态恢复**：失败任务可以重置状态重新执行
- **优雅降级**：部分节点故障不影响整体服务

## 11. 总结

MIT6.824 的 MapReduce 实现展示了分布式计算的核心思想：

1. **简单的编程模型**：用户只需实现 Map 和 Reduce 函数
2. **强大的执行引擎**：系统自动处理并行化、容错、负载均衡
3. **可扩展的架构**：支持从单机到大规模集群的部署
4. **实用的设计模式**：体现了分布式系统设计的最佳实践

这个实现为理解分布式计算系统提供了一个优秀的学习案例，涵盖了任务调度、状态管理、容错处理、通信协议等关键技术。 