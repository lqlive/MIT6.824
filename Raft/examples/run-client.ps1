# Raft 客户端运行示例
# 这个脚本演示如何使用Raft客户端与集群交互

Write-Host "🚀 Raft 客户端使用示例" -ForegroundColor Green
Write-Host ""

# 检查是否构建了项目
Write-Host "📦 检查项目构建状态..." -ForegroundColor Yellow
$buildResult = dotnet build --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 项目构建失败，请先运行 'dotnet build'" -ForegroundColor Red
    exit 1
}

Write-Host "✅ 项目构建成功" -ForegroundColor Green
Write-Host ""

# 客户端使用说明
Write-Host "📋 Raft客户端使用方法:" -ForegroundColor Cyan
Write-Host "  1. 首先启动Raft集群节点 (需要实现服务器端)"
Write-Host "  2. 然后运行客户端连接到集群"
Write-Host ""

Write-Host "🎯 启动客户端示例命令:" -ForegroundColor Cyan
Write-Host "  dotnet run --project src/Raft.Client localhost:5001,localhost:5002,localhost:5003" -ForegroundColor White
Write-Host ""

Write-Host "💡 客户端可用命令:" -ForegroundColor Cyan
Write-Host "  • submit <命令>  - 提交命令到Raft集群" -ForegroundColor White
Write-Host "  • status         - 查看集群状态" -ForegroundColor White
Write-Host "  • exit           - 退出客户端" -ForegroundColor White
Write-Host ""

Write-Host "📝 使用示例:" -ForegroundColor Cyan
Write-Host "  raft> submit SET key1 value1" -ForegroundColor White
Write-Host "  raft> submit GET key1" -ForegroundColor White
Write-Host "  raft> status" -ForegroundColor White
Write-Host "  raft> exit" -ForegroundColor White
Write-Host ""

# 询问是否要启动客户端
$response = Read-Host "是否要启动客户端? (输入集群节点列表，例如: localhost:5001,localhost:5002,localhost:5003) [回车跳过]"

if ($response) {
    Write-Host ""
    Write-Host "🚀 启动Raft客户端..." -ForegroundColor Green
    Write-Host "连接到集群: $response" -ForegroundColor Yellow
    Write-Host ""
    
    # 启动客户端
    dotnet run --project src/Raft.Client $response
}
else {
    Write-Host ""
    Write-Host "ℹ️  跳过客户端启动。您可以稍后手动运行:" -ForegroundColor Blue
    Write-Host "   dotnet run --project src/Raft.Client <节点列表>" -ForegroundColor White
    Write-Host ""
} 