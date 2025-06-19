# Raft 共识算法实现 - 设计文档

本文档提供了Raft算法实现的详细设计和开发指南，帮助您了解当前进度并继续完成剩余部分。

## 📁 项目架构

```
Raft/
├── src/
│   ├── Raft.Common/         # 🔧 公共组件和模型
│   │   ├── Models/          # 数据模型定义
│   │   └── Interfaces/      # 接口定义
│   ├── Raft.Server/         # 🖥️ Raft节点实现
│   ├── Raft.Client/         # 👤 Raft客户端实现
│   └── Raft.Tests/          # 🧪 单元测试
├── docs/                    # 📚 文档
├── examples/                # 💡 示例脚本
└── Raft.sln                # Visual Studio解决方案
```

## ✅ 当前完成状态

### 🔧 Raft.Common - 公共组件 ✅
- ✅ **RaftNodeState** - 节点状态枚举(Follower/Candidate/Leader)
- ✅ **LogEntry** - 日志条目模型
- ✅ **VoteRequest/VoteResponse** - RequestVote RPC消息
- ✅ **AppendEntriesRequest/AppendEntriesResponse** - AppendEntries RPC消息
- ✅ **IRaftNode** - Raft节点核心接口

### 🖥️ Raft.Server - 节点实现 ✅
- ✅ **RaftNode类** - 基础Raft节点实现
- ✅ **状态管理** - 节点状态转换逻辑
- ✅ **RequestVote RPC** - 投票请求处理
- ✅ **AppendEntries RPC** - 日志追加和心跳处理(基础版本)
- ✅ **选举超时机制** - 基础超时处理
- ✅ **任期管理** - CurrentTerm管理

### 👤 Raft.Client - 客户端实现 ✅
- ✅ **RaftClient类** - 客户端核心实现
- ✅ **Leader发现** - 自动发现集群Leader节点
- ✅ **命令提交** - 向Leader提交命令
- ✅ **集群状态查询** - 获取整个集群状态
- ✅ **错误处理** - Leader重定向和故障恢复
- ✅ **交互式控制台** - 命令行客户端工具

### 🧪 Raft.Tests - 测试套件 ✅
- ✅ **RaftNode基础测试** - 节点初始化和状态测试
- ✅ **投票机制测试** - RequestVote RPC测试
- ✅ **日志追加测试** - AppendEntries RPC测试
- ✅ **RaftClient测试** - 客户端功能测试
- ✅ **TestLogger实现** - 自定义测试日志器

## 🚧 下一步开发计划

### Part 2A: Leader选举 (优先级: 🔴 HIGH)

**需要完善的功能：**

1. **完整的选举逻辑** 📋
   ```csharp
   // 在 RaftNode.cs 的 StartElection() 方法中实现：
   // - 向所有peer发送RequestVote RPC
   // - 并行收集投票结果
   // - 统计选票数量
   // - 如果获得多数选票，成为Leader
   ```

2. **Leader心跳机制** 📋
   ```csharp
   // 实现Leader的心跳发送：
   // - 启动心跳定时器
   // - 定期向所有Follower发送空的AppendEntries
   // - 维持Leader权威性
   ```

3. **网络通信层** 📋
   ```csharp
   // 创建 IRaftCommunication 接口
   // 实现HTTP/gRPC通信
   // 处理节点间的RPC调用
   ```

### Part 2B: 日志复制 (优先级: 🟡 MEDIUM)

**需要实现的功能：**

1. **日志一致性检查**
   ```csharp
   // 在AppendEntries中实现：
   // - 检查PrevLogIndex和PrevLogTerm
   // - 处理日志冲突
   // - 实现日志回退机制
   ```

2. **客户端命令处理**
   ```csharp
   // 完善SubmitCommandAsync方法：
   // - 添加日志条目
   // - 复制到大多数节点
   // - 提交并应用到状态机
   ```

3. **状态机集成**
   ```csharp
   // 实现状态机接口
   // 应用已提交的日志条目
   ```

### Part 2C: 持久化 (优先级: 🟢 LOW)

**需要实现的功能：**

1. **状态持久化**
   ```csharp
   // 持久化关键状态：
   // - currentTerm
   // - votedFor  
   // - log[]
   ```

2. **故障恢复**
   ```csharp
   // 节点重启时恢复状态
   // 重建内存中的数据结构
   ```

## 🛠️ 立即可以开始的任务

### 任务1: 实现网络通信层（推荐优先开始）

创建文件：`src/Raft.Common/Interfaces/IRaftCommunication.cs`

```csharp
public interface IRaftCommunication
{
    Task<VoteResponse> SendRequestVoteAsync(string targetNodeId, VoteRequest request);
    Task<AppendEntriesResponse> SendAppendEntriesAsync(string targetNodeId, AppendEntriesRequest request);
}
```

### 任务2: 完善选举逻辑

在 `RaftNode.cs` 的 `StartElection` 方法中：

```csharp
private async Task StartElection()
{
    // 1. 向所有peer并行发送投票请求
    // 2. 等待响应并统计选票
    // 3. 如果获得多数选票，成为Leader
    // 4. 如果选举超时，重新开始选举
}
```

### 任务3: 实现Leader心跳

```csharp
private async Task SendHeartbeats()
{
    // 定期向所有Follower发送心跳
    // 检测Follower是否离线
    // 维持Leader权威性
}
```

## 🏃‍♂️ 快速开始

### 构建项目
```bash
cd Raft
dotnet build
```

### 运行测试
```bash
dotnet test
```

### 运行客户端
```bash
cd src/Raft.Client
dotnet run localhost:5001,localhost:5002,localhost:5003
```

或使用示例脚本：
```bash
./examples/run-client.ps1
```

## 📋 开发任务清单

### 立即执行 (本周)
- [ ] 实现`IRaftCommunication`接口
- [ ] 完善`StartElection`方法
- [ ] 添加选举超时随机化
- [ ] 实现基础的HTTP API

### 短期目标 (2-3周)
- [ ] 完成Leader选举功能
- [ ] 实现基础日志复制
- [ ] 添加更多集成测试
- [ ] 创建演示应用

### 长期目标 (1-2月)
- [ ] 完成完整的Raft实现
- [ ] 性能测试和优化
- [ ] 文档完善
- [ ] 部署示例

## 🧪 测试策略

### 单元测试
- 为每个新功能编写单元测试
- 测试各种边界条件和异常情况

### 集成测试
- 创建多节点集群测试
- 模拟网络分区和节点故障

### 示例测试场景
```csharp
[Fact]
public async Task Election_WithThreeNodes_ShouldElectLeader()
{
    // 创建3个节点
    // 断开一个节点的网络
    // 验证剩余两个节点能选出Leader
}
```

## 🔍 调试和测试

### 测试命令
```bash
# 运行所有测试
dotnet test

# 运行特定测试类
dotnet test --filter "RaftNodeTests"

# 详细输出
dotnet test --logger "console;verbosity=detailed"
```

### 日志配置
项目使用Microsoft.Extensions.Logging，支持不同日志级别：
- `Trace` - 详细调试信息
- `Debug` - 调试信息
- `Information` - 一般信息 (默认)
- `Warning` - 警告信息
- `Error` - 错误信息

## 📚 学习资源

- [Raft论文原文](https://raft.github.io/raft.pdf)
- [Raft可视化](https://raft.github.io/)
- [MIT 6.824课程](https://pdos.csail.mit.edu/6.824/)
- [Raft算法详解](https://www.cnblogs.com/mindwind/p/5231986.html)

## 🎯 编码规范

1. **命名约定**
   - 类名使用PascalCase
   - 方法名使用PascalCase
   - 私有字段使用_开头的camelCase
   - 常量使用UPPER_CASE

2. **异步编程**
   - 所有I/O操作使用async/await
   - 方法名以Async结尾
   - 传递CancellationToken

3. **错误处理**
   - 使用异常处理关键错误
   - 使用Result模式处理业务逻辑错误
   - 添加详细的日志记录

4. **测试策略**
   - 每个公共方法都要有对应测试
   - 使用Arrange-Act-Assert模式
   - 测试方法名要清楚描述测试场景

## 🎯 里程碑目标

- **第1周**：完成网络通信层和完整的选举机制
- **第2周**：实现基础的日志复制功能  
- **第3周**：添加持久化支持
- **第4周**：性能优化和全面测试

## 🤝 获取帮助

如果在开发过程中遇到问题，可以：

1. 查看MIT 6.824课程官方资料
2. 参考Raft论文的具体实现指导
3. 分析现有的开源Raft实现
4. 逐步调试和测试每个组件

---

**当前进度: 基础框架完成 ✅ | 下一步: 实现Leader选举 🎯**

开始编码吧！🚀 