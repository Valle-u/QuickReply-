using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using QuickReply.Forms;
using QuickReply.Models;
using QuickReply.Services;
using QuickReply.Ui;

namespace QuickReply;

/// <summary>App de bandeja: cablea el hook de teclado, el inyector y las ventanas (WinForms).</summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly SnippetStore _store;
    private readonly KeyboardHook _hook;
    private readonly TextInjector _injector;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly Control _marshal; // para volver al hilo de UI desde el hook

    private MainForm? _config;
    private PaletteForm? _palette;
    private IntPtr _paletteTarget;
    private Hotkey? _paletteHotkey;
    private Hotkey? _sendHotkey;
    private readonly StringBuilder _typed = new();
    private bool _hookPausedBeforeCapture;

    public TrayContext()
    {
        _marshal = new Control();
        _ = _marshal.Handle; // forzar creación en el hilo de UI

        string path = Path.Combine(AppContext.BaseDirectory, "snippets.json");
        _store = new SnippetStore(path);
        try { _store.Load(); }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo leer la configuración:\n\n{ex.Message}", "QuickReply",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _injector = new TextInjector(() => _store.Settings);
        _paletteHotkey = Hotkey.Parse(_store.Settings.PaletteHotkey);
        _sendHotkey = Hotkey.Parse(_store.Settings.SendHotkey);

        _hook = new KeyboardHook { OnKeyDown = OnKeyDown, TrackCharacters = _store.HasAbbreviations };
        _store.Changed += () => _hook.TrackCharacters = _store.HasAbbreviations;
        _hook.Install();

        _pauseItem = new ToolStripMenuItem("Pausar", null, (_, _) => TogglePause());
        var menu = new ContextMenuStrip { Renderer = new DarkToolStripRenderer(), ForeColor = Theme.Text };
        menu.BackColor = Theme.Panel;
        menu.Items.Add(new ToolStripMenuItem("Configurar textos…", null, (_, _) => ShowConfig()));
        menu.Items.Add(new ToolStripMenuItem("Opciones…", null, (_, _) => ShowOptions()));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Salir", null, (_, _) => ExitApp()));

        _tray = new NotifyIcon
        {
            Icon = BrandIcon.Create(),
            Visible = true,
            Text = "QuickReply",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowConfig();

        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--config", StringComparison.OrdinalIgnoreCase)))
            ShowConfig();
    }

    // ---- Hook ----

    private bool OnKeyDown(KeyEventArgsLL args)
    {
        if (_paletteHotkey is { } ph && ph.Vk == args.Vk && ph.Modifiers == args.Modifiers)
        {
            _typed.Clear();
            var target = GetForegroundWindow();
            _marshal.BeginInvoke(new Action(() => OpenPalette(target)));
            return true;
        }

        if (_palette is not null)
            return false;

        // Tecla de envío rápido: manda Ctrl+Enter a la ventana activa (ej. para enviar en Kommo).
        if (_sendHotkey is { } sh && sh.Vk == args.Vk && sh.Modifiers == args.Modifiers)
        {
            _injector.EnqueueSendShortcut();
            return true;
        }

        var snippet = _store.Match(args.Vk, args.Modifiers);
        if (snippet is not null)
        {
            _typed.Clear();
            return FireSnippet(snippet, 0);
        }

        return HandleAbbreviations(args);
    }

    private bool HandleAbbreviations(KeyEventArgsLL args)
    {
        bool shortcut = (args.Modifiers.HasFlag(ModMask.Control) && !args.Modifiers.HasFlag(ModMask.Alt))
                        || args.Modifiers.HasFlag(ModMask.Win);
        if (shortcut) { _typed.Clear(); return false; }
        if (IsBoundaryKey(args.Vk)) { _typed.Clear(); return false; }

        if (args.Vk == 0x08) // Backspace
        {
            if (_typed.Length > 0) _typed.Remove(_typed.Length - 1, 1);
            return false;
        }

        var text = args.Text;
        if (string.IsNullOrEmpty(text) || text[0] < ' ') return false;

        _typed.Append(text);
        if (_typed.Length > 64) _typed.Remove(0, _typed.Length - 64);

        var match = _store.MatchAbbreviation(_typed.ToString());
        if (match is { } m)
        {
            _typed.Clear();
            return FireSnippet(m.snippet, m.length - 1);
        }
        return false;
    }

    private bool FireSnippet(Snippet s, int backspaces)
    {
        if (HasVariables(s.Content))
        {
            var target = GetForegroundWindow();
            _marshal.BeginInvoke(new Action(() => DispatchWithDialog(s, backspaces, target)));
        }
        else
        {
            EnqueueProcessed(s, backspaces, IntPtr.Zero, null);
        }
        return true;
    }

    private void DispatchWithDialog(Snippet s, int backspaces, IntPtr target)
    {
        var vars = ExtractVariables(s.Content);
        using var dlg = new VariablesForm(s.Title, vars);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        EnqueueProcessed(s, backspaces, target, dlg.Values);
    }

    private void EnqueueProcessed(Snippet s, int backspaces, IntPtr target, Dictionary<string, string>? values)
    {
        var (parts, caretLeft) = ProcessToMessages(s.Content, values);
        if (target != IntPtr.Zero) SetForegroundWindow(target);
        _injector.EnqueueMessages(parts, backspaces, s.AutoSend, caretLeft);
    }

    // ---- Paleta ----

    private void OpenPalette(IntPtr target)
    {
        if (_palette is not null) { _palette.Activate(); return; }
        _paletteTarget = target;
        _palette = new PaletteForm(_store);
        _palette.Picked += s => OnPalettePick(s);
        _palette.FormClosed += (_, _) => _palette = null;
        _palette.Show();
        _palette.Activate();
    }

    private void OnPalettePick(Snippet snippet)
    {
        if (HasVariables(snippet.Content))
            DispatchWithDialog(snippet, 0, _paletteTarget);
        else
            EnqueueProcessed(snippet, 0, _paletteTarget, null);
    }

    // ---- Ventanas ----

    private void ShowConfig()
    {
        if (_config is null || _config.IsDisposed)
        {
            _config = new MainForm(_store, this);
            _config.FormClosed += (_, _) => _config = null;
        }
        if (_config.WindowState == FormWindowState.Minimized) _config.WindowState = FormWindowState.Normal;
        _config.Show();
        _config.Activate();
    }

    public void ShowOptions()
    {
        using var dlg = new OptionsForm(_store, this);
        if (dlg.ShowDialog(_config) == DialogResult.OK)
        {
            _paletteHotkey = Hotkey.Parse(_store.Settings.PaletteHotkey);
            _sendHotkey = Hotkey.Parse(_store.Settings.SendHotkey);
        }
    }

    private void TogglePause()
    {
        _hook.Paused = !_hook.Paused;
        _pauseItem.Text = _hook.Paused ? "Reanudar" : "Pausar";
        _tray.Text = _hook.Paused ? "QuickReply (en pausa)" : "QuickReply";
    }

    public void BeginKeyCapture() { _hookPausedBeforeCapture = _hook.Paused; _hook.Paused = true; }
    public void EndKeyCapture() { _hook.Paused = _hookPausedBeforeCapture; }

    private void ExitApp()
    {
        _hook.Dispose();
        _injector.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }

    // ---- Placeholders / texto dinámico ----

    private static readonly HashSet<string> BuiltIns =
        new(StringComparer.OrdinalIgnoreCase) { "fecha", "hora", "fechahora", "cursor", "enter" };

    private static List<string> ExtractVariables(string content)
    {
        var list = new List<string>();
        foreach (Match m in Regex.Matches(content ?? "", @"\{([^{}]+)\}"))
        {
            var token = m.Groups[1].Value.Trim();
            if (token.Length == 0 || BuiltIns.Contains(token)) continue;
            if (!list.Any(x => string.Equals(x, token, StringComparison.OrdinalIgnoreCase)))
                list.Add(token);
        }
        return list;
    }

    private static bool HasVariables(string content) => ExtractVariables(content).Count > 0;

    private static (string[] parts, int caretLeft) ProcessToMessages(string content, Dictionary<string, string>? values)
    {
        var now = DateTime.Now;
        string s = content ?? "";
        s = ReplaceToken(s, "fechahora", now.ToString("dd/MM/yyyy HH:mm"));
        s = ReplaceToken(s, "fecha", now.ToString("dd/MM/yyyy"));
        s = ReplaceToken(s, "hora", now.ToString("HH:mm"));
        if (values is not null)
            foreach (var kv in values) s = ReplaceToken(s, kv.Key, kv.Value);

        var raw = Regex.Split(s, @"\{enter\}", RegexOptions.IgnoreCase);
        if (raw.Length == 1)
        {
            string one = raw[0];
            int caretLeft = 0;
            var cur = Regex.Match(one, @"\{cursor\}", RegexOptions.IgnoreCase);
            if (cur.Success)
            {
                var after = one[(cur.Index + cur.Length)..].Replace("\r\n", "\n").Replace("\r", "\n");
                caretLeft = after.Length;
                one = one.Remove(cur.Index, cur.Length);
            }
            return (string.IsNullOrWhiteSpace(one) ? Array.Empty<string>() : new[] { one }, caretLeft);
        }

        var parts = raw
            .Select(p => Regex.Replace(p, @"\{cursor\}", "", RegexOptions.IgnoreCase).Trim())
            .Where(p => p.Length > 0)
            .ToArray();
        return (parts, 0);
    }

    private static string ReplaceToken(string input, string token, string value)
        => Regex.Replace(input, @"\{" + Regex.Escape(token) + @"\}",
                         (value ?? "").Replace("$", "$$"), RegexOptions.IgnoreCase);

    private static bool IsBoundaryKey(int vk) => vk is
        0x0D or 0x09 or 0x1B or 0x25 or 0x26 or 0x27 or 0x28 or
        0x24 or 0x23 or 0x21 or 0x22 or 0x2E or 0x2D;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hWnd);
}
