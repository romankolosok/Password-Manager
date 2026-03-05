using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PasswordManager.Core.Services.Interfaces;
using System.Threading.Tasks;

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

        public void CopyWithAutoClear(string text, int secondsToClear = 15)
        {
            // Fire-and-forget with internal async workflow that safely handles exceptions.
            _ = CopyWithAutoClearAsync(text, secondsToClear);
        }

        public void ClearClipboard()
        {
            _clearTimer.Stop();
            // Fire-and-forget; internal method handles its own exceptions.
            _ = ClearClipboardAsync();
        }

        private async Task CopyWithAutoClearAsync(string text, int secondsToClear)
        {
            try
            {
                var clipboard = GetClipboard();
                if (clipboard == null) return;

                await clipboard.SetTextAsync(text);
                _lastCopiedText = text;

                _clearTimer.Stop();
                _clearTimer.Interval = TimeSpan.FromSeconds(secondsToClear);
                _clearTimer.Start();
            }
            catch
            {
                // Ignore clipboard errors (clipboard may be locked or unavailable).
            }
        }

        private async Task ClearClipboardAsync()
        {
            try
            {
                var clipboard = GetClipboard();
                if (clipboard != null)
                    await clipboard.SetTextAsync(string.Empty);
            }
            catch
            {
                // Clipboard might be locked by another app.
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
