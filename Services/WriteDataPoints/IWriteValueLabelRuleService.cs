using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    public interface IWriteValueLabelRuleService
    {
        string GetDisplayText(object? currentValue, bool usesRuleDisplay, IEnumerable<WriteValueDisplayRule> displayRules, string defaultDisplayText);

        string BuildRuleScript(IEnumerable<WriteValueDisplayRule> displayRules, string defaultDisplayText);

        WriteValueLabelRuleParseResult ParseRuleScript(string? ruleScriptText, string defaultDisplayText);
    }
}
