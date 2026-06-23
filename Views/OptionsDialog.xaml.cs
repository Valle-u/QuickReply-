using System.Windows;
using System.Windows.Input;
using QuickReply.Models;
using QuickReply.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace QuickReply.Views;

public partial class OptionsDialog : Window
{
    private readonly SnippetStore _store;
    private bool _capturing;

    public OptionsDialog(SnippetStore store)
    {
        _store = store;
        InitializeComponent();
        StartupCheck.IsChecked = StartupManager.IsEnabled();
        PaletteHotkeyBox.Text = store.Settings.PaletteHotkey;
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        _capturing = !_capturing;
        CaptureButton.Content = _capturing ? "Apretá una tecla…" : "Capturar";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { StopCapture(null); return; }
        if (IsModifierKey(key)) return;

        var mods = ModMask.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= ModMask.Control;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= ModMask.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= ModMask.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= ModMask.Win;

        int vk = KeyInterop.VirtualKeyFromKey(key);
        StopCapture(new Hotkey(mods, vk));
    }

    private void StopCapture(Hotkey? hk)
    {
        _capturing = false;
        CaptureButton.Content = "Capturar";
        if (hk is { } h)
            PaletteHotkeyBox.Text = h.ToString();
    }

    private static bool IsModifierKey(Key k) => k is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try { StartupManager.SetEnabled(StartupCheck.IsChecked == true); }
        catch { /* el registro no es crítico */ }

        _store.Settings.PaletteHotkey = PaletteHotkeyBox.Text?.Trim() ?? "";
        try { _store.Save(); } catch { /* ignorar */ }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
