# MIT6.824 MapReduce C# 实现

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)]()

## 📖 项目介绍

这是一个基于 C# 和 .NET 8.0 的 MIT6.824 分布式系统课程 MapReduce 框架实现。项目完整实现了 MapReduce 编程模型，包括任务调度、容错处理、分布式计算等核心功能，为学习分布式系统提供了一个优秀的实践案例。

## ✨ 功能特性

- 🔄 **完整的 MapReduce 实现**：支持 Map 和 Reduce 两阶段处理
- 🎯 **任务调度系统**：Master 节点智能分配和管理任务
- 💪 **容错机制**：自动检测节点故障并重新分配任务
- 🌐 **WCF 通信**：基于 Windows Communication Foundation 的分布式通信
- 📊 **实时监控**：心跳检测和状态跟踪
- 🔧 **易扩展架构**：支持自定义 Map 和 Reduce 函数
- 📈 **性能优化**：异步编程和并发处理
- 🧪 **单元测试**：完整的测试覆盖

## 🛠️ 技术栈

- **语言**: C# 12.0
- **框架**: .NET 8.0
- **通信**: WCF (Windows Communication Foundation)
- **序列化**: Newtonsoft.Json
- **测试**: xUnit
- **IDE**: Visual Studio 2022 / VS Code

## 📁 项目结构

```
MIT6.824/
├── MapReduce/
│   ├── src/
│   │   ├── MapReduce.Master/          # Master 节点实现
│   │   │   ├── MapReduceMaster.cs     # 核心调度逻辑
│   │   │   └── Program.cs             # 程序入口
│   │   ├── MapReduce.Worker/          # Worker 节点实现
│   │   │   ├── MapReduceWorker.cs     # 任务执行逻辑
│   │   │   └── Program.cs             # 程序入口
│   │   ├── MapReduce.Common/          # 公共组件
│   │   │   ├── Interfaces/            # 接口定义
│   │   │   ├── Models/                # 数据模型
│   │   │   ├── Contracts/             # WCF 服务契约
│   │   │   └── Utilities/             # 工具类
│   │   ├── MapReduce.Examples/        # 示例实现
│   │   │   ├── WordCountMapFunction.cs
│   │   │   └── WordCountReduceFunction.cs
│   │   └── MapReduce.Tests/           # 单元测试
│   ├── docs/
│   │   └── design.md                  # 详细设计文档
│   ├── MapReduce.sln                  # 解决方案文件
│   └── README.md                      # 项目说明
└── README.md                          # 总体说明
```

## 🚀 快速开始

### 先决条件

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 或 VS Code
- Windows 10/11 (推荐) 或 Linux/macOS

### 安装和构建

1. **克隆仓库**
   ```bash
   git clone https://github.com/your-username/MIT6.824.git
   cd MIT6.824/MapReduce
   ```

2. **恢复依赖**
   ```bash
   dotnet restore
   ```

3. **构建项目**
   ```bash
   dotnet build
   ```

4. **运行测试**
   ```bash
   dotnet test
   ```

### 运行示例

1. **启动 Master 节点**
   ```bash
   cd src/MapReduce.Master
   dotnet run
   ```

2. **启动 Worker 节点**（另开终端）
   ```bash
   cd src/MapReduce.Worker
   dotnet run
   ```

3. **准备输入数据**
   ```bash
   # 在项目根目录创建输入文件
   echo "hello world hello" > input1.txt
   echo "world hello world" > input2.txt
   ```

## 💡 使用示例

### 实现自定义 Map 函数

```csharp
public class CustomMapFunction : IMapFunction
{
    public IEnumerable<KeyValuePair<string, string>> Map(string filename, string content)
    {
        // 自定义 Map 逻辑
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            yield return new KeyValuePair<string, string>(word.ToLower(), "1");
        }
    }
}
```

### 实现自定义 Reduce 函数

```csharp
public class CustomReduceFunction : IReduceFunction
{
    public string Reduce(string key, IEnumerable<string> values)
    {
        // 自定义 Reduce 逻辑
        return values.Count().ToString();
    }
}
```

### WordCount 示例

系统包含了完整的 WordCount 实现，展示了如何：

1. **分词处理**：使用正则表达式分割文本
2. **键值对生成**：为每个单词生成 (word, "1") 对
3. **计数聚合**：统计每个单词的出现次数

```csharp
// Map 阶段：分词
Input: "hello world hello"
Output: [("hello", "1"), ("world", "1"), ("hello", "1")]

// Reduce 阶段：计数
Input: ("hello", ["1", "1"])
Output: ("hello", "2")
```

## 🏗️ 架构设计

### 核心组件

- **Master 节点**：任务调度、状态管理、容错处理
- **Worker 节点**：任务执行、心跳维护、结果报告
- **WCF 服务**：分布式通信和服务发现
- **任务管理**：状态跟踪和生命周期管理

### 执行流程

1. **初始化**：Master 根据输入文件创建 Map 任务
2. **Map 阶段**：Worker 执行 Map 函数，生成中间结果
3. **Shuffle 阶段**：数据重分布和排序
4. **Reduce 阶段**：Worker 执行 Reduce 函数，生成最终结果
5. **完成**：所有任务完成，输出结果文件

## 🔧 配置选项

### Master 配置

```csharp
// 在 MapReduceMaster 构造函数中配置
var master = new MapReduceMaster(
    inputFiles: new[] { "input1.txt", "input2.txt" },
    reduceCount: 3  // Reduce 任务数量
);
```

### Worker 配置

```csharp
// 在 MapReduceWorker 构造函数中配置
var worker = new MapReduceWorker(
    masterEndpoint: "http://localhost:8080"  // Master 服务地址
);
```

## 🧪 测试

运行所有测试：
```bash
dotnet test
```

运行特定测试：
```bash
dotnet test --filter "TestClassName"
```

生成测试报告：
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📊 性能指标

- **任务调度延迟**：< 100ms
- **心跳检测间隔**：3秒
- **任务超时时间**：10秒
- **并发 Worker 支持**：无限制
- **内存使用**：< 100MB (单节点)

## 🤝 贡献指南

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 代码规范

- 遵循 C# 编码规范
- 添加必要的单元测试
- 更新相关文档
- 确保所有测试通过

## 📚 学习资源

- [MIT6.824 课程主页](https://pdos.csail.mit.edu/6.824/)
- [MapReduce 论文](https://static.googleusercontent.com/media/research.google.com/en//archive/mapreduce-osdi04.pdf)
- [设计文档](MapReduce/docs/design.md)
- [C# 异步编程指南](https://docs.microsoft.com/en-us/dotnet/csharp/async)
- [WCF 开发指南](https://docs.microsoft.com/en-us/dotnet/framework/wcf/)

## 🐛 问题排查

### 常见问题

1. **端口占用**
   ```bash
   netstat -ano | findstr :8080
   ```

2. **依赖包缺失**
   ```bash
   dotnet restore --force
   ```

3. **WCF 服务启动失败**
   - 检查防火墙设置
   - 确认端口可用性
   - 验证服务配置

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) - 查看 LICENSE 文件了解详情。

## 👥 致谢

- MIT6.824 课程团队
- .NET 开源社区
- 所有贡献者

## 📧 联系方式

- 项目维护者：[qing long](https://github.com/lqlive)
- 邮箱：zze@live.com
- 问题反馈：[Issues](https://github.com/lqlive/MIT6.824/issues)

---

⭐ 如果这个项目对你有帮助，请给它一个星标！
