using System.Text.Json;
using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine.Skills.Implementations;

/// <summary>
/// 简单计算器函数技能：执行四则运算和幂运算。
/// 注册为 Calculator，LLM 可在需要数学计算时调用。
/// </summary>
public class SimpleCalculatorSkill : IFunctionSkill
{
    public string Name => "Calculator";
    public string Description => "执行数学计算：加法、减法、乘法、除法、幂运算，支持浮点数";
    public string InputSchemaJson => /* language=json */ """
        {
            "type": "object",
            "properties": {
                "a": {
                    "type": "number",
                    "description": "第一个操作数"
                },
                "b": {
                    "type": "number",
                    "description": "第二个操作数"
                },
                "operator": {
                    "type": "string",
                    "description": "运算符：add（加）、subtract（减）、multiply（乘）、divide（除）、power（幂）",
                    "enum": ["add", "subtract", "multiply", "divide", "power"]
                }
            },
            "required": ["a", "b", "operator"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return Fail("缺少参数");
        }

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("a", out var aProp))
                return Fail("缺少参数 'a'");
            if (!root.TryGetProperty("b", out var bProp))
                return Fail("缺少参数 'b'");
            if (!root.TryGetProperty("operator", out var opProp) || opProp.ValueKind != JsonValueKind.String)
                return Fail("缺少参数 'operator'");

            var a = aProp.GetDouble();
            var b = bProp.GetDouble();
            var op = opProp.GetString()?.ToLowerInvariant() ?? string.Empty;

            double resultValue;
            string opSymbol;

            switch (op)
            {
                case "add":
                    resultValue = a + b;
                    opSymbol = "+";
                    break;
                case "subtract":
                    resultValue = a - b;
                    opSymbol = "-";
                    break;
                case "multiply":
                    resultValue = a * b;
                    opSymbol = "×";
                    break;
                case "divide":
                    if (b == 0)
                        return Fail("除数不能为 0");
                    resultValue = a / b;
                    opSymbol = "÷";
                    break;
                case "power":
                    resultValue = Math.Pow(a, b);
                    opSymbol = "^";
                    break;
                default:
                    return Fail($"不支持的运算符: {op}");
            }

            var expression = $"{FormatNumber(a)} {opSymbol} {FormatNumber(b)}";
            var isInteger = resultValue == Math.Truncate(resultValue) && Math.Abs(resultValue) < long.MaxValue;

            var response = new Dictionary<string, object>
            {
                ["expression"] = expression,
                ["result"] = isInteger ? (object)(long)resultValue : Math.Round(resultValue, 10),
                ["operator"] = op
            };

            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (JsonException)
        {
            return Fail("参数格式错误");
        }
    }

    private static string FormatNumber(double value)
    {
        return value == Math.Truncate(value) && Math.Abs(value) < long.MaxValue
            ? ((long)value).ToString()
            : value.ToString("G");
    }

    private static Task<string> Fail(string reason)
    {
        var result = new Dictionary<string, object> { ["error"] = reason };
        return Task.FromResult(JsonSerializer.Serialize(result));
    }
}
