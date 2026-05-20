using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.Geometry;

namespace BlockCatalogPlugin.UI
{
    /// <summary>
    /// 目录生成预览对话框
    /// </summary>
    public class CatalogPreviewDialog : Form
    {
        private Button btnConfirm;
        private Button btnCancel;
        private Button btnStyleSettings;
        private Label lblInfo;
        private Label lblDimensions;

        private List<AttributeBlockData> _blocks;
        private CatalogStyle _style;
        private MergeConfig _mergeConfig;
        private Point3d? _insertPoint;
        private List<ColumnWidthConfig> _customWidths = new List<ColumnWidthConfig>();

        // 预览比例（像素到CAD单位的转换）
        private double _previewScale = 2.0; // 1mm = 2px

        public bool GenerateClicked { get; private set; }
        public CatalogStyle ResultStyle => _style;
        public MergeConfig ResultMergeConfig => _mergeConfig;
        public Point3d? ResultInsertPoint => _insertPoint;
        public List<ColumnWidthConfig> CustomWidths => _customWidths;

        private static class Theme
        {
            public static Color Bg => ThemeConfig.Bg;
            public static Color Card => ThemeConfig.Card;
            public static Color InputBg => ThemeConfig.InputBg;
            public static Color Text => ThemeConfig.Text;
            public static Color TextBright => ThemeConfig.TextBright;
            public static Color TextDim => ThemeConfig.TextDim;
            public static Color Accent => ThemeConfig.Accent;
            public static Color Success => ThemeConfig.Success;
            public static Color Warning => ThemeConfig.Warning;
            public static Color Border => ThemeConfig.Border;
        }

        public CatalogPreviewDialog(List<AttributeBlockData> blocks, CatalogStyle style, MergeConfig mergeConfig = null, Point3d? insertPoint = null)
        {
            _blocks = blocks ?? new List<AttributeBlockData>();
            _style = style ?? new CatalogStyle();
            _mergeConfig = mergeConfig ?? new MergeConfig { EnableMerge = false };
            _insertPoint = insertPoint;

            // Load custom widths from style
            if (_style.Columns != null)
            {
                foreach (var col in _style.Columns)
                {
                    _customWidths.Add(new ColumnWidthConfig { Tag = col.Tag, Width = col.Width });
                }
            }

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F);

            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(700, 550);
            Text = "目录预览";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(500, 450);

            int topHeight = 420;

            // 预览区 - 使用 Panel + Graphics 绘制
            var pnlPreview = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(ClientSize.Width - 20, topHeight),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true
            };
            pnlPreview.Paint += PnlPreview_Paint;
            pnlPreview.Resize += (s, e) => pnlPreview.Invalidate();
            Controls.Add(pnlPreview);
            _previewPanel = pnlPreview;

            var lblPreviewTitle = new Label
            {
                Text = "表格预览",
                Location = new Point(10, 5),
                AutoSize = true,
                ForeColor = Theme.Text,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            pnlPreview.Controls.Add(lblPreviewTitle);

            // 尺寸信息标签
            lblDimensions = new Label
            {
                Text = "",
                Location = new Point(80, 8),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            pnlPreview.Controls.Add(lblDimensions);

            // 样式设置按钮
            btnStyleSettings = new Button
            {
                Text = "样式设置",
                Location = new Point(10, topHeight + 15),
                Width = 100,
                Height = 28,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnStyleSettings.FlatAppearance.BorderSize = 0;
            btnStyleSettings.Click += BtnStyleSettings_Click;
            Controls.Add(btnStyleSettings);

            // 信息标签
            lblInfo = new Label
            {
                Text = $"共 {_blocks.Count} 行数据",
                Location = new Point(120, topHeight + 20),
                AutoSize = true,
                ForeColor = Theme.TextDim
            };
            Controls.Add(lblInfo);

            // 底部按钮
            int bottomY = ClientSize.Height - 50;

            btnConfirm = new Button
            {
                Text = "确认",
                Location = new Point(ClientSize.Width - 220, bottomY),
                Width = 100,
                Height = 32,
                BackColor = Theme.Success,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += BtnConfirm_Click;
            Controls.Add(btnConfirm);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(ClientSize.Width - 110, bottomY),
                Width = 90,
                Height = 32,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { GenerateClicked = false; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnConfirm;
            CancelButton = btnCancel;

            // 初始绘制预览
            UpdatePreview();
        }

        private Panel _previewPanel;

        private void UpdatePreview()
        {
            if (_previewPanel == null) return;

            // 计算预览尺寸
            var displayBlocks = _blocks;
            if (_mergeConfig != null && _mergeConfig.EnableMerge)
            {
                var generator = new CatalogGenerator();
                displayBlocks = generator.ApplyMerge(_blocks, _mergeConfig);
            }

            var visibleCols = _style.Columns.Where(c => c.Visible).ToList();
            int colCount = visibleCols.Count + 1;

            // 自动计算列宽（基于内容）
            double[] colWidths = CalculateAutoColumnWidths(displayBlocks, visibleCols);

            // 更新customWidths以便后续使用
            _customWidths.Clear();
            _customWidths.Add(new ColumnWidthConfig { Tag = "序号", Width = colWidths[0] });
            for (int c = 1; c < colCount; c++)
            {
                var tag = visibleCols[c - 1]?.Tag ?? "";
                _customWidths.Add(new ColumnWidthConfig { Tag = tag, Width = colWidths[c] });
            }

            double totalWidth = colWidths.Sum();
            double headerHeight = _style.HeaderHeight > 0 ? _style.HeaderHeight : _style.RowHeight * 1.2;
            int rowCount = displayBlocks.Count;
            double totalHeight = headerHeight + rowCount * _style.RowHeight;

            // 更新尺寸标签
            lblDimensions.Text = $"宽: {totalWidth:F1}mm | 高: {totalHeight:F1}mm | {displayBlocks.Count} 行";
            lblInfo.Text = $"共 {displayBlocks.Count} 行数据 | 自动调整列宽";

            // 设置Panel滚动区域
            int pixelWidth = (int)(totalWidth * _previewScale) + 40;
            int pixelHeight = (int)(totalHeight * _previewScale) + 40;
            _previewPanel.AutoScrollMargin = new Size(pixelWidth, pixelHeight);

            _previewPanel.Invalidate();
        }

        private double GetColumnWidth(string tag, double defaultWidth)
        {
            var custom = _customWidths.FirstOrDefault(c => c.Tag == tag);
            return custom?.Width ?? defaultWidth;
        }

        /// <summary>
        /// 根据内容自动计算列宽（标题+数据取最大值）
        /// </summary>
        private double[] CalculateAutoColumnWidths(List<AttributeBlockData> displayBlocks, List<ColumnDef> visibleCols)
        {
            int colCount = visibleCols.Count + 1;
            double[] widths = new double[colCount];

            // 序号列：基于最大序号宽度
            string maxSeq = FormatSeqNum(displayBlocks.Count, _style.SeqFormat);
            widths[0] = Math.Max(GetColumnWidth("序号", 20), EstimateTextWidth(maxSeq, 9) + 10);

            // 数据列：根据标题和数据内容计算
            using (var g = _previewPanel?.CreateGraphics())
            {
                for (int c = 1; c < colCount; c++)
                {
                    var tag = visibleCols[c - 1]?.Tag ?? "";
                    var header = visibleCols[c - 1]?.Header ?? tag;
                    double baseWidth = visibleCols[c - 1]?.Width ?? 40;

                    // 标题宽度
                    double headerWidth = EstimateTextWidth(header, 10) + 10;

                    // 数据最大宽度
                    double dataWidth = headerWidth;
                    foreach (var block in displayBlocks)
                    {
                        string value = block.GetAttribute(tag) ?? "";
                        double vw = EstimateTextWidth(value, 8) + 6;
                        if (vw > dataWidth) dataWidth = vw;
                    }

                    // 取自定义宽度和自动宽度的较大值
                    double customWidth = GetColumnWidth(tag, baseWidth);
                    widths[c] = Math.Max(customWidth, Math.Min(dataWidth, 150)); // 最大150mm
                }
            }

            return widths;
        }

        private double EstimateTextWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 10;
            // 粗略估算：中文字符约1.4倍字高，英文约0.6倍
            double chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FA5);
            double asciiChars = text.Length - chineseChars;
            return chineseChars * fontSize * 1.4 + asciiChars * fontSize * 0.6;
        }

        private void PnlPreview_Paint(object sender, PaintEventArgs e)
        {
            if (e.Graphics == null) return;

            var g = e.Graphics;
            g.Clear(Color.White);

            // 应用滚动偏移
            var savedTransform = g.Transform;
            if (_previewPanel != null)
            {
                g.TranslateTransform(_previewPanel.AutoScrollPosition.X, _previewPanel.AutoScrollPosition.Y);
            }

            try
            {
                var displayBlocks = _blocks;
                if (_mergeConfig != null && _mergeConfig.EnableMerge)
                {
                    var generator = new CatalogGenerator();
                    displayBlocks = generator.ApplyMerge(_blocks, _mergeConfig);
                }

                var visibleCols = _style.Columns.Where(c => c.Visible).ToList();
                int colCount = visibleCols.Count + 1;

                // 自动计算列宽（基于内容）
                double[] colWidths = CalculateAutoColumnWidths(displayBlocks, visibleCols);

                double totalWidth = colWidths.Sum();
                double headerHeight = _style.HeaderHeight > 0 ? _style.HeaderHeight : _style.RowHeight * 1.2;
                int rowCount = displayBlocks.Count;
                double totalHeight = headerHeight + rowCount * _style.RowHeight;

                double startX = 20;
                double startY = 20;

                // 缩放比例
                double scale = _previewScale;

                using (var borderPen = new Pen(Color.Black, 1))
                using (var gridPen = new Pen(Color.LightGray, 0.5f))
                using (var headerBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
                using (var textBrush = new SolidBrush(Color.Black))
                using (var headerFont = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold))
                using (var dataFont = new Font("Microsoft YaHei UI", 7F))
                {
                    // 绘制外边框
                    g.DrawRectangle(borderPen,
                        (float)(startX * scale),
                        (float)(startY * scale),
                        (float)(totalWidth * scale),
                        (float)(totalHeight * scale));

                    // 绘制垂直分割线
                    double x = startX;
                    for (int c = 0; c <= colCount; c++)
                    {
                        if (c > 0) x += colWidths[c - 1];
                        if (c > 0 && c < colCount)
                        {
                            float px = (float)(x * scale);
                            g.DrawLine(gridPen, px, (float)(startY * scale), px, (float)((startY - totalHeight) * scale));
                        }
                    }

                    // 绘制水平分割线（表头与内容之间）
                    double y = startY - headerHeight;
                    g.DrawLine(gridPen, (float)(startX * scale), (float)(y * scale),
                        (float)((startX + totalWidth) * scale), (float)(y * scale));

                    // 行分隔线
                    double rowY = startY - headerHeight;
                    for (int r = 0; r < rowCount; r++)
                    {
                        rowY -= _style.RowHeight;
                        if (r < rowCount - 1)
                        {
                            g.DrawLine(gridPen, (float)(startX * scale), (float)(rowY * scale),
                                (float)((startX + totalWidth) * scale), (float)(rowY * scale));
                        }
                    }

                    // 绘制表头背景
                    g.FillRectangle(headerBrush,
                        (float)(startX * scale),
                        (float)(startY * scale),
                        (float)(totalWidth * scale),
                        (float)(headerHeight * scale));

                    // 绘制表头文字
                    double textHeight = _style.RowHeight * 0.8;
                    y = startY - textHeight * 0.5;
                    double textY = startY - headerHeight / 2;

                    // 序号列标题
                    DrawCellText(g, "序号", startX + 2, textY, colWidths[0], textHeight, headerFont, textBrush, scale);

                    x = startX;
                    for (int c = 1; c < colCount; c++)
                    {
                        x += colWidths[c - 1];
                        string header = visibleCols[c - 1]?.Header ?? visibleCols[c - 1]?.Tag ?? "";
                        DrawCellText(g, header, x + 2, textY, colWidths[c], textHeight, headerFont, textBrush, scale);
                    }

                    // 绘制数据行
                    double rowTextY = startY - headerHeight - textHeight * 0.5;
                    int seqNum = 1;

                    foreach (var block in displayBlocks)
                    {
                        x = startX;

                        // 序号
                        string seqStr = FormatSeqNum(seqNum++, _style.SeqFormat);
                        DrawCellText(g, seqStr, x + 2, rowTextY, colWidths[0], textHeight, dataFont, textBrush, scale);

                        // 属性列
                        for (int c = 1; c < colCount; c++)
                        {
                            x += colWidths[c - 1];
                            var tag = visibleCols[c - 1]?.Tag;
                            string value = tag != null ? block.GetAttribute(tag) ?? "" : "";
                            DrawCellText(g, value, x + 2, rowTextY, colWidths[c], textHeight, dataFont, textBrush, scale);
                        }

                        rowTextY -= _style.RowHeight;
                    }
                }
            }
            finally
            {
                g.Transform = savedTransform;
            }
        }

        private void DrawCellText(Graphics g, string text, double x, double y, double colWidth, double textHeight, Font font, Brush brush, double scale)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 裁剪文本如果超出列宽
            float maxWidth = (float)(colWidth * scale) - 4;
            if (maxWidth <= 0) return;

            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                RectangleF rect = new RectangleF(
                    (float)(x * scale),
                    (float)((y - textHeight * 0.5) * scale),
                    maxWidth,
                    (float)(textHeight * scale));

                g.DrawString(text, font, brush, rect, sf);
            }
        }

        private string FormatSeqNum(int num, SeqFormatConfig format)
        {
            if (format == null) return num.ToString();

            string result = (format.Template ?? "{n}")
                .Replace("{n}", num.ToString())
                .Replace("{nn}", num.ToString("D2"))
                .Replace("{nnn}", num.ToString("D3"))
                .Replace("{c}", IndexToCircle(num))
                .Replace("{c1}", IndexToChinese1(num))
                .Replace("{cc}", IndexToCircle(num));

            return (format.Prefix ?? "") + result + (format.Suffix ?? "");
        }

        private string IndexToCircle(int num)
        {
            string[] circle = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩", "⑪", "⑫", "⑬", "⑭", "⑮", "⑯", "⑰", "⑱", "⑲", "⑳" };
            return (num >= 1 && num <= 20) ? circle[num - 1] : num.ToString();
        }

        private string IndexToChinese1(int num)
        {
            string[] c1 = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (num <= 0) return c1[0];
            if (num <= 10) return c1[num];
            if (num < 20) return "十" + (num == 10 ? "" : c1[num - 10]);
            if (num < 100) return c1[num / 10] + "十" + (num % 10 == 0 ? "" : c1[num % 10]);
            return num.ToString();
        }

        private void BtnStyleSettings_Click(object sender, EventArgs e)
        {
            using (var dlg = new StyleSettingsDialog(_style, _customWidths))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _style = dlg.ResultStyle;
                    // 应用自定义列宽到预览
                    if (dlg.CustomWidths != null)
                    {
                        _customWidths = dlg.CustomWidths;
                    }
                    UpdatePreview();
                }
            }
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            GenerateClicked = true;
            Close();
        }

        /// <summary>
        /// 获取自定义列宽配置
        /// </summary>
        public List<ColumnWidthConfig> GetCustomColumnWidths()
        {
            return _customWidths;
        }

        /// <summary>
        /// 应用自定义列宽
        /// </summary>
        public void ApplyCustomWidths(List<ColumnWidthConfig> widths)
        {
            if (widths == null) return;
            _customWidths = new List<ColumnWidthConfig>(widths);
        }
    }

    /// <summary>
    /// 表格样式设置对话框
    /// </summary>
    public class TableStyleDialog : Form
    {
        private ComboBox cmbFontName;
        private NumericUpDown numFontHeight;
        private NumericUpDown numRowHeight;
        private NumericUpDown numHeaderHeight;
        private CheckBox chkSmartMerge;
        private ComboBox cmbMergeCriterion;
        private Button btnOK;
        private Button btnCancel;

        private CatalogStyle _style;
        public CatalogStyle ResultStyle { get; private set; }

        private static class Theme
        {
            public static Color Bg => ThemeConfig.Bg;
            public static Color Card => ThemeConfig.Card;
            public static Color InputBg => ThemeConfig.InputBg;
            public static Color Text => ThemeConfig.Text;
            public static Color TextDim => ThemeConfig.TextDim;
            public static Color Success => ThemeConfig.Success;
        }

        public TableStyleDialog(CatalogStyle style)
        {
            _style = style ?? new CatalogStyle();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(320, 300);
            Text = "表格样式设置";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9F);

            int y = 15;

            // 字体设置
            var lblFont = new Label { Text = "字体:", Location = new Point(15, y), AutoSize = true, ForeColor = Theme.Text };
            Controls.Add(lblFont);

            cmbFontName = new ComboBox
            {
                Location = new Point(100, y),
                Width = 180,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFontName.Items.AddRange(new object[] { "宋体", "黑体", "楷体", "仿宋", "Arial", "Times New Roman" });
            cmbFontName.SelectedItem = _style.FontName;
            Controls.Add(cmbFontName);
            y += 30;

            // 字高
            var lblFontHeight = new Label { Text = "字高:", Location = new Point(15, y), AutoSize = true, ForeColor = Theme.Text };
            Controls.Add(lblFontHeight);

            numFontHeight = new NumericUpDown
            {
                Location = new Point(100, y),
                Width = 80,
                Minimum = 1,
                Maximum = 20,
                DecimalPlaces = 1,
                Value = (decimal)_style.FontHeight,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            Controls.Add(numFontHeight);
            y += 30;

            // 行高
            var lblRowHeight = new Label { Text = "行高:", Location = new Point(15, y), AutoSize = true, ForeColor = Theme.Text };
            Controls.Add(lblRowHeight);

            numRowHeight = new NumericUpDown
            {
                Location = new Point(100, y),
                Width = 80,
                Minimum = 2,
                Maximum = 50,
                DecimalPlaces = 1,
                Value = (decimal)_style.RowHeight,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            Controls.Add(numRowHeight);
            y += 30;

            // 表头高度
            var lblHeaderHeight = new Label { Text = "表头高:", Location = new Point(15, y), AutoSize = true, ForeColor = Theme.Text };
            Controls.Add(lblHeaderHeight);

            numHeaderHeight = new NumericUpDown
            {
                Location = new Point(100, y),
                Width = 80,
                Minimum = 2,
                Maximum = 50,
                DecimalPlaces = 1,
                Value = (decimal)_style.HeaderHeight,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            Controls.Add(numHeaderHeight);
            y += 35;

            // 合并选项区域
            var lblMergeTitle = new Label
            {
                Text = "智能合并选项",
                Location = new Point(15, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            Controls.Add(lblMergeTitle);
            y += 20;

            chkSmartMerge = new CheckBox
            {
                Text = "启用智能合并",
                Location = new Point(15, y),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Bg,
                Checked = _style.SmartMergeEnabled
            };
            Controls.Add(chkSmartMerge);
            y += 25;

            var lblCriterion = new Label { Text = "合并依据:", Location = new Point(15, y), AutoSize = true, ForeColor = Theme.Text };
            Controls.Add(lblCriterion);

            cmbMergeCriterion = new ComboBox
            {
                Location = new Point(100, y),
                Width = 180,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMergeCriterion.Items.AddRange(new object[] { "图号前缀", "图名相同", "前缀+图名", "幅面相同", "比例相同" });
            cmbMergeCriterion.SelectedIndex = 0;
            Controls.Add(cmbMergeCriterion);
            y += 40;

            // 按钮
            btnOK = new Button
            {
                Text = "确定",
                Location = new Point(100, y),
                Width = 90,
                Height = 28,
                BackColor = Theme.Success,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += BtnOK_Click;
            Controls.Add(btnOK);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(200, y),
                Width = 90,
                Height = 28,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => Close();
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            ResultStyle = new CatalogStyle
            {
                FontName = cmbFontName.SelectedItem?.ToString() ?? "宋体",
                FontHeight = (double)numFontHeight.Value,
                RowHeight = (double)numRowHeight.Value,
                HeaderHeight = (double)numHeaderHeight.Value,
                SmartMergeEnabled = chkSmartMerge.Checked,
                Columns = _style.Columns,
                SeqFormat = _style.SeqFormat
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}