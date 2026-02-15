using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;
using PasswordManager.App.Views;
using VaultEntry = PasswordManager.Core.Models.VaultEntry;

namespace PasswordManager.App
{
    public partial class MainWindow : Window
    {
        private bool _vaultContentLoaded;
        private bool _wasHiddenForLock;
        private VaultListViewModel? _currentVaultListViewModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vaultContentLoaded) return;
            _vaultContentLoaded = true;

            var app = Application.Current as App;
            if (app?.ServiceProvider == null) return;

            var view = app.ServiceProvider.GetRequiredService<VaultListView>();
            var viewModel = app.ServiceProvider.GetRequiredService<VaultListViewModel>();

            SubscribeToVaultListViewModel(viewModel);

            view.DataContext = viewModel;
            Content = view;

            _ = viewModel.LoadEntriesCommand.ExecuteAsync(null);
        }

        private void SubscribeToVaultListViewModel(VaultListViewModel vm)
        {
            UnsubscribeFromVaultListViewModel();
            _currentVaultListViewModel = vm;
            vm.NavigateToLogin += OnNavigateToLogin;
            vm.NavigateToEntryDetail += OnNavigateToEntryDetail;
        }

        private void UnsubscribeFromVaultListViewModel()
        {
            if (_currentVaultListViewModel != null)
            {
                _currentVaultListViewModel.NavigateToLogin -= OnNavigateToLogin;
                _currentVaultListViewModel.NavigateToEntryDetail -= OnNavigateToEntryDetail;
                _currentVaultListViewModel.Detach();
                _currentVaultListViewModel = null;
            }
        }

        private void OnNavigateToLogin(object? sender, System.EventArgs e)
        {
            if (sender is VaultListViewModel vm)
            {
                vm.NavigateToLogin -= OnNavigateToLogin;
                vm.NavigateToEntryDetail -= OnNavigateToEntryDetail;
                vm.Detach();
                if (_currentVaultListViewModel == vm)
                    _currentVaultListViewModel = null;
            }

            var app = Application.Current as App;
            var coordinator = app?.ServiceProvider?.GetService<IAuthCoordinator>();
            if (coordinator != null)
            {
                _wasHiddenForLock = true;
                coordinator.ShowLogin();
                Hide();
            }
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible && _wasHiddenForLock && Content is Views.VaultListView vaultView && vaultView.DataContext is VaultListViewModel vm)
            {
                _wasHiddenForLock = false;
                vm.Attach();
                SubscribeToVaultListViewModel(vm);
                _ = vm.LoadEntriesCommand.ExecuteAsync(null);
            }
        }

        private void OnNavigateToEntryDetail(object? sender, VaultEntry? e)
        {
            var app = Application.Current as App;
            if (app?.ServiceProvider == null) return;

            var view = app.ServiceProvider.GetRequiredService<Views.EntryDetailView>();
            var viewModel = app.ServiceProvider.GetRequiredService<EntryDetailViewModel>();

            viewModel.EntrySaved += OnEntryDetailSaved;
            viewModel.Cancelled += OnEntryDetailCancelled;

            if (e != null)
                viewModel.LoadEntry(e);
            else
                viewModel.NewEntry();

            view.DataContext = viewModel;
            Content = view;
        }

        private void OnEntryDetailSaved(object? sender, EventArgs e)
        {
            NavigateBackFromEntryDetail(sender as EntryDetailViewModel);
        }

        private void OnEntryDetailCancelled(object? sender, EventArgs e)
        {
            NavigateBackFromEntryDetail(sender as EntryDetailViewModel);
        }

        private void NavigateBackFromEntryDetail(EntryDetailViewModel? vm)
        {
            if (vm != null)
            {
                vm.EntrySaved -= OnEntryDetailSaved;
                vm.Cancelled -= OnEntryDetailCancelled;
            }

            var app = Application.Current as App;
            if (app?.ServiceProvider == null) return;

            var view = app.ServiceProvider.GetRequiredService<Views.VaultListView>();
            var viewModel = app.ServiceProvider.GetRequiredService<VaultListViewModel>();

            SubscribeToVaultListViewModel(viewModel);

            view.DataContext = viewModel;
            Content = view;

            _ = viewModel.LoadEntriesCommand.ExecuteAsync(null);
        }
    }
}