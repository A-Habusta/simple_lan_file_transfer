using Avalonia.Controls;
using Avalonia.Interactivity;
using simple_lan_file_transfer.ViewModels;

namespace simple_lan_file_transfer.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public void StartNewTransfer(object? sender, RoutedEventArgs e)
    {
        var ip = TextBoxTargetIp.Text ?? string.Empty;
        var password = TextBoxOutboundPassword.Text ?? string.Empty;


        (DataContext as MainViewModel)?.StartFileSendAsync(ip, password);
    }

    public void ChangeIpAddressFieldText(object? sender, RoutedEventArgs e)
    {
        var castSender = (Button?)sender;
        if (castSender is null) return;

        TextBoxTargetIp.Text = castSender.Content?.ToString();
    }
}