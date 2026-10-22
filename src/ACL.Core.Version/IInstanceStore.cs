using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>
/// 实例 / 安装清单的读写。本地 JSON IO 以同步方式提供（文件小、调用点固定），
/// 需要异步的调用方可自行包 <see cref="Task.Run(System.Action)"/>。
/// </summary>
public interface IInstanceStore
{
    /// <summary>读取安装清单。</summary>
    Result<InstallManifest> LoadManifest(string path);

    /// <summary>写入安装清单（自动创建父目录）。</summary>
    Result SaveManifest(string path, InstallManifest manifest);

    /// <summary>读取实例描述符。</summary>
    Result<InstanceDescriptor> LoadDescriptor(string path);

    /// <summary>写入实例描述符（自动创建父目录）。</summary>
    Result SaveDescriptor(string path, InstanceDescriptor descriptor);
}