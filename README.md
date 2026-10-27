# HwOps 硬件助手（M1）

内网硬件运维工具的本机部分：采集硬件与驱动状态，匹配官方驱动下载页，导出 HTML/JSON 报告。

## 环境要求

- 目标框架：`net10.0-windows`（SDK 10 及以上构建；发布产物自包含，目标机免装 .NET 运行时）
- 运行平台：Windows 10 22H2 / Windows 11（x64）
- 依赖：`System.Management`（WMI 只读采集）

## 构建与测试

```bash
dotnet build HwOps.sln
dotnet test HwOps.sln -c Release
```

## 运行

```bash
dotnet run --project src/HwOps.Agent
```

## 无界面自检

```bash
dotnet run --project src/HwOps.Agent -c Release -- --selftest ./dist/selftest
```

自检会生成 `snapshot.json`、`report.html`、`selftest.log`，不需要人工点界面；全部成功时进程退出码为 0。发布产物同样支持：

```bash
./dist/agent/HwOps.Agent.exe --selftest ./dist/published-selftest
```

## 发布单文件

```bash
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

脚本会先跑 Release 全量测试，再清理 `dist/agent` 与历史探针目录，最后单文件自包含发布。
输出：`dist/agent/HwOps.Agent.exe`（实测约 50 MB，目标机免装 .NET 运行时）。

## 功能范围（M1）

- 采集：系统、主板、BIOS、CPU、内存、显卡、磁盘、网络、声音、全部 PnP 设备与驱动版本、机器标识
  - 11 个采集任务并行执行，整体预算 10 秒，本机实测约 2–2.6 秒完成
- 问题设备：按 `ConfigManagerErrorCode` 标记并用中文解释
- 驱动建议：内置 40 条规则（`rulesVersion 2026.10.2`），按 OEM / 主板 / 设备硬件 ID / Microsoft Update Catalog 兜底分层匹配
- 导出：单文件离线 HTML 报告、JSON 快照
- 托盘常驻，关闭窗口不退出

## 实现细节与约束

- 显卡、网卡视图已排除 `ROOT\`、`BTH\`、`USBSTOR\` 前缀的虚拟/枚举设备，避免驱动建议误导；「全部设备」列表仍是全量采集。
- 规则库为内置种子规则（嵌入资源），报告中的链接仅允许 http/https 协议白名单。
- M1 不支持外部 `rules.json` 加载与规则编辑（属 M3）。

## 不包含（M2/M3）

- M2：内网发现、配对、远程拉取、UDP 信标
- M3：规则编辑器、规则同步、外部规则加载
- 自动下载或安装驱动

## 已知限制

- 部分字段（如硬盘序列号）需要管理员权限，拿不到时标注为「需管理员」
- 规则里的厂商链接可能随官网改版失效，需要维护 `src/HwOps.Core/Rules/seed-rules.json`
- 未签名 exe 可能被 360/火绒等杀软拦截；分发前需真机验证并准备白名单材料
- 采集为只读操作，不读取文件、屏幕、账号或已安装软件清单
