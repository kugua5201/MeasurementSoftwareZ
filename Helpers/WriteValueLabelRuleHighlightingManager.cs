using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.IO;
using System.Xml;

namespace MeasurementSoftware.Helpers
{
    /// <summary>
    /// 写入点位 Label 规则脚本高亮管理器。
    /// </summary>
    public static class WriteValueLabelRuleHighlightingManager
    {
        private static IHighlightingDefinition? _highlighting;

        public static IHighlightingDefinition Definition => GetOrCreate();

        public static IHighlightingDefinition GetOrCreate()
        {
            if (_highlighting != null)
            {
                return _highlighting;
            }

            const string xshd = """
                <SyntaxDefinition name="WriteValueLabelRule" extensions=".wvlr" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                  <Color name="Comment" foreground="#5F8F3A" />
                  <Color name="Keyword" foreground="#005FB8" fontWeight="bold" />
                  <Color name="Operator" foreground="#C43E00" fontWeight="bold" />
                  <Color name="Number" foreground="#008B5E" />
                  <Color name="Text" foreground="#7A1FA2" />

                  <RuleSet ignoreCase="true">
                    <Span color="Comment" begin="//" end="$" />
                    <Span color="Comment" begin="#" end="$" />

                    <Keywords color="Keyword">
                      <Word>default</Word>
                      <Word>else</Word>
                    </Keywords>

                    <Rule color="Operator">&gt;=|&lt;=|&gt;|&lt;|=</Rule>
                    <Rule color="Number">(?&lt;!\w)-?\d+(?:\.\d+)?(?!\w)</Rule>
                    <Span color="Text" begin="=&quot;" end="&quot;" />
                  </RuleSet>
                </SyntaxDefinition>
                """;

            using var reader = XmlReader.Create(new StringReader(xshd));
            _highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            return _highlighting;
        }
    }
}
