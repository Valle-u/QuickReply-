using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using QuickReply.Models;
using QuickReply.Services;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace QuickReply.Views;

public partial class MainWindow : Window
{
    private readonly SnippetStore _store;
    private readonly ICollectionView _view;

    // El editor trabaja sobre este "borrador". Los cambios solo pasan al texto
    // real (_current) cuando se toca Guardar. Cambiar de selección sin guardar los descarta.
    private readonly Snippet _draft = new();
    private Snippet? _current;

    private string _search = "";
    private bool _capturing;
    private bool _revertingSelection;

    public MainWindow(SnippetStore store)
    {
        _store = store;
        InitializeComponent();
        Icon = BrandIcon.CreateWindowImage();

        _view = CollectionViewSource.GetDefaultView(_store.Snippets);
        // La vista por defecto es compartida y persiste entre aperturas: limpiamos antes de
        // configurar, para no acumular agrupaciones/órdenes duplicados al reabrir la ventana.
        _view.GroupDescriptions.Clear();
        _view.SortDescriptions.Clear();
        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Snippet.Category)));
        _view.SortDescriptions.Add(new SortDescription(nameof(Snippet.Category), ListSortDirection.Ascending));
        _view.SortDescriptions.Add(new SortDescription(nameof(Snippet.Title), ListSortDirection.Ascending));
        _view.Filter = FilterSnippet;
        List.ItemsSource = _view;

        Editor.DataContext = _draft;
        CategoryBox.ItemsSource = _store.Categories; // lista observable: se actualiza sola
        LoadDraft(null);
        UpdateEditorVisibility();
        SetStatus($"{_store.Snippets.Count} textos · {_store.ValidCount} con disparador válido");
    }

    // ---- Borrador ----

    private void LoadDraft(Snippet? s)
    {
        _draft.Title = s?.Title ?? "";
        _draft.Category = s?.Category ?? "General";
        _draft.Trigger = s?.Trigger ?? "";
        _draft.Abbreviation = s?.Abbreviation ?? "";
        _draft.AutoSend = s?.AutoSend ?? false;
        _draft.Content = s?.Content ?? "";
    }

    private bool IsDirty()
    {
        if (_current is null) return false;
        return _draft.Title != _current.Title
               || _draft.Category != _current.Category
               || _draft.Trigger != _current.Trigger
               || _draft.Abbreviation != _current.Abbreviation
               || _draft.AutoSend != _current.AutoSend
               || _draft.Content != _current.Content;
    }

    private void CommitDraft()
    {
        if (_current is null) return;
        _current.Title = _draft.Title;
        _current.Category = _draft.Category;
        _current.Trigger = _draft.Trigger; // recalcula el Hotkey
        _current.Abbreviation = _draft.Abbreviation;
        _current.AutoSend = _draft.AutoSend;
        _current.Content = _draft.Content;
    }

    /// <summary>Pregunta si se pueden descartar los cambios actuales. True = se puede continuar.</summary>
    private bool ConfirmLeave()
    {
        if (_current is null || !IsDirty()) return true;
        var r = MessageBox.Show(
            $"Tenés cambios sin guardar en «{_current.Title}».\n\n¿Descartarlos?",
            "Cambios sin guardar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes;
    }

    // ---- Lista / búsqueda ----

    private bool FilterSnippet(object obj)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        if (obj is not Snippet s) return false;

        var q = _search.Trim();
        const StringComparison c = StringComparison.OrdinalIgnoreCase;
        return (s.Title?.Contains(q, c) ?? false)
               || (s.Category?.Contains(q, c) ?? false)
               || (s.Trigger?.Contains(q, c) ?? false)
               || (s.Abbreviation?.Contains(q, c) ?? false)
               || (s.Content?.Contains(q, c) ?? false);
    }

    private void SearchBox_TextChanged(object sender, RoutedEventArgs e)
    {
        _search = SearchBox.Text;
        _view.Refresh();
    }

    private void List_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_revertingSelection) return;

        var newSel = List.SelectedItem as Snippet;
        if (ReferenceEquals(newSel, _current)) return;

        if (!ConfirmLeave())
        {
            // Volver a seleccionar el texto anterior (sin descartar el borrador).
            _revertingSelection = true;
            List.SelectedItem = _current;
            _revertingSelection = false;
            return;
        }

        _current = newSel;
        LoadDraft(_current);
        UpdateEditorVisibility();
        UpdateConflict();
    }

    private void UpdateEditorVisibility()
    {
        bool has = _current is not null;
        EditorScroll.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("Nueva categoría", "Nombre de la categoría:") { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var name = dlg.Value.Trim();
        if (name.Length == 0) return;

        bool existed = _store.Categories.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        _store.AddCategory(name);
        SetStatus(existed ? $"La categoría «{name}» ya existía" : $"Categoría agregada: {name}");
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DeleteCategoryDialog(_store) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Selected is null) return;

        var name = dlg.Selected;
        int moved = _store.DeleteCategory(name);
        if (moved < 0) { SetStatus("Esa categoría no se puede eliminar"); return; }

        // Si el texto en edición usaba esa categoría, reflejar el cambio en el borrador.
        if (string.Equals(_draft.Category, name, StringComparison.OrdinalIgnoreCase))
            _draft.Category = "General";

        _view.Refresh();
        SetStatus(moved > 0
            ? $"Categoría «{name}» eliminada · {moved} texto(s) movidos a General"
            : $"Categoría «{name}» eliminada");
    }

    // ---- Captura de tecla ----

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing) { StopCapture(null); return; }
        ((App)Application.Current).BeginKeyCapture();
        _capturing = true;
        CaptureLabel.Text = "Apretá una tecla…";
        ConflictText.Visibility = Visibility.Collapsed;
        Keyboard.Focus(this);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { StopCapture(null); return; }
        if (IsModifierKey(key)) return; // esperamos a una tecla "real"

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
        CaptureLabel.Text = "Capturar tecla";
        ((App)Application.Current).EndKeyCapture();

        if (hk is { } h && _current is not null)
        {
            _draft.Trigger = h.ToString();
            UpdateConflict();
            SetStatus($"Disparador asignado: {_draft.Trigger} (acordate de Guardar)");
        }
    }

    private static bool IsModifierKey(Key k) => k is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System or Key.None;

    private void Abbr_TextChanged(object sender, RoutedEventArgs e) => UpdateConflict();

    private void UpdateConflict()
    {
        ConflictText.Visibility = Visibility.Collapsed;
        if (_current is null) return;

        var msgs = new List<string>();

        if (_draft.Hotkey is { } h)
        {
            var other = _store.FindConflict(h, _current);
            if (other is not null)
                msgs.Add($"La tecla ya la usa «{other.Title}».");
        }
        else if (!string.IsNullOrWhiteSpace(_draft.Trigger))
        {
            msgs.Add("Disparador no reconocido.");
        }

        if (!string.IsNullOrWhiteSpace(_draft.Abbreviation))
        {
            var other = _store.FindAbbreviationConflict(_draft.Abbreviation, _current);
            if (other is not null)
                msgs.Add($"La abreviatura ya la usa «{other.Title}».");
        }

        if (msgs.Count > 0)
        {
            ConflictText.Text = string.Join("    ", msgs);
            ConflictText.Visibility = Visibility.Visible;
        }
    }

    // ---- Acciones de la barra ----

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLeave()) return;

        var category = _current?.Category ?? "General";
        _current = null; // evita que el cambio de selección vuelva a preguntar

        var s = new Snippet { Title = "Nuevo texto", Category = category, Content = "" };
        _store.Snippets.Add(s);
        Persist();
        _view.Refresh();

        List.SelectedItem = s;
        List.ScrollIntoView(s);
        TitleBox.Focus();
        TitleBox.SelectAll();
        SetStatus("Texto nuevo creado");
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null && List.SelectedItem is not Snippet) return;
        var source = _current;
        if (source is null) return;
        if (!ConfirmLeave()) return;

        _current = null;
        var copy = new Snippet
        {
            Title = source.Title + " (copia)",
            Category = source.Category,
            Trigger = "", // sin disparador para evitar conflicto
            AutoSend = source.AutoSend,
            Content = source.Content,
        };
        _store.Snippets.Add(copy);
        Persist();
        _view.Refresh();

        List.SelectedItem = copy;
        List.ScrollIntoView(copy);
        SetStatus("Texto duplicado");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var r = MessageBox.Show($"¿Eliminar «{_current.Title}»?", "QuickReply",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        var toRemove = _current;
        _current = null; // descartar borrador y evitar preguntas al cambiar selección
        _store.Snippets.Remove(toRemove);
        Persist();
        SetStatus("Texto eliminado");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        CommitDraft();
        if (Persist())
        {
            _view.Refresh();
            UpdateConflict();
            SetStatus("Cambios guardados");
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.IsChecked = false;
        if (!ConfirmLeave()) return;

        var dlg = new OpenFileDialog { Filter = "QuickReply (*.json)|*.json|Todos|*.*" };
        if (dlg.ShowDialog(this) != true) return;

        var r = MessageBox.Show("Esto reemplaza TODOS tus textos actuales por los del archivo. ¿Seguir?",
            "Importar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        try
        {
            _current = null;
            _store.ImportFrom(dlg.FileName);
            _view.Refresh();
            LoadDraft(null);
            UpdateEditorVisibility();
            SetStatus("Importado");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo importar:\n\n{ex.Message}", "QuickReply",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.IsChecked = false;
        var dlg = new SaveFileDialog
        {
            Filter = "QuickReply (*.json)|*.json",
            FileName = "quickreply-textos.json",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            _store.ExportTo(dlg.FileName);
            SetStatus("Exportado");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar:\n\n{ex.Message}", "QuickReply",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Options_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.IsChecked = false;
        ((App)Application.Current).ShowOptions();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.IsChecked = false;
        try
        {
            var folder = Path.GetDirectoryName(_store.Path)!;
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus($"No pude abrir la carpeta: {ex.Message}");
        }
    }

    // ---- Guardado / cierre ----

    private bool Persist()
    {
        try
        {
            _store.Save();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo guardar:\n\n{ex.Message}", "QuickReply",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_capturing) ((App)Application.Current).EndKeyCapture(); // no dejar el hook pausado

        if (IsDirty())
        {
            var r = MessageBox.Show(
                "Tenés cambios sin guardar.\n\n¿Guardarlos antes de cerrar?",
                "QuickReply", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (r == MessageBoxResult.Yes) CommitDraft();
        }

        Persist();
        // La ventana se cierra de verdad para liberar memoria; la app sigue en la bandeja.
        base.OnClosing(e);
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
