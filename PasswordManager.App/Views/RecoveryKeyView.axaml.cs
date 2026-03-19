using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PasswordManager.App.ViewModels;
using System.Text;

namespace PasswordManager.App.Views
{
    public partial class RecoveryKeyView : Window
    {
        private RecoveryKeyViewModel? _subscribedVm;

        public RecoveryKeyView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.ContinueRequested -= OnContinueRequested;
                _subscribedVm.DownloadRequested -= OnDownloadRequested;
            }

            _subscribedVm = DataContext as RecoveryKeyViewModel;
            if (_subscribedVm != null)
            {
                _subscribedVm.ContinueRequested += OnContinueRequested;
                _subscribedVm.DownloadRequested += OnDownloadRequested;
            }
        }

        private void OnContinueRequested(object? sender, System.EventArgs e) => Close(true);

        private async void OnDownloadRequested(object? sender, System.EventArgs e)
        {
            if (_subscribedVm == null)
                return;

            try
            {
                var storageProvider = StorageProvider;
                if (storageProvider == null)
                {
                    _subscribedVm.ErrorMessage = "Download is not available on this platform.";
                    return;
                }

                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save recovery key",
                    SuggestedFileName = "crypty-recovery-key.txt",
                    DefaultExtension = "txt",
                    ShowOverwritePrompt = true
                });

                if (file == null)
                    return;

                await using var stream = await file.OpenWriteAsync();
                var bytes = Encoding.UTF8.GetBytes(_subscribedVm.RecoveryKey + "\n");
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch
            {
                _subscribedVm.ErrorMessage = "Failed to save the file. Please try again.";
            }
        }
    }
}

