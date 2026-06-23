using System.Windows.Forms;

namespace QuickReply;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

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
