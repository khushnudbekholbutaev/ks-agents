using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class Screenshots
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string PCName { get; set; }
        public string ActiveWindowTitle { get; set; }
        public string ActiveProcessName { get; set; }
        public string FilePath { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
