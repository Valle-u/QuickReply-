using System.Windows;
using System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace QuickReply.Views;

public partial class VariablesDialog : Window
{
    private readonly Dictionary<string, TextBox> _boxes = new();

    /// <summary>Valores cargados por el usuario (token de variable → valor).</summary>
    public Dictionary<string, string> Values { get; } = new();

    public VariablesDialog(string title, IEnumerable<string> variables)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(title))
            HeaderText.Text = $"Completá los datos de «{title}»";

        TextBox? first = null;
        foreach (var v in variables)
        {
            var label = new TextBlock
            {
                Text = v.ToUpperInvariant(),
                Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 4),
            };
            var box = new TextBox { Height = 36 };
            _boxes[v] = box;
            first ??= box;
            FieldsPanel.Children.Add(label);
            FieldsPanel.Children.Add(box);
        }

        Loaded += (_, _) =>
        {
            first?.Focus();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Values.Clear();
        foreach (var kv in _boxes)
            Values[kv.Key] = kv.Value.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        base.OnKeyDown(e);
    }
}
