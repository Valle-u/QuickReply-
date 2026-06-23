using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace QuickReply.Ui;

/// <summary>Modo de diagnóstico: renderiza el botón Capturar junto a inputs y guarda un PNG,
/// para verificar centrado/altura sin depender de abrir la app. Se invoca con --render-test.</summary>
internal static class RenderTest
{
    public static void Run()
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000),
            ShowInTaskbar = false,
            Size = new Size(520, 150),
            BackColor = Theme.Bg,
            FormBorderStyle = FormBorderStyle.None,
        };

        var tb = Theme.InputBox(); tb.Font = Theme.Font; tb.Text = "Ctrl+NumPad2";
        var btn = new ChipButton { Text = "Capturar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(96, 0) };

        var tb2 = Theme.InputBox(); tb2.Font = Theme.Mono; tb2.Text = "NumPad2";
        var btn2 = new ChipButton { Text = "Apretá tecla…", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(96, 0) };

        form.Controls.AddRange(new Control[] { tb, btn, tb2, btn2 });
        form.Show();

        tb.SetBounds(20, 24, 220, tb.Height);
        btn.Location = new Point(250, 24);
        tb2.SetBounds(20, 80, 220, tb2.Height);
        btn2.Location = new Point(250, 80);
        form.Refresh();
        Application.DoEvents();

        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(Path.Combine(AppContext.BaseDirectory, "render-test.png"), ImageFormat.Png);
        form.Close();
    }
}
