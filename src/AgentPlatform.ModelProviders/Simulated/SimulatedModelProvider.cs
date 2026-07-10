using System.Text.Json;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.ModelProviders.Simulated;

/// <summary>
/// 模拟 LLM 实现，用于在没有真实 LLM 的情况下测试完整的 Agent → FunctionCall → Skill/MCP 链路
/// 
/// 它会智能地检测用户输入中是否应该触发 function call，
/// 如果匹配到 Skill/MCP 工具则返回 function_call JSON，否则返回模拟回复。
/// </summary>
public class SimulatedModelProvider : IModelProvider
{
    private readonly ILogger<SimulatedModelProvider> _logger;
    private readonly Dictionary<string, (string Description, string Schema)> _knownFunctions = new();

    public string ProviderName => "SimulatedLLM";

    public SimulatedModelProvider(ILogger<SimulatedModelProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册已知的函数定义，用于模拟 function calling 行为
    /// </summary>
    public void RegisterFunctions(Dictionary<string, (string Description, string Schema)> functions)
    {
        foreach (var (name, def) in functions)
        {
            _knownFunctions[name] = def;
        }
        _logger.LogInformation("Registered {Count} functions for simulated LLM", functions.Count);
    }

    public Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var lastMessage = request.Messages.LastOrDefault();
        var userInput = lastMessage?.Content ?? "";

        _logger.LogInformation("SimulatedLLM received: {Input}", userInput[..Math.Min(userInput.Length, 200)]);

        // 尝试匹配 function call
        foreach (var (name, def) in _knownFunctions)
        {
            if (ShouldCallFunction(userInput, name))
            {
                var args = ExtractArguments(userInput, name, def.Schema);
                var fcJson = JsonSerializer.Serialize(new
                {
                    name,
                    arguments = args
                });

                _logger.LogInformation("SimulatedLLM → function_call: {Name}({Args})", name, JsonSerializer.Serialize(args));

                return Task.FromResult(new ChatCompletionResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    Model = "simulated",
                    Content = fcJson,
                    RawResponse = fcJson,
                    InputTokens = userInput.Length / 4,
                    OutputTokens = fcJson.Length / 4
                });
            }
        }

        // 否则返回普通文本
        var textResponse = GenerateTextResponse(userInput);

        return Task.FromResult(new ChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString(),
            Model = "simulated",
            Content = textResponse,
            RawResponse = textResponse,
            InputTokens = userInput.Length / 4,
            OutputTokens = textResponse.Length / 4
        });
    }

    public async IAsyncEnumerable<string> CompleteStreamAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var response = await CompleteAsync(request, ct);
        foreach (var ch in response.Content)
        {
            yield return ch.ToString();
        }
    }

    public Task<bool> HealthCheckAsync() => Task.FromResult(true);

    /// <summary>
    /// 判断用户输入是否应该触发指定函数
    /// </summary>
    private static bool ShouldCallFunction(string input, string functionName)
    {
        // 简单的关键词匹配
        var keywords = functionName.ToLower().Split('_');
        var inputLower = input.ToLower();
        return keywords.Any(k => inputLower.Contains(k)) ||
               inputLower.Contains("调用") ||
               inputLower.Contains("执行") ||
               inputLower.Contains("查") ||
               inputLower.Contains("搜索");
    }

    /// <summary>
    /// 从用户输入中提取函数参数
    /// </summary>
    private static Dictionary<string, object?> ExtractArguments(string input, string functionName, string schema)
    {
        // 简单参数提取：从输入中摘取关键信息
        var args = new Dictionary<string, object?>();

        // 提取可能的 query 参数
        if (input.Contains("搜索") || input.Contains("查"))
        {
            var searchIdx = input.IndexOf("搜索");
            var chaIdx = input.IndexOf("查");
            var idx = searchIdx >= 0 ? searchIdx : chaIdx;
            if (idx >= 0)
            {
                var query = input[(idx + 2)..].Split('，', ',', '。', '.')[0].Trim();
                if (!string.IsNullOrWhiteSpace(query))
                    args["query"] = query;
            }
        }

        args.TryAdd("query", input);
        args.TryAdd("input", input);

        return args;
    }

    /// <summary>
    /// 生成普通文本回复
    /// </summary>
    private static string GenerateTextResponse(string input)
    {
        if (input.Contains("好") || input.Contains("hello") || input.Contains("你好"))
            return "你好！我是 Agent 平台模拟助手。我可以帮你调用工具箱中的函数来处理任务。请告诉我你需要什么帮助？";

        if (input.Contains("天气"))
            return "要查询天气信息，我需要使用 weather_query 函数。请在 Agent 配置中绑定天气查询技能。";

        return $"已收到您的消息：「{input[..Math.Min(input.Length, 50)]}...」。如需调用工具，我会自动识别并执行。";
    }
}
