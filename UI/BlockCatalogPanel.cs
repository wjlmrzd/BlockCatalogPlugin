using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BlockCatalogPlugin;
using Font = System.Drawing.Font;
using FlowLayoutPanel = System.Windows.Forms.FlowLayoutPanel;
using FlowDirection = System.Windows.Forms.FlowDirection;

namespace BlockCatalogPlugin.UI
{
    // Theme alias for ThemeConfig
    internal static class Theme
    {
        internal static Color Bg => ThemeConfig.BgAlt;
        internal static Color Card => ThemeConfig.CardAlt;
        internal static Color CardHover => ThemeConfig.CardHover;
        internal static Color Border => ThemeConfig.BorderAlt;
        internal static Color Primary => ThemeConfig.Primary;
        internal static Color PrimaryLight => ThemeConfig.PrimaryLight;
        internal static Color Accent => ThemeConfig.Accent;
        internal static Color AccentLight => ThemeConfig.AccentLight;
        internal static Color Text => ThemeConfig.Text;
        internal static Color TextBright => ThemeConfig.TextBright;
        internal static Color TextDim => ThemeConfig.TextDim;
        internal static Color InputBg => ThemeConfig.InputBgAlt;
        internal static Color LogBg => ThemeConfig.LogBg;
        internal static Color Success => ThemeConfig.SuccessAlt;
        internal static Color Warning => ThemeConfig.WarningAlt;
        internal static Color Error => ThemeConfig.Error;
    }

    public class BlockCatalogPanel : UserControl
    {
        private BlockExtractor _extractor = new BlockExtractor();
        private CatalogGenerator _generator = new CatalogGenerator();
        private SortEngine _sortEngine = new SortEngine();
        private AttributeModifier _modifier = new AttributeModifier();
        private FrameSelector _frameSelector = new FrameSelector();
        private SuffixPatternEngine _suffixEngine = new SuffixPatternEngine();

        private ExtractionResult _currentResult;
        private List<string> _selectedBlockNames = new List<string>();
        private List<string> _selectedTags = new List<string>();
        private CatalogStyle _currentStyle = new CatalogStyle();
        private UserPreferences _preferences;

        private RichTextBox rtbLog;

        // 看板式UI控件
        private TableLayoutPanel _rootLayout;
        private DataGridView dgvBlocks;
        private RadioButton radCatalogMode;
        private RadioButton radEditMode;
        private RadioButton radSortLR_TB;
        private RadioButton radSortTB_LR;
        private RadioButton radSortSelection;
        private RadioButton radSortNumeric;
        private CheckBox chkReverse;
        private TextBox txtSuffixStart;
        private TextBox txtSuffixLength;
        private TextBox txtSuffixPrefix;
        private TextBox txtSuffixSuffix;
        private CheckBox chkSuffixContinuous;
        private TextBox txtColumnFormula;
        private NumericUpDown numFontHeight;
        private NumericUpDown numRowHeight;
        private CheckBox chkShowHeader;
        private ComboBox cmbLayoutName;
        private RadioButton radModelSpace;
        private RadioButton radLayout;
        private TextBox txtSpacingExpression;
        private ComboBox cmbBlockNameFilter;
        private Button btnReset;
        private Button btnImport;
        private Button btnExport;

        // 缓存的 GDI 资源（避免在 Paint 事件中重复创建）
        private static Font _headerTitleFont;
        private static Font _headerVerFont;
        private static Brush _headerTitleBrush;
        private static Brush _headerVerBrush;
        private static Font _tabFontNormal;
        private static Font _tabFontBold;
        private static Brush _tabSelectedBrush;
        private static Brush _tabNormalBrush;
        private static Brush _tabTextSelectedBrush;
        private static Brush _tabTextNormalBrush;
        private static StringFormat _tabStringFormat;

        // Drag-drop state for DataGridView
        private int _dragRowIndex = -1;
        private Point _dragStartPoint;
        private bool _isDragging = false;

        public BlockCatalogPanel()
        {
            _preferences = PreferencesManager.Instance.Load();
            _currentStyle = LoadStyleFromPreferences();
            EnsureCachedResources();

            InitializeComponents();
        }

        private static void EnsureCachedResources()
        {
            _headerTitleFont ??= new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            _headerVerFont ??= new Font("Microsoft YaHei UI", 8F);
            _headerTitleBrush ??= new SolidBrush(Theme.TextBright);
            _headerVerBrush ??= new SolidBrush(Theme.TextDim);
            _tabFontNormal ??= new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            _tabFontBold ??= new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            _tabSelectedBrush ??= new SolidBrush(Theme.Card);
            _tabNormalBrush ??= new SolidBrush(Theme.Bg);
            _tabTextSelectedBrush ??= new SolidBrush(Theme.TextBright);
            _tabTextNormalBrush ??= new SolidBrush(Theme.TextDim);
            _tabStringFormat ??= new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        }

        /// <summary>
        /// 释放静态 GDI 资源（在插件卸载时调用）
        /// </summary>
        public static void ReleaseCachedResources()
        {
            try { _headerTitleFont?.Dispose(); } catch { }
            try { _headerVerFont?.Dispose(); } catch { }
            try { _headerTitleBrush?.Dispose(); } catch { }
            try { _headerVerBrush?.Dispose(); } catch { }
            try { _tabFontNormal?.Dispose(); } catch { }
            try { _tabFontBold?.Dispose(); } catch { }
            try { _tabSelectedBrush?.Dispose(); } catch { }
            try { _tabNormalBrush?.Dispose(); } catch { }
            try { _tabTextSelectedBrush?.Dispose(); } catch { }
            try { _tabTextNormalBrush?.Dispose(); } catch { }
            try { _tabStringFormat?.Dispose(); } catch { }

            _headerTitleFont = null;
            _headerVerFont = null;
            _headerTitleBrush = null;
            _headerVerBrush = null;
            _tabFontNormal = null;
            _tabFontBold = null;
            _tabSelectedBrush = null;
            _tabNormalBrush = null;
            _tabTextSelectedBrush = null;
            _tabTextNormalBrush = null;
            _tabStringFormat = null;
        }

        private CatalogStyle LoadStyleFromPreferences()
        {
            return new CatalogStyle
            {
                RowHeight = _preferences.TableRowHeight,
                MergeStrategy = MergeStrategy.PrefixConsecutive,
                SequenceFormat = SequenceFormat.Numeric,
                HeaderVisible = true,
                Columns = new List<ColumnDef>
                {
                    new ColumnDef { Tag = "XH", Header = "序号", Width = 20, Visible = true, Order = 0 },
                    new ColumnDef { Tag = "TH", Header = "图号", Width = 40, Visible = true, Order = 1 },
                    new ColumnDef { Tag = "TM", Header = "图名", Width = 60, Visible = true, Order = 2 }
                }
            };
        }

        private void InitializeComponents()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            AutoScroll = false;
            Padding = new Padding(0);

            // 根布局：3行（Header 56px, Content *, Log 120px）
            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Theme.Bg,
                Padding = new Padding(0)
            };
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

            // === Header ===
            var pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            pnlHeader.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.DrawString("图框目录工具", _headerTitleFont, _headerTitleBrush, 12, 14);
                g.DrawString("看板式 V3.0", _headerVerFont, _headerVerBrush, 12, 34);
            };
            _rootLayout.Controls.Add(pnlHeader, 0, 0);

            // === Content: 3列布局 (25% | 45% | 30%) ===
            var contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 3,
                BackColor = Theme.Bg,
                Padding = new Padding(4)
            };
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            // Column 0: 总控与排序区
            contentPanel.Controls.Add(CreateControlPanel(), 0, 0);

            // Column 1: 图形缓冲区 + 缀参数区
            contentPanel.Controls.Add(CreateBufferPanel(), 1, 0);

            // Column 2: 目录输出区
            contentPanel.Controls.Add(CreateOutputPanel(), 2, 0);

            _rootLayout.Controls.Add(contentPanel, 0, 1);

            // === Log Panel ===
            var logPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            var logHeader = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Theme.Card };
            logHeader.Controls.Add(new Label
            {
                Text = "操作日志",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold),
                ForeColor = Theme.TextDim,
                Padding = new Padding(6, 4, 0, 0)
            });
            var btnClear = new Button
            {
                Text = "清空",
                Dock = DockStyle.Right,
                Width = 45,
                BackColor = Theme.Card,
                ForeColor = Theme.TextDim,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 7F),
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => rtbLog?.Clear();
            logHeader.Controls.Add(btnClear);
            logPanel.Controls.Add(logHeader);

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.LogBg,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 7.5F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            logPanel.Controls.Add(rtbLog);
            _rootLayout.Controls.Add(logPanel, 0, 2);

            Controls.Add(_rootLayout);
            AppendLog("准备就绪 - 看板式界面已加载", Theme.TextDim);
        }

        /// <summary>
        /// 创建总控与排序区（左侧栏）
        /// </summary>
        private Panel CreateControlPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(4),
                ColumnCount = 1,
                RowCount = 4
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 工作模式 GroupBox
            var grpMode = new GroupBox
            {
                Text = "工作模式",
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 16, 8, 8)
            };

            radCatalogMode = new RadioButton
            {
                Text = "生成目录",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Checked = true,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpMode.Controls.Add(radCatalogMode);

            radEditMode = new RadioButton
            {
                Text = "更改图号",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpMode.Controls.Add(radEditMode);
            panel.Controls.Add(grpMode, 0, 0);

            // 排序模式 GroupBox
            var grpSort = new GroupBox
            {
                Text = "排序模式",
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 16, 8, 8)
            };

            radSortLR_TB = new RadioButton
            {
                Text = "左右 → 上下",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpSort.Controls.Add(radSortLR_TB);

            radSortTB_LR = new RadioButton
            {
                Text = "上下 → 左右",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Checked = true,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpSort.Controls.Add(radSortTB_LR);

            radSortSelection = new RadioButton
            {
                Text = "选择顺序",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpSort.Controls.Add(radSortSelection);

            radSortNumeric = new RadioButton
            {
                Text = "数值顺序",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpSort.Controls.Add(radSortNumeric);

            chkReverse = new CheckBox
            {
                Text = "反序",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                Padding = new Padding(0, 4, 0, 4)
            };
            grpSort.Controls.Add(chkReverse);
            panel.Controls.Add(grpSort, 0, 1);

            // 操作按钮 FlowLayoutPanel
            var btnActionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Theme.Bg,
                Padding = new Padding(0, 8, 0, 0)
            };

            var btnSelect = CreateFlatButton("框选图块", 90, Theme.Primary);
            btnSelect.Click += (s, e) => ExecuteCommand("_BCSELECT");
            btnActionPanel.Controls.Add(btnSelect);

            var btnSmart = CreateFlatButton("智能提取", 80, Theme.AccentLight);
            btnSmart.Click += (s, e) => ExecuteCommand("_BCSMARTEXTRACT");
            btnActionPanel.Controls.Add(btnSmart);

            var btnClearData = CreateFlatButton("清除数据", 80, Theme.Warning);
            btnClearData.Click += (s, e) => ClearData();
            btnActionPanel.Controls.Add(btnClearData);

            panel.Controls.Add(btnActionPanel, 0, 2);

            // 重置/导入/导出按钮 FlowLayoutPanel
            var btnSettingsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = Theme.Bg,
                Padding = new Padding(0, 8, 0, 0)
            };

            btnReset = CreateFlatButton("重置", 55, Theme.Warning);
            btnReset.Height = 24;
            btnReset.Click += (s, e) => ResetPanel();
            btnSettingsPanel.Controls.Add(btnReset);

            btnImport = CreateFlatButton("导入", 55, Theme.Primary);
            btnImport.Height = 24;
            btnImport.Click += (s, e) => ImportSettings();
            btnSettingsPanel.Controls.Add(btnImport);

            btnExport = CreateFlatButton("导出", 55, Theme.AccentLight);
            btnExport.Height = 24;
            btnExport.Click += (s, e) => ExportSettings();
            btnSettingsPanel.Controls.Add(btnExport);

            panel.Controls.Add(btnSettingsPanel, 0, 3);

            return panel;
        }

        /// <summary>
        /// 创建图形缓冲区 + 缀参数区（中间栏）- 流式布局
        /// </summary>
        private Panel CreateBufferPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(4),
                ColumnCount = 1,
                RowCount = 2
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

            // 上部：图形缓冲区 GroupBox
            var grpBuffer = new GroupBox
            {
                Text = "图形缓冲区",
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(4)
            };

            // 内部 TableLayoutPanel：下拉框 + DataGridView + 右侧按钮
            var bufferLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 1
            };
            bufferLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bufferLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));

            // 图框块名下拉去重选择框
            cmbBlockNameFilter = new ComboBox
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            cmbBlockNameFilter.Items.Add("(全部)");
            cmbBlockNameFilter.SelectedIndex = 0;
            cmbBlockNameFilter.SelectedIndexChanged += (s, e) => FilterBlocksByName();

            // DataGridView
            dgvBlocks = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Theme.Card,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 25,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical
            };
            dgvBlocks.EnableHeadersVisualStyles = false;
            dgvBlocks.ColumnHeadersDefaultCellStyle.BackColor = Theme.CardHover;
            dgvBlocks.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextBright;
            dgvBlocks.DefaultCellStyle.BackColor = Theme.Card;
            dgvBlocks.DefaultCellStyle.ForeColor = Theme.Text;
            dgvBlocks.AlternatingRowsDefaultCellStyle.BackColor = Theme.Card;
            dgvBlocks.AllowDrop = true;

            // DataGridView 拖拽排序支持
            dgvBlocks.MouseDown += DgvBlocks_MouseDown;
            dgvBlocks.MouseMove += DgvBlocks_MouseMove;
            dgvBlocks.MouseUp += DgvBlocks_MouseUp;
            dgvBlocks.DragOver += DgvBlocks_DragOver;
            dgvBlocks.DragDrop += DgvBlocks_DragDrop;

            // 右侧小按钮 FlowLayoutPanel
            var btnActionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Card
            };

            var btnPick = CreateFlatButton("选块", 42, Theme.Primary);
            btnPick.Height = 24;
            btnPick.Click += (s, e) => ExecuteCommand("_BCSELECT");
            btnActionPanel.Controls.Add(btnPick);

            var btnRemove = CreateFlatButton("删块", 42, Theme.Warning);
            btnRemove.Height = 24;
            btnRemove.Click += (s, e) => RemoveSelectedBlock();
            btnActionPanel.Controls.Add(btnRemove);

            var btnMoveUp = CreateFlatButton("▲", 42, Theme.Primary);
            btnMoveUp.Height = 24;
            btnMoveUp.Click += (s, e) => MoveBlockUp();
            btnActionPanel.Controls.Add(btnMoveUp);

            var btnMoveDown = CreateFlatButton("▼", 42, Theme.Primary);
            btnMoveDown.Height = 24;
            btnMoveDown.Click += (s, e) => MoveBlockDown();
            btnActionPanel.Controls.Add(btnMoveDown);

            // 左侧：下拉框 + DataGridView
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Theme.Card
            };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftPanel.Controls.Add(cmbBlockNameFilter, 0, 0);
            leftPanel.Controls.Add(dgvBlocks, 0, 1);

            bufferLayout.Controls.Add(leftPanel, 0, 0);
            bufferLayout.Controls.Add(btnActionPanel, 1, 0);
            grpBuffer.Controls.Add(bufferLayout);
            panel.Controls.Add(grpBuffer, 0, 0);

            // 下部：缀参数重编设置 GroupBox
            var grpSuffix = new GroupBox
            {
                Text = "缀参数重编",
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(4)
            };

            // 内部 FlowLayoutPanel 实现自动折行
            var suffixFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = true,
                BackColor = Theme.Card,
                AutoScroll = false
            };

            // 第一行：缀始 | 缀长 | 连续
            var row1 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            var lblStart = new Label { Text = "缀始:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F) };
            row1.Controls.Add(lblStart);
            txtSuffixStart = new TextBox { Width = 50, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F), Text = "1" };
            row1.Controls.Add(txtSuffixStart);
            var lblLen = new Label { Text = "缀长:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F), Margin = new Padding(8, 0, 0, 0) };
            row1.Controls.Add(lblLen);
            txtSuffixLength = new TextBox { Width = 40, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F), Text = "2" };
            row1.Controls.Add(txtSuffixLength);
            chkSuffixContinuous = new CheckBox { Text = "连续", AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card, Checked = true, Margin = new Padding(8, 0, 0, 0) };
            row1.Controls.Add(chkSuffixContinuous);

            // 第二行：前缀 | 后缀
            var row2 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            var lblPrefix = new Label { Text = "前缀:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F) };
            row2.Controls.Add(lblPrefix);
            txtSuffixPrefix = new TextBox { Width = 80, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F) };
            row2.Controls.Add(txtSuffixPrefix);
            var lblSuffix = new Label { Text = "后缀:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F), Margin = new Padding(8, 0, 0, 0) };
            row2.Controls.Add(lblSuffix);
            txtSuffixSuffix = new TextBox { Width = 70, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F) };
            row2.Controls.Add(txtSuffixSuffix);

            // 第三行：按钮
            var row3 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            var btnApplySuffix = CreateFlatButton("应用缀编号", 90, Theme.Success);
            btnApplySuffix.Height = 26;
            btnApplySuffix.Click += (s, e) => ApplySuffixRename();
            row3.Controls.Add(btnApplySuffix);
            var btnPreviewSuffix = CreateFlatButton("预览", 60, Theme.Primary);
            btnPreviewSuffix.Height = 26;
            btnPreviewSuffix.Click += (s, e) => PreviewSuffixRename();
            row3.Controls.Add(btnPreviewSuffix);

            suffixFlow.Controls.Add(row1);
            suffixFlow.Controls.Add(row2);
            suffixFlow.Controls.Add(row3);
            grpSuffix.Controls.Add(suffixFlow);
            panel.Controls.Add(grpSuffix, 0, 1);

            return panel;
        }

        /// <summary>
        /// 创建目录输出区（右侧栏）
        /// </summary>
        private Panel CreateOutputPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(4),
                ColumnCount = 1,
                RowCount = 5
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 列宽公式 GroupBox
            var grpColWidth = new GroupBox
            {
                Text = "列宽公式",
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(4)
            };

            var colWidthFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Card,
                AutoSize = true
            };

            txtColumnFormula = new TextBox
            {
                Width = 200,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8F),
                Text = "20+40+60"
            };
            colWidthFlow.Controls.Add(txtColumnFormula);

            var btnRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            var btnApplyFormula = CreateFlatButton("应用", 55, Theme.Primary);
            btnApplyFormula.Height = 22;
            btnApplyFormula.Click += (s, e) => ApplyColumnFormula();
            btnRow.Controls.Add(btnApplyFormula);

            var btnGetFormula = CreateFlatButton("获取", 55, Theme.Accent);
            btnGetFormula.Height = 22;
            btnGetFormula.Click += (s, e) => txtColumnFormula.Text = _currentStyle.GetFormulaWidths();
            btnRow.Controls.Add(btnGetFormula);
            colWidthFlow.Controls.Add(btnRow);

            grpColWidth.Controls.Add(colWidthFlow);
            panel.Controls.Add(grpColWidth, 0, 0);

            // 表格样式 GroupBox
            var grpStyle = new GroupBox
            {
                Text = "表格样式",
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(4)
            };

            var styleFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = true,
                BackColor = Theme.Card,
                AutoSize = true
            };

            // 第一行：字高 + 行高
            var row1 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            var lblFontH = new Label { Text = "字高:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F) };
            row1.Controls.Add(lblFontH);
            numFontHeight = new NumericUpDown { Width = 55, Minimum = 1, Maximum = 20, Value = 3.5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            row1.Controls.Add(numFontHeight);
            var lblRowH = new Label { Text = "行高:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F), Margin = new Padding(10, 0, 0, 0) };
            row1.Controls.Add(lblRowH);
            numRowHeight = new NumericUpDown { Width = 55, Minimum = 1, Maximum = 50, Value = 5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            row1.Controls.Add(numRowHeight);

            // 第二行：显示表头
            var row2 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            chkShowHeader = new CheckBox { Text = "显示表头", AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card, Checked = true };
            row2.Controls.Add(chkShowHeader);

            // 第三行：输出目标
            var row3 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Card
            };
            var lblLayout = new Label { Text = "输出:", AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F) };
            row3.Controls.Add(lblLayout);
            radModelSpace = new RadioButton { Text = "模型", AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card, Checked = true };
            row3.Controls.Add(radModelSpace);
            radLayout = new RadioButton { Text = "布局", AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            row3.Controls.Add(radLayout);
            cmbLayoutName = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.InputBg, ForeColor = Theme.Text, Enabled = false };
            cmbLayoutName.Items.Add("Model");
            cmbLayoutName.SelectedIndex = 0;
            row3.Controls.Add(cmbLayoutName);
            radLayout.CheckedChanged += (s, e) => { cmbLayoutName.Enabled = radLayout.Checked; };

            styleFlow.Controls.Add(row1);
            styleFlow.Controls.Add(row2);
            styleFlow.Controls.Add(row3);
            grpStyle.Controls.Add(styleFlow);
            panel.Controls.Add(grpStyle, 0, 1);

            // 间距表达式
            var grpSpacing = new GroupBox
            {
                Text = "间距表达式",
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(4)
            };

            txtSpacingExpression = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8F),
                Text = "5"
            };
            grpSpacing.Controls.Add(txtSpacingExpression);
            panel.Controls.Add(grpSpacing, 0, 2);

            // 输出按钮区
            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Bg
            };

            var btnPreview = CreateFlatButton("预览目录", 120, Theme.Primary);
            btnPreview.Height = 30;
            btnPreview.Click += (s, e) => ShowPreview();
            btnFlow.Controls.Add(btnPreview);

            var btnGenerate = CreateFlatButton("生成目录", 120, Theme.Success);
            btnGenerate.Height = 35;
            btnGenerate.Click += (s, e) => GenerateToPosition();
            btnFlow.Controls.Add(btnGenerate);

            var btnSyncAttrs = CreateFlatButton("同步至图纸属性", 120, Theme.AccentLight);
            btnSyncAttrs.Height = 30;
            btnSyncAttrs.Click += (s, e) => SyncAttributesToBlocks();
            btnFlow.Controls.Add(btnSyncAttrs);

            panel.Controls.Add(btnFlow, 0, 3);

            return panel;
        }

        private void ExecuteCommand(string command)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                AppendLog($"执行: {command}", Theme.TextDim);
                doc.SendStringToExecute(command + " ", true, false, true);
            }
        }

        private void ClearData()
        {
            _currentResult = null;
            _selectedBlockNames.Clear();
            _selectedTags.Clear();
            if (dgvBlocks != null)
                dgvBlocks.DataSource = null;
            AppendLog("已清除数据", Theme.TextDim);
        }

        private void ResetPanel()
        {
            // 重置缀参数控件
            txtSuffixStart.Text = "1";
            txtSuffixLength.Text = "2";
            txtSuffixPrefix.Text = "";
            txtSuffixSuffix.Text = "";
            chkSuffixContinuous.Checked = true;

            // 重置排序选项
            radSortTB_LR.Checked = true;
            chkReverse.Checked = false;

            // 重置表格样式控件
            numFontHeight.Value = 3.5m;
            numRowHeight.Value = 5m;
            chkShowHeader.Checked = true;
            txtColumnFormula.Text = "20+40+60";
            txtSpacingExpression.Text = "5";
            radModelSpace.Checked = true;

            // 重置下拉框
            if (cmbBlockNameFilter != null)
            {
                cmbBlockNameFilter.Items.Clear();
                cmbBlockNameFilter.Items.Add("(全部)");
                cmbBlockNameFilter.SelectedIndex = 0;
            }

            // 清除数据
            ClearData();

            AppendLog("面板已重置", Theme.Success);
        }

        private void ImportSettings()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                dlg.Title = "导入设置";
                dlg.DefaultExt = "json";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = System.IO.File.ReadAllText(dlg.FileName);
                        var settings = System.Text.Json.JsonSerializer.Deserialize<PanelSettings>(json);
                        if (settings != null)
                        {
                            ApplySettings(settings);
                            AppendLog($"已导入设置: {dlg.FileName}", Theme.Success);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"导入失败: {ex.Message}", Theme.Error);
                    }
                }
            }
        }

        private void ExportSettings()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                dlg.Title = "导出设置";
                dlg.DefaultExt = "json";
                dlg.FileName = "BlockCatalogSettings.json";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var settings = CollectSettings();
                        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        string json = System.Text.Json.JsonSerializer.Serialize(settings, options);
                        System.IO.File.WriteAllText(dlg.FileName, json);
                        AppendLog($"已导出设置: {dlg.FileName}", Theme.Success);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"导出失败: {ex.Message}", Theme.Error);
                    }
                }
            }
        }

        private void FilterBlocksByName()
        {
            if (_currentResult == null || dgvBlocks == null) return;

            string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";

            if (string.IsNullOrEmpty(selectedFilter) || selectedFilter == "(全部)")
            {
                // 显示所有块
                RefreshDataGridView();
            }
            else
            {
                // 按块名筛选
                var filteredBlocks = _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList();
                dgvBlocks.DataSource = null;
                dgvBlocks.Columns.Clear();

                int startNum = 1, numLength = 2;
                int.TryParse(txtSuffixStart.Text, out startNum);
                int.TryParse(txtSuffixLength.Text, out numLength);
                string prefix = txtSuffixPrefix.Text ?? "";
                string suffix = txtSuffixSuffix.Text ?? "";
                var previewValues = _suffixEngine.GenerateNumberSequence(filteredBlocks.Count, prefix, suffix, startNum, numLength);

                dgvBlocks.DataSource = filteredBlocks.Select((b, idx) => new
                {
                    块名 = b.BlockName,
                    图号 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                    图名 = b.GetAttribute("TM") ?? "",
                    幅面 = b.GetAttribute("FM") ?? "",
                    X = Math.Round(b.Position.X, 1),
                    Y = Math.Round(b.Position.Y, 1),
                    当前提取值 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                    重编预览值 = idx < previewValues.Count ? previewValues[idx] : ""
                }).ToList();
            }
        }

        /// <summary>
        /// 收集当前面板设置
        /// </summary>
        private PanelSettings CollectSettings()
        {
            return new PanelSettings
            {
                SuffixStart = txtSuffixStart.Text,
                SuffixLength = txtSuffixLength.Text,
                SuffixPrefix = txtSuffixPrefix.Text,
                SuffixSuffix = txtSuffixSuffix.Text,
                SuffixContinuous = chkSuffixContinuous.Checked,
                SortTypeIndex = GetSelectedSortTypeIndex(),
                Reverse = chkReverse.Checked,
                FontHeight = (double)numFontHeight.Value,
                RowHeight = (double)numRowHeight.Value,
                ShowHeader = chkShowHeader.Checked,
                ColumnFormula = txtColumnFormula.Text,
                SpacingExpression = txtSpacingExpression.Text,
                OutputToLayout = radLayout.Checked
            };
        }

        /// <summary>
        /// 应用设置到面板
        /// </summary>
        private void ApplySettings(PanelSettings settings)
        {
            if (settings == null) return;

            txtSuffixStart.Text = settings.SuffixStart ?? "1";
            txtSuffixLength.Text = settings.SuffixLength ?? "2";
            txtSuffixPrefix.Text = settings.SuffixPrefix ?? "";
            txtSuffixSuffix.Text = settings.SuffixSuffix ?? "";
            chkSuffixContinuous.Checked = settings.SuffixContinuous;

            SetSelectedSortType(settings.SortTypeIndex);
            chkReverse.Checked = settings.Reverse;

            numFontHeight.Value = (decimal)(settings.FontHeight > 0 ? settings.FontHeight : 3.5);
            numRowHeight.Value = (decimal)(settings.RowHeight > 0 ? settings.RowHeight : 5);
            chkShowHeader.Checked = settings.ShowHeader;
            txtColumnFormula.Text = settings.ColumnFormula ?? "20+40+60";
            txtSpacingExpression.Text = settings.SpacingExpression ?? "5";

            if (settings.OutputToLayout)
                radLayout.Checked = true;
            else
                radModelSpace.Checked = true;
        }

        private int GetSelectedSortTypeIndex()
        {
            if (radSortLR_TB.Checked) return 0;
            if (radSortTB_LR.Checked) return 1;
            if (radSortSelection.Checked) return 2;
            if (radSortNumeric.Checked) return 3;
            return 1;
        }

        private void SetSelectedSortType(int index)
        {
            radSortLR_TB.Checked = index == 0;
            radSortTB_LR.Checked = index == 1;
            radSortSelection.Checked = index == 2;
            radSortNumeric.Checked = index == 3;
        }

        /// <summary>
        /// 动态识别当前图纸中的有效图号标签（TH > 图号 > XH）
        /// </summary>
        private string GetActiveTag()
        {
            if (_currentResult != null && _currentResult.Blocks.Count > 0)
            {
                var firstBlock = _currentResult.Blocks[0];
                if (firstBlock.GetAttribute("TH") != null) return "TH";
                if (firstBlock.GetAttribute("图号") != null) return "图号";
            }
            return "XH"; // 缺省默认
        }

        /// <summary>
        /// 面板设置数据结构（用于导入/导出）
        /// </summary>
        private class PanelSettings
        {
            public string SuffixStart { get; set; } = "1";
            public string SuffixLength { get; set; } = "2";
            public string SuffixPrefix { get; set; } = "";
            public string SuffixSuffix { get; set; } = "";
            public bool SuffixContinuous { get; set; } = true;
            public int SortTypeIndex { get; set; } = 1;
            public bool Reverse { get; set; } = false;
            public double FontHeight { get; set; } = 3.5;
            public double RowHeight { get; set; } = 5;
            public bool ShowHeader { get; set; } = true;
            public string ColumnFormula { get; set; } = "20+40+60";
            public string SpacingExpression { get; set; } = "5";
            public bool OutputToLayout { get; set; } = false;
        }

        private void RemoveSelectedBlock()
        {
            if (dgvBlocks.SelectedRows.Count > 0 && _currentResult != null)
            {
                int idx = dgvBlocks.SelectedRows[0].Index;
                if (idx >= 0 && idx < _currentResult.Blocks.Count)
                {
                    _currentResult.Blocks.RemoveAt(idx);
                    RefreshDataGridView();
                    AppendLog($"已删除第 {idx + 1} 行", Theme.Warning);
                }
            }
        }

        private void MoveBlockUp()
        {
            if (dgvBlocks.SelectedRows.Count > 0 && _currentResult != null)
            {
                int idx = dgvBlocks.SelectedRows[0].Index;
                if (idx > 0)
                {
                    var temp = _currentResult.Blocks[idx];
                    _currentResult.Blocks[idx] = _currentResult.Blocks[idx - 1];
                    _currentResult.Blocks[idx - 1] = temp;
                    RefreshDataGridView();
                    dgvBlocks.Rows[idx - 1].Selected = true;
                }
            }
        }

        private void MoveBlockDown()
        {
            if (dgvBlocks.SelectedRows.Count > 0 && _currentResult != null)
            {
                int idx = dgvBlocks.SelectedRows[0].Index;
                if (idx >= 0 && idx < _currentResult.Blocks.Count - 1)
                {
                    var temp = _currentResult.Blocks[idx];
                    _currentResult.Blocks[idx] = _currentResult.Blocks[idx + 1];
                    _currentResult.Blocks[idx + 1] = temp;
                    RefreshDataGridView();
                    dgvBlocks.Rows[idx + 1].Selected = true;
                }
            }
        }

        private void RefreshDataGridView()
        {
            if (dgvBlocks == null || _currentResult == null) return;

            dgvBlocks.DataSource = null;
            dgvBlocks.Columns.Clear();

            // 生成预览值序列
            int startNum = 1, numLength = 2;
            int.TryParse(txtSuffixStart.Text, out startNum);
            int.TryParse(txtSuffixLength.Text, out numLength);
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            var engine = _suffixEngine;
            var previewValues = engine.GenerateNumberSequence(_currentResult.Blocks.Count, prefix, suffix, startNum, numLength);

            dgvBlocks.DataSource = _currentResult.Blocks.Select((b, idx) => new
            {
                块名 = b.BlockName,
                图号 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                图名 = b.GetAttribute("TM") ?? "",
                幅面 = b.GetAttribute("FM") ?? "",
                X = Math.Round(b.Position.X, 1),
                Y = Math.Round(b.Position.Y, 1),
                当前提取值 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                重编预览值 = idx < previewValues.Count ? previewValues[idx] : ""
            }).ToList();

            // 刷新下拉框的块名列表
            RefreshBlockNameFilter();
        }

        private void RefreshBlockNameFilter()
        {
            if (cmbBlockNameFilter == null || _currentResult == null) return;

            string currentSelection = cmbBlockNameFilter.SelectedItem?.ToString() ?? "(全部)";

            cmbBlockNameFilter.Items.Clear();
            cmbBlockNameFilter.Items.Add("(全部)");

            var distinctNames = _currentResult.Blocks
                .Select(b => b.BlockName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            cmbBlockNameFilter.Items.AddRange(distinctNames.ToArray());

            // 恢复之前的选中项
            if (!string.IsNullOrEmpty(currentSelection) && cmbBlockNameFilter.Items.Contains(currentSelection))
            {
                cmbBlockNameFilter.SelectedItem = currentSelection;
            }
            else
            {
                cmbBlockNameFilter.SelectedIndex = 0;
            }
        }

        private void DgvBlocks_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var hit = dgvBlocks.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0)
                {
                    _dragRowIndex = hit.RowIndex;
                    _dragStartPoint = e.Location;
                    _isDragging = false;
                }
            }
        }

        private void DgvBlocks_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragRowIndex >= 0 && !_isDragging)
            {
                var diff = Math.Abs(e.X - _dragStartPoint.X) + Math.Abs(e.Y - _dragStartPoint.Y);
                if (diff > 5)
                {
                    _isDragging = true;
                    dgvBlocks.DoDragDrop(_dragRowIndex, DragDropEffects.Move);
                }
            }
        }

        private void DgvBlocks_MouseUp(object sender, MouseEventArgs e)
        {
            _dragRowIndex = -1;
            _isDragging = false;
        }

        private void DgvBlocks_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void DgvBlocks_DragDrop(object sender, DragEventArgs e)
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0) return;

            var hit = dgvBlocks.HitTest(dgvBlocks.PointToClient(new Point(e.X, e.Y)).X, dgvBlocks.PointToClient(new Point(e.X, e.Y)).Y);
            int targetIndex = hit.RowIndex;

            if (targetIndex < 0 || _dragRowIndex < 0 || targetIndex == _dragRowIndex) return;

            // 标准 RemoveAt/Insert 插队逻辑（去掉错误的-1修正）
            var draggedBlock = _currentResult.Blocks[_dragRowIndex];
            _currentResult.Blocks.RemoveAt(_dragRowIndex);
            _currentResult.Blocks.Insert(targetIndex, draggedBlock);

            RefreshDataGridView();

            // 选中新位置的行
            if (targetIndex >= 0 && targetIndex < dgvBlocks.Rows.Count)
            {
                dgvBlocks.Rows[targetIndex].Selected = true;
            }

            _dragRowIndex = -1;
            _isDragging = false;
        }

        private void ApplyColumnFormula()
        {
            try
            {
                _currentStyle.ApplyFormulaWidths(txtColumnFormula.Text);
                AppendLog($"已应用列宽公式: {txtColumnFormula.Text}", Theme.Success);
            }
            catch (Exception ex)
            {
                AppendLog($"列宽公式错误: {ex.Message}", Theme.Error);
            }
        }

        private void ApplySuffixRename()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("没有可重编的数据", Theme.Warning);
                return;
            }

            // 获取当前筛选范围
            string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";
            var targetBlocks = (selectedFilter != "(全部)")
                ? _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList()
                : _currentResult.Blocks;

            if (targetBlocks.Count == 0)
            {
                AppendLog("筛选范围内没有可重编的数据", Theme.Warning);
                return;
            }

            if (!int.TryParse(txtSuffixStart.Text, out int startNum)) startNum = 1;
            if (!int.TryParse(txtSuffixLength.Text, out int numLength)) numLength = 2;
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            var engine = new BlockCatalogPlugin.SuffixPatternEngine();
            bool success = engine.BulkRenameAttributes(
                targetBlocks,
                GetActiveTag(),
                prefix,
                suffix,
                startNum,
                numLength);

            if (success)
            {
                RefreshDataGridView();
                AppendLog($"缀编号已应用: {prefix}{startNum.ToString().PadLeft(numLength, '0')}~", Theme.Success);
            }
            else
            {
                AppendLog("缀编号应用失败", Theme.Error);
            }
        }

        private void PreviewSuffixRename()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("没有可预览的数据", Theme.Warning);
                return;
            }

            if (!int.TryParse(txtSuffixStart.Text, out int startNum)) startNum = 1;
            if (!int.TryParse(txtSuffixLength.Text, out int numLength)) numLength = 2;
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            var engine = new BlockCatalogPlugin.SuffixPatternEngine();
            var preview = engine.GenerateNumberSequence(_currentResult.Blocks.Count, prefix, suffix, startNum, numLength);

            AppendLog("预览: " + string.Join(", ", preview.Take(5).ToArray()) + (preview.Count > 5 ? "..." : ""), Theme.TextDim);
        }

        private void SyncAttributesToBlocks()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("没有可同步的数据", Theme.Warning);
                return;
            }

            // 获取当前筛选范围
            string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";
            var targetBlocks = (selectedFilter != "(全部)")
                ? _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList()
                : _currentResult.Blocks;

            if (targetBlocks.Count == 0)
            {
                AppendLog("筛选范围内没有可同步的数据", Theme.Warning);
                return;
            }

            if (!int.TryParse(txtSuffixStart.Text, out int startNum)) startNum = 1;
            if (!int.TryParse(txtSuffixLength.Text, out int numLength)) numLength = 2;
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            AppendLog($"正在同步属性到图纸...", Theme.TextDim);

            bool success = _suffixEngine.BulkRenameAttributes(
                targetBlocks,
                GetActiveTag(),
                prefix,
                suffix,
                startNum,
                numLength);

            if (success)
            {
                RefreshDataGridView();
                AppendLog($"属性同步完成", Theme.Success);
            }
            else
            {
                AppendLog("属性同步失败", Theme.Error);
            }
        }

        private void ShowPreview()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("请先提取属性块", Theme.Warning);
                return;
            }

            AppendLog("打开预览窗口...", Theme.TextDim);
            using (var dlg = new CatalogPreviewDialog(_currentResult.Blocks, _currentStyle, null, null))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _currentStyle = dlg.ResultStyle;
                    AppendLog("预览配置已应用", Theme.Success);
                }
            }
        }

        private void GenerateToPosition()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("请先提取属性块", Theme.Warning);
                return;
            }

            AppendLog("请在CAD中指定目录插入位置...", Theme.Primary);
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                Plugin._pendingGenerateAfterPick = true;
                doc.SendStringToExecute("_BCPICKPOS ", true, false, true);
            }
        }

        #region Event Handlers

        /// <summary>
        /// Callback when blocks are selected via CAD command
        /// </summary>
        public void OnBlocksSelected(ExtractionResult result)
        {
            _currentResult = result;
            _selectedBlockNames = result.Blocks.Select(b => b.BlockName).Distinct().ToList();
            _selectedTags = result.AllTags.ToList();

            // 刷新 DataGridView
            RefreshDataGridView();

            AppendLog($"已提取 {result.Blocks.Count} 个块", Theme.Success);
        }

        /// <summary>
        /// Insert point selected callback
        /// </summary>
        public void OnInsertPointSelected(Point3d pos)
        {
            AppendLog($"插入点已设置: ({pos.X:F1}, {pos.Y:F1})", Theme.Success);

            try
            {
                // 获取当前筛选范围
                string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";
                var targetBlocks = (selectedFilter != "(全部)")
                    ? _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList()
                    : _currentResult.Blocks;

                if (targetBlocks.Count == 0)
                {
                    AppendLog("筛选范围内没有可生成的块数据", Theme.Warning);
                    return;
                }

                // 根据排序模式获取排序后的数据（含容差和反序参数）
                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(targetBlocks, sortType, 500.0, chkReverse.Checked);

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                {
                    targetLayout = cmbLayoutName.SelectedItem.ToString();
                }

                // 将排序后的 AttributeBlockData 转换为 BlockDataResult
                var blockDataResult = new BlockDataResult();
                foreach (var attrBlock in sortedBlocks)
                {
                    var blockData = new BlockData
                    {
                        BlockName = attrBlock.BlockName,
                        ObjectId = attrBlock.BlockId,
                        Attributes = attrBlock.Attributes?.Select(kv => new BlockAttribute { Tag = kv.Key, Value = kv.Value }).ToList()
                            ?? new List<BlockAttribute>()
                    };
                    blockDataResult.Blocks.Add(blockData);
                }

                // 调用真正的 Generate 方法生成带网格线的目录实体
                var entities = _generator.Generate(blockDataResult, _currentStyle, pos, targetLayout);

                // 将实体写入 CAD 图纸数据库
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (var docLock = doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        // 获取目标空间（模型空间或布局）
                        BlockTableRecord ms;
                        if (!string.IsNullOrEmpty(targetLayout))
                        {
                            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                            var btr = (BlockTableRecord)tr.GetObject(bt[targetLayout], OpenMode.ForRead);
                            ms = (BlockTableRecord)tr.GetObject(btr.Id, OpenMode.ForWrite);
                        }
                        else
                        {
                            ms = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                        }

                        // 将所有生成的实体添加到图纸
                        foreach (var entity in entities)
                        {
                            ms.AppendEntity(entity);
                            tr.AddNewlyCreatedDBObject(entity, true);
                        }

                        tr.Commit();
                    }
                }

                AppendLog("目录已生成", Theme.Success);

                if (targetLayout != null)
                    AppendLog($"  插入到布局: {targetLayout}", Theme.TextDim);
            }
            catch (Exception ex)
            {
                AppendLog($"生成失败: {ex.Message}", Theme.Error);
            }
        }

        /// <summary>
        /// Block picked callback
        /// </summary>
        public void OnBlockPicked(string blockName)
        {
            AppendLog($"已选择块: {blockName}", Theme.TextDim);
            if (!string.IsNullOrEmpty(blockName) && !_selectedBlockNames.Contains(blockName))
            {
                _selectedBlockNames.Add(blockName);
            }
        }

        private SortEngine.SortType GetSelectedSortType()
        {
            if (radSortLR_TB.Checked) return SortEngine.SortType.LeftRight_TopBottom;
            if (radSortTB_LR.Checked) return SortEngine.SortType.TopBottom_LeftRight;
            if (radSortSelection.Checked) return SortEngine.SortType.SelectionOrder;
            if (radSortNumeric.Checked) return SortEngine.SortType.NumericOrder;
            return SortEngine.SortType.SelectionOrder;
        }

        #endregion

        #region Helpers

        private Button CreateFlatButton(string text, int width, Color? bgColor = null)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 26,
                BackColor = bgColor ?? Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        /// <summary>
        /// Thread-safe invoke
        /// </summary>
        public void SafeInvoke(Action action)
        {
            if (InvokeRequired) Invoke(action);
            else action();
        }

        /// <summary>
        /// Append log message with timestamp
        /// </summary>
        public void AppendLog(string message, Color color)
        {
            if (rtbLog == null) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => AppendLog(message, color))); return; }
            if (rtbLog.TextLength > 60000) rtbLog.Clear();
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            rtbLog.ScrollToCaret();
        }

        /// <summary>
        /// Get current style configuration
        /// </summary>
        public CatalogStyle GetCurrentStyle()
        {
            return _currentStyle;
        }

        /// <summary>
        /// Get current block count
        /// </summary>
        public int GetBlockCount()
        {
            return _currentResult?.Blocks?.Count ?? 0;
        }

        /// <summary>
        /// Get current block data result (for InsertPointCommand)
        /// </summary>
        public BlockDataResult GetCurrentBlockData()
        {
            if (_currentResult == null) return null;

            // Convert ExtractionResult to BlockDataResult
            var result = new BlockDataResult
            {
                AllTags = _currentResult.AllTags,
                BlockNames = _currentResult.Blocks.Select(b => b.BlockName).Distinct().ToList(),
                LayerNames = new List<string>()
            };

            // Convert AttributeBlockData to BlockData
            foreach (var attrBlock in _currentResult.Blocks)
            {
                var blockData = new BlockData
                {
                    BlockName = attrBlock.BlockName,
                    ObjectId = attrBlock.BlockId,
                    Attributes = attrBlock.Attributes?.Select(kv => new BlockAttribute { Tag = kv.Key, Value = kv.Value }).ToList()
                        ?? new List<BlockAttribute>()
                };
                result.Blocks.Add(blockData);
            }

            return result;
        }

        /// <summary>
        /// Get current merge configuration
        /// </summary>
        public MergeConfig GetCurrentMergeConfig()
        {
            return new MergeConfig
            {
                EnableMerge = _currentStyle.MergeStrategy == MergeStrategy.PrefixConsecutive,
                Criterion = MergeCriterion.Prefix,
                GroupSymbol = "-",
                RangeSymbol = "~"
            };
        }

        /// <summary>
        /// Get layout radio button state
        /// </summary>
        public bool GetLayoutRadioState()
        {
            return radLayout != null && radLayout.Checked;
        }

        /// <summary>
        /// Direct generate (for command callback)
        /// </summary>
        public void DoGenerateCatalogDirect()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("没有可生成的块数据", Theme.Warning);
                return;
            }

            try
            {
                // 获取当前筛选范围
                string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";
                var targetBlocks = (selectedFilter != "(全部)")
                    ? _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList()
                    : _currentResult.Blocks;

                if (targetBlocks.Count == 0)
                {
                    AppendLog("筛选范围内没有可生成的块数据", Theme.Warning);
                    return;
                }

                // 根据排序模式获取排序后的数据（含容差和反序参数）
                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(targetBlocks, sortType, 500.0, chkReverse.Checked);

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                {
                    targetLayout = cmbLayoutName.SelectedItem.ToString();
                }

                // 将排序后的 AttributeBlockData 转换为 BlockDataResult
                var blockDataResult = new BlockDataResult();
                foreach (var attrBlock in sortedBlocks)
                {
                    var blockData = new BlockData
                    {
                        BlockName = attrBlock.BlockName,
                        ObjectId = attrBlock.BlockId,
                        Attributes = attrBlock.Attributes?.Select(kv => new BlockAttribute { Tag = kv.Key, Value = kv.Value }).ToList()
                            ?? new List<BlockAttribute>()
                    };
                    blockDataResult.Blocks.Add(blockData);
                }

                var pos = Plugin._pendingInsertPoint ?? new Point3d(0, 0, 0);

                // 调用真正的 Generate 方法生成带网格线的目录实体
                var entities = _generator.Generate(blockDataResult, _currentStyle, pos, targetLayout);

                // 将实体写入 CAD 图纸数据库
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (var docLock = doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        // 获取目标空间（模型空间或布局）
                        BlockTableRecord ms;
                        if (!string.IsNullOrEmpty(targetLayout))
                        {
                            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                            var btr = (BlockTableRecord)tr.GetObject(bt[targetLayout], OpenMode.ForRead);
                            ms = (BlockTableRecord)tr.GetObject(btr.Id, OpenMode.ForWrite);
                        }
                        else
                        {
                            ms = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                        }

                        // 将所有生成的实体添加到图纸
                        foreach (var entity in entities)
                        {
                            ms.AppendEntity(entity);
                            tr.AddNewlyCreatedDBObject(entity, true);
                        }

                        tr.Commit();
                    }
                }

                AppendLog("目录已生成", Theme.Success);
            }
            catch (Exception ex)
            {
                AppendLog($"生成失败: {ex.Message}", Theme.Error);
            }
        }

        #endregion
    }
}