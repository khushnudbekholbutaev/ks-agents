using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Screenshot.Interfaces;
using System;
using System.Timers;

namespace Screenshot.Services
{
    public class CaptureWithInterval : ICaptureWithInterval
    {
        private readonly ITakeScreenshot screenshot;
        private readonly int interval;
        private readonly bool onChangedWindow;
        private readonly Timer timer;
        private DateTime lastWindowChange = DateTime.Now;
        private readonly ILogger logger;

        public CaptureWithInterval(ITakeScreenshot screenshot, ILogger logger = null)
        {
            this.screenshot = screenshot;
            this.logger = logger ?? new Logger();

            interval = ConfigurationManager.CurrentConfig.ScrConfig.ScreenshotInterval;
            onChangedWindow = ConfigurationManager.CurrentConfig.ScrConfig.OnChangedWindow;

            timer = new Timer(interval * 1000);
            timer.Elapsed += TimerElapsed;
        }

        public void Capture()
        {
            try
            {
                if (onChangedWindow)
                {
                    WindowChangeWatcher watcher = new WindowChangeWatcher();
                    watcher.OnWindowChange += (s, e) =>
                    {
                        screenshot.TakeScreenshot();
                        lastWindowChange = DateTime.Now;
                        logger.LogInformation("Screenshot taken due to window change.");
                    };
                    watcher.Start();

                    timer.Start();
                    logger.LogInformation("CaptureWithInterval started with window change monitoring.");
                }
                else
                {
                    timer.Start();
                    logger.LogInformation("CaptureWithInterval started with periodic screenshots only.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error starting CaptureWithInterval: {ex.Message}");
            }
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (onChangedWindow)
                {
                    if ((DateTime.Now - lastWindowChange).TotalMilliseconds >= interval)
                    {
                        screenshot.TakeScreenshot();
                        lastWindowChange = DateTime.Now;
                        logger.LogInformation("Screenshot taken due to interval timeout.");
                    }
                }
                else
                {
                    screenshot.TakeScreenshot();
                    logger.LogInformation("Screenshot taken due to interval timeout.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during TimerElapsed in CaptureWithInterval: {ex.Message}");
            }
        }
    }
}
