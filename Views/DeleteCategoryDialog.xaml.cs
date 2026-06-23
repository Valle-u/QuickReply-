using System.Windows;
using QuickReply.Services;

namespace QuickReply.Views;

public partial class DeleteCategoryDialog : Window
{
    private readonly SnippetStore _store;

    public string? Selected => CatBox.SelectedItem as string;

    public DeleteCategoryDialog(SnippetStore store)
    {
        _store = store;
        InitializeComponent();

        var deletable = store.Categories
            .Where(c => !string.Equals(c, "General", StringComparison.OrdinalIgnoreCase))
            .ToList();
        CatBox.ItemsSource = deletable;

        if (deletable.Count > 0)
        {
            CatBox.SelectedIndex = 0;
        }
        else
        {
            InfoText.Text = "No hay categorías para eliminar («General» no se puede borrar).";
            OkButton.IsEnabled = false;
        }
    }

    private void CatBox_SelectionChanged(object sender, RoutedEventArgs e) => UpdateInfo();

    private void UpdateInfo()
    {
        if (Selected is null) { InfoText.Text = ""; return; }
        int n = _store.CountUsage(Selected);
        InfoText.Text = n == 0
            ? "Ningún texto usa esta categoría."
            : $"{n} texto(s) pasarán a «General».";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not null)
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
