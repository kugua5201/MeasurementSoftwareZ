using Microsoft.Win32;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace MeasurementSoftware.Services.Licensing
{
    /// <summary>
    /// 软件授权服务。
    /// </summary>
    public class LicenseService : ILicenseService
    {
        private const string Salt = "X9#m$Kfssad1231231`3fgsdg@p!2*Lz&5^vQ8~bC1%ytyurtyu()^yW4(jR_";
        private const string RegistryPath = @"SOFTWARE\MeasurementSoftware";
        private const string RegistryKeyName = "LicenseKey";

        private bool _isRegistered;

        public LicenseService()
        {
            MachineCode = GetMachineCode();
            RefreshRegistrationStatus();
        }

        /// <summary>
        /// 当前机器码。
        /// </summary>
        public string MachineCode { get; }

        /// <summary>
        /// 当前软件是否已注册。
        /// </summary>
        public bool IsRegistered
        {
            get => _isRegistered;
            private set
            {
                if (_isRegistered == value)
                {
                    return;
                }

                _isRegistered = value;
                RegistrationStatusChanged?.Invoke(this, value);
            }
        }

        /// <summary>
        /// 注册状态变化事件。
        /// </summary>
        public event EventHandler<bool>? RegistrationStatusChanged;

        /// <summary>
        /// 刷新当前注册状态。
        /// </summary>
        public void RefreshRegistrationStatus()
        {
            string expectedKey = GenerateLicenseKey(MachineCode);
            string? storedKey = ReadLicenseKey(Registry.LocalMachine) ?? ReadLicenseKey(Registry.CurrentUser);
            IsRegistered = string.Equals(storedKey, expectedKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 验证授权码是否与当前机器匹配。
        /// </summary>
        public bool ValidateLicenseKey(string licenseKey)
        {
            return string.Equals(NormalizeLicenseKey(licenseKey), GenerateLicenseKey(MachineCode), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 尝试完成注册并持久化授权码。
        /// </summary>
        public bool TryRegister(string licenseKey, out string errorMessage)
        {
            string normalizedLicenseKey = NormalizeLicenseKey(licenseKey);
            if (string.IsNullOrWhiteSpace(normalizedLicenseKey))
            {
                errorMessage = "请输入授权码。";
                return false;
            }

            if (!ValidateLicenseKey(normalizedLicenseKey))
            {
                errorMessage = "授权码无效，请检查后重试。";
                return false;
            }

            if (!TrySaveLicenseKey(normalizedLicenseKey))
            {
                errorMessage = "保存授权码失败，请尝试以管理员身份运行软件后再注册。";
                return false;
            }

            RefreshRegistrationStatus();
            if (!IsRegistered)
            {
                errorMessage = "授权码已保存，但校验未通过。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string GetMachineCode()
        {
            try
            {
                string cpuId = string.Empty;
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        cpuId = item["ProcessorId"]?.ToString()?.Trim() ?? string.Empty;
                        break;
                    }
                }

                string hardDiskSerialNumber = string.Empty;
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        hardDiskSerialNumber = item["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                        break;
                    }
                }

                string rawValue = cpuId + hardDiskSerialNumber;
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    rawValue = Environment.MachineName;
                }

                using var md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(rawValue));
                string hex = Convert.ToHexString(hash);
                return $"{hex[..5]}-{hex[5..10]}-{hex[10..15]}-{hex[15..20]}";
            }
            catch
            {
                return "UNKNOWN-MACH-CODE-0000";
            }
        }

        private static string GenerateLicenseKey(string machineCode)
        {
            using var md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(machineCode + Salt));
            return Convert.ToHexString(hashBytes).ToUpperInvariant();
        }

        private static string NormalizeLicenseKey(string? licenseKey)
        {
            return (licenseKey ?? string.Empty).Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        }

        private static string? ReadLicenseKey(RegistryKey registryRoot)
        {
            try
            {
                using RegistryKey? subKey = registryRoot.OpenSubKey(RegistryPath);
                return NormalizeLicenseKey(subKey?.GetValue(RegistryKeyName)?.ToString());
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySaveLicenseKey(string licenseKey)
        {
            return TrySaveLicenseKey(Registry.LocalMachine, licenseKey) || TrySaveLicenseKey(Registry.CurrentUser, licenseKey);
        }

        private static bool TrySaveLicenseKey(RegistryKey registryRoot, string licenseKey)
        {
            try
            {
                using RegistryKey subKey = registryRoot.CreateSubKey(RegistryPath);
                subKey.SetValue(RegistryKeyName, licenseKey);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
