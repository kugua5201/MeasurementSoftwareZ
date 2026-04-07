namespace MeasurementSoftware.Services.Licensing
{
    /// <summary>
    /// 软件授权服务接口。
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>
        /// 当前机器码。
        /// </summary>
        string MachineCode { get; }

        /// <summary>
        /// 当前软件是否已注册。
        /// </summary>
        bool IsRegistered { get; }

        /// <summary>
        /// 注册状态变化事件。
        /// </summary>
        event EventHandler<bool>? RegistrationStatusChanged;

        /// <summary>
        /// 刷新当前注册状态。
        /// </summary>
        void RefreshRegistrationStatus();

        /// <summary>
        /// 验证授权码是否与当前机器匹配。
        /// </summary>
        bool ValidateLicenseKey(string licenseKey);

        /// <summary>
        /// 尝试完成注册并持久化授权码。
        /// </summary>
        bool TryRegister(string licenseKey, out string errorMessage);
    }
}
