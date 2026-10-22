using System.Globalization;

namespace ACL.Core.Domain;

/// <summary>Java 版本号文本解析（如 <c>17.0.9</c>、<c>1.8.0_392</c>、<c>21</c>、<c>11.0.20+8</c>）。</summary>
public static class JavaVersionParser
{
    /// <summary>解析为可比版本号。旧式 <c>1.x</c> 会把次版本作为主版本（如 <c>1.8.0_392</c> → <c>8.0.392</c>）。</summary>
    public static Version Parse(string versionText)
    {
        if (!TryParse(versionText, out Version? version))
        {
            throw new FormatException($"无法解析 Java 版本：{versionText}");
        }

        return version;
    }

    /// <summary>尝试解析。</summary>
    public static bool TryParse(string versionText, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        string text = versionText.Trim().Trim('"');
        int plus = text.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            text = text[..plus];
        }

        string[] segments = text.Split('.');
        int? update = null;
        int lastIndex = segments.Length - 1;
        string[] lastParts = segments[lastIndex].Split('_');
        if (lastParts.Length >= 2 && int.TryParse(lastParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedUpdate))
        {
            update = parsedUpdate;
            segments[lastIndex] = lastParts[0];
        }

        int[] numbers = new int[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            if (!int.TryParse(segments[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        if (numbers.Length == 0)
        {
            return false;
        }

        int major = numbers[0];
        int minor = numbers.Length > 1 ? numbers[1] : 0;
        int build = numbers.Length > 2 ? numbers[2] : 0;
        if (major == 1 && numbers.Length > 1)
        {
            major = numbers[1];
            minor = numbers.Length > 2 ? numbers[2] : 0;
            build = update ?? 0;
        }
        else if (update is not null)
        {
            build = update.Value;
        }

        version = new Version(major, minor, build);
        return true;
    }
}