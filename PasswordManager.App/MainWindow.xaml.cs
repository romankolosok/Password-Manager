using System;
using System.Windows;
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
            PreviewMouseMove += ResetInactivityTimer;
            PreviewKeyDown += ResetInactivityTimer;
        }

        private void ResetInactivityTimer(object sender, EventArgs e)
        {
            var session = _serviceProvider.GetService<ISessionService>();
            if (session?.IsActive() == true)
                session.ResetInactivityTimer();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vaultContentLoaded) return;
            _vaultContentLoaded = true;
            ShowVaultListView();
        }

        /// <summary>Navigates to the vault list view and loads entries.</summary>
        private void ShowVaultListView()
        {
            var viewModel = _serviceProvider.GetRequiredService<VaultListViewModel>();
            var view = _serviceProvider.GetRequiredService<VaultListView>();

            SubscribeToVaultListViewModel(viewModel);

            view.DataContext = viewModel;
            MainContent.Content = view;

            _ = viewModel.LoadEntriesCommand.ExecuteAsync(null);
        }

        /// <summary>Navigates to add (entry=null) or edit (entry) view.</summary>
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

            var coordinator = _serviceProvider.GetService<IAuthCoordinator>();
            if (coordinator != null)
            {
                _wasHiddenForLock = true;
                coordinator.ShowLogin();
                Hide();
            }
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible || !_wasHiddenForLock) return;
            _wasHiddenForLock = false;

            if (MainContent.Content is EntryDetailView entryDetailView && entryDetailView.DataContext is EntryDetailViewModel entryVm)
            {
                entryVm.EntrySaved -= OnEntryDetailSaved;
                entryVm.Cancelled -= OnEntryDetailCancelled;
            }

            UnsubscribeFromVaultListViewModel();
            ShowVaultListView();
        }

        private void OnNavigateToEntryDetail(object? sender, VaultEntry? e)
        {
            ShowEntryDetailView(e);
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