using System.ComponentModel;
using System.Reflection;

namespace MeasurementSoftware.Extensions
{
    /// <summary>
    /// 枚举扩展方法
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// 获取枚举的 Description 特性值
        /// </summary>
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null)
                return value.ToString();

            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        /// <summary>
        /// 获取枚举的显示名称（兼容 System.IO.Ports 的枚举）
        /// </summary>
        public static string GetDisplayName(this Enum value)
        {
            return value switch
            {
                // System.IO.Ports.Parity 的中文映射
                System.IO.Ports.Parity parity => parity switch
                {
                    System.IO.Ports.Parity.None => "无校验",
                    System.IO.Ports.Parity.Odd => "奇校验",
                    System.IO.Ports.Parity.Even => "偶校验",
                    System.IO.Ports.Parity.Mark => "标记",
                    System.IO.Ports.Parity.Space => "空格",
                    _ => parity.ToString()
                },
                // System.IO.Ports.StopBits 的中文映射
                System.IO.Ports.StopBits stopBits => stopBits switch
                {
                    System.IO.Ports.StopBits.None => "无",
                    System.IO.Ports.StopBits.One => "1位",
                    System.IO.Ports.StopBits.Two => "2位",
                    System.IO.Ports.StopBits.OnePointFive => "1.5位",
                    _ => stopBits.ToString()
                },
                // 其他枚举使用 Description 特性
                _ => value.GetDescription()
            };
        }
    }
}
