using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Screenshot.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Screenshot.Services
{
    public class CaptureWithInterval : ICaptureWithInterval, IDisposable
    {
        private readonly ITakeScreenshotAsync screenshot;
        private readonly int interval;
        private readonly bool onChangedWindow;
        private readonly ILogger logger;
        private Timer timer;
        private DateTime lastWindowChange = DateTime.Now;

        public CaptureWithInterval(ITakeScreenshotAsync screenshot, ILogger logger = null)
        {
            this.screenshot = screenshot;
            this.logger = logger ?? new Logger();

            interval = ConfigurationManager.CurrentConfig.ScrConfig.ScreenshotInterval;
            onChangedWindow = ConfigurationManager.CurrentConfig.ScrConfig.OnChangedWindow;
        }

        public async Task CaptureAsync()
        {
            try
            {
                if (onChangedWindow)
                {
                    WindowChangeWatcher watcher = new WindowChangeWatcher();
                    watcher.OnWindowChange += async (s, e) =>
                    {
                        await TakeScreenshotSafeAsync("window change");
                        lastWindowChange = DateTime.Now;
                    };
                    watcher.Start();
                }

                timer = new Timer(async _ =>
                {
                    await TimerElapsedAsync();
                }, null, 0, interval * 1000);

                logger.LogInformation("CaptureWithInterval started.");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error starting CaptureWithInterval: {ex.Message}");
            }
        }

        private async Task TimerElapsedAsync()
        {
            try
            {
                if (onChangedWindow)
                {
                    if ((DateTime.Now - lastWindowChange).TotalMilliseconds >= interval * 1000)
                    {
                        await TakeScreenshotSafeAsync("interval timeout");
                        lastWindowChange = DateTime.Now;
                    }
                }
                else
                {
                    await TakeScreenshotSafeAsync("interval timeout");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during TimerElapsedAsync: {ex.Message}");
            }
        }

        private async Task TakeScreenshotSafeAsync(string reason)
        {
            try
            {
                if (screenshot is ITakeScreenshotAsync asyncScreenshot)
                    await asyncScreenshot.TakeScreenshotAsync();
                else
                    await screenshot.TakeScreenshotAsync();

                logger.LogInformation($"Screenshot taken due to {reason}.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error taking screenshot ({reason}): {ex.Message}");
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
        }
    }
}
