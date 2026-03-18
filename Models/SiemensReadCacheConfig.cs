using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 西门子双缓冲读取配置（1200/1500专用）
    /// 工作流程：交替检查 Cache1/Cache2 的可读标志 → 读取整块DB → 按字段定义解析 → 复位可读标志
    /// </summary>
    public partial class SiemensReadCacheConfig : ObservableViewModel
    {
        /// <summary>
        /// 是否启用缓存读取机制
        /// </summary>
        [ObservableProperty]
        private bool isEnabled;

        /// <summary>
        /// 缓存1配置
        /// </summary>
        [ObservableProperty]
        private SiemensReadCacheItemConfig cache1 = new();

        /// <summary>
        /// 缓存2配置
        /// </summary>
        [ObservableProperty]
        private SiemensReadCacheItemConfig cache2 = new();

        /// <summary>
        /// 结构定义文本（用户输入）
        /// 格式：字段名:数据类型[:字节序][:偏移]，每行一个字段；未指定偏移时自动按顺序计算
        /// </summary>
        [ObservableProperty]
        private string structureDefinitionText = "时间戳:Double:DCBA\n测量值1:Float:DCBA\n测量值2:Float:DCBA";

        /// <summary>
        /// 数据组数（一个缓存块中包含多少组重复结构）
        /// </summary>
        [ObservableProperty]
        private int groupCount = 1;

        /// <summary>
        /// 单组结构大小（字节），验证后自动计算
        /// </summary>
        [ObservableProperty]
        private int groupSize;

        /// <summary>
        /// 结构是否已验证通过
        /// </summary>
        [ObservableProperty]
        private bool isStructureValid;

        /// <summary>
        /// 验证结果消息
        /// </summary>
        [ObservableProperty]
        private string structureValidationMessage = "未验证";

        /// <summary>
        /// 已验证的字段定义列表（从文本解析生成，两个缓存共用同一结构，仅模板）
        /// </summary>
        public ObservableCollection<CacheFieldDefinition> FieldDefinitions { get; set; } = [];

        /// <summary>
        /// 展开后的字段列表（缓存区 × 组数 × 字段数），用于 Grid 显示和缓存值更新
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<CacheFieldDefinition> ExpandedFieldDefinitions { get; set; } = [];

        /// <summary>
        /// 验证结构定义命令
        /// </summary>
        [RelayCommand]
        private void ValidateStructure()
        {
            ValidateAndApplyStructure();
        }

        /// <summary>
        /// 解析结构定义文本，验证并生成字段定义列表
        /// </summary>
        public (bool Success, string Message) ValidateAndApplyStructure()
        {
            var lines = StructureDefinitionText?
                .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("//") && !l.StartsWith("#"))
                .ToList() ?? [];

            if (lines.Count == 0)
            {
                IsStructureValid = false;
                StructureValidationMessage = "❌ 结构定义为空";
                return (false, StructureValidationMessage);
            }

            var newFields = new List<CacheFieldDefinition>();
            ushort currentOffset = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Split(':');
                if (parts.Length < 2)
                {
                    IsStructureValid = false;
                    StructureValidationMessage = $"❌ 第{i + 1}行格式错误：'{lines[i]}'，需要 字段名:数据类型[:字节序][:偏移]";
                    return (false, StructureValidationMessage);
                }

                var fieldName = parts[0].Trim();
                if (string.IsNullOrEmpty(fieldName))
                {
                    IsStructureValid = false;
                    StructureValidationMessage = $"❌ 第{i + 1}行字段名为空";
                    return (false, StructureValidationMessage);
                }

                if (!Enum.TryParse<FieldType>(parts[1].Trim(), true, out var dataType))
                {
                    IsStructureValid = false;
                    StructureValidationMessage = $"❌ 第{i + 1}行数据类型无效：'{parts[1].Trim()}'，可选：{string.Join(", ", Enum.GetNames<FieldType>())}";
                    return (false, StructureValidationMessage);
                }

                var byteOrder = ByteOrder.DCBA;
                ushort? explicitOffset = null;

                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    var thirdPart = parts[2].Trim();
                    if (Enum.TryParse<ByteOrder>(thirdPart, true, out var parsedByteOrder))
                    {
                        byteOrder = parsedByteOrder;
                    }
                    else if (ushort.TryParse(thirdPart, out var parsedOffset))
                    {
                        explicitOffset = parsedOffset;
                    }
                    else
                    {
                        IsStructureValid = false;
                        StructureValidationMessage = $"❌ 第{i + 1}行第三段无效：'{thirdPart}'，应为字节序(ABCD/BADC/CDAB/DCBA)或偏移";
                        return (false, StructureValidationMessage);
                    }
                }

                if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]))
                {
                    if (!ushort.TryParse(parts[3].Trim(), out var parsedOffset))
                    {
                        IsStructureValid = false;
                        StructureValidationMessage = $"❌ 第{i + 1}行偏移无效：'{parts[3].Trim()}'，应为非负整数";
                        return (false, StructureValidationMessage);
                    }

                    explicitOffset = parsedOffset;
                }

                ushort actualOffset = explicitOffset ?? currentOffset;

                newFields.Add(new CacheFieldDefinition
                {
                    FieldName = fieldName,
                    Offset = actualOffset,
                    DataType = dataType,
                    ByteOrder = byteOrder
                });

                int fieldEnd = actualOffset + GetFieldTypeSize(dataType);
                if (fieldEnd > ushort.MaxValue)
                {
                    IsStructureValid = false;
                    StructureValidationMessage = $"❌ 第{i + 1}行字段超出支持范围";
                    return (false, StructureValidationMessage);
                }

                currentOffset = (ushort)Math.Max(currentOffset, fieldEnd);
            }

            var duplicates = newFields.GroupBy(f => f.FieldName).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
            {
                IsStructureValid = false;
                StructureValidationMessage = $"❌ 存在重复字段名：{string.Join(", ", duplicates)}";
                return (false, StructureValidationMessage);
            }

            // 单组大小 = 所有字段占用的最大结束偏移
            GroupSize = currentOffset;

            if (GroupCount < 1) GroupCount = 1;

            int totalRequired = GroupSize * GroupCount;

            // 自动设置单个缓存块读取长度（每个缓存块都包含 GroupCount 组数据）
            Cache1.Length = (ushort)totalRequired;
            Cache2.Length = (ushort)totalRequired;

            FieldDefinitions.Clear();
            foreach (var field in newFields)
            {
                FieldDefinitions.Add(field);
            }

            // 构建展开后的列表（缓存区 × 组数 × 字段数）
            ExpandedFieldDefinitions.Clear();
            for (int cacheIndex = 1; cacheIndex <= 2; cacheIndex++)
            {
                for (int g = 0; g < GroupCount; g++)
                {
                    foreach (var field in newFields)
                    {
                        string cacheSuffix = $"_C{cacheIndex}";
                        string groupSuffix = GroupCount > 1 ? $"_G{g + 1}" : "";
                        ExpandedFieldDefinitions.Add(new CacheFieldDefinition
                        {
                            FieldName = field.FieldName,
                            Offset = field.Offset,
                            DataType = field.DataType,
                            ByteOrder = field.ByteOrder,
                            CacheIndex = cacheIndex,
                            GroupIndex = g,
                            DisplayName = $"{field.FieldName}{cacheSuffix}{groupSuffix}",
                            CacheFieldKey = $"CACHE:C{cacheIndex}:G{g}:{field.FieldName}"
                        });
                    }
                }
            }

            IsStructureValid = true;
            StructureValidationMessage = $"✅ 验证通过，{newFields.Count} 个字段 × {GroupCount} 组，单组 {GroupSize} 字节，总计 {totalRequired} 字节";
            OnPropertyChanged(nameof(FieldDefinitions));
            OnPropertyChanged(nameof(ExpandedFieldDefinitions));
            return (true, StructureValidationMessage);
        }

        /// <summary>
        /// 获取 FieldType 对应的字节大小
        /// </summary>
        public static int GetFieldTypeSize(FieldType type) => type switch
        {
            FieldType.Bool => 1,
            FieldType.Byte => 1,
            FieldType.Int16 => 2,
            FieldType.UInt16 => 2,
            FieldType.Int32 => 4,
            FieldType.UInt32 => 4,
            FieldType.Int64 => 8,
            FieldType.UInt64 => 8,
            FieldType.Long => 8,
            FieldType.Float => 4,
            FieldType.Double => 8,
            FieldType.Char => 2,
            _ => 4
        };
    }

    /// <summary>
    /// 单个缓存区配置
    /// </summary>
    public partial class SiemensReadCacheItemConfig : ObservableViewModel
    {
        /// <summary>
        /// DB块标识，例如：DB10
        /// </summary>
        [ObservableProperty]
        private string dbBlock = "DB1";

        /// <summary>
        /// 读取长度（字节）
        /// </summary>
        [ObservableProperty]
        private ushort length = 256;

        /// <summary>
        /// 可读标志地址（Bool 类型 PLC 地址）
        /// 轮询此地址判断缓存是否就绪，读完后写 false 复位
        /// 例如：DB1.DBX0.0
        /// </summary>
        [ObservableProperty]
        private string readableFlagAddress = string.Empty;
    }

    /// <summary>
    /// 缓存数据字段定义
    /// 描述缓存字节块中单个字段的解析规则
    /// </summary>
    public partial class CacheFieldDefinition : ObservableViewModel
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        [ObservableProperty]
        private string fieldName = string.Empty;

        /// <summary>
        /// 在缓存字节块中的偏移量（字节）
        /// </summary>
        [ObservableProperty]
        private ushort offset;

        /// <summary>
        /// 数据类型
        /// </summary>
        [ObservableProperty]
        private FieldType dataType = FieldType.Float;

        /// <summary>
        /// 字节序
        /// </summary>
        [ObservableProperty]
        private ByteOrder byteOrder = ByteOrder.DCBA;

        /// <summary>
        /// 备注说明
        /// </summary>
        [ObservableProperty]
        private string description = string.Empty;

        /// <summary>
        /// 缓冲区索引（1=缓存1，2=缓存2）
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private int cacheIndex = 1;

        /// <summary>
        /// 组索引（0-based，用于展开后的多组显示）
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private int groupIndex;

        /// <summary>
        /// 显示名称（多组时带组号后缀，如"测量值1_G2"）
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private string displayName = string.Empty;

        /// <summary>
        /// 缓存字段键（格式：CACHE:C{缓存}:G{组}:{字段名}，用于关联通道/点位）
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private string cacheFieldKey = string.Empty;

        /// <summary>
        /// 解析后的当前值（实时更新）
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private object? parsedValue;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private DateTime lastUpdateTime;
    }
}
