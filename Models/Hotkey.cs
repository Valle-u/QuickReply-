using WinKeys = System.Windows.Forms.Keys;

namespace QuickReply.Models;

/// <summary>
/// Una combinación de teclas ya interpretada: modificadores + tecla principal (virtual key code).
/// </summary>
public readonly record struct Hotkey(ModMask Modifiers, int Vk)
{
    /// <summary>
    /// Convierte texto como "Ctrl+Shift+S", "F2", "Alt+1", "Win+Space" en un <see cref="Hotkey"/>.
    /// Devuelve null si el texto está vacío o la tecla no se reconoce.
    /// </summary>
    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mods = ModMask.None;
        int vk = 0;
        bool haveKey = false;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= ModMask.Control;
                    break;
                case "alt":
                    mods |= ModMask.Alt;
                    break;
                case "shift":
                    mods |= ModMask.Shift;
                    break;
                case "win":
                case "windows":
                case "meta":
                    mods |= ModMask.Win;
                    break;
                default:
                    var token = part;
                    // "1" -> "D1" (los dígitos en la enum Keys son D0..D9)
                    if (token.Length == 1 && char.IsDigit(token[0]))
                        token = "D" + token;

                    if (Enum.TryParse<WinKeys>(token, ignoreCase: true, out var key) && key != WinKeys.None)
                    {
                        vk = (int)key;
                        haveKey = true;
                    }
                    else
                    {
                        return null; // tecla desconocida -> hotkey inválido
                    }
                    break;
            }
        }

        return haveKey ? new Hotkey(mods, vk) : null;
    }

    /// <summary>Texto legible para mostrar, ej: "Ctrl+Shift+S".</summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        if (Modifiers.HasFlag(ModMask.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(ModMask.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(ModMask.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(ModMask.Win)) sb.Append("Win+");
        sb.Append(((WinKeys)Vk).ToString());
        return sb.ToString();
    }
}
