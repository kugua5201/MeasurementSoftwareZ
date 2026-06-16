namespace MeasurementSoftware.Services.WriteDataPoints
{
    /// <summary>
    /// 写入限制脚本计算器接口。
    /// 用于计算写入点位限制模式中的脚本，并返回最终是否满足限制条件。
    /// </summary>
    /// <remarks>
    /// 脚本规则：
    /// <list type="bullet">
    /// <item><description>脚本支持多行，每一行都必须是“变量名 = 表达式”格式。</description></item>
    /// <item><description>脚本最后一行必须为 <c>result = 条件表达式</c>。</description></item>
    /// <item><description>输入变量通常来自限制模式变量表，如 <c>V1</c>、<c>V2</c>。</description></item>
    /// <item><description><c>TRUE</c>/<c>FALSE</c> 会被当作 <c>1</c>/<c>0</c> 参与判断。</description></item>
    /// </list>
    /// </remarks>
    public interface IWriteRestrictionScriptEvaluator
    {
        /// <summary>
        /// 计算写入限制脚本。
        /// </summary>
        /// <param name="script">限制脚本文本。</param>
        /// <param name="inputVariables">输入变量字典。变量值可以是数字、布尔或字符串。</param>
        /// <param name="isSatisfied">计算成功时返回最终限制是否满足。</param>
        /// <param name="errorMessage">失败时返回错误信息。</param>
        /// <returns>计算成功返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        bool TryEvaluateScript(string? script, IReadOnlyDictionary<string, object?> inputVariables, out bool isSatisfied, out string errorMessage);
    }
}
