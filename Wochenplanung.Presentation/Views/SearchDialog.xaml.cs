using System.Windows;
using System.Windows.Input;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Views;

public partial class SearchDialog : Window
{
    public sealed record SearchHit(WorkTask Task, string Display);

    private readonly Func<string, Task<IReadOnlyList<SearchHit>>> _search;

    public WorkTask? SelectedTask { get; private set; }

    public SearchDialog(Func<string, Task<IReadOnlyList<SearchHit>>> search)
    {
        _search = search;
        InitializeComponent();
        Loaded += (_, _) => QueryBox.Focus();
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await RunSearchAsync();

    private async Task RunSearchAsync()
    {
        var hits = await _search(QueryBox.Text ?? string.Empty);
        ResultList.ItemsSource = hits;
        if (hits.Count > 0)
            ResultList.SelectedIndex = 0;
    }

    private void Jump_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is SearchHit hit)
        {
            SelectedTask = hit.Task;
            DialogResult = true;
            Close();
        }
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.FocusedElement == QueryBox)
        {
            e.Handled = true;
            await RunSearchAsync();
            return;
        }

        base.OnKeyDown(e);
    }
}
