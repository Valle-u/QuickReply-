namespace QuickReply.Models;

/// <summary>
/// Modificadores de una combinación de teclas (Ctrl / Alt / Shift / Win).
/// Es independiente de izquierda/derecha: solo importa si está presionado o no.
/// </summary>
[Flags]
public enum ModMask
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}
