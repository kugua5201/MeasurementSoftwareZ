namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 间接测量公式脚本计算器接口。
    /// 仅支持“变量 = 表达式”的脚本写法，且最后一行必须为 RESULT = 表达式。
    /// </summary>
    /// <remarks>
    /// 推荐脚本格式示例：
    /// <code>
    /// A = (X1 + X2) / 2
    /// B = abs(X3 - X4)
    /// RESULT = round((A + B) / 2, 3)
    /// </code>
        /// 其中最后用于输出的结果变量必须命名为 <c>RESULT</c>。
    /// </remarks>
    public interface IMeasurementFormulaScriptEvaluator
    {
        /// <summary>
        /// 计算公式脚本。
        /// </summary>
        /// <param name="script">脚本文本。每一行都必须是“变量名 = 表达式”的形式，且最后一行必须是 RESULT = 表达式。</param>
        /// <param name="inputVariables">输入变量字典，通常来自间接测量的数据源绑定。</param>
        /// <param name="result">计算成功时返回最终结果。</param>
        /// <param name="calculatedVariables">计算完成后的变量字典，包含输入变量和脚本中定义的中间变量。</param>
        /// <param name="executionSteps">按执行顺序输出的脚本计算过程。</param>
        /// <param name="errorMessage">失败时返回错误信息。</param>
        /// <returns>计算成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        bool TryEvaluateScript(string script, IReadOnlyDictionary<string, double> inputVariables, out double result, out IReadOnlyDictionary<string, double> calculatedVariables, out IReadOnlyList<FormulaScriptExecutionStep> executionSteps, out string errorMessage);
    }
}
