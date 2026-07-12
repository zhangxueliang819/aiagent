using Microsoft.Extensions.Logging;
using AgentPlatform.AgentEngine.Harness;

namespace AgentPlatform.AgentEngine.Middleware;

/// <summary>
/// 输入输出转换中间件 — 对用户输入和 Agent 输出进行自定义转换。
///
/// 用途：
/// - 输入转换：脱敏、格式化、语言翻译、提示增强
/// - 输出转换：脱敏、格式化、后处理
///
/// 设计依据（Microsoft Agent Framework 文档 — 代理中间件）：
/// - 代理中间件使用 .Use() 装饰器模式
/// - 中间件可以检查或修改输入和输出
/// - 适用于任何 AIAgent 类型
/// </summary>
public class InputOutputTransformMiddleware : IAgentMiddleware
{
    private readonly Func<string, string>? _inputTransform;
    private readonly Func<string, string>? _outputTransform;
    private readonly ILogger<InputOutputTransformMiddleware> _logger;

    public string Name => "InputOutputTransform";

    public InputOutputTransformMiddleware(
        Func<string, string>? inputTransform,
        Func<string, string>? outputTransform,
        ILogger<InputOutputTransformMiddleware> logger)
    {
        _inputTransform = inputTransform;
        _outputTransform = outputTransform;
        _logger = logger;
    }

    public Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        if (_inputTransform is not null && !string.IsNullOrEmpty(context.UserMessage))
        {
            var original = context.UserMessage;
            context.UserMessage = _inputTransform(original);
            context.Properties["OriginalInput"] = original;

            if (original != context.UserMessage)
            {
                _logger.LogDebug(
                    "[MW-IOTransform] Input transformed: {OrigLen} → {NewLen} chars",
                    original.Length, context.UserMessage.Length);
            }
        }

        return Task.FromResult(true);
    }

    public Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
    {
        if (_outputTransform is not null)
        {
            // 提取响应文本进行转换
            var text = response switch
            {
                string s => s,
                AgentRunResult r => r.Content,
                _ => response?.ToString()
            };

            if (!string.IsNullOrEmpty(text))
            {
                var transformed = _outputTransform(text);
                context.Properties["TransformedOutput"] = transformed;

                // 如果是 AgentRunResult，更新其 Content
                if (response is AgentRunResult result)
                {
                    result.Content = transformed;
                }

                _logger.LogDebug("[MW-IOTransform] Output transformed: {OrigLen} → {NewLen} chars",
                    text.Length, transformed.Length);
            }
        }

        return Task.CompletedTask;
    }

    public Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "[MW-IOTransform] Error during transform");
        return Task.CompletedTask;
    }
}
