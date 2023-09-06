using ReactiveUI;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace simple_lan_file_transfer.ViewModels;

public class ViewModelBase : ReactiveObject
{
    // No need to use dispatcher, the internals of messagebox already do that
    protected static async Task ShowPopup(string message)
    {
        var messageBox = MessageBoxManager.GetMessageBoxStandard(
            title: "Error",
            text: message,
            ButtonEnum.OkCancel);
        await ShowPopup(messageBox);
    }

    protected static async Task<ButtonResult> ShowPopup(IMsBox<ButtonResult> messageBox)
    {
        return await messageBox.ShowAsync();
    }
}