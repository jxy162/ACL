using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>版本 JSON 链合并（<c>inheritsFrom</c>）：子版本覆盖标量，库与参数按序拼接。</summary>
public static class VersionResolver
{
    private const int MaxChainDepth = 16;

    /// <summary>合并链（<paramref name="chain"/>[0] 为最老父版本，末尾为最终子版本）。</summary>
    public static VersionJson Merge(IReadOnlyList<VersionJson> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (chain.Count == 0)
        {
            throw new ArgumentException("版本链为空。", nameof(chain));
        }

        VersionJson result = chain[0];
        for (int i = 1; i < chain.Count; i++)
        {
            result = MergeTwo(result, chain[i]);
        }

        return result;
    }

    /// <summary>按 <c>inheritsFrom</c> 逐级加载并合并。</summary>
    /// <param name="id">起始版本名。</param>
    /// <param name="loader">按版本名加载版本 JSON。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task<Result<VersionJson>> ResolveAsync(
        string id,
        Func<string, CancellationToken, Task<Result<VersionJson>>> loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(loader);

        List<VersionJson> chain = [];
        HashSet<string> visited = new(StringComparer.Ordinal);
        string? current = id;

        while (current is not null)
        {
            if (!visited.Add(current))
            {
                return Result<VersionJson>.Failure(ErrorCode.ManifestCorrupt, $"inheritsFrom 存在环：{current}。");
            }

            if (visited.Count > MaxChainDepth)
            {
                return Result<VersionJson>.Failure(ErrorCode.ManifestCorrupt, "inheritsFrom 链过深。");
            }

            Result<VersionJson> loaded = await loader(current, cancellationToken).ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                return loaded;
            }

            chain.Insert(0, loaded.Value!);
            current = loaded.Value!.InheritsFrom;
        }

        return Result<VersionJson>.Success(Merge(chain));
    }

    private static VersionJson MergeTwo(VersionJson parent, VersionJson child)
    {
        Dictionary<string, LibraryRef> libraries = new(StringComparer.Ordinal);
        List<string> order = [];
        foreach (LibraryRef library in parent.Libraries)
        {
            if (libraries.TryAdd(library.Name, library))
            {
                order.Add(library.Name);
            }
        }

        foreach (LibraryRef library in child.Libraries)
        {
            if (libraries.ContainsKey(library.Name))
            {
                libraries[library.Name] = library;
            }
            else
            {
                libraries[library.Name] = library;
                order.Add(library.Name);
            }
        }

        return child with
        {
            InheritsFrom = null,
            MainClass = child.MainClass ?? parent.MainClass,
            Assets = child.Assets ?? parent.Assets,
            Type = child.Type ?? parent.Type,
            AssetIndex = child.AssetIndex ?? parent.AssetIndex,
            JavaVersion = child.JavaVersion ?? parent.JavaVersion,
            Downloads = child.Downloads ?? parent.Downloads,
            LegacyMinecraftArguments = child.LegacyMinecraftArguments ?? parent.LegacyMinecraftArguments,
            Libraries = [.. order.Select(name => libraries[name])],
            GameArguments = [.. parent.GameArguments, .. child.GameArguments],
            JvmArguments = [.. parent.JvmArguments, .. child.JvmArguments],
        };
    }
}