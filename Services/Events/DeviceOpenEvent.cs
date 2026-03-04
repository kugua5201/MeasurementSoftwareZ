using System;
using System.Collections.Generic;
using System.Text;

namespace MeasurementSoftware.Services.Events
{
    public class DeviceOpenEventArgs : EventArgs
    {
        //触发事件
        public bool Open { get; set; }

        public DeviceOpenEventArgs(bool open)
        {
            Open = open;
        }
    }
}
