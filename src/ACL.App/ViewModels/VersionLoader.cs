using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;

namespace ACL.App.ViewModels;

/// <summary>版本清单加载（侧栏与下载页共用）。</summary>
public static class VersionLoader
{
    /// <summary>加载并写入共享状态；返回状态文本。</summary>
    public static async Task<string> LoadIntoAsync(AppServices services, AppState state, int limit = 60)
    {
        Result<VersionManifest> manifest = await services.Versions.GetManifestAsync().ConfigureAwait(true);
        if (!manifest.IsSuccess)
        {
            return $"获取版本清单失败：{manifest.Code}（{manifest.Detail}）";
        }

        state.Versions.Clear();
        foreach (VersionEntry entry in manifest.Value!.Versions.Take(limit))
        {
            state.Versions.Add(new VersionRowViewModel(entry.Id, entry.Type, entry.ReleaseTime));
        }

        state.SelectedVersion ??= state.Versions.FirstOrDefault(v => v.Type == "release") ?? state.Versions.FirstOrDefault();
        return $"共 {manifest.Value.Versions.Count} 个版本（显示前 {state.Versions.Count} 个）";
    }
}