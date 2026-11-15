using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using InputSimulatorStandard.Native;

namespace RemoteKeyboarGridBuilder;

// Turns a LayoutModel into one self-contained HTML string
public static class HtmlBuilder
{
    // Limits agreed for the project: a phone screen is narrow, so a row holds at most four buttons
    public const int MaxCellsPerRow = 4;
    public const int MinWeight = 1;
    public const int MaxWeight = 4;

    // All key names the main app can parse (it uses Enum.TryParse<VirtualKeyCode>).
    // The editor offers exactly these, so a layout can not contain a key the app does not know.
    public static readonly IReadOnlyList<string> KnownKeys =
        Enum.GetNames<VirtualKeyCode>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // Declared after KnownKeys on purpose: static fields are initialized in textual order
    private static readonly HashSet<string> KnownKeySet = new(KnownKeys, StringComparer.OrdinalIgnoreCase);

    // The optional line for sending a whole text at once
    private const string TextInputBlock = """
                <form id="text-form" class="text-box-container">
                    <input type="text" id="text-input" placeholder="Enter text..." autocomplete="off">
                    <button type="submit" class="send-btn">Enter</button>
                </form>
        """;

    // Returns human-readable problems; an empty list means the layout can be saved
    public static List<string> Validate(LayoutModel model)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.Name))
            errors.Add("Layout name is empty.");

        if (model.Rows.Count == 0)
            errors.Add("The layout has no rows.");

        for (int r = 0; r < model.Rows.Count; r++)
        {
            var row = model.Rows[r];
            string rowName = $"Row {r + 1}";

            if (row.Cells.Count == 0)
                errors.Add($"{rowName} is empty.");

            // The editor already prevents this; the check protects against future code paths
            if (row.Cells.Count > MaxCellsPerRow)
                errors.Add($"{rowName} has {row.Cells.Count} cells, the limit is {MaxCellsPerRow}.");

            for (int c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                string at = $"{rowName}, cell {c + 1}";

                if (cell.Weight < MinWeight || cell.Weight > MaxWeight)
                    errors.Add($"{at}: width must be between {MinWeight} and {MaxWeight}.");

                if (cell.IsSpacer) continue; // nothing else to check for a spacer

                if (string.IsNullOrWhiteSpace(cell.Label))
                    errors.Add($"{at}: label is empty.");

                // A set lookup instead of Enum.TryParse: TryParse also accepts numbers ("65")
                // and comma lists ("A,B"), which we do not want in a layout file
                if (!KnownKeySet.Contains(cell.Key.Trim()))
                    errors.Add($"{at}: unknown key \"{cell.Key}\".");
            }
        }

        return errors;
    }

    public static string Build(LayoutModel model)
    {
        var errors = Validate(model);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var rows = new StringBuilder();
        foreach (var row in model.Rows)
        {
            rows.AppendLine("            <div class=\"row\">");

            foreach (var cell in row.Cells)
            {
                // "flex: N" is how the weight turns into a relative width
                string style = $"flex: {cell.Weight}";

                if (cell.IsSpacer)
                {
                    rows.AppendLine($"                <div class=\"empty\" style=\"{style}\"></div>");
                }
                else
                {
                    rows.AppendLine(
                        $"                <button class=\"key\" style=\"{style}\" data-key=\"{Encode(cell.Key.Trim())}\">{Encode(cell.Label)}</button>");
                }
            }

            rows.AppendLine("            </div>");
        }

        var values = new Dictionary<string, string>
        {
            ["TITLE"] = Encode(model.Name.Trim()),
            ["TEXT_INPUT"] = model.TextInput ? TextInputBlock : "",
            ["ROWS"] = rows.ToString().TrimEnd()
        };

        // One pass over the template. Chained string.Replace calls would also rescan text that was
        // already inserted, so a label like "%%ROWS%%" could be expanded by mistake.
        return Regex.Replace(PageTemplate.Html, "%%([A-Z_]+)%%", m => values[m.Groups[1].Value]);
    }

    // Everything that comes from the user goes into HTML only after encoding (quotes and angle brackets)
    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
