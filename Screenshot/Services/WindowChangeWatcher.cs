using Common.Helpers;
using Common.Interfaces;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Screenshot.Services
{
    public class WindowChangeWatcher
    {
        public event EventHandler OnWindowChange;
        private string lastWindow = "";
        private readonly ILogger logger;

        public WindowChangeWatcher(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                logger.LogInformation("WindowChangeWatcher started.");
                while (true)
                {
                    var helper = new ForegroundWindowHelper();
                    string current = helper.GetActiveWindowTitle();
                    if (!string.IsNullOrEmpty(current) && current != lastWindow)
                    {
                        lastWindow = current;
                        logger.LogInformation($"Active window changed: {current}");
                        OnWindowChange?.Invoke(this, EventArgs.Empty);
                    }
                    await Task.Delay(1000);
                }
            });
        }
    }
}
