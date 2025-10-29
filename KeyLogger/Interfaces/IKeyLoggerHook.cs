using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogger.Interfaces
{
    public interface IKeyLoggerHook
    {
        void Start();
        void Stop();
    }
}
