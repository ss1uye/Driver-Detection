using System.Text.Encodings.Web;
using System.Text.Json;

namespace HwOps.Core.Models;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            RespectNullableAnnotations = true,
            // 仅用于本地 .json 导出；嵌入 HTML/HTTP 时必须另行编码。
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
