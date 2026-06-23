using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace QuickReply.Ui;

/// <summary>Modo de diagnóstico: renderiza controles del editor a un PNG para verificar
/// look/altura/recortes sin abrir la app. Se invoca con --render-test.</summary>
internal static class RenderTest
{
    public static void Run()
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000),
            ShowInTaskbar = false,
            Size = new Size(560, 230),
            BackColor = Theme.Bg,
            FormBorderStyle = FormBorderStyle.None,
        };

        // Botón capturar + input
        var tb = Theme.InputBox(); tb.Font = Theme.Font; tb.Text = "Ctrl+NumPad2";
        var btn = new ChipButton { Text = "Capturar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(96, 0) };

        // Checkbox sin tildar y tildado (como en el editor)
        var chk1 = MakeCheck("Enviar con Enter automáticamente", false);
        var chk2 = MakeCheck("Enviar con Enter automáticamente (tildado)", true);

        // Botón Guardar (mismo estilo retro que los demás)
        var save = new ChipButton { Text = "Guardar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(110, 0) };

        form.Controls.AddRange(new Control[] { tb, btn, chk1, chk2, save });
        form.Show();

        tb.SetBounds(20, 20, 220, tb.Height);
        btn.Location = new Point(250, 20);
        chk1.Location = new Point(20, 70);
        chk2.Location = new Point(20, 104);
        save.Location = new Point(20, 150);
        form.Refresh();
        Application.DoEvents();

        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(Path.Combine(AppContext.BaseDirectory, "render-test.png"), ImageFormat.Png);
        form.Close();
    }

    private static CheckBox MakeCheck(string text, bool chk)
    {
        var c = new CheckBox
        {
            Text = text,
            ForeColor = Theme.Text,
            BackColor = Theme.Bg,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Checked = chk,
        };
        c.FlatAppearance.BorderColor = Theme.Border;
        c.FlatAppearance.CheckedBackColor = Theme.Accent;
        return c;
    }
}
