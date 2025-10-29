using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class UploadQueue
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string PayloadType { get; set; }
        public string PayloadJson { get; set; }
        public bool IsSent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


}
