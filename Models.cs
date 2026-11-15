namespace RemoteKeyboarGridBuilder;

// One cell of a row: either a real key button or an empty spacer
public sealed class KeyCell
{
    // Text shown on the button
    public string Label { get; set; } = "";

    // Name from the VirtualKeyCode enum, exactly the string the main app parses (for example "ESCAPE")
    public string Key { get; set; } = "";

    // Relative width inside the row. It becomes the CSS "flex" value in the page.
    public int Weight { get; set; } = 1;

    // A spacer keeps its place in the row but has no button (used to center an arrow key, for example)
    public bool IsSpacer { get; set; }
}

public sealed class KeyRow
{
    public List<KeyCell> Cells { get; } = new();
}

// Everything the editor knows about one layout. The editor does not save this model anywhere:
// the only output is the finished HTML file.
public sealed class LayoutModel
{
    public string Name { get; set; } = "My layout";

    // Whether the page contains the "enter whole text" line above the buttons
    public bool TextInput { get; set; } = true;

    public List<KeyRow> Rows { get; } = new();
}
