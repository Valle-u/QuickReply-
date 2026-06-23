using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using QuickReply.Models;
using QuickReply.Services;
using QuickReply.Views;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using MessageBox = System.Windows.MessageBox;

namespace QuickReply;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private SnippetStore _store = null!;
    private KeyboardHook _hook = null!;
    private TextInjector _injector = null!;
    private WinForms.NotifyIcon _tray = null!;
    private WinForms.ToolStripMenuItem _pauseItem = null!;
    private MainWindow? _configWindow;
    private Hotkey? _paletteHotkey;
    private IntPtr _paletteTarget;
    private PaletteWindow? _palette;
    private readonly System.Text.StringBuilder _typed = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Una sola instancia a la vez (evita hooks duplicados).
        _mutex = new Mutex(initiallyOwned: true, "QuickReply.SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("QuickReply ya está en ejecución.", "QuickReply",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "snippets.json");
        _store = new SnippetStore(path);

        try
        {
            _store.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo leer la configuración:\n\n{ex.Message}\n\nRevisá el archivo:\n{path}",
                "QuickReply", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _injector = new TextInjector(() => _store.Settings);
        _paletteHotkey = Hotkey.Parse(_store.Settings.PaletteHotkey);

        _hook = new KeyboardHook { OnKeyDown = OnKeyDown };
        try
        {
            _hook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo iniciar el motor de teclado:\n\n{ex.Message}",
                "QuickReply", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        SetupTray();
        ShowBalloon($"QuickReply activo · {_store.ValidCount} textos listos");

        _store.Changed += UpdateCharacterTracking;
        UpdateCharacterTracking();

        // Permite abrir la configuración directo:  QuickReply.exe --config
        if (e.Args.Any(a => a.Equals("--config", StringComparison.OrdinalIgnoreCase)))
            ShowConfig();
    }

    private bool OnKeyDown(KeyEventArgsLL args)
    {
        // ¿Es la hotkey de la paleta de búsqueda?
        if (_paletteHotkey is { } ph && ph.Vk == args.Vk && ph.Modifiers == args.Modifiers)
        {
            _typed.Clear();
            var target = GetForegroundWindow(); // recordar dónde estaba el foco (el chat)
            Dispatcher.BeginInvoke(new Action(() => OpenPalette(target)));
            return true;
        }

        // Mientras la paleta está abierta, no disparar textos (se está tipeando en el buscador).
        if (_palette is not null)
            return false;

        // Disparador por tecla/combinación
        var snippet = _store.Match(args.Vk, args.Modifiers);
        if (snippet is not null)
        {
            _typed.Clear();
            return FireSnippet(snippet, 0); // tragamos la tecla disparadora
        }

        // Abreviaturas que se expanden al tipear
        return HandleAbbreviations(args);
    }

    private bool HandleAbbreviations(KeyEventArgsLL args)
    {
        // Atajos (Ctrl/Win) invalidan el buffer (no es tipeo de texto). AltGr (Ctrl+Alt) sí escribe.
        bool shortcut = (args.Modifiers.HasFlag(ModMask.Control) && !args.Modifiers.HasFlag(ModMask.Alt))
                        || args.Modifiers.HasFlag(ModMask.Win);
        if (shortcut) { _typed.Clear(); return false; }

        // Teclas que cortan el "renglón" en curso
        if (IsBoundaryKey(args.Vk)) { _typed.Clear(); return false; }

        if (args.Vk == 0x08) // Backspace
        {
            if (_typed.Length > 0) _typed.Remove(_typed.Length - 1, 1);
            return false;
        }

        var text = args.Text;
        if (string.IsNullOrEmpty(text) || text[0] < ' ')
            return false; // no es un carácter imprimible

        _typed.Append(text);
        if (_typed.Length > 64)
            _typed.Remove(0, _typed.Length - 64);

        var match = _store.MatchAbbreviation(_typed.ToString());
        if (match is { } m)
        {
            // Borrar lo que ya se tipeó (todo menos el último char, que tragamos) y pegar.
            _typed.Clear();
            return FireSnippet(m.snippet, m.length - 1);
        }

        return false;
    }

    /// <summary>Procesa placeholders y encola el pegado. Devuelve true (traga la tecla disparadora).</summary>
    private bool FireSnippet(Snippet s, int backspaces)
    {
        if (HasVariables(s.Content))
        {
            // Necesita diálogo: diferir (no bloquear el hook) recordando dónde estaba el foco.
            var target = GetForegroundWindow();
            Dispatcher.BeginInvoke(new Action(() => DispatchSnippetWithDialog(s, backspaces, target)));
        }
        else
        {
            EnqueueProcessed(s, backspaces, IntPtr.Zero, null);
        }
        return true;
    }

    private void DispatchSnippetWithDialog(Snippet s, int backspaces, IntPtr target)
    {
        var vars = ExtractVariables(s.Content);
        var dlg = new VariablesDialog(s.Title, vars);
        if (_configWindow is { IsVisible: true })
            dlg.Owner = _configWindow;
        if (dlg.ShowDialog() != true)
            return; // cancelado
        EnqueueProcessed(s, backspaces, target, dlg.Values);
    }

    private void EnqueueProcessed(Snippet s, int backspaces, IntPtr target, Dictionary<string, string>? values)
    {
        var (parts, caretLeft) = ProcessToMessages(s.Content, values);
        if (target != IntPtr.Zero)
            SetForegroundWindow(target); // devolver el foco al chat antes de pegar
        _injector.EnqueueMessages(parts, backspaces, s.AutoSend, caretLeft);
    }

    // ---- Placeholders / texto dinámico ----

    private static readonly HashSet<string> BuiltInPlaceholders =
        new(StringComparer.OrdinalIgnoreCase) { "fecha", "hora", "fechahora", "cursor", "enter" };

    private static List<string> ExtractVariables(string content)
    {
        var list = new List<string>();
        foreach (Match m in Regex.Matches(content ?? "", @"\{([^{}]+)\}"))
        {
            var token = m.Groups[1].Value.Trim();
            if (token.Length == 0 || BuiltInPlaceholders.Contains(token)) continue;
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
            foreach (var kv in values)
                s = ReplaceToken(s, kv.Key, kv.Value);

        var raw = Regex.Split(s, @"\{enter\}", RegexOptions.IgnoreCase);

        // Un solo mensaje: respetar {cursor}.
        if (raw.Length == 1)
        {
            string one = raw[0];
            int caretLeft = 0;
            var cur = Regex.Match(one, @"\{cursor\}", RegexOptions.IgnoreCase);
            if (cur.Success)
            {
                var after = one[(cur.Index + cur.Length)..].Replace("\r\n", "\n").Replace("\r", "\n");
                caretLeft = after.Length; // cada salto de línea cuenta como 1
                one = one.Remove(cur.Index, cur.Length);
            }
            return (string.IsNullOrWhiteSpace(one) ? Array.Empty<string>() : new[] { one }, caretLeft);
        }

        // Varios mensajes: sacar {cursor}, recortar y descartar partes vacías.
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
        0x0D or 0x09 or 0x1B or          // Enter, Tab, Esc
        0x25 or 0x26 or 0x27 or 0x28 or  // ← ↑ → ↓
        0x24 or 0x23 or 0x21 or 0x22 or  // Home, End, RePág, AvPág
        0x2E or 0x2D;                    // Supr, Insert

    private void OpenPalette(IntPtr target)
    {
        if (_palette is not null) { _palette.Activate(); return; }

        _paletteTarget = target;
        _palette = new PaletteWindow(_store);
        _palette.Picked += OnPalettePick;
        _palette.Closed += (_, _) => _palette = null;
        _palette.Show();
        _palette.Activate();
    }

    private void OnPalettePick(Snippet snippet)
    {
        // Ya estamos en el hilo de UI (no en el hook): podemos mostrar el diálogo directamente.
        if (HasVariables(snippet.Content))
            DispatchSnippetWithDialog(snippet, 0, _paletteTarget);
        else
            EnqueueProcessed(snippet, 0, _paletteTarget, null);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void SetupTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = BrandIcon.CreateTrayIcon(),
            Visible = true,
            Text = "QuickReply",
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Configurar textos…", null, (_, _) => ShowConfig());
        menu.Items.Add("Opciones…", null, (_, _) => ShowOptions());
        _pauseItem = new WinForms.ToolStripMenuItem("Pausar", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApp());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowConfig();
    }

    private void ShowConfig()
    {
        if (_configWindow is null)
        {
            _configWindow = new MainWindow(_store);
            _configWindow.Closed += (_, _) => _configWindow = null; // liberar memoria al cerrar
        }
        _configWindow.Show();
        if (_configWindow.WindowState == WindowState.Minimized)
            _configWindow.WindowState = WindowState.Normal;
        _configWindow.Activate();
    }

    private void UpdateCharacterTracking()
    {
        if (_hook is not null)
            _hook.TrackCharacters = _store.HasAbbreviations;
    }

    // Mientras se captura una tecla en la config, pausamos el hook para que no intercepte/dispare.
    private bool _hookPausedBeforeCapture;

    public void BeginKeyCapture()
    {
        _hookPausedBeforeCapture = _hook.Paused;
        _hook.Paused = true;
    }

    public void EndKeyCapture()
    {
        _hook.Paused = _hookPausedBeforeCapture;
    }

    public void ShowOptions()
    {
        var dlg = new OptionsDialog(_store);
        if (_configWindow is { IsVisible: true })
            dlg.Owner = _configWindow;
        if (dlg.ShowDialog() == true)
            ReloadSettings();
    }

    /// <summary>Re-lee ajustes que cambian en caliente (hotkey de la paleta).</summary>
    public void ReloadSettings()
    {
        _paletteHotkey = Hotkey.Parse(_store.Settings.PaletteHotkey);
    }

    private void TogglePause()
    {
        _hook.Paused = !_hook.Paused;
        _pauseItem.Text = _hook.Paused ? "Reanudar" : "Pausar";
        _tray.Text = _hook.Paused ? "QuickReply (en pausa)" : "QuickReply";
        ShowBalloon(_hook.Paused ? "QuickReply en pausa" : "QuickReply reanudado");
    }

    private void ExitApp() => Shutdown();

    private void ShowBalloon(string message)
    {
        if (_tray is null) return;
        _tray.BalloonTipTitle = "QuickReply";
        _tray.BalloonTipText = message;
        _tray.ShowBalloonTip(1500);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _store?.Save(); } catch { /* mejor esfuerzo */ }
        _hook?.Dispose();
        _injector?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
