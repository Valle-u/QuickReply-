using System.Drawing;
using System.Windows.Forms;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

public sealed class VariablesForm : Form
{
    private readonly Dictionary<string, TextBox> _boxes = new();
    public Dictionary<string, string> Values { get; } = new();

    public VariablesForm(string title, IEnumerable<string> variables)
    {
        var vars = variables.ToList();
        Text = "Completar datos";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyWindow(this);
        Icon = BrandIcon.Create();

        var header = Theme.Label(string.IsNullOrWhiteSpace(title) ? "Completá los datos" : $"Completá los datos de «{title}»");
        header.Font = Theme.FontBold;
        header.Location = new Point(14, 14);

        int y = 44;
        TextBox? first = null;
        foreach (var v in vars)
        {
            var lbl = Theme.Label(v.ToUpperInvariant(), muted: true);
            lbl.Location = new Point(14, y);
            var box = Theme.InputBox();
            box.SetBounds(14, y + 20, 360, 26);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            Theme.DarkControl(box);
            _boxes[v] = box;
            first ??= box;
            Controls.Add(lbl);
            Controls.Add(box);
            y += 56;
        }

        // Fijar el tamaño ANTES de agregar los botones anclados abajo (si no, el anclaje los sube encima de los inputs).
        ClientSize = new Size(388, y + 48);

        var ok = Theme.PrimaryButton("Pegar");
        ok.SetBounds(274, y + 6, 100, 30);
        ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        ok.Click += (_, _) =>
        {
            Values.Clear();
            foreach (var kv in _boxes) Values[kv.Key] = kv.Value.Text;
            DialogResult = DialogResult.OK;
        };

        var cancel = Theme.FlatButton("Cancelar");
        cancel.SetBounds(166, y + 6, 100, 30);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.DialogResult = DialogResult.Cancel;

        Controls.Add(header);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Shown += (_, _) => first?.Focus();
    }
}
