using System.Windows;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
