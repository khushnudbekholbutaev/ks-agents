using Common.Helpers;
using Common.Interfaces;
using Microsoft.Win32;
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
        public string MachineId { get; set; } = Guid.NewGuid().ToString();

        private readonly ILogger logger;

        public Machines(ILogger logger = null)
        {
            this.logger = logger ?? new Logger();
        }
        //???
        //    try
        //    {
        //        PCName = Environment.MachineName;

        //        if (string.IsNullOrEmpty(MachineId))
        //        {
        //            logger.LogInformation("MachineGuid not found, generating new GUID.");
        //            MachineId = Guid.NewGuid().ToString();
        //        }

        //        DBContexts.Insert(this);
        //        logger.LogInformation($"Machine info inserted into database: {PCName}, {MachineId}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError($"Failed to initialize machine info: {ex.Message}");
        //        MachineId = Guid.NewGuid().ToString();
        //    }
        //}

        //private string GetMachineId()
        //{
        //    try
        //    {
        //        string machineGuid = Guid.NewGuid().ToString();

        //        logger.LogInformation("MachineGuid not found, generating new GUID.");
        //        return machineGuid;
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError($"Error retrieving MachineGuid: {ex.Message}");
        //        return Guid.NewGuid().ToString();
        //    }
        //}

        public UploadQueue UploadQueue()
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
//Registry.GetValue(
//                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
//                    "MachineGuid",
//                    null
//                )?.ToString();

//if (!string.IsNullOrEmpty(machineGuid))
//{
//    logger.LogInformation($"Machine ID (MachineGuid) found: {machineGuid}");
//    return machineGuid;
//}