using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using QuickReply.Forms;

namespace QuickReply.Ui;

/// <summary>Modo de diagnóstico: renderiza ventanas/controles a un PNG para verificar el look
/// sin abrir la app interactivamente. Se invoca con --render-test.</summary>
internal static class RenderTest
{
    public static void Run()
    {
        // Render del diálogo real de variables (el área cliente, sin barra de título).
        using var vf = new VariablesForm("Recordar credenciales", new[] { "nombre", "contraseña" })
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000),
            ShowInTaskbar = false,
        };
        vf.Show();
        Application.DoEvents();

        using var bmp = new Bitmap(vf.Width, vf.Height);
        vf.DrawToBitmap(bmp, new Rectangle(0, 0, vf.Width, vf.Height));
        bmp.Save(Path.Combine(AppContext.BaseDirectory, "render-test.png"), ImageFormat.Png);
        vf.Close();
    }
}
