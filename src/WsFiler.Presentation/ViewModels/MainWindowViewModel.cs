using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Files;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string statusMessage = "Ready";

    public FilePaneViewModel LeftPane { get; } = new() { IsActive = true };

    public FilePaneViewModel RightPane { get; } = new();

    public MainWindowViewModel(IFileSystemProvider? fileSystemProvider = null)
    {
        fileSystemProvider ??= new EmptyFileSystemProvider();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LoadInitialPanes(fileSystemProvider, home);
    }

    private async void LoadInitialPanes(IFileSystemProvider fileSystemProvider, string home)
    {
        try
        {
            var items = await fileSystemProvider.ListDirectoryAsync(home);
            LeftPane.Load(home, items);
            RightPane.Load(home, items);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            LeftPane.Load(home, []);
            RightPane.Load(home, []);
        }
    }

    private sealed class EmptyFileSystemProvider : IFileSystemProvider
    {
        public Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FileSystemItem>>([]);
        }
    }
}
