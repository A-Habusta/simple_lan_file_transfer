using MsBox.Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace simple_lan_file_transfer.ViewModels;

public class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Show a popup with the specified message, title, buttons, icon and window startup location. The popup is shown
    /// on the UI thread.
    /// </summary>
    /// <param name="message">Message inside the messagebox</param>
    /// <param name="title">Title of the messagebox</param>
    /// <param name="buttonEnum">Buttons shown on the messagebox</param>
    /// <param name="icon">Icon show on the messagebox</param>
    /// <param name="windowStartupLocation">Where on the screen the windows will be shown</param>
    /// <returns></returns>
    protected static async Task<ButtonResult> ShowPopup(
        string message,
        string title = "Error",
        ButtonEnum buttonEnum = ButtonEnum.Ok,
        Icon icon = Icon.Error,
        WindowStartupLocation windowStartupLocation = WindowStartupLocation.CenterScreen)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var popup = MessageBoxManager.GetMessageBoxStandard(title, message, buttonEnum,
                icon, windowStartupLocation);
            return await popup.ShowAsync();
        });
    }
}