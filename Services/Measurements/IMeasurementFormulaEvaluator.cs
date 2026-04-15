namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 间接测量公式计算器接口。
    /// 用于将用户输入的公式字符串和变量值字典计算为最终数值结果。
    /// </summary>
    /// <remarks>
    /// 当前公式能力主要包括：
    /// <list type="bullet">
    /// <item><description>运算/结构符号：<c>+</c>、<c>-</c>、<c>*</c>、<c>/</c>、<c>%</c>、<c>^</c>、<c>|</c>、<c>||</c>、<c>&amp;</c>、<c>&amp;&amp;</c>、<c>&lt;&lt;</c>、<c>&gt;&gt;</c>、<c>~</c>、<c>!</c>、<c>(</c>、<c>)</c>、<c>,</c></description></item>
    /// <item><description>常量：<c>PI</c>、<c>E</c></description></item>
    /// <item><description>内置函数：见 <see cref="MeasurementFormulaEvaluator"/> 的函数定义</description></item>
    /// </list>
    /// </remarks>
    public interface IMeasurementFormulaEvaluator
    {
        /// <summary>
        /// 使用变量上下文计算公式结果。
        /// </summary>
        /// <param name="expression">待计算的公式字符串，例如 <c>(X1 + X2) / 2</c>。</param>
        /// <param name="variables">公式变量字典，键为变量名，值为变量对应的数值。</param>
        /// <param name="result">计算成功时返回的结果值；失败时返回 0。</param>
        /// <param name="errorMessage">计算失败时返回的错误信息；成功时为空字符串。</param>
        /// <returns>
        /// 计算成功返回 <see langword="true"/>；
        /// 计算失败返回 <see langword="false"/>。
        /// </returns>
        bool TryEvaluate(string expression, IReadOnlyDictionary<string, double> variables, out double result, out string errorMessage);
    }
}
