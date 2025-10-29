using Common.Helpers;
using Common.Interfaces; // ILogger uchun
using Newtonsoft.Json;
using SQLite;
using System;
using System.IO;
using System.Management;

namespace Common.Models
{
    public class Machines
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string PCName { get; set; } = Environment.MachineName;
        public string MachineId { get; set; }

        private readonly ILogger logger;

        public Machines(ILogger logger = null)
        {
            this.logger = logger;

            try
            {
                MachineId = GetMachineId();
                logger.LogInformation($"Machine initialized. PCName: {PCName}, MachineId: {MachineId}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to initialize machine info: {ex.Message}");
                MachineId = Guid.NewGuid().ToString();
            }

            var machine = new Machines
            {
                PCName = this.PCName,
                MachineId = this.MachineId
            };

            DBContexts.Insert( machine );
            logger.LogInformation("Machine info inserted into database.");
        }

        private string GetMachineId()
        {
            string id = "";
            try
            {
                logger.LogInformation("Attempting to get ProcessorId via WMI...");
                using (var mos = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject mo in mos.Get())
                    {
                        id = mo["ProcessorId"]?.ToString() ?? "";
                        break;
                    }
                }

                if (string.IsNullOrEmpty(id))
                {
                    logger.LogInformation("ProcessorId not found, generating new GUID.");
                    id = Guid.NewGuid().ToString();
                }

                return id;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error retrieving ProcessorId: {ex.Message}");
                return Guid.NewGuid().ToString();
            }
        }

        public UploadQueue ToUploadQueue()
        {
            try
            {
                var upload = new UploadQueue
                {
                    PayloadType = "MachineInfo",
                    PayloadJson = JsonConvert.SerializeObject(this),
                    IsSent = false,
                };

                logger.LogInformation($"Machine info serialized to UploadQueue. PayloadType: {upload.PayloadType}");
                return upload;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to serialize Machine info: {ex.Message}");
                throw;
            }
        }
    }
}
