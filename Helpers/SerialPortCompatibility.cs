using System.IO.Ports;
using Microsoft.Win32;

namespace MeasurementSoftware.Helpers
{
    public static class SerialPortCompatibility
    {
        public static bool TryGetPortNames(out string[] portNames, out string error)
        {
            if (!OperatingSystem.IsWindows())
            {
                portNames = new string[0];
                error = "当前运行环境不是 Windows，无法获取串口列表。";
                return false;
            }

#if NET8_0_OR_GREATER
            try
            {
                portNames = NormalizePortNames(SerialPort.GetPortNames());
                error = string.Empty;
                return true;
            }
            catch (PlatformNotSupportedException)
            {
            }
#endif

            if (TryGetPortNamesFromRegistry(out portNames))
            {
                error = string.Empty;
                return true;
            }

            portNames = new string[0];
            error = $"当前目标框架 {GetFrameworkDisplayText()} 未能通过系统接口获取串口列表。";
            return false;
        }

        public static bool TryEnsureSupported(out string error)
        {
            if (OperatingSystem.IsWindows())
            {
                error = string.Empty;
                return true;
            }

            error = "当前运行环境不是 Windows，无法使用串口功能。";
            return false;
        }

        private static bool TryGetPortNamesFromRegistry(out string[] portNames)
        {
            using var serialCommKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (serialCommKey == null)
            {
                portNames = new string[0];
                return false;
            }

            var valueNames = serialCommKey.GetValueNames();
            var ports = new List<string>(valueNames.Length);
            foreach (var valueName in valueNames)
            {
                if (serialCommKey.GetValue(valueName) is string portName && !string.IsNullOrWhiteSpace(portName))
                {
                    ports.Add(portName);
                }
            }

            portNames = NormalizePortNames(ports.ToArray());
            return true;
        }

        private static string[] NormalizePortNames(string[] portNames)
        {
            return portNames
                .Where(static portName => !string.IsNullOrWhiteSpace(portName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static portName => GetPortSortKey(portName))
                .ThenBy(static portName => portName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int GetPortSortKey(string portName)
        {
            if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(portName[3..], out var portNumber))
            {
                return portNumber;
            }

            return int.MaxValue;
        }

        private static string GetFrameworkDisplayText()
        {
#if NET10_0_OR_GREATER
            return "net10.0";
#elif NET9_0_OR_GREATER
            return "net9.0";
#elif NET8_0_OR_GREATER
            return "net8.0";
#elif NET7_0_OR_GREATER
            return "net7.0";
#elif NET6_0_OR_GREATER
            return "net6.0";
#else
            return ".NET";
#endif
        }
    }
}
