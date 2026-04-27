using System.IO;
using System.Windows.Controls;

namespace MeasurementSoftware.UserControls
{
    /// <summary>
    /// 公式函数说明展示控件。
    /// 用于在弹窗中直接展示 Docs/间接测量公式函数说明.md 的内容。
    /// </summary>
    public partial class FormulaFunctionHelpUserControl : UserControl
    {
        public FormulaFunctionHelpUserControl()
        {
            InitializeComponent();
            LoadMarkdownContent();
        }

        private void LoadMarkdownContent()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "间接测量公式函数说明.md");
            if (!File.Exists(filePath))
            {
                var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null)
                {
                    filePath = Path.Combine(directory.FullName, "Docs", "间接测量公式函数说明.md");
                    if (File.Exists(filePath))
                    {
                        break;
                    }

                    directory = directory.Parent;
                }
            }

            HelpContentTextBox.Text = File.Exists(filePath)
                ? File.ReadAllText(filePath)
                : "未找到公式函数说明文档。";
        }
    }
}
