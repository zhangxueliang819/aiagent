using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.ModelProviders.Simulated;

/// <summary>
/// 模拟 IChatClient 实现（V2.0 MEAI 标准接口），用于在没有真实 LLM 时测试 Agent 链路。
/// 支持简单的 function call 模拟：检测输入中的关键词并生成 tool_calls。
/// </summary>
public class SimulatedModelProvider : IChatClient
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

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.LastOrDefault();
        var userInput = lastMessage?.Text ?? "";

        _logger.LogInformation("SimulatedLLM received: {Input}", userInput[..Math.Min(userInput.Length, 200)]);

        // 尝试匹配 function call
        foreach (var (name, def) in _knownFunctions)
        {
            if (ShouldCallFunction(userInput, name))
            {
                var args = ExtractArguments(userInput, name, def.Schema);
                var argsJson = JsonSerializer.Serialize(args);
                var callId = $"call_{Guid.NewGuid():N}";

                _logger.LogInformation("SimulatedLLM → function_call: {Name}({Args})", name, argsJson);

                // 返回含 FunctionCallContent 的 ChatResponse
                var fcContent = new FunctionCallContent(callId, name, new Dictionary<string, object?>(args));
                var fcMessage = new ChatMessage(ChatRole.Assistant, [fcContent]);

                return Task.FromResult(new ChatResponse(fcMessage)
                {
                    ResponseId = Guid.NewGuid().ToString(),
                    ModelId = "simulated",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = userInput.Length / 4,
                        OutputTokenCount = argsJson.Length / 4
                    }
                });
            }
        }

        // 否则返回普通文本
        var textResponse = GenerateTextResponse(userInput);

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, textResponse))
        {
            ResponseId = Guid.NewGuid().ToString(),
            ModelId = "simulated",
            Usage = new UsageDetails
            {
                InputTokenCount = userInput.Length / 4,
                OutputTokenCount = textResponse.Length / 4
            }
        });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 直接调用非流式逻辑获取完整响应
        var lastMessage = messages.LastOrDefault();
        var userInput = lastMessage?.Text ?? "";

        _logger.LogInformation("SimulatedLLM streaming received: {Input}", userInput[..Math.Min(userInput.Length, 200)]);

        bool functionCalled = false;

        // 尝试匹配 function call（模拟工具调用）
        foreach (var (name, def) in _knownFunctions)
        {
            if (ShouldCallFunction(userInput, name))
            {
                var args = ExtractArguments(userInput, name, def.Schema);
                var argsJson = System.Text.Json.JsonSerializer.Serialize(args);
                var callId = $"call_{Guid.NewGuid():N}";

                _logger.LogInformation("SimulatedLLM streaming → function_call: {Name}({Args})", name, argsJson);

                // 先发送工具调用
                var fcContent = new FunctionCallContent(callId, name, new Dictionary<string, object?>(args));
                yield return new ChatResponseUpdate(ChatRole.Assistant, [fcContent]);

                // 发送工具调用后的文本说明
                var toolText = $"[模拟工具调用] {name}({argsJson})";
                foreach (var ch in toolText)
                    yield return new ChatResponseUpdate(ChatRole.Assistant, ch.ToString());

                functionCalled = true;
                break;
            }
        }

        // 无工具调用时生成普通文本响应
        if (!functionCalled)
        {
            var textResponse = GenerateTextResponse(userInput);
            foreach (var ch in textResponse)
                yield return new ChatResponseUpdate(ChatRole.Assistant, ch.ToString());
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    void IDisposable.Dispose() { }

    /// <summary>
    /// 判断用户输入是否应该触发指定函数
    /// </summary>
    private static bool ShouldCallFunction(string input, string functionName)
    {
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
        var args = new Dictionary<string, object?>();

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

    private static string GenerateTextResponse(string input)
    {
        if (input.Contains("好") || input.Contains("hello") || input.Contains("你好"))
            return "你好！我是 Agent 平台模拟助手。我可以帮你调用工具箱中的函数来处理任务。请告诉我你需要什么帮助？";

        if (input.Contains("天气"))
            return "要查询天气信息，我需要使用 weather_query 函数。请在 Agent 配置中绑定天气查询技能。";

        return $"已收到您的消息：「{input[..Math.Min(input.Length, 50)]}...」。如需调用工具，我会自动识别并执行。";
    }
}
