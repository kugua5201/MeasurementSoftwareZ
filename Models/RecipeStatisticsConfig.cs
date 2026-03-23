using MeasurementSoftware.ViewModels;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 配方统计信息。
    /// 用于按配方维度记录首页采集统计数据。
    /// </summary>
    public class RecipeStatisticsConfig : ObservableViewModel
    {
        private int passCount;
        private int failCount;
        private int totalCount;

        /// <summary>
        /// 合格数量。
        /// </summary>
        public int PassCount
        {
            get => passCount;
            set => SetProperty(ref passCount, value);
        }

        /// <summary>
        /// 不合格数量。
        /// </summary>
        public int FailCount
        {
            get => failCount;
            set => SetProperty(ref failCount, value);
        }

        /// <summary>
        /// 总数量。
        /// </summary>
        public int TotalCount
        {
            get => totalCount;
            set => SetProperty(ref totalCount, value);
        }
    }
}
