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

            viewModel.NavigateToLogin += OnNavigateToLogin;
            viewModel.NavigateToEntryDetail += OnNavigateToEntryDetail;

            view.DataContext = viewModel;
            Content = view;

            _ = viewModel.LoadEntriesCommand.ExecuteAsync(null);
        }

        private void OnNavigateToLogin(object? sender, System.EventArgs e)
        {
            if (sender is VaultListViewModel vm)
                vm.NavigateToLogin -= OnNavigateToLogin;

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
                vm.NavigateToLogin += OnNavigateToLogin;
                _ = vm.LoadEntriesCommand.ExecuteAsync(null);
            }
        }

        private void OnNavigateToEntryDetail(object? sender, VaultEntry? e)
        {
            // TODO: navigate to EntryDetailView (add when null, edit when non-null)
        }
    }
}
