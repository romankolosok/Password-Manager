using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.Services
{
    internal class AvaloniaClipboardService : IClipboardService
    {
        private readonly DispatcherTimer _clearTimer;
        private string? _lastCopiedText;

        public AvaloniaClipboardService()
        {
            _clearTimer = new DispatcherTimer();
            _clearTimer.Tick += OnClearTimerTick;
        }

        public async void CopyWithAutoClear(string text, int secondsToClear = 15)
        {
            var clipboard = GetClipboard();
            if (clipboard == null) return;

            await clipboard.SetTextAsync(text);
            _lastCopiedText = text;

            _clearTimer.Stop();
            _clearTimer.Interval = TimeSpan.FromSeconds(secondsToClear);
            _clearTimer.Start();
        }

        public async void ClearClipboard()
        {
            _clearTimer.Stop();
            try
            {
                var clipboard = GetClipboard();
                if (clipboard != null)
                    await clipboard.SetTextAsync(string.Empty);
            }
            catch
            {
                // Clipboard might be locked by another app
            }
        }

        private async void OnClearTimerTick(object? sender, EventArgs e)
        {
            _clearTimer.Stop();

            try
            {
                var clipboard = GetClipboard();
                if (clipboard != null)
                {
                    var current = await clipboard.GetTextAsync();
                    if (current == _lastCopiedText)
                        await clipboard.SetTextAsync(string.Empty);
                }
            }
            catch
            {
                // Ignore clipboard errors
            }

            _lastCopiedText = null;
        }

        private static IClipboard? GetClipboard()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.MainWindow ?? (desktop.Windows.Count > 0 ? desktop.Windows[0] : null);
                return window != null ? TopLevel.GetTopLevel(window)?.Clipboard : null;
            }
            return null;
        }
    }
}
