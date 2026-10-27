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
Write-Host "==> writing dist/agent/BUILD-INFO.txt" -ForegroundColor Cyan
# 发布溯源：把 commit 与时间写进产物目录，验收前先确认它与 HEAD 一致，
# 避免出现「dist 产物滞后于源码」的误判。
$commit = (& git rev-parse --short HEAD 2>$null)
if (-not $commit) { $commit = "unknown" }
$exe = Get-ChildItem dist/agent -Filter *.exe | Select-Object -First 1
if (-not $exe) { throw "发布目录里找不到 exe 产物。" }
$buildInfo = @(
    "commit=$($commit.ToString().Trim())",
    "builtAt=$([DateTimeOffset]::Now.ToString("yyyy-MM-ddTHH:mm:sszzz"))",
    "exe=$($exe.Name)",
    "exeBytes=$($exe.Length)"
)
# 用 .NET 写入（UTF-8 无 BOM），保证 cat / grep 在 Git Bash 下读到的第一行就是 commit=
[System.IO.File]::WriteAllLines((Join-Path (Get-Location) "dist/agent/BUILD-INFO.txt"), [string[]]$buildInfo)
$buildInfo | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "Published to dist/agent:" -ForegroundColor Green
Get-ChildItem dist/agent | ForEach-Object {
    "{0,-24} {1,10:N0} bytes ({2:N1} MB)" -f $_.Name, $_.Length, ($_.Length / 1MB)
}

exit 0
