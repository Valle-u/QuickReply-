using System.Collections.ObjectModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using QuickReply.Models;

namespace QuickReply.Services;

/// <summary>
/// Carga y guarda los textos desde el archivo snippets.json, y resuelve qué texto
/// corresponde a una combinación de teclas. La lista es observable: la interfaz se
/// actualiza sola y el motor lee siempre la versión vigente.
/// </summary>
public sealed class SnippetStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Se dispara cuando se cargan/guardan/importan textos (para refrescar estado derivado).</summary>
    public event Action? Changed;

    public string Path { get; }
    public AppSettings Settings { get; private set; } = new();
    public ObservableCollection<Snippet> Snippets { get; } = new();

    /// <summary>True si algún texto tiene abreviatura (para no procesar cada tecla si no hace falta).</summary>
    public bool HasAbbreviations => Snippets.Any(s => !string.IsNullOrEmpty(s.Abbreviation));

    /// <summary>Categorías disponibles (persisten aunque ningún texto las use). Ordenadas.</summary>
    public ObservableCollection<string> Categories { get; } = new();

    public SnippetStore(string path) => Path = path;

    public void Load()
    {
        if (!File.Exists(Path))
            CreateDefault();

        var json = File.ReadAllText(Path);
        var cfg = JsonSerializer.Deserialize<ConfigFile>(json, JsonOpts)
                  ?? throw new InvalidDataException("El archivo de configuración está vacío o malformado.");

        ApplyConfig(cfg);
    }

    private void ApplyConfig(ConfigFile cfg)
    {
        Settings = cfg.Settings ?? new AppSettings();

        Snippets.Clear();
        foreach (var s in cfg.Snippets)
        {
            s.Hotkey = Hotkey.Parse(s.Trigger);
            Snippets.Add(s);
        }

        // Categorías: las guardadas + las usadas por textos + "General", ordenadas y sin duplicar.
        Categories.Clear();
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "General" };
        foreach (var c in cfg.Categories ?? new())
            if (!string.IsNullOrWhiteSpace(c)) set.Add(c.Trim());
        foreach (var s in Snippets)
            if (!string.IsNullOrWhiteSpace(s.Category)) set.Add(s.Category.Trim());
        foreach (var c in set) Categories.Add(c);

        // Normalizar la categoría de cada texto a la forma canónica de la lista
        // (para que el selector matchee aunque difieran mayúsculas/espacios).
        foreach (var s in Snippets)
        {
            var canon = Categories.FirstOrDefault(c => string.Equals(c, s.Category, StringComparison.OrdinalIgnoreCase));
            if (canon is not null && canon != s.Category)
                s.Category = canon;
        }

        Changed?.Invoke();
    }

    public void Save()
    {
        // Asegurar que toda categoría usada por un texto esté en la lista.
        foreach (var s in Snippets)
            EnsureCategory(s.Category);

        BackupCurrent();

        var cfg = new ConfigFile
        {
            Settings = Settings,
            Categories = Categories.ToList(),
            Snippets = Snippets.ToList(),
        };
        File.WriteAllText(Path, JsonSerializer.Serialize(cfg, JsonOpts));
        Changed?.Invoke();
    }

    /// <summary>Copia el archivo actual a /backups antes de sobrescribirlo. Conserva los últimos 20.</summary>
    private void BackupCurrent()
    {
        try
        {
            if (!File.Exists(Path)) return;

            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "backups");
            Directory.CreateDirectory(dir);

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var dest = System.IO.Path.Combine(dir, $"snippets-{stamp}.json");
            if (!File.Exists(dest))
                File.Copy(Path, dest);

            // Conservar solo los 20 más nuevos (nombre = orden cronológico).
            foreach (var old in Directory.GetFiles(dir, "snippets-*.json")
                                          .OrderByDescending(f => f)
                                          .Skip(20))
            {
                try { File.Delete(old); } catch { /* ignorar */ }
            }
        }
        catch { /* un backup fallido nunca debe impedir guardar */ }
    }

    public void ExportTo(string path)
    {
        var cfg = new ConfigFile { Settings = Settings, Categories = Categories.ToList(), Snippets = Snippets.ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(cfg, JsonOpts));
    }

    public void ImportFrom(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<ConfigFile>(json, JsonOpts)
                  ?? throw new InvalidDataException("El archivo a importar está vacío o malformado.");
        ApplyConfig(cfg);
        Save();
    }

    public Snippet? Match(int vk, ModMask mods)
        => Snippets.FirstOrDefault(s => s.Hotkey is { } h && h.Vk == vk && h.Modifiers == mods);

    public Snippet? FindConflict(Hotkey hk, Snippet? except)
        => Snippets.FirstOrDefault(s => !ReferenceEquals(s, except)
                                        && s.Hotkey is { } h && h.Vk == hk.Vk && h.Modifiers == hk.Modifiers);

    /// <summary>Busca el texto cuya abreviatura sea el final de lo tipeado (la más larga gana).</summary>
    public (Snippet snippet, int length)? MatchAbbreviation(string buffer)
    {
        Snippet? best = null;
        int bestLen = 0;
        foreach (var s in Snippets)
        {
            var a = s.Abbreviation;
            if (string.IsNullOrEmpty(a) || a.Length > buffer.Length) continue;
            if (buffer.EndsWith(a, StringComparison.OrdinalIgnoreCase) && a.Length > bestLen)
            {
                best = s;
                bestLen = a.Length;
            }
        }
        return best is null ? null : (best, bestLen);
    }

    /// <summary>Busca otro texto que use la misma abreviatura (para avisar conflictos).</summary>
    public Snippet? FindAbbreviationConflict(string abbreviation, Snippet? except)
        => string.IsNullOrWhiteSpace(abbreviation)
            ? null
            : Snippets.FirstOrDefault(s => !ReferenceEquals(s, except)
                                           && string.Equals(s.Abbreviation, abbreviation, StringComparison.OrdinalIgnoreCase));

    /// <summary>Agrega una categoría (si no existe) en orden alfabético y guarda. Devuelve el nombre normalizado.</summary>
    public string AddCategory(string name)
    {
        var added = EnsureCategory(name);
        if (added is not null)
            Save();
        return (name ?? "").Trim();
    }

    /// <summary>Cantidad de textos que usan una categoría.</summary>
    public int CountUsage(string category)
        => Snippets.Count(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Elimina una categoría. Los textos que la usaban pasan a "General".
    /// "General" no se puede eliminar. Devuelve cuántos textos se movieron (-1 si no se permite).
    /// </summary>
    public int DeleteCategory(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0 || string.Equals(name, "General", StringComparison.OrdinalIgnoreCase))
            return -1;

        int moved = 0;
        foreach (var s in Snippets)
        {
            if (string.Equals(s.Category, name, StringComparison.OrdinalIgnoreCase))
            {
                s.Category = "General";
                moved++;
            }
        }

        var existing = Categories.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            Categories.Remove(existing);
        EnsureCategory("General");

        Save();
        return moved;
    }

    /// <summary>Inserta la categoría ordenada si falta (sin guardar). Devuelve el nombre si la agregó, o null.</summary>
    private string? EnsureCategory(string? name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return null;
        if (Categories.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
            return null;

        int i = 0;
        while (i < Categories.Count && string.Compare(Categories[i], name, StringComparison.OrdinalIgnoreCase) < 0)
            i++;
        Categories.Insert(i, name);
        return name;
    }

    public int ValidCount => Snippets.Count(s => s.Hotkey is not null);

    private void CreateDefault()
    {
        var cfg = new ConfigFile
        {
            Settings = new AppSettings(),
            Categories = new List<string> { "General", "Pagos" },
            Snippets = new List<Snippet>
            {
                new()
                {
                    Title = "Saludo inicial",
                    Category = "General",
                    Trigger = "Ctrl+Shift+S",
                    Abbreviation = "/hola",
                    AutoSend = false,
                    Content = "¡Hola! 👋 ¿En qué puedo ayudarte hoy?",
                },
                new()
                {
                    Title = "Datos de carga (ejemplo)",
                    Category = "Pagos",
                    Trigger = "F2",
                    AutoSend = false,
                    Content = "Te paso los datos para la carga 💳\n\n"
                              + "Alias: CASINO.EJEMPLO\n"
                              + "Titular: Nombre Apellido\n\n"
                              + "Cuando hagas la transferencia, mandame el comprobante y lo acredito al toque 🙌",
                },
                new()
                {
                    Title = "Despedida",
                    Category = "General",
                    Trigger = "Ctrl+Shift+D",
                    AutoSend = false,
                    Content = "¡Listo! Cualquier cosa estoy por acá. ¡Que tengas buena suerte! 🍀🎰",
                },
            },
        };

        File.WriteAllText(Path, JsonSerializer.Serialize(cfg, JsonOpts));
    }
}
