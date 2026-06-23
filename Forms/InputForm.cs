using System.Drawing;
using System.Windows.Forms;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

public sealed class InputForm : Form
{
    private readonly TextBox _input;
    public string Value => _input.Text.Trim();

    public InputForm(string title, string prompt, string initial = "")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 132);
        Theme.ApplyWindow(this);
        Icon = BrandIcon.Create();

        var lbl = Theme.Label(prompt);
        lbl.Location = new Point(14, 16);

        _input = Theme.InputBox();
        _input.SetBounds(14, 44, 352, 26);
        _input.Text = initial;
        _input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        Theme.DarkControl(_input);

        var ok = Theme.PrimaryButton("Aceptar");
        ok.SetBounds(266, 88, 100, 30);
        ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        ok.Click += (_, _) => { if (Value.Length > 0) DialogResult = DialogResult.OK; };

        var cancel = Theme.FlatButton("Cancelar");
        cancel.SetBounds(158, 88, 100, 30);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.DialogResult = DialogResult.Cancel;

        Controls.Add(lbl);
        Controls.Add(_input);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Shown += (_, _) => { _input.Focus(); _input.SelectAll(); };
    }
}
