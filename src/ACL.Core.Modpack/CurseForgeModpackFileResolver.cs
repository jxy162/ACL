using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Resources;

namespace ACL.Core.Modpack;

/// <summary>用 CurseForge API 解析整合包中缺失的直链（按 fileID 批量查询并精确回映射）。</summary>
public sealed class CurseForgeModpackFileResolver : IModpackFileResolver
{
    private readonly CurseForgeClient _client;

    /// <summary>构造解析器。</summary>
    public CurseForgeModpackFileResolver(CurseForgeClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ModpackFile>>> ResolveAsync(
        ModpackManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<ModpackFile> unresolved = [.. manifest.UnresolvedFiles.Where(file => file.FileId is not null)];
        if (unresolved.Count == 0)
        {
            return Result<IReadOnlyList<ModpackFile>>.Success([]);
        }

        Result<IReadOnlyList<ResourceFile>> fetched = await _client
            .GetFilesByIdsAsync([.. unresolved.Select(file => file.FileId!.Value)], cancellationToken)
            .ConfigureAwait(false);
        if (!fetched.IsSuccess)
        {
            return Result<IReadOnlyList<ModpackFile>>.Failure(fetched.Code, fetched.Detail);
        }

        Dictionary<string, ResourceFile> byId = new(StringComparer.Ordinal);
        foreach (ResourceFile file in fetched.Value!)
        {
            if (!string.IsNullOrEmpty(file.SourceId))
            {
                byId[file.SourceId] = file;
            }
        }

        List<ModpackFile> resolved = [];
        foreach (ModpackFile file in unresolved)
        {
            string id = file.FileId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!byId.TryGetValue(id, out ResourceFile? match))
            {
                continue;
            }

            resolved.Add(file with
            {
                RelativePath = $"{FolderFor(match.FileName)}/{match.FileName}",
                Downloads = [match.Url],
                Sha1 = match.Sha1,
                Sha512 = match.Sha512,
                Size = match.Size,
            });
        }

        return Result<IReadOnlyList<ModpackFile>>.Success(resolved);
    }

    /// <summary>按文件名启发式决定落盘子目录（整合包格式本身不携带该信息）。</summary>
    private static string FolderFor(string fileName)
    {
        if (fileName.Contains("shader", StringComparison.OrdinalIgnoreCase))
        {
            return "shaderpacks";
        }

        if (fileName.Contains("resourcepack", StringComparison.OrdinalIgnoreCase))
        {
            return "resourcepacks";
        }

        return "mods";
    }
}