using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using QuickReply.Models;
using WinClipboard = System.Windows.Forms.Clipboard;

namespace QuickReply.Services;

/// <summary>
/// Pega el texto de un snippet usando el portapapeles (Ctrl+V) y, si corresponde, manda Enter.
/// Trabaja en un hilo STA propio para no bloquear el hilo del hook de teclado.
/// </summary>
public sealed class TextInjector : IDisposable
{
    private readonly record struct Job(string[] Parts, int Backspaces, bool AutoSend, int CaretLeft, bool SendShortcut = false);

    private readonly BlockingCollection<Job> _queue = new();
    private readonly Thread _thread;
    private readonly Func<AppSettings> _settings;

    public TextInjector(Func<AppSettings> settings)
    {
        _settings = settings;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "QuickReply-Injector",
        };
        _thread.SetApartmentState(ApartmentState.STA); // el portapapeles requiere STA
        _thread.Start();
    }

    /// <summary>
    /// Encola un pegado de uno o varios mensajes (separados por {enter}). Borra N caracteres antes
    /// (abreviatura), manda Enter entre mensajes, y al final según autoSend / cursor.
    /// </summary>
    public void EnqueueMessages(IReadOnlyList<string> parts, int backspaces, bool autoSend, int caretLeft)
        => _queue.Add(new Job(parts.ToArray(), Math.Max(0, backspaces), autoSend, Math.Max(0, caretLeft)));

    /// <summary>Encola un envío "Ctrl+Enter" a la ventana activa (chats que envían con esa combinación, ej. Kommo).</summary>
    public void EnqueueSendShortcut()
        => _queue.Add(new Job(Array.Empty<string>(), 0, false, 0, SendShortcut: true));

    private void Loop()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
        {
            try { Inject(job); }
            catch { /* tragamos errores para no tumbar el hilo. */ }
        }
    }

    private void Inject(Job job)
    {
        if (job.SendShortcut)
        {
            NeutralizeModifiers();
            Thread.Sleep(5);
            SendCtrlEnter();
            return;
        }

        var parts = job.Parts ?? Array.Empty<string>();
        bool hasText = parts.Any(p => !string.IsNullOrEmpty(p));

        if (!hasText && job.Backspaces == 0)
            return;

        var cfg = _settings();

        // Borrar lo que el usuario tipeó de la abreviatura (si corresponde).
        if (job.Backspaces > 0)
        {
            SendBackspaces(job.Backspaces);
            Thread.Sleep(15);
        }

        if (!hasText)
            return;

        string? backup = TryGetClipboardText();
        string lastSet = "";

        for (int i = 0; i < parts.Length; i++)
        {
            var part = NormalizeNewlines(parts[i]);
            if (part.Length == 0) continue;

            if (!TrySetClipboardText(part)) continue;
            lastSet = part;

            Thread.Sleep(Math.Max(40, cfg.PasteDelayMs));
            NeutralizeModifiers(); // Ctrl/Alt/Shift/Win abajo → Ctrl+V limpio
            Thread.Sleep(5);
            SendCtrlV();

            bool isLast = i == parts.Length - 1;
            if (!isLast)
            {
                // Enviar este mensaje y esperar a que el chat limpie el campo antes del próximo.
                Thread.Sleep(Math.Max(60, cfg.EnterDelayMs));
                if (cfg.SendWithCtrlEnter) SendCtrlEnter(); else SendEnter();
                Thread.Sleep(150);
            }
            else if (job.CaretLeft > 0)
            {
                Thread.Sleep(40);
                SendLeft(job.CaretLeft); // colocar el cursor donde estaba {cursor}
            }
            else if (job.AutoSend)
            {
                Thread.Sleep(Math.Max(10, cfg.EnterDelayMs));
                if (cfg.SendWithCtrlEnter) SendCtrlEnter(); else SendEnter();
            }
        }

        // Restaurar el portapapeles con cuidado (esperar a que apps lentas peguen; no pisar copias nuevas).
        Thread.Sleep(Math.Max(400, cfg.RestoreClipboardDelayMs));
        if (backup is not null)
        {
            var current = TryGetClipboardText();
            if (current == lastSet)
                TrySetClipboardText(backup);
        }
    }

    private static string NormalizeNewlines(string s)
        => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

    // ---- Portapapeles (con reintentos: a veces está bloqueado por otra app) ----

    private static string? TryGetClipboardText()
    {
        for (int i = 0; i < 6; i++)
        {
            try { return WinClipboard.ContainsText() ? WinClipboard.GetText() : null; }
            catch { Thread.Sleep(20); }
        }
        return null;
    }

    private static bool TrySetClipboardText(string text)
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                // copy: true -> los datos quedan en el portapapeles aunque cierre la app.
                WinClipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch { Thread.Sleep(25); }
        }
        return false;
    }

    // ---- Envío de teclas (SendInput) ----

    private static void SendCtrlV() => SendInputs(
        (VK_CONTROL, false),
        (VK_V, false),
        (VK_V, true),
        (VK_CONTROL, true));

    private static void SendEnter() => SendInputs(
        (VK_RETURN, false),
        (VK_RETURN, true));

    private static void SendCtrlEnter() => SendInputs(
        (VK_CONTROL, false),
        (VK_RETURN, false),
        (VK_RETURN, true),
        (VK_CONTROL, true));

    private static void SendBackspaces(int count)
    {
        if (count <= 0) return;
        var keys = new (ushort vk, bool up)[count * 2];
        for (int i = 0; i < count; i++)
        {
            keys[i * 2] = (VK_BACK, false);
            keys[i * 2 + 1] = (VK_BACK, true);
        }
        SendInputs(keys);
    }

    private static void SendLeft(int count)
    {
        if (count <= 0) return;
        var keys = new (ushort vk, bool up)[count * 2];
        for (int i = 0; i < count; i++)
        {
            keys[i * 2] = (VK_LEFT, false);
            keys[i * 2 + 1] = (VK_LEFT, true);
        }
        SendInputs(keys);
    }

    private static void NeutralizeModifiers() => SendInputs(
        (VK_LCONTROL, true),
        (VK_RCONTROL, true),
        (VK_LMENU, true),
        (VK_RMENU, true),
        (VK_LSHIFT, true),
        (VK_RSHIFT, true),
        (VK_LWIN, true),
        (VK_RWIN, true));

    private static readonly HashSet<ushort> Extended = new()
    {
        VK_RCONTROL, VK_RMENU, VK_LWIN, VK_RWIN, VK_LEFT,
    };

    private static void SendInputs(params (ushort vk, bool up)[] keys)
    {
        var inputs = new INPUT[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            uint flags = 0;
            if (keys[i].up) flags |= KEYEVENTF_KEYUP;
            if (Extended.Contains(keys[i].vk)) flags |= KEYEVENTF_EXTENDEDKEY;

            inputs[i] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keys[i].vk,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
    }

    // ---- Constantes de teclas ----

    private const ushort VK_BACK = 0x08;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RWIN = 0x5C;
    private const ushort VK_LSHIFT = 0xA0;
    private const ushort VK_RSHIFT = 0xA1;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_RCONTROL = 0xA3;
    private const ushort VK_LMENU = 0xA4;
    private const ushort VK_RMENU = 0xA5;

    // ---- P/Invoke SendInput ----

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
