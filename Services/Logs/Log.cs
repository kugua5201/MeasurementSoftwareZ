using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.Logs
{
    public class Log : ILog
    {

        private readonly Logger _log;

        public Log()
        {
            _log = LogManager.GetCurrentClassLogger();
             MultiProtocol.Services.IIndustrialProtocol.IndustrialProtocol.LoggerFactory= _log;
        }

        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(string message)
        {
            _log.Error(message);
        }

        public void Debug(string message)
        {
            _log.Debug(message);
        }
    }
}
