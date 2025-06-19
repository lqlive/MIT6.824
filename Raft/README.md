# MIT 6.824 Lab 2: Raft 共识算法

## 📖 实验介绍

这是MIT 6.824分布式系统课程的第二个实验，实现Raft共识算法。Raft是一个用于管理复制日志的共识算法，它比Paxos更容易理解和实现。

## 🎯 实验目标

实现一个完整的Raft算法，包括：

### Part 2A: Leader选举
- 实现Raft leader选举
- 实现心跳机制（不带日志条目的AppendEntries RPC）

### Part 2B: 日志复制
- 实现leader和follower的日志复制功能
- 处理日志不一致的情况

### Part 2C: 持久化
- 实现状态持久化
- 确保重启后能正确恢复状态

### Part 2D: 日志压缩（可选）
- 实现快照机制
- 减少日志存储空间

## 🏗️ 项目结构

```
Raft/
├── src/
│   ├── Raft.Common/           # 公共组件
│   │   ├── Models/            # 数据模型
│   │   ├── Interfaces/        # 接口定义
│   │   └── Utilities/         # 工具类
│   ├── Raft.Server/           # Raft服务器实现
│   ├── Raft.Client/           # 客户端实现
│   └── Raft.Tests/            # 单元测试
├── docs/                      # 文档
└── Raft.sln                   # 解决方案文件
```

## 🔑 核心概念

### Raft状态
- **Leader**: 处理客户端请求，管理日志复制
- **Follower**: 被动接收leader的指令
- **Candidate**: 参与leader选举

### 关键组件
- **选举定时器**: 触发leader选举
- **心跳机制**: 维持leader权威性
- **日志复制**: 确保集群状态一致性
- **状态持久化**: 保证故障恢复

## 🚀 开发计划

1. **阶段一**: 搭建基础框架和数据结构
2. **阶段二**: 实现leader选举机制
3. **阶段三**: 实现日志复制功能
4. **阶段四**: 添加持久化支持
5. **阶段五**: 性能优化和测试

## 📚 参考资料

- [Raft论文](https://raft.github.io/raft.pdf)
- [Raft可视化](http://thesecretlivesofdata.com/raft/)
- [MIT 6.824 Course Website](https://pdos.csail.mit.edu/6.824/)

## 🛠️ 技术栈

- **语言**: C# 12.0
- **框架**: .NET 8.0
- **通信**: HTTP/gRPC
- **序列化**: System.Text.Json
- **测试**: xUnit
- **持久化**: File System / SQLite 