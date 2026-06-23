using System.Drawing;
using System.Windows.Forms;

namespace QuickReply.Ui;

/// <summary>Botón plano oscuro dibujado a mano: el texto se centra sobre todo el rectángulo
/// con TextRenderer, así que nunca se recorta y queda perfectamente centrado en cualquier alto.</summary>
internal sealed class ChipButton : Button
{
    private bool _hover, _down;

    public ChipButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        ForeColor = Theme.Text;
        BackColor = Theme.Panel;
        Font = Theme.Font;
        Padding = Padding.Empty;
    }

    // Alto = alto del texto + 8 (≈ alto de un input estándar). Ancho = texto + aire.
    public override Size GetPreferredSize(Size proposedSize)
    {
        var s = TextRenderer.MeasureText(Text, Font);
        return new Size(Math.Max(s.Width + 28, MinimumSize.Width), s.Height + 8);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color fill = _down ? Theme.Sel : (_hover ? Theme.Hover : Theme.Panel);
        using (var b = new SolidBrush(fill)) g.FillRectangle(b, ClientRectangle);
        using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
