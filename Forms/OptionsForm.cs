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
    private readonly TextBox _sendKey;
    private readonly Button _captureSend;
    private readonly CheckBox _ctrlEnter;

    private bool _capturing;
    private TextBox? _captureTarget;
    private Button? _captureBtn;

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
        ClientSize = new Size(470, 296);
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
            Location = new Point(14, 16),
            Checked = StartupManager.IsEnabled(),
        };

        var lblPalette = Theme.Label("Hotkey de la paleta de búsqueda:");
        lblPalette.Location = new Point(14, 48);

        _hotkey = Theme.InputBox();
        _hotkey.SetBounds(14, 72, 300, 26);
        _hotkey.ReadOnly = true;
        _hotkey.Font = Theme.Mono;
        _hotkey.Text = store.Settings.PaletteHotkey;
        _hotkey.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        _capture = Theme.FlatButton("Capturar");
        _capture.SetBounds(322, 71, 120, 28);
        _capture.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _capture.Click += (_, _) => ToggleCapture(_hotkey, _capture);

        var lblSend = Theme.Label("Tecla de envío rápido — manda Ctrl+Enter (para Kommo):");
        lblSend.Location = new Point(14, 110);

        _sendKey = Theme.InputBox();
        _sendKey.SetBounds(14, 134, 300, 26);
        _sendKey.ReadOnly = true;
        _sendKey.Font = Theme.Mono;
        _sendKey.Text = store.Settings.SendHotkey;
        _sendKey.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        _captureSend = Theme.FlatButton("Capturar");
        _captureSend.SetBounds(322, 133, 120, 28);
        _captureSend.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _captureSend.Click += (_, _) => ToggleCapture(_sendKey, _captureSend);

        var hint = Theme.Label("Elegí una tecla libre (ej. F9, Insertar). Supr durante la captura la borra.", muted: true);
        hint.Location = new Point(14, 164);

        _ctrlEnter = new CheckBox
        {
            Text = "Auto-enviar los textos con Ctrl+Enter (en vez de Enter)",
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Location = new Point(14, 196),
            Checked = store.Settings.SendWithCtrlEnter,
        };

        var ok = Theme.PrimaryButton("Guardar");
        ok.SetBounds(342, 252, 100, 30);
        ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        ok.Click += (_, _) => Save();

        var cancel = Theme.FlatButton("Cancelar");
        cancel.SetBounds(234, 252, 100, 30);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.DialogResult = DialogResult.Cancel;

        Controls.Add(_startup);
        Controls.Add(lblPalette);
        Controls.Add(_hotkey);
        Controls.Add(_capture);
        Controls.Add(lblSend);
        Controls.Add(_sendKey);
        Controls.Add(_captureSend);
        Controls.Add(hint);
        Controls.Add(_ctrlEnter);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        FormClosed += (_, _) => { if (_capturing) _tray.EndKeyCapture(); };
    }

    private void ToggleCapture(TextBox target, Button btn)
    {
        if (_capturing) { StopCapture(); return; }
        _capturing = true;
        _captureTarget = target;
        _captureBtn = btn;
        btn.Text = "Apretá una tecla…";
        _tray.BeginKeyCapture(OnKeyCaptured);
    }

    private void StopCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        _tray.EndKeyCapture();
        if (_captureBtn is not null) _captureBtn.Text = "Capturar";
        _captureBtn = null;
        _captureTarget = null;
    }

    // El hook global nos entrega la tecla capturada (en el hilo de UI).
    private void OnKeyCaptured(Hotkey? hk)
    {
        var target = _captureTarget;
        _capturing = false;
        if (_captureBtn is not null) _captureBtn.Text = "Capturar";
        _captureBtn = null;
        _captureTarget = null;

        if (target is null || hk is not { } h) return; // Esc: cancelado
        // Supr / Backspace: limpiar (desactivar) la tecla.
        if (h.Modifiers == ModMask.None && (h.Vk == 0x2E || h.Vk == 0x08)) target.Text = "";
        else target.Text = h.ToString();
    }

    private void Save()
    {
        try { StartupManager.SetEnabled(_startup.Checked); } catch { }
        _store.Settings.PaletteHotkey = _hotkey.Text.Trim();
        _store.Settings.SendHotkey = _sendKey.Text.Trim();
        _store.Settings.SendWithCtrlEnter = _ctrlEnter.Checked;
        try { _store.Save(); } catch { }
        DialogResult = DialogResult.OK;
    }
}
