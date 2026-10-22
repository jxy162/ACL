using System.Text.Json;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>把 <see cref="VersionJson"/> 写回官方格式的 JSON 文本（用于自建实例 / 加载器补丁）。</summary>
public static class VersionJsonWriter
{
    /// <summary>序列化版本 JSON。</summary>
    public static string Write(VersionJson version)
    {
        ArgumentNullException.ThrowIfNull(version);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", version.Id);
            if (version.InheritsFrom is { Length: > 0 } parent)
            {
                writer.WriteString("inheritsFrom", parent);
            }

            WriteString(writer, "mainClass", version.MainClass);
            WriteString(writer, "assets", version.Assets);
            WriteString(writer, "type", version.Type);
            WriteString(writer, "minecraftArguments", version.LegacyMinecraftArguments);

            if (version.AssetIndex is { } index)
            {
                writer.WritePropertyName("assetIndex");
                writer.WriteStartObject();
                writer.WriteString("id", index.Id);
                writer.WriteString("url", index.Url.ToString());
                WriteString(writer, "sha1", index.Sha1);
                WriteNumber(writer, "size", index.Size);
                WriteNumber(writer, "totalSize", index.TotalSize);
                writer.WriteEndObject();
            }

            if (version.JavaVersion is { } java)
            {
                writer.WritePropertyName("javaVersion");
                writer.WriteStartObject();
                writer.WriteString("component", java.Component);
                writer.WriteNumber("majorVersion", java.MajorVersion);
                writer.WriteEndObject();
            }

            if (version.Downloads is { } downloads)
            {
                writer.WritePropertyName("downloads");
                writer.WriteStartObject();
                WriteArtifact(writer, "client", downloads.Client);
                WriteArtifact(writer, "server", downloads.Server);
                writer.WriteEndObject();
            }

            writer.WritePropertyName("libraries");
            writer.WriteStartArray();
            foreach (LibraryRef library in version.Libraries)
            {
                WriteLibrary(writer, library);
            }

            writer.WriteEndArray();

            if (version.GameArguments.Count > 0 || version.JvmArguments.Count > 0)
            {
                writer.WritePropertyName("arguments");
                writer.WriteStartObject();
                WriteArguments(writer, "game", version.GameArguments);
                WriteArguments(writer, "jvm", version.JvmArguments);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteLibrary(Utf8JsonWriter writer, LibraryRef library)
    {
        writer.WriteStartObject();
        writer.WriteString("name", library.Name);

        if (library.Downloads is { } downloads)
        {
            writer.WritePropertyName("downloads");
            writer.WriteStartObject();
            WriteArtifact(writer, "artifact", downloads.Artifact);
            if (downloads.Classifiers is { Count: > 0 } classifiers)
            {
                writer.WritePropertyName("classifiers");
                writer.WriteStartObject();
                foreach (KeyValuePair<string, DownloadArtifact> pair in classifiers)
                {
                    WriteArtifact(writer, pair.Key, pair.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        WriteString(writer, "url", library.Url);

        if (library.Natives is { Count: > 0 } natives)
        {
            writer.WritePropertyName("natives");
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> pair in natives)
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
        }

        if (library.Rules is { Count: > 0 } rules)
        {
            writer.WritePropertyName("rules");
            writer.WriteStartArray();
            foreach (RuleEntry rule in rules)
            {
                writer.WriteStartObject();
                writer.WriteString("action", rule.Action);
                if (rule.OsName is not null || rule.OsVersion is not null || rule.OsArch is not null)
                {
                    writer.WritePropertyName("os");
                    writer.WriteStartObject();
                    WriteString(writer, "name", rule.OsName);
                    WriteString(writer, "version", rule.OsVersion);
                    WriteString(writer, "arch", rule.OsArch);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteArguments(Utf8JsonWriter writer, string name, IReadOnlyList<ArgumentEntry> entries)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (ArgumentEntry entry in entries)
        {
            if (entry.Rules is { Count: 0 } or null)
            {
                if (entry.Values.Count == 1)
                {
                    writer.WriteStringValue(entry.Values[0]);
                }
                else
                {
                    writer.WriteStartArray();
                    foreach (string value in entry.Values)
                    {
                        writer.WriteStringValue(value);
                    }

                    writer.WriteEndArray();
                }

                continue;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("rules");
            writer.WriteStartArray();
            foreach (RuleEntry rule in entry.Rules)
            {
                writer.WriteStartObject();
                writer.WriteString("action", rule.Action);
                if (rule.OsName is not null)
                {
                    writer.WritePropertyName("os");
                    writer.WriteStartObject();
                    WriteString(writer, "name", rule.OsName);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("value");
            if (entry.Values.Count == 1)
            {
                writer.WriteStringValue(entry.Values[0]);
            }
            else
            {
                writer.WriteStartArray();
                foreach (string value in entry.Values)
                {
                    writer.WriteStringValue(value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteArtifact(Utf8JsonWriter writer, string name, DownloadArtifact? artifact)
    {
        if (artifact is null)
        {
            return;
        }

        writer.WritePropertyName(name);
        writer.WriteStartObject();
        WriteString(writer, "url", artifact.Url?.ToString());
        WriteString(writer, "sha1", artifact.Sha1);
        WriteNumber(writer, "size", artifact.Size);
        WriteString(writer, "path", artifact.Path);
        writer.WriteEndObject();
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is not null)
        {
            writer.WriteNumber(name, value.Value);
        }
    }
}