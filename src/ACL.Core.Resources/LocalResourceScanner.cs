using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Resources;

/// <summary><see cref="ILocalResourceScanner"/> 的默认实现（TOML 部分为轻量尽力解析）。</summary>
public sealed class LocalResourceScanner : ILocalResourceScanner
{
    private readonly IFileSystem _fileSystem;

    /// <summary>构造扫描器。</summary>
    public LocalResourceScanner(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <inheritdoc />
    public Result<LocalResourceInfo> Scan(string jarPath)
    {
        if (string.IsNullOrWhiteSpace(jarPath) || !_fileSystem.FileExists(jarPath))
        {
            return Result<LocalResourceInfo>.Failure(ErrorCode.NotFound, jarPath ?? string.Empty);
        }

        try
        {
            using Stream stream = _fileSystem.OpenRead(jarPath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read);

            if (ReadEntry(archive, "fabric.mod.json") is { } fabric)
            {
                return Result<LocalResourceInfo>.Success(ParseFabric(jarPath, fabric));
            }

            if (ReadEntry(archive, "quilt.mod.json") is { } quilt)
            {
                return Result<LocalResourceInfo>.Success(ParseQuilt(jarPath, quilt));
            }

            if (ReadEntry(archive, "META-INF/neoforge.mods.toml") is { } neoForge)
            {
                return Result<LocalResourceInfo>.Success(ParseToml(jarPath, neoForge, ModMetadataFormat.NeoForgeToml));
            }

            if (ReadEntry(archive, "META-INF/mods.toml") is { } forge)
            {
                return Result<LocalResourceInfo>.Success(ParseToml(jarPath, forge, ModMetadataFormat.ForgeToml));
            }

            if (ReadEntry(archive, "mcmod.info") is { } legacy)
            {
                return Result<LocalResourceInfo>.Success(ParseLegacy(jarPath, legacy));
            }

            return Result<LocalResourceInfo>.Success(
                new LocalResourceInfo(jarPath, ModMetadataFormat.Unknown, null, null, null, [], []));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            return Result<LocalResourceInfo>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<LocalResourceInfo>> ScanDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !_fileSystem.DirectoryExists(directory))
        {
            return Result<IReadOnlyList<LocalResourceInfo>>.Failure(ErrorCode.NotFound, directory ?? string.Empty);
        }

        List<LocalResourceInfo> results = [];
        foreach (string path in _fileSystem.EnumerateFiles(directory))
        {
            if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Result<LocalResourceInfo> scanned = Scan(path);
            if (scanned.IsSuccess)
            {
                results.Add(scanned.Value!);
            }
        }

        return Result<IReadOnlyList<LocalResourceInfo>>.Success(results);
    }

    private static string? ReadEntry(ZipArchive archive, string name)
    {
        ZipArchiveEntry? entry = archive.GetEntry(name);
        if (entry is null)
        {
            return null;
        }

        using Stream stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static LocalResourceInfo ParseFabric(string jarPath, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        return new LocalResourceInfo(
            jarPath,
            ModMetadataFormat.Fabric,
            GetString(root, "id"),
            GetString(root, "name") ?? GetString(root, "id"),
            GetString(root, "version"),
            ReadFabricAuthors(root),
            ReadObjectKeys(root, "depends"));
    }

    private static LocalResourceInfo ParseQuilt(string jarPath, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement loader = root.TryGetProperty("quilt_loader", out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? value
            : root;

        string? name = loader.TryGetProperty("metadata", out JsonElement metadata) && metadata.ValueKind == JsonValueKind.Object
            ? GetString(metadata, "name")
            : null;

        List<string> authors = [];
        if (loader.TryGetProperty("metadata", out JsonElement meta) && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("contributors", out JsonElement contributors) && contributors.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in contributors.EnumerateObject())
            {
                authors.Add(property.Name);
            }
        }

        return new LocalResourceInfo(
            jarPath,
            ModMetadataFormat.Quilt,
            GetString(loader, "id"),
            name ?? GetString(loader, "id"),
            GetString(loader, "version"),
            authors,
            []);
    }

    private static LocalResourceInfo ParseToml(string jarPath, string toml, ModMetadataFormat format)
    {
        List<string> modIds = FindTomlEntries(toml, "modId");
        string? modId = modIds.Count > 0 ? modIds[0] : null;

        List<string> dependencies = modIds.Count > 1 ? [.. modIds.Skip(1)] : [];

        return new LocalResourceInfo(
            jarPath,
            format,
            modId,
            FindTomlLineValue(toml, "displayName") ?? modId,
            FindTomlLineValue(toml, "version"),
            SplitAuthors(FindTomlLineValue(toml, "authors")),
            dependencies);
    }

    private static LocalResourceInfo ParseLegacy(string jarPath, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement mod = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? root[0]
            : root.TryGetProperty("modList", out JsonElement list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0
                ? list[0]
                : root;

        return new LocalResourceInfo(
            jarPath,
            ModMetadataFormat.LegacyMcModInfo,
            GetString(mod, "modid"),
            GetString(mod, "name"),
            GetString(mod, "version"),
            ReadStringList(mod, "authorList"),
            []);
    }

    private static List<string> ReadFabricAuthors(JsonElement root)
    {
        List<string> authors = [];
        if (!root.TryGetProperty("authors", out JsonElement array) || array.ValueKind != JsonValueKind.Array)
        {
            return authors;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                authors.Add(item.GetString()!);
            }
            else if (item.ValueKind == JsonValueKind.Object && GetString(item, "name") is { } name)
            {
                authors.Add(name);
            }
        }

        return authors;
    }

    private static List<string> ReadObjectKeys(JsonElement root, string property)
    {
        List<string> keys = [];
        if (root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty item in value.EnumerateObject())
            {
                keys.Add(item.Name);
            }
        }

        return keys;
    }

    private static List<string> ReadStringList(JsonElement element, string property)
    {
        List<string> values = [];
        if (element.TryGetProperty(property, out JsonElement array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    values.Add(item.GetString()!);
                }
            }
        }

        return values;
    }

    /// <summary>查找 TOML 中某键的全部取值（首个通常为模组自身，其余多为依赖）。</summary>
    private static List<string> FindTomlEntries(string toml, string key)
    {
        List<string> values = [];
        int search = 0;
        while (search < toml.Length)
        {
            int index = toml.IndexOf(key, search, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            bool boundaryOk = index == 0 || !char.IsLetterOrDigit(toml[index - 1]);
            int equals = toml.IndexOf('=', index + key.Length);
            if (boundaryOk && equals >= 0)
            {
                int firstQuote = toml.IndexOf('"', equals);
                int secondQuote = firstQuote >= 0 ? toml.IndexOf('"', firstQuote + 1) : -1;
                if (firstQuote >= 0 && secondQuote > firstQuote)
                {
                    values.Add(toml[(firstQuote + 1)..secondQuote]);
                    search = secondQuote + 1;
                    continue;
                }
            }

            search = index + key.Length;
        }

        return values;
    }

    private static string? FindTomlLineValue(string toml, string key)
    {
        foreach (string raw in toml.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            int equals = line.IndexOf('=');
            if (equals < 0)
            {
                continue;
            }

            string value = line[(equals + 1)..].Trim();
            if (value.StartsWith('"'))
            {
                int end = value.IndexOf('"', 1);
                return end > 0 ? value[1..end] : value;
            }

            if (value.StartsWith('['))
            {
                return value;
            }
        }

        return null;
    }

    private static List<string> SplitAuthors(string? raw)
        => string.IsNullOrEmpty(raw)
            ? []
            : [.. raw.Trim('[', ']', ' ')
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(author => author.Trim('"'))];

    private static string? GetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}