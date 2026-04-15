using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.IO;
using System.Xml;

namespace MeasurementSoftware.Helpers
{
    /// <summary>
    /// 公式脚本语法高亮管理器。
    /// 统一创建 AvalonEdit 使用的高亮规则，便于后续维护和复用。
    /// </summary>
    public static class FormulaScriptHighlightingManager
    {
        private static IHighlightingDefinition? _highlighting;

        /// <summary>
        /// 获取公式脚本高亮定义。
        /// </summary>
        public static IHighlightingDefinition GetOrCreate()
        {
            if (_highlighting != null)
            {
                return _highlighting;
            }

            const string xshd = """
                <SyntaxDefinition name="FormulaScript" extensions=".formula" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                  <Color name="Comment" foreground="#5F8F3A" />
                  <Color name="Keyword" foreground="#005FB8" fontWeight="bold" />
                  <Color name="Function" foreground="#8A2BE2" fontWeight="bold" />
                  <Color name="Operator" foreground="#C43E00" fontWeight="bold" />
                  <Color name="Number" foreground="#008B5E" />
                  <Color name="Constant" foreground="#C2185B" fontWeight="bold" />

                  <RuleSet ignoreCase="true">
                    <Span color="Comment" begin="//" end="$" />
                    <Span color="Comment" begin="#" end="$" />

                    <Keywords color="Keyword">
                      <Word>RESULT</Word>
                    </Keywords>

                    <Keywords color="Constant">
                      <Word>PI</Word>
                      <Word>E</Word>
                    </Keywords>

                    <Keywords color="Function">
                      <Word>sin</Word>
                      <Word>cos</Word>
                      <Word>tan</Word>
                      <Word>asin</Word>
                      <Word>acos</Word>
                      <Word>atan</Word>
                      <Word>abs</Word>
                      <Word>sqrt</Word>
                      <Word>exp</Word>
                      <Word>ln</Word>
                      <Word>log</Word>
                      <Word>log10</Word>
                      <Word>pow</Word>
                      <Word>min</Word>
                      <Word>max</Word>
                      <Word>floor</Word>
                      <Word>ceil</Word>
                      <Word>round</Word>
                      <Word>deg</Word>
                      <Word>rad</Word>
                      <Word>xor</Word>
                    </Keywords>

                    <Rule color="Operator">\+|\-|\*|/|%|\^|\||\|\||&amp;|&amp;&amp;|&lt;&lt;|&gt;&gt;|~|!|\(|\)|,|=</Rule>
                    <Rule color="Number">\b\d+(?:\.\d+)?\b</Rule>
                  </RuleSet>
                </SyntaxDefinition>
                """;

            using var reader = XmlReader.Create(new StringReader(xshd));
            _highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            return _highlighting;
        }
    }
}
