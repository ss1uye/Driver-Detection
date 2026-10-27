# HwOps M1（采集与本地报告）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付可独立使用的「硬件助手」单文件程序：采集本机硬件与驱动状态，匹配驱动官方下载页，展示并导出 HTML/JSON 报告。

**Architecture:** Core 类库承载数据模型、WMI 采集、硬件 ID 解析、规则匹配与 HTML 生成；Agent 是 WinForms 外壳，只负责交互与展示。M1 不包含网络功能（M2）与规则编辑/信标（M3）。每个任务都通过可运行的测试或自检命令验证。

**Tech Stack:** C# / .NET 10（`net10.0-windows`）、WinForms、System.Management、System.Text.Json、xUnit。

---

**执行约定：**

- 本计划默认使用 git 分步提交。执行者必须先获得用户对 `git init` 与提交的明确同意；未获得同意时，把每个 Commit 步骤视为人工检查点（打印 `git status` 并等待确认），不要执行 `git commit`。
- 所有命令在仓库根目录 `D:\硬件检测` 下执行。
- 代码标识符用英文，界面文案用中文。
- 每个任务结束都要能编译；`dotnet build HwOps.sln` 必须是绿的。

## 文件结构

```
HwOps.sln
.gitignore
README.md                              # 构建、运行、自检、已知限制
scripts/build.ps1                      # 测试 + 发布单文件
src/
  HwOps.Core/
    HwOps.Core.csproj
    Models/
      HardwareSnapshot.cs              # 快照与系统/主板/BIOS/CPU/内存模型
      DeviceModels.cs                  # 显卡/磁盘/网络/声音/驱动/设备/问题设备
      JsonDefaults.cs                  # 统一 JSON 选项
      ProblemCodes.cs                  # 设备问题代码中文说明
    Collection/
      IHardwareCollector.cs
      Wmi.cs                           # WMI 查询与字段读取扩展
      MachineIdentity.cs               # 机器指纹
      WmiHardwareCollector.cs          # 采集实现
    Rules/
      DriverRule.cs                    # 规则、匹配条件、建议模型
      RuleSet.cs                       # 加载/合并/校验
      HardwareId.cs                    # PCI/USB 硬件 ID 解析
      RuleMatcher.cs                   # 匹配与 URL 渲染
      DriverAdvisor.cs                 # 面向整机的建议聚合
      seed-rules.json                  # 内置种子规则（嵌入资源）
    Reporting/
      HtmlReportBuilder.cs             # 单文件 HTML 报告
  HwOps.Agent/
    HwOps.Agent.csproj
    Program.cs                         # 入口 + --selftest
    SelfTest.cs                        # 无界面自检
    MainForm.cs                        # 主窗口
tests/
  HwOps.Core.Tests/
    HwOps.Core.Tests.csproj
    SnapshotSerializationTests.cs
    HardwareIdTests.cs
    RuleSetTests.cs
    RuleMatcherTests.cs
    DriverAdvisorTests.cs
    ProblemCodesTests.cs
    MachineIdentityTests.cs
    WmiCollectorTests.cs
    HtmlReportTests.cs
```

---

## Task 1: 初始化仓库与解决方案

**Files:**
- Create: `.gitignore`
- Create: `HwOps.sln`（命令生成）
- Create: `src/HwOps.Core/HwOps.Core.csproj`
- Create: `src/HwOps.Agent/HwOps.Agent.csproj`
- Create: `tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj`

- [ ] **Step 1: 初始化 git 与项目骨架**

```bash
cd /d/硬件检测
git init
dotnet new sln -n HwOps
dotnet new classlib -n HwOps.Core -o src/HwOps.Core -f net10.0
dotnet new winforms -n HwOps.Agent -o src/HwOps.Agent -f net10.0
dotnet new xunit -n HwOps.Core.Tests -o tests/HwOps.Core.Tests -f net10.0
dotnet sln add src/HwOps.Core/HwOps.Core.csproj src/HwOps.Agent/HwOps.Agent.csproj tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj
dotnet add src/HwOps.Core package System.Management --version 8.0.0
dotnet add src/HwOps.Agent reference src/HwOps.Core/HwOps.Core.csproj
dotnet add tests/HwOps.Core.Tests reference src/HwOps.Core/HwOps.Core.csproj
rm -f src/HwOps.Core/Class1.cs src/HwOps.Agent/Form1.cs src/HwOps.Agent/Form1.Designer.cs tests/HwOps.Core.Tests/UnitTest1.cs
```

- [ ] **Step 2: 写入 `.gitignore`**

```gitignore
bin/
obj/
dist/
.vs/
*.user
TestResults/
```

- [ ] **Step 3: 修改三个 csproj 为最终内容**

`src/HwOps.Core/HwOps.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Management" Version="8.0.0" />
  </ItemGroup>
</Project>
```

`src/HwOps.Agent/HwOps.Agent.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <UseWindowsForms>true</UseWindowsForms>
    <AssemblyName>HwOps.Agent</AssemblyName>
    <RootNamespace>HwOps.Agent</RootNamespace>
    <Product>硬件助手</Product>
    <AssemblyTitle>硬件助手</AssemblyTitle>
    <Version>0.1.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\HwOps.Core\HwOps.Core.csproj" />
  </ItemGroup>
</Project>
```

`tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\HwOps.Core\HwOps.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: 建立 Program.cs 占位入口并确认编译**

`src/HwOps.Agent/Program.cs`:

```csharp
namespace HwOps.Agent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form { Text = "硬件助手（M1 骨架）" });
        return 0;
    }
}
```

Run:

```bash
dotnet build HwOps.sln
```

Expected: `Build succeeded`，0 errors。

- [ ] **Step 5: Commit**

```bash
git add .gitignore HwOps.sln src tests
git commit -m "chore: scaffold HwOps solution (core, agent, tests)"
```

---

## Task 2: 数据模型与 JSON 序列化

**Files:**
- Create: `src/HwOps.Core/Models/HardwareSnapshot.cs`
- Create: `src/HwOps.Core/Models/DeviceModels.cs`
- Create: `src/HwOps.Core/Models/JsonDefaults.cs`
- Test: `tests/HwOps.Core.Tests/SnapshotSerializationTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/SnapshotSerializationTests.cs`:

```csharp
using HwOps.Core.Models;

namespace HwOps.Core.Tests;

public class SnapshotSerializationTests
{
    [Fact]
    public void Serialize_UsesCamelCase_AndRoundTrips()
    {
        var snapshot = new HardwareSnapshot
        {
            MachineId = "abc123",
            CollectedAt = DateTimeOffset.Parse("2026-10-27T09:00:00+08:00"),
            System = new MachineSystemInfo
            {
                Hostname = "PC-01",
                Manufacturer = "ASUS",
                Model = "B560M"
            },
            Devices =
            {
                new DeviceInfo
                {
                    Name = "RTX 3060",
                    ClassName = "Display",
                    PnpId = "PCI\\VEN_10DE&DEV_2504",
                    ProblemCode = 0
                }
            }
        };

        var json = JsonDefaults.Serialize(snapshot);

        Assert.Contains("\"machineId\": \"abc123\"", json);
        Assert.Contains("\"hostname\": \"PC-01\"", json);

        var back = JsonDefaults.Deserialize<HardwareSnapshot>(json);

        Assert.NotNull(back);
        Assert.Equal("PC-01", back!.System.Hostname);
        Assert.Single(back.Devices);
        Assert.Equal("PCI\\VEN_10DE&DEV_2504", back.Devices[0].PnpId);
    }

    [Fact]
    public void Deserialize_AllowsMissingOptionals()
    {
        var snapshot = JsonDefaults.Deserialize<HardwareSnapshot>("""{"schemaVersion":1,"machineId":"x"}""");

        Assert.NotNull(snapshot);
        Assert.Equal("x", snapshot!.MachineId);
        Assert.Null(snapshot.System.Hostname);
        Assert.Empty(snapshot.Devices);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~SnapshotSerializationTests" -v minimal
```

Expected: 编译失败，`HardwareSnapshot` / `JsonDefaults` 未定义。

- [ ] **Step 3: 实现模型**

`src/HwOps.Core/Models/HardwareSnapshot.cs`:

```csharp
namespace HwOps.Core.Models;

public sealed class HardwareSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string MachineId { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public MachineSystemInfo System { get; set; } = new();
    public BoardInfo Board { get; set; } = new();
    public BiosInfo Bios { get; set; } = new();
    public List<CpuInfo> Cpu { get; set; } = new();
    public List<MemoryModuleInfo> Memory { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<NetworkAdapterInfo> Network { get; set; } = new();
    public List<SoundDeviceInfo> Sound { get; set; } = new();
    public List<DeviceInfo> Devices { get; set; } = new();
    public List<ProblemDevice> ProblemDevices { get; set; } = new();
    public CollectionInfo Collection { get; set; } = new();
}

public sealed class MachineSystemInfo
{
    public string? Hostname { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
    public string? SmbiosUuid { get; set; }
    public OsInfo Os { get; set; } = new();
}

public sealed class OsInfo
{
    public string? Caption { get; set; }
    public string? Version { get; set; }
    public string? Build { get; set; }
    public string? Arch { get; set; }
}

public sealed class BoardInfo
{
    public string? Manufacturer { get; set; }
    public string? Product { get; set; }
    public string? Version { get; set; }
    public string? Serial { get; set; }
}

public sealed class BiosInfo
{
    public string? Vendor { get; set; }
    public string? Version { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Serial { get; set; }
}

public sealed class CpuInfo
{
    public string? Name { get; set; }
    public int? Cores { get; set; }
    public int? Threads { get; set; }
    public int? MaxClockMhz { get; set; }
}

public sealed class MemoryModuleInfo
{
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public long? CapacityBytes { get; set; }
    public int? SpeedMhz { get; set; }
    public string? Slot { get; set; }
    public string? FormFactor { get; set; }
}

public sealed class CollectionInfo
{
    public List<string> Warnings { get; set; } = new();
}
```

`src/HwOps.Core/Models/DeviceModels.cs`:

```csharp
namespace HwOps.Core.Models;

public sealed class GpuInfo
{
    public string? Name { get; set; }
    public string? DriverVersion { get; set; }
    public string? DriverDate { get; set; }
    public string? PnpId { get; set; }
    public string? Status { get; set; }
}

public sealed class DiskInfo
{
    public string? Model { get; set; }
    public string? MediaType { get; set; }
    public string? InterfaceType { get; set; }
    public long? SizeBytes { get; set; }
    public string? PnpId { get; set; }
    public string? Serial { get; set; }
}

public sealed class NetworkAdapterInfo
{
    public string? Name { get; set; }
    public string? Mac { get; set; }
    public string? PnpId { get; set; }
    public string? ConnectionName { get; set; }
    public bool? Enabled { get; set; }
    public long? SpeedBps { get; set; }
}

public sealed class SoundDeviceInfo
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? PnpId { get; set; }
    public string? Status { get; set; }
}

public sealed class DriverInfo
{
    public string? Version { get; set; }
    public string? Date { get; set; }
    public string? Provider { get; set; }
    public string? Inf { get; set; }
    public bool? Signed { get; set; }
}

public sealed class DeviceInfo
{
    public string ClassName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string PnpId { get; set; } = "";
    public string? Status { get; set; }
    public int ProblemCode { get; set; }
    public DriverInfo? Driver { get; set; }
}

public sealed class ProblemDevice
{
    public string PnpId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int ProblemCode { get; set; }
    public string ProblemText { get; set; } = "";
}
```

`src/HwOps.Core/Models/JsonDefaults.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HwOps.Core.Models;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
```

- [ ] **Step 4: 运行测试确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~SnapshotSerializationTests" -v minimal
```

Expected: 2 passed。

- [ ] **Step 5: Commit**

```bash
git add src/HwOps.Core/Models tests/HwOps.Core.Tests/SnapshotSerializationTests.cs
git commit -m "feat: add hardware snapshot models and JSON defaults"
```

---

## Task 3: 硬件 ID 解析

**Files:**
- Create: `src/HwOps.Core/Rules/HardwareId.cs`
- Test: `tests/HwOps.Core.Tests/HardwareIdTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/HardwareIdTests.cs`:

```csharp
using HwOps.Core.Rules;

namespace HwOps.Core.Tests;

public class HardwareIdTests
{
    [Theory]
    [InlineData("PCI\\VEN_10DE&DEV_2504&SUBSYS_146219DA&REV_A1", "10DE", "2504", "146219DA")]
    [InlineData("pci\\ven_8086&dev_a0f0", "8086", "A0F0", null)]
    [InlineData("PCI\\VEN_10EC&DEV_8168", "10EC", "8168", null)]
    public void Parse_Pci_ExtractsIds(string raw, string vendor, string device, string? subsystem)
    {
        var id = HardwareId.Parse(raw);

        Assert.True(id.IsPci);
        Assert.Equal(vendor, id.VendorId);
        Assert.Equal(device, id.DeviceId);
        Assert.Equal(subsystem, id.SubSystemId);
    }

    [Fact]
    public void Parse_Usb_ExtractsVidPid()
    {
        var id = HardwareId.Parse("USB\\VID_8087&PID_0026&REV_0001");

        Assert.True(id.IsUsb);
        Assert.Equal("8087", id.UsbVendorId);
        Assert.Equal("0026", id.UsbProductId);
    }

    [Fact]
    public void Parse_Empty_ReturnsEmptyIds()
    {
        var id = HardwareId.Parse(null);

        Assert.False(id.IsPci);
        Assert.False(id.IsUsb);
        Assert.Null(id.VendorId);
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~HardwareIdTests" -v minimal
```

Expected: 编译失败，`HardwareId` 未定义。

- [ ] **Step 3: 实现解析器**

`src/HwOps.Core/Rules/HardwareId.cs`:

```csharp
using System.Text.RegularExpressions;

namespace HwOps.Core.Rules;

public sealed class HardwareId
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public string Raw { get; }
    public string? VendorId { get; }
    public string? DeviceId { get; }
    public string? SubSystemId { get; }
    public string? UsbVendorId { get; }
    public string? UsbProductId { get; }
    public bool IsPci => Raw.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase);
    public bool IsUsb => Raw.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase);

    private HardwareId(string raw, string? vendorId, string? deviceId, string? subSystemId, string? usbVendorId, string? usbProductId)
    {
        Raw = raw;
        VendorId = vendorId;
        DeviceId = deviceId;
        SubSystemId = subSystemId;
        UsbVendorId = usbVendorId;
        UsbProductId = usbProductId;
    }

    public static HardwareId Parse(string? raw)
    {
        var value = raw?.Trim() ?? "";
        return new HardwareId(
            value,
            Find(value, @"VEN_([0-9A-Fa-f]{4})"),
            Find(value, @"DEV_([0-9A-Fa-f]{4})"),
            Find(value, @"SUBSYS_([0-9A-Fa-f]{8})"),
            Find(value, @"VID_([0-9A-Fa-f]{4})"),
            Find(value, @"PID_([0-9A-Fa-f]{4})"));
    }

    private static string? Find(string input, string pattern)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase, RegexTimeout);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
}
```

- [ ] **Step 4: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~HardwareIdTests" -v minimal
```

Expected: 5 passed。

- [ ] **Step 5: Commit**

```bash
git add src/HwOps.Core/Rules/HardwareId.cs tests/HwOps.Core.Tests/HardwareIdTests.cs
git commit -m "feat: parse PCI and USB hardware IDs"
```

---

## Task 4: 规则模型、种子规则与加载合并

> 实现备注（质量审查后加固）：`RuleSet.Parse` 额外校验 tier 1–5、type 白名单、title 非空、URL 必须 http/https、重复 id（忽略大小写）；`MergeWith` 在外部无 `rulesVersion` 时保留基版本。最终实现见提交 `4759dc6`，8 条厂商搜索页规则为 tier 4。链接可达性核查后修正了 2 条失效链接（acer 500、goodix 404），种子版本升至 `2026.10.3`。

**Files:**
- Create: `src/HwOps.Core/Rules/DriverRule.cs`
- Create: `src/HwOps.Core/Rules/RuleSet.cs`
- Create: `src/HwOps.Core/Rules/seed-rules.json`
- Modify: `src/HwOps.Core/HwOps.Core.csproj`
- Test: `tests/HwOps.Core.Tests/RuleSetTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/RuleSetTests.cs`:

```csharp
using HwOps.Core.Rules;

namespace HwOps.Core.Tests;

public class RuleSetTests
{
    [Fact]
    public void LoadEmbedded_ContainsSeedRules()
    {
        var rules = RuleSet.LoadEmbedded();

        Assert.False(string.IsNullOrWhiteSpace(rules.RulesVersion));
        Assert.True(rules.Rules.Count >= 30, $"seed rules count = {rules.Rules.Count}");
        Assert.Contains(rules.Rules, r => r.Match.Type == "board");
        Assert.Contains(rules.Rules, r => r.Match.Type == "oem");
        Assert.Contains(rules.Rules, r => r.Match.Type == "pci");
        Assert.Contains(rules.Rules, r => r.Match.Type == "any");
    }

    [Fact]
    public void MergeWith_ReplacesSameId_AndUsesNewVersion()
    {
        var baseSet = RuleSet.Parse("""
        {"rulesVersion":"1","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A","urlTemplate":"https://a"},
          {"id":"b","tier":1,"match":{"type":"any"},"title":"B","urlTemplate":"https://b"}
        ]}
        """);
        var external = RuleSet.Parse("""
        {"rulesVersion":"2","rules":[
          {"id":"a","tier":1,"match":{"type":"any"},"title":"A2","urlTemplate":"https://a2"}
        ]}
        """);

        var merged = baseSet.MergeWith(external);

        Assert.Equal("2", merged.RulesVersion);
        Assert.Equal(2, merged.Rules.Count);
        Assert.Equal("A2", merged.Rules.Single(r => r.Id == "a").Title);
        Assert.Equal("https://b", merged.Rules.Single(r => r.Id == "b").UrlTemplate);
    }

    [Fact]
    public void Parse_RejectsRuleWithoutUrl()
    {
        var json = """{"rulesVersion":"1","rules":[{"id":"x","tier":1,"match":{"type":"any"},"title":"X"}]}""";

        Assert.Throws<InvalidDataException>(() => RuleSet.Parse(json));
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~RuleSetTests" -v minimal
```

Expected: 编译失败，`RuleSet` 未定义。

- [ ] **Step 3: 实现规则模型**

`src/HwOps.Core/Rules/DriverRule.cs`:

```csharp
namespace HwOps.Core.Rules;

public sealed class DriverRule
{
    public string Id { get; set; } = "";
    public int Tier { get; set; }
    public RuleMatch Match { get; set; } = new();
    public string Title { get; set; } = "";
    public string UrlTemplate { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class RuleMatch
{
    public string Type { get; set; } = "any";
    public string? VendorId { get; set; }
    public string? DeviceId { get; set; }
    public string? ManufacturerPattern { get; set; }
    public string? ProductPattern { get; set; }
    public string? ClassPattern { get; set; }
}

public sealed class DriverSuggestion
{
    public string RuleId { get; set; } = "";
    public int Tier { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Note { get; set; }
    public string MatchReason { get; set; } = "";
}
```

- [ ] **Step 4: 实现 RuleSet**

`src/HwOps.Core/Rules/RuleSet.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class RuleSet
{
    public string RulesVersion { get; set; } = "0";
    public List<DriverRule> Rules { get; set; } = new();

    public static RuleSet LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("HwOps.Core.Rules.seed-rules.json")
            ?? throw new InvalidOperationException("内置规则资源缺失：HwOps.Core.Rules.seed-rules.json");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static RuleSet LoadFile(string path) => Parse(File.ReadAllText(path));

    public static RuleSet Parse(string json)
    {
        var set = JsonSerializer.Deserialize<RuleSet>(json, JsonDefaults.Options)
            ?? throw new InvalidDataException("规则 JSON 为空");

        foreach (var rule in set.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidDataException("规则缺少 id");
            }

            if (string.IsNullOrWhiteSpace(rule.UrlTemplate))
            {
                throw new InvalidDataException($"规则 {rule.Id} 缺少 urlTemplate");
            }
        }

        return set;
    }

    public RuleSet MergeWith(RuleSet overrideSet)
    {
        var merged = Rules.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in overrideSet.Rules)
        {
            merged[rule.Id] = rule;
        }

        return new RuleSet
        {
            RulesVersion = string.IsNullOrWhiteSpace(overrideSet.RulesVersion) ? RulesVersion : overrideSet.RulesVersion,
            Rules = merged.Values
                .OrderBy(r => r.Tier)
                .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
```

- [ ] **Step 5: 注册嵌入资源并写种子规则**

先在 `src/HwOps.Core/HwOps.Core.csproj` 的 `</Project>` 前加入：

```xml
  <ItemGroup>
    <EmbeddedResource Include="Rules\seed-rules.json" />
  </ItemGroup>
```

再创建 `src/HwOps.Core/Rules/seed-rules.json`（每条一行，便于后续维护）：

```json
{
  "rulesVersion": "2026.10.3",
  "rules": [
    { "id": "oem-lenovo", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "Lenovo|联想" }, "title": "联想官方驱动支持", "urlTemplate": "https://newsupport.lenovo.com.cn/", "note": "输入主机编号或选择机型，进入驱动下载" },
    { "id": "oem-dell", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "Dell|戴尔" }, "title": "戴尔官方驱动支持", "urlTemplate": "https://www.dell.com/support/home/zh-cn", "note": "用服务编号或型号查找驱动" },
    { "id": "oem-hp", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "HP|Hewlett|惠普" }, "title": "惠普官方驱动支持", "urlTemplate": "https://support.hp.com/cn-zh/drivers", "note": "输入产品名称或序列号" },
    { "id": "oem-asus", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "ASUS|ASUSTeK|华硕" }, "title": "华硕官方驱动支持", "urlTemplate": "https://www.asus.com.cn/support/", "note": "搜索机型后进入驱动程序和工具" },
    { "id": "oem-acer", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "Acer|宏碁" }, "title": "宏碁官方驱动支持", "urlTemplate": "https://www.acer.com.cn/support.html", "note": "按机型查找驱动" },
    { "id": "oem-huawei", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "Huawei|华为" }, "title": "华为官方支持", "urlTemplate": "https://consumer.huawei.com/cn/support/", "note": "搜索 MateBook 型号" },
    { "id": "oem-xiaomi", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "Xiaomi|小米|Redmi" }, "title": "小米笔记本服务支持", "urlTemplate": "https://www.mi.com/service/bijiben/", "note": "选择型号进入驱动下载" },
    { "id": "oem-honor", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "HONOR|荣耀" }, "title": "荣耀官方支持", "urlTemplate": "https://www.honor.com/cn/support/", "note": "搜索 MagicBook 型号" },
    { "id": "oem-mechrevo", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "MECHREVO|机械革命" }, "title": "机械革命驱动下载", "urlTemplate": "https://www.mechrevo.com/", "note": "进入服务支持，按型号下载" },
    { "id": "oem-hasee", "tier": 2, "match": { "type": "oem", "manufacturerPattern": "Hasee|神舟" }, "title": "神舟驱动下载", "urlTemplate": "https://www.hasee.com/", "note": "进入服务支持，按型号下载" },
    { "id": "board-asus", "tier": 3, "match": { "type": "board", "manufacturerPattern": "ASUS|ASUSTeK|华硕" }, "title": "华硕主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.asus.com.cn/support/", "note": "在支持页搜索 {boardProduct}，进入驱动程序和工具" },
    { "id": "board-msi", "tier": 3, "match": { "type": "board", "manufacturerPattern": "MSI|Micro-Star|微星" }, "title": "微星主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.msi.cn/support/", "note": "搜索 {boardProduct} 后选择驱动" },
    { "id": "board-gigabyte", "tier": 3, "match": { "type": "board", "manufacturerPattern": "Gigabyte|技嘉" }, "title": "技嘉主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.gigabyte.cn/Support", "note": "搜索 {boardProduct} 后进入支持与下载" },
    { "id": "board-asrock", "tier": 3, "match": { "type": "board", "manufacturerPattern": "ASRock|华擎" }, "title": "华擎主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.asrock.com/support/index.cn.asp", "note": "按型号选择驱动" },
    { "id": "board-colorful", "tier": 3, "match": { "type": "board", "manufacturerPattern": "Colorful|七彩虹" }, "title": "七彩虹主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.colorful.cn/", "note": "进入服务支持，按型号查找驱动" },
    { "id": "board-maxsun", "tier": 3, "match": { "type": "board", "manufacturerPattern": "MAXSUN|铭瑄" }, "title": "铭瑄主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.maxsun.com.cn/", "note": "进入下载中心，按型号查找" },
    { "id": "board-onda", "tier": 3, "match": { "type": "board", "manufacturerPattern": "ONDA|昂达" }, "title": "昂达主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.onda.cn/", "note": "进入服务支持，按型号查找" },
    { "id": "board-biostar", "tier": 3, "match": { "type": "board", "manufacturerPattern": "Biostar|映泰" }, "title": "映泰主板 {boardProduct} 驱动下载", "urlTemplate": "https://www.biostar.com.tw/app/en/support/index.php", "note": "按型号选择驱动" },
    { "id": "gpu-nvidia", "tier": 1, "match": { "type": "pci", "vendorId": "10DE", "classPattern": "^Display$" }, "title": "NVIDIA 官方驱动下载", "urlTemplate": "https://www.nvidia.cn/Download/index.aspx?lang=cn", "note": "按显卡型号选择产品系列" },
    { "id": "gpu-amd", "tier": 1, "match": { "type": "pci", "vendorId": "1002", "classPattern": "^Display$" }, "title": "AMD 官方驱动下载", "urlTemplate": "https://www.amd.com/zh-cn/support/download/drivers.html", "note": "选择显卡系列与型号" },
    { "id": "gpu-intel", "tier": 1, "match": { "type": "pci", "vendorId": "8086", "classPattern": "^Display$" }, "title": "Intel 显卡驱动下载", "urlTemplate": "https://www.intel.cn/content/www/cn/zh/download-center/home.html", "note": "搜索核显型号或使用驱动助手" },
    { "id": "wifi-intel", "tier": 1, "match": { "type": "pci", "vendorId": "8086", "classPattern": "^Net$" }, "title": "Intel 无线网卡驱动", "urlTemplate": "https://www.intel.cn/content/www/cn/zh/download-center/home.html", "note": "搜索网卡型号（如 AX201）" },
    { "id": "nic-realtek", "tier": 1, "match": { "type": "pci", "vendorId": "10EC", "classPattern": "^Net$" }, "title": "Realtek 网卡驱动", "urlTemplate": "https://www.realtek.com/Download/List?cate_id=584", "note": "选择对应网卡系列下载" },
    { "id": "wifi-realtek-usb", "tier": 1, "match": { "type": "usb", "vendorId": "0BDA", "classPattern": "^Net$" }, "title": "Realtek USB 无线网卡驱动", "urlTemplate": "https://www.realtek.com/Download/List?cate_id=584", "note": "选择对应无线网卡系列" },
    { "id": "wifi-mediatek", "tier": 1, "match": { "type": "pci", "vendorId": "14C3", "classPattern": "^Net$" }, "title": "MediaTek 无线网卡驱动", "urlTemplate": "https://www.mediatek.cn/", "note": "按网卡型号在支持页查找" },
    { "id": "wifi-qualcomm", "tier": 1, "match": { "type": "pci", "vendorId": "168C", "classPattern": "^Net$" }, "title": "Qualcomm 无线网卡驱动", "urlTemplate": "https://www.qualcomm.com/support", "note": "按网卡型号查找，或使用整机厂商页面" },
    { "id": "wifi-broadcom", "tier": 1, "match": { "type": "pci", "vendorId": "14E4", "classPattern": "^Net$" }, "title": "Broadcom 网卡驱动", "urlTemplate": "https://www.broadcom.com/support/download-search", "note": "按芯片型号搜索" },
    { "id": "bt-intel", "tier": 1, "match": { "type": "usb", "vendorId": "8087", "classPattern": "^Bluetooth$" }, "title": "Intel 蓝牙驱动", "urlTemplate": "https://www.intel.cn/content/www/cn/zh/download-center/home.html", "note": "搜索蓝牙型号" },
    { "id": "bt-realtek", "tier": 1, "match": { "type": "usb", "vendorId": "0BDA", "classPattern": "^Bluetooth$" }, "title": "Realtek 蓝牙驱动", "urlTemplate": "https://www.realtek.com/Download/List?cate_id=585", "note": "选择蓝牙芯片系列" },
    { "id": "bt-mediatek", "tier": 1, "match": { "type": "usb", "vendorId": "0E8D", "classPattern": "^Bluetooth$" }, "title": "MediaTek 蓝牙驱动", "urlTemplate": "https://www.mediatek.cn/", "note": "按芯片型号在支持页查找" },
    { "id": "audio-realtek", "tier": 1, "match": { "type": "pci", "vendorId": "10EC", "classPattern": "^Media$" }, "title": "Realtek 声卡驱动", "urlTemplate": "https://www.realtek.com/Download/List?cate_id=594", "note": "选择 High Definition Audio 系列" },
    { "id": "audio-conexant", "tier": 4, "match": { "type": "pci", "vendorId": "14F1", "classPattern": "^Media$" }, "title": "Conexant 声卡驱动", "urlTemplate": "https://www.conexant.com/", "note": "建议优先使用整机厂商页面" },
    { "id": "chipset-intel", "tier": 4, "match": { "type": "pci", "vendorId": "8086", "classPattern": "^System$" }, "title": "Intel 芯片组驱动", "urlTemplate": "https://www.intel.cn/content/www/cn/zh/download-center/home.html", "note": "搜索芯片组型号；主板厂商页面通常更准确" },
    { "id": "chipset-amd", "tier": 4, "match": { "type": "pci", "vendorId": "1022", "classPattern": "^System$" }, "title": "AMD 芯片组驱动", "urlTemplate": "https://www.amd.com/zh-cn/support/download/drivers.html", "note": "选择芯片组型号；主板厂商页面通常更准确" },
    { "id": "touchpad-synaptics", "tier": 4, "match": { "type": "usb", "vendorId": "06CB", "classPattern": "^Mouse$" }, "title": "Synaptics 触控板驱动", "urlTemplate": "https://www.synaptics.com/products/touchpad-family", "note": "建议优先使用笔记本厂商页面" },
    { "id": "touchpad-elan", "tier": 4, "match": { "type": "usb", "vendorId": "04F3", "classPattern": "^Mouse$" }, "title": "ELAN 触控板驱动", "urlTemplate": "https://www.elantech.com/", "note": "建议优先使用笔记本厂商页面" },
    { "id": "fingerprint-goodix", "tier": 4, "match": { "type": "usb", "vendorId": "27C6", "classPattern": "^Biometric$" }, "title": "Goodix 指纹驱动", "urlTemplate": "https://www.goodix.com/zh/support", "note": "建议优先使用笔记本厂商页面" },
    { "id": "camera-sonix", "tier": 4, "match": { "type": "usb", "vendorId": "0C45", "classPattern": "^Camera$|^Image$" }, "title": "Sonix 摄像头驱动", "urlTemplate": "https://www.sonix.com.tw/", "note": "建议优先使用笔记本厂商页面" },
    { "id": "camera-sunplus", "tier": 4, "match": { "type": "usb", "vendorId": "1BCF", "classPattern": "^Camera$|^Image$" }, "title": "Sunplus 摄像头驱动", "urlTemplate": "https://www.sunplus.com/", "note": "建议优先使用笔记本厂商页面" },
    { "id": "catalog-fallback", "tier": 5, "match": { "type": "any" }, "title": "Microsoft Update Catalog 搜索 {hardwareId}", "urlTemplate": "https://www.catalog.update.microsoft.com/Search.aspx?q={hardwareId}", "note": "用硬件 ID 搜索官方驱动包，适合未知设备" }
  ]
}
```

- [ ] **Step 6: 运行测试确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~RuleSetTests" -v minimal
```

Expected: 3 passed。

- [ ] **Step 7: Commit**

```bash
git add src/HwOps.Core/Rules/DriverRule.cs src/HwOps.Core/Rules/RuleSet.cs src/HwOps.Core/Rules/seed-rules.json src/HwOps.Core/HwOps.Core.csproj tests/HwOps.Core.Tests/RuleSetTests.cs
git commit -m "feat: add driver rule model and seed rule set"
```

---

## Task 5: 规则匹配与 URL 渲染

**Files:**
- Create: `src/HwOps.Core/Rules/RuleMatcher.cs`
- Test: `tests/HwOps.Core.Tests/RuleMatcherTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/RuleMatcherTests.cs`:

```csharp
using HwOps.Core.Models;
using HwOps.Core.Rules;

namespace HwOps.Core.Tests;

public class RuleMatcherTests
{
    private static RuleSet Rules(params DriverRule[] rules) => new()
    {
        RulesVersion = "test",
        Rules = rules.ToList()
    };

    [Fact]
    public void SuggestForMachine_MatchesBoardPattern_AndRendersProduct()
    {
        var rules = Rules(new DriverRule
        {
            Id = "board-asus",
            Tier = 3,
            Match = new RuleMatch { Type = "board", ManufacturerPattern = "ASUSTeK|ASUS" },
            Title = "华硕 {boardProduct} 驱动",
            UrlTemplate = "https://example.com/download/{boardProduct}"
        });
        var snapshot = new HardwareSnapshot
        {
            Board = new BoardInfo { Manufacturer = "ASUSTeK COMPUTER INC.", Product = "PRIME B560M-A" }
        };

        var suggestion = new RuleMatcher(rules).SuggestForMachine(snapshot).Single();

        Assert.Equal("华硕 PRIME B560M-A 驱动", suggestion.Title);
        Assert.Equal("https://example.com/download/PRIME%20B560M-A", suggestion.Url);
        Assert.Equal("主板型号匹配", suggestion.MatchReason);
    }

    [Fact]
    public void SuggestForDevice_MatchesPciVendorAndClass()
    {
        var rules = Rules(new DriverRule
        {
            Id = "gpu-nvidia",
            Tier = 1,
            Match = new RuleMatch { Type = "pci", VendorId = "10DE", ClassPattern = "^Display$" },
            Title = "NVIDIA 驱动",
            UrlTemplate = "https://example.com/{vendorId}/{deviceId}"
        });
        var device = new DeviceInfo
        {
            ClassName = "Display",
            PnpId = "PCI\\VEN_10DE&DEV_2504&SUBSYS_146219DA"
        };
        var snapshot = new HardwareSnapshot { Devices = { device } };

        var suggestion = new RuleMatcher(rules).SuggestForDevice(snapshot, device).Single();

        Assert.Equal("https://example.com/10DE/2504", suggestion.Url);
    }

    [Fact]
    public void SuggestForDevice_RespectsClassFilter()
    {
        var rules = Rules(new DriverRule
        {
            Id = "gpu-intel",
            Tier = 1,
            Match = new RuleMatch { Type = "pci", VendorId = "8086", ClassPattern = "^Display$" },
            Title = "Intel 显卡",
            UrlTemplate = "https://example.com/intel"
        });
        var nic = new DeviceInfo { ClassName = "Net", PnpId = "PCI\\VEN_8086&DEV_A0F0" };
        var snapshot = new HardwareSnapshot { Devices = { nic } };

        Assert.Empty(new RuleMatcher(rules).SuggestForDevice(snapshot, nic));
    }

    [Fact]
    public void SuggestForDevice_FallbackAny_EncodesHardwareId()
    {
        var rules = Rules(new DriverRule
        {
            Id = "fallback",
            Tier = 5,
            Match = new RuleMatch { Type = "any" },
            Title = "Catalog {hardwareId}",
            UrlTemplate = "https://example.com/search?q={hardwareId}"
        });
        var device = new DeviceInfo { ClassName = "USB", PnpId = "USB\\VID_1234&PID_5678" };
        var snapshot = new HardwareSnapshot { Devices = { device } };

        var suggestion = new RuleMatcher(rules).SuggestForDevice(snapshot, device).Single();

        Assert.Equal("https://example.com/search?q=USB%5CVID_1234%26PID_5678", suggestion.Url);
    }

    [Fact]
    public void InvalidRegex_DoesNotMatch_AndDoesNotThrow()
    {
        var rules = Rules(new DriverRule
        {
            Id = "bad",
            Tier = 3,
            Match = new RuleMatch { Type = "board", ManufacturerPattern = "([" },
            Title = "Bad",
            UrlTemplate = "https://example.com"
        });
        var snapshot = new HardwareSnapshot { Board = new BoardInfo { Manufacturer = "ASUS" } };

        Assert.Empty(new RuleMatcher(rules).SuggestForMachine(snapshot));
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~RuleMatcherTests" -v minimal
```

Expected: 编译失败，`RuleMatcher` 未定义。

- [ ] **Step 3: 实现匹配器**

`src/HwOps.Core/Rules/RuleMatcher.cs`:

```csharp
using System.Text.RegularExpressions;
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class RuleMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    private readonly IReadOnlyList<DriverRule> _rules;

    public RuleMatcher(RuleSet ruleSet)
    {
        _rules = ruleSet.Rules
            .OrderBy(r => r.Tier)
            .ThenByDescending(Specificity)
            .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<DriverSuggestion> SuggestForMachine(HardwareSnapshot snapshot)
    {
        var result = new List<DriverSuggestion>();
        foreach (var rule in _rules)
        {
            var type = rule.Match.Type?.ToLowerInvariant();
            if (type is not ("oem" or "board"))
            {
                continue;
            }

            if (!Matches(rule, snapshot, null))
            {
                continue;
            }

            result.Add(ToSuggestion(rule, snapshot, null, type == "oem" ? "整机厂商匹配" : "主板型号匹配"));
        }

        return result;
    }

    public IReadOnlyList<DriverSuggestion> SuggestForDevice(HardwareSnapshot snapshot, DeviceInfo device)
    {
        var result = new List<DriverSuggestion>();
        foreach (var rule in _rules)
        {
            var type = rule.Match.Type?.ToLowerInvariant();
            if (type is "oem" or "board")
            {
                continue;
            }

            if (!Matches(rule, snapshot, device))
            {
                continue;
            }

            result.Add(ToSuggestion(rule, snapshot, device, "硬件 ID 匹配"));
        }

        return result;
    }

    private static bool Matches(DriverRule rule, HardwareSnapshot snapshot, DeviceInfo? device)
    {
        var match = rule.Match;
        var type = match.Type?.ToLowerInvariant() ?? "any";
        return type switch
        {
            "oem" => MachinePatternMatch(snapshot.System.Manufacturer, snapshot.System.Model, match),
            "board" => MachinePatternMatch(snapshot.Board.Manufacturer, snapshot.Board.Product, match),
            "pci" => device is not null && MatchPci(device, match),
            "usb" => device is not null && MatchUsb(device, match),
            "class" => device is not null && Pattern(device.ClassName, match.ClassPattern),
            "any" => true,
            _ => false
        };
    }

    private static bool MachinePatternMatch(string? manufacturer, string? model, RuleMatch match)
    {
        if (string.IsNullOrWhiteSpace(match.ManufacturerPattern) && string.IsNullOrWhiteSpace(match.ProductPattern))
        {
            return false;
        }

        return Pattern(manufacturer, match.ManufacturerPattern) && Pattern(model, match.ProductPattern);
    }

    private static bool MatchPci(DeviceInfo device, RuleMatch match)
    {
        var id = HardwareId.Parse(device.PnpId);
        if (!id.IsPci)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.VendorId) && !SameId(id.VendorId, match.VendorId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.DeviceId) && !SameId(id.DeviceId, match.DeviceId))
        {
            return false;
        }

        return Pattern(device.ClassName, match.ClassPattern);
    }

    private static bool MatchUsb(DeviceInfo device, RuleMatch match)
    {
        var id = HardwareId.Parse(device.PnpId);
        if (!id.IsUsb)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.VendorId) && !SameId(id.UsbVendorId, match.VendorId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(match.DeviceId) && !SameId(id.UsbProductId, match.DeviceId))
        {
            return false;
        }

        return Pattern(device.ClassName, match.ClassPattern);
    }

    private static bool SameId(string? left, string right)
        => string.Equals(left?.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Pattern(string? input, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static DriverSuggestion ToSuggestion(DriverRule rule, HardwareSnapshot snapshot, DeviceInfo? device, string reason)
        => new()
        {
            RuleId = rule.Id,
            Tier = rule.Tier,
            Title = Render(rule.Title, snapshot, device, escape: false),
            Url = Render(rule.UrlTemplate, snapshot, device, escape: true),
            Note = rule.Note,
            MatchReason = reason
        };

    private static string Render(string template, HardwareSnapshot snapshot, DeviceInfo? device, bool escape)
    {
        var hardwareId = device is null ? null : HardwareId.Parse(device.PnpId);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["hardwareId"] = device?.PnpId,
            ["className"] = device?.ClassName,
            ["deviceName"] = device?.Name,
            ["boardManufacturer"] = snapshot.Board.Manufacturer,
            ["boardProduct"] = snapshot.Board.Product,
            ["oemManufacturer"] = snapshot.System.Manufacturer,
            ["oemModel"] = snapshot.System.Model,
            ["serial"] = snapshot.System.Serial,
            ["vendorId"] = hardwareId?.VendorId ?? hardwareId?.UsbVendorId,
            ["deviceId"] = hardwareId?.DeviceId ?? hardwareId?.UsbProductId
        };

        var result = template;
        foreach (var (key, value) in values)
        {
            var replacement = value ?? "";
            if (escape)
            {
                replacement = Uri.EscapeDataString(replacement);
            }

            result = result.Replace("{" + key + "}", replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static int Specificity(DriverRule rule)
        => (string.IsNullOrEmpty(rule.Match.DeviceId) ? 0 : 4)
           + (string.IsNullOrEmpty(rule.Match.ProductPattern) ? 0 : 2)
           + (string.IsNullOrEmpty(rule.Match.ClassPattern) ? 0 : 1);
}
```

- [ ] **Step 4: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~RuleMatcherTests" -v minimal
```

Expected: 5 passed。

- [ ] **Step 5: Commit**

```bash
git add src/HwOps.Core/Rules/RuleMatcher.cs tests/HwOps.Core.Tests/RuleMatcherTests.cs
git commit -m "feat: match driver rules and render download URLs"
```

---

## Task 6: 整机驱动建议聚合

**Files:**
- Create: `src/HwOps.Core/Rules/DriverAdvisor.cs`
- Test: `tests/HwOps.Core.Tests/DriverAdvisorTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/DriverAdvisorTests.cs`:

```csharp
using HwOps.Core.Models;
using HwOps.Core.Rules;

namespace HwOps.Core.Tests;

public class DriverAdvisorTests
{
    private static RuleSet Rules() => new()
    {
        RulesVersion = "test",
        Rules =
        {
            new DriverRule
            {
                Id = "board-asus",
                Tier = 3,
                Match = new RuleMatch { Type = "board", ManufacturerPattern = "ASUS" },
                Title = "华硕主板 {boardProduct}",
                UrlTemplate = "https://example.com/board/{boardProduct}"
            },
            new DriverRule
            {
                Id = "gpu-nvidia",
                Tier = 1,
                Match = new RuleMatch { Type = "pci", VendorId = "10DE", ClassPattern = "^Display$" },
                Title = "NVIDIA 驱动",
                UrlTemplate = "https://example.com/nvidia"
            },
            new DriverRule
            {
                Id = "fallback",
                Tier = 5,
                Match = new RuleMatch { Type = "any" },
                Title = "Catalog {hardwareId}",
                UrlTemplate = "https://example.com/catalog?q={hardwareId}"
            }
        }
    };

    [Fact]
    public void Advise_ReturnsPlatformGpuAndProblemSuggestions()
    {
        var gpu = new DeviceInfo
        {
            ClassName = "Display",
            Name = "NVIDIA GeForce RTX 3060",
            PnpId = "PCI\\VEN_10DE&DEV_2504"
        };
        var usb = new DeviceInfo
        {
            ClassName = "USB",
            Name = "未知 USB 设备",
            PnpId = "USB\\VID_1234&PID_5678",
            ProblemCode = 28
        };
        var snapshot = new HardwareSnapshot
        {
            Board = new BoardInfo { Manufacturer = "ASUS", Product = "PRIME B560M-A" },
            Devices = { gpu, usb },
            ProblemDevices =
            {
                new ProblemDevice
                {
                    PnpId = usb.PnpId,
                    Name = usb.Name,
                    ClassName = usb.ClassName,
                    ProblemCode = 28,
                    ProblemText = "未安装驱动程序"
                }
            }
        };

        var advice = new DriverAdvisor(Rules()).Advise(snapshot);

        Assert.Contains(advice.Platform, s => s.RuleId == "board-asus");
        Assert.Contains(advice.GpuDevices, d => d.DeviceName.Contains("RTX 3060"));
        Assert.Contains(advice.GpuDevices.Single().Suggestions, s => s.RuleId == "gpu-nvidia");
        var problem = Assert.Single(advice.ProblemDevices);
        Assert.Equal(28, problem.ProblemCode);
        Assert.Contains(problem.Suggestions, s => s.RuleId == "fallback");
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~DriverAdvisorTests" -v minimal
```

Expected: 编译失败，`DriverAdvisor` / `MachineAdvice` 未定义。

- [ ] **Step 3: 实现建议聚合**

`src/HwOps.Core/Rules/DriverAdvisor.cs`:

```csharp
using HwOps.Core.Models;

namespace HwOps.Core.Rules;

public sealed class MachineAdvice
{
    public List<DriverSuggestion> Platform { get; set; } = new();
    public List<DeviceAdvice> GpuDevices { get; set; } = new();
    public List<DeviceAdvice> ProblemDevices { get; set; } = new();
}

public sealed class DeviceAdvice
{
    public string DeviceName { get; set; } = "";
    public string PnpId { get; set; } = "";
    public int ProblemCode { get; set; }
    public List<DriverSuggestion> Suggestions { get; set; } = new();
}

public sealed class DriverAdvisor
{
    private readonly RuleMatcher _matcher;

    public DriverAdvisor(RuleSet rules)
    {
        _matcher = new RuleMatcher(rules);
    }

    public MachineAdvice Advise(HardwareSnapshot snapshot)
    {
        var platform = _matcher.SuggestForMachine(snapshot).ToList();

        var gpuDevices = snapshot.Devices
            .Where(d => string.Equals(d.ClassName, "Display", StringComparison.OrdinalIgnoreCase))
            .Select(d => ToAdvice(snapshot, d))
            .ToList();

        var problemDevices = snapshot.ProblemDevices
            .Select(problem =>
            {
                var device = snapshot.Devices.FirstOrDefault(d =>
                                 string.Equals(d.PnpId, problem.PnpId, StringComparison.OrdinalIgnoreCase))
                             ?? new DeviceInfo
                             {
                                 Name = problem.Name,
                                 ClassName = problem.ClassName,
                                 PnpId = problem.PnpId,
                                 ProblemCode = problem.ProblemCode
                             };
                return ToAdvice(snapshot, device);
            })
            .ToList();

        return new MachineAdvice
        {
            Platform = platform,
            GpuDevices = gpuDevices,
            ProblemDevices = problemDevices
        };
    }

    private DeviceAdvice ToAdvice(HardwareSnapshot snapshot, DeviceInfo device)
        => new()
        {
            DeviceName = device.Name,
            PnpId = device.PnpId,
            ProblemCode = device.ProblemCode,
            Suggestions = _matcher.SuggestForDevice(snapshot, device).Take(3).ToList()
        };
}
```

- [ ] **Step 4: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~DriverAdvisorTests" -v minimal
```

Expected: 1 passed。

- [ ] **Step 5: Commit**

```bash
git add src/HwOps.Core/Rules/DriverAdvisor.cs tests/HwOps.Core.Tests/DriverAdvisorTests.cs
git commit -m "feat: aggregate driver advice per machine"
```

---

## Task 7: 设备问题代码说明

**Files:**
- Create: `src/HwOps.Core/Models/ProblemCodes.cs`
- Test: `tests/HwOps.Core.Tests/ProblemCodesTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/ProblemCodesTests.cs`:

```csharp
using HwOps.Core.Models;

namespace HwOps.Core.Tests;

public class ProblemCodesTests
{
    [Theory]
    [InlineData(28, "未安装驱动程序")]
    [InlineData(10, "设备无法启动")]
    [InlineData(43, "Windows 已停止此设备，因为它报告了问题")]
    public void Describe_ReturnsKnownText(int code, string expected)
    {
        Assert.Equal(expected, ProblemCodes.Describe(code));
    }

    [Fact]
    public void Describe_UnknownCode_IncludesNumber()
    {
        var text = ProblemCodes.Describe(99);

        Assert.Contains("99", text);
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~ProblemCodesTests" -v minimal
```

Expected: 编译失败，`ProblemCodes` 未定义。

- [ ] **Step 3: 实现 ProblemCodes**

`src/HwOps.Core/Models/ProblemCodes.cs`:

```csharp
namespace HwOps.Core.Models;

public static class ProblemCodes
{
    public static string Describe(int code) => code switch
    {
        0 => "正常",
        1 => "设备未正确配置",
        3 => "驱动程序已损坏",
        10 => "设备无法启动",
        12 => "设备无法找到足够的可用资源",
        18 => "请重新安装此设备的驱动程序",
        19 => "注册表信息不完整或已损坏",
        21 => "Windows 正在删除此设备",
        22 => "设备已禁用",
        24 => "设备不存在、工作不正常或未安装全部驱动",
        28 => "未安装驱动程序",
        31 => "设备无法正常工作，因为 Windows 无法加载所需驱动",
        32 => "设备的驱动程序服务已禁用",
        37 => "Windows 无法初始化此设备的驱动程序",
        39 => "Windows 无法加载此设备的驱动程序",
        43 => "Windows 已停止此设备，因为它报告了问题",
        45 => "设备当前未连接到计算机",
        _ => $"设备问题代码 {code}"
    };
}
```

- [ ] **Step 4: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~ProblemCodesTests" -v minimal
```

Expected: 4 passed。

- [ ] **Step 5: Commit**

```bash
git add src/HwOps.Core/Models/ProblemCodes.cs tests/HwOps.Core.Tests/ProblemCodesTests.cs
git commit -m "feat: map device problem codes to Chinese descriptions"
```

---

## Task 8: WMI 基础设施与机器指纹

**Files:**
- Create: `src/HwOps.Core/Collection/Wmi.cs`
- Create: `src/HwOps.Core/Collection/MachineIdentity.cs`
- Test: `tests/HwOps.Core.Tests/MachineIdentityTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/MachineIdentityTests.cs`:

```csharp
using HwOps.Core.Collection;

namespace HwOps.Core.Tests;

public class MachineIdentityTests
{
    [Fact]
    public void Get_ReturnsStable32CharHex()
    {
        var first = MachineIdentity.Get(new List<string>());
        var second = MachineIdentity.Get(new List<string>());

        Assert.Equal(first, second);
        Assert.Equal(32, first.Length);
        Assert.Matches("^[0-9a-f]{32}$", first);
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~MachineIdentityTests" -v minimal
```

Expected: 编译失败，`MachineIdentity` 未定义。

- [ ] **Step 3: 实现 WMI 辅助**

`src/HwOps.Core/Collection/Wmi.cs`:

```csharp
using System.Globalization;
using System.Management;

namespace HwOps.Core.Collection;

internal static class Wmi
{
    public static void Query(string wql, string label, List<string> warnings, Action<ManagementObject> map)
        => Query(null, wql, label, warnings, map);

    public static void Query(string? scopePath, string wql, string label, List<string> warnings, Action<ManagementObject> map)
    {
        try
        {
            ManagementScope? scope = null;
            if (scopePath is not null)
            {
                scope = new ManagementScope(scopePath);
                scope.Connect();
            }

            using var searcher = scope is null
                ? new ManagementObjectSearcher(new ObjectQuery(wql))
                : new ManagementObjectSearcher(scope, new ObjectQuery(wql));
            searcher.Options.Timeout = TimeSpan.FromSeconds(5);

            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    map(item);
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"{label}: {ex.Message}");
        }
        // System.Management 8.0.0 的 ManagementScope 未实现 IDisposable（仅有 Connect/IsConnected），
        // 其底层 WMI 绑定由类型自身 finalizer 释放，无需也不能显式 Dispose。
    }

    public static string? Text(this ManagementObject item, string property)
    {
        try
        {
            return item[property]?.ToString()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static int? Int(this ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static long? Long(this ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static bool? Bool(this ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? null : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static string? WmiDate(this ManagementObject item, string property)
    {
        var raw = item.Text(property);
        if (string.IsNullOrEmpty(raw) || raw.Length < 8)
        {
            return raw;
        }

        return DateTime.TryParseExact(
            raw[..8],
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : raw;
    }
}
```

- [ ] **Step 4: 实现机器指纹**

`src/HwOps.Core/Collection/MachineIdentity.cs`:

```csharp
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace HwOps.Core.Collection;

public static class MachineIdentity
{
    public static string Get(List<string> warnings)
    {
        var uuid = ReadUuid(warnings);
        if (!string.IsNullOrWhiteSpace(uuid) && !IsPlaceholder(uuid))
        {
            return Hash(uuid!);
        }

        var machineGuid = ReadMachineGuid();
        if (!string.IsNullOrWhiteSpace(machineGuid))
        {
            return Hash(machineGuid!);
        }

        return Hash(Environment.MachineName + "|" + ReadFirstMac());
    }

    private static string? ReadUuid(List<string> warnings)
    {
        string? uuid = null;
        Wmi.Query("SELECT UUID FROM Win32_ComputerSystemProduct", "机器 UUID", warnings, item => uuid = item.Text("UUID"));
        return uuid;
    }

    private static bool IsPlaceholder(string uuid)
    {
        var normalized = uuid.Replace("-", "").Trim();
        return normalized.Length == 0
               || normalized.All(c => c is '0' or 'F' or 'f')
               || uuid.Contains("Default string", StringComparison.OrdinalIgnoreCase)
               || uuid.Contains("To be filled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadMachineGuid()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string ReadFirstMac()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => n.GetPhysicalAddress().ToString())
                .FirstOrDefault(mac => mac.Length == 12) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
        return Convert.ToHexString(bytes.AsSpan(0, 16)).ToLowerInvariant();
    }
}
```

- [ ] **Step 5: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~MachineIdentityTests" -v minimal
```

Expected: 1 passed。

- [ ] **Step 6: Commit**

```bash
git add src/HwOps.Core/Collection/Wmi.cs src/HwOps.Core/Collection/MachineIdentity.cs tests/HwOps.Core.Tests/MachineIdentityTests.cs
git commit -m "feat: add WMI helpers and stable machine identity"
```

---

## Task 9: 硬件采集器

> 实现备注（质量审查后重构，提交 `285382f`、`bede8fc`）：互不依赖的采集类别改为 **11 个任务并行**（drivers→devices 同任务串行），新进程单次采集从 3.06–3.26 秒降到 1.97–2.22 秒，满足「< 3 秒」验收线；每类别使用独立 warnings，按固定顺序合并；新增 10 秒整体预算（`CancelAfter`）与逐行取消检查，调用方取消会向外传播、预算超时则该类别记「超过采集时间预算，已跳过」；`gpus`/`network` 排除 `ROOT\`/`BTH\`/`USBSTOR\` 虚拟适配器（`devices` 仍为全量视图）；`devices` 排序补 `PnpId` tie-breaker；全空 `DriverInfo` 不再挂到设备上；序列号占位值经 `ScrubSerial` 归一为 null；`BuildProblemDevices`/`IsVirtualPnpId`/`ScrubSerial` 抽为 internal 纯函数并单测；机器标识回退路径不写 warnings（避免与「健康机器无告警」断言耦合）。

**Files:**
- Create: `src/HwOps.Core/Collection/IHardwareCollector.cs`
- Create: `src/HwOps.Core/Collection/WmiHardwareCollector.cs`
- Test: `tests/HwOps.Core.Tests/WmiCollectorTests.cs`

- [ ] **Step 1: 写集成测试**

`tests/HwOps.Core.Tests/WmiCollectorTests.cs`:

```csharp
using HwOps.Core.Collection;

namespace HwOps.Core.Tests;

public class WmiCollectorTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Collect_CurrentMachine_ReturnsCoreData()
    {
        var snapshot = new WmiHardwareCollector().Collect();

        Assert.Equal(32, snapshot.MachineId.Length);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.System.Hostname));
        Assert.True(snapshot.Devices.Count > 0);
        Assert.True(snapshot.Cpu.Count > 0);
        Assert.Equal(snapshot.Devices.Count(d => d.ProblemCode != 0), snapshot.ProblemDevices.Count);
        Assert.All(snapshot.Devices, d => Assert.False(string.IsNullOrWhiteSpace(d.PnpId)));
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~WmiCollectorTests" -v minimal
```

Expected: 编译失败，`WmiHardwareCollector` 未定义。

- [ ] **Step 3: 定义采集接口**

`src/HwOps.Core/Collection/IHardwareCollector.cs`:

```csharp
using HwOps.Core.Models;

namespace HwOps.Core.Collection;

public interface IHardwareCollector
{
    HardwareSnapshot Collect(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: 实现采集器**

`src/HwOps.Core/Collection/WmiHardwareCollector.cs`:

```csharp
using HwOps.Core.Models;

namespace HwOps.Core.Collection;

public sealed class WmiHardwareCollector : IHardwareCollector
{
    public HardwareSnapshot Collect(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        cancellationToken.ThrowIfCancellationRequested();
        var system = ReadSystem(warnings);
        var board = ReadBoard(warnings);
        var bios = ReadBios(warnings);
        var cpu = ReadCpu(warnings);
        var memory = ReadMemory(warnings);
        var gpus = ReadGpus(warnings);
        var disks = ReadDisks(warnings);
        var network = ReadNetwork(warnings);
        var sound = ReadSound(warnings);

        cancellationToken.ThrowIfCancellationRequested();
        var drivers = ReadDrivers(warnings);
        var devices = ReadDevices(warnings, drivers);

        var problems = devices
            .Where(d => d.ProblemCode != 0)
            .Select(d => new ProblemDevice
            {
                PnpId = d.PnpId,
                Name = d.Name,
                ClassName = d.ClassName,
                ProblemCode = d.ProblemCode,
                ProblemText = ProblemCodes.Describe(d.ProblemCode)
            })
            .ToList();

        return new HardwareSnapshot
        {
            MachineId = MachineIdentity.Get(warnings),
            CollectedAt = DateTimeOffset.Now,
            System = system,
            Board = board,
            Bios = bios,
            Cpu = cpu,
            Memory = memory,
            Gpus = gpus,
            Disks = disks,
            Network = network,
            Sound = sound,
            Devices = devices,
            ProblemDevices = problems,
            Collection = new CollectionInfo { Warnings = warnings }
        };
    }

    private static MachineSystemInfo ReadSystem(List<string> warnings)
    {
        var info = new MachineSystemInfo { Hostname = Environment.MachineName };

        Wmi.Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem", "系统信息", warnings, item =>
        {
            info.Manufacturer = item.Text("Manufacturer");
            info.Model = item.Text("Model");
        });

        Wmi.Query("SELECT UUID, IdentifyingNumber FROM Win32_ComputerSystemProduct", "整机标识", warnings, item =>
        {
            info.SmbiosUuid = item.Text("UUID");
            info.Serial = item.Text("IdentifyingNumber");
        });

        Wmi.Query("SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem", "操作系统", warnings, item =>
        {
            info.Os = new OsInfo
            {
                Caption = item.Text("Caption"),
                Version = item.Text("Version"),
                Build = item.Text("BuildNumber"),
                Arch = item.Text("OSArchitecture")
            };
        });

        return info;
    }

    private static BoardInfo ReadBoard(List<string> warnings)
    {
        var info = new BoardInfo();
        Wmi.Query("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard", "主板信息", warnings, item =>
        {
            info.Manufacturer = item.Text("Manufacturer");
            info.Product = item.Text("Product");
            info.Version = item.Text("Version");
            info.Serial = item.Text("SerialNumber");
        });
        return info;
    }

    private static BiosInfo ReadBios(List<string> warnings)
    {
        var info = new BiosInfo();
        Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS", "BIOS 信息", warnings, item =>
        {
            info.Vendor = item.Text("Manufacturer");
            info.Version = item.Text("SMBIOSBIOSVersion");
            info.ReleaseDate = item.WmiDate("ReleaseDate");
            info.Serial = item.Text("SerialNumber");
        });
        return info;
    }

    private static List<CpuInfo> ReadCpu(List<string> warnings)
    {
        var list = new List<CpuInfo>();
        Wmi.Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor", "CPU", warnings, item =>
            list.Add(new CpuInfo
            {
                Name = item.Text("Name"),
                Cores = item.Int("NumberOfCores"),
                Threads = item.Int("NumberOfLogicalProcessors"),
                MaxClockMhz = item.Int("MaxClockSpeed")
            }));
        return list;
    }

    private static List<MemoryModuleInfo> ReadMemory(List<string> warnings)
    {
        var list = new List<MemoryModuleInfo>();
        Wmi.Query(
            "SELECT Manufacturer, PartNumber, Capacity, Speed, ConfiguredClockSpeed, DeviceLocator, FormFactor FROM Win32_PhysicalMemory",
            "内存",
            warnings,
            item => list.Add(new MemoryModuleInfo
            {
                Manufacturer = item.Text("Manufacturer"),
                PartNumber = item.Text("PartNumber"),
                CapacityBytes = item.Long("Capacity"),
                SpeedMhz = item.Int("ConfiguredClockSpeed") ?? item.Int("Speed"),
                Slot = item.Text("DeviceLocator"),
                FormFactor = item.Text("FormFactor")
            }));
        return list;
    }

    private static List<GpuInfo> ReadGpus(List<string> warnings)
    {
        var list = new List<GpuInfo>();
        Wmi.Query("SELECT Name, DriverVersion, DriverDate, PNPDeviceID, Status FROM Win32_VideoController", "显卡", warnings, item =>
            list.Add(new GpuInfo
            {
                Name = item.Text("Name"),
                DriverVersion = item.Text("DriverVersion"),
                DriverDate = item.WmiDate("DriverDate"),
                PnpId = item.Text("PNPDeviceID"),
                Status = item.Text("Status")
            }));
        return list;
    }

    private static List<DiskInfo> ReadDisks(List<string> warnings)
    {
        var list = new List<DiskInfo>();
        Wmi.Query(
            "SELECT Model, InterfaceType, MediaType, Size, PNPDeviceID, SerialNumber FROM Win32_DiskDrive",
            "磁盘",
            warnings,
            item => list.Add(new DiskInfo
            {
                Model = item.Text("Model"),
                InterfaceType = item.Text("InterfaceType"),
                MediaType = item.Text("MediaType"),
                SizeBytes = item.Long("Size"),
                PnpId = item.Text("PNPDeviceID"),
                Serial = item.Text("SerialNumber")
            }));

        var mediaBySerial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Wmi.Query(
            @"\\.\root\Microsoft\Windows\Storage",
            "SELECT SerialNumber, MediaType FROM MSFT_PhysicalDisk",
            "磁盘介质类型",
            warnings,
            item =>
            {
                var serial = item.Text("SerialNumber");
                var media = item.Int("MediaType") switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM",
                    _ => null
                };
                if (!string.IsNullOrEmpty(serial) && media is not null)
                {
                    mediaBySerial[serial] = media;
                }
            });

        foreach (var disk in list)
        {
            if (!string.IsNullOrEmpty(disk.Serial) && mediaBySerial.TryGetValue(disk.Serial, out var media))
            {
                disk.MediaType = media;
            }
        }

        return list;
    }

    private static List<NetworkAdapterInfo> ReadNetwork(List<string> warnings)
    {
        var list = new List<NetworkAdapterInfo>();
        Wmi.Query(
            "SELECT Name, MACAddress, PNPDeviceID, NetConnectionID, NetEnabled, Speed FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
            "网络适配器",
            warnings,
            item =>
            {
                var pnpId = item.Text("PNPDeviceID");
                if (string.IsNullOrEmpty(pnpId))
                {
                    return;
                }

                list.Add(new NetworkAdapterInfo
                {
                    Name = item.Text("Name"),
                    Mac = item.Text("MACAddress"),
                    PnpId = pnpId,
                    ConnectionName = item.Text("NetConnectionID"),
                    Enabled = item.Bool("NetEnabled"),
                    SpeedBps = item.Long("Speed")
                });
            });
        return list;
    }

    private static List<SoundDeviceInfo> ReadSound(List<string> warnings)
    {
        var list = new List<SoundDeviceInfo>();
        Wmi.Query("SELECT Name, Manufacturer, PNPDeviceID, Status FROM Win32_SoundDevice", "声音设备", warnings, item =>
            list.Add(new SoundDeviceInfo
            {
                Name = item.Text("Name"),
                Manufacturer = item.Text("Manufacturer"),
                PnpId = item.Text("PNPDeviceID"),
                Status = item.Text("Status")
            }));
        return list;
    }

    private static Dictionary<string, DriverInfo> ReadDrivers(List<string> warnings)
    {
        var drivers = new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase);
        Wmi.Query(
            "SELECT DeviceID, DriverVersion, DriverDate, DriverProviderName, InfName, IsSigned FROM Win32_PnPSignedDriver",
            "驱动信息",
            warnings,
            item =>
            {
                var deviceId = item.Text("DeviceID");
                if (string.IsNullOrEmpty(deviceId))
                {
                    return;
                }

                drivers[deviceId] = new DriverInfo
                {
                    Version = item.Text("DriverVersion"),
                    Date = item.WmiDate("DriverDate"),
                    Provider = item.Text("DriverProviderName"),
                    Inf = item.Text("InfName"),
                    Signed = item.Bool("IsSigned")
                };
            });
        return drivers;
    }

    private static List<DeviceInfo> ReadDevices(List<string> warnings, Dictionary<string, DriverInfo> drivers)
    {
        var list = new List<DeviceInfo>();
        Wmi.Query(
            "SELECT Name, PNPClass, DeviceID, ConfigManagerErrorCode, Status, Manufacturer FROM Win32_PnPEntity",
            "设备列表",
            warnings,
            item =>
            {
                var deviceId = item.Text("DeviceID");
                if (string.IsNullOrEmpty(deviceId))
                {
                    return;
                }

                drivers.TryGetValue(deviceId, out var driver);
                list.Add(new DeviceInfo
                {
                    ClassName = item.Text("PNPClass") ?? "",
                    Name = item.Text("Name") ?? "(未知设备)",
                    Manufacturer = item.Text("Manufacturer"),
                    PnpId = deviceId,
                    Status = item.Text("Status"),
                    ProblemCode = item.Int("ConfigManagerErrorCode") ?? 0,
                    Driver = driver
                });
            });

        list.Sort((left, right) =>
        {
            var byClass = string.Compare(left.ClassName, right.ClassName, StringComparison.OrdinalIgnoreCase);
            return byClass != 0
                ? byClass
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }
}
```

- [ ] **Step 5: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~WmiCollectorTests" -v minimal
```

Expected: 1 passed（采集约 2–8 秒）。若 `MSFT_PhysicalDisk` 查询失败，只会出现在 `collection.warnings`，测试仍应通过。

- [ ] **Step 6: Commit**

```bash
git add src/HwOps.Core/Collection/IHardwareCollector.cs src/HwOps.Core/Collection/WmiHardwareCollector.cs tests/HwOps.Core.Tests/WmiCollectorTests.cs
git commit -m "feat: collect hardware and driver state via WMI"
```

---

## Task 10: HTML 报告生成

**Files:**
- Create: `src/HwOps.Core/Reporting/HtmlReportBuilder.cs`
- Test: `tests/HwOps.Core.Tests/HtmlReportTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/HwOps.Core.Tests/HtmlReportTests.cs`:

```csharp
using HwOps.Core.Models;
using HwOps.Core.Reporting;
using HwOps.Core.Rules;

namespace HwOps.Core.Tests;

public class HtmlReportTests
{
    [Fact]
    public void Build_EncodesHtml_AndIncludesAdviceLinks()
    {
        var snapshot = new HardwareSnapshot
        {
            MachineId = "x",
            CollectedAt = DateTimeOffset.Parse("2026-10-27T09:00:00+08:00"),
            System = new MachineSystemInfo
            {
                Hostname = "<script>alert(1)</script>",
                Os = new OsInfo { Caption = "Windows 11", Arch = "64-bit" }
            },
            Board = new BoardInfo { Manufacturer = "ASUS", Product = "B560M" },
            Devices =
            {
                new DeviceInfo
                {
                    Name = "未知 USB 设备",
                    ClassName = "USB",
                    PnpId = "USB\\VID_1234&PID_5678",
                    ProblemCode = 28
                }
            },
            ProblemDevices =
            {
                new ProblemDevice
                {
                    PnpId = "USB\\VID_1234&PID_5678",
                    Name = "未知 USB 设备",
                    ClassName = "USB",
                    ProblemCode = 28,
                    ProblemText = "未安装驱动程序"
                }
            }
        };
        var advice = new MachineAdvice
        {
            Platform =
            {
                new DriverSuggestion
                {
                    RuleId = "board-asus",
                    Tier = 3,
                    Title = "主板驱动",
                    Url = "https://example.com/board?a=1&b=2",
                    Note = "在页面内搜索型号"
                }
            },
            ProblemDevices =
            {
                new DeviceAdvice
                {
                    DeviceName = "未知 USB 设备",
                    PnpId = "USB\\VID_1234&PID_5678",
                    ProblemCode = 28,
                    Suggestions =
                    {
                        new DriverSuggestion
                        {
                            RuleId = "fallback",
                            Tier = 5,
                            Title = "Catalog 搜索",
                            Url = "https://example.com/catalog?q=USB%5CVID"
                        }
                    }
                }
            }
        };

        var html = HtmlReportBuilder.Build(snapshot, advice, "0.1.0");

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("https://example.com/board?a=1&amp;b=2", html);
        Assert.Contains("问题设备", html);
        Assert.Contains("未安装驱动程序", html);
        Assert.Contains("0.1.0", html);
    }
}
```

- [ ] **Step 2: 运行确认失败**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~HtmlReportTests" -v minimal
```

Expected: 编译失败，`HtmlReportBuilder` 未定义。

- [ ] **Step 3: 实现报告生成器**

`src/HwOps.Core/Reporting/HtmlReportBuilder.cs`:

```csharp
using System.Net;
using System.Text;
using HwOps.Core.Models;
using HwOps.Core.Rules;

namespace HwOps.Core.Reporting;

public static class HtmlReportBuilder
{
    private const string Css = """
        body{font-family:"Microsoft YaHei UI","Microsoft YaHei",sans-serif;margin:32px;color:#1f2328;line-height:1.55;background:#fff}
        h1{font-size:24px;margin:0 0 4px}
        h2{font-size:17px;margin:28px 0 10px;border-left:4px solid #2f6feb;padding-left:8px}
        .muted{color:#6e7781;font-size:13px}
        table{border-collapse:collapse;width:100%;margin:8px 0;font-size:13px}
        th,td{border:1px solid #d0d7de;padding:6px 8px;text-align:left;vertical-align:top}
        th{background:#f6f8fa;white-space:nowrap}
        .problem{color:#b00020;font-weight:600}
        .warning{color:#9a6700}
        ul{margin:6px 0;padding-left:20px}
        li{margin:3px 0}
        code{background:#f6f8fa;padding:1px 4px;border-radius:4px;font-size:12px}
        .card{border:1px solid #d0d7de;border-radius:8px;padding:12px 14px;margin:10px 0}
        details{margin:10px 0}
        summary{cursor:pointer;color:#2f6feb}
        """;

    public static string Build(HardwareSnapshot snapshot, MachineAdvice advice, string toolVersion)
    {
        var sb = new StringBuilder(16 * 1024);
        sb.Append("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<title>硬件体检报告 - ").Append(E(snapshot.System.Hostname)).Append("</title>")
            .Append("<style>").Append(Css).Append("</style></head><body>");

        sb.Append("<h1>硬件体检报告</h1>");
        sb.Append("<p class=\"muted\">主机：").Append(E(snapshot.System.Hostname))
            .Append(" ｜ 采集时间：").Append(E(snapshot.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss")))
            .Append(" ｜ 工具版本：").Append(E(toolVersion)).Append("</p>");

        AppendOverview(sb, snapshot);
        AppendProblems(sb, advice);
        AppendSuggestions(sb, "平台驱动（主板 / 整机厂商）", advice.Platform);
        AppendDeviceSuggestions(sb, "显卡驱动", advice.GpuDevices);
        AppendDeviceSuggestions(sb, "问题设备处理建议", advice.ProblemDevices);
        AppendAllDevices(sb, snapshot);
        AppendWarnings(sb, snapshot);

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendOverview(StringBuilder sb, HardwareSnapshot s)
    {
        sb.Append("<h2>系统概况</h2><table>");
        Row(sb, "操作系统", $"{s.System.Os.Caption} {s.System.Os.Arch}".Trim());
        Row(sb, "整机", $"{s.System.Manufacturer} {s.System.Model}".Trim());
        Row(sb, "序列号", s.System.Serial);
        Row(sb, "主板", $"{s.Board.Manufacturer} {s.Board.Product} {s.Board.Version}".Trim());
        Row(sb, "BIOS", $"{s.Bios.Vendor} {s.Bios.Version} ({s.Bios.ReleaseDate})".Trim());
        Row(sb, "CPU", string.Join(" / ", s.Cpu.Select(c => $"{c.Name}（{c.Cores} 核 / {c.Threads} 线程）")));
        Row(sb, "内存", $"{s.Memory.Count} 条，共 {FormatGb(s.Memory.Sum(m => m.CapacityBytes ?? 0))} GB");
        Row(sb, "显卡", string.Join(" / ", s.Gpus.Select(g => g.Name)));
        Row(sb, "磁盘", string.Join(" / ", s.Disks.Select(d => $"{d.Model}（{d.MediaType ?? d.InterfaceType}）")));
        Row(sb, "网络", string.Join(" / ", s.Network.Select(n => n.Name)));
        Row(sb, "设备总数", s.Devices.Count.ToString());
        sb.Append("</table>");
    }

    private static void AppendProblems(StringBuilder sb, MachineAdvice advice)
    {
        sb.Append("<h2>问题设备</h2>");
        if (advice.ProblemDevices.Count == 0)
        {
            sb.Append("<p class=\"muted\">未发现带问题代码的设备。</p>");
            return;
        }

        sb.Append("<table><tr><th>设备</th><th>硬件 ID</th><th>代码</th><th>说明</th></tr>");
        foreach (var device in advice.ProblemDevices)
        {
            sb.Append("<tr><td>").Append(E(device.DeviceName))
                .Append("</td><td><code>").Append(E(device.PnpId))
                .Append("</code></td><td class=\"problem\">").Append(device.ProblemCode)
                .Append("</td><td>").Append(E(ProblemCodes.Describe(device.ProblemCode))).Append("</td></tr>");
        }
        sb.Append("</table>");
    }

    private static void AppendSuggestions(StringBuilder sb, string title, List<DriverSuggestion> suggestions)
    {
        sb.Append("<h2>").Append(E(title)).Append("</h2>");
        AppendList(sb, suggestions);
    }

    private static void AppendDeviceSuggestions(StringBuilder sb, string title, List<DeviceAdvice> devices)
    {
        if (devices.Count == 0)
        {
            return;
        }

        sb.Append("<h2>").Append(E(title)).Append("</h2>");
        foreach (var device in devices)
        {
            sb.Append("<div class=\"card\"><strong>").Append(E(device.DeviceName)).Append("</strong>");
            if (device.ProblemCode != 0)
            {
                sb.Append(" <span class=\"problem\">代码 ").Append(device.ProblemCode)
                    .Append("：").Append(E(ProblemCodes.Describe(device.ProblemCode))).Append("</span>");
            }
            sb.Append("<div class=\"muted\"><code>").Append(E(device.PnpId)).Append("</code></div>");
            AppendList(sb, device.Suggestions);
            sb.Append("</div>");
        }
    }

    private static void AppendList(StringBuilder sb, IEnumerable<DriverSuggestion> suggestions)
    {
        var items = suggestions.ToList();
        if (items.Count == 0)
        {
            sb.Append("<p class=\"muted\">未命中规则。</p>");
            return;
        }

        sb.Append("<ul>");
        foreach (var suggestion in items)
        {
            sb.Append("<li><a href=\"").Append(E(suggestion.Url))
                .Append("\" target=\"_blank\" rel=\"noopener\">").Append(E(suggestion.Title)).Append("</a>");
            if (!string.IsNullOrWhiteSpace(suggestion.Note))
            {
                sb.Append("<div class=\"muted\">").Append(E(suggestion.Note)).Append("</div>");
            }
            sb.Append("</li>");
        }
        sb.Append("</ul>");
    }

    private static void AppendAllDevices(StringBuilder sb, HardwareSnapshot snapshot)
    {
        sb.Append("<details><summary>全部设备（").Append(snapshot.Devices.Count).Append("）</summary>");
        sb.Append("<table><tr><th>类别</th><th>名称</th><th>厂商</th><th>状态</th><th>问题</th><th>驱动版本</th><th>硬件 ID</th></tr>");
        foreach (var device in snapshot.Devices)
        {
            sb.Append("<tr><td>").Append(E(device.ClassName))
                .Append("</td><td>").Append(E(device.Name))
                .Append("</td><td>").Append(E(device.Manufacturer))
                .Append("</td><td>").Append(E(device.Status))
                .Append("</td><td>").Append(device.ProblemCode == 0 ? "" : device.ProblemCode.ToString())
                .Append("</td><td>").Append(E(device.Driver?.Version))
                .Append("</td><td><code>").Append(E(device.PnpId)).Append("</code></td></tr>");
        }
        sb.Append("</table></details>");
    }

    private static void AppendWarnings(StringBuilder sb, HardwareSnapshot snapshot)
    {
        if (snapshot.Collection.Warnings.Count == 0)
        {
            return;
        }

        sb.Append("<h2>采集告警</h2><ul>");
        foreach (var warning in snapshot.Collection.Warnings)
        {
            sb.Append("<li class=\"warning\">").Append(E(warning)).Append("</li>");
        }
        sb.Append("</ul>");
    }

    private static void Row(StringBuilder sb, string label, string? value)
    {
        sb.Append("<tr><th>").Append(E(label)).Append("</th><td>")
            .Append(string.IsNullOrWhiteSpace(value) ? "—" : E(value))
            .Append("</td></tr>");
    }

    private static string FormatGb(long bytes)
        => (bytes / 1073741824.0).ToString("0.#");

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
```

- [ ] **Step 4: 运行确认通过**

```bash
dotnet test tests/HwOps.Core.Tests/HwOps.Core.Tests.csproj --filter "FullyQualifiedName~HtmlReportTests" -v minimal
```

Expected: 1 passed。

- [ ] **Step 5: 全量测试**

```bash
dotnet test HwOps.sln -c Release -v minimal
```

Expected: 全部通过。

- [ ] **Step 6: Commit**

```bash
git add src/HwOps.Core/Reporting/HtmlReportBuilder.cs tests/HwOps.Core.Tests/HtmlReportTests.cs
git commit -m "feat: generate offline HTML hardware report"
```

---

## Task 11: 同事端主界面

**Files:**
- Modify: `src/HwOps.Agent/Program.cs`
- Create: `src/HwOps.Agent/MainForm.cs`

- [ ] **Step 1: 写主窗口**

`src/HwOps.Agent/MainForm.cs`（完整文件；本任务一次写完，后续任务只加自检入口）：

```csharp
using System.Diagnostics;
using System.Text;
using HwOps.Core.Collection;
using HwOps.Core.Models;
using HwOps.Core.Reporting;
using HwOps.Core.Rules;

namespace HwOps.Agent;

public sealed class MainForm : Form
{
    private const string ToolVersion = "0.1.0";

    private readonly IHardwareCollector _collector = new WmiHardwareCollector();
    private readonly RuleSet _rules;
    private readonly ToolTip _toolTip = new();
    private readonly Label _headerLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _collectButton = new();
    private readonly Button _exportHtmlButton = new();
    private readonly Button _exportJsonButton = new();
    private readonly TabControl _tabs = new();
    private readonly TableLayoutPanel _summaryGrid = new();
    private readonly DataGridView _deviceGrid = new();
    private readonly TableLayoutPanel _problemPanel = new();
    private readonly TableLayoutPanel _advicePanel = new();
    private readonly NotifyIcon _trayIcon;

    private HardwareSnapshot? _snapshot;
    private MachineAdvice? _advice;
    private bool _allowExit;

    public MainForm()
    {
        _rules = LoadRules();

        Text = "硬件助手";
        Width = 960;
        Height = 700;
        MinimumSize = new Size(800, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _headerLabel.AutoSize = false;
        _headerLabel.Dock = DockStyle.Fill;
        _headerLabel.Height = 70;
        _headerLabel.Padding = new Padding(12, 10, 12, 4);
        _headerLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _headerLabel.Text = "正在读取本机信息…";
        root.Controls.Add(_headerLabel, 0, 0);

        _tabs.Dock = DockStyle.Fill;
        root.Controls.Add(_tabs, 0, 1);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(8)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.Controls.Add(bottom, 0, 2);

        ConfigureButton(_collectButton, "重新采集");
        ConfigureButton(_exportHtmlButton, "导出 HTML");
        ConfigureButton(_exportJsonButton, "导出 JSON");

        _collectButton.Click += async (_, _) => await CollectAsync();
        _exportHtmlButton.Click += (_, _) => ExportHtml();
        _exportJsonButton.Click += (_, _) => ExportJson();

        bottom.Controls.Add(_collectButton, 0, 0);
        bottom.Controls.Add(_exportHtmlButton, 1, 0);
        bottom.Controls.Add(_exportJsonButton, 2, 0);

        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text = "准备就绪";
        bottom.Controls.Add(_statusLabel, 3, 0);

        BuildSummaryTab();
        BuildDeviceTab();
        BuildProblemTab();
        BuildAdviceTab();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "硬件助手",
            Visible = true
        };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("打开面板", null, (_, _) => ShowPanel());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) =>
        {
            _allowExit = true;
            Close();
        });
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowPanel();

        FormClosing += (_, e) =>
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
        FormClosed += (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        };

        Shown += async (_, _) => await CollectAsync();
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(10, 4, 10, 4);
        button.Margin = new Padding(0, 0, 8, 0);
    }

    private static RuleSet LoadRules()
    {
        try
        {
            return RuleSet.LoadEmbedded();
        }
        catch (Exception ex)
        {
            MessageBox.Show("驱动规则加载失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new RuleSet();
        }
    }

    private void BuildSummaryTab()
    {
        var page = new TabPage("概况");
        _summaryGrid.Dock = DockStyle.Fill;
        _summaryGrid.AutoScroll = true;
        _summaryGrid.ColumnCount = 2;
        _summaryGrid.RowCount = 0;
        _summaryGrid.Padding = new Padding(14);
        _summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        _summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(_summaryGrid);
        _tabs.TabPages.Add(page);
    }

    private void BuildDeviceTab()
    {
        var page = new TabPage("设备列表");
        _deviceGrid.Dock = DockStyle.Fill;
        _deviceGrid.ReadOnly = true;
        _deviceGrid.AllowUserToAddRows = false;
        _deviceGrid.AllowUserToDeleteRows = false;
        _deviceGrid.AllowUserToResizeRows = false;
        _deviceGrid.RowHeadersVisible = false;
        _deviceGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _deviceGrid.MultiSelect = false;
        _deviceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _deviceGrid.BackgroundColor = Color.White;
        page.Controls.Add(_deviceGrid);
        _tabs.TabPages.Add(page);
    }

    private void BuildProblemTab()
    {
        var page = new TabPage("问题设备");
        _problemPanel.Dock = DockStyle.Fill;
        _problemPanel.AutoScroll = true;
        _problemPanel.ColumnCount = 1;
        _problemPanel.Padding = new Padding(14);
        _problemPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(_problemPanel);
        _tabs.TabPages.Add(page);
    }

    private void BuildAdviceTab()
    {
        var page = new TabPage("驱动建议");
        _advicePanel.Dock = DockStyle.Fill;
        _advicePanel.AutoScroll = true;
        _advicePanel.ColumnCount = 1;
        _advicePanel.Padding = new Padding(14);
        _advicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.Controls.Add(_advicePanel);
        _tabs.TabPages.Add(page);
    }

    private async Task CollectAsync()
    {
        _collectButton.Enabled = false;
        _statusLabel.Text = "正在采集硬件信息…";
        try
        {
            var snapshot = await Task.Run(() => _collector.Collect());
            var advice = new DriverAdvisor(_rules).Advise(snapshot);
            _snapshot = snapshot;
            _advice = advice;
            RenderAll();
            _statusLabel.Text = $"采集完成：{snapshot.CollectedAt:HH:mm:ss}，设备 {snapshot.Devices.Count} 个，问题 {snapshot.ProblemDevices.Count} 个";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "采集失败：" + ex.Message;
            MessageBox.Show(this, "采集失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _collectButton.Enabled = true;
        }
    }

    private void RenderAll()
    {
        if (_snapshot is null || _advice is null)
        {
            return;
        }

        RenderHeader();
        RenderSummary();
        RenderDevices();
        RenderProblems();
        RenderAdvice();
    }

    private void RenderHeader()
    {
        var s = _snapshot!;
        _headerLabel.Text =
            $"{s.System.Hostname}  |  {s.System.Os.Caption} {s.System.Os.Arch}\r\n" +
            $"{s.Board.Manufacturer} {s.Board.Product}  |  BIOS {s.Bios.Version}（{s.Bios.ReleaseDate}）";
    }

    private void RenderSummary()
    {
        _summaryGrid.SuspendLayout();
        _summaryGrid.Controls.Clear();
        _summaryGrid.RowStyles.Clear();
        _summaryGrid.RowCount = 0;

        var s = _snapshot!;
        AddRow("主机名", s.System.Hostname);
        AddRow("操作系统", $"{s.System.Os.Caption} {s.System.Os.Arch}".Trim());
        AddRow("整机", $"{s.System.Manufacturer} {s.System.Model}".Trim());
        AddRow("序列号", s.System.Serial);
        AddRow("主板", $"{s.Board.Manufacturer} {s.Board.Product} {s.Board.Version}".Trim());
        AddRow("BIOS", $"{s.Bios.Vendor} {s.Bios.Version}（{s.Bios.ReleaseDate}）".Trim());
        AddRow("CPU", string.Join(" / ", s.Cpu.Select(c => $"{c.Name}（{c.Cores} 核 / {c.Threads} 线程）")));
        AddRow("内存", $"{s.Memory.Count} 条，共 {FormatGb(s.Memory.Sum(m => m.CapacityBytes ?? 0))} GB");
        AddRow("显卡", string.Join(" / ", s.Gpus.Select(g => g.Name)));
        AddRow("磁盘", string.Join(" / ", s.Disks.Select(d => $"{d.Model}（{d.MediaType ?? d.InterfaceType}）")));
        AddRow("网络", string.Join(" / ", s.Network.Select(n => n.Name)));
        AddRow("设备总数", s.Devices.Count.ToString());
        AddRow("采集时间", s.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        AddRow("规则库版本", _rules.RulesVersion);

        foreach (var warning in s.Collection.Warnings)
        {
            AddRow("采集告警", warning, Color.Firebrick);
        }

        _summaryGrid.ResumeLayout();
    }

    private void AddRow(string label, string? value, Color? color = null)
    {
        var row = _summaryGrid.RowCount++;
        _summaryGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _summaryGrid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 5, 10, 5)
        }, 0, row);
        _summaryGrid.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = color ?? Color.Black,
            Margin = new Padding(0, 5, 0, 5)
        }, 1, row);
    }

    private void RenderDevices()
    {
        _deviceGrid.DataSource = _snapshot!.Devices.Select(d => new
        {
            类别 = d.ClassName,
            名称 = d.Name,
            厂商 = d.Manufacturer,
            状态 = d.Status,
            问题 = d.ProblemCode == 0 ? "" : d.ProblemCode.ToString(),
            驱动版本 = d.Driver?.Version,
            驱动日期 = d.Driver?.Date,
            硬件ID = d.PnpId
        }).ToList();
    }

    private void RenderProblems()
    {
        _problemPanel.SuspendLayout();
        _problemPanel.Controls.Clear();
        _problemPanel.RowStyles.Clear();
        _problemPanel.RowCount = 0;

        if (_advice!.ProblemDevices.Count == 0)
        {
            AddProblemControl(new Label
            {
                Text = "没有发现带问题代码的设备。",
                AutoSize = true,
                ForeColor = Color.DimGray
            });
        }
        else
        {
            foreach (var device in _advice.ProblemDevices)
            {
                AddProblemControl(BuildDeviceCard(device));
            }
        }

        _problemPanel.ResumeLayout();
    }

    private void AddProblemControl(Control control)
    {
        var row = _problemPanel.RowCount++;
        _problemPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _problemPanel.Controls.Add(control, 0, row);
    }

    private Control BuildDeviceCard(DeviceAdvice device)
    {
        var card = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.FromArgb(248, 249, 250)
        };
        card.Controls.Add(new Label
        {
            Text = $"{device.DeviceName}（代码 {device.ProblemCode}：{ProblemCodes.Describe(device.ProblemCode)}）",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        });

        if (device.Suggestions.Count == 0)
        {
            card.Controls.Add(new Label
            {
                Text = "未命中规则，请用硬件 ID 搜索 Microsoft Update Catalog。",
                AutoSize = true
            });
        }
        else
        {
            foreach (var suggestion in device.Suggestions)
            {
                card.Controls.Add(CreateSuggestionLink(suggestion));
            }
        }

        return card;
    }

    private void RenderAdvice()
    {
        _advicePanel.SuspendLayout();
        _advicePanel.Controls.Clear();
        _advicePanel.RowStyles.Clear();
        _advicePanel.RowCount = 0;

        AddAdviceSection("平台驱动（主板 / 整机厂商）", _advice!.Platform);
        AddAdviceSection(
            "显卡驱动",
            _advice.GpuDevices.SelectMany(d => d.Suggestions).DistinctBy(s => s.Url).ToList());
        AddAdviceSection(
            "问题设备处理",
            _advice.ProblemDevices.SelectMany(d => d.Suggestions).DistinctBy(s => s.Url).ToList());

        _advicePanel.ResumeLayout();
    }

    private void AddAdviceSection(string title, List<DriverSuggestion> suggestions)
    {
        var row = _advicePanel.RowCount++;
        _advicePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var section = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12)
        };
        section.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        if (suggestions.Count == 0)
        {
            section.Controls.Add(new Label
            {
                Text = "暂无匹配建议。",
                AutoSize = true,
                ForeColor = Color.DimGray
            });
        }
        else
        {
            foreach (var suggestion in suggestions)
            {
                section.Controls.Add(CreateSuggestionLink(suggestion));
            }
        }

        _advicePanel.Controls.Add(section, 0, row);
    }

    private LinkLabel CreateSuggestionLink(DriverSuggestion suggestion)
    {
        var link = new LinkLabel
        {
            Text = suggestion.Title,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2)
        };
        if (!string.IsNullOrWhiteSpace(suggestion.Note))
        {
            _toolTip.SetToolTip(link, suggestion.Note);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("复制链接", null, (_, _) => CopyToClipboard(suggestion.Url));
        link.ContextMenuStrip = menu;
        link.LinkClicked += (_, _) => OpenUrl(suggestion.Url);
        return link;
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            _statusLabel.Text = "链接已复制";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "复制失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "链接格式无效：" + url, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法打开链接：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportHtml()
    {
        if (_snapshot is null || _advice is null)
        {
            MessageBox.Show(this, "请先完成采集。", "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "HTML 报告 (*.html)|*.html",
            FileName = $"硬件报告_{SafeFileName(_snapshot.System.Hostname)}_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, HtmlReportBuilder.Build(_snapshot, _advice, ToolVersion), new UTF8Encoding(true));
            _statusLabel.Text = "已导出 HTML：" + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportJson()
    {
        if (_snapshot is null)
        {
            MessageBox.Show(this, "请先完成采集。", "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON 快照 (*.json)|*.json",
            FileName = $"硬件快照_{SafeFileName(_snapshot.System.Hostname)}_{DateTime.Now:yyyyMMdd_HHmm}.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, JsonDefaults.Serialize(_snapshot), new UTF8Encoding(false));
            _statusLabel.Text = "已导出 JSON：" + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "硬件助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowPanel()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private static string SafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PC";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static string FormatGb(long bytes) => (bytes / 1073741824.0).ToString("0.#");
}
```

- [ ] **Step 2: 替换 Program.cs**

`src/HwOps.Agent/Program.cs`:

```csharp
namespace HwOps.Agent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }
}
```

- [ ] **Step 3: 编译**

```bash
dotnet build HwOps.sln -c Release
```

Expected: 0 errors。若出现 CS8618（`_trayIcon` 未赋值）说明构造函数被提前 return，检查 `LoadRules` 是否抛异常。

- [ ] **Step 4: 启动界面做初步冒烟**

```bash
dotnet run --project src/HwOps.Agent -c Release
```

Expected: 窗口出现，2–8 秒内标题栏下方显示主机/主板/BIOS，四个标签页可切换；右下状态栏显示设备数与问题数。

手动核对：
- 概况页数值与本机实际一致（系统设置 → 关于、`msinfo32` 对照主板/BIOS）。
- 设备列表有数据，列头中文正常。
- 问题设备页：若本机无问题设备，显示「没有发现带问题代码的设备。」
- 驱动建议页：至少出现主板/OEM 链接；点击可在默认浏览器打开。
- 关闭窗口后程序留在托盘，双击托盘图标可恢复；托盘「退出」可真正退出。

- [ ] **Step 5: Commit**

```bash
git add src/HwOps.Agent/MainForm.cs src/HwOps.Agent/Program.cs
git commit -m "feat: add WinForms agent UI with local report"
```

---

## Task 12: 无界面自检入口

**Files:**
- Create: `src/HwOps.Agent/SelfTest.cs`
- Modify: `src/HwOps.Agent/Program.cs`

- [ ] **Step 1: 实现 SelfTest**

`src/HwOps.Agent/SelfTest.cs`:

```csharp
using System.Text;
using HwOps.Core.Collection;
using HwOps.Core.Models;
using HwOps.Core.Reporting;
using HwOps.Core.Rules;

namespace HwOps.Agent;

internal static class SelfTest
{
    public static int Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var snapshot = new WmiHardwareCollector().Collect();
        var advice = new DriverAdvisor(RuleSet.LoadEmbedded()).Advise(snapshot);

        File.WriteAllText(
            Path.Combine(outputDirectory, "snapshot.json"),
            JsonDefaults.Serialize(snapshot),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(outputDirectory, "report.html"),
            HtmlReportBuilder.Build(snapshot, advice, "0.1.0"),
            new UTF8Encoding(true));

        File.WriteAllText(
            Path.Combine(outputDirectory, "selftest.log"),
            $"OK host={snapshot.System.Hostname} board={snapshot.Board.Manufacturer} {snapshot.Board.Product} " +
            $"devices={snapshot.Devices.Count} problems={snapshot.ProblemDevices.Count} platform={advice.Platform.Count}" +
            Environment.NewLine,
            new UTF8Encoding(false));

        return 0;
    }
}
```

- [ ] **Step 2: 修改 Program 支持 `--selftest`**

`src/HwOps.Agent/Program.cs`:

```csharp
using System.Text;

namespace HwOps.Agent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return SelfTest.Run(args[1]);
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(args[1]);
                    File.WriteAllText(Path.Combine(args[1], "selftest.error.txt"), ex.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // 自检失败时不再抛出，退出码已能说明问题
                }

                return 1;
            }
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }
}
```

- [ ] **Step 3: 运行自检**

```bash
rm -rf dist/selftest
dotnet run --project src/HwOps.Agent -c Release -- --selftest ./dist/selftest
echo "exit=$?"
cat dist/selftest/selftest.log
ls -lh dist/selftest
```

Expected:

- `exit=0`
- `selftest.log` 以 `OK host=` 开头，包含 `devices=` 与 `platform=`
- 生成 `snapshot.json`、`report.html`

再检查报告内容：

```bash
grep -c "https://" dist/selftest/report.html
grep -q "问题设备" dist/selftest/report.html && echo "sections-ok"
```

Expected: 链接数 ≥ 1；输出 `sections-ok`。

- [ ] **Step 4: Commit**

```bash
git add src/HwOps.Agent/SelfTest.cs src/HwOps.Agent/Program.cs
git commit -m "feat: add headless selftest mode"
```

---

## Task 13: 构建脚本、README 与发布验收

**Files:**
- Create: `scripts/build.ps1`
- Create: `README.md`

- [ ] **Step 1: 写构建脚本**

`scripts/build.ps1`:

```powershell
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet test HwOps.sln -c Release

dotnet publish src/HwOps.Agent/HwOps.Agent.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist/agent

Write-Host ""
Write-Host "Published to dist/agent:" -ForegroundColor Green
Get-ChildItem dist/agent | Format-Table Name, Length
```

- [ ] **Step 2: 写 README**

`README.md`:

````markdown
# HwOps 硬件助手（M1）

内网硬件运维工具的本机部分：采集硬件与驱动状态，匹配官方驱动下载页，导出 HTML/JSON 报告。

## 构建与测试

```bash
dotnet build HwOps.sln
dotnet test HwOps.sln
```

## 运行

```bash
dotnet run --project src/HwOps.Agent
```

## 无界面自检

```bash
dotnet run --project src/HwOps.Agent -c Release -- --selftest ./dist/selftest
```

自检会生成 `snapshot.json`、`report.html`、`selftest.log`，不需要人工点界面。

## 发布单文件

```bash
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

输出：`dist/agent/HwOps.Agent.exe`（自包含单文件，实测约 50 MB；目标机免装 .NET 运行时）。

## 功能范围（M1）

- 采集：系统、主板、BIOS、CPU、内存、显卡、磁盘、网络、声音、全部 PnP 设备与驱动版本
- 问题设备：按 `ConfigManagerErrorCode` 标记并解释
- 驱动建议：OEM / 主板 / 设备硬件 ID / Microsoft Update Catalog 兜底
- 导出：单文件 HTML 报告、JSON 快照
- 托盘常驻，关闭窗口不退出

## 不包含（M2/M3）

- 内网发现、配对、远程拉取
- 规则编辑器、规则同步、UDP 信标
- 自动下载或安装驱动

## 已知限制

- 部分字段（如硬盘序列号）需要管理员权限，拿不到时标注为「需管理员」
- 规则里的厂商链接可能随官网改版失效，需要维护 `src/HwOps.Core/Rules/seed-rules.json`
- 未签名 exe 可能触发 360/火绒提示；分发前需真机验证并准备白名单材料
- 采集为只读操作，不读取文件、屏幕、账号或已安装软件清单
````

- [ ] **Step 3: 运行构建脚本**

```bash
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

Expected: 测试全部通过；`dist/agent/HwOps.Agent.exe` 存在，大小约 45–60 MB。

- [ ] **Step 4: 对发布产物跑自检**

```bash
rm -rf dist/published-selftest
./dist/agent/HwOps.Agent.exe --selftest ./dist/published-selftest
echo "exit=$?"
cat dist/published-selftest/selftest.log
```

Expected: `exit=0`，日志包含设备数与平台建议数。

- [ ] **Step 5: 种子链接人工核验**

用浏览器逐条打开 `src/HwOps.Core/Rules/seed-rules.json` 中的全部 URL（至少覆盖 10 条 OEM、8 条主板、3 条显卡），确认：

- 能正常打开且落在对应厂商的支持/下载页；
- 若跳转到首页或 404，更新 `urlTemplate` 并在 `note` 里写明站内定位方式。

这是人工步骤，不能由脚本判定；完成后在提交信息里注明「已核验」。

- [ ] **Step 6: 最终验收**

```bash
dotnet test HwOps.sln -c Release -v minimal
ls -lh dist/agent
```

Expected: 全绿；exe 存在。

请用户确认界面验收（必须由人完成）：

- 双击 `dist/agent/HwOps.Agent.exe`，等待采集完成；
- 概况页与 `msinfo32` 对照一致；
- 问题设备页与设备管理器中的黄色感叹号一致；
- 驱动建议链接可打开，右键可复制；
- 导出 HTML 后用浏览器打开，内容完整、中文正常；
- 关闭窗口后托盘图标仍在，可通过托盘退出。

- [ ] **Step 7: Commit**

```bash
git add scripts/build.ps1 README.md
git commit -m "chore: add build script and project README"
```

---

## M1 完成定义

- `dotnet test HwOps.sln -c Release` 全绿。
- `dist/agent/HwOps.Agent.exe` 可在干净的 Windows 10 22H2 / Windows 11 上双击运行，无需安装运行时。
- 无网络时报告、建议、导出全部可用（链接点击除外）。
- 自检命令 `--selftest` 在开发机与发布产物上均返回 0。
- 种子规则链接完成人工核验，失效链接已修正。
- 用户完成界面验收。

M1 通过后，再编写 M2（反向连接、配对、拉取、代开链接）实现计划。
