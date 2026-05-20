using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BlockCatalogPlugin;

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
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(4) };

            int y = 8;
            int leftMargin = 6;

            // 工作模式 GroupBox
            var grpMode = new GroupBox
            {
                Text = "工作模式",
                Location = new Point(leftMargin, y),
                Size = new Size(panel.Width - 12, 65),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            radCatalogMode = new RadioButton
            {
                Text = "生成目录",
                Location = new Point(10, 20),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Checked = true
            };
            grpMode.Controls.Add(radCatalogMode);

            radEditMode = new RadioButton
            {
                Text = "更改图号",
                Location = new Point(10, 42),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            grpMode.Controls.Add(radEditMode);
            panel.Controls.Add(grpMode);
            y += 72;

            // 排序模式 GroupBox
            var grpSort = new GroupBox
            {
                Text = "排序模式",
                Location = new Point(leftMargin, y),
                Size = new Size(panel.Width - 12, 130),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            radSortLR_TB = new RadioButton
            {
                Text = "左右 → 上下",
                Location = new Point(10, 20),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            grpSort.Controls.Add(radSortLR_TB);

            radSortTB_LR = new RadioButton
            {
                Text = "上下 → 左右",
                Location = new Point(10, 42),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Checked = true
            };
            grpSort.Controls.Add(radSortTB_LR);

            radSortSelection = new RadioButton
            {
                Text = "选择顺序",
                Location = new Point(10, 64),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            grpSort.Controls.Add(radSortSelection);

            radSortNumeric = new RadioButton
            {
                Text = "数值顺序",
                Location = new Point(10, 86),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            grpSort.Controls.Add(radSortNumeric);

            chkReverse = new CheckBox
            {
                Text = "反序",
                Location = new Point(10, 108),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card
            };
            grpSort.Controls.Add(chkReverse);
            panel.Controls.Add(grpSort);
            y += 137;

            // 操作按钮
            var btnSelect = CreateFlatButton("框选图块", leftMargin, y, 90, Theme.Primary);
            btnSelect.Click += (s, e) => ExecuteCommand("_BCSELECT");
            panel.Controls.Add(btnSelect);

            var btnSmart = CreateFlatButton("智能提取", leftMargin + 95, y, 80, Theme.AccentLight);
            btnSmart.Click += (s, e) => ExecuteCommand("_BCSMARTEXTRACT");
            panel.Controls.Add(btnSmart);

            y += 32;

            var btnClearData = CreateFlatButton("清除数据", leftMargin, y, 80, Theme.Warning);
            btnClearData.Click += (s, e) => ClearData();
            panel.Controls.Add(btnClearData);

            return panel;
        }

        /// <summary>
        /// 创建图形缓冲区 + 缀参数区（中间栏）
        /// </summary>
        private Panel CreateBufferPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(4) };

            // 上部：图形缓冲区 DataGridView
            var grpBuffer = new GroupBox
            {
                Text = "图形缓冲区",
                Location = new Point(4, 4),
                Size = new Size(panel.Width - 8, 260),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            // DataGridView
            dgvBlocks = new DataGridView
            {
                Location = new Point(6, 18),
                Size = new Size(grpBuffer.Width - 12, 195),
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

            // 右侧小按钮
            var btnPanel = new Panel
            {
                Location = new Point(grpBuffer.Width - 52, 18),
                Size = new Size(46, 195),
                BackColor = Theme.Card
            };

            var btnPick = CreateFlatButton("选块", 2, 0, 42, Theme.Primary);
            btnPick.Height = 24;
            btnPick.Click += (s, e) => ExecuteCommand("_BCSELECT");
            btnPanel.Controls.Add(btnPick);

            var btnRemove = CreateFlatButton("删块", 2, 28, 42, Theme.Warning);
            btnRemove.Height = 24;
            btnRemove.Click += (s, e) => RemoveSelectedBlock();
            btnPanel.Controls.Add(btnRemove);

            var btnMoveUp = CreateFlatButton("▲", 2, 100, 42, Theme.Primary);
            btnMoveUp.Height = 24;
            btnMoveUp.Click += (s, e) => MoveBlockUp();
            btnPanel.Controls.Add(btnMoveUp);

            var btnMoveDown = CreateFlatButton("▼", 2, 128, 42, Theme.Primary);
            btnMoveDown.Height = 24;
            btnMoveDown.Click += (s, e) => MoveBlockDown();
            btnPanel.Controls.Add(btnMoveDown);

            grpBuffer.Controls.Add(btnPanel);
            grpBuffer.Controls.Add(dgvBlocks);
            panel.Controls.Add(grpBuffer);

            // 下部：缀参数重编设置
            var grpSuffix = new GroupBox
            {
                Text = "缀参数重编",
                Location = new Point(4, 270),
                Size = new Size(panel.Width - 8, 120),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            int sy = 18;
            var lblStart = new Label { Text = "缀始:", Location = new Point(8, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpSuffix.Controls.Add(lblStart);

            txtSuffixStart = new TextBox { Location = new Point(42, sy - 2), Width = 45, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F), Text = "1" };
            grpSuffix.Controls.Add(txtSuffixStart);

            var lblLen = new Label { Text = "缀长:", Location = new Point(92, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpSuffix.Controls.Add(lblLen);

            txtSuffixLength = new TextBox { Location = new Point(126, sy - 2), Width = 35, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F), Text = "2" };
            grpSuffix.Controls.Add(txtSuffixLength);

            chkSuffixContinuous = new CheckBox { Text = "连续", Location = new Point(170, sy), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card, Checked = true };
            grpSuffix.Controls.Add(chkSuffixContinuous);

            sy += 24;
            var lblPrefix = new Label { Text = "前缀:", Location = new Point(8, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpSuffix.Controls.Add(lblPrefix);

            txtSuffixPrefix = new TextBox { Location = new Point(42, sy - 2), Width = 70, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F) };
            grpSuffix.Controls.Add(txtSuffixPrefix);

            var lblSuffix = new Label { Text = "后缀:", Location = new Point(118, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpSuffix.Controls.Add(lblSuffix);

            txtSuffixSuffix = new TextBox { Location = new Point(152, sy - 2), Width = 60, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 8F) };
            grpSuffix.Controls.Add(txtSuffixSuffix);

            sy += 28;
            var btnApplySuffix = CreateFlatButton("应用缀编号", 8, sy, 100, Theme.Success);
            btnApplySuffix.Click += (s, e) => ApplySuffixRename();
            grpSuffix.Controls.Add(btnApplySuffix);

            var btnPreviewSuffix = CreateFlatButton("预览", 115, sy, 70, Theme.Primary);
            btnPreviewSuffix.Click += (s, e) => PreviewSuffixRename();
            grpSuffix.Controls.Add(btnPreviewSuffix);

            panel.Controls.Add(grpSuffix);

            return panel;
        }

        /// <summary>
        /// 创建目录输出区（右侧栏）
        /// </summary>
        private Panel CreateOutputPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(4) };

            // 列宽公式 GroupBox
            var grpColWidth = new GroupBox
            {
                Text = "列宽公式",
                Location = new Point(4, 4),
                Size = new Size(panel.Width - 8, 60),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            txtColumnFormula = new TextBox
            {
                Location = new Point(8, 18),
                Size = new Size(grpColWidth.Width - 16, 22),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8F),
                Text = "20+40+60"
            };
            grpColWidth.Controls.Add(txtColumnFormula);

            var btnApplyFormula = CreateFlatButton("应用", 8, 42, 55, Theme.Primary);
            btnApplyFormula.Height = 18;
            btnApplyFormula.Click += (s, e) => ApplyColumnFormula();
            grpColWidth.Controls.Add(btnApplyFormula);

            var btnGetFormula = CreateFlatButton("获取", 68, 42, 55, Theme.Accent);
            btnGetFormula.Height = 18;
            btnGetFormula.Click += (s, e) => txtColumnFormula.Text = _currentStyle.GetFormulaWidths();
            grpColWidth.Controls.Add(btnGetFormula);

            panel.Controls.Add(grpColWidth);

            // 表格样式 GroupBox
            var grpStyle = new GroupBox
            {
                Text = "表格样式",
                Location = new Point(4, 70),
                Size = new Size(panel.Width - 8, 95),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            int sy = 18;

            var lblFontH = new Label { Text = "字高:", Location = new Point(8, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpStyle.Controls.Add(lblFontH);

            numFontHeight = new NumericUpDown { Location = new Point(50, sy - 2), Width = 50, Minimum = 1, Maximum = 20, Value = 3.5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            grpStyle.Controls.Add(numFontHeight);

            var lblRowH = new Label { Text = "行高:", Location = new Point(108, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpStyle.Controls.Add(lblRowH);

            numRowHeight = new NumericUpDown { Location = new Point(145, sy - 2), Width = 50, Minimum = 1, Maximum = 50, Value = 5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            grpStyle.Controls.Add(numRowHeight);

            sy += 26;

            chkShowHeader = new CheckBox { Text = "显示表头", Location = new Point(8, sy), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card, Checked = true };
            grpStyle.Controls.Add(chkShowHeader);

            sy += 22;

            var lblLayout = new Label { Text = "输出:", Location = new Point(8, sy), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 7.5F) };
            grpStyle.Controls.Add(lblLayout);

            radModelSpace = new RadioButton { Text = "模型", Location = new Point(45, sy), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card, Checked = true };
            grpStyle.Controls.Add(radModelSpace);

            radLayout = new RadioButton { Text = "布局", Location = new Point(90, sy), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            grpStyle.Controls.Add(radLayout);

            cmbLayoutName = new ComboBox { Location = new Point(130, sy - 2), Width = 55, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.InputBg, ForeColor = Theme.Text, Enabled = false };
            cmbLayoutName.Items.Add("Model");
            cmbLayoutName.SelectedIndex = 0;
            grpStyle.Controls.Add(cmbLayoutName);

            radLayout.CheckedChanged += (s, e) => { cmbLayoutName.Enabled = radLayout.Checked; };
            panel.Controls.Add(grpStyle);

            // 输出按钮
            int btnY = 175;

            var btnPreview = CreateFlatButton("预览目录", 4, btnY, panel.Width - 8, Theme.Primary);
            btnPreview.Height = 35;
            btnPreview.Click += (s, e) => ShowPreview();
            panel.Controls.Add(btnPreview);

            btnY += 42;

            var btnGenerate = CreateFlatButton("生成目录", 4, btnY, panel.Width - 8, Theme.Success);
            btnGenerate.Height = 40;
            btnGenerate.Click += (s, e) => GenerateToPosition();
            panel.Controls.Add(btnGenerate);

            btnY += 48;

            var btnSyncAttrs = CreateFlatButton("同步至图纸属性", 4, btnY, panel.Width - 8, Theme.AccentLight);
            btnSyncAttrs.Height = 35;
            btnSyncAttrs.Click += (s, e) => SyncAttributesToBlocks();
            panel.Controls.Add(btnSyncAttrs);

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

            dgvBlocks.DataSource = _currentResult.Blocks.Select(b => new
            {
                块名 = b.BlockName,
                图号 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                图名 = b.GetAttribute("TM") ?? "",
                幅面 = b.GetAttribute("FM") ?? "",
                X = Math.Round(b.Position.X, 1),
                Y = Math.Round(b.Position.Y, 1)
            }).ToList();
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

            // 交换行数据
            var temp = _currentResult.Blocks[_dragRowIndex];
            _currentResult.Blocks[_dragRowIndex] = _currentResult.Blocks[targetIndex];
            _currentResult.Blocks[targetIndex] = temp;

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

            if (!int.TryParse(txtSuffixStart.Text, out int startNum)) startNum = 1;
            if (!int.TryParse(txtSuffixLength.Text, out int numLength)) numLength = 2;
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            var engine = new BlockCatalogPlugin.SuffixPatternEngine();
            bool success = engine.BulkRenameAttributes(
                _currentResult.Blocks,
                "XH",
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
            AppendLog("同步属性到图纸...", Theme.TextDim);
            // TODO: 实现属性同步
            AppendLog("属性同步功能开发中", Theme.Warning);
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
                // 根据排序模式获取排序后的数据
                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(_currentResult.Blocks, sortType);

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                {
                    targetLayout = cmbLayoutName.SelectedItem.ToString();
                }

                MergeConfig mergeConfig = null;
                if (_currentStyle.MergeStrategy == MergeStrategy.PrefixConsecutive)
                {
                    mergeConfig = new MergeConfig
                    {
                        EnableMerge = true,
                        Criterion = MergeCriterion.Prefix,
                        GroupSymbol = "-",
                        RangeSymbol = "~"
                    };
                }

                var tableId = _generator.GenerateTable(sortedBlocks, _currentStyle, mergeConfig, pos, targetLayout);
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

        private Button CreateFlatButton(string text, int x, int y, int width, Color? bgColor = null)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
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
                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(_currentResult.Blocks, sortType);

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                {
                    targetLayout = cmbLayoutName.SelectedItem.ToString();
                }

                MergeConfig mergeConfig = null;
                if (_currentStyle.MergeStrategy == MergeStrategy.PrefixConsecutive)
                {
                    mergeConfig = new MergeConfig
                    {
                        EnableMerge = true,
                        Criterion = MergeCriterion.Prefix,
                        GroupSymbol = "-",
                        RangeSymbol = "~"
                    };
                }

                var pos = Plugin._pendingInsertPoint ?? new Point3d(0, 0, 0);
                var tableId = _generator.GenerateTable(sortedBlocks, _currentStyle, mergeConfig, pos, targetLayout);
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