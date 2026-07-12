using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using AgentPlatform.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly SkillService _service;
    private readonly FunctionSkillRegistry _skillRegistry;

    public SkillsController(SkillService service, FunctionSkillRegistry skillRegistry)
    {
        _service = service;
        _skillRegistry = skillRegistry;
    }

    /// <summary>获取已注册的 FunctionTool 执行器列表（前端创建/编辑技能时选择）</summary>
    [HttpGet("executor-types")]
    public ActionResult<ApiResponse<List<ExecutorTypeDto>>> GetExecutorTypes()
    {
        var executors = _skillRegistry.GetAll()
            .Select(e => new ExecutorTypeDto(e.Name, e.Description, e.InputSchemaJson))
            .ToList();
        return Ok(new ApiResponse<List<ExecutorTypeDto>>(true, "OK", executors));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SkillDto>>>> GetAll(CancellationToken ct)
    {
        var skills = await _service.GetAllAsync(ct);
        return Ok(new ApiResponse<List<SkillDto>>(true, "OK", skills));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillDto>>> GetById(Guid id, CancellationToken ct)
    {
        var s = await _service.GetByIdAsync(id, ct);
        if (s is null) return NotFound(new ApiResponse<SkillDto>(false, "Not found", null));
        return Ok(new ApiResponse<SkillDto>(true, "OK", s));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SkillDto>>> Create([FromBody] CreateSkillRequest request, CancellationToken ct)
    {
        try
        {
            var s = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = s.Id }, new ApiResponse<SkillDto>(true, "Created", s));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<SkillDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillDto>>> Update(Guid id, [FromBody] UpdateSkillRequest request, CancellationToken ct)
    {
        try
        {
            var s = await _service.UpdateAsync(id, request, ct);
            return Ok(new ApiResponse<SkillDto>(true, "Updated", s));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<SkillDto>(false, ex.Message, null));
        }
    }

    /// <summary>上传技能包（multipart/form-data .zip）</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50 MB
    public async Task<ActionResult<ApiResponse<SkillUploadResponse>>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiResponse<SkillUploadResponse>(false, "请选择要上传的文件", null));

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse<SkillUploadResponse>(false, "仅支持 .zip 格式的技能包", null));

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _service.UploadAsync(stream, file.FileName, ct);
            return Ok(new ApiResponse<SkillUploadResponse>(true, "上传成功", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<SkillUploadResponse>(false, ex.Message, null));
        }
    }

    /// <summary>查看技能包内文件列表</summary>
    [HttpGet("{id:guid}/files")]
    public ActionResult<ApiResponse<List<SkillFileItem>>> GetFiles(Guid id)
    {
        var files = _service.GetFileList(id);
        return Ok(new ApiResponse<List<SkillFileItem>>(true, "OK", files));
    }

    /// <summary>下载/预览技能包内单个文件</summary>
    [HttpGet("{id:guid}/files/{**filePath}")]
    public ActionResult GetFile(Guid id, string filePath)
    {
        var result = _service.GetFileContent(id, filePath);
        if (result is null)
            return NotFound(new ApiResponse<object>(false, "File not found", null));

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>获取技能包内单个文件文本内容（JSON 格式）</summary>
    [HttpGet("{id:guid}/files/text/{**filePath}")]
    public async Task<ActionResult<ApiResponse<SkillFileContentDto>>> GetFileText(Guid id, string filePath)
    {
        var result = await _service.GetFileContentTextAsync(id, filePath);
        if (result is null)
            return NotFound(new ApiResponse<SkillFileContentDto>(false, "File not found", null));

        return Ok(new ApiResponse<SkillFileContentDto>(true, "OK",
            new SkillFileContentDto(result.Value.Content, result.Value.FileName)));
    }

    /// <summary>更新技能包内单个文件内容</summary>
    [HttpPut("{id:guid}/files/{**filePath}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateFile(Guid id, string filePath,
        [FromBody] UpdateFileContentRequest request)
    {
        var success = await _service.UpdateFileContentAsync(id, filePath, request.Content);
        if (!success)
            return NotFound(new ApiResponse<object>(false, "File not found", null));

        return Ok(new ApiResponse<object>(true, "Saved", null));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }
}
