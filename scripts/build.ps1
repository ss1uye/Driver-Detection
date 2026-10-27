# HwOps 构建与发布脚本（M1）
# 流程：Release 全量测试 -> 清理旧发布产物 -> 单文件自包含发布到 dist/agent
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "==> dotnet test (Release)" -ForegroundColor Cyan
dotnet test HwOps.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "测试未通过，终止发布。" }

# 发布前清理：删除旧发布目录与历史探针残留（保留 dist/uismoke 截图证据）
foreach ($dir in @("dist/agent", "dist/net10-size")) {
    if (Test-Path $dir) {
        Write-Host "==> removing $dir" -ForegroundColor Cyan
        Remove-Item -Recurse -Force $dir
    }
}

Write-Host "==> dotnet publish (single-file, self-contained)" -ForegroundColor Cyan
dotnet publish src/HwOps.Agent/HwOps.Agent.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist/agent
if ($LASTEXITCODE -ne 0) { throw "发布失败。" }

Write-Host ""
Write-Host "Published to dist/agent:" -ForegroundColor Green
Get-ChildItem dist/agent | ForEach-Object {
    "{0,-24} {1,10:N0} bytes ({2:N1} MB)" -f $_.Name, $_.Length, ($_.Length / 1MB)
}
