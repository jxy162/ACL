namespace ACL.Cli;

/// <summary>极简命令行解析：<c>命令 [位置参数...] [--选项 值]</c>。</summary>
public sealed class CommandLine
{
    private readonly List<string> _positional = [];
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);

    private CommandLine(string? command)
    {
        Command = command;
    }

    /// <summary>命令名（无则 <see langword="null"/>）。</summary>
    public string? Command { get; }

    /// <summary>位置参数。</summary>
    public IReadOnlyList<string> Positional => _positional;

    /// <summary>解析参数。</summary>
    public static CommandLine Parse(string[] args)
    {
        CommandLine line = new(args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null);
        for (int i = line.Command is null ? 0 : 1; i < args.Length; i++)
        {
            string token = args[i];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                string key = token[2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    line._options[key] = args[++i];
                }
                else
                {
                    line._options[key] = "true";
                }
            }
            else
            {
                line._positional.Add(token);
            }
        }

        return line;
    }

    /// <summary>取选项值。</summary>
    public string? Option(string key) => _options.TryGetValue(key, out string? value) ? value : null;

    /// <summary>取选项值（带默认）。</summary>
    public string Option(string key, string defaultValue) => Option(key) ?? defaultValue;

    /// <summary>取整数选项。</summary>
    public int OptionInt(string key, int defaultValue)
        => int.TryParse(Option(key), out int value) ? value : defaultValue;

    /// <summary>第 index 个位置参数（越界返回 null）。</summary>
    public string? Arg(int index) => index < _positional.Count ? _positional[index] : null;
}