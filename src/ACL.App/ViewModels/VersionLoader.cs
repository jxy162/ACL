using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;

namespace ACL.App.ViewModels;

/// <summary>版本清单加载（下载页 / 启动页 / 侧栏共用）。</summary>
public static class VersionLoader
{
    /// <summary>加载全部版本并写入共享状态；返回状态文本。</summary>
    public static async Task<string> LoadIntoAsync(AppServices services, AppState state)
    {
        Result<VersionManifest> manifest = await services.Versions.GetManifestAsync().ConfigureAwait(true);
        if (!manifest.IsSuccess)
        {
            return $"获取版本清单失败：{manifest.Code}（{manifest.Detail}）";
        }

        HashSet<string> installed = [.. state.Instances.Select(instance => instance.Id)];
        state.Versions.Clear();
        foreach (VersionEntry entry in manifest.Value!.Versions)
        {
            state.Versions.Add(new VersionRowViewModel(entry.Id, entry.Type, entry.ReleaseTime)
            {
                IsInstalled = installed.Contains(entry.Id),
            });
        }

        state.SelectedVersion ??= state.Versions.FirstOrDefault(v => v.Type == "release") ?? state.Versions.FirstOrDefault();
        return $"已加载 {state.Versions.Count} 个版本（正式版 {Count(state, VersionCategory.Release)}，快照 {Count(state, VersionCategory.Snapshot)}，预览 {Count(state, VersionCategory.Preview)}，远古 {Count(state, VersionCategory.Ancient)}）";
    }

    private static int Count(AppState state, string category)
        => state.Versions.Count(row => row.Category == category);
}