using System.Windows.Forms;
using QuickReply.Ui;

namespace QuickReply;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Renderer oscuro GLOBAL: cubre también los menús desplegables y el overflow de la toolbar.
        ToolStripManager.Renderer = new DarkToolStripRenderer();

        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--render-test", StringComparison.OrdinalIgnoreCase)))
        {
            RenderTest.Run();
            return;
        }

        using var mutex = new Mutex(true, "QuickReply.SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("QuickReply ya está en ejecución.", "QuickReply",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayContext());
    }
}
