using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;
using PasswordManager.App.Views;
using PasswordManager.Core.Services.Interfaces;
using VaultEntry = PasswordManager.Core.Models.VaultEntry;

namespace PasswordManager.App
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private bool _vaultContentLoaded;
        private bool _wasHiddenForLock;
        private VaultListViewModel? _currentVaultListViewModel;

        public MainWindow(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
            Opened += MainWindow_Opened;
            PointerMoved += ResetInactivityTimer;
            KeyDown += ResetInactivityTimer;
        }

        private void ResetInactivityTimer(object? sender, EventArgs e)
        {
            var session = _serviceProvider.GetService<ISessionService>();
            if (session?.IsActive() == true)
                session.ResetInactivityTimer();
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            if (_vaultContentLoaded) return;
            _vaultContentLoaded = true;
            ShowVaultListView();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty && IsVisible && _wasHiddenForLock)
            {
                _wasHiddenForLock = false;

                if (MainContent.Content is EntryDetailView entryDetailView
                    && entryDetailView.DataContext is EntryDetailViewModel entryVm)
                {
                    entryVm.EntrySaved -= OnEntryDetailSaved;
                    entryVm.Cancelled -= OnEntryDetailCancelled;
                }

                UnsubscribeFromVaultListViewModel();
                ShowVaultListView();
            }
        }

        private void ShowVaultListView()
        {
            var viewModel = _serviceProvider.GetRequiredService<VaultListViewModel>();
            var view = _serviceProvider.GetRequiredService<VaultListView>();

            SubscribeToVaultListViewModel(viewModel);

            view.DataContext = viewModel;
            MainContent.Content = view;

            _ = viewModel.LoadEntriesCommand.ExecuteAsync(null);
        }

        private void ShowEntryDetailView(VaultEntry? entry)
        {
            var viewModel = _serviceProvider.GetRequiredService<EntryDetailViewModel>();
            var view = _serviceProvider.GetRequiredService<EntryDetailView>();

            viewModel.EntrySaved += OnEntryDetailSaved;
            viewModel.Cancelled += OnEntryDetailCancelled;

            if (entry != null)
                viewModel.LoadEntry(entry);
            else
                viewModel.NewEntry();

            view.DataContext = viewModel;
            MainContent.Content = view;
        }

        private void SubscribeToVaultListViewModel(VaultListViewModel vm)
        {
            UnsubscribeFromVaultListViewModel();
            _currentVaultListViewModel = vm;
            vm.NavigateToLogin += OnNavigateToLogin;
            vm.NavigateToEntryDetail += OnNavigateToEntryDetail;
            vm.NavigateToChangePassword += OnNavigateToChangePassword;
        }

        private void UnsubscribeFromVaultListViewModel()
        {
            if (_currentVaultListViewModel != null)
            {
                _currentVaultListViewModel.NavigateToLogin -= OnNavigateToLogin;
                _currentVaultListViewModel.NavigateToEntryDetail -= OnNavigateToEntryDetail;
                _currentVaultListViewModel.NavigateToChangePassword -= OnNavigateToChangePassword;
                _currentVaultListViewModel.Detach();
                _currentVaultListViewModel = null;
            }
        }

        private void OnNavigateToLogin(object? sender, EventArgs e)
        {
            if (sender is VaultListViewModel vm)
            {
                vm.NavigateToLogin -= OnNavigateToLogin;
                vm.NavigateToEntryDetail -= OnNavigateToEntryDetail;
                vm.NavigateToChangePassword -= OnNavigateToChangePassword;
                vm.Detach();
                if (_currentVaultListViewModel == vm)
                    _currentVaultListViewModel = null;
            }

            var coordinator = _serviceProvider.GetService<IAuthCoordinator>();
            if (coordinator != null)
            {
                _wasHiddenForLock = true;
                coordinator.ShowLogin();
                Hide();
            }
        }

        private void OnNavigateToEntryDetail(object? sender, VaultEntry? e)
        {
            ShowEntryDetailView(e);
        }

        private void OnNavigateToChangePassword(object? sender, EventArgs e)
        {
            var coordinator = _serviceProvider.GetService<IAuthCoordinator>();
            coordinator?.ShowChangePassword();
        }

        private void OnEntryDetailSaved(object? sender, EventArgs e)
        {
            if (sender is EntryDetailViewModel vm)
            {
                vm.EntrySaved -= OnEntryDetailSaved;
                vm.Cancelled -= OnEntryDetailCancelled;
            }
            ShowVaultListView();
        }

        private void OnEntryDetailCancelled(object? sender, EventArgs e)
        {
            if (sender is EntryDetailViewModel vm)
            {
                vm.EntrySaved -= OnEntryDetailSaved;
                vm.Cancelled -= OnEntryDetailCancelled;
            }
            ShowVaultListView();
        }
    }
}
