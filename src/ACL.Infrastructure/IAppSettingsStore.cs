using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Infrastructure;

/// <summary>启动器设置的读取与写入（便携 JSON）。</summary>
public interface IAppSettingsStore
{
    /// <summary>读取设置；文件不存在时返回默认值。</summary>
    Result<AclSettings> Load();

    /// <summary>写入设置（自动创建目录）。</summary>
    Result Save(AclSettings settings);
}