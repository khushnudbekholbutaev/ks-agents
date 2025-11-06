using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ks.Services
{
    internal class UploaderService : BackgroundService
    {
        private readonly string _authToken;
        private readonly int _senderInterval;
        private readonly ILogger logger;
        private readonly SocketIOClient.SocketIO _socket;

        public UploaderService(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();

            var cfg = ConfigurationManager.CurrentConfig.UpConfig;

            _authToken = cfg.AuthToken;
            _senderInterval = cfg.SenderInterval;

            logger.LogInformation($"Socket uploader started with interval={_senderInterval} minutes");

            _socket = new SocketIOClient.SocketIO(cfg.UploadUrl, new SocketIOOptions
            {
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                Reconnection = true,
                ReconnectionAttempts = 10,
                ReconnectionDelay = 2000,
                ExtraHeaders = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_authToken}" }
                }
            });

            _socket.OnConnected += (sender, e) =>
            {
                logger.LogInformation("[SOCKET-UPLOADER] Connected to socket server.");
            };

            _socket.OnDisconnected += (sender, reason) =>
            {
                logger.LogInformation($"[SOCKET-UPLOADER] Disconnected: {reason}");
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("UploaderService (Socket mode) started.");

            await _socket.ConnectAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UploadBatchAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error in ExecuteAsync: {ex.Message}");
                }

                logger.LogInformation($"Sleeping for {_senderInterval} minutes...");
                await Task.Delay(TimeSpan.FromMinutes(_senderInterval), stoppingToken);
            }

            logger.LogInformation("UploaderService stopping...");
            await _socket.DisconnectAsync();
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

                logger.LogInformation($"[SOCKET-UPLOADER] Sending batch of {batch.Count} items...");

                foreach (var group in batch.GroupBy(x => x.PayloadType))
                {
                    string type = group.Key;
                    var payloadArray = group.Select(x => JsonConvert.DeserializeObject(x.PayloadJson)).ToList();

                    var payload = new { type, data = payloadArray };

                    await _socket.EmitAsync("upload_batch", payload);
                    logger.LogInformation($"[SOCKET-UPLOADER] → Sent type='{type}' ({group.Count()} items)");

                    using (var cmdUpdate = connection.CreateCommand())
                    {
                        cmdUpdate.CommandText = "UPDATE UploadQueue SET IsSent = 1 WHERE Id IN (" +
                            string.Join(",", group.Select(x => x.Id)) + ")";
                        cmdUpdate.ExecuteNonQuery();
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
