using System.Text;
using InputSimulatorStandard.Native;

namespace RemoteKeyboarGridBuilder;

public sealed class MainForm : Form
{
    // The preview imitates the phone page, so it uses the same sizes as its CSS
    private const int PhoneWidth = 340;
    private const int RowHeight = 52;

    private readonly LayoutModel _model = new();

    // What the user has selected in the preview. A row can be selected without a cell (an empty row).
    private KeyRow? _selectedRow;
    private KeyCell? _selectedCell;

    // True while we fill the property controls from code, so their change events are ignored
    private bool _updatingUi;
    private bool _capturingKey;

    private readonly TextBox _nameBox = new() { Text = "Classic", Width = 220 };
    private readonly CheckBox _textInputCheck = new() { Text = "Text input line", Checked = true, AutoSize = true };

    private readonly Panel _previewHost = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = Color.FromArgb(18, 18, 20)
    };

    // The "phone screen": rows are stacked top to bottom
    private readonly FlowLayoutPanel _phone = new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        Location = new Point(24, 24),
        BackColor = Color.Transparent
    };

    // Properties of the selected cell
    private readonly FlowLayoutPanel _propsPanel = new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        Enabled = false
    };

    private readonly TextBox _labelBox = new() { Width = 264 };

    private readonly ComboBox _keyCombo = new()
    {
        Width = 264,
        DropDownStyle = ComboBoxStyle.DropDown,
        AutoCompleteMode = AutoCompleteMode.SuggestAppend,
        AutoCompleteSource = AutoCompleteSource.ListItems
    };

    private readonly Button _captureKeyButton = new()
    {
        Text = "Press a key...",
        Width = 264,
        Height = 28
    };

    private readonly NumericUpDown _weightBox = new()
    {
        Width = 80,
        Minimum = HtmlBuilder.MinWeight,
        Maximum = HtmlBuilder.MaxWeight,
        Value = 1
    };

    private readonly CheckBox _spacerCheck = new() { Text = "Empty spacer (no button)", AutoSize = true };
    private readonly ToolTip _tip = new();

    // Stored in Button.Tag so a click knows which model objects the button stands for
    private sealed record CellRef(KeyRow Row, KeyCell? Cell);

    public MainForm()
    {
        Text = "Remote Keyboard - Layout Editor";
        ClientSize = new Size(860, 640);
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;

        _keyCombo.Items.AddRange(HtmlBuilder.KnownKeys
            .Select(code => new KeyOption(code))
            .OrderBy(option => option.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToArray());

        BuildUi();
        WireEvents();

        SeedClassicLayout();
        RebuildPreview();
        LoadProps();
    }

    // ---------------------------------------------------------------- UI construction

    private void BuildUi()
    {
        // --- top bar: name and the text line switch ---
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(12, 10, 12, 0),
            WrapContents = false
        };
        top.Controls.Add(new Label { Text = "Layout name:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_nameBox);
        _textInputCheck.Margin = new Padding(20, 4, 0, 0);
        top.Controls.Add(_textInputCheck);

        // --- right column: tools ---
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };

        right.Controls.Add(new Label
        {
            Text = "Click a button in the preview to select it.",
            AutoSize = true,
            MaximumSize = new Size(270, 0),
            Margin = new Padding(3, 0, 3, 0)
        });

        right.Controls.Add(Header("Rows"));
        right.Controls.Add(ToolButton("Add row", OnAddRow));
        right.Controls.Add(ToolButton("Delete row", OnDeleteRow));
        right.Controls.Add(ToolButton("Row up", (_, _) => MoveRow(-1)));
        right.Controls.Add(ToolButton("Row down", (_, _) => MoveRow(1)));

        right.Controls.Add(Header("Cells in the selected row"));
        right.Controls.Add(ToolButton("Add key", (_, _) => AddCellToSelectedRow(MakeKey("Esc", "ESCAPE"))));
        right.Controls.Add(ToolButton("Add spacer", (_, _) => AddCellToSelectedRow(MakeSpacer())));
        right.Controls.Add(ToolButton("Delete cell", OnDeleteCell));
        right.Controls.Add(new Label { Width = 130, Height = 30, Margin = new Padding(3) }); // keeps the grid aligned
        right.Controls.Add(ToolButton("Move left", (_, _) => MoveCell(-1)));
        right.Controls.Add(ToolButton("Move right", (_, _) => MoveCell(1)));

        right.Controls.Add(Header("Selected cell"));
        _propsPanel.Controls.Add(Caption("Label (text on the button)"));
        _propsPanel.Controls.Add(_labelBox);
        _propsPanel.Controls.Add(Caption("Key (VirtualKeyCode name, you can type to search)"));
        _propsPanel.Controls.Add(_keyCombo);
        _propsPanel.Controls.Add(_captureKeyButton);
        _propsPanel.Controls.Add(Caption("Width weight (1-4)"));
        _propsPanel.Controls.Add(_weightBox);
        _spacerCheck.Margin = new Padding(3, 8, 3, 3);
        _propsPanel.Controls.Add(_spacerCheck);
        right.Controls.Add(_propsPanel);

        var save = new Button
        {
            Text = "Save HTML...",
            Width = 270,
            Height = 40,
            Margin = new Padding(3, 24, 3, 3),
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        save.Click += OnSave;
        right.Controls.Add(save);

        // --- center: preview ---
        _previewHost.Controls.Add(_phone);

        // The Fill control is added first, edge-docked ones after it (WinForms docking order)
        Controls.Add(_previewHost);
        Controls.Add(right);
        Controls.Add(top);
    }

    private void WireEvents()
    {
        _captureKeyButton.Click += (_, _) => SetKeyCapture(!_capturingKey);
        _captureKeyButton.Leave += (_, _) => SetKeyCapture(false);
        Deactivate += (_, _) => SetKeyCapture(false);

        // The fake text line in the preview depends on this checkbox
        _textInputCheck.CheckedChanged += (_, _) => RebuildPreview();

        _labelBox.TextChanged += (_, _) =>
        {
            if (_updatingUi || _selectedCell == null) return;
            _selectedCell.Label = _labelBox.Text;
            RebuildPreview();
        };

        _keyCombo.TextChanged += (_, _) =>
        {
            if (_updatingUi || _selectedCell == null) return;
            string text = _keyCombo.Text;
            int index = _keyCombo.FindStringExact(text);
            var option = index >= 0 ? _keyCombo.Items[index] as KeyOption : null;
            SetCellKey(_selectedCell, option?.Code ?? text, option?.Name ?? text);
        };

        _weightBox.ValueChanged += (_, _) =>
        {
            if (_updatingUi || _selectedCell == null) return;
            _selectedCell.Weight = (int)_weightBox.Value;
            RebuildPreview();
        };

        _spacerCheck.CheckedChanged += (_, _) =>
        {
            if (_updatingUi || _selectedCell == null) return;
            _selectedCell.IsSpacer = _spacerCheck.Checked;
            LoadProps(); // enables or disables the label and key controls
            RebuildPreview();
        };
    }

    private void SetKeyCapture(bool active)
    {
        _capturingKey = active;
        _captureKeyButton.Text = active ? "Waiting for a key..." : "Press a key...";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_capturingKey || _selectedCell is not { IsSpacer: false } cell)
            return base.ProcessCmdKey(ref msg, keyData);

        int flags = unchecked((int)msg.LParam.ToInt64());
        // Ignore auto-repeat from a key that was already held down.
        if ((flags & (1 << 30)) != 0)
            return true;

        bool extended = (flags & (1 << 24)) != 0;
        int scanCode = (flags >> 16) & 0xff;
        // Windows reports generic modifiers here. The scan code and extended bit identify the side.
        Keys key = (keyData & Keys.KeyCode) switch
        {
            Keys.ShiftKey => scanCode == 0x36 ? Keys.RShiftKey : Keys.LShiftKey,
            Keys.ControlKey => extended ? Keys.RControlKey : Keys.LControlKey,
            Keys.Menu => extended ? Keys.RMenu : Keys.LMenu,
            var pressed => pressed
        };

        string? code = Enum.GetName((VirtualKeyCode)key);
        if (code == null)
            return true;

        var option = new KeyOption(code);
        SetKeyCapture(false);
        SetCellKey(cell, option.Code, option.Name);
        LoadProps();
        return true;
    }

    private void SetCellKey(KeyCell cell, string code, string name)
    {
        cell.Key = code;
        if (string.IsNullOrWhiteSpace(cell.Label) && _labelBox.Text != name)
            _labelBox.Text = name; // TextChanged updates the model and preview.
        else
            RebuildPreview();
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        Width = 270,
        Height = 22,
        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        Margin = new Padding(3, 14, 3, 2)
    };

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 6, 3, 0)
    };

    private static Button ToolButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, Width = 130, Height = 30, Margin = new Padding(3) };
        button.Click += onClick;
        return button;
    }

    // ---------------------------------------------------------------- model helpers

    private static KeyCell MakeKey(string label, string key, int weight = 1) =>
        new() { Label = label, Key = key, Weight = weight };

    private static KeyCell MakeSpacer() => new() { IsSpacer = true };

    private void AddRowToModel(params KeyCell[] cells)
    {
        var row = new KeyRow();
        row.Cells.AddRange(cells);
        _model.Rows.Add(row);
    }

    // The layout of the original index.html, so there is something to look at and edit right away
    private void SeedClassicLayout()
    {
        AddRowToModel(MakeKey("Alt", "MENU"), MakeKey("Tab", "TAB"), MakeKey("Esc", "ESCAPE"));
        AddRowToModel(MakeSpacer(), MakeKey("▲", "UP"), MakeSpacer());
        AddRowToModel(MakeKey("◀", "LEFT"), MakeKey("▼", "DOWN"), MakeKey("▶", "RIGHT"));
    }

    // ---------------------------------------------------------------- selection

    private void SelectCell(KeyRow? row, KeyCell? cell)
    {
        SetKeyCapture(false);
        _selectedRow = row;
        _selectedCell = cell;
        LoadProps();
        RefreshHighlight();
    }

    // Copies the selected cell into the property controls
    private void LoadProps()
    {
        _updatingUi = true;
        try
        {
            _propsPanel.Enabled = _selectedCell != null;
            _labelBox.Text = _selectedCell?.Label ?? "";
            string code = _selectedCell?.Key ?? "";
            var option = _keyCombo.Items.OfType<KeyOption>()
                .FirstOrDefault(item => string.Equals(item.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
            _keyCombo.Text = option?.ToString() ?? code;
            _weightBox.Value = _selectedCell?.Weight ?? 1;
            _spacerCheck.Checked = _selectedCell?.IsSpacer ?? false;

            // A spacer has neither a label nor a key
            _labelBox.Enabled = !_spacerCheck.Checked;
            _keyCombo.Enabled = !_spacerCheck.Checked;
            _captureKeyButton.Enabled = !_spacerCheck.Checked;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    // ---------------------------------------------------------------- preview

    private void RebuildPreview()
    {
        _phone.SuspendLayout();

        // Controls are disposed, not just removed, otherwise their handles leak on every rebuild
        var old = _phone.Controls.Cast<Control>().ToList();
        _phone.Controls.Clear();
        foreach (var control in old) control.Dispose();

        if (_textInputCheck.Checked)
        {
            _phone.Controls.Add(new Label
            {
                Text = "  Enter text...                                          [ Enter ]",
                Width = PhoneWidth,
                Height = 44,
                Margin = new Padding(0, 0, 0, 12),
                TextAlign = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(32, 32, 36),
                ForeColor = Color.Gray
            });
        }

        foreach (var row in _model.Rows)
            _phone.Controls.Add(CreateRowControl(row));

        _phone.ResumeLayout(true);
        RefreshHighlight();
    }

    private Control CreateRowControl(KeyRow row)
    {
        var table = new TableLayoutPanel
        {
            Width = PhoneWidth,
            Height = RowHeight,
            Margin = new Padding(0, 0, 0, 8),
            RowCount = 1,
            ColumnCount = Math.Max(1, row.Cells.Count)
        };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        if (row.Cells.Count == 0)
        {
            // An empty row still needs something to click on
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.Controls.Add(CreatePreviewButton(new CellRef(row, null), "(empty row)"), 0, 0);
            return table;
        }

        // Percent widths are shared proportionally, exactly like "flex: N" in the page
        foreach (var cell in row.Cells)
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, cell.Weight * 100f));

        for (int i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            string text = cell.IsSpacer ? "· empty ·" : cell.Label;
            table.Controls.Add(CreatePreviewButton(new CellRef(row, cell), text), i, 0);
        }

        return table;
    }

    private Button CreatePreviewButton(CellRef reference, string text)
    {
        var button = new Button
        {
            Text = text,
            Tag = reference,
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White
        };
        button.FlatAppearance.BorderSize = 2;
        button.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 56);
        button.Click += OnPreviewButtonClick;

        if (reference.Cell is { IsSpacer: false } cell)
            _tip.SetToolTip(button, cell.Key); // hover shows which key the button sends

        return button;
    }

    private void OnPreviewButtonClick(object? sender, EventArgs e)
    {
        if (sender is Button { Tag: CellRef reference })
            SelectCell(reference.Row, reference.Cell);
    }

    // Recolors the existing buttons without rebuilding them (safe to call from a button's own click)
    private void RefreshHighlight()
    {
        foreach (var table in _phone.Controls.OfType<TableLayoutPanel>())
        {
            foreach (var button in table.Controls.OfType<Button>())
            {
                if (button.Tag is CellRef reference)
                    button.BackColor = ColorFor(reference);
            }
        }
    }

    private Color ColorFor(CellRef reference)
    {
        bool selected = reference.Cell != null
            ? reference.Cell == _selectedCell
            : reference.Row == _selectedRow && _selectedCell == null;

        if (selected) return Color.FromArgb(0, 135, 95);
        if (reference.Cell is { IsSpacer: true }) return Color.FromArgb(60, 60, 66);
        return Color.FromArgb(32, 32, 36);
    }

    // ---------------------------------------------------------------- row and cell commands

    private void OnAddRow(object? sender, EventArgs e)
    {
        var row = new KeyRow();
        var cell = MakeKey("Esc", "ESCAPE");
        row.Cells.Add(cell);

        // Right after the selected row, otherwise at the end
        int index = _selectedRow != null ? _model.Rows.IndexOf(_selectedRow) + 1 : _model.Rows.Count;
        _model.Rows.Insert(index, row);

        SelectCell(row, cell);
        RebuildPreview();
    }

    private void OnDeleteRow(object? sender, EventArgs e)
    {
        if (_selectedRow == null) return;

        int index = _model.Rows.IndexOf(_selectedRow);
        _model.Rows.RemoveAt(index);

        // Select the row that took the deleted one's place, if any
        if (_model.Rows.Count > 0)
        {
            var next = _model.Rows[Math.Min(index, _model.Rows.Count - 1)];
            SelectCell(next, next.Cells.FirstOrDefault());
        }
        else
        {
            SelectCell(null, null);
        }

        RebuildPreview();
    }

    private void MoveRow(int direction)
    {
        if (_selectedRow == null) return;

        int from = _model.Rows.IndexOf(_selectedRow);
        int to = from + direction;
        if (to < 0 || to >= _model.Rows.Count) return;

        (_model.Rows[from], _model.Rows[to]) = (_model.Rows[to], _model.Rows[from]);
        RebuildPreview();
    }

    private void AddCellToSelectedRow(KeyCell cell)
    {
        if (_selectedRow == null)
        {
            MessageBox.Show(this, "Select a row first: click any button in the preview, or add a row.", Text);
            return;
        }

        if (_selectedRow.Cells.Count >= HtmlBuilder.MaxCellsPerRow)
        {
            MessageBox.Show(this, $"A row can hold at most {HtmlBuilder.MaxCellsPerRow} cells.", Text);
            return;
        }

        // Right after the selected cell, so the user decides where the new one goes
        int index = _selectedCell != null
            ? _selectedRow.Cells.IndexOf(_selectedCell) + 1
            : _selectedRow.Cells.Count;
        _selectedRow.Cells.Insert(index, cell);

        SelectCell(_selectedRow, cell);
        RebuildPreview();
    }

    private void OnDeleteCell(object? sender, EventArgs e)
    {
        if (_selectedRow == null || _selectedCell == null) return;

        int index = _selectedRow.Cells.IndexOf(_selectedCell);
        _selectedRow.Cells.RemoveAt(index);

        // The row stays even when it becomes empty: Validate will report it at save time
        KeyCell? next = _selectedRow.Cells.Count > 0
            ? _selectedRow.Cells[Math.Min(index, _selectedRow.Cells.Count - 1)]
            : null;

        SelectCell(_selectedRow, next);
        RebuildPreview();
    }

    private void MoveCell(int direction)
    {
        if (_selectedRow == null || _selectedCell == null) return;

        var cells = _selectedRow.Cells;
        int from = cells.IndexOf(_selectedCell);
        int to = from + direction;
        if (to < 0 || to >= cells.Count) return;

        (cells[from], cells[to]) = (cells[to], cells[from]);
        RebuildPreview();
    }

    // ---------------------------------------------------------------- saving

    private void OnSave(object? sender, EventArgs e)
    {
        _model.Name = _nameBox.Text.Trim();
        _model.TextInput = _textInputCheck.Checked;

        var errors = HtmlBuilder.Validate(_model);
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Cannot save",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save layout page",
            Filter = "HTML page (*.html)|*.html",
            DefaultExt = "html",
            AddExtension = true,
            FileName = SafeFileName(_model.Name) + ".html"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string html = HtmlBuilder.Build(_model);

            // UTF-8 without BOM: the page declares its charset itself
            File.WriteAllText(dialog.FileName, html, new UTF8Encoding(false));

            MessageBox.Show(this, "Saved:" + Environment.NewLine + dialog.FileName, Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Cannot write the file: " + ex.Message, "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // The layout name later becomes the name in the dropdown, so keep it a clean file name
    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string cleaned = string.Concat(name.Select(ch => invalid.Contains(ch) ? '_' : ch)).Trim();
        return cleaned.Length == 0 ? "layout" : cleaned;
    }
}
