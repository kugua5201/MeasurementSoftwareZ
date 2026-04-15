using System.Globalization;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 轻量级数学公式计算器。
    /// 支持变量、常量、常用运算符、结构符号和常见数学函数。
    /// </summary>
    /// <remarks>
    /// 为了便于维护，这里把“公式里到底支持多少符号/函数”直接写清楚：
    /// <list type="bullet">
    /// <item><description>运算/结构符号共 9 个：<c>+</c>、<c>-</c>、<c>*</c>、<c>/</c>、<c>%</c>、<c>^</c>、<c>(</c>、<c>)</c>、<c>,</c></description></item>
    /// <item><description>内置常量共 2 个：<c>PI</c>、<c>E</c></description></item>
    /// <item><description>可用函数名共 21 个：<c>sin</c>、<c>cos</c>、<c>tan</c>、<c>asin</c>、<c>acos</c>、<c>atan</c>、<c>abs</c>、<c>sqrt</c>、<c>exp</c>、<c>ln</c>、<c>log</c>、<c>log10</c>、<c>pow</c>、<c>min</c>、<c>max</c>、<c>floor</c>、<c>ceil</c>、<c>ceiling</c>、<c>round</c>、<c>deg</c>、<c>rad</c></description></item>
    /// <item><description>其中 <c>ceil</c> 和 <c>ceiling</c> 是同一能力的两个名字，所以如果按“独立函数能力”统计则是 20 类。</description></item>
    /// </list>
    /// </summary>
    public sealed class MeasurementFormulaEvaluator : IMeasurementFormulaEvaluator
    {
        /// <summary>
        /// 公式内置函数映射表。
        /// 键为函数名，值为对应的计算实现。
        /// </summary>
        private static readonly IReadOnlyDictionary<string, Func<double[], double>> Functions =
            new Dictionary<string, Func<double[], double>>(StringComparer.OrdinalIgnoreCase)
            {
                ["sin"] = args => Math.Sin(args[0]),
                ["cos"] = args => Math.Cos(args[0]),
                ["tan"] = args => Math.Tan(args[0]),
                ["asin"] = args => Math.Asin(args[0]),
                ["acos"] = args => Math.Acos(args[0]),
                ["atan"] = args => Math.Atan(args[0]),
                ["abs"] = args => Math.Abs(args[0]),
                ["sqrt"] = args => Math.Sqrt(args[0]),
                ["exp"] = args => Math.Exp(args[0]),
                ["ln"] = args => Math.Log(args[0]),
                ["log"] = args => args.Length == 1 ? Math.Log10(args[0]) : Math.Log(args[0], args[1]),
                ["log10"] = args => Math.Log10(args[0]),
                ["pow"] = args => Math.Pow(args[0], args[1]),
                ["min"] = args => args.Min(),
                ["max"] = args => args.Max(),
                ["xor"] = args => ToIntegerResult(args[0]) ^ ToIntegerResult(args[1]),
                ["floor"] = args => Math.Floor(args[0]),
                ["ceil"] = args => Math.Ceiling(args[0]),
                ["round"] = args => args.Length == 1 ? Math.Round(args[0]) : Math.Round(args[0], (int)args[1]),
                ["deg"] = args => args[0] * Math.PI / 180d,
                ["rad"] = args => args[0] * 180d / Math.PI
            };

        /// <summary>
        /// 公式函数支持的参数个数定义。
        /// 例如：
        /// <list type="bullet">
        /// <item><description><c>sin</c> 只能传 1 个参数</description></item>
        /// <item><description><c>log</c> 可以传 1 个或 2 个参数</description></item>
        /// <item><description><c>min</c>、<c>max</c> 当前允许传 2~100 个参数</description></item>
        /// <item><description><c>xor</c> 传 2 个参数，表示按位异或</description></item>
        /// </list>
        /// </summary>
        private static readonly IReadOnlyDictionary<string, int[]> FunctionArgCounts =
            new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["sin"] = [1],
                ["cos"] = [1],
                ["tan"] = [1],
                ["asin"] = [1],
                ["acos"] = [1],
                ["atan"] = [1],
                ["abs"] = [1],
                ["sqrt"] = [1],
                ["exp"] = [1],
                ["ln"] = [1],
                ["log"] = [1, 2],
                ["log10"] = [1],
                ["pow"] = [2],
                ["min"] = Enumerable.Range(2, 99).ToArray(),
                ["max"] = Enumerable.Range(2, 99).ToArray(),
                ["xor"] = [2],
                ["floor"] = [1],
                ["ceil"] = [1],
                ["round"] = [1, 2],
                ["deg"] = [1],
                ["rad"] = [1]
            };

        /// <summary>
        /// 计算公式。
        /// </summary>
        /// <param name="expression">公式字符串，例如 <c>round((X1 + X2) / 2, 3)</c>。</param>
        /// <param name="variables">变量字典，例如 <c>X1=1.23</c>、<c>X2=2.34</c>。</param>
        /// <param name="result">成功时返回计算结果，失败时返回 0。</param>
        /// <param name="errorMessage">失败时返回错误说明。</param>
        /// <returns>计算成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public bool TryEvaluate(string expression, IReadOnlyDictionary<string, double> variables, out double result, out string errorMessage)
        {
            result = 0;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(expression))
            {
                errorMessage = "公式不能为空";
                return false;
            }

            try
            {
                var parser = new Parser(expression, variables);
                result = parser.ParseExpression();
                parser.EnsureCompleted();

                if (double.IsNaN(result) || double.IsInfinity(result))
                {
                    errorMessage = "公式计算结果无效";
                    result = 0;
                    return false;
                }

                return true;
            }
            catch (FormulaEvaluationException ex)
            {
                errorMessage = ex.Message;
                result = 0;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _expression;
            private readonly IReadOnlyDictionary<string, double> _variables;
            private int _position;

            public Parser(string expression, IReadOnlyDictionary<string, double> variables)
            {
                _expression = expression;
                _variables = variables;
            }

            public double ParseExpression()
            {
                return ParseLogicalOr();
            }

            private double ParseLogicalOr()
            {
                var value = ParseLogicalAnd();
                while (true)
                {
                    SkipWhiteSpace();
                    if (MatchString("||"))
                    {
                        value = ToBooleanNumber(IsTrue(value) || IsTrue(ParseLogicalAnd()));
                        continue;
                    }

                    return value;
                }
            }

            private double ParseLogicalAnd()
            {
                var value = ParseBitwiseOr();
                while (true)
                {
                    SkipWhiteSpace();
                    if (MatchString("&&"))
                    {
                        value = ToBooleanNumber(IsTrue(value) && IsTrue(ParseBitwiseOr()));
                        continue;
                    }

                    return value;
                }
            }

            private double ParseBitwiseOr()
            {
                var value = ParseBitwiseAnd();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('|'))
                    {
                        if (Match('|'))
                        {
                            _position--;
                            return value;
                        }

                        value = ToIntegerResult(value) | ToIntegerResult(ParseBitwiseAnd());
                        continue;
                    }

                    return value;
                }
            }

            private double ParseBitwiseAnd()
            {
                var value = ParseShift();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('&'))
                    {
                        if (Match('&'))
                        {
                            _position--;
                            return value;
                        }

                        value = ToIntegerResult(value) & ToIntegerResult(ParseShift());
                        continue;
                    }

                    return value;
                }
            }

            private double ParseShift()
            {
                var value = ParseAddSubtract();
                while (true)
                {
                    SkipWhiteSpace();
                    if (MatchString("<<"))
                    {
                        value = ToIntegerResult(value) << checked((int)ToIntegerResult(ParseAddSubtract()));
                        continue;
                    }

                    if (MatchString(">>"))
                    {
                        value = ToIntegerResult(value) >> checked((int)ToIntegerResult(ParseAddSubtract()));
                        continue;
                    }

                    return value;
                }
            }

            private double ParseAddSubtract()
            {
                var value = ParseTerm();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('+'))
                    {
                        value += ParseTerm();
                        continue;
                    }

                    if (Match('-'))
                    {
                        value -= ParseTerm();
                        continue;
                    }

                    return value;
                }
            }

            public void EnsureCompleted()
            {
                SkipWhiteSpace();
                if (!IsEnd)
                {
                    throw new FormulaEvaluationException($"无法识别的表达式片段：{_expression[_position..]}");
                }
            }

            private double ParseTerm()
            {
                var value = ParsePower();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('*'))
                    {
                        value *= ParsePower();
                        continue;
                    }

                    if (Match('/'))
                    {
                        var divisor = ParsePower();
                        if (Math.Abs(divisor) <= double.Epsilon)
                        {
                            throw new FormulaEvaluationException("除数不能为 0");
                        }

                        value /= divisor;
                        continue;
                    }

                    if (Match('%'))
                    {
                        var divisor = ParsePower();
                        if (Math.Abs(divisor) <= double.Epsilon)
                        {
                            throw new FormulaEvaluationException("取模除数不能为 0");
                        }

                        value %= divisor;
                        continue;
                    }

                    return value;
                }
            }

            private double ParsePower()
            {
                var value = ParseUnary();
                SkipWhiteSpace();
                if (Match('^'))
                {
                    value = Math.Pow(value, ParsePower());
                }

                return value;
            }

            private double ParseUnary()
            {
                SkipWhiteSpace();
                if (Match('+'))
                {
                    return ParseUnary();
                }

                if (Match('-'))
                {
                    return -ParseUnary();
                }

                if (Match('!'))
                {
                    return ToBooleanNumber(!IsTrue(ParseUnary()));
                }

                if (Match('~'))
                {
                    return ~ToIntegerResult(ParseUnary());
                }

                return ParsePrimary();
            }

            private double ParsePrimary()
            {
                SkipWhiteSpace();
                if (Match('('))
                {
                    var value = ParseExpression();
                    Expect(')');
                    return value;
                }

                if (char.IsDigit(Current) || Current == '.')
                {
                    return ParseNumber();
                }

                if (char.IsLetter(Current) || Current == '_')
                {
                    return ParseIdentifier();
                }

                throw new FormulaEvaluationException($"位置 {_position + 1} 附近存在无效字符 '{Current}'");
            }

            private double ParseNumber()
            {
                var start = _position;
                while (!IsEnd && (char.IsDigit(Current) || Current == '.'))
                {
                    _position++;
                }

                if (!double.TryParse(_expression[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw new FormulaEvaluationException($"无法解析数字：{_expression[start.._position]}");
                }

                return value;
            }

            private double ParseIdentifier()
            {
                var name = ReadIdentifier();
                SkipWhiteSpace();
                if (Match('('))
                {
                    var arguments = new List<double>();
                    SkipWhiteSpace();
                    if (!Match(')'))
                    {
                        do
                        {
                            arguments.Add(ParseExpression());
                            SkipWhiteSpace();
                        }
                        while (Match(','));

                        Expect(')');
                    }

                    return EvaluateFunction(name, arguments.ToArray());
                }

                if (string.Equals(name, "PI", StringComparison.OrdinalIgnoreCase))
                {
                    return Math.PI;
                }

                if (string.Equals(name, "E", StringComparison.OrdinalIgnoreCase))
                {
                    return Math.E;
                }

                if (_variables.TryGetValue(name, out var value))
                {
                    return value;
                }

                throw new FormulaEvaluationException($"未找到公式变量：{name}");
            }

            private double EvaluateFunction(string name, double[] args)
            {
                if (!Functions.TryGetValue(name, out var function))
                {
                    throw new FormulaEvaluationException($"不支持的函数：{name}");
                }

                if (FunctionArgCounts.TryGetValue(name, out var supportedArgCounts) && !supportedArgCounts.Contains(args.Length))
                {
                    throw new FormulaEvaluationException($"函数 {name} 的参数个数不正确");
                }

                try
                {
                    return function(args);
                }
                catch (Exception ex)
                {
                    throw new FormulaEvaluationException($"函数 {name} 计算失败：{ex.Message}");
                }
            }

            private string ReadIdentifier()
            {
                var start = _position;
                while (!IsEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
                {
                    _position++;
                }

                return _expression[start.._position];
            }

            private void Expect(char expected)
            {
                SkipWhiteSpace();
                if (!Match(expected))
                {
                    throw new FormulaEvaluationException($"位置 {_position + 1} 处应为 '{expected}'");
                }
            }

            private bool Match(char ch)
            {
                if (!IsEnd && Current == ch)
                {
                    _position++;
                    return true;
                }

                return false;
            }

            private bool MatchString(string text)
            {
                if (_position + text.Length > _expression.Length)
                {
                    return false;
                }

                if (string.Compare(_expression, _position, text, 0, text.Length, StringComparison.Ordinal) == 0)
                {
                    _position += text.Length;
                    return true;
                }

                return false;
            }

            private void SkipWhiteSpace()
            {
                while (!IsEnd && char.IsWhiteSpace(Current))
                {
                    _position++;
                }
            }

            private bool IsEnd => _position >= _expression.Length;

            private char Current => IsEnd ? '\0' : _expression[_position];
        }

        private sealed class FormulaEvaluationException : Exception
        {
            public FormulaEvaluationException(string message) : base(message)
            {
            }
        }

        private static bool IsTrue(double value)
        {
            return Math.Abs(value) > double.Epsilon;
        }

        private static double ToBooleanNumber(bool value)
        {
            return value ? 1d : 0d;
        }

        private static long ToIntegerResult(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new FormulaEvaluationException("位运算要求参与计算的值必须是有效数值");
            }

            return checked((long)Math.Truncate(value));
        }
    }
}
