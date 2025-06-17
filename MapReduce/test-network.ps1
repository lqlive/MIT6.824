#!/usr/bin/env pwsh

Write-Host "=== MapReduce 网络版本测试脚本 ===" -ForegroundColor Green
Write-Host ""

# 检查是否在正确的目录
if (-not (Test-Path "MapReduce.sln")) {
    Write-Host "❌ 请在MapReduce目录下运行此脚本" -ForegroundColor Red
    exit 1
}

# 构建项目
Write-Host "🔨 构建项目..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 构建失败" -ForegroundColor Red
    exit 1
}

Write-Host "✅ 构建成功!" -ForegroundColor Green
Write-Host ""

# 准备测试数据
Write-Host "📁 准备测试数据..." -ForegroundColor Yellow
$testDir = "testassets"
if (-not (Test-Path $testDir)) {
    New-Item -Path $testDir -ItemType Directory -Force
}

# 创建测试文件
@"
apple banana apple
cherry date apple
banana cherry date
apple banana
"@ | Out-File -FilePath "$testDir/input1.txt" -Encoding UTF8

@"
grape orange grape  
lemon apple orange
grape lemon banana
orange grape
"@ | Out-File -FilePath "$testDir/input2.txt" -Encoding UTF8

Write-Host "✅ 测试数据准备完成!" -ForegroundColor Green
Write-Host ""

Write-Host "🚀 启动测试流程..." -ForegroundColor Yellow
Write-Host ""

# 在后台启动Master
Write-Host "1️⃣  启动Master服务..." -ForegroundColor Cyan
$masterJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --project src/MapReduce.Master testassets 3
}

# 等待Master启动
Write-Host "⏳ 等待Master启动 (5秒)..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# 测试Master连接
Write-Host "🔍 测试Master连接..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8080/status" -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Master连接成功!" -ForegroundColor Green
    }
    else {
        throw "连接失败"
    }
}
catch {
    Write-Host "❌ Master连接失败，请检查Master是否正常启动" -ForegroundColor Red
    Stop-Job $masterJob
    Remove-Job $masterJob
    exit 1
}

Write-Host ""

# 启动多个Worker
Write-Host "2️⃣  启动Worker节点..." -ForegroundColor Cyan
$workers = @()

for ($i = 1; $i -le 2; $i++) {
    Write-Host "🔧 启动Worker #$i..." -ForegroundColor Yellow
    $workerJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project src/MapReduce.Worker http://localhost:8080 output
    }
    $workers += $workerJob
    Start-Sleep -Seconds 2
}

Write-Host "✅ 已启动 $($workers.Count) 个Worker节点!" -ForegroundColor Green
Write-Host ""

# 监控任务进度
Write-Host "3️⃣  监控任务执行..." -ForegroundColor Cyan
$startTime = Get-Date
$timeout = 60 # 60秒超时

do {
    Start-Sleep -Seconds 3
    
    try {
        $statusResponse = Invoke-RestMethod -Uri "http://localhost:8080/status" -TimeoutSec 5
        $isCompleted = Invoke-RestMethod -Uri "http://localhost:8080/completed" -TimeoutSec 5
        
        Write-Host "📊 状态: $($statusResponse.CurrentPhase) | Map: $($statusResponse.CompletedMapTasks)/$($statusResponse.TotalMapTasks) | Reduce: $($statusResponse.CompletedReduceTasks)/$($statusResponse.TotalReduceTasks) | Workers: $($statusResponse.ActiveWorkers.Count)" -ForegroundColor White
        
        if ($isCompleted -eq "true") {
            Write-Host "🎉 所有任务已完成!" -ForegroundColor Green
            break
        }
    }
    catch {
        Write-Host "⚠️  无法获取状态信息" -ForegroundColor Yellow
    }
    
    $elapsed = (Get-Date) - $startTime
    if ($elapsed.TotalSeconds -gt $timeout) {
        Write-Host "⏰ 执行超时 ($timeout 秒)" -ForegroundColor Red
        break
    }
} while ($true)

Write-Host ""

# 检查结果
Write-Host "4️⃣  检查输出结果..." -ForegroundColor Cyan
$outputDir = "output"
if (Test-Path $outputDir) {
    $outputFiles = Get-ChildItem -Path $outputDir -Filter "mr-out-*" | Sort-Object Name
} else {
    $outputFiles = @()
}

if ($outputFiles.Count -gt 0) {
    Write-Host "✅ 找到 $($outputFiles.Count) 个输出文件:" -ForegroundColor Green
    foreach ($file in $outputFiles) {
        Write-Host "📄 $($file.Name):" -ForegroundColor White
        $content = Get-Content $file.FullName | Select-Object -First 10
        foreach ($line in $content) {
            Write-Host "   $line" -ForegroundColor Gray
        }
        if ((Get-Content $file.FullName).Count -gt 10) {
            Write-Host "   ..." -ForegroundColor Gray
        }
        Write-Host ""
    }
}
else {
    Write-Host "❌ 未找到输出文件" -ForegroundColor Red
}

# 清理资源
Write-Host "🧹 清理资源..." -ForegroundColor Yellow

# 停止所有Worker
foreach ($worker in $workers) {
    Stop-Job $worker -ErrorAction SilentlyContinue
    Remove-Job $worker -ErrorAction SilentlyContinue
}

# 停止Master
Stop-Job $masterJob -ErrorAction SilentlyContinue
Remove-Job $masterJob -ErrorAction SilentlyContinue

Write-Host "✅ 测试完成!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 说明:" -ForegroundColor White
Write-Host "  - Master服务运行在 http://localhost:8080" -ForegroundColor Gray
Write-Host "  - 可以在浏览器中访问查看状态页面" -ForegroundColor Gray
Write-Host "  - Worker通过HTTP协议与Master通信" -ForegroundColor Gray
Write-Host "  - 支持多个Worker并发执行任务" -ForegroundColor Gray 