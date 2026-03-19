using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.ViewModels
{
    public partial class RecoveryKeyViewModel : ObservableObject
    {
        private readonly IClipboardService _clipboardService;

        public RecoveryKeyViewModel(IClipboardService clipboardService)
        {
            _clipboardService = clipboardService;
        }

        [ObservableProperty]
        private string _recoveryKey = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayErrorVisible))]
        private string _errorMessage = string.Empty;

        public bool DisplayErrorVisible => !string.IsNullOrWhiteSpace(ErrorMessage);

        public event EventHandler? ContinueRequested;
        public event EventHandler? DownloadRequested;

        [RelayCommand]
        private void Copy()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey))
                return;

            _clipboardService.CopyWithAutoClear(RecoveryKey, secondsToClear: 30);
        }

        [RelayCommand]
        private void Download()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey))
            {
                ErrorMessage = "Recovery key is missing.";
                return;
            }

            ErrorMessage = string.Empty;
            DownloadRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Continue()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey))
            {
                ErrorMessage = "Recovery key is missing.";
                return;
            }

            ErrorMessage = string.Empty;
            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

