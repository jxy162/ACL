using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Modpack;

/// <summary>整合包格式处理器：识别归档并解析为 <see cref="ModpackManifest"/>。</summary>
public interface IModpackFormatHandler
{
    /// <summary>处理器名称。</summary>
    string Name { get; }

    /// <summary>能否处理该归档。</summary>
    bool CanHandle(IModpackArchive archive);

    /// <summary>解析清单。</summary>
    Result<ModpackManifest> Parse(IModpackArchive archive);
}