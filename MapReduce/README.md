# MapReduce C# 实现

这是一个基于C#的MapReduce框架实现，遵循MIT 6.824分布式系统课程的设计理念。该系统支持真正的分布式架构，Master和Worker节点通过HTTP协议进行通信。

## 📁 项目结构

```
MapReduce/
├── testassets/           # 测试数据文件夹
│   ├── input1.txt
│   ├── input2.txt
│   ├── input3.txt
│   └── comprehensive-test.txt
├── src/
│   ├── MapReduce.Common/     # 共享模型、接口和工具类
│   ├── MapReduce.Examples/   # 示例实现（WordCount）
│   ├── MapReduce.Master/     # Master节点实现
│   ├── MapReduce.Worker/     # Worker节点实现
│   └── MapReduce.Tests/      # 单元测试
├── test-network.ps1          # 自动化测试脚本
└── MapReduce.sln            # 解决方案文件
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

## 🚀 快速开始

### 方法1：使用自动化测试脚本（推荐）
```powershell
# 在MapReduce根目录下运行
./test-network.ps1
```

### 方法2：手动启动

#### 📍 重要提示
- **请在MapReduce根目录下运行所有命令**
- testassets文件夹位于项目根目录

#### 步骤1：构建项目
```powershell
dotnet build
```

#### 步骤2：启动Master节点
```powershell
# 在MapReduce根目录下运行
dotnet run --project src/MapReduce.Master testassets 3
```

#### 步骤3：启动Worker节点（可启动多个）
```powershell
# 终端2（默认输出到output目录）
dotnet run --project src/MapReduce.Worker http://localhost:8080

# 终端3（可选，启动第二个Worker，指定输出目录）
dotnet run --project src/MapReduce.Worker http://localhost:8080 output

# 或者指定自定义输出目录
dotnet run --project src/MapReduce.Worker http://localhost:8080 my-output
```

### 🔍 验证运行

#### 检查Master状态
- 浏览器访问：http://localhost:8080
- API状态：http://localhost:8080/status

#### 查看输出结果
输出文件将生成在专门的output目录下：
```
MapReduce/
├── testassets/          # 输入文件目录
│   ├── input1.txt
│   ├── input2.txt
│   └── ...
└── output/              # 输出文件目录
    ├── mr-out-0         # 最终输出文件
    ├── mr-out-1
    └── ...
```

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

## ✨ 特性

- **真正的分布式架构**: Worker和Master可以运行在不同机器上
- **HTTP协议通信**: 标准的REST API，易于调试和扩展
- **多Worker并发**: 支持多个Worker节点同时工作
- **实时状态监控**: 通过Web页面查看任务进度
- **容错处理**: Worker超时检测和任务重新分配
- **心跳机制**: Worker定期发送心跳维持连接
- **优雅关闭**: 支持Ctrl+C优雅停止

## 扩展

要实现自定义的Map/Reduce逻辑：

1. 实现 `IMapFunction` 接口
2. 实现 `IReduceFunction` 接口
3. 在Worker中注册自定义函数

## ⚠️ 常见问题

### 1. Master无法找到输入文件
- 确保在MapReduce根目录下运行
- 确认testassets文件夹存在且包含.txt文件

### 2. Worker无法连接Master
- 确保Master已经启动并监听8080端口
- 检查防火墙设置

### 3. 权限问题
- 确保有权限读写testassets目录
- Windows用户可能需要以管理员身份运行

### 4. PowerShell脚本执行策略
- 如果无法运行test-network.ps1，请使用手动启动方式
- 或者设置执行策略：`Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`

## 💻 技术栈

- **框架**: .NET 8.0
- **通信协议**: HTTP/REST API
- **序列化**: System.Text.Json（.NET原生）
- **测试**: xUnit
- **网络**: HttpClient, HttpListener 