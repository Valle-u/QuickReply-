using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using QuickReply.Models;

namespace QuickReply.Services;

/// <summary>Datos de una tecla presionada que llegan al manejador.</summary>
public readonly record struct KeyEventArgsLL(int Vk, ModMask Modifiers, string? Text);

/// <summary>
/// Engancha el teclado a bajo nivel (WH_KEYBOARD_LL), igual que hace AutoHotkey por debajo.
/// Permite detectar cualquier tecla/combinación y "tragarla" para que no escriba su carácter.
///
/// El estado de los modificadores (Ctrl/Alt/Shift/Win) se calcula a partir de los propios
/// eventos del hook (no con GetAsyncKeyState, que dentro del hook no es confiable).
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_INJECTED = 0x10;

    // Virtual key codes de los modificadores (genéricos y específicos izq/der).
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_CAPITAL = 0x14;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    // Conjunto de modificadores actualmente presionados (los seguimos nosotros).
    private readonly HashSet<int> _modsDown = new();

    /// <summary>
    /// Manejador que se llama en cada tecla presionada.
    /// Si devuelve true, la tecla se "traga" (no llega a la app activa).
    /// </summary>
    public Func<KeyEventArgsLL, bool>? OnKeyDown { get; set; }

    /// <summary>Si está en pausa, no procesa ni traga ninguna tecla.</summary>
    public bool Paused { get; set; }

    /// <summary>Solo traduce el carácter tipeado (para abreviaturas) si hace falta. Ahorra trabajo por tecla.</summary>
    public bool TrackCharacters { get; set; }

    // Buffers reutilizados para no generar basura (GC) en cada tecla.
    private readonly byte[] _keyState = new byte[256];
    private readonly StringBuilder _charBuf = new(8);
    private IntPtr _cachedLayout;
    private long _layoutTick;

    public KeyboardHook() => _proc = HookCallback;

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo instalar el hook de teclado.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)data.vkCode;
            bool injected = (data.flags & LLKHF_INJECTED) != 0;
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            // Ignoramos los eventos que generamos nosotros (SendInput) para no ensuciar el estado.
            if (!injected)
            {
                // Mantener el estado de los modificadores al día.
                if (IsModifierVk(vk))
                {
                    if (isDown) _modsDown.Add(vk);
                    else if (isUp) _modsDown.Remove(vk);
                }
                else if (isDown && !Paused)
                {
                    var mods = CurrentModifiers();
                    string? text = TrackCharacters ? TranslateChar(data.vkCode, data.scanCode, mods) : null;
                    var args = new KeyEventArgsLL(vk, mods, text);
                    try
                    {
                        if (OnKeyDown?.Invoke(args) == true)
                            return (IntPtr)1; // tragar la tecla
                    }
                    catch
                    {
                        // Nunca dejar que una excepción rompa el hook del sistema.
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private ModMask CurrentModifiers()
    {
        var m = ModMask.None;
        if (_modsDown.Contains(VK_LCONTROL) || _modsDown.Contains(VK_RCONTROL) || _modsDown.Contains(VK_CONTROL))
            m |= ModMask.Control;
        if (_modsDown.Contains(VK_LMENU) || _modsDown.Contains(VK_RMENU) || _modsDown.Contains(VK_MENU))
            m |= ModMask.Alt;
        if (_modsDown.Contains(VK_LSHIFT) || _modsDown.Contains(VK_RSHIFT) || _modsDown.Contains(VK_SHIFT))
            m |= ModMask.Shift;
        if (_modsDown.Contains(VK_LWIN) || _modsDown.Contains(VK_RWIN))
            m |= ModMask.Win;
        return m;
    }

    private static bool IsModifierVk(int vk) => vk is
        VK_SHIFT or VK_CONTROL or VK_MENU or
        VK_LSHIFT or VK_RSHIFT or
        VK_LCONTROL or VK_RCONTROL or
        VK_LMENU or VK_RMENU or
        VK_LWIN or VK_RWIN;

    /// <summary>
    /// Traduce la tecla al carácter que produciría, según el layout activo y los modificadores.
    /// Usa wFlags=4 (no modifica el estado del teclado) para NO romper acentos/dead keys.
    /// </summary>
    private string? TranslateChar(uint vk, uint scan, ModMask mods)
    {
        Array.Clear(_keyState, 0, _keyState.Length);
        if (mods.HasFlag(ModMask.Shift)) _keyState[VK_SHIFT] = 0x80;
        if (mods.HasFlag(ModMask.Control)) _keyState[VK_CONTROL] = 0x80;
        if (mods.HasFlag(ModMask.Alt)) _keyState[VK_MENU] = 0x80;
        if ((GetKeyState(VK_CAPITAL) & 1) != 0) _keyState[VK_CAPITAL] = 1;

        // Cachear el layout del teclado (~1 vez por segundo) en vez de 3 syscalls por tecla.
        long now = Environment.TickCount64;
        if (_cachedLayout == IntPtr.Zero || now - _layoutTick > 1000)
        {
            _cachedLayout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out _));
            _layoutTick = now;
        }

        _charBuf.Clear();
        int rc = ToUnicodeEx(vk, scan, _keyState, _charBuf, _charBuf.Capacity, 4, _cachedLayout);
        return rc > 0 ? _charBuf.ToString() : null;
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    // ---- P/Invoke ----

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
}
