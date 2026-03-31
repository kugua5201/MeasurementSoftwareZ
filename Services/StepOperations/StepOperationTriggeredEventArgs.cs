using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.StepOperations
{
    /// <summary>
    /// 工步操作触发事件参数。
    /// </summary>
    public sealed class StepOperationTriggeredEventArgs : EventArgs
    {
        public StepOperationTriggeredEventArgs(StepOperationType operationType)
        {
            OperationType = operationType;
        }

        /// <summary>
        /// 本次触发的工步操作类型。
        /// </summary>
        public StepOperationType OperationType { get; }
    }
}
