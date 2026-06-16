using MeasurementSoftware.Services.Measurements;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    /// <summary>
    /// 写入限制脚本计算器。
    /// 负责解析“变量 = 表达式”脚本，并输出最终的 <c>result</c> 布尔结果。
    /// </summary>
    public sealed class WriteRestrictionScriptEvaluator : IWriteRestrictionScriptEvaluator
    {
        private readonly IMeasurementFormulaEvaluator _formulaEvaluator;

        /// <summary>
        /// 初始化写入限制脚本计算器。
        /// </summary>
        /// <param name="formulaEvaluator">基础数值表达式计算器。</param>
        public WriteRestrictionScriptEvaluator(IMeasurementFormulaEvaluator formulaEvaluator)
        {
            _formulaEvaluator = formulaEvaluator;
        }

        /// <summary>
        /// 计算写入限制脚本。
        /// </summary>
        /// <param name="script">限制脚本文本。</param>
        /// <param name="inputVariables">外部输入变量，如 V1、V2。变量值可以是数字、布尔或字符串。</param>
        /// <param name="isSatisfied">最终是否满足限制条件。</param>
        /// <param name="errorMessage">失败时的错误信息。</param>
        /// <returns>脚本计算成功返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        public bool TryEvaluateScript(string? script, IReadOnlyDictionary<string, object?> inputVariables, out bool isSatisfied, out string errorMessage)
        {
            isSatisfied = false;
            errorMessage = string.Empty;
            var runtimeVariables = new Dictionary<string, object?>(inputVariables, StringComparer.OrdinalIgnoreCase);
            var numericVariables = BuildNumericVariables(inputVariables);

            var executableLines = GetExecutableLines(script).ToList();
            if (executableLines.Count == 0)
            {
                errorMessage = "限制脚本不能为空，最后必须写 result = 条件表达式";
                return false;
            }

            string? lastAssignedVariable = null;
            var assignedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in executableLines)
            {
                int assignmentIndex = FindAssignmentOperator(line.Content);
                if (assignmentIndex <= 0 || assignmentIndex == line.Content.Length - 1)
                {
                    errorMessage = $"第 {line.LineNumber} 行格式错误，应为 变量名 = 表达式";
                    return false;
                }

                var variableName = line.Content[..assignmentIndex].Trim();
                var expression = line.Content[(assignmentIndex + 1)..].Trim();
                if (!IsValidAlias(variableName))
                {
                    errorMessage = $"第 {line.LineNumber} 行变量名 {variableName} 不合法，只能包含字母、数字和下划线，且必须以字母或下划线开头";
                    return false;
                }

                if (inputVariables.ContainsKey(variableName))
                {
                    errorMessage = $"第 {line.LineNumber} 行不能覆盖输入变量 {variableName}";
                    return false;
                }

                if (!assignedVariables.Add(variableName))
                {
                    errorMessage = $"第 {line.LineNumber} 行变量 {variableName} 重复定义";
                    return false;
                }

                if (!TryEvaluateValueExpression(expression, runtimeVariables, numericVariables, out var lineValue, out errorMessage))
                {
                    errorMessage = $"第 {line.LineNumber} 行计算失败：{errorMessage}";
                    return false;
                }

                runtimeVariables[variableName] = lineValue;
                numericVariables[variableName] = lineValue;
                lastAssignedVariable = variableName;
            }

            if (!string.Equals(lastAssignedVariable, "RESULT", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "限制脚本最后一行必须是 result = 条件表达式";
                return false;
            }

            isSatisfied = runtimeVariables.TryGetValue("RESULT", out var resultValue) && IsTruthy(resultValue);
            return true;
        }

        /// <summary>
        /// 计算单行右侧表达式。
        /// 如果表达式包含布尔比较/逻辑运算，则按布尔结果输出 1 或 0；否则按普通数值表达式计算。
        /// </summary>
        private bool TryEvaluateValueExpression(string expression, IReadOnlyDictionary<string, object?> rawVariables, IReadOnlyDictionary<string, double> numericVariables, out double value, out string errorMessage)
        {
            value = 0d;
            errorMessage = string.Empty;
            if (ContainsBooleanOperator(expression))
            {
                if (!TryEvaluateBooleanExpression(expression, rawVariables, numericVariables, out var boolResult, out errorMessage))
                {
                    return false;
                }

                value = boolResult ? 1d : 0d;
                return true;
            }

            if (!_formulaEvaluator.TryEvaluate(expression, numericVariables, out value, out errorMessage))
            {
                errorMessage = $"表达式无效：{errorMessage}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 计算布尔表达式。
        /// 支持比较运算、逻辑与、逻辑或以及括号嵌套。
        /// </summary>
        private bool TryEvaluateBooleanExpression(string expression, IReadOnlyDictionary<string, object?> rawVariables, IReadOnlyDictionary<string, double> numericVariables, out bool result, out string errorMessage)
        {
            result = false;
            errorMessage = string.Empty;
            var normalizedExpression = TrimOuterParentheses(expression.Trim());
            var orParts = SplitTopLevel(normalizedExpression, "||");
            if (orParts.Count > 1)
            {
                foreach (var orPart in orParts)
                {
                    if (!TryEvaluateBooleanExpression(orPart, rawVariables, numericVariables, out var partResult, out errorMessage))
                    {
                        return false;
                    }

                    if (partResult)
                    {
                        result = true;
                        return true;
                    }
                }

                return true;
            }

            var andParts = SplitTopLevel(normalizedExpression, "&&");
            if (andParts.Count > 1)
            {
                foreach (var andPart in andParts)
                {
                    if (!TryEvaluateBooleanExpression(andPart, rawVariables, numericVariables, out var partResult, out errorMessage))
                    {
                        return false;
                    }

                    if (!partResult)
                    {
                        return true;
                    }
                }

                result = true;
                return true;
            }

            return TryEvaluateComparisonExpression(normalizedExpression, rawVariables, numericVariables, out result, out errorMessage);
        }

        /// <summary>
        /// 计算最小粒度的比较表达式。
        /// 如果不存在比较运算符，则把该表达式按数值表达式求值，非零视为 <see langword="true"/>。
        /// </summary>
        private bool TryEvaluateComparisonExpression(string expression, IReadOnlyDictionary<string, object?> rawVariables, IReadOnlyDictionary<string, double> numericVariables, out bool result, out string errorMessage)
        {
            result = false;
            errorMessage = string.Empty;
            foreach (var comparisonOperator in new[] { "==", "!=", ">=", "<=", ">", "<" })
            {
                int operatorIndex = FindTopLevelOperator(expression, comparisonOperator);
                if (operatorIndex < 0)
                {
                    continue;
                }

                var leftExpression = expression[..operatorIndex].Trim();
                var rightExpression = expression[(operatorIndex + comparisonOperator.Length)..].Trim();
                var hasLeftStringOperand = TryResolveStringOperand(leftExpression, rawVariables, out var leftStringOperand);
                var hasRightStringOperand = TryResolveStringOperand(rightExpression, rawVariables, out var rightStringOperand);

                if ((hasLeftStringOperand || hasRightStringOperand)
                    && comparisonOperator is "==" or "!=")
                {
                    var leftText = leftStringOperand ?? ResolveOperandText(leftExpression, rawVariables, numericVariables, out errorMessage);
                    if (errorMessage.Length > 0)
                    {
                        errorMessage = $"限制表达式左侧无效：{errorMessage}";
                        return false;
                    }

                    var rightText = rightStringOperand ?? ResolveOperandText(rightExpression, rawVariables, numericVariables, out errorMessage);
                    if (errorMessage.Length > 0)
                    {
                        errorMessage = $"限制表达式右侧无效：{errorMessage}";
                        return false;
                    }

                    result = comparisonOperator == "=="
                        ? string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase)
                        : !string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
                    return true;
                }

                if (!_formulaEvaluator.TryEvaluate(leftExpression, numericVariables, out var leftValue, out errorMessage))
                {
                    errorMessage = $"限制表达式左侧无效：{errorMessage}";
                    return false;
                }

                if (!_formulaEvaluator.TryEvaluate(rightExpression, numericVariables, out var rightValue, out errorMessage))
                {
                    errorMessage = $"限制表达式右侧无效：{errorMessage}";
                    return false;
                }

                result = comparisonOperator switch
                {
                    "==" => Math.Abs(leftValue - rightValue) <= 0.0000001d,
                    "!=" => Math.Abs(leftValue - rightValue) > 0.0000001d,
                    ">=" => leftValue >= rightValue,
                    "<=" => leftValue <= rightValue,
                    ">" => leftValue > rightValue,
                    "<" => leftValue < rightValue,
                    _ => false
                };
                return true;
            }

            if (TryResolveStringOperand(expression, rawVariables, out var stringOperand))
            {
                result = !string.IsNullOrWhiteSpace(stringOperand);
                return true;
            }

            if (!_formulaEvaluator.TryEvaluate(expression, numericVariables, out var numericValue, out errorMessage))
            {
                errorMessage = $"限制表达式无效：{errorMessage}";
                return false;
            }

            result = Math.Abs(numericValue) > 0.0000001d;
            return true;
        }

        /// <summary>
        /// 构建可用于基础公式计算器的数值变量字典。
        /// 只有数字、布尔和可解析数字字符串会进入该字典。
        /// </summary>
        private static Dictionary<string, double> BuildNumericVariables(IReadOnlyDictionary<string, object?> inputVariables)
        {
            var numericVariables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in inputVariables)
            {
                if (TryConvertToDouble(item.Value, out var numericValue))
                {
                    numericVariables[item.Key] = numericValue;
                }
            }

            return numericVariables;
        }

        /// <summary>
        /// 尝试把表达式解析为字符串操作数。
        /// 支持直接写字符串字面量，或引用原始字符串变量。
        /// </summary>
        private static bool TryResolveStringOperand(string expression, IReadOnlyDictionary<string, object?> rawVariables, out string? stringValue)
        {
            var trimmedExpression = expression.Trim();
            if (TryParseStringLiteral(trimmedExpression, out stringValue))
            {
                return true;
            }

            if (rawVariables.TryGetValue(trimmedExpression, out var rawValue) && rawValue is string textValue)
            {
                stringValue = textValue;
                return true;
            }

            stringValue = null;
            return false;
        }

        /// <summary>
        /// 将操作数转换为用于字符串比较的文本。
        /// </summary>
        private string? ResolveOperandText(string expression, IReadOnlyDictionary<string, object?> rawVariables, IReadOnlyDictionary<string, double> numericVariables, out string errorMessage)
        {
            if (TryResolveStringOperand(expression, rawVariables, out var stringOperand))
            {
                errorMessage = string.Empty;
                return stringOperand ?? string.Empty;
            }

            var trimmedExpression = expression.Trim();
            if (rawVariables.TryGetValue(trimmedExpression, out var rawValue))
            {
                errorMessage = string.Empty;
                return ConvertObjectToText(rawValue);
            }

            if (!_formulaEvaluator.TryEvaluate(trimmedExpression, numericVariables, out var numericValue, out errorMessage))
            {
                return null;
            }

            errorMessage = string.Empty;
            return numericValue.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 解析脚本中的字符串字面量。
        /// 当前支持双引号包裹的文本，并允许使用反斜杠转义双引号。
        /// </summary>
        private static bool TryParseStringLiteral(string expression, out string stringValue)
        {
            stringValue = string.Empty;
            if (expression.Length < 2 || expression[0] != '"' || expression[^1] != '"')
            {
                return false;
            }

            stringValue = expression[1..^1].Replace("\\\"", "\"");
            return true;
        }

        /// <summary>
        /// 把任意对象转换为比较时使用的文本。
        /// </summary>
        private static string ConvertObjectToText(object? value)
        {
            return value switch
            {
                null => string.Empty,
                bool boolValue => boolValue ? "TRUE" : "FALSE",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// 判断对象在限制脚本语义下是否为真。
        /// </summary>
        private static bool IsTruthy(object? value)
        {
            return value switch
            {
                null => false,
                bool boolValue => boolValue,
                string stringValue => !string.IsNullOrWhiteSpace(stringValue),
                _ when TryConvertToDouble(value, out var numericValue) => Math.Abs(numericValue) > 0.0000001d,
                _ => !string.IsNullOrWhiteSpace(value.ToString())
            };
        }

        /// <summary>
        /// 尝试把对象转成 double。
        /// </summary>
        private static bool TryConvertToDouble(object? value, out double numericValue)
        {
            numericValue = 0d;
            switch (value)
            {
                case null:
                    return false;
                case bool boolValue:
                    numericValue = boolValue ? 1d : 0d;
                    return true;
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    numericValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;
                default:
                    var text = value.ToString();
                    return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out numericValue)
                        || double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out numericValue);
            }
        }

        /// <summary>
        /// 读取脚本中的可执行行，并在这里统一处理 TRUE/FALSE 常量兼容。
        /// </summary>
        private static IEnumerable<WriteRestrictionScriptLine> GetExecutableLines(string? script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                yield break;
            }

            var normalizedScript = NormalizeScript(script);
            var lines = normalizedScript.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var content = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(content) || content.StartsWith("//", StringComparison.Ordinal) || content.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return new WriteRestrictionScriptLine(i + 1, content);
            }
        }

        /// <summary>
        /// 规范化脚本文本。
        /// 当前只负责把 TRUE/FALSE 替换成 1/0，避免后续公式计算器无法识别布尔常量。
        /// </summary>
        private static string NormalizeScript(string script)
        {
            var normalizedScript = script.Trim();
            normalizedScript = Regex.Replace(normalizedScript, @"\bTRUE\b", "1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            normalizedScript = Regex.Replace(normalizedScript, @"\bFALSE\b", "0", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return normalizedScript;
        }

        /// <summary>
        /// 检查变量名是否合法。
        /// </summary>
        private static bool IsValidAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (!(char.IsLetter(alias[0]) || alias[0] == '_'))
            {
                return false;
            }

            return alias.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
        }

        /// <summary>
        /// 判断表达式是否包含布尔相关运算符。
        /// </summary>
        private static bool ContainsBooleanOperator(string expression)
        {
            return FindTopLevelOperator(expression, "||") >= 0
                || FindTopLevelOperator(expression, "&&") >= 0
                || FindTopLevelOperator(expression, "==") >= 0
                || FindTopLevelOperator(expression, "!=") >= 0
                || FindTopLevelOperator(expression, ">=") >= 0
                || FindTopLevelOperator(expression, "<=") >= 0
                || FindTopLevelOperator(expression, ">") >= 0
                || FindTopLevelOperator(expression, "<") >= 0;
        }

        /// <summary>
        /// 查找赋值运算符“=”，并排除“==”“>=”“<=”“!=”等比较场景。
        /// </summary>
        private static int FindAssignmentOperator(string expression)
        {
            int depth = 0;
            for (int i = 0; i < expression.Length; i++)
            {
                char current = expression[i];
                if (current == '(')
                {
                    depth++;
                    continue;
                }

                if (current == ')')
                {
                    depth--;
                    continue;
                }

                if (depth != 0 || current != '=')
                {
                    continue;
                }

                char previous = i > 0 ? expression[i - 1] : '\0';
                char next = i < expression.Length - 1 ? expression[i + 1] : '\0';
                if (previous is '>' or '<' or '!' or '=' || next == '=')
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        /// <summary>
        /// 去掉最外层完整包裹表达式的括号。
        /// </summary>
        private static string TrimOuterParentheses(string expression)
        {
            var normalizedExpression = expression.Trim();
            while (normalizedExpression.Length >= 2 && normalizedExpression[0] == '(' && normalizedExpression[^1] == ')')
            {
                int depth = 0;
                bool wrapsAll = true;
                for (int i = 0; i < normalizedExpression.Length; i++)
                {
                    if (normalizedExpression[i] == '(')
                    {
                        depth++;
                    }
                    else if (normalizedExpression[i] == ')')
                    {
                        depth--;
                        if (depth == 0 && i < normalizedExpression.Length - 1)
                        {
                            wrapsAll = false;
                            break;
                        }
                    }
                }

                if (!wrapsAll || depth != 0)
                {
                    break;
                }

                normalizedExpression = normalizedExpression[1..^1].Trim();
            }

            return normalizedExpression;
        }

        /// <summary>
        /// 在顶层括号作用域内按指定逻辑运算符分割表达式。
        /// </summary>
        private static List<string> SplitTopLevel(string expression, string token)
        {
            var parts = new List<string>();
            int depth = 0;
            int startIndex = 0;
            for (int i = 0; i <= expression.Length - token.Length; i++)
            {
                if (expression[i] == '(')
                {
                    depth++;
                    continue;
                }

                if (expression[i] == ')')
                {
                    depth--;
                    continue;
                }

                if (depth == 0 && string.Compare(expression, i, token, 0, token.Length, StringComparison.Ordinal) == 0)
                {
                    parts.Add(expression[startIndex..i].Trim());
                    startIndex = i + token.Length;
                    i += token.Length - 1;
                }
            }

            if (parts.Count == 0)
            {
                return new List<string> { expression.Trim() };
            }

            parts.Add(expression[startIndex..].Trim());
            return parts;
        }

        /// <summary>
        /// 在顶层括号作用域内查找指定运算符。
        /// </summary>
        private static int FindTopLevelOperator(string expression, string comparisonOperator)
        {
            int depth = 0;
            for (int i = 0; i <= expression.Length - comparisonOperator.Length; i++)
            {
                if (expression[i] == '(')
                {
                    depth++;
                    continue;
                }

                if (expression[i] == ')')
                {
                    depth--;
                    continue;
                }

                if (depth == 0 && string.Compare(expression, i, comparisonOperator, 0, comparisonOperator.Length, StringComparison.Ordinal) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 限制脚本中的单行描述。
        /// </summary>
        private sealed record WriteRestrictionScriptLine(int LineNumber, string Content);
    }
}
