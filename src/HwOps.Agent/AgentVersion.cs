using System.Reflection;

namespace HwOps.Agent;

internal static class AgentVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var informational = typeof(AgentVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
    }
}
