using System.Drawing;
using System.Windows.Forms;
using QuickReply.Models;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

public sealed class MainForm : Form
{
    private readonly SnippetStore _store;
    private readonly TrayContext _tray;

    private readonly TextBox _searchBox;
    private readonly TreeView _tree;
    private readonly TextBox _titleBox;
    private readonly ComboBox _catCombo;
    private readonly CheckBox _autoSend;
    private readonly TextBox _trigBox;
    private readonly Button _captureBtn;
    private readonly TextBox _abbrBox;
    private readonly TextBox _contentBox;
    private readonly Label _conflict;
    private readonly Button _saveBtn;
    private readonly Panel _editor;
    private readonly Label _empty;
    private readonly ToolStripStatusLabel _status;

    private Snippet? _current;
    private bool _loading;
    private bool _capturing;

    public MainForm(SnippetStore store, TrayContext tray)
    {
        _store = store;
        _tray = tray;

        Text = "QuickReply — Configuración";
        ClientSize = new Size(940, 600);
        MinimumSize = new Size(660, 440);
        StartPosition = FormStartPosition.CenterScreen;
        Theme.ApplyWindow(this);
        Icon = BrandIcon.Create();
        KeyPreview = true;

        // ---- Toolbar estilo menú clásico (retro) + menú "Más" desplegable ----
        var tb = new ToolStrip
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(4, 2, 4, 2),
        };
        ToolStripButton Btn(string text, EventHandler onClick)
        {
            var b = new ToolStripButton(text) { ForeColor = Theme.Text, DisplayStyle = ToolStripItemDisplayStyle.Text, AutoToolTip = false };
            b.Click += onClick;
            return b;
        }
        tb.Items.Add(Btn("Nuevo texto", NewText));
        tb.Items.Add(Btn("Nueva categoría", NewCategory));
        tb.Items.Add(Btn("Eliminar categoría", DeleteCategory));
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(Btn("Duplicar", Duplicate));
        tb.Items.Add(Btn("Eliminar", DeleteSnippet));
        tb.Items.Add(new ToolStripSeparator());

        var mas = new ToolStripDropDownButton("Más") { ForeColor = Theme.Text, DisplayStyle = ToolStripItemDisplayStyle.Text, AutoToolTip = false };
        mas.DropDown.Renderer = new DarkToolStripRenderer();
        mas.DropDown.BackColor = Theme.Panel;
        mas.DropDown.ForeColor = Theme.Text;
        void MasItem(string text, EventHandler onClick)
        {
            var it = new ToolStripMenuItem(text) { ForeColor = Theme.Text, BackColor = Theme.Panel };
            it.Click += onClick;
            mas.DropDownItems.Add(it);
        }
        MasItem("Importar", Import);
        MasItem("Exportar", Export);
        MasItem("Carpeta", OpenFolder);
        MasItem("Opciones", (_, _) => _tray.ShowOptions());
        tb.Items.Add(mas);

        // por si igual hay overflow al achicar, que su menú también sea oscuro
        tb.OverflowButton.DropDown.Renderer = new DarkToolStripRenderer();
        tb.OverflowButton.DropDown.BackColor = Theme.Panel;

        // ---- Status ----
        var status = new StatusStrip { Renderer = new DarkToolStripRenderer(), BackColor = Theme.Panel };
        _status = new ToolStripStatusLabel { ForeColor = Theme.Muted, Text = "" };
        status.Items.Add(_status);

        // ---- Split: árbol | editor ----
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Border,
        };
        Load += (_, _) =>
        {
            try { if (split.Width > 360) split.SplitterDistance = 300; } catch { }
            int h = _trigBox.Height;            // alto real del input (correcto en cualquier DPI)
            _captureBtn.MinimumSize = new Size(96, h);
            _captureBtn.MaximumSize = new Size(0, h);
        };
        split.Panel1.BackColor = Theme.TreeBg;
        split.Panel2.BackColor = Theme.Bg;

        _searchBox = new TextBox
        {
            Dock = DockStyle.Top,
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Buscar…",
            Margin = new Padding(0),
        };
        _searchBox.TextChanged += (_, _) => BuildTree(_searchBox.Text);
        Theme.DarkControl(_searchBox);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.TreeBg,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            Indent = 18,
            ItemHeight = 22,
        };
        Theme.DarkControl(_tree);
        _tree.BeforeSelect += Tree_BeforeSelect;
        _tree.AfterSelect += Tree_AfterSelect;

        split.Panel1.Controls.Add(_tree);
        split.Panel1.Controls.Add(_searchBox);

        // ---- Editor ----
        _editor = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(14) };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Theme.Bg,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _titleBox = Field();
        _catCombo = Theme.ComboDark();
        _catCombo.Dock = DockStyle.Fill;
        _catCombo.Margin = new Padding(0, 0, 0, 8);
        _autoSend = new CheckBox
        {
            Text = "Enviar con Enter automáticamente",
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };

        var trigRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        trigRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        trigRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _trigBox = Field();
        _trigBox.ReadOnly = true;
        _trigBox.Font = Theme.Mono;
        _trigBox.Margin = new Padding(0, 0, 8, 0);
        _captureBtn = Theme.FlatButton("Capturar");
        _captureBtn.AutoSize = true;
        _captureBtn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _captureBtn.Padding = new Padding(16, 0, 16, 0);
        _captureBtn.MinimumSize = new Size(96, 0);
        _captureBtn.Margin = new Padding(0);
        _captureBtn.Click += (_, _) => ToggleCapture();
        trigRow.Controls.Add(_trigBox, 0, 0);
        trigRow.Controls.Add(_captureBtn, 1, 0);

        _abbrBox = Field();
        _abbrBox.Font = Theme.Mono;
        _abbrBox.TextChanged += (_, _) => { if (!_loading) UpdateConflict(); };

        _contentBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f),
            AcceptsReturn = true,
            Margin = new Padding(0, 0, 0, 6),
        };
        Theme.DarkControl(_contentBox);

        _conflict = Theme.Label("", muted: true);
        _conflict.ForeColor = Color.FromArgb(0xE0, 0xA9, 0x4B);
        _conflict.Dock = DockStyle.Fill;

        var bottom = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        _saveBtn = Theme.PrimaryButton("Guardar cambios");
        _saveBtn.Dock = DockStyle.Right;
        _saveBtn.Height = 30;
        _saveBtn.Click += (_, _) => Save();
        bottom.Controls.Add(_conflict, 0, 0);
        bottom.Controls.Add(_saveBtn, 1, 0);

        AddRow(layout, Theme.Label("TÍTULO", muted: true));
        AddRow(layout, _titleBox);
        AddRow(layout, Theme.Label("CATEGORÍA", muted: true));
        AddRow(layout, _catCombo);
        AddRow(layout, _autoSend);
        AddRow(layout, Theme.Label("DISPARADOR (TECLA O COMBINACIÓN)", muted: true));
        AddRow(layout, trigRow);
        AddRow(layout, Theme.Label("ABREVIATURA (OPCIONAL)", muted: true));
        AddRow(layout, _abbrBox);
        AddRow(layout, Theme.Label("CONTENIDO", muted: true));
        AddRow(layout, _contentBox, fill: true);
        AddRow(layout, Theme.Label("Dinámico: {fecha} {hora} {cursor} · {enter} divide en mensajes · variables {nombre}", muted: true));
        AddRow(layout, bottom);

        _editor.Controls.Add(layout);

        _empty = new Label
        {
            Text = "Ningún texto seleccionado.\nElegí uno del árbol o creá uno nuevo.",
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
        };

        split.Panel2.Controls.Add(_editor);
        split.Panel2.Controls.Add(_empty);

        Controls.Add(split);
        Controls.Add(tb);
        Controls.Add(status);

        KeyDown += OnKeyDownCapture;

        RefreshCategories();
        BuildTree("");
        ShowEditor(false);
        _status.Text = $"{_store.Snippets.Count} textos · {_store.ValidCount} con disparador válido";
    }

    // ---- Helpers de construcción ----

    private static TextBox Field()
    {
        var t = Theme.InputBox();
        t.Dock = DockStyle.Fill;
        t.Margin = new Padding(0, 0, 0, 8);
        Theme.DarkControl(t);
        return t;
    }

    private static void AddRow(TableLayoutPanel tlp, Control c, bool fill = false)
    {
        int row = tlp.RowCount;
        tlp.RowCount = row + 1;
        tlp.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
        if (c is Label && !fill) c.Margin = new Padding(0, 6, 0, 2);
        tlp.Controls.Add(c, 0, row);
    }

    private void ShowEditor(bool show)
    {
        _editor.Visible = show;
        _empty.Visible = !show;
        if (show) _editor.BringToFront(); else _empty.BringToFront();
    }

    // ---- Árbol ----

    private void BuildTree(string filter)
    {
        _loading = true;
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        bool HasFilter = !string.IsNullOrWhiteSpace(filter);
        var f = filter.Trim();
        bool Match(Snippet s) => !HasFilter ||
            ($"{s.Title} {s.Category} {s.Trigger} {s.Abbreviation} {s.Content}".Contains(f, StringComparison.OrdinalIgnoreCase));

        foreach (var cat in _store.Snippets.Select(s => s.Category).Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            var items = _store.Snippets.Where(s => string.Equals(s.Category, cat, StringComparison.OrdinalIgnoreCase) && Match(s))
                                       .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase).ToList();
            if (items.Count == 0) continue;

            var catNode = new TreeNode(cat) { ForeColor = Theme.Muted };
            foreach (var s in items)
            {
                var label = string.IsNullOrEmpty(s.Trigger) ? s.Title : $"{s.Title}   [{s.Trigger}]";
                catNode.Nodes.Add(new TreeNode(label) { Tag = s, ForeColor = Theme.Text });
            }
            _tree.Nodes.Add(catNode);
        }
        _tree.ExpandAll();
        _tree.EndUpdate();
        _loading = false;

        // reseleccionar el actual si sigue visible
        if (_current is not null)
            SelectNodeFor(_current);
    }

    private void SelectNodeFor(Snippet s)
    {
        foreach (TreeNode cat in _tree.Nodes)
            foreach (TreeNode n in cat.Nodes)
                if (ReferenceEquals(n.Tag, s)) { _loading = true; _tree.SelectedNode = n; _loading = false; return; }
    }

    private void Tree_BeforeSelect(object? sender, TreeViewCancelEventArgs e)
    {
        if (_loading) return;
        var target = e.Node?.Tag as Snippet;
        if (ReferenceEquals(target, _current)) return;
        if (!ConfirmLeave()) e.Cancel = true;
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (_loading) return;
        if (e.Node?.Tag is Snippet s) LoadSnippet(s);
    }

    // ---- Editor / borrador ----

    private void LoadSnippet(Snippet s)
    {
        _current = s;
        _loading = true;
        _titleBox.Text = s.Title;
        if (!_catCombo.Items.Contains(s.Category)) _catCombo.Items.Add(s.Category);
        _catCombo.SelectedItem = s.Category;
        _trigBox.Text = s.Trigger;
        _abbrBox.Text = s.Abbreviation;
        _autoSend.Checked = s.AutoSend;
        _contentBox.Text = s.Content;
        _loading = false;
        ShowEditor(true);
        UpdateConflict();
    }

    private bool IsDirty()
    {
        if (_current is null) return false;
        return _titleBox.Text != _current.Title
               || (_catCombo.SelectedItem as string ?? _current.Category) != _current.Category
               || _trigBox.Text != _current.Trigger
               || _abbrBox.Text != _current.Abbreviation
               || _autoSend.Checked != _current.AutoSend
               || _contentBox.Text != _current.Content;
    }

    private bool ConfirmLeave()
    {
        if (!IsDirty()) return true;
        var r = MessageBox.Show($"Tenés cambios sin guardar en «{_current!.Title}».\n\n¿Descartarlos?",
            "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return r == DialogResult.Yes;
    }

    private void Save()
    {
        if (_current is null) return;
        _current.Title = _titleBox.Text.Trim();
        _current.Category = (_catCombo.SelectedItem as string) ?? "General";
        _current.Trigger = _trigBox.Text;
        _current.Abbreviation = _abbrBox.Text.Trim();
        _current.AutoSend = _autoSend.Checked;
        _current.Content = _contentBox.Text;
        try { _store.Save(); } catch (Exception ex) { MessageBox.Show(ex.Message); return; }
        BuildTree(_searchBox.Text);
        UpdateConflict();
        _status.Text = "Cambios guardados";
    }

    private void UpdateConflict()
    {
        _conflict.Text = "";
        if (_current is null) return;
        var msgs = new List<string>();
        var hk = Hotkey.Parse(_trigBox.Text);
        if (hk is { } h)
        {
            var other = _store.FindConflict(h, _current);
            if (other is not null) msgs.Add($"La tecla ya la usa «{other.Title}».");
        }
        else if (!string.IsNullOrWhiteSpace(_trigBox.Text)) msgs.Add("Disparador no reconocido.");

        if (!string.IsNullOrWhiteSpace(_abbrBox.Text))
        {
            var other = _store.FindAbbreviationConflict(_abbrBox.Text.Trim(), _current);
            if (other is not null) msgs.Add($"La abreviatura ya la usa «{other.Title}».");
        }
        _conflict.Text = string.Join("    ", msgs);
    }

    private void RefreshCategories()
    {
        var sel = _catCombo.SelectedItem as string;
        _catCombo.Items.Clear();
        _catCombo.Items.AddRange(_store.Categories.Cast<object>().ToArray());
        if (sel is not null && _catCombo.Items.Contains(sel)) _catCombo.SelectedItem = sel;
    }

    // ---- Captura de tecla ----

    private void ToggleCapture()
    {
        if (_current is null) return;
        if (_capturing) { StopCapture(); return; }
        _capturing = true;
        _tray.BeginKeyCapture();
        _captureBtn.Text = "Apretá tecla…";
    }

    private void StopCapture()
    {
        _capturing = false;
        _tray.EndKeyCapture();
        _captureBtn.Text = "Capturar";
    }

    private void OnKeyDownCapture(object? sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        var kc = e.KeyCode;
        if (kc is Keys.Escape) { StopCapture(); return; }
        if (kc is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin) return;

        var mods = ModMask.None;
        if (e.Control) mods |= ModMask.Control;
        if (e.Alt) mods |= ModMask.Alt;
        if (e.Shift) mods |= ModMask.Shift;

        _trigBox.Text = new Hotkey(mods, (int)kc).ToString();
        StopCapture();
        UpdateConflict();
        _status.Text = $"Disparador asignado: {_trigBox.Text} (acordate de Guardar)";
    }

    // ---- Toolbar ----

    private void NewText(object? sender, EventArgs e)
    {
        if (!ConfirmLeave()) return;
        var cat = _current?.Category ?? (_store.Categories.FirstOrDefault() ?? "General");
        _current = null;
        var s = new Snippet { Title = "Nuevo texto", Category = cat, Content = "" };
        _store.Snippets.Add(s);
        try { _store.Save(); } catch { }
        BuildTree("");
        LoadSnippet(s);
        SelectNodeFor(s);
        _titleBox.Focus();
        _titleBox.SelectAll();
        _status.Text = "Texto nuevo creado";
    }

    private void NewCategory(object? sender, EventArgs e)
    {
        using var dlg = new InputForm("Nueva categoría", "Nombre de la categoría:") { };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var existed = _store.Categories.Any(c => string.Equals(c, dlg.Value, StringComparison.OrdinalIgnoreCase));
        _store.AddCategory(dlg.Value);
        RefreshCategories();
        _status.Text = existed ? $"La categoría «{dlg.Value}» ya existía" : $"Categoría agregada: {dlg.Value}";
    }

    private void DeleteCategory(object? sender, EventArgs e)
    {
        using var dlg = new DeleteCategoryForm(_store);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Selected is null) return;
        int moved = _store.DeleteCategory(dlg.Selected);
        RefreshCategories();
        BuildTree(_searchBox.Text);
        _status.Text = moved > 0 ? $"Categoría «{dlg.Selected}» eliminada · {moved} texto(s) a General" : $"Categoría «{dlg.Selected}» eliminada";
    }

    private void Duplicate(object? sender, EventArgs e)
    {
        if (_current is null) return;
        if (!ConfirmLeave()) return;
        var src = _current;
        _current = null;
        var copy = new Snippet { Title = src.Title + " (copia)", Category = src.Category, Trigger = "", Abbreviation = "", AutoSend = src.AutoSend, Content = src.Content };
        _store.Snippets.Add(copy);
        try { _store.Save(); } catch { }
        BuildTree("");
        LoadSnippet(copy);
        SelectNodeFor(copy);
        _status.Text = "Texto duplicado";
    }

    private void DeleteSnippet(object? sender, EventArgs e)
    {
        if (_current is null) return;
        var r = MessageBox.Show($"¿Eliminar «{_current.Title}»?", "QuickReply", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        _store.Snippets.Remove(_current);
        _current = null;
        try { _store.Save(); } catch { }
        BuildTree(_searchBox.Text);
        ShowEditor(false);
        _status.Text = "Texto eliminado";
    }

    private void Import(object? sender, EventArgs e)
    {
        if (!ConfirmLeave()) return;
        using var dlg = new OpenFileDialog { Filter = "QuickReply (*.json)|*.json|Todos|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show("Esto reemplaza TODOS tus textos por los del archivo. ¿Seguir?", "Importar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { _store.ImportFrom(dlg.FileName); _current = null; RefreshCategories(); BuildTree(""); ShowEditor(false); _status.Text = "Importado"; }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void Export(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Filter = "QuickReply (*.json)|*.json", FileName = "quickreply-textos.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { _store.ExportTo(dlg.FileName); _status.Text = "Exportado"; }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void OpenFolder(object? sender, EventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(_store.Path)!;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_capturing) _tray.EndKeyCapture();
        if (IsDirty())
        {
            var r = MessageBox.Show("Tenés cambios sin guardar.\n\n¿Guardar antes de cerrar?",
                "QuickReply", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            if (r == DialogResult.Yes) Save();
        }
        base.OnFormClosing(e);
    }
}
