namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 公式脚本单步执行结果。
    /// 用于在“检查脚本”时展示每一行变量的计算过程。
    /// </summary>
    /// <param name="LineNumber">脚本中的原始行号。</param>
    /// <param name="TargetName">本行被赋值的变量名。单行表达式兼容模式下固定为 RESULT。</param>
    /// <param name="Expression">本行实际执行的表达式文本。</param>
    /// <param name="Value">本行计算得到的数值。</param>
    public sealed record FormulaScriptExecutionStep(int LineNumber, string TargetName, string Expression, double Value);
}
