namespace ACL.Core.Domain;

/// <summary>authlib-injector 第三方认证所需的启动参数构造（纯算法）。</summary>
public static class AuthlibInjector
{
    /// <summary>约定的注入器文件名。</summary>
    public const string DefaultJarName = "authlib-injector.jar";

    /// <summary>构造 <c>-javaagent</c> 参数，如 <c>-javaagent:authlib-injector.jar=https://littleskin.cn/api/yggdrasil</c>。</summary>
    /// <param name="jarPath">注入器 jar 路径。</param>
    /// <param name="apiRoot">Yggdrasil API 根地址。</param>
    public static string BuildJavaAgentArgument(string jarPath, string apiRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiRoot);
        return $"-javaagent:{jarPath}={apiRoot.TrimEnd('/')}";
    }

    /// <summary>构造预取属性参数（可选，减少启动时的元数据请求）。</summary>
    public static string BuildPrefetchedProperty(string prefetchedJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefetchedJson);
        return "-Dauthlibinjector.yggdrasil.prefetched=" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(prefetchedJson));
    }
}