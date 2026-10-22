using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Modpack;

/// <summary>通用压缩包处理（无清单，整个归档视为覆盖文件）。始终可处理，作为兜底。</summary>
public sealed class GenericModpackHandler : IModpackFormatHandler
{
    /// <inheritdoc />
    public string Name => "GenericZip";

    /// <inheritdoc />
    public bool CanHandle(IModpackArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return true;
    }

    /// <inheritdoc />
    public Result<ModpackManifest> Parse(IModpackArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        return Result<ModpackManifest>.Success(new ModpackManifest(
            "Generic Pack",
            null,
            string.Empty,
            [],
            [],
            string.Empty,
            ModpackFormat.Generic));
    }
}