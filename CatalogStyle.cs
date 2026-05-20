using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockCatalogPlugin
{
    /// <summary>
    /// 合并策略
    /// </summary>
    public enum MergeStrategy
    {
        None,
        PrefixConsecutive,
        NameConsecutive
    }

    /// <summary>
    /// 序号格式（简单枚举，供 UI 选择）
    /// </summary>
    public enum SequenceFormat
    {
        Numeric,       // 1,2,3
        Numeric2Digit, // 01,02,03
        Circle,        // ①,②,③
        Chinese,       // 一,二,三
        Alpha          // A,B,C
    }

    /// <summary>
    /// 序号格式配置（兼容 Generate 方法的字符串模板格式）
    /// </summary>
    public class SeqFormatConfig
    {
        public string Template { get; set; } = "{n}";
        public int StartNum { get; set; } = 1;
        public int Step { get; set; } = 1;
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";
    }

    /// <summary>
    /// 列定义
    /// </summary>
    public class ColumnDef
    {
        public string Tag { get; set; }
        public string Header { get; set; }  // 显示名，默认等于 Tag
        public double Width { get; set; } = 40;
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    /// <summary>
    /// 列配置（兼容 StyleTemplateManager）
    /// </summary>
    public class ColumnConfig
    {
        public string Tag { get; set; } = "";
        public string Header { get; set; } = "";
        public double Width { get; set; } = 40;
        public bool Visible { get; set; } = true;
    }

    /// <summary>
    /// 列宽配置（兼容 StyleTemplateManager）
    /// </summary>
    public class ColumnWidthConfig
    {
        public string Tag { get; set; }
        public double Width { get; set; }
    }

    /// <summary>
    /// 目录样式配置
    /// </summary>
    public class CatalogStyle
    {
        public List<ColumnDef> Columns { get; set; } = new List<ColumnDef>();
        public double RowHeight { get; set; } = 5.0;
        public MergeStrategy MergeStrategy { get; set; } = MergeStrategy.None;
        public SequenceFormat SequenceFormat { get; set; } = SequenceFormat.Numeric;
        public bool HeaderVisible { get; set; } = true;

        // 兼容字段（供 Generate/GenerateTable 使用）
        public string FontName { get; set; } = "宋体";
        public double FontHeight { get; set; } = 3.5;
        public double HeaderHeight { get; set; } = 10.0;
        public bool UseMouseDefineSize { get; set; } = false;
        public bool DrawBorder { get; set; } = true;
        public bool SmartMergeEnabled { get; set; } = false;
        public SeqFormatConfig SeqFormat { get; set; } = new SeqFormatConfig { Template = "{n}", StartNum = 1, Step = 1 };

        /// <summary>
        /// 通过公式字符串批量设置可见列宽度
        /// 格式如 "16+31+65+20"
        /// </summary>
        /// <param name="formulaExpress">宽度公式表达式，用加号分隔</param>
        public void ApplyFormulaWidths(string formulaExpress)
        {
            if (string.IsNullOrEmpty(formulaExpress) || Columns == null)
                return;

            // 解析公式字符串
            var parts = formulaExpress.Split('+');
            var widths = new List<double>();

            foreach (var part in parts)
            {
                if (double.TryParse(part.Trim(), out double width))
                {
                    widths.Add(width);
                }
                else
                {
                    // 解析失败，使用默认值
                    widths.Add(30.0);
                }
            }

            // 获取可见列（按Order排序）
            var visibleCols = Columns
                .Where(c => c.Visible)
                .OrderBy(c => c.Order)
                .ToList();

            // 应用宽度到可见列
            for (int i = 0; i < visibleCols.Count && i < widths.Count; i++)
            {
                visibleCols[i].Width = widths[i];
            }
            // 如果宽度数量少于可见列数量，其余列保持原宽度不变
        }

        /// <summary>
        /// 获取当前可见列的宽度公式字符串
        /// </summary>
        public string GetFormulaWidths()
        {
            if (Columns == null) return "";

            var visibleCols = Columns
                .Where(c => c.Visible)
                .OrderBy(c => c.Order)
                .ToList();

            return string.Join("+", visibleCols.Select(c => c.Width.ToString("F0")));
        }
    }
}