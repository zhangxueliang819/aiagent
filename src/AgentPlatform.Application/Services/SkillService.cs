using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.Application.Services;

public class SkillService
{
    private readonly ISkillRepository _repository;
    private readonly ILogger<SkillService> _logger;

    /// <summary>上传技能包解压根目录</summary>
    private readonly string _uploadRoot;

    public SkillService(ISkillRepository repository, ILogger<SkillService> logger)
    {
        _repository = repository;
        _logger = logger;
        _uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploaded-skills");
        Directory.CreateDirectory(_uploadRoot);
    }

    public async Task<List<SkillDto>> GetAllAsync(CancellationToken ct = default)
    {
        var skills = await _repository.GetAllAsync(ct);
        return skills.Select(Map).ToList();
    }

    public async Task<SkillDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _repository.GetByIdAsync(id, ct);
        return s is null ? null : Map(s);
    }

    public async Task<SkillDto> CreateAsync(CreateSkillRequest request, CancellationToken ct = default)
    {
        var storageType = SkillStorageType.Inline;
        if (!string.IsNullOrEmpty(request.StorageType))
            Enum.TryParse<SkillStorageType>(request.StorageType, true, out storageType);

        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Type = Enum.TryParse<SkillType>(request.Type, out var t) ? t : SkillType.FunctionTool,
            Implementation = request.Implementation,
            InputSchema = request.InputSchema,
            StorageType = storageType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Map(await _repository.AddAsync(skill, ct));
    }

    public async Task<SkillDto> UpdateAsync(Guid id, UpdateSkillRequest request, CancellationToken ct = default)
    {
        var skill = await _repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Skill {id} not found");

        if (request.Name is not null) skill.Name = request.Name;
        if (request.Description is not null) skill.Description = request.Description;
        if (request.Type is not null)
            skill.Type = Enum.TryParse<SkillType>(request.Type, out var t) ? t : SkillType.FunctionTool;
        if (request.Implementation is not null) skill.Implementation = request.Implementation;
        if (request.InputSchema is not null) skill.InputSchema = request.InputSchema;
        if (request.IsEnabled.HasValue) skill.IsEnabled = request.IsEnabled.Value;
        skill.UpdatedAt = DateTime.UtcNow;

        return Map(await _repository.UpdateAsync(skill, ct));
    }

    /// <summary>
    /// 上传技能包（.zip），解压并写入数据库
    /// </summary>
    public async Task<SkillUploadResponse> UploadAsync(Stream fileStream, string originalFileName, CancellationToken ct = default)
    {
        var skillId = Guid.NewGuid();
        var extractDir = Path.Combine(_uploadRoot, skillId.ToString());
        Directory.CreateDirectory(extractDir);

        var fileManifest = new List<SkillFileItem>();
        string? skillMdPath = null;

        try
        {
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // 验证是否包含 SKILL.md
            var skillMdEntry = archive.Entries.FirstOrDefault(
                e => string.Equals(e.Name, "SKILL.md", StringComparison.OrdinalIgnoreCase)
                     && string.IsNullOrEmpty(GetDirectoryPrefix(e.FullName)));

            if (skillMdEntry is null)
            {
                // 也检查一级子目录根
                skillMdEntry = archive.Entries.FirstOrDefault(
                    e => e.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase));
            }

            if (skillMdEntry is null)
                throw new InvalidOperationException("上传的 ZIP 包中未找到 SKILL.md 文件，请确保压缩包根目录包含 SKILL.md");

            // 解压所有文件
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目跳过

                var destPath = Path.Combine(extractDir, entry.FullName);
                var destDir = Path.GetDirectoryName(destPath)!;
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                entry.ExtractToFile(destPath, overwrite: true);

                fileManifest.Add(new SkillFileItem(
                    entry.FullName, entry.Length,
                    entry.LastWriteTime.UtcDateTime));

                if (entry.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(GetDirectoryPrefix(entry.FullName)))
                {
                    skillMdPath = destPath;
                }
            }

            // 解析 SKILL.md frontmatter
            if (skillMdPath is null)
                skillMdPath = Directory.GetFiles(extractDir, "SKILL.md", SearchOption.AllDirectories).FirstOrDefault();

            var (skillName, skillDesc) = ParseSkillMdFrontmatter(skillMdPath, originalFileName);

            var skill = new Skill
            {
                Id = skillId,
                Name = skillName,
                Description = skillDesc,
                Type = SkillType.AgentSkill,
                StorageType = SkillStorageType.File,
                StoragePath = extractDir,
                OriginalFileName = originalFileName,
                FileManifest = JsonSerializer.Serialize(fileManifest),
                InputSchema = "{}",
                Implementation = skillMdPath is not null ? await File.ReadAllTextAsync(skillMdPath, ct) : string.Empty,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var saved = await _repository.AddAsync(skill, ct);
            _logger.LogInformation("Skill uploaded: {Name} ({Id}), {FileCount} files", skillName, skillId, fileManifest.Count);

            return new SkillUploadResponse(Map(saved), fileManifest);
        }
        catch
        {
            // 清理失败的解压目录
            try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); }
            catch { /* best-effort */ }
            throw;
        }
    }

    /// <summary>获取技能包内文件列表</summary>
    public List<SkillFileItem> GetFileList(Guid skillId)
    {
        var dir = Path.Combine(_uploadRoot, skillId.ToString());
        if (!Directory.Exists(dir))
            return new();

        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f =>
            {
                var info = new FileInfo(f);
                return new SkillFileItem(
                    Path.GetRelativePath(dir, f).Replace('\\', '/'),
                    info.Length,
                    info.LastWriteTimeUtc);
            })
            .ToList();
    }

    /// <summary>获取技能包内单个文件内容</summary>
    public (Stream Content, string ContentType, string FileName)? GetFileContent(Guid skillId, string relativePath)
    {
        var dir = Path.Combine(_uploadRoot, skillId.ToString());
        var fullPath = Path.GetFullPath(Path.Combine(dir, relativePath));

        // 安全检查：确保路径在技能目录内
        if (!fullPath.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar)
            && fullPath != Path.GetFullPath(dir))
            return null;

        if (!File.Exists(fullPath))
            return null;

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".py" => "text/x-python",
            ".js" => "text/javascript",
            ".sh" => "text/x-shellscript",
            ".json" => "application/json",
            ".yaml" or ".yml" => "text/yaml",
            ".txt" => "text/plain",
            ".xml" => "text/xml",
            ".html" => "text/html",
            ".css" => "text/css",
            _ => "application/octet-stream"
        };

        return (File.OpenRead(fullPath), contentType, Path.GetFileName(fullPath));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var skill = await _repository.GetByIdAsync(id, ct);

        // 清理磁盘文件
        if (skill is not null && skill.StorageType != SkillStorageType.Inline)
        {
            var dir = skill.StoragePath;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete skill directory: {Dir}", dir); }
            }
        }

        await _repository.DeleteAsync(id, ct);
        return true;
    }

    private static SkillDto Map(Skill s) => new(
        s.Id, s.Name, s.Description, s.Type.ToString(),
        s.Implementation, s.InputSchema, s.IsEnabled,
        s.StorageType.ToString(), s.StoragePath, s.OriginalFileName, s.FileManifest,
        s.CreatedAt, s.UpdatedAt);

    // ---- helpers ----

    private static string GetDirectoryPrefix(string fullName)
    {
        var idx = fullName.LastIndexOf('/');
        return idx >= 0 ? fullName[..idx] : string.Empty;
    }

    /// <summary>解析 SKILL.md YAML frontmatter 提取 name / description</summary>
    private static (string name, string description) ParseSkillMdFrontmatter(string? skillMdPath, string fallbackName)
    {
        if (skillMdPath is null || !File.Exists(skillMdPath))
            return (fallbackName.Replace(".zip", ""), string.Empty);

        try
        {
            var content = File.ReadAllText(skillMdPath);
            var match = Regex.Match(content, @"^---\s*\n(.*?)\n---", RegexOptions.Singleline);
            if (!match.Success)
                return (fallbackName.Replace(".zip", ""), content[..Math.Min(200, content.Length)]);

            var frontmatter = match.Groups[1].Value;
            var name = "";
            var desc = "";

            foreach (var line in frontmatter.Split('\n'))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;
                var key = line[..colonIdx].Trim().ToLowerInvariant();
                var val = line[(colonIdx + 1)..].Trim().Trim('"', '\'');

                if (key == "name") name = val;
                else if (key == "description") desc = val;
            }

            if (string.IsNullOrEmpty(name))
                name = Path.GetFileNameWithoutExtension(fallbackName);

            return (name, desc);
        }
        catch
        {
            return (fallbackName.Replace(".zip", ""), string.Empty);
        }
    }
}
