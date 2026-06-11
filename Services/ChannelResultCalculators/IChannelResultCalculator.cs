using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{
    public interface IChannelResultCalculator
    {
        ChannelType ChannelType { get; }

        double Calculate(IReadOnlyList<HistoryRecordModel> records, MeasurementChannel channel);
    }
}
