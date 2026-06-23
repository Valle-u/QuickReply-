using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

/// <summary>
/// Pide los valores de las variables ({nombre}, {contraseña}, …) antes de pegar.
/// Es NO-modal e independiente: se muestra solo, al frente, sin importar qué otra ventana esté abierta.
/// Avisa el resultado por el evento <see cref="Confirmed"/> (no bloquea como ShowDialog).
/// </summary>
public sealed class VariablesForm : Form
{
    private readonly Dictionary<string, TextBox> _boxes = new();
    private readonly TextBox? _first;

    public Dictionary<string, string> Values { get; } = new();

    /// <summary>Se dispara cuando el usuario aprieta "Pegar", con los valores cargados.</summary>
    public event Action<Dictionary<string, string>>? Confirmed;

    public VariablesForm(string title, IEnumerable<string> variables)
    {
        var vars = variables.ToList();
        Text = "Completar datos";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen; // centrado en pantalla, sin depender de un "padre"
        TopMost = true;                                  // siempre al frente, como la paleta
        Theme.ApplyWindow(this);
        Icon = BrandIcon.Create();
        KeyPreview = true;

        var header = Theme.Label(string.IsNullOrWhiteSpace(title) ? "Completá los datos" : $"Completá los datos de «{title}»");
        header.Font = Theme.FontBold;
        header.Location = new Point(14, 14);

        int y = 44;
        foreach (var v in vars)
        {
            var lbl = Theme.Label(v.ToUpperInvariant(), muted: true);
            lbl.Location = new Point(14, y);
            var box = Theme.InputBox();
            box.SetBounds(14, y + 20, 360, 26);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            Theme.DarkControl(box);
            _boxes[v] = box;
            _first ??= box;
            Controls.Add(lbl);
            Controls.Add(box);
            y += 56;
        }

        // Fijar el tamaño ANTES de los botones anclados abajo (si no, el anclaje los sube encima de los inputs).
        ClientSize = new Size(388, y + 48);

        var ok = Theme.PrimaryButton("Pegar");
        ok.SetBounds(274, y + 6, 100, 30);
        ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        ok.Click += (_, _) => Confirm();

        var cancel = Theme.FlatButton("Cancelar");
        cancel.SetBounds(166, y + 6, 100, 30);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.Click += (_, _) => Close();

        Controls.Add(header);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;     // Enter = Pegar
        CancelButton = cancel; // Esc = Cancelar
    }

    private void Confirm()
    {
        Values.Clear();
        foreach (var kv in _boxes) Values[kv.Key] = kv.Value.Text;
        Confirmed?.Invoke(Values);
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ForceForeground();
        _first?.Focus();
    }

    /// <summary>Trae la ventana al frente y le da el foco aunque la app esté en segundo plano.</summary>
    private void ForceForeground()
    {
        try
        {
            var fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, out _);
            uint thisThread = GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != thisThread && AttachThreadInput(thisThread, fgThread, true);
            BringToFront();
            Activate();
            SetForegroundWindow(Handle);
            if (attached) AttachThreadInput(thisThread, fgThread, false);
        }
        catch { /* si falla el foco, igual queda TopMost y visible */ }
    }

    /// <summary>Solo para pruebas automáticas (--selftest): carga valores y confirma.</summary>
    internal void SimulateConfirm(Dictionary<string, string> values)
    {
        foreach (var kv in values)
            if (_boxes.TryGetValue(kv.Key, out var b)) b.Text = kv.Value;
        Confirm();
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hWnd);
}
