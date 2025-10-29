using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ks.EngineServices
{
    public class UploaderService
    {
        private readonly Timer _timer;
        private readonly string _uploadUrl;
        private readonly string _authToken;
        private readonly ILogger logger;

        public UploaderService(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();

            var cfg = ConfigurationManager.CurrentConfig.UpConfig;
            _uploadUrl = cfg.UploadUrl;
            _authToken = cfg.AuthToken;

            _timer = new Timer(cfg.SenderInterval * 60 * 1000);
            _timer.Elapsed += async (s, e) => await UploadBatchAsync();
            _timer.AutoReset = true;
            _timer.Start();

            this.logger.LogInformation("UploaderService started. Timer running.");
        }

        private async Task UploadBatchAsync()
        {
            try
            {
                var connection = DBContexts.CreateConnection();
                if (connection == null)
                {
                    logger.LogError("Failed to create DB connection for uploading batch.");
                    return;
                }

                List<UploadQueue> batch = new List<UploadQueue>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM UploadQueue WHERE IsSent = 0 LIMIT 50";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            batch.Add(new UploadQueue
                            {
                                Id = reader.GetInt32(0),
                                PayloadType = reader.GetString(1),
                                PayloadJson = reader.GetString(2),
                                IsSent = false
                            });
                        }
                    }
                }

                if (!batch.Any())
                {
                    logger.LogInformation("No unsent rows in UploadQueue. Nothing to upload.");
                    return;
                }

                logger.LogInformation($"Uploading batch of {batch.Count} items to {_uploadUrl}");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                    var json = JsonConvert.SerializeObject(batch.Select(x => new { x.PayloadType, x.PayloadJson }));
                    var content = new StringContent(json, Encoding.UTF8, "agent701/json");

                    var response = await client.PostAsync(_uploadUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        logger.LogInformation("Batch uploaded successfully. Updating local DB...");

                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "UPDATE UploadQueue SET IsSent = 1 WHERE Id IN (" +
                                              string.Join(",", batch.Select(x => x.Id)) + ")";
                            cmd.ExecuteNonQuery();
                        }

                        logger.LogInformation("Local DB updated. Batch marked as sent.");
                    }
                    else
                    {
                        logger.LogError($"Failed to upload batch. StatusCode: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"UploaderService error: {ex.Message}");
            }
        }
        public void FlushQueue()
        {
            logger.LogInformation("Flushing UploadQueue synchronously...");
            UploadBatchAsync().Wait();
            logger.LogInformation("Flush complete.");
        }
    }
}
