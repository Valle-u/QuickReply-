using System.Windows;
using System.Windows.Controls;
using QuickReply.Models;
using QuickReply.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace QuickReply.Views;

public partial class PaletteWindow : Window
{
    private readonly List<Snippet> _all;

    /// <summary>Se dispara cuando el usuario elige un texto (Enter o doble clic).</summary>
    public event Action<Snippet>? Picked;

    public PaletteWindow(SnippetStore store)
    {
        InitializeComponent();
        _all = store.Snippets.ToList();

        // Posición: centrada arriba (estilo Spotlight).
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height * 0.16;

        Loaded += (_, _) =>
        {
            ApplyFilter("");
            SearchBox.Focus();
        };
    }

    private void SearchBox_TextChanged(object sender, RoutedEventArgs e) => ApplyFilter(SearchBox.Text);

    private void ApplyFilter(string query)
    {
        IEnumerable<Snippet> result = _all;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            result = _all.Where(s =>
            {
                var hay = $"{s.Title} {s.Category} {s.Trigger} {s.Content}";
                return terms.All(t => hay.Contains(t, StringComparison.OrdinalIgnoreCase));
            });
        }

        List.ItemsSource = result.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase).ToList();
        if (List.Items.Count > 0)
            List.SelectedIndex = 0;
    }

    private void Move(int delta)
    {
        if (List.Items.Count == 0) return;
        int i = List.SelectedIndex + delta;
        i = Math.Max(0, Math.Min(List.Items.Count - 1, i));
        List.SelectedIndex = i;
        List.ScrollIntoView(List.SelectedItem);
    }

    private void PickSelected()
    {
        if (List.SelectedItem is Snippet s)
        {
            Hide(); // ocultar ya, por si sigue un diálogo de variables
            Picked?.Invoke(s);
            Close();
        }
    }

    private void List_MouseDoubleClick(object sender, RoutedEventArgs e) => PickSelected();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Move(+1); e.Handled = true; break;
            case Key.Up: Move(-1); e.Handled = true; break;
            case Key.Enter: PickSelected(); e.Handled = true; break;
            case Key.Escape: Close(); e.Handled = true; break;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Close();
}
