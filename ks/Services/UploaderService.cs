using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ks.EngineServices
{
    public class UploaderService : BackgroundService
    {
        private readonly string _uploadUrl;
        private readonly string _authToken;
        private readonly int _senderInterval;
        private readonly ILogger logger;

        public UploaderService(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();

            var cfg = ConfigurationManager.CurrentConfig.UpConfig;
            //???
            _uploadUrl = "https://offish-charley-preachiest.ngrok-free.dev/api/create";
            _authToken = cfg.AuthToken;
            _senderInterval = cfg.SenderInterval;

            logger.LogInformation($" UploadUrl={_uploadUrl}, Interval={cfg.SenderInterval} minutes, {_authToken} token");
        }
        //?????
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("UploaderService started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UploadBatchAsync();
                }
                catch(Exception ex)
                {
                    logger.LogError($"Error in ExecuteAsync: {ex.Message}");
                }

                logger.LogInformation($"UploaderService sleeping for {_senderInterval} minutes...");
                await Task.Delay(TimeSpan.FromMinutes(_senderInterval), stoppingToken);
            }

            logger.LogInformation("UploaderService stopping...");
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
