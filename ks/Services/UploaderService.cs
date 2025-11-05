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
            _uploadUrl = "https://offish-charley-preachiest.ngrok-free.dev/api/create";
            _authToken = cfg.AuthToken;

            logger.LogInformation($" UploadUrl={_uploadUrl}, Interval={cfg.SenderInterval} minutes, {_authToken} token");

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

                    foreach (var group in batch.GroupBy(x => x.PayloadType))
                    {
                        string type = group.Key;
                        var payloadArray = group.Select(x => JsonConvert.DeserializeObject(x.PayloadJson)).ToList();

                        string jsonBody = JsonConvert.SerializeObject(payloadArray);

                        string urlWithParam = $"{_uploadUrl}?type={Uri.EscapeDataString(type)}";

                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(urlWithParam, content);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var cmdUpdate = connection.CreateCommand())
                            {
                                cmdUpdate.CommandText = "UPDATE UploadQueue SET IsSent = 1 WHERE Id IN (" +
                                    string.Join(",", group.Select(x => x.Id)) + ")";
                                cmdUpdate.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            logger.LogError($"Failed to upload batch with PayloadType={type}. StatusCode={response.StatusCode}");
                        }
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
