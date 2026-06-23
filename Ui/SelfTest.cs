using System.IO;
using System.Text;
using System.Windows.Forms;
using QuickReply.Forms;

namespace QuickReply.Ui;

/// <summary>Pruebas automáticas de comportamiento. Se ejecutan con --selftest y escriben selftest.txt.
/// Verifican que el formulario de variables sea independiente (no-modal, sin dueño, visible al frente).</summary>
internal static class SelfTest
{
    public static void Run()
    {
        var sb = new StringBuilder();
        int fail = 0;
        void Check(string name, bool ok) { sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}"); if (!ok) fail++; }

        // --- Escenario: NINGUNA otra ventana abierta (ni Opciones ni Configuración) ---
        var dlg = new VariablesForm("Recordar credenciales", new[] { "nombre", "contraseña" });
        bool confirmed = false;
        Dictionary<string, string>? got = null;
        dlg.Confirmed += v => { confirmed = true; got = new Dictionary<string, string>(v); };

        dlg.Show();
        Application.DoEvents();

        Check("Se muestra sin ninguna otra ventana (Visible)", dlg.Visible);
        Check("Es independiente (Owner == null)", dlg.Owner is null);
        Check("No es modal (Modal == false)", !dlg.Modal);
        Check("Está al frente (TopMost)", dlg.TopMost);
        Check("Aparece dentro de la pantalla", Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(dlg.Bounds)));

        // --- Confirmar dispara el callback con los valores correctos ---
        dlg.SimulateConfirm(new Dictionary<string, string> { ["nombre"] = "Juan", ["contraseña"] = "abc123" });
        Application.DoEvents();

        Check("Confirmed se disparó", confirmed);
        Check("Devolvió los valores cargados",
            got is not null && got.GetValueOrDefault("nombre") == "Juan" && got.GetValueOrDefault("contraseña") == "abc123");
        Check("Se cerró tras confirmar", dlg.IsDisposed);

        sb.Insert(0, fail == 0 ? "RESULTADO: TODO OK\n\n" : $"RESULTADO: {fail} FALLO(S)\n\n");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.txt"), sb.ToString());
    }
}
