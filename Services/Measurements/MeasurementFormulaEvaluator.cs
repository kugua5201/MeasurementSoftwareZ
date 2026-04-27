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
    /// <item><description>额外支持逻辑/位运算符：<c>||</c>、<c>&amp;&amp;</c>、<c>!</c>、<c>|</c>、<c>&amp;</c>、<c>~</c>、<c>&lt;&lt;</c>、<c>&gt;&gt;</c>，其中逻辑结果统一返回 <c>1</c>/<c>0</c>。</description></item>
    /// </list>
    /// 当前解析器采用“递归下降 + 逐字符硬解析”的方式实现，不会先把整段公式拆成独立 Token 列表，
    /// 而是直接维护一个当前位置指针，从左到右边读边判断：
    /// <list type="number">
    /// <item><description>先从最低优先级入口 <c>ParseLogicalOr</c> 开始解析。</description></item>
    /// <item><description>每一层只处理自己这一层的运算符，其操作数交给更高优先级的方法继续解析。</description></item>
    /// <item><description>遇到括号时递归重新解析一整段子表达式。</description></item>
    /// <item><description>遇到标识符时，再区分它是函数、常量还是变量。</description></item>
    /// <item><description>最终在 <c>EnsureCompleted</c> 中确认整段公式已经被完整消费，没有剩余脏字符。</description></item>
    /// </list>
    /// 运算符优先级从低到高如下：
    /// <list type="number">
    /// <item><description><c>||</c> 逻辑或</description></item>
    /// <item><description><c>&amp;&amp;</c> 逻辑与</description></item>
    /// <item><description><c>|</c> 按位或</description></item>
    /// <item><description><c>&amp;</c> 按位与</description></item>
    /// <item><description><c>&lt;&lt;</c>、<c>&gt;&gt;</c> 移位</description></item>
    /// <item><description><c>+</c>、<c>-</c> 加减</description></item>
    /// <item><description><c>*</c>、<c>/</c>、<c>%</c> 乘除模</description></item>
    /// <item><description><c>^</c> 幂运算（右结合）</description></item>
    /// <item><description><c>+</c>、<c>-</c>、<c>!</c>、<c>~</c> 一元运算</description></item>
    /// <item><description>括号、数字、变量、常量、函数调用</description></item>
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
            /// <summary>
            /// 原始公式文本。
            /// 整个解析过程不做词法对象缓存，而是直接基于字符串和当前位置进行“硬解析”。
            /// </summary>
            private readonly string _expression;

            /// <summary>
            /// 外部传入的变量表，例如 X1、温度、ResultA 等。
            /// 当标识符既不是函数也不是内置常量时，会从这里取值。
            /// </summary>
            private readonly IReadOnlyDictionary<string, double> _variables;

            /// <summary>
            /// 当前解析指针位置。
            /// 解析器会随着 Match/ReadIdentifier/ParseNumber 等方法逐字符向后推进。
            /// </summary>
            private int _position;

            public Parser(string expression, IReadOnlyDictionary<string, double> variables)
            {
                _expression = expression;
                _variables = variables;
            }

            public double ParseExpression()
            {
                // 表达式总入口。
                // 当前按“逻辑或 -> 逻辑与 -> 位或 -> 位与 -> 移位 -> 加减 -> 乘除模 -> 幂 -> 一元 -> 基础项”逐层下降解析。
                return ParseLogicalOr();
            }

            /// <summary>
            /// 负责什么：解析逻辑或这一层。
            /// 支持哪些符号：<c>||</c>。
            /// 为什么优先级在这里：逻辑或优先级最低，所以放在最外层作为总入口，保证右侧更复杂的表达式会先由更高优先级层算完。
            /// 例子：<c>X1 || X2 || X3</c>。
            /// </summary>
            private double ParseLogicalOr()
            {
                var value = ParseLogicalAnd();
                while (true)
                {
                    SkipWhiteSpace();
                    if (MatchString("||"))
                    {
                        // 逻辑运算统一把非 0 视为 true，结果再转回 1/0，便于继续参与后续数值计算。
                        value = ToBooleanNumber(IsTrue(value) || IsTrue(ParseLogicalAnd()));
                        continue;
                    }

                    return value;
                }
            }

            /// <summary>
            /// 负责什么：解析逻辑与这一层。
            /// 支持哪些符号：<c>&amp;&amp;</c>。
            /// 为什么优先级在这里：逻辑与比逻辑或更紧，但仍然要晚于位运算和普通数学运算。
            /// 例子：<c>X1 &amp;&amp; X2</c>。
            /// </summary>
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

            /// <summary>
            /// 负责什么：解析按位或这一层。
            /// 支持哪些符号：<c>|</c>。
            /// 为什么优先级在这里：按位或高于逻辑运算，但低于按位与、移位和普通算术运算；同时这里还要避开 <c>||</c>，避免误把逻辑或当成位或。
            /// 例子：<c>X1 | X2</c>。
            /// </summary>
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
                            // 如果其实是 ||，说明这一层不该继续消费，回退一个字符交给上层 ParseLogicalOr 处理。
                            _position--;
                            return value;
                        }

                        // 位运算前先把 double 截断为整数再计算。
                        value = ToIntegerResult(value) | ToIntegerResult(ParseBitwiseAnd());
                        continue;
                    }

                    return value;
                }
            }

            /// <summary>
            /// 负责什么：解析按位与这一层。
            /// 支持哪些符号：<c>&amp;</c>。
            /// 为什么优先级在这里：按位与比按位或更紧，但仍然晚于移位和数学运算；同时需要避开 <c>&amp;&amp;</c>，避免和逻辑与冲突。
            /// 例子：<c>X1 &amp; X2</c>。
            /// </summary>
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
                            // 如果实际是逻辑与，则回退一个字符，让上层逻辑与解析器处理。
                            _position--;
                            return value;
                        }

                        value = ToIntegerResult(value) & ToIntegerResult(ParseShift());
                        continue;
                    }

                    return value;
                }
            }

            /// <summary>
            /// 负责什么：解析移位运算这一层。
            /// 支持哪些符号：<c>&lt;&lt;</c>、<c>&gt;&gt;</c>。
            /// 为什么优先级在这里：移位比按位与/或更紧，但又比加减更松，符合常见表达式求值习惯；这里的两侧都会先截断成整数后再移位。
            /// 例子：<c>X1 &lt;&lt; 2</c>、<c>X2 &gt;&gt; 1</c>。
            /// </summary>
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

            /// <summary>
            /// 负责什么：解析双目加减这一层。
            /// 支持哪些符号：<c>+</c>、<c>-</c>。
            /// 为什么优先级在这里：加减要晚于乘除模和幂运算，所以这里先取左项，再把右侧每一项交给更高优先级的 <c>ParseTerm</c> 去完成。
            /// 例子：<c>X1 + X2 - X3</c>。
            /// </summary>
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
                    // 公式正常计算完后，理论上应该正好走到结尾；如果还有残留，说明有无法识别的片段。
                    throw new FormulaEvaluationException($"无法识别的表达式片段：{_expression[_position..]}");
                }
            }

            /// <summary>
            /// 负责什么：解析乘除模这一层。
            /// 支持哪些符号：<c>*</c>、<c>/</c>、<c>%</c>。
            /// 为什么优先级在这里：乘除模必须先于加减，但仍然要晚于幂运算，因此这里每次都把左右操作数交给 <c>ParsePower</c> 解析。
            /// 例子：<c>X1 * 2 / X2 % 3</c>。
            /// </summary>
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

            /// <summary>
            /// 负责什么：解析幂运算这一层。
            /// 支持哪些符号：<c>^</c>。
            /// 为什么优先级在这里：幂运算高于乘除模和加减，但仍然低于一元运算和最基础值；这里采用右结合，所以 <c>a^b^c</c> 会按 <c>a^(b^c)</c> 处理。
            /// 例子：<c>X1 ^ 2</c>、<c>2 ^ 3 ^ 2</c>。
            /// </summary>
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

            /// <summary>
            /// 负责什么：解析一元运算这一层。
            /// 支持哪些符号：<c>+</c>、<c>-</c>、<c>!</c>、<c>~</c>。
            /// 为什么优先级在这里：一元运算必须比双目运算更早绑定到后面的值上，所以会在进入最基础项之前先处理。
            /// 例子：<c>-X1</c>、<c>!X2</c>、<c>~X3</c>。
            /// </summary>
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

            /// <summary>
            /// 负责什么：解析最基础的值单元。
            /// 支持哪些符号：<c>(</c>、<c>)</c>，以及数字、变量、常量、函数调用。
            /// 为什么优先级在这里：这是所有表达式的叶子节点，其他层最终都会落到这里；括号也在这一层触发递归，从而强制改变默认优先级。
            /// 例子：<c>(X1 + X2)</c>、<c>123.45</c>、<c>PI</c>、<c>round(X1, 2)</c>。
            /// </summary>
            private double ParsePrimary()
            {
                SkipWhiteSpace();
                if (Match('('))
                {
                    // 括号会强制提升内部表达式的优先级。
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

            /// <summary>
            /// 负责什么：从当前位置连续读取一个数字字面量。
            /// 支持哪些符号：数字字符和小数点 <c>.</c>。
            /// 为什么优先级在这里：数字本身已经是最小不可再拆的值，所以作为基础项的一个分支单独读取。
            /// 例子：<c>123</c>、<c>45.67</c>。
            /// </summary>
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

            /// <summary>
            /// 负责什么：解析名字型内容，也就是标识符。
            /// 支持哪些符号：字母、数字、下划线组成的变量名、常量名、函数名，以及函数后的 <c>(</c>、<c>)</c>、<c>,</c>。
            /// 为什么优先级在这里：标识符和数字一样，都是基础项的一种；只有先读出完整名字，才能继续判断它到底是函数、常量还是变量。
            /// 例子：<c>X1</c>、<c>PI</c>、<c>max(X1, X2, X3)</c>。
            /// </summary>
            private double ParseIdentifier()
            {
                var name = ReadIdentifier();
                SkipWhiteSpace();
                if (Match('('))
                {
                    // 函数参数按逗号分隔，每个参数本身仍然可以是完整表达式。
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

                // 内置数学常量。
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

            /// <summary>
            /// 负责什么：执行已经解析完成的函数调用。
            /// 支持哪些符号：这里不再直接消费符号，而是处理函数名和参数数组。
            /// 为什么优先级在这里：函数的参数已经在前面的解析过程中按完整表达式算好，这里只负责做函数合法性校验和最终计算。
            /// 例子：<c>round(12.3456, 2)</c>、<c>max(X1, X2, X3)</c>。
            /// </summary>
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

            /// <summary>
            /// 从当前位置读取完整标识符。
            /// 标识符规则：首字符由外层保证，后续允许字母、数字、下划线。
            /// </summary>
            private string ReadIdentifier()
            {
                var start = _position;
                while (!IsEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
                {
                    _position++;
                }

                return _expression[start.._position];
            }

            /// <summary>
            /// 要求当前位置必须是指定字符。
            /// 常用于强校验右括号、参数结束符等固定结构。
            /// </summary>
            private void Expect(char expected)
            {
                SkipWhiteSpace();
                if (!Match(expected))
                {
                    throw new FormulaEvaluationException($"位置 {_position + 1} 处应为 '{expected}'");
                }
            }

            /// <summary>
            /// 匹配单个字符并推进位置。
            /// 失败时不移动指针。
            /// </summary>
            private bool Match(char ch)
            {
                if (!IsEnd && Current == ch)
                {
                    _position++;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 匹配固定字符串并推进位置。
            /// 用于处理双字符运算符，例如 ||、&&、<<、>>。
            /// </summary>
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

            /// <summary>
            /// 跳过空白字符。
            /// 这样用户在公式里写空格、制表符不会影响解析结果。
            /// </summary>
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
            /// <summary>
            /// 公式解析或计算过程中的业务异常。
            /// 统一用这个异常把内部错误转换成对用户可读的提示信息。
            /// </summary>
            public FormulaEvaluationException(string message) : base(message)
            {
            }
        }

        /// <summary>
        /// 将数值转为逻辑真假。
        /// 当前规则：绝对值大于 0 视为真，等于 0 视为假。
        /// </summary>
        private static bool IsTrue(double value)
        {
            return Math.Abs(value) > double.Epsilon;
        }

        /// <summary>
        /// 将布尔结果重新映射为数值。
        /// 公式内部统一用 1 表示真，0 表示假，便于继续参与数值表达式。
        /// </summary>
        private static double ToBooleanNumber(bool value)
        {
            return value ? 1d : 0d;
        }

        /// <summary>
        /// 位运算前的统一转换。
        /// 这里会先截断小数部分，再转成 long；如果是 NaN/Infinity 会直接报错。
        /// </summary>
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
