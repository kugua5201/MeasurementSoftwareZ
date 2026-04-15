namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 间接测量公式脚本计算器。
    /// 只支持显式赋值脚本，且最后一行必须为 RESULT = 表达式。
    /// </summary>
    public sealed class MeasurementFormulaScriptEvaluator : IMeasurementFormulaScriptEvaluator
    {
        private readonly IMeasurementFormulaEvaluator _formulaEvaluator;

        public MeasurementFormulaScriptEvaluator(IMeasurementFormulaEvaluator formulaEvaluator)
        {
            _formulaEvaluator = formulaEvaluator;
        }

        /// <summary>
        /// 计算公式脚本。
        /// 脚本按顺序执行，且最后一行必须输出 RESULT 变量。
        /// </summary>
        public bool TryEvaluateScript(string script, IReadOnlyDictionary<string, double> inputVariables, out double result, out IReadOnlyDictionary<string, double> calculatedVariables, out IReadOnlyList<FormulaScriptExecutionStep> executionSteps, out string errorMessage)
        {
            result = 0;
            errorMessage = string.Empty;
            var runtimeVariables = new Dictionary<string, double>(inputVariables, StringComparer.OrdinalIgnoreCase);
            calculatedVariables = runtimeVariables;
            var steps = new List<FormulaScriptExecutionStep>();
            executionSteps = steps;

            var executableLines = GetExecutableLines(script).ToList();
            if (executableLines.Count == 0)
            {
                errorMessage = "公式脚本不能为空";
                return false;
            }

            string? lastAssignedVariable = null;
            var assignedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in executableLines)
            {
                var equalsIndex = line.Content.IndexOf('=');
                if (equalsIndex <= 0 || equalsIndex == line.Content.Length - 1)
                {
                    errorMessage = $"第 {line.LineNumber} 行格式错误，应为 变量名 = 表达式";
                    return false;
                }

                var variableName = line.Content[..equalsIndex].Trim();
                var expression = line.Content[(equalsIndex + 1)..].Trim();
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

                if (!_formulaEvaluator.TryEvaluate(expression, runtimeVariables, out var lineValue, out errorMessage))
                {
                    errorMessage = $"第 {line.LineNumber} 行计算失败：{errorMessage}";
                    return false;
                }

                runtimeVariables[variableName] = lineValue;
                steps.Add(new FormulaScriptExecutionStep(line.LineNumber, variableName, expression, lineValue));
                lastAssignedVariable = variableName;
            }

            if (!string.Equals(lastAssignedVariable, "RESULT", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "多行公式脚本最后一行必须是 RESULT = 表达式";
                return false;
            }

            result = runtimeVariables["RESULT"];
            return true;
        }

        private static IEnumerable<FormulaScriptLine> GetExecutableLines(string? script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                yield break;
            }

            var lines = script.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var content = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(content) || content.StartsWith("//", StringComparison.Ordinal) || content.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return new FormulaScriptLine(i + 1, content);
            }
        }

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

        private sealed record FormulaScriptLine(int LineNumber, string Content);
    }
}
