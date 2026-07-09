using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.ModelProviders.Mcp;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class McpEndpointsController : ControllerBase
{
    private readonly IMcpEndpointRepository _repo;
    private readonly IMcpToolRepository _toolRepo;
    private readonly McpClient _mcpClient;

    public McpEndpointsController(IMcpEndpointRepository repo, IMcpToolRepository toolRepo, McpClient mcpClient)
    {
        _repo = repo;
        _toolRepo = toolRepo;
        _mcpClient = mcpClient;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<McpEndpointDto>>>> GetAll(CancellationToken ct)
    {
        var endpoints = await _repo.GetAllAsync(ct);
        var dtos = endpoints.Select(Map).ToList();
        return Ok(new ApiResponse<List<McpEndpointDto>>(true, "OK", dtos));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<McpEndpointDto>>> GetById(Guid id, CancellationToken ct)
    {
        var e = await _repo.GetByIdWithToolsAsync(id, ct);
        if (e is null) return NotFound(new ApiResponse<McpEndpointDto>(false, "Not found", null));
        return Ok(new ApiResponse<McpEndpointDto>(true, "OK", Map(e)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<McpEndpointDto>>> Create([FromBody] CreateMcpRequest request, CancellationToken ct)
    {
        var endpoint = new McpEndpoint
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            EndpointUrl = request.EndpointUrl,
            Protocol = request.Protocol,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _repo.AddAsync(endpoint, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<McpEndpointDto>(true, "Created", Map(result)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<McpEndpointDto>>> Update(Guid id, [FromBody] UpdateMcpRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdWithToolsAsync(id, ct);
        if (existing is null) return NotFound(new ApiResponse<McpEndpointDto>(false, "Not found", null));

        existing.Name = request.Name;
        existing.EndpointUrl = request.EndpointUrl;
        existing.Protocol = request.Protocol;
        existing.IsEnabled = request.IsEnabled;

        var result = await _repo.UpdateAsync(existing, ct);
        return Ok(new ApiResponse<McpEndpointDto>(true, "Updated", Map(result)));
    }

    [HttpPost("{id:guid}/discover")]
    public async Task<ActionResult<ApiResponse<List<McpToolDto>>>> DiscoverTools(Guid id, CancellationToken ct)
    {
        var endpoint = await _repo.GetByIdWithToolsAsync(id, ct);
        if (endpoint is null) return NotFound(new ApiResponse<List<McpToolDto>>(false, "Not found", null));

        var tools = await _mcpClient.DiscoverToolsAsync(endpoint, ct);

        // 清空旧缓存，写入新工具
        await _toolRepo.ClearByEndpointIdAsync(id, ct);
        await _toolRepo.AddRangeAsync(tools, ct);

        var dtos = tools.Select(t => new McpToolDto(t.Id, t.ToolName, t.Description, t.InputSchema, t.IsEnabled)).ToList();
        return Ok(new ApiResponse<List<McpToolDto>>(true, $"Discovered {tools.Count} tools", dtos));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _repo.DeleteAsync(id, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }

    private static McpEndpointDto Map(McpEndpoint e) => new(e.Id, e.Name, e.EndpointUrl, e.Protocol,
        e.IsEnabled, e.CreatedAt,
        e.Tools.Select(t => new McpToolDto(t.Id, t.ToolName, t.Description, t.InputSchema, t.IsEnabled)).ToList());
}

public record McpEndpointDto(Guid Id, string Name, string EndpointUrl, string Protocol,
    bool IsEnabled, DateTime CreatedAt, List<McpToolDto> Tools);

public record McpToolDto(Guid Id, string ToolName, string Description, string InputSchema, bool IsEnabled);

public record CreateMcpRequest(string Name, string EndpointUrl, string Protocol = "sse");

public record UpdateMcpRequest(string Name, string EndpointUrl, string Protocol, bool IsEnabled);
