using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.ViewModels
{
    public partial class EntryDetailViewModel : ObservableObject
    {
        private readonly IVaultService _vaultService;
        private readonly ICryptoService _cryptoService;
        private readonly ISessionService _sessionService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PasswordStrength))]
        [NotifyPropertyChangedFor(nameof(PasswordStrengthLabel))]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        private string _websiteName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PasswordStrength))]
        [NotifyPropertyChangedFor(nameof(PasswordStrengthLabel))]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        private string _url = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        private string _username = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PasswordStrength))]
        [NotifyPropertyChangedFor(nameof(PasswordStrengthLabel))]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        [NotifyPropertyChangedFor(nameof(IsPasswordEmpty))]
        private string _password = string.Empty;

        /// <summary>True when Password is null or empty (for placeholder visibility).</summary>
        public bool IsPasswordEmpty => string.IsNullOrEmpty(Password);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        private string _notes = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        private string _category = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
        private bool _isFavorite;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ErrorMessageVisible))]
        private string _errorMessage = string.Empty;

        /// <summary>True when ErrorMessage is not empty (for visibility binding).</summary>
        public bool ErrorMessageVisible => !string.IsNullOrWhiteSpace(ErrorMessage);

        [ObservableProperty]
        private double _passwordStrength;

        [ObservableProperty]
        private string _passwordStrengthLabel = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private VaultEntry? _currentEntry;

        [ObservableProperty]
        private int _generatorLength = 20;

        [ObservableProperty]
        private bool _generatorIncludeUpper = true;

        [ObservableProperty]
        private bool _generatorIncludeLower = true;

        [ObservableProperty]
        private bool _generatorIncludeDigits = true;

        [ObservableProperty]
        private bool _generatorIncludeSymbols = true;

        [ObservableProperty]
        private bool _isPasswordVisible;

        public event EventHandler? EntrySaved;
        public event EventHandler? Cancelled;

        public EntryDetailViewModel(IVaultService vaultService, ICryptoService cryptoService, ISessionService sessionService)
        {
            _vaultService = vaultService;
            _cryptoService = cryptoService;
            _sessionService = sessionService;
        }

        public void LoadEntry(VaultEntry entry)
        {
            CurrentEntry = entry;
            IsEditing = true;
            IsPasswordVisible = false;
            WebsiteName = entry.WebsiteName ?? string.Empty;
            Url = entry.Url ?? string.Empty;
            Username = entry.Username ?? string.Empty;
            Password = entry.Password ?? string.Empty;
            Notes = entry.Notes ?? string.Empty;
            Category = entry.Category ?? string.Empty;
            IsFavorite = entry.IsFavorite;
            ErrorMessage = string.Empty;
            UpdatePasswordStrength();
        }

        public void NewEntry()
        {
            CurrentEntry = new VaultEntry();
            IsEditing = false;
            IsPasswordVisible = false;
            WebsiteName = string.Empty;
            Url = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            Notes = string.Empty;
            Category = string.Empty;
            IsFavorite = false;
            ErrorMessage = string.Empty;
            UpdatePasswordStrength();
        }

        /// <summary>Returns true if there are unsaved changes (for abandon-changes dialog).</summary>
        public bool HasUnsavedChanges =>
            IsEditing
                ? HasChangesFromLoaded()
                : HasAnyData();

        private bool HasAnyData() =>
            !string.IsNullOrWhiteSpace(WebsiteName) ||
            !string.IsNullOrWhiteSpace(Url) ||
            !string.IsNullOrWhiteSpace(Username) ||
            !string.IsNullOrWhiteSpace(Password) ||
            !string.IsNullOrWhiteSpace(Notes) ||
            !string.IsNullOrWhiteSpace(Category) ||
            IsFavorite;

        private bool HasChangesFromLoaded()
        {
            if (CurrentEntry == null) return HasAnyData();
            return !StringEquals(WebsiteName, CurrentEntry.WebsiteName) ||
                   !StringEquals(Url, CurrentEntry.Url) ||
                   !StringEquals(Username, CurrentEntry.Username) ||
                   !StringEquals(Password, CurrentEntry.Password) ||
                   !StringEquals(Notes, CurrentEntry.Notes) ||
                   !StringEquals(Category, CurrentEntry.Category) ||
                   IsFavorite != CurrentEntry.IsFavorite;
        }

        private static bool StringEquals(string? a, string? b) =>
            (a ?? string.Empty).Trim() == (b ?? string.Empty).Trim();

        [RelayCommand]
        private async Task SaveAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(WebsiteName))
            {
                ErrorMessage = "Site name is required";
                return;
            }

            if (CurrentEntry == null)
                CurrentEntry = new VaultEntry();

            CurrentEntry.WebsiteName = WebsiteName.Trim();
            CurrentEntry.Url = Url?.Trim() ?? string.Empty;
            CurrentEntry.Username = Username?.Trim() ?? string.Empty;
            CurrentEntry.Password = Password ?? string.Empty;
            CurrentEntry.Notes = Notes?.Trim() ?? string.Empty;
            CurrentEntry.Category = Category?.Trim() ?? string.Empty;
            CurrentEntry.IsFavorite = IsFavorite;
            CurrentEntry.UpdatedAt = DateTime.UtcNow;

            Result result = await _vaultService.AddEntryAsync(CurrentEntry);

            if (result.Success)
            {
                _sessionService.ResetInactivityTimer();
                EntrySaved?.Invoke(this, EventArgs.Empty);
            }
            else
                ErrorMessage = result.Message ?? "Failed to save";
        }

        [RelayCommand]
        private void GeneratePassword()
        {
            var options = new PasswordOptions
            {
                Length = GeneratorLength,
                IncludeUppercase = GeneratorIncludeUpper,
                IncludeLowercase = GeneratorIncludeLower,
                IncludeDigits = GeneratorIncludeDigits,
                IncludeSpecialCharacters = GeneratorIncludeSymbols
            };

            Result<string> result = _cryptoService.GeneratePassword(options);
            if (result.Success && result.Value != null)
            {
                Password = result.Value;
                IsPasswordVisible = false;
                UpdatePasswordStrength();
            }
            else
            {
                ErrorMessage = result.Message ?? "Failed to generate password";
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (HasUnsavedChanges && MessageBox.Show(
                    "You have unsaved data. Abandon changes?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        partial void OnPasswordChanged(string value)
        {
            UpdatePasswordStrength();
        }

        private void UpdatePasswordStrength()
        {
            if (string.IsNullOrEmpty(Password))
            {
                PasswordStrength = 0;
                PasswordStrengthLabel = string.Empty;
                return;
            }

            PasswordStrength = _cryptoService.CalcuateEntropy(Password);

            PasswordStrengthLabel = PasswordStrength switch
            {
                < 28 => "Very Weak",
                < 36 => "Weak",
                < 60 => "Fair",
                < 128 => "Strong",
                _ => "Very Strong"
            };
        }
    }
}
