using System.Drawing;
using System.Windows.Forms;
using QuickReply.Models;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

public sealed class PaletteForm : Form
{
    private readonly List<Snippet> _all;
    private readonly TextBox _search;
    private readonly ListBox _list;

    public event Action<Snippet>? Picked;

    public PaletteForm(SnippetStore store)
    {
        _all = store.Snippets.ToList();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Font = Theme.Font;
        ClientSize = new Size(620, 420);

        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (int)(wa.Height * 0.16));

        _search = new TextBox
        {
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 13f),
            PlaceholderText = "Buscar texto…",
        };
        _search.SetBounds(12, 12, 596, 38);
        _search.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _search.TextChanged += (_, _) => ApplyFilter(_search.Text);
        Theme.DarkControl(_search);

        _list = new ListBox
        {
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 42,
            IntegralHeight = false,
        };
        _list.SetBounds(12, 60, 596, 348);
        _list.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
        _list.DrawItem += DrawRow;
        _list.DoubleClick += (_, _) => PickSelected();
        Theme.DarkControl(_list);

        Controls.Add(_search);
        Controls.Add(_list);

        KeyDown += OnKey;
        Deactivate += (_, _) => Close();
        Shown += (_, _) => { _search.Focus(); ApplyFilter(""); };
    }

    private void DrawRow(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var s = (Snippet)_list.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(sel ? Theme.Sel : Theme.Panel))
            e.Graphics.FillRectangle(bg, e.Bounds);

        var r = e.Bounds;
        TextRenderer.DrawText(e.Graphics, s.Title, Theme.Font,
            new Rectangle(r.X + 12, r.Y + 5, r.Width - 130, 18), sel ? Theme.SelText : Theme.Text, TextFormatFlags.Left);
        TextRenderer.DrawText(e.Graphics, s.Category, Theme.Font,
            new Rectangle(r.X + 12, r.Y + 22, r.Width - 130, 16), Theme.Muted, TextFormatFlags.Left);
        if (!string.IsNullOrEmpty(s.Trigger))
            TextRenderer.DrawText(e.Graphics, s.Trigger, Theme.Mono,
                new Rectangle(r.Right - 116, r.Y, 108, r.Height), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private void ApplyFilter(string query)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        IEnumerable<Snippet> res = _all;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            res = _all.Where(s =>
            {
                var hay = $"{s.Title} {s.Category} {s.Trigger} {s.Abbreviation} {s.Content}";
                return terms.All(t => hay.Contains(t, StringComparison.OrdinalIgnoreCase));
            });
        }
        foreach (var s in res.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
            _list.Items.Add(s);
        _list.EndUpdate();
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void Move(int delta)
    {
        if (_list.Items.Count == 0) return;
        int i = Math.Max(0, Math.Min(_list.Items.Count - 1, _list.SelectedIndex + delta));
        _list.SelectedIndex = i;
    }

    private void PickSelected()
    {
        if (_list.SelectedItem is Snippet s)
        {
            Hide();
            Picked?.Invoke(s);
            Close();
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down: Move(1); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.Up: Move(-1); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.Enter: PickSelected(); e.Handled = e.SuppressKeyPress = true; break;
            case Keys.Escape: Close(); e.Handled = e.SuppressKeyPress = true; break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var p = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}
