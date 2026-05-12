using MeasurementSoftware.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    public sealed class WriteValueLabelRuleService : IWriteValueLabelRuleService
    {
        public string GetDisplayText(object? currentValue, bool usesRuleDisplay, IEnumerable<WriteValueDisplayRule> displayRules, string defaultDisplayText)
        {
            var normalizedDefaultDisplayText = string.IsNullOrWhiteSpace(defaultDisplayText) ? "--" : defaultDisplayText;
            if (currentValue == null)
            {
                return normalizedDefaultDisplayText;
            }

            if (!usesRuleDisplay)
            {
                return currentValue.ToString() ?? string.Empty;
            }

            var rawValue = currentValue.ToString() ?? string.Empty;
            var rule = displayRules.FirstOrDefault(x => IsMatch(x.SourceValue, rawValue));
            if (rule != null && !string.IsNullOrWhiteSpace(rule.DisplayText))
            {
                return rule.DisplayText;
            }

            return normalizedDefaultDisplayText;
        }

        public string BuildRuleScript(IEnumerable<WriteValueDisplayRule> displayRules, string defaultDisplayText)
        {
            var lines = new List<string>();
            foreach (var rule in displayRules)
            {
                if (string.IsNullOrWhiteSpace(rule.SourceValue) && string.IsNullOrWhiteSpace(rule.DisplayText))
                {
                    continue;
                }

                lines.Add($"{rule.SourceValue}={rule.DisplayText}");
            }

            if (!string.IsNullOrWhiteSpace(defaultDisplayText))
            {
                lines.Add($"default={defaultDisplayText}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public WriteValueLabelRuleParseResult ParseRuleScript(string? ruleScriptText, string defaultDisplayText)
        {
            var parsedRules = new List<WriteValueDisplayRule>();
            var parsedDefaultDisplayText = string.IsNullOrWhiteSpace(defaultDisplayText) ? "--" : defaultDisplayText;
            var defaultRuleCount = 0;
            var lines = (ruleScriptText ?? string.Empty).Replace("\r\n", "\n").Split('\n');

            for (int index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                {
                    continue;
                }

                var separatorIndex = line.LastIndexOf('=');
                if (separatorIndex < 0)
                {
                    return new WriteValueLabelRuleParseResult
                    {
                        IsValid = false,
                        StatusText = $"第 {index + 1} 行格式错误，请使用 表达式=显示值",
                        DefaultDisplayText = parsedDefaultDisplayText,
                        Rules = parsedRules
                    };
                }

                var sourceValue = line[..separatorIndex].Trim();
                var displayText = line[(separatorIndex + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(sourceValue))
                {
                    return new WriteValueLabelRuleParseResult
                    {
                        IsValid = false,
                        StatusText = $"第 {index + 1} 行原始值不能为空",
                        DefaultDisplayText = parsedDefaultDisplayText,
                        Rules = parsedRules
                    };
                }

                if (sourceValue.Equals("default", StringComparison.OrdinalIgnoreCase)
                 )
                {
                    defaultRuleCount++;
                    if (defaultRuleCount > 1)
                    {
                        return new WriteValueLabelRuleParseResult
                        {
                            IsValid = false,
                            StatusText = $"第 {index + 1} 行默认规则重复，default 只能存在一条",
                            DefaultDisplayText = parsedDefaultDisplayText,
                            Rules = parsedRules
                        };
                    }

                    parsedDefaultDisplayText = displayText;
                    continue;
                }

                if (!IsValidConditionExpression(sourceValue))
                {
                    return new WriteValueLabelRuleParseResult
                    {
                        IsValid = false,
                        StatusText = $"第 {index + 1} 行条件无效，每行只支持一个条件，可使用精确值或 >、<、>=、<= 比较，例如 >=10",
                        DefaultDisplayText = parsedDefaultDisplayText,
                        Rules = parsedRules
                    };
                }

                parsedRules.Add(new WriteValueDisplayRule
                {
                    SourceValue = sourceValue,
                    DisplayText = displayText
                });
            }

            if (defaultRuleCount == 0)
            {
                return new WriteValueLabelRuleParseResult
                {
                    IsValid = false,
                    StatusText = "必须包含且只能包含一条 default 默认规则",
                    DefaultDisplayText = parsedDefaultDisplayText,
                    Rules = parsedRules
                };
            }

            return new WriteValueLabelRuleParseResult
            {
                IsValid = true,
                StatusText = string.Empty,
                DefaultDisplayText = string.IsNullOrWhiteSpace(parsedDefaultDisplayText) ? "--" : parsedDefaultDisplayText,
                Rules = new ReadOnlyCollection<WriteValueDisplayRule>(parsedRules)
            };
        }

        private static bool IsMatch(string? conditionExpression, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression))
            {
                return false;
            }

            var expression = conditionExpression.Trim();
            if (!ContainsComparisonOperator(expression))
            {
                return string.Equals(expression, rawValue, StringComparison.OrdinalIgnoreCase);
            }

            if (!double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var numericValue)
                && !double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out numericValue))
            {
                return false;
            }

            return EvaluateSingleCondition(expression, numericValue);
        }

        private static bool IsValidConditionExpression(string? conditionExpression)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression))
            {
                return false;
            }

            var expression = conditionExpression.Trim();
            if (!ContainsComparisonOperator(expression))
            {
                return true;
            }

            return IsValidSingleCondition(expression);
        }

        private static bool ContainsComparisonOperator(string expression)
        {
            return expression.Contains('>') || expression.Contains('<');
        }

        private static bool IsValidSingleCondition(string condition)
        {
            return TryParseSingleCondition(condition, out _, out _);
        }

        private static bool EvaluateSingleCondition(string condition, double numericValue)
        {
            if (!TryParseSingleCondition(condition, out var comparisonOperator, out var comparisonValue))
            {
                return false;
            }

            return comparisonOperator switch
            {
                ">" => numericValue > comparisonValue,
                ">=" => numericValue >= comparisonValue,
                "<" => numericValue < comparisonValue,
                "<=" => numericValue <= comparisonValue,
                _ => false
            };
        }

        private static bool TryParseSingleCondition(string condition, out string comparisonOperator, out double comparisonValue)
        {
            comparisonOperator = string.Empty;
            comparisonValue = 0;

            var trimmedCondition = condition.Trim();
            if (trimmedCondition.StartsWith(">=" , StringComparison.Ordinal))
            {
                comparisonOperator = ">=";
                return TryParseComparisonValue(trimmedCondition[2..], out comparisonValue);
            }

            if (trimmedCondition.StartsWith("<=", StringComparison.Ordinal))
            {
                comparisonOperator = "<=";
                return TryParseComparisonValue(trimmedCondition[2..], out comparisonValue);
            }

            if (trimmedCondition.StartsWith('>'))
            {
                comparisonOperator = ">";
                return TryParseComparisonValue(trimmedCondition[1..], out comparisonValue);
            }

            if (trimmedCondition.StartsWith('<'))
            {
                comparisonOperator = "<";
                return TryParseComparisonValue(trimmedCondition[1..], out comparisonValue);
            }

            return false;
        }

        private static bool TryParseComparisonValue(string text, out double value)
        {
            var trimmedText = text.Trim();
            return double.TryParse(trimmedText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
                || double.TryParse(trimmedText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value);
        }
    }
}
