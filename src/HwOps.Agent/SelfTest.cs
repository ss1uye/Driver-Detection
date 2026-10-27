using System.Diagnostics;
using System.Globalization;
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

        var collectStopwatch = Stopwatch.StartNew();
        var snapshot = new WmiHardwareCollector().Collect();
        collectStopwatch.Stop();

        var rules = RuleSet.LoadEmbedded();
        var advice = new DriverAdvisor(rules).Advise(snapshot);

        File.WriteAllText(
            Path.Combine(outputDirectory, "snapshot.json"),
            JsonDefaults.Serialize(snapshot),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(outputDirectory, "report.html"),
            HtmlReportBuilder.Build(snapshot, advice, AgentVersion.Current, rules.RulesVersion),
            new UTF8Encoding(true));

        var gpuSuggestionCount = advice.GpuDevices.Sum(g => g.Suggestions.Count);

        var log = new StringBuilder();
        log.Append("OK")
            .Append(" host=").Append(snapshot.System.Hostname)
            .Append(" board=").Append(snapshot.Board.Manufacturer).Append(' ').Append(snapshot.Board.Product)
            .Append(" devices=").Append(snapshot.Devices.Count)
            .Append(" problems=").Append(snapshot.ProblemDevices.Count)
            .Append(" platform=").Append(advice.Platform.Count)
            .Append(" gpu=").Append(gpuSuggestionCount)
            .Append(" rulesVersion=").Append(rules.RulesVersion)
            .Append(" collectMs=").Append(collectStopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture))
            .Append(" toolVersion=").Append(AgentVersion.Current)
            .Append(Environment.NewLine);

        if (snapshot.Collection.Warnings.Count == 0)
        {
            log.Append("warnings=0").Append(Environment.NewLine);
        }
        else
        {
            log.Append("warnings=").Append(snapshot.Collection.Warnings.Count).Append(Environment.NewLine);
            foreach (var warning in snapshot.Collection.Warnings)
            {
                log.Append("warning: ").Append(warning).Append(Environment.NewLine);
            }
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, "selftest.log"),
            log.ToString(),
            new UTF8Encoding(false));

        return 0;
    }
}
