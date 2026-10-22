namespace ACL.Core.Domain;

/// <summary>实例标识。</summary>
/// <param name="Name">实例名（显示用）。</param>
/// <param name="FolderPath">实例目录绝对路径。</param>
public sealed record InstanceId(string Name, string FolderPath);

/// <summary>实例描述符：版本、组件与版本 JSON 路径。</summary>
/// <param name="Id">实例标识。</param>
/// <param name="Version">版本规格。</param>
/// <param name="Components">组件集合。</param>
/// <param name="JsonPath">版本 JSON 路径。</param>
public sealed record InstanceDescriptor(
    InstanceId Id,
    McVersionSpec Version,
    ComponentSet Components,
    string JsonPath);