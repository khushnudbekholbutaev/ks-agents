using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Screenshot.Interfaces
{
    public interface ICaptureWithInterval
    {
        Task CaptureAsync();
    }
}
