using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.ViewModels
{
    public partial class VaultListViewModel : ObservableObject
    {
        private readonly IVaultService _vaultService;
        private readonly IAuthService _authService;
        private readonly IClipboardService _clipboardService;
        private readonly ISessionService _sessionService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        private ObservableCollection<VaultEntry> _entries = new();

        [ObservableProperty]
        private List<VaultEntry> _allEntries = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private VaultEntry? _selectedEntry;

        [ObservableProperty]
        private bool _isLoading;

        /// <summary>Logged-in user email for display (Account: email).</summary>
        public string? CurrentUserEmail => _authService.CurrentUserEmail;

        /// <summary>Status line: entry count and optional clipboard message.</summary>
        public string StatusText => Entries.Count == 1
            ? "1 entry"
            : $"{Entries.Count} entries";

        [RelayCommand]
        private async Task LoadEntriesAsync()
        {
            IsLoading = true;
            try
            {
                Result<List<VaultEntry>> result = await _vaultService.GetAllEntriesAsync();
                if (result.Success && result.Value != null)
                {
                    AllEntries = result.Value;
                    Entries = new ObservableCollection<VaultEntry>(AllEntries);
                    OnPropertyChanged(nameof(StatusText));
                }
            }
            finally
            {
                IsLoading = false;
            }
            _sessionService.ResetInactivityTimer();
        }

        [RelayCommand]
        private void AddEntry()
        {
            _sessionService.ResetInactivityTimer();
            NavigateToEntryDetail?.Invoke(this, null);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteEditOrDelete))]
        private void EditEntry(VaultEntry? entry)
        {
            if (entry != null)
            {
                _sessionService.ResetInactivityTimer();
                NavigateToEntryDetail?.Invoke(this, entry);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteEditOrDelete))]
        private async Task DeleteEntryAsync(VaultEntry? entry)
        {
            if (entry == null) return;
            if (MessageBox.Show(
                    $"Delete entry for \"{entry.WebsiteName ?? "Unknown"}\"?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            Result result = await _vaultService.DeleteEntryAsync(entry.Id.ToString());
            if (result.Success)
            {
                Entries.Remove(entry);
                AllEntries.Remove(entry);
                OnPropertyChanged(nameof(StatusText));
            }
            else
            {
                MessageBox.Show(result.Message ?? "Failed to delete.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _sessionService.ResetInactivityTimer();
        }

        private static bool CanExecuteEditOrDelete(VaultEntry? entry) => entry != null;

        [RelayCommand]
        private void CopyPassword(VaultEntry? entry)
        {
            if (entry == null) return;
            _sessionService.ResetInactivityTimer();
            _clipboardService.CopyWithAutoClear(entry.Password ?? "", 15);
        }

        [RelayCommand]
        private void CopyUsername(VaultEntry? entry)
        {
            if (entry == null) return;
            _sessionService.ResetInactivityTimer();
            _clipboardService.CopyWithAutoClear(entry.Username ?? "", 30);
        }

        [RelayCommand]
        private void Lock()
        {
            _authService.Lock();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteSearch))]
        private void Search()
        {
            ExecuteSearch();
        }

        private bool CanExecuteSearch() => true;

        private void ExecuteSearch()
        {
            _sessionService.ResetInactivityTimer();
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                Entries = new ObservableCollection<VaultEntry>(AllEntries);
            }
            else
            {
                List<VaultEntry> filtered = _vaultService.SearchEntries(SearchQuery.Trim(), AllEntries);
                Entries = new ObservableCollection<VaultEntry>(filtered);
            }
            OnPropertyChanged(nameof(StatusText));
        }

        partial void OnSearchQueryChanged(string value)
        {
            ExecuteSearch();
        }

        /// <summary>Raised to navigate to add (null) or edit (entry).</summary>
        public event EventHandler<VaultEntry?>? NavigateToEntryDetail;

        /// <summary>Raised when vault is locked; navigate back to login.</summary>
        public event EventHandler? NavigateToLogin;

        public VaultListViewModel(
            IVaultService vaultService,
            IAuthService authService,
            IClipboardService clipboardService,
            ISessionService sessionService)
        {
            _vaultService = vaultService;
            _authService = authService;
            _clipboardService = clipboardService;
            _sessionService = sessionService;

            _sessionService.VaultLocked += OnVaultLocked;
        }

        private void OnVaultLocked(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                NavigateToLogin?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}
