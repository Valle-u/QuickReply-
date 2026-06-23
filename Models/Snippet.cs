using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace QuickReply.Models;

/// <summary>
/// Un texto predeterminado con su disparador.
/// Notifica cambios para que la interfaz se actualice sola.
/// </summary>
public sealed class Snippet : INotifyPropertyChanged
{
    private string _title = "";
    private string _category = "General";
    private string _trigger = "";
    private string _abbreviation = "";
    private bool _autoSend;
    private string _content = "";

    /// <summary>Nombre visible del texto (ej: "Saludo inicial").</summary>
    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    /// <summary>Categoría para organizar (ej: "Pagos", "General").</summary>
    public string Category
    {
        get => _category;
        set => Set(ref _category, string.IsNullOrWhiteSpace(value) ? "General" : value);
    }

    /// <summary>Disparador en texto, ej: "Ctrl+Shift+S" o "F2".</summary>
    public string Trigger
    {
        get => _trigger;
        set
        {
            if (Set(ref _trigger, value))
            {
                Hotkey = QuickReply.Models.Hotkey.Parse(value);
                OnPropertyChanged(nameof(Hotkey));
            }
        }
    }

    /// <summary>Abreviatura que se expande al tipear (ej: "/saludo"). Opcional.</summary>
    public string Abbreviation
    {
        get => _abbreviation;
        set => Set(ref _abbreviation, value ?? "");
    }

    /// <summary>Si es true, después de pegar manda Enter automáticamente.</summary>
    public bool AutoSend
    {
        get => _autoSend;
        set => Set(ref _autoSend, value);
    }

    /// <summary>El contenido a pegar (soporta saltos de línea y emojis).</summary>
    public string Content
    {
        get => _content;
        set => Set(ref _content, value);
    }

    /// <summary>Disparador ya interpretado. Se calcula al cargar/editar; no se guarda en el JSON.</summary>
    [JsonIgnore]
    public Hotkey? Hotkey { get; set; }

    // ---- INotifyPropertyChanged ----

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Ajustes generales de la app.</summary>
public sealed class AppSettings
{
    /// <summary>Espera (ms) entre copiar al portapapeles y pegar.</summary>
    public int PasteDelayMs { get; set; } = 60;

    /// <summary>Espera (ms) entre pegar y mandar Enter (cuando AutoSend está activo).</summary>
    public int EnterDelayMs { get; set; } = 90;

    /// <summary>Espera (ms) antes de restaurar el portapapeles original (alto para chats web lentos).</summary>
    public int RestoreClipboardDelayMs { get; set; } = 400;

    /// <summary>Combinación que abre la ventana de búsqueda (paleta). Ej: "Ctrl+Space".</summary>
    public string PaletteHotkey { get; set; } = "Ctrl+Space";

    /// <summary>Tecla que, al apretarse, manda Ctrl+Enter a la ventana activa (para enviar en Kommo). Vacío = desactivado.</summary>
    public string SendHotkey { get; set; } = "";

    /// <summary>Si es true, el auto-envío y los mensajes múltiples se mandan con Ctrl+Enter en vez de Enter (para Kommo).</summary>
    public bool SendWithCtrlEnter { get; set; } = false;
}

/// <summary>Estructura completa del archivo de configuración (snippets.json).</summary>
public sealed class ConfigFile
{
    public AppSettings Settings { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<Snippet> Snippets { get; set; } = new();
}
