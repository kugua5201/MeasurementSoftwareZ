using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MeasurementSoftware.Converters
{
    public class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        private readonly string _format;
        public CustomDateTimeConverter(string format = "yyyy-MM-dd HH:mm:ss")
        {
            _format = format;
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return default;

            // 先尝试自定义格式
            if (DateTime.TryParseExact(value, _format, null, System.Globalization.DateTimeStyles.None, out var dt))
                return dt;

            // 再尝试标准格式（ISO 8601等）
            if (DateTime.TryParse(value, out dt))
                return dt;

            throw new FormatException($"无法解析时间字符串: {value}");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(_format));
        }
    }
}
