using ReactiveUI;
using MsBox.Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia.Enums;

namespace simple_lan_file_transfer.ViewModels;

public class ViewModelBase : ReactiveObject
{
    // We need to use Dispatcher.UIThread.InvokeAsync to show the popup
    protected static async Task<ButtonResult> ShowPopup(
        string message,
        string title = "Error",
        ButtonEnum buttonEnum = ButtonEnum.Ok,
        Icon icon = Icon.Error,
        WindowStartupLocation windowStartupLocation = WindowStartupLocation.CenterScreen
        )
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var popup = MessageBoxManager.GetMessageBoxStandard(title, message, buttonEnum,
                icon, windowStartupLocation);
            return await popup.ShowAsync();
        });
    }
}