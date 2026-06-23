using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickReply.Ui;

/// <summary>Paleta oscura + utilidades para que WinForms se vea prolijo en oscuro (estilo retro).</summary>
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(0x23, 0x23, 0x27);
    public static readonly Color Panel = Color.FromArgb(0x2B, 0x2B, 0x31);
    public static readonly Color TreeBg = Color.FromArgb(0x1D, 0x1D, 0x21);
    public static readonly Color Input = Color.FromArgb(0x19, 0x19, 0x1D);
    public static readonly Color Border = Color.FromArgb(0x3A, 0x3A, 0x42);
    public static readonly Color Text = Color.FromArgb(0xE8, 0xE8, 0xEA);
    public static readonly Color Muted = Color.FromArgb(0x9A, 0x9A, 0xA2);
    public static readonly Color Accent = Color.FromArgb(0x36, 0xC2, 0x6E);
    public static readonly Color Hover = Color.FromArgb(0x31, 0x31, 0x39);
    public static readonly Color Sel = Color.FromArgb(0x2E, 0x47, 0x38);
    public static readonly Color SelText = Color.White;

    public static readonly Font Font = new("Segoe UI", 9f);
    public static readonly Font FontBold = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 8.5f);

    public static void ApplyWindow(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = Text;
        f.Font = Font;
        DarkTitleBar(f);
    }

    /// <summary>Botón plano oscuro (estilo barra).</summary>
    public static Button FlatButton(string text)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Panel,
            ForeColor = Text,
            Font = Font,
            Padding = new Padding(8, 0, 8, 0),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Hover;
        b.FlatAppearance.MouseDownBackColor = Sel;
        return b;
    }

    public static Button PrimaryButton(string text)
    {
        var b = FlatButton(text);
        b.BackColor = Color.FromArgb(0xEF, 0xEF, 0xEF);
        b.ForeColor = Color.FromArgb(0x14, 0x14, 0x14);
        b.FlatAppearance.BorderColor = Color.FromArgb(0xEF, 0xEF, 0xEF);
        b.FlatAppearance.MouseOverBackColor = Color.White;
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xDD, 0xDD, 0xDD);
        b.Font = FontBold;
        return b;
    }

    public static TextBox InputBox(bool multiline = false) => new()
    {
        BackColor = Input,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = Font,
        Multiline = multiline,
    };

    public static ComboBox ComboDark()
    {
        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Input,
            ForeColor = Text,
            Font = Font,
            DrawMode = DrawMode.OwnerDrawFixed,
        };
        cb.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? Hover : Input);
            e.Graphics.FillRectangle(bg, e.Bounds);
            var txt = cb.GetItemText(cb.Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, txt, Font, e.Bounds, Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        DarkControl(cb);
        return cb;
    }

    public static Label Label(string text, bool muted = false) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = muted ? Muted : Text,
        Font = Font,
        BackColor = Color.Transparent,
    };

    // ---- Modo oscuro nativo (Win10 1809+/Win11) ----

    public static void DarkTitleBar(Form f)
    {
        if (!f.IsHandleCreated) { f.HandleCreated += (_, _) => DarkTitleBar(f); return; }
        int on = 1;
        if (DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(f.Handle, 19, ref on, sizeof(int));
    }

    /// <summary>Scrollbars/selección oscuras en TreeView/ListView/TextBox.</summary>
    public static void DarkControl(Control c)
    {
        if (!c.IsHandleCreated) { c.HandleCreated += (_, _) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null); return; }
        SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? sub, string? idList);
}

/// <summary>Renderer oscuro para ToolStrip/MenuStrip.</summary>
internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer() : base(new DarkColors()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(Theme.Panel);
        e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.ToolStrip.Size));
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(Theme.Panel);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is not ToolStripDropDown) return;
        using var p = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => Theme.Panel;
        public override Color ToolStripGradientMiddle => Theme.Panel;
        public override Color ToolStripGradientEnd => Theme.Panel;
        public override Color ToolStripContentPanelGradientBegin => Theme.Panel;
        public override Color ToolStripContentPanelGradientEnd => Theme.Panel;
        public override Color ToolStripBorder => Theme.Border;
        public override Color MenuStripGradientBegin => Theme.Panel;
        public override Color MenuStripGradientEnd => Theme.Panel;
        public override Color ImageMarginGradientBegin => Theme.Panel;
        public override Color ImageMarginGradientMiddle => Theme.Panel;
        public override Color ImageMarginGradientEnd => Theme.Panel;
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Border;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemSelectedGradientBegin => Theme.Hover;
        public override Color MenuItemSelectedGradientEnd => Theme.Hover;
        public override Color MenuItemPressedGradientBegin => Theme.Panel;
        public override Color MenuItemPressedGradientEnd => Theme.Panel;
        public override Color ButtonSelectedGradientBegin => Theme.Hover;
        public override Color ButtonSelectedGradientMiddle => Theme.Hover;
        public override Color ButtonSelectedGradientEnd => Theme.Hover;
        public override Color ButtonSelectedBorder => Theme.Border;
        public override Color ButtonPressedGradientBegin => Theme.Sel;
        public override Color ButtonPressedGradientMiddle => Theme.Sel;
        public override Color ButtonPressedGradientEnd => Theme.Sel;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
