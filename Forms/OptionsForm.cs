using System.Drawing;
using System.Windows.Forms;
using QuickReply.Models;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply.Forms;

public sealed class OptionsForm : Form
{
    private readonly SnippetStore _store;
    private readonly TrayContext _tray;
    private readonly CheckBox _startup;
    private readonly TextBox _hotkey;
    private readonly Button _capture;
    private bool _capturing;

    public OptionsForm(SnippetStore store, TrayContext tray)
    {
        _store = store;
        _tray = tray;
        Text = "Opciones";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 170);
        Theme.ApplyWindow(this);
        Icon = BrandIcon.Create();
        KeyPreview = true;

        _startup = new CheckBox
        {
            Text = "Iniciar QuickReply con Windows",
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Location = new Point(14, 18),
            Checked = StartupManager.IsEnabled(),
        };

        var lbl = Theme.Label("Hotkey de la paleta de búsqueda:");
        lbl.Location = new Point(14, 54);

        _hotkey = Theme.InputBox();
        _hotkey.SetBounds(14, 80, 290, 26);
        _hotkey.ReadOnly = true;
        _hotkey.Font = Theme.Mono;
        _hotkey.Text = store.Settings.PaletteHotkey;
        _hotkey.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        _capture = Theme.FlatButton("Capturar");
        _capture.SetBounds(312, 79, 114, 28);
        _capture.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _capture.Click += (_, _) => ToggleCapture();

        var ok = Theme.PrimaryButton("Guardar");
        ok.SetBounds(326, 128, 100, 30);
        ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        ok.Click += (_, _) => Save();

        var cancel = Theme.FlatButton("Cancelar");
        cancel.SetBounds(218, 128, 100, 30);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.DialogResult = DialogResult.Cancel;

        Controls.Add(_startup);
        Controls.Add(lbl);
        Controls.Add(_hotkey);
        Controls.Add(_capture);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        KeyDown += OnKeyDownCapture;
        FormClosed += (_, _) => { if (_capturing) _tray.EndKeyCapture(); };
    }

    private void ToggleCapture()
    {
        if (_capturing) { StopCapture(); return; }
        _capturing = true;
        _tray.BeginKeyCapture();
        _capture.Text = "Apretá una tecla…";
    }

    private void StopCapture()
    {
        _capturing = false;
        _tray.EndKeyCapture();
        _capture.Text = "Capturar";
    }

    private void OnKeyDownCapture(object? sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        e.SuppressKeyPress = true;

        var kc = e.KeyCode;
        if (kc is Keys.Escape) { StopCapture(); return; }
        if (kc is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin) return;

        var mods = ModMask.None;
        if (e.Control) mods |= ModMask.Control;
        if (e.Alt) mods |= ModMask.Alt;
        if (e.Shift) mods |= ModMask.Shift;

        _hotkey.Text = new Hotkey(mods, (int)kc).ToString();
        StopCapture();
    }

    private void Save()
    {
        try { StartupManager.SetEnabled(_startup.Checked); } catch { }
        _store.Settings.PaletteHotkey = _hotkey.Text.Trim();
        try { _store.Save(); } catch { }
        DialogResult = DialogResult.OK;
    }
}
