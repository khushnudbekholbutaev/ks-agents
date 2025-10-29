using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class LoggerEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string LogLevel { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
