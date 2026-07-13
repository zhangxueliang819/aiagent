using System.Text.Json;
using System.Text.RegularExpressions;
using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine.Skills.Implementations;

/// <summary>
/// 文本分析函数技能：统计字数、字符数、行数、段落数等。
/// 注册为 TextAnalyzer，LLM 可在需要分析文本内容时调用。
/// </summary>
public class TextAnalyzerSkill : IFunctionSkill
{
    public string Name => "TextAnalyzer";
    public string Description => "分析文本内容，返回字数统计、字符数、行数、段落数等基本信息";
    public string InputSchemaJson => /* language=json */ """
        {
            "type": "object",
            "properties": {
                "text": {
                    "type": "string",
                    "description": "要分析的文本内容"
                }
            },
            "required": ["text"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        string text;

        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    text = textProp.GetString() ?? string.Empty;
                }
                else
                {
                    return Fail("缺少 'text' 参数");
                }
            }
            catch (JsonException)
            {
                return Fail("参数格式错误，需要 JSON 对象");
            }
        }
        else
        {
            return Fail("缺少 'text' 参数");
        }

        var charCount = text.Length;
        var charCountNoSpace = text.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "").Length;
        var wordCount = string.IsNullOrWhiteSpace(text) ? 0 : Regex.Matches(text, @"\b\w+\b").Count;
        var lineCount = text.Split('\n').Length;
        var paraCount = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries).Length;
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);

        var result = new Dictionary<string, object>
        {
            ["char_count"] = charCount,
            ["char_count_no_space"] = charCountNoSpace,
            ["word_count"] = wordCount,
            ["line_count"] = lineCount,
            ["paragraph_count"] = paraCount > 0 ? paraCount : 1,
            ["byte_count_utf8"] = byteCount
        };

        return Task.FromResult(JsonSerializer.Serialize(result));
    }

    private static Task<string> Fail(string reason)
    {
        var result = new Dictionary<string, object>
        {
            ["error"] = reason
        };
        return Task.FromResult(JsonSerializer.Serialize(result));
    }

}
