using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.StepOperations
{
    /// <summary>
    /// 工步操作监听服务接口。
     /// 负责根据配方启用状态启用或停用点位事件监听，并在点位满足触发条件时抛出统一动作事件。
    /// </summary>
    public interface IStepOperationMonitorService
    {
        /// <summary>
        /// 当点位触发了开始采集、停止采集、上一步、下一步中的某一个动作时触发。
        /// </summary>
        event EventHandler<StepOperationTriggeredEventArgs>? OperationTriggered;

        /// <summary>
        /// 设置当前需要监听的配方。
        /// 服务会自动监听配方启用状态，并按需绑定或解除点位事件监听。
        /// </summary>
        void SetRecipe(MeasurementRecipe? recipe);
    }
}
