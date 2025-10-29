using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class KeySessions
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string ProcessName { get; set; }
        public string ProcessTitle { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string KeyText { get; set; }
        public int KeyCount { get; set; }
    }
}
