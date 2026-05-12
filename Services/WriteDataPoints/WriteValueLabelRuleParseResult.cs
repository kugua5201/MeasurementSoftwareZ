using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    public sealed class WriteValueLabelRuleParseResult
    {
        public bool IsValid { get; init; } = true;

        public string StatusText { get; init; } = string.Empty;

        public string DefaultDisplayText { get; init; } = "--";

        public IReadOnlyList<WriteValueDisplayRule> Rules { get; init; } = Array.Empty<WriteValueDisplayRule>();
    }
}
