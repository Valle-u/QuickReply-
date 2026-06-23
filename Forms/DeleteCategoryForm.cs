using System.Drawing;
using System.Windows.Forms;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

public sealed class DeleteCategoryForm : Form
{
    private readonly ComboBox _combo;
    private readonly Label _info;
    private readonly SnippetStore _store;

    public string? Selected => _combo.SelectedItem as string;

    public DeleteCategoryForm(SnippetStore store)
    {
        _store = store;
        Text = "Eliminar categoría";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 150);
        Theme.ApplyWindow(this);
        Icon = BrandIcon.Create();

        var lbl = Theme.Label("Elegí la categoría a eliminar:");
        lbl.Location = new Point(14, 16);

        _combo = Theme.ComboDark();
        _combo.SetBounds(14, 42, 372, 26);
        _combo.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        _info = Theme.Label("", muted: true);
        _info.Location = new Point(14, 76);
        _info.MaximumSize = new Size(372, 0);

        var deletable = store.Categories
            .Where(c => !string.Equals(c, "General", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var ok = Theme.PrimaryButton("Eliminar");
        ok.SetBounds(286, 108, 100, 30);
        ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        ok.Click += (_, _) => { if (Selected is not null) DialogResult = DialogResult.OK; };

        var cancel = Theme.FlatButton("Cancelar");
        cancel.SetBounds(178, 108, 100, 30);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.DialogResult = DialogResult.Cancel;

        _combo.SelectedIndexChanged += (_, _) => UpdateInfo();
        _combo.Items.AddRange(deletable);
        if (deletable.Length > 0) _combo.SelectedIndex = 0;
        else { _info.Text = "No hay categorías para eliminar («General» no se puede borrar)."; ok.Enabled = false; }

        Controls.Add(lbl);
        Controls.Add(_combo);
        Controls.Add(_info);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void UpdateInfo()
    {
        if (Selected is null) { _info.Text = ""; return; }
        int n = _store.CountUsage(Selected);
        _info.Text = n == 0 ? "Ningún texto usa esta categoría." : $"{n} texto(s) pasarán a «General».";
    }
}
