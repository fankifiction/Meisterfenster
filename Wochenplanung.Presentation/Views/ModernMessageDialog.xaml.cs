using System.Windows;
using System.Windows.Input;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Views;

public partial class ModernMessageDialog : Window
{
    private readonly MessageBoxButton _buttons;

    public ModernMessageDialog(string message, string caption, MessageBoxButton buttons)
    {
        InitializeComponent();
        _buttons = buttons;
        TitleText.Text = caption;
        MessageText.Text = message;

        switch (buttons)
        {
            case MessageBoxButton.OK:
                OkButton.Content = UiText.MsgOk;
                break;
            case MessageBoxButton.OKCancel:
                CancelButton.Visibility = Visibility.Visible;
                OkButton.Content = UiText.MsgOk;
                break;
            case MessageBoxButton.YesNo:
                OkButton.Content = UiText.MsgYes;
                NoButton.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNoCancel:
                OkButton.Content = UiText.MsgYes;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                break;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        OkButton.Focus();
        Keyboard.Focus(OkButton);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Ok_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_buttons == MessageBoxButton.YesNo)
                No_Click(this, new RoutedEventArgs());
            else if (_buttons == MessageBoxButton.OK)
                Ok_Click(this, new RoutedEventArgs());
            else
                Cancel_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = null;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = null;
        Close();
    }
}
