using System.Text.Json;
using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine.Skills.Implementations;

/// <summary>
/// 获取当前日期/时间的函数技能，支持时区格式化和多种日期格式。
/// 注册为 CurrentTime，LLM 可在需要时间信息时自动调用。
/// </summary>
public class CurrentTimeSkill : IFunctionSkill
{
    public string Name => "CurrentTime";
    public string Description => "获取当前系统日期和时间，支持时区格式化和多种输出格式";
    public string InputSchemaJson => /* language=json */ """
        {
            "type": "object",
            "properties": {
                "format": {
                    "type": "string",
                    "description": "输出格式：iso（ISO 8601）、date（仅日期）、time（仅时间）、full（完整日期时间，默认）",
                    "default": "full"
                },
                "timezone": {
                    "type": "string",
                    "description": "时区偏移，如 +08:00（北京时间）、+00:00（UTC）、-05:00（美东），默认使用系统时区",
                    "default": ""
                }
            }
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        var now = DateTimeOffset.Now;

        // 尝试解析参数
        if (!string.IsNullOrWhiteSpace(argumentsJson) && argumentsJson != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                var root = doc.RootElement;

                // 时区偏移
                if (root.TryGetProperty("timezone", out var tzProp) && tzProp.ValueKind == JsonValueKind.String)
                {
                    var tzStr = tzProp.GetString()!;
                    if (!string.IsNullOrWhiteSpace(tzStr))
                    {
                        if (TimeSpan.TryParse(tzStr, out var offset))
                        {
                            now = now.ToOffset(offset);
                        }
                    }
                }

                // 格式
                var format = "full";
                if (root.TryGetProperty("format", out var fmtProp) && fmtProp.ValueKind == JsonValueKind.String)
                {
                    format = fmtProp.GetString()?.ToLowerInvariant() ?? "full";
                }

                var result = format switch
                {
                    "iso" => now.ToString("o"),
                    "date" => now.ToString("yyyy-MM-dd"),
                    "time" => now.ToString("HH:mm:ss"),
                    "unix" => now.ToUnixTimeSeconds().ToString(),
                    _ => now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var response = new Dictionary<string, object>
                {
                    ["result"] = result,
                    ["format"] = format,
                    ["timezone"] = now.Offset.ToString(),
                    ["unix_timestamp"] = now.ToUnixTimeSeconds(),
                    ["utc_time"] = now.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                };
                return Task.FromResult(JsonSerializer.Serialize(response));
            }
            catch (JsonException)
            {
                // 参数解析失败，返回默认格式
            }
        }

        var defaultResponse = new Dictionary<string, object>
        {
            ["result"] = now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["timezone"] = now.Offset.ToString(),
            ["unix_timestamp"] = now.ToUnixTimeSeconds(),
            ["utc_time"] = now.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
        };
        return Task.FromResult(JsonSerializer.Serialize(defaultResponse));
    }
}
