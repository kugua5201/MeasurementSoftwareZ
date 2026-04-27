using System;
using System.Windows;
using System.Windows.Documents;

namespace MeasurementSoftware.Helpers
{
    /// <summary>
    /// 公式说明内容提供器。
    /// 负责返回内嵌说明文本，并构建富文本展示文档。
    /// </summary>
    public static class FormulaHelpContentProvider
    {
        public static FlowDocument CreateDocument(string helpMode)
        {
            return BuildFormulaHelpDocument(GetEmbeddedMarkdown(helpMode), helpMode);
        }

        private static string GetEmbeddedMarkdown(string helpMode)
        {
            return string.Equals(helpMode, "Virtual", StringComparison.OrdinalIgnoreCase)
                ? GetVirtualFormulaHelpMarkdown()
                : GetIndirectFormulaHelpMarkdown();
        }

        private static string GetVirtualFormulaHelpMarkdown()
        {
            return $$"""
# 虚拟测量公式函数说明

本文档说明当前虚拟测量公式脚本支持的写法、运算符、常量、函数和变量命名规则。


{{GetCommonFormulaHelpMarkdown("来源通道")}}
""";
        }

        private static string GetIndirectFormulaHelpMarkdown()
        {
            return $$"""
# 间接测量公式函数说明

本文档说明当前间接测量公式脚本支持的写法、运算符、常量、函数和变量命名规则。


## 间接测量触发模式

当前间接测量支持以下三种触发模式：

| 触发模式 | 触发条件 | 说明 |
| --- | --- | --- |
| 事件触发 | 绑定的点位值事件一收到就计算一次 | 不区分公式变量值是否真的变化 |
| 任意变化触发 | 任意一个变量值相对上一次快照发生变化就计算一次 | 适合希望尽快更新结果的场景 |
| 全部变化触发（默认） | 所有变量值相对上一次快照都发生变化后才计算一次 | 例如上次快照为 X1=145、X2=156，后续只有当快照变成类似 X1=146、X2=147 时才再次触发；这样可以避免间接测量在同一轮点位更新过程中，比直接测量多追加历史数据 |

{{GetCommonFormulaHelpMarkdown("数据源")}}
""";
        }

        private static string GetCommonFormulaHelpMarkdown(string sourceLabel)
        {
            return $$"""
## 1. 脚本写法

```text
A = (X1 + X2) / 2
B = abs(X3 - X4)
RESULT = round((A + B) / 2, 3)
```

规则：

- 一行写一个表达式
- 中间变量写法：`变量名 = 表达式`
- 最后一行必须写 `RESULT = 表达式`
- 检查脚本和运行脚本时，会按行从上到下顺序执行
- 支持空行
- 支持注释行：
  - `# 注释内容`
  - `// 注释内容`

## 2. 基本规则

- 结果值必须赋值给 `RESULT` 变量，且只能在最后一行赋值，且不区分大小写
- 公式区分大小写不敏感，函数名可写成 `sin`、`SIN`、`Sin`
- 支持空格，解析时会自动忽略
- 变量名由用户在“{{sourceLabel}}”表格中配置，例如 `X1`、`A`、`DIA_1`
- 变量名必须：
  - 以字母或下划线 `_` 开头
  - 后续只能包含字母、数字、下划线 `_`

示例：

- 合法：`X1`、`A`、`_Temp`、`DIA_1`
- 非法：`1X`、`A-B`、`直径`

## 3. 支持的运算/结构符号（共 17 个）

| 符号 | 含义 | 示例 |
| --- | --- | --- |
| `+` | 加法 | `X1 + X2` |
| `-` | 减法 | `X1 - X2` |
| `*` | 乘法 | `X1 * 2` |
| `/` | 除法 | `X1 / X2` |
| `%` | 取模 | `X1 % 2` |
| `^` | 幂运算 | `X1 ^ 2` |
| `|` | 按位或（整数位运算） | `X1 | X2` |
| `||` | 逻辑或（非 0 视为 true） | `X1 || X2` |
| `&` | 按位与（整数位运算） | `X1 & X2` |
| `&&` | 逻辑与（非 0 视为 true） | `X1 && X2` |
| `<<` | 左移位（整数位运算） | `X1 << 2` |
| `>>` | 右移位（整数位运算） | `X1 >> 1` |
| `~` | 按位取反（整数位运算） | `~X1` |
| `!` | 逻辑非（非 0 视为 true） | `!X1` |
| `(` | 左括号 | `(X1 + X2) / 2` |
| `)` | 右括号 | `(X1 + X2) / 2` |
| `,` | 参数分隔符 | `max(X1, X2, X3)` |

> 说明：
> - `^` 在当前公式里表示“幂运算”，不是 C# 里的按位异或。
> - 如果需要“按位异或”，请使用 `xor(a, b)` 函数。
> - `|`、`&`、`<<`、`>>`、`~` 会先将参与运算的值截断为整数再做位运算。
> - `||`、`&&`、`!` 会把非 0 视为 `true`，返回结果为 `1` 或 `0`。

## 4. 支持的常量（共 2 个）

| 常量 | 含义 |
| --- | --- |
| `PI` | 圆周率 |
| `E` | 自然常数 |

示例：

- `sin(PI / 2)`
- `E ^ 2`

## 5. 支持的函数

按函数名数量统计，当前共 21 个函数。

### 5.1 三角函数

| 函数 | 参数个数 | 说明 | 示例 |
| --- | --- | --- | --- |
| `sin(x)` | 1 | 正弦 | `sin(X1)` |
| `cos(x)` | 1 | 余弦 | `cos(X1)` |
| `tan(x)` | 1 | 正切 | `tan(X1)` |
| `asin(x)` | 1 | 反正弦 | `asin(X1)` |
| `acos(x)` | 1 | 反余弦 | `acos(X1)` |
| `atan(x)` | 1 | 反正切 | `atan(X1)` |

> 说明：三角函数输入值按弧度计算。

### 5.2 常用数学函数

| 函数 | 参数个数 | 说明 | 示例 |
| --- | --- | --- | --- |
| `abs(x)` | 1 | 绝对值 | `abs(X1)` |
| `sqrt(x)` | 1 | 平方根 | `sqrt(X1)` |
| `exp(x)` | 1 | 指数函数 e^x | `exp(X1)` |
| `ln(x)` | 1 | 自然对数 | `ln(X1)` |
| `log(x)` | 1 | 常用对数，等同 `log10(x)` | `log(X1)` |
| `log(x, b)` | 2 | 以 `b` 为底的对数 | `log(X1, 2)` |
| `log10(x)` | 1 | 以 10 为底的对数 | `log10(X1)` |
| `pow(x, y)` | 2 | x 的 y 次幂 | `pow(X1, 2)` |
| `floor(x)` | 1 | 向下取整 | `floor(X1)` |
| `ceil(x)` | 1 | 向上取整 | `ceil(X1)` |
| `round(x)` | 1 | 四舍五入到整数 | `round(X1)` |
| `round(x, n)` | 2 | 四舍五入到小数点后 `n` 位 | `round(X1, 3)` |
| `xor(x, y)` | 2 | 按位异或（整数位运算） | `xor(X1, X2)` |

### 5.3 多参数函数

| 函数 | 参数个数 | 说明 | 示例 |
| --- | --- | --- | --- |
| `min(a, b, ...)` | 2~100 | 取最小值 | `min(X1, X2, X3)` |
| `max(a, b, ...)` | 2~100 | 取最大值 | `max(X1, X2, X3)` |

### 5.4 角度转换函数

| 函数 | 参数个数 | 说明 | 示例 |
| --- | --- | --- | --- |
| `deg(x)` | 1 | 角度转弧度 | `sin(deg(30))` |
| `rad(x)` | 1 | 弧度转角度 | `rad(PI)` |

### 5.5 函数数量汇总

| 统计维度 | 数量 | 说明 |
| --- | --- | --- |
| 三角函数 | 6 | `sin`、`cos`、`tan`、`asin`、`acos`、`atan` |
| 常用数学函数 | 13 | `abs`、`sqrt`、`exp`、`ln`、`log`、`log10`、`pow`、`floor`、`ceil`、`round`、`deg`、`rad`、`xor` |
| 多参数函数 | 2 | `min`、`max` |
| 函数总数 | 21 | 按当前实际支持的函数名统计 |

## 6. 常见公式/脚本示例

- `(X1 + X2) / 2`
- `A = (X1 + X2) / 2`
- `B = max(X3, X4, X5) - min(X3, X4, X5)`
- `RESULT = round(A + B, 3)`
- `abs(X1 - X2)`
- `sqrt(pow(X1, 2) + pow(X2, 2))`
- `sin(deg(X1))`
- `max(X1, X2, X3) - min(X1, X2, X3)`
- `round((X1 + X2) / 2, 3)`
- `(X1 | X2) << 1`
- `xor(X1, X2)`
- `~X1`
- `!X1`

## 7. 脚本检查建议

建议在保存前先点击“检查脚本”按钮：

- 检查变量名是否重复
- 检查变量名格式是否正确
- 检查函数名和参数个数是否正确
- 检查表达式语法是否正确
""";
        }

        private static FlowDocument BuildFormulaHelpDocument(string markdown, string helpMode)
        {
            var document = CreateBaseHelpDocument(helpMode);
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var isCodeBlock = false;
            Paragraph? currentParagraph = null;
            List? currentList = null;
            var tableLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                var trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushMarkdownTable(document, tableLines);
                    isCodeBlock = !isCodeBlock;
                    currentParagraph = null;
                    currentList = null;
                    continue;
                }

                if (isCodeBlock)
                {
                    document.Blocks.Add(new Paragraph(new Run(rawLine))
                    {
                        Margin = new Thickness(0, 0, 0, 2),
                        Background = System.Windows.Media.Brushes.WhiteSmoke,
                        Padding = new Thickness(8)
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    FlushMarkdownTable(document, tableLines);
                    currentParagraph = null;
                    currentList = null;
                    continue;
                }

                if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                {
                    FlushMarkdownTable(document, tableLines);
                    document.Blocks.Add(CreateHeadingParagraph(trimmed[2..], 22));
                    currentParagraph = null;
                    currentList = null;
                    continue;
                }

                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    FlushMarkdownTable(document, tableLines);
                    document.Blocks.Add(CreateHeadingParagraph(trimmed[3..], 18));
                    currentParagraph = null;
                    currentList = null;
                    continue;
                }

                if (trimmed.StartsWith("### ", StringComparison.Ordinal))
                {
                    FlushMarkdownTable(document, tableLines);
                    document.Blocks.Add(CreateHeadingParagraph(trimmed[4..], 16));
                    currentParagraph = null;
                    currentList = null;
                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || IsOrderedListItem(trimmed))
                {
                    FlushMarkdownTable(document, tableLines);
                    currentList ??= new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(20, 0, 0, 8) };
                    if (!document.Blocks.Contains(currentList))
                    {
                        document.Blocks.Add(currentList);
                    }

                    var listText = trimmed.StartsWith("- ", StringComparison.Ordinal)
                        ? trimmed[2..]
                        : trimmed[(trimmed.IndexOf(' ') + 1)..];

                    currentList.ListItems.Add(new ListItem(CreateRichParagraph(listText)));
                    currentParagraph = null;
                    continue;
                }

                currentList = null;
                if (trimmed.StartsWith("|", StringComparison.Ordinal) && trimmed.EndsWith("|", StringComparison.Ordinal))
                {
                    tableLines.Add(trimmed);
                    currentParagraph = null;
                    continue;
                }

                FlushMarkdownTable(document, tableLines);

                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    document.Blocks.Add(new Paragraph(new Run(CleanInlineMarkdown(trimmed.TrimStart('>', ' '))))
                    {
                        Margin = new Thickness(20, 0, 0, 8),
                        Background = System.Windows.Media.Brushes.WhiteSmoke,
                        Padding = new Thickness(8)
                    });
                    currentParagraph = null;
                    continue;
                }

                if (currentParagraph == null)
                {
                    currentParagraph = CreateRichParagraph(trimmed);
                    document.Blocks.Add(currentParagraph);
                }
                else
                {
                    currentParagraph.Inlines.Add(new LineBreak());
                    AppendRichText(currentParagraph.Inlines, trimmed);
                }
            }

            FlushMarkdownTable(document, tableLines);

            return document;
        }

        private static FlowDocument CreateBaseHelpDocument(string helpMode)
        {
            var document = new FlowDocument
            {
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                LineHeight = 22
            };

            return document;
        }

        private static Paragraph CreateHeadingParagraph(string text, double fontSize)
        {
            return new Paragraph(new Run(text))
            {
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 8)
            };
        }

        private static Paragraph CreateRichParagraph(string text)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
            AppendRichText(paragraph.Inlines, text);
            return paragraph;
        }

        private static void AppendRichText(InlineCollection inlines, string text)
        {
            var segments = CleanInlineMarkdown(text).Split("**", StringSplitOptions.None);
            for (int i = 0; i < segments.Length; i++)
            {
                var run = new Run(segments[i]);
                if (i % 2 == 1)
                {
                    run.FontWeight = FontWeights.Bold;
                }

                inlines.Add(run);
            }
        }

        private static string CleanInlineMarkdown(string text)
        {
            return text.Replace("`", string.Empty).Trim();
        }

        private static void FlushMarkdownTable(FlowDocument document, List<string> tableLines)
        {
            if (tableLines.Count < 2)
            {
                tableLines.Clear();
                return;
            }

            var rows = tableLines
                .Where(line => !line.Contains("---", StringComparison.Ordinal))
                .Select(ParseMarkdownTableCells)
                .Where(cells => cells.Length > 0)
                .ToList();

            tableLines.Clear();
            if (rows.Count == 0)
            {
                return;
            }

            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 0, 0, 10)
            };

            for (int i = 0; i < rows[0].Length; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow();
            foreach (var cell in rows[0])
            {
                headerRow.Cells.Add(CreateTableCell(cell, true));
            }
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);

            var bodyGroup = new TableRowGroup();
            foreach (var row in rows.Skip(1))
            {
                var tableRow = new TableRow();
                foreach (var cell in row)
                {
                    tableRow.Cells.Add(CreateTableCell(cell, false));
                }
                bodyGroup.Rows.Add(tableRow);
            }
            table.RowGroups.Add(bodyGroup);
            document.Blocks.Add(table);
        }

        private static string[] ParseMarkdownTableCells(string line)
        {
            var cells = new List<string>();
            var current = new System.Text.StringBuilder();
            var trimmedLine = line.Trim();
            var inCodeSpan = false;

            for (int i = 0; i < trimmedLine.Length; i++)
            {
                var ch = trimmedLine[i];

                if (ch == '`')
                {
                    inCodeSpan = !inCodeSpan;
                    current.Append(ch);
                    continue;
                }

                if (ch == '|' && !inCodeSpan)
                {
                    if (i == 0 || i == trimmedLine.Length - 1)
                    {
                        continue;
                    }

                    cells.Add(CleanInlineMarkdown(current.ToString()));
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
            {
                cells.Add(CleanInlineMarkdown(current.ToString()));
            }

            return cells.ToArray();
        }

        private static TableCell CreateTableCell(string text, bool isHeader)
        {
            var paragraph = new Paragraph(new Run(text))
            {
                Margin = new Thickness(4, 2, 4, 2),
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal
            };

            return new TableCell(paragraph)
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(0.5),
                Background = isHeader ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.Transparent
            };
        }

        private static bool IsOrderedListItem(string text)
        {
            var dotIndex = text.IndexOf('.');
            return dotIndex > 0
                && dotIndex < text.Length - 1
                && int.TryParse(text[..dotIndex], out _)
                && char.IsWhiteSpace(text[dotIndex + 1]);
        }
    }
}
