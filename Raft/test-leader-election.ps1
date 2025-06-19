#!/usr/bin/env pwsh

<#
.SYNOPSIS
运行Raft Leader选举测试

.DESCRIPTION
此脚本用于运行Raft算法中Leader选举功能的测试。
它会构建项目并运行单元测试来验证选举机制的正确性。

.EXAMPLE
./test-leader-election.ps1
#>

# 设置错误行为
$ErrorActionPreference = "Stop"

# 脚本所在目录
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "🚀 开始Raft Leader选举测试..." -ForegroundColor Green

try {
    # 清理之前的构建输出
    Write-Host "🧹 清理之前的构建..." -ForegroundColor Yellow
    dotnet clean Raft.sln --verbosity quiet
    
    # 恢复NuGet包
    Write-Host "📦 恢复NuGet包..." -ForegroundColor Yellow
    dotnet restore Raft.sln --verbosity quiet
    
    # 构建解决方案
    Write-Host "🔨 构建解决方案..." -ForegroundColor Yellow
    dotnet build Raft.sln --configuration Release --no-restore --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败"
    }
    
    Write-Host "✅ 构建成功!" -ForegroundColor Green
    
    # 运行测试
    Write-Host "🧪 运行Leader选举测试..." -ForegroundColor Yellow
    dotnet test src/Raft.Tests/Raft.Tests.csproj --configuration Release --no-build --verbosity normal --logger "console;verbosity=detailed"
    
    if ($LASTEXITCODE -ne 0) {
        throw "测试失败"
    }
    
    Write-Host "🎉 所有测试通过!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Leader选举功能测试摘要:" -ForegroundColor Cyan
    Write-Host "✓ 单节点自动成为Leader" -ForegroundColor Green
    Write-Host "✓ 多节点集群选举出唯一Leader" -ForegroundColor Green
    Write-Host "✓ 投票机制正确处理不同场景" -ForegroundColor Green
    Write-Host "✓ 任期管理和状态转换正确" -ForegroundColor Green
    Write-Host "✓ 心跳机制正常工作" -ForegroundColor Green
    
}
catch {
    Write-Host "❌ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎊 Leader选举测试完成!" -ForegroundColor Green 