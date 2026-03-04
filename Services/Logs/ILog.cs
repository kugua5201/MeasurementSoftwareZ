using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.Logs
{
    public interface ILog
    {
        void Info(string message);

        void Warn(string message);

        void Error(string message);

        void Debug(string message);
    }
}
