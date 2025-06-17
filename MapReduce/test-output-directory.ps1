#!/usr/bin/env pwsh

Write-Host "=== 测试输出目录功能 ===" -ForegroundColor Green
Write-Host ""

# 清理之前的输出
if (Test-Path "output") {
    Write-Host "🧹 清理旧的output目录..." -ForegroundColor Yellow
    Remove-Item -Path "output" -Recurse -Force
}

if (Test-Path "test-output") {
    Write-Host "🧹 清理旧的test-output目录..." -ForegroundColor Yellow
    Remove-Item -Path "test-output" -Recurse -Force
}

# 清理根目录的mr-*文件
Get-ChildItem -Path . -Filter "mr-*" | Remove-Item -Force

Write-Host "✅ 清理完成" -ForegroundColor Green
Write-Host ""

# 启动Master
Write-Host "1️⃣  启动Master..." -ForegroundColor Cyan
$masterJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --project src/MapReduce.Master testassets 2
}

Start-Sleep -Seconds 3

# 启动Worker
Write-Host "2️⃣  启动Worker (输出到output目录)..." -ForegroundColor Cyan
$workerJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --project src/MapReduce.Worker http://localhost:8080 output
}

Start-Sleep -Seconds 2

# 等待任务完成
Write-Host "3️⃣  等待任务完成..." -ForegroundColor Cyan
$timeout = 30
$elapsed = 0

do {
    Start-Sleep -Seconds 2
    $elapsed += 2
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8080/completed" -TimeoutSec 5
        if ($response -eq "true") {
            Write-Host "🎉 任务已完成!" -ForegroundColor Green
            break
        }
    } catch {
        # 忽略连接错误
    }
    
    Write-Host "⏳ 等待中... ($elapsed/$timeout 秒)" -ForegroundColor Yellow
    
    if ($elapsed -ge $timeout) {
        Write-Host "⏰ 超时" -ForegroundColor Red
        break
    }
} while ($true)

Start-Sleep -Seconds 2

# 检查输出结果
Write-Host ""
Write-Host "4️⃣  检查输出结果..." -ForegroundColor Cyan

Write-Host "📂 MapReduce根目录内容:" -ForegroundColor White
Get-ChildItem -Path . -Filter "mr-*" | ForEach-Object {
    Write-Host "  ❌ $($_.Name) (不应该在根目录)" -ForegroundColor Red
}

if (Test-Path "output") {
    Write-Host "📂 output目录内容:" -ForegroundColor White
    Get-ChildItem -Path "output" | ForEach-Object {
        Write-Host "  ✅ $($_.Name)" -ForegroundColor Green
        if ($_.Name.StartsWith("mr-out-")) {
            $content = Get-Content $_.FullName | Select-Object -First 3
            foreach ($line in $content) {
                Write-Host "     $line" -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host "❌ output目录不存在" -ForegroundColor Red
}

# 清理
Write-Host ""
Write-Host "5️⃣  清理资源..." -ForegroundColor Cyan
Stop-Job $workerJob -ErrorAction SilentlyContinue
Remove-Job $workerJob -ErrorAction SilentlyContinue
Stop-Job $masterJob -ErrorAction SilentlyContinue
Remove-Job $masterJob -ErrorAction SilentlyContinue

Write-Host "✅ 测试完成!" -ForegroundColor Green 