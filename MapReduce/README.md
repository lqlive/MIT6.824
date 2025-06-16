# MapReduce C# 实现

这是一个基于C#的MapReduce框架实现，遵循MIT 6.824分布式系统课程的设计理念。

## 项目结构

```
MapReduce/
├── MapReduce.Common/     # 共享模型、接口和工具类
├── MapReduce.Examples/   # 示例实现（WordCount）
├── MapReduce.Master/     # Master节点实现
├── MapReduce.Worker/     # Worker节点实现
└── MapReduce.sln        # 解决方案文件
```

## 核心组件

### MapReduce.Common
- **Models**: TaskType, TaskStatus, MapReduceTask, MasterStatus
- **Interfaces**: IMapFunction, IReduceFunction, IMapReduceService
- **Utilities**: HashingHelper（用于键分区和文件命名）

### MapReduce.Master
- 任务调度和分配
- Worker心跳检测和超时处理
- Map/Reduce阶段管理
- 故障容错和任务重新分配

### MapReduce.Worker
- 向Master请求任务
- 执行Map和Reduce操作
- 文件I/O处理
- 任务状态报告

## 使用方法

### 1. 构建项目
```bash
dotnet build
```

### 2. 启动Master节点
```bash
cd MapReduce.Master
dotnet run [输入目录] [Reduce任务数]
```

示例：
```bash
dotnet run "C:\input" 3
```

### 3. 启动Worker节点
在另一个终端中：
```bash
cd MapReduce.Worker
dotnet run [Master端点]
```

示例：
```bash
dotnet run http://localhost:8080
```

可以启动多个Worker实例来并行处理任务。

## 工作流程

1. **Master启动**: 初始化Map任务，每个输入文件对应一个Map任务
2. **Worker请求任务**: Worker定期向Master请求可用任务
3. **Map阶段**: 
   - Worker执行Map函数处理输入文件
   - 生成按key分区的中间文件 (mr-{mapId}-{reduceId})
4. **Reduce阶段**: 
   - 所有Map任务完成后开始
   - Worker读取相关中间文件，执行Reduce函数
   - 生成最终输出文件 (mr-out-{reduceId})

## 文件格式

### 中间文件
```
key1    value1
key2    value2
```

### 输出文件
```
key1 count1
key2 count2
```

## 特性

- **容错处理**: Worker超时检测和任务重新分配
- **心跳机制**: Worker定期发送心跳维持连接
- **状态监控**: 实时显示任务进度和系统状态
- **优雅关闭**: 支持Ctrl+C优雅停止

## 扩展

要实现自定义的Map/Reduce逻辑：

1. 实现 `IMapFunction` 接口
2. 实现 `IReduceFunction` 接口
3. 在Worker中注册自定义函数

## 注意事项

- 当前实现使用直接方法调用而非网络RPC（简化版本）
- 实际分布式环境中应使用WCF或gRPC进行通信
- 输入文件应放在Master节点可访问的目录中 