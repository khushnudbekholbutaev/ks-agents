using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class KeyEvents
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string ProcessName { get; set; }
        public string ProcessTitle { get; set; }
        public string KeyText { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
