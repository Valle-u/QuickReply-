using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace QuickReply.Services;

/// <summary>Genera el logo de QuickReply por código: cuadrado verde con "Q".</summary>
public static class BrandIcon
{
    public static Icon Create() => Icon.FromHandle(CreateLogo(64).GetHicon());

    private static Bitmap CreateLogo(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        float pad = size * 0.06f;
        var rect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
        float radius = size * 0.22f;

        using (var bg = new SolidBrush(ColorTranslator.FromHtml("#35C46B")))
        using (var path = Rounded(rect, radius))
            g.FillPath(bg, path);

        using var font = new Font("Segoe UI", size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fg = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("Q", font, fg, new RectangleF(0, -size * 0.03f, size, size), sf);

        return bmp;
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
