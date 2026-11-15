namespace RemoteKeyboarGridBuilder;

internal sealed class KeyOption(string code)
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["BACK"] = "Backspace",
        ["RETURN"] = "Enter",
        ["ESCAPE"] = "Esc",
        ["TAB"] = "Tab",
        ["SPACE"] = "Space",
        ["SHIFT"] = "Shift",
        ["CAPITAL"] = "Caps Lock",
        ["CONTROL"] = "Ctrl",
        ["MENU"] = "Alt",
        ["LSHIFT"] = "Left Shift",
        ["RSHIFT"] = "Right Shift",
        ["LCONTROL"] = "Left Ctrl",
        ["RCONTROL"] = "Right Ctrl",
        ["LMENU"] = "Left Alt",
        ["RMENU"] = "Right Alt",
        ["LWIN"] = "Left Windows",
        ["RWIN"] = "Right Windows",
        ["APPS"] = "Context Menu",
        ["SNAPSHOT"] = "Print Screen",
        ["SCROLL"] = "Scroll Lock",
        ["PAUSE"] = "Pause / Break",
        ["INSERT"] = "Insert",
        ["DELETE"] = "Delete",
        ["HOME"] = "Home",
        ["END"] = "End",
        ["PRIOR"] = "Page Up",
        ["NEXT"] = "Page Down",
        ["LEFT"] = "Left Arrow",
        ["RIGHT"] = "Right Arrow",
        ["UP"] = "Up Arrow",
        ["DOWN"] = "Down Arrow",
        ["NUMLOCK"] = "Num Lock",
        ["DIVIDE"] = "Numpad /",
        ["MULTIPLY"] = "Numpad *",
        ["SUBTRACT"] = "Numpad -",
        ["ADD"] = "Numpad +",
        ["DECIMAL"] = "Numpad Decimal",
        // Punctuation legends for the US ANSI layout.
        ["OEM_1"] = "; / :",
        ["OEM_PLUS"] = "= / +",
        ["OEM_COMMA"] = ", / <",
        ["OEM_MINUS"] = "- / _",
        ["OEM_PERIOD"] = ". / >",
        ["OEM_2"] = "/ / ?",
        ["OEM_3"] = "` / ~",
        ["OEM_4"] = "[ / {",
        ["OEM_5"] = "\\ / |",
        ["OEM_6"] = "] / }",
        ["OEM_7"] = "' / \"",
        ["OEM_102"] = "ISO extra key"
    };

    public string Code { get; } = code;
    public string Name { get; } = GetName(code);

    public override string ToString() => Name == Code ? Code : $"{Name} ({Code})";

    private static string GetName(string code)
    {
        if (Names.TryGetValue(code, out string? name))
            return name;

        if (code.Length == 4 && code.StartsWith("VK_", StringComparison.Ordinal))
            return code[3..];

        if (code.Length == 7 && code.StartsWith("NUMPAD", StringComparison.Ordinal))
            return $"Numpad {code[6]}";

        return code;
    }
}
