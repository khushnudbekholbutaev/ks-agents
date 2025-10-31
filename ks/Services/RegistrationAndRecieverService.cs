using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

public class RegistrationAndReceiver : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger logger;

    private readonly string registerUrl = "https://example13/api/register";
    private readonly string configUrl = "https://example.common/api/config";

    public Dictionary<string, string> ConfigKeys { get; private set; } = new Dictionary<string, string>();

    private readonly Machines _machine;

    public RegistrationAndReceiver(HttpClient httpClient, ILogger logger = null)
    {
        _httpClient = httpClient;
        this.logger = logger ?? new Logger();

        _machine = new Machines(this.logger);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RegisterAgentAsync();
        await base.StartAsync(cancellationToken);
    }

    private async Task RegisterAgentAsync()
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(registerUrl, _machine);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

                ConfigKeys = result ?? new Dictionary<string, string>();

                foreach (var kv in ConfigKeys)
                {
                    var config = new Configurations { Name = kv.Key, Value = kv.Value };
                    DBContexts.InsertOrUpdateConfig(config);
                }

                ConfigurationApplier.Apply(ConfigKeys, logger);
                logger.LogInformation("Agent registered successfully and configurations applied.");
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                logger.LogError($"Failed to register agent. StatusCode: {response.StatusCode}, Response: {content}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Agent registration error: {ex.Message}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var configDict = await LoadConfigFromHttpAsync();
                if (configDict != null && configDict.Count > 0)
                {
                    ConfigurationApplier.Apply(configDict);
                    logger.LogInformation("Configuration successfully refreshed from backend.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in background config refresh: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        }
    }

    private async Task<Dictionary<string, string>> LoadConfigFromHttpAsync()
    {
        var configDict = new Dictionary<string, string>();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, configUrl);
            var token = ConfigurationManager.CurrentConfig.UpConfig.AuthToken;

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var configList = JsonConvert.DeserializeObject<List<Configurations>>(jsonContent);

            if (configList != null && configList.Count > 0)
            {
                configDict = configList.ToDictionary(c => c.Name, c => c.Value);
                foreach (var kv in configDict)
                {
                    var config = new Configurations { Name = kv.Key, Value = kv.Value };
                    DBContexts.InsertOrUpdateConfig(config);
                }

                logger.LogInformation("Configurations updated in DB from HTTP GET.");
            }
            else
            {
                logger.LogInformation("Configuration JSON is empty from backend GET.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to load configuration from backend: {ex.Message}");
        }

        return configDict;
    }
}
