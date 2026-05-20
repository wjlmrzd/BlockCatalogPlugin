using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BlockCatalogPlugin;
using Font = System.Drawing.Font;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BlockCatalogPlugin.UI
{
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
        private Panel _canvasPanel;

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

        private static Font _headerTitleFont;
        private static Font _headerVerFont;
        private static Brush _headerTitleBrush;
        private static Brush _headerVerBrush;

        private int _dragRowIndex = -1;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private bool _isUpdatingFilter = false;
        private double _sortTolerance = 500.0; // 排序容差，默认500
        private CheckBox chkMergeSameName; // 合并相同前缀的图名/图号
        private ListBox lstColumns; // 列管理列表框
        private ComboBox cmbAddColumn; // 添加列下拉框

        // 列宽/行高拖拽相关
        private bool _isResizingColumn = false;
        private bool _isResizingRow = false;
        private bool _isPickingColumnWidth = false; // 是否处于拖拽获取列宽模式
        private int _selectedColumnIndexForPick = -1; // 用于拖拽获取列宽时指定哪一列

        public BlockCatalogPanel()
        {
            _preferences = PreferencesManager.Instance.Load();
            _currentStyle = LoadStyleFromPreferences();
            EnsureCachedResources();
            InitializeComponents();
        }

        private static void EnsureCachedResources()
        {
            _headerTitleFont ??= new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _headerVerFont ??= new Font("Microsoft YaHei UI", 8F);
            _headerTitleBrush ??= new SolidBrush(Theme.TextBright);
            _headerVerBrush ??= new SolidBrush(Theme.TextDim);
        }

        public static void ReleaseCachedResources()
        {
            try { _headerTitleFont?.Dispose(); } catch { }
            try { _headerVerFont?.Dispose(); } catch { }
            try { _headerTitleBrush?.Dispose(); } catch { }
            try { _headerVerBrush?.Dispose(); } catch { }
            _headerTitleFont = null; _headerVerFont = null;
            _headerTitleBrush = null; _headerVerBrush = null;
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
            Size = new Size(340, 800);

            // === Header ===
            var headerPanel = new Panel
            {
                Bounds = new Rectangle(0, 0, 340, 50),
                BackColor = Theme.Card,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            headerPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawString("图框目录工具", _headerTitleFont, _headerTitleBrush, 12, 8);
                e.Graphics.DrawString("全总控看板 V3.0 (固定版)", _headerVerFont, _headerVerBrush, 12, 28);
            };

            // 快捷键配置
            var lblShortcut = new Label
            {
                Text = "快捷键:",
                Location = new Point(200, 8),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8F),
                ForeColor = Theme.TextDim
            };
            var txtShortcut = new TextBox
            {
                Text = _preferences.ShortcutKey ?? "bca",
                Location = new Point(238, 5),
                Width = 45,
                Height = 18,
                Font = new Font("Consolas", 8F),
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextBright,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center
            };
            txtShortcut.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    string key = txtShortcut.Text.Trim().ToLower();
                    // 验证：只允许字母和数字
                    if (IsValidShortcutKey(key) && key.Length <= 10 && key.Length >= 1)
                    {
                        _preferences.ShortcutKey = key;
                        PreferencesManager.Instance.Save();
                        // 重新注册快捷键
                        Plugin.RegisterShortcutKey();
                        AppendLog($"快捷键已保存并注册: {key}", Theme.Success);
                    }
                    else
                    {
                        AppendLog($"快捷键无效（仅支持字母和数字）", Theme.Error);
                        txtShortcut.Text = _preferences.ShortcutKey ?? "bca";
                    }
                    txtShortcut.SelectAll();
                }
            };
            txtShortcut.LostFocus += (s, e) =>
            {
                string key = txtShortcut.Text.Trim().ToLower();
                // 验证：只允许字母和数字
                if (IsValidShortcutKey(key) && key.Length <= 10 && key.Length >= 1)
                {
                    _preferences.ShortcutKey = key;
                    PreferencesManager.Instance.Save();
                    // 重新注册快捷键
                    Plugin.RegisterShortcutKey();
                }
                else
                {
                    txtShortcut.Text = _preferences.ShortcutKey ?? "bca";
                }
            };
            headerPanel.Controls.Add(lblShortcut);
            headerPanel.Controls.Add(txtShortcut);
            Controls.Add(headerPanel);

            // === Canvas ===
            _canvasPanel = new Panel
            {
                Bounds = new Rectangle(0, 50, 340, 630),
                BackColor = Theme.Bg,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            BuildFixedLayoutCanvas();
            Controls.Add(_canvasPanel);

            // === Log Panel ===
            var logPanel = new Panel
            {
                Bounds = new Rectangle(0, 680, 340, 120),
                BackColor = Theme.Card,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblLogTitle = new Label
            {
                Text = "操作日志",
                Location = new Point(6, 4),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold),
                ForeColor = Theme.TextDim
            };
            logPanel.Controls.Add(lblLogTitle);

            var btnClear = CreateFlatButton("清空", 290, 2, 45, Theme.Card);
            btnClear.Height = 18;
            btnClear.Font = new Font("Microsoft YaHei UI", 7F);
            btnClear.Click += (s, e) => rtbLog?.Clear();
            logPanel.Controls.Add(btnClear);

            rtbLog = new RichTextBox
            {
                Bounds = new Rectangle(0, 22, 340, 98),
                BackColor = Theme.LogBg,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 8F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            logPanel.Controls.Add(rtbLog);
            Controls.Add(logPanel);

            AppendLog("准备就绪 - 工业级固定看板布局已加载", Theme.TextDim);
        }

        private void BuildFixedLayoutCanvas()
        {
            int curY = 6;
            int boxW = 312;
            int leftX = 10;

            // === Box 1: 总控模式与配置 ===
            var grpMode = new GroupBox
            {
                Text = "总控模式与配置",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 85),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            radCatalogMode = new RadioButton
            {
                Text = "生成目录",
                Location = new Point(12, 20),
                AutoSize = true,
                Checked = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            radEditMode = new RadioButton
            {
                Text = "更改图号",
                Location = new Point(110, 20),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            grpMode.Controls.Add(radCatalogMode);
            grpMode.Controls.Add(radEditMode);

            btnReset = CreateFlatButton("重置面板", 12, 48, 75, Theme.Warning);
            btnReset.Click += (s, e) => ResetPanel();
            btnImport = CreateFlatButton("导入配置", 95, 48, 75, Theme.Primary);
            btnImport.Click += (s, e) => ImportSettings();
            btnExport = CreateFlatButton("导出当前", 178, 48, 75, Theme.AccentLight);
            btnExport.Click += (s, e) => ExportSettings();

            grpMode.Controls.Add(btnReset);
            grpMode.Controls.Add(btnImport);
            grpMode.Controls.Add(btnExport);

            _canvasPanel.Controls.Add(grpMode);
            curY += 95;

            // === Box 2: 空间矩阵排序模式 ===
            var grpSort = new GroupBox
            {
                Text = "空间矩阵排序模式",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 100),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            radSortTB_LR = new RadioButton
            {
                Text = "上下 → 左右",
                Location = new Point(12, 20),
                AutoSize = true,
                Checked = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            radSortLR_TB = new RadioButton
            {
                Text = "左右 → 上下",
                Location = new Point(12, 44),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            radSortSelection = new RadioButton
            {
                Text = "选择序",
                Location = new Point(120, 20),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            radSortNumeric = new RadioButton
            {
                Text = "数值序",
                Location = new Point(120, 44),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            chkReverse = new CheckBox
            {
                Text = "反序",
                Location = new Point(210, 20),
                AutoSize = true,
                ForeColor = Theme.Warning,
                BackColor = Theme.Card
            };

            var lblTolerance = new Label
            {
                Text = "容差",
                Location = new Point(210, 44),
                AutoSize = true,
                ForeColor = Theme.TextDim
            };
            var numTolerance = new NumericUpDown
            {
                Location = new Point(245, 42),
                Width = 55,
                Minimum = 10,
                Maximum = 5000,
                Value = 500,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            numTolerance.ValueChanged += (s, e) =>
            {
                // 存储容差值到 preferences 或直接使用
                _sortTolerance = (double)numTolerance.Value;
            };
            _sortTolerance = 500; // 默认值

            // 添加所有排序控件到分组
            grpSort.Controls.AddRange(new Control[] {
                radSortTB_LR, radSortLR_TB,
                radSortSelection, radSortNumeric,
                chkReverse, lblTolerance, numTolerance
            });
            _canvasPanel.Controls.Add(grpSort); curY += 110;

            // === Box 3: 图形缓冲区与手动调序 ===
            var grpBuffer = new GroupBox
            {
                Text = "图形缓冲区与手动调序",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 260),  // 增大高度以显示更多预览行
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            cmbBlockNameFilter = new ComboBox
            {
                Location = new Point(12, 20),
                Size = new Size(180, 24),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            cmbBlockNameFilter.Items.Add("(全部)");
            cmbBlockNameFilter.SelectedIndex = 0;
            cmbBlockNameFilter.SelectedIndexChanged += (s, e) => FilterBlocksByName();
            grpBuffer.Controls.Add(cmbBlockNameFilter);

            var btnSelect = CreateFlatButton("框选图块", 200, 18, 98, Theme.Primary);
            btnSelect.Height = 24;
            btnSelect.Click += (s, e) => ExecuteCommand("_BCSELECT");
            grpBuffer.Controls.Add(btnSelect);

            dgvBlocks = new DataGridView
            {
                Location = new Point(12, 50),
                Size = new Size(240, 198),  // 增大高度，从138增至198，可显示更多预览行
                BackgroundColor = Theme.LogBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 24,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing,
                RowTemplate = { Height = 22 },  // 设置默认行高为22像素
                ScrollBars = ScrollBars.Both,
                AllowDrop = true,
                AllowUserToResizeColumns = true,  // 允许用户拖拽调整列宽
                AllowUserToResizeRows = true      // 允许用户拖拽调整行高
            };
            dgvBlocks.EnableHeadersVisualStyles = false;
            dgvBlocks.ColumnHeadersDefaultCellStyle.BackColor = Theme.CardHover;
            dgvBlocks.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextBright;
            dgvBlocks.DefaultCellStyle.BackColor = Theme.Card;
            dgvBlocks.DefaultCellStyle.ForeColor = Theme.Text;

            dgvBlocks.MouseDown += DgvBlocks_MouseDown;
            dgvBlocks.MouseMove += DgvBlocks_MouseMove;
            dgvBlocks.MouseUp += DgvBlocks_MouseUp;
            dgvBlocks.DragOver += DgvBlocks_DragOver;
            dgvBlocks.DragDrop += DgvBlocks_DragDrop;
            dgvBlocks.ColumnWidthChanged += DgvBlocks_ColumnWidthChanged;
            dgvBlocks.RowHeightChanged += DgvBlocks_RowHeightChanged;

            grpBuffer.Controls.Add(dgvBlocks);

            var btnRemove = CreateFlatButton("删块", 258, 50, 42, Theme.Warning);
            btnRemove.Height = 24;
            btnRemove.Click += (s, e) => RemoveSelectedBlock();
            var btnMoveUp = CreateFlatButton("▲", 258, 90, 42, Theme.Primary);
            btnMoveUp.Height = 24;
            btnMoveUp.Click += (s, e) => MoveBlockUp();
            var btnMoveDown = CreateFlatButton("▼", 258, 120, 42, Theme.Primary);
            btnMoveDown.Height = 24;
            btnMoveDown.Click += (s, e) => MoveBlockDown();
            var btnReselect = CreateFlatButton("重选", 258, 150, 42, Theme.Accent);  // 新增重选按钮
            btnReselect.Height = 24;
            btnReselect.Click += (s, e) => ReselectBlocks();
            grpBuffer.Controls.Add(btnReselect);

            grpBuffer.Controls.Add(btnRemove);
            grpBuffer.Controls.Add(btnMoveUp);
            grpBuffer.Controls.Add(btnMoveDown);

            _canvasPanel.Controls.Add(grpBuffer);
            curY += 270;  // 调整以匹配新的 grpBuffer 高度(260)

            // === Box 4: 缀参数规律重编属性 ===
            var grpSuffix = new GroupBox
            {
                Text = "缀参数规律重编属性",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 105),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            var lblStart = new Label { Text = "缀始", Location = new Point(12, 22), AutoSize = true, ForeColor = Theme.TextDim };
            txtSuffixStart = new TextBox { Location = new Point(45, 19), Width = 40, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Text = "1" };
            var lblLen = new Label { Text = "缀长", Location = new Point(95, 22), AutoSize = true, ForeColor = Theme.TextDim };
            txtSuffixLength = new TextBox { Location = new Point(128, 19), Width = 30, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Text = "2" };
            chkSuffixContinuous = new CheckBox { Text = "连续连号", Location = new Point(170, 20), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };

            grpSuffix.Controls.AddRange(new Control[] { lblStart, txtSuffixStart, lblLen, txtSuffixLength, chkSuffixContinuous });

            var lblPrefix = new Label { Text = "前缀", Location = new Point(12, 48), AutoSize = true, ForeColor = Theme.TextDim };
            txtSuffixPrefix = new TextBox { Location = new Point(45, 45), Width = 75, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            var lblSuffix = new Label { Text = "后缀", Location = new Point(130, 48), AutoSize = true, ForeColor = Theme.TextDim };
            txtSuffixSuffix = new TextBox { Location = new Point(162, 45), Width = 65, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };

            grpSuffix.Controls.AddRange(new Control[] { lblPrefix, txtSuffixPrefix, lblSuffix, txtSuffixSuffix });

            var btnApplySuffix = CreateFlatButton("应用缀编号", 12, 73, 90, Theme.Success);
            btnApplySuffix.Height = 22;
            btnApplySuffix.Click += (s, e) => ApplySuffixRename();
            var btnPreviewSuffix = CreateFlatButton("预览序列", 110, 73, 65, Theme.Primary);
            btnPreviewSuffix.Height = 22;
            btnPreviewSuffix.Click += (s, e) => PreviewSuffixRename();

            grpSuffix.Controls.AddRange(new Control[] { btnApplySuffix, btnPreviewSuffix });

            _canvasPanel.Controls.Add(grpSuffix);
            curY += 115;

            // === Box 5: 列宽表达式与表格参数 ===
            var grpFormula = new GroupBox
            {
                Text = "列宽表达式与表格参数",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 215),  // 增大高度以容纳列管理控件
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            // === 子区域：列管理 ===
            var lblColMgr = new Label { Text = "列管理:", Location = new Point(12, 18), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold) };
            grpFormula.Controls.Add(lblColMgr);

            // 列选择下拉框（用于添加新列，使用字段引用）
            cmbAddColumn = new ComboBox
            {
                Location = new Point(12, 38),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            cmbAddColumn.Items.Add("XH-图号");
            cmbAddColumn.Items.Add("TH-图号");
            cmbAddColumn.Items.Add("TM-图名");
            cmbAddColumn.Items.Add("BL-比例");
            cmbAddColumn.Items.Add("DH-图别");
            cmbAddColumn.Items.Add("JZ-建筑");
            cmbAddColumn.Items.Add("GC-工程");
            cmbAddColumn.SelectedIndex = 0;
            grpFormula.Controls.Add(cmbAddColumn);

            var btnAddColumn = CreateFlatButton("增加列", 120, 36, 55, Theme.Success);
            btnAddColumn.Height = 20;
            btnAddColumn.Click += (s, e) =>
            {
                if (cmbAddColumn.SelectedItem != null)
                {
                    string selected = cmbAddColumn.SelectedItem.ToString();
                    string[] parts = selected.Split('-');
                    string tag = parts[0];
                    string header = parts.Length > 1 ? parts[1] : tag;
                    AddColumn(tag, header);
                }
            };
            grpFormula.Controls.Add(btnAddColumn);

            var btnRemoveColumn = CreateFlatButton("删除列", 180, 36, 55, Theme.Warning);
            btnRemoveColumn.Height = 20;
            btnRemoveColumn.Click += (s, e) => RemoveSelectedColumn();
            grpFormula.Controls.Add(btnRemoveColumn);

            var btnColUp = CreateFlatButton("▲", 240, 36, 28, Theme.Primary);
            btnColUp.Height = 20;
            btnColUp.Click += (s, e) => MoveColumnUp();
            grpFormula.Controls.Add(btnColUp);

            var btnColDown = CreateFlatButton("▼", 270, 36, 28, Theme.Primary);
            btnColDown.Height = 20;
            btnColDown.Click += (s, e) => MoveColumnDown();
            grpFormula.Controls.Add(btnColDown);

            // 当前列 ListBox（使用字段引用）
            lstColumns = new ListBox
            {
                Location = new Point(12, 60),
                Size = new Size(290, 55),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 8F),
                BorderStyle = BorderStyle.FixedSingle
            };
            lstColumns.SelectedIndexChanged += (s, e) => { /* 可选中列进行操作 */ };
            grpFormula.Controls.Add(lstColumns);

            // === 分隔线 ===
            var sepLine = new Panel { Location = new Point(12, 120), Size = new Size(290, 1), BackColor = Theme.Border };
            grpFormula.Controls.Add(sepLine);

            // === 子区域：列宽与行高 ===
            var lblForm = new Label { Text = "列宽公式:", Location = new Point(12, 128), AutoSize = true, ForeColor = Theme.TextDim };
            grpFormula.Controls.Add(lblForm);

            txtColumnFormula = new TextBox
            {
                Location = new Point(62, 126),
                Width = 130,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                Text = "20+40+60"
            };
            grpFormula.Controls.Add(txtColumnFormula);

            var btnApplyForm = CreateFlatButton("应用", 198, 124, 40, Theme.Primary);
            btnApplyForm.Height = 18;
            btnApplyForm.Click += (s, e) => ApplyColumnFormula();
            grpFormula.Controls.Add(btnApplyForm);

            var btnGetForm = CreateFlatButton("获取", 242, 124, 40, Theme.Accent);
            btnGetForm.Height = 18;
            btnGetForm.Click += (s, e) => txtColumnFormula.Text = _currentStyle.GetFormulaWidths();
            grpFormula.Controls.Add(btnGetForm);

            var btnPickColWidth = CreateFlatButton("拖拽获取列宽", 286, 124, 62, Theme.AccentLight);
            btnPickColWidth.Height = 18;
            btnPickColWidth.Click += (s, e) => StartPickColumnWidthMode();
            grpFormula.Controls.Add(btnPickColWidth);

            // 字高和行高
            var lblFontH = new Label { Text = "字高", Location = new Point(12, 150), AutoSize = true, ForeColor = Theme.TextDim };
            grpFormula.Controls.Add(lblFontH);
            numFontHeight = new NumericUpDown { Location = new Point(45, 148), Width = 45, Minimum = 1, Maximum = 20, Value = 3.5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            grpFormula.Controls.Add(numFontHeight);

            var lblRowH = new Label { Text = "行高", Location = new Point(100, 150), AutoSize = true, ForeColor = Theme.TextDim };
            grpFormula.Controls.Add(lblRowH);
            numRowHeight = new NumericUpDown { Location = new Point(132, 148), Width = 45, Minimum = 1, Maximum = 50, Value = 5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            grpFormula.Controls.Add(numRowHeight);

            chkShowHeader = new CheckBox { Text = "表头", Location = new Point(185, 149), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            grpFormula.Controls.Add(chkShowHeader);

            // 目标空间
            var lblOut = new Label { Text = "目标:", Location = new Point(12, 175), AutoSize = true, ForeColor = Theme.TextDim };
            grpFormula.Controls.Add(lblOut);
            radModelSpace = new RadioButton { Text = "模型", Location = new Point(48, 173), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            grpFormula.Controls.Add(radModelSpace);
            radLayout = new RadioButton { Text = "布局", Location = new Point(90, 173), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            grpFormula.Controls.Add(radLayout);
            cmbLayoutName = new ComboBox
            {
                Location = new Point(125, 171),
                Width = 70,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            cmbLayoutName.Items.Add("Model");
            cmbLayoutName.SelectedIndex = 0;
            radLayout.CheckedChanged += (s, e) => { cmbLayoutName.Enabled = radLayout.Checked; };
            grpFormula.Controls.Add(cmbLayoutName);

            // 鼠标拖拽定义尺寸按钮
            var btnPickDimSize = CreateFlatButton("拖拽获取尺寸", 200, 169, 70, Theme.Primary);
            btnPickDimSize.Height = 20;
            btnPickDimSize.Click += (s, e) =>
            {
                var style = GetCurrentStyle();
                style.UseMouseDefineSize = true;
                AppendLog("请在CAD中拖拽定义表格尺寸...", Theme.Primary);
                ExecuteCommand("_BCGENPOS");
            };
            grpFormula.Controls.Add(btnPickDimSize);

            _canvasPanel.Controls.Add(grpFormula);
            curY += 225;

            // === 间距公式 ===
            var lblSpace = new Label { Text = "间距公式:", Location = new Point(leftX + 6, curY + 4), AutoSize = true, ForeColor = Theme.TextDim };
            txtSpacingExpression = new TextBox
            {
                Location = new Point(leftX + 70, curY + 2),
                Width = 238,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "5"
            };
            _canvasPanel.Controls.Add(lblSpace);
            _canvasPanel.Controls.Add(txtSpacingExpression);
            curY += 30;

            // === 合并选项 ===
            chkMergeSameName = new CheckBox
            {
                Text = "合并相同前缀的图名/图号",
                Location = new Point(leftX, curY),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Bg
            };
            _canvasPanel.Controls.Add(chkMergeSameName);
            curY += 28;

            // === 输出按钮 ===
            var btnPreview = CreateFlatButton("预览目录表格", boxW, Theme.Primary);
            btnPreview.Location = new Point(leftX, curY);
            btnPreview.Height = 30;
            btnPreview.Click += (s, e) => ShowPreview();
            _canvasPanel.Controls.Add(btnPreview);
            curY += 36;

            var btnGenerate = CreateFlatButton("指定点绘制目录", boxW, Theme.Success);
            btnGenerate.Location = new Point(leftX, curY);
            btnGenerate.Height = 36;
            btnGenerate.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnGenerate.Click += (s, e) => GenerateToPosition();
            _canvasPanel.Controls.Add(btnGenerate);
            curY += 42;

            var btnSyncAttrs = CreateFlatButton("一键反向同步至图纸块属性", boxW, Theme.AccentLight);
            btnSyncAttrs.Location = new Point(leftX, curY);
            btnSyncAttrs.Height = 30;
            btnSyncAttrs.Click += (s, e) => SyncAttributesToBlocks();
            _canvasPanel.Controls.Add(btnSyncAttrs);
        }

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

        private Button CreateFlatButton(string text, int width, Color? bgColor = null)
        {
            return CreateFlatButton(text, 0, 0, width, bgColor);
        }

        private void ExecuteCommand(string command)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
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
            if (dgvBlocks != null) dgvBlocks.DataSource = null;
            AppendLog("已清除数据", Theme.TextDim);
        }

        private void ResetPanel()
        {
            txtSuffixStart.Text = "1";
            txtSuffixLength.Text = "2";
            txtSuffixPrefix.Text = "";
            txtSuffixSuffix.Text = "";
            chkSuffixContinuous.Checked = true;

            radSortTB_LR.Checked = true;
            chkReverse.Checked = false;

            numFontHeight.Value = 3.5m;
            numRowHeight.Value = 5m;
            chkShowHeader.Checked = true;
            txtColumnFormula.Text = "20+40+60";
            txtSpacingExpression.Text = "5";
            radModelSpace.Checked = true;

            if (cmbBlockNameFilter != null)
            {
                cmbBlockNameFilter.Items.Clear();
                cmbBlockNameFilter.Items.Add("(全部)");
                cmbBlockNameFilter.SelectedIndex = 0;
            }

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
        /// 验证快捷键是否有效（只允许字母和数字）
        /// </summary>
        private bool IsValidShortcutKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }

        private string GetActiveTag()
        {
            if (_currentResult != null && _currentResult.Blocks.Count > 0)
            {
                var firstBlock = _currentResult.Blocks[0];
                // 尝试常见的图号标签名，返回第一个找到非空值的标签名
                string[] tagCandidates = { "TH", "XH", "BH", "图号", "编号", "DRAWING_NO" };
                foreach (var tag in tagCandidates)
                {
                    var val = firstBlock.GetAttribute(tag);
                    if (!string.IsNullOrEmpty(val)) return tag;
                }
            }
            return "XH";
        }

        /// <summary>
        /// 尝试多个标签名，返回第一个找到的非空值
        /// </summary>
        private string GetAttributeMulti(AttributeBlockData block, params string[] tags)
        {
            foreach (var tag in tags)
            {
                var val = block.GetAttribute(tag);
                if (!string.IsNullOrEmpty(val)) return val;
            }
            return null;
        }

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

        /// <summary>
        /// DataGridView 行包装类，包含原始块索引
        /// </summary>
        private class BlockRowData
        {
            public int OriginalIndex { get; set; }
            public string 块名 { get; set; }
            public string 图号 { get; set; }
            public string 图名 { get; set; }
            public string 比例 { get; set; }
            public string 重编预览 { get; set; }
        }

        private List<BlockRowData> _currentBlockRows = new List<BlockRowData>();

        private void RemoveSelectedBlock()
        {
            if (dgvBlocks.SelectedRows.Count > 0 && _currentResult != null && _currentBlockRows.Count > 0)
            {
                int rowIdx = dgvBlocks.SelectedRows[0].Index;
                if (rowIdx >= 0 && rowIdx < _currentBlockRows.Count)
                {
                    // 使用 BlockRowData 中保存的原始索引
                    int originalIdx = _currentBlockRows[rowIdx].OriginalIndex;
                    if (originalIdx >= 0 && originalIdx < _currentResult.Blocks.Count)
                    {
                        _currentResult.Blocks.RemoveAt(originalIdx);
                        RefreshDataGridView();
                        AppendLog($"已删除第 {originalIdx + 1} 行", Theme.Warning);
                    }
                }
            }
        }

        private void MoveBlockUp()
        {
            if (dgvBlocks.SelectedRows.Count > 0 && _currentResult != null && _currentBlockRows.Count > 0)
            {
                int rowIdx = dgvBlocks.SelectedRows[0].Index;
                if (rowIdx > 0 && rowIdx < _currentBlockRows.Count)
                {
                    // 使用原始索引进行交换
                    int origIdx = _currentBlockRows[rowIdx].OriginalIndex;
                    int prevOrigIdx = _currentBlockRows[rowIdx - 1].OriginalIndex;

                    if (origIdx > 0 && origIdx < _currentResult.Blocks.Count &&
                        prevOrigIdx >= 0 && prevOrigIdx < _currentResult.Blocks.Count)
                    {
                        var temp = _currentResult.Blocks[origIdx];
                        _currentResult.Blocks[origIdx] = _currentResult.Blocks[prevOrigIdx];
                        _currentResult.Blocks[prevOrigIdx] = temp;
                        RefreshDataGridView();
                        if (rowIdx - 1 >= 0 && rowIdx - 1 < dgvBlocks.Rows.Count)
                            dgvBlocks.Rows[rowIdx - 1].Selected = true;
                    }
                }
            }
        }

        private void MoveBlockDown()
        {
            if (dgvBlocks.SelectedRows.Count > 0 && _currentResult != null && _currentBlockRows.Count > 0)
            {
                int rowIdx = dgvBlocks.SelectedRows[0].Index;
                if (rowIdx >= 0 && rowIdx < _currentBlockRows.Count - 1)
                {
                    // 使用原始索引进行交换
                    int origIdx = _currentBlockRows[rowIdx].OriginalIndex;
                    int nextOrigIdx = _currentBlockRows[rowIdx + 1].OriginalIndex;

                    if (origIdx >= 0 && origIdx < _currentResult.Blocks.Count &&
                        nextOrigIdx >= 0 && nextOrigIdx < _currentResult.Blocks.Count)
                    {
                        var temp = _currentResult.Blocks[origIdx];
                        _currentResult.Blocks[origIdx] = _currentResult.Blocks[nextOrigIdx];
                        _currentResult.Blocks[nextOrigIdx] = temp;
                        RefreshDataGridView();
                        if (rowIdx + 1 >= 0 && rowIdx + 1 < dgvBlocks.Rows.Count)
                            dgvBlocks.Rows[rowIdx + 1].Selected = true;
                    }
                }
            }
        }

        private void RefreshDataGridView()
        {
            if (dgvBlocks == null || _currentResult == null) return;

            dgvBlocks.DataSource = null;
            dgvBlocks.Columns.Clear();

            int startNum = 1, numLength = 2;
            int.TryParse(txtSuffixStart.Text, out startNum);
            int.TryParse(txtSuffixLength.Text, out numLength);
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            var previewValues = _suffixEngine.GenerateNumberSequence(_currentResult.Blocks.Count, prefix, suffix, startNum, numLength);

            // 使用 BlockRowData 包装，保存原始索引
            _currentBlockRows = _currentResult.Blocks.Select((b, idx) => new BlockRowData
            {
                OriginalIndex = idx,
                块名 = b.BlockName,
                图号 = GetAttributeMulti(b, "图号", "编号", "TH", "XH", "BH", "DRAWING_NO") ?? "",
                图名 = GetAttributeMulti(b, "图名", "NAME", "TM", "DRAWINGNAME", "TNAME") ?? "",
                比例 = GetAttributeMulti(b, "比例", "BL", "SCALE", "缩放比例") ?? "",
                重编预览 = idx < previewValues.Count ? previewValues[idx] : ""
            }).ToList();

            dgvBlocks.DataSource = _currentBlockRows;

            // 设置列宽以支持横向滚动
            dgvBlocks.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);

            RefreshBlockNameFilter();
        }

        private void FilterBlocksByName()
        {
            if (_isUpdatingFilter) return;
            if (_currentResult == null || dgvBlocks == null) return;

            string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";

            if (string.IsNullOrEmpty(selectedFilter) || selectedFilter == "(全部)")
            {
                RefreshDataGridView();
            }
            else
            {
                var filteredBlocks = _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList();
                dgvBlocks.DataSource = null;
                dgvBlocks.Columns.Clear();

                int startNum = 1, numLength = 2;
                int.TryParse(txtSuffixStart.Text, out startNum);
                int.TryParse(txtSuffixLength.Text, out numLength);
                string prefix = txtSuffixPrefix.Text ?? "";
                string suffix = txtSuffixSuffix.Text ?? "";
                var previewValues = _suffixEngine.GenerateNumberSequence(filteredBlocks.Count, prefix, suffix, startNum, numLength);

                // 使用 BlockRowData 包装，保留原始索引以支持正确的删除/移动操作
                _currentBlockRows = filteredBlocks.Select((b, idx) => new BlockRowData
                {
                    OriginalIndex = idx,
                    块名 = b.BlockName,
                    图号 = GetAttributeMulti(b, "图号", "编号", "TH", "XH", "BH", "DRAWING_NO") ?? "",
                    图名 = GetAttributeMulti(b, "图名", "NAME", "TM", "DRAWINGNAME", "TNAME") ?? "",
                    比例 = GetAttributeMulti(b, "比例", "BL", "SCALE", "缩放比例") ?? "",
                    重编预览 = idx < previewValues.Count ? previewValues[idx] : ""
                }).ToList();

                dgvBlocks.DataSource = _currentBlockRows;

                // 设置列宽以支持横向滚动
                dgvBlocks.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            }
        }

        #region 列管理方法

        /// <summary>
        /// 添加新列
        /// </summary>
        private void AddColumn(string tag, string header)
        {
            if (string.IsNullOrEmpty(tag)) return;

            // 检查是否已存在
            if (_currentStyle.Columns.Any(c => c.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            {
                AppendLog($"列 '{header}' 已存在", Theme.Warning);
                return;
            }

            int newOrder = _currentStyle.Columns.Count;
            _currentStyle.Columns.Add(new ColumnDef
            {
                Tag = tag.ToUpperInvariant(),
                Header = header,
                Width = 40,
                Visible = true,
                Order = newOrder
            });

            RefreshColumnsList();
            UpdateColumnFormula();
            AppendLog($"已添加列: {header}", Theme.Success);
        }

        /// <summary>
        /// 删除选中的列
        /// </summary>
        private void RemoveSelectedColumn()
        {
            if (lstColumns == null || _currentStyle == null) return;

            if (lstColumns.SelectedIndex < 0)
            {
                AppendLog("请先选择要删除的列", Theme.Warning);
                return;
            }

            int idx = lstColumns.SelectedIndex;
            var visibleCols = _currentStyle.Columns.Where(c => c.Visible).OrderBy(c => c.Order).ToList();
            if (idx >= 0 && idx < visibleCols.Count)
            {
                var colToRemove = visibleCols[idx];
                _currentStyle.Columns.Remove(colToRemove);
                RefreshColumnsList();
                UpdateColumnFormula();
                AppendLog($"已删除列: {colToRemove.Header}", Theme.Warning);
            }
        }

        /// <summary>
        /// 上移选中列
        /// </summary>
        private void MoveColumnUp()
        {
            if (lstColumns == null || _currentStyle == null) return;
            if (lstColumns.SelectedIndex <= 0) return;

            int idx = lstColumns.SelectedIndex;
            var visibleCols = _currentStyle.Columns.Where(c => c.Visible).OrderBy(c => c.Order).ToList();
            if (idx > 0 && idx < visibleCols.Count)
            {
                // 交换顺序
                int orderIdx1 = visibleCols[idx].Order;
                int orderIdx2 = visibleCols[idx - 1].Order;
                visibleCols[idx].Order = orderIdx2;
                visibleCols[idx - 1].Order = orderIdx1;

                RefreshColumnsList();
                UpdateColumnFormula();
                lstColumns.SelectedIndex = idx - 1;
            }
        }

        /// <summary>
        /// 下移选中列
        /// </summary>
        private void MoveColumnDown()
        {
            if (lstColumns == null || _currentStyle == null) return;
            int idx = lstColumns.SelectedIndex;
            var visibleCols = _currentStyle.Columns.Where(c => c.Visible).OrderBy(c => c.Order).ToList();
            if (idx < 0 || idx >= visibleCols.Count - 1) return;

            // 交换顺序
            int orderIdx1 = visibleCols[idx].Order;
            int orderIdx2 = visibleCols[idx + 1].Order;
            visibleCols[idx].Order = orderIdx2;
            visibleCols[idx + 1].Order = orderIdx1;

            RefreshColumnsList();
            UpdateColumnFormula();
            lstColumns.SelectedIndex = idx + 1;
        }

        /// <summary>
        /// 刷新列列表显示
        /// </summary>
        private void RefreshColumnsList()
        {
            if (lstColumns == null || _currentStyle == null) return;

            lstColumns.Items.Clear();
            var visibleCols = _currentStyle.Columns.Where(c => c.Visible).OrderBy(c => c.Order).ToList();
            foreach (var col in visibleCols)
            {
                lstColumns.Items.Add($"[{col.Width:F0}]{col.Header}({col.Tag})");
            }
        }

        /// <summary>
        /// 更新列宽公式
        /// </summary>
        private void UpdateColumnFormula()
        {
            if (_currentStyle == null) return;
            txtColumnFormula.Text = _currentStyle.GetFormulaWidths();
        }

        /// <summary>
        /// 开始拖拽获取列宽模式
        /// </summary>
        private void StartPickColumnWidthMode()
        {
            if (lstColumns == null || lstColumns.SelectedIndex < 0)
            {
                AppendLog("请先在列表中选择要设置宽度的列", Theme.Warning);
                return;
            }
            _isPickingColumnWidth = true;
            _selectedColumnIndexForPick = lstColumns.SelectedIndex;
            AppendLog("请在CAD中框选两个点来获取列宽...", Theme.Primary);
            ExecuteCommand("_BCPICKCOLWIDTH");
        }

        /// <summary>
        /// 应用获取到的列宽到选中的列
        /// </summary>
        internal void ApplyPickedColumnWidth(double width)
        {
            _isPickingColumnWidth = false;
            if (width <= 0)
            {
                AppendLog("列宽无效", Theme.Warning);
                return;
            }

            var visibleCols = _currentStyle.Columns.Where(c => c.Visible).OrderBy(c => c.Order).ToList();
            int targetIdx = _selectedColumnIndexForPick >= 0 ? _selectedColumnIndexForPick : 0;
            if (targetIdx < visibleCols.Count)
            {
                visibleCols[targetIdx].Width = width;
                RefreshColumnsList();
                UpdateColumnFormula();
                AppendLog($"已设置列宽: {width:F1}", Theme.Success);
            }
            _selectedColumnIndexForPick = -1;
        }

        #endregion

        private void RefreshBlockNameFilter()
        {
            if (cmbBlockNameFilter == null || _currentResult == null) return;

            string currentSelection = cmbBlockNameFilter.SelectedItem?.ToString() ?? "(全部)";

            _isUpdatingFilter = true;
            try
            {
                cmbBlockNameFilter.Items.Clear();
                cmbBlockNameFilter.Items.Add("(全部)");

                var distinctNames = _currentResult.Blocks.Select(b => b.BlockName).Distinct().OrderBy(n => n).ToList();
                cmbBlockNameFilter.Items.AddRange(distinctNames.ToArray());

                if (!string.IsNullOrEmpty(currentSelection) && cmbBlockNameFilter.Items.Contains(currentSelection))
                    cmbBlockNameFilter.SelectedItem = currentSelection;
                else
                    cmbBlockNameFilter.SelectedIndex = 0;
            }
            finally
            {
                _isUpdatingFilter = false;
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

            var hit = dgvBlocks.HitTest(
                dgvBlocks.PointToClient(new Point(e.X, e.Y)).X,
                dgvBlocks.PointToClient(new Point(e.X, e.Y)).Y);
            int targetIndex = hit.RowIndex;

            if (targetIndex < 0 || _dragRowIndex < 0 || targetIndex == _dragRowIndex) return;

            var draggedBlock = _currentResult.Blocks[_dragRowIndex];
            _currentResult.Blocks.RemoveAt(_dragRowIndex);
            _currentResult.Blocks.Insert(targetIndex, draggedBlock);

            RefreshDataGridView();

            if (targetIndex >= 0 && targetIndex < dgvBlocks.Rows.Count)
                dgvBlocks.Rows[targetIndex].Selected = true;

            _dragRowIndex = -1;
            _isDragging = false;
        }

        /// <summary>
        /// 列宽拖拽调整后触发 - 将调整后的列宽同步到样式配置
        /// </summary>
        private void DgvBlocks_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (dgvBlocks == null || _currentStyle?.Columns == null) return;
            if (_isResizingColumn) return;
            _isResizingColumn = true;

            try
            {
                var visibleCols = _currentStyle.Columns.Where(c => c.Visible).OrderBy(c => c.Order).ToList();
                int colIndex = e.Column.Index;

                // 尝试将像素转换为 CAD 单位（约简：像素 * 0.5 作为粗略估算）
                double widthInUnits = dgvBlocks.Columns[colIndex].Width * 0.5;
                if (colIndex < visibleCols.Count)
                {
                    visibleCols[colIndex].Width = Math.Max(5, widthInUnits);
                    // 更新公式文本框
                    txtColumnFormula.Text = _currentStyle.GetFormulaWidths();
                }
            }
            catch { }
            finally
            {
                _isResizingColumn = false;
            }
        }

        /// <summary>
        /// 行高拖拽调整后触发 - 将调整后的行高同步到样式配置
        /// </summary>
        private void DgvBlocks_RowHeightChanged(object sender, DataGridViewRowEventArgs e)
        {
            if (dgvBlocks == null || _currentStyle == null) return;
            if (_isResizingRow) return;
            _isResizingRow = true;

            try
            {
                // 将像素转换为 CAD 单位（约简：像素 * 0.5 作为粗略估算）
                double heightInUnits = e.Row.Height * 0.5;
                _currentStyle.RowHeight = Math.Max(2, heightInUnits);
                numRowHeight.Value = (decimal)_currentStyle.RowHeight;
            }
            catch { }
            finally
            {
                _isResizingRow = false;
            }
        }

        /// <summary>
        /// 重选块 - 清除当前数据并重新框选
        /// </summary>
        private void ReselectBlocks()
        {
            ClearData();
            ExecuteCommand("_BCSELECT");
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

            bool success = _suffixEngine.BulkRenameAttributes(
                targetBlocks, GetActiveTag(), prefix, suffix, startNum, numLength);

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

            var preview = _suffixEngine.GenerateNumberSequence(_currentResult.Blocks.Count, prefix, suffix, startNum, numLength);
            AppendLog("预览: " + string.Join(", ", preview.Take(4).ToArray()) + (preview.Count > 4 ? "..." : ""), Theme.TextDim);
        }

        private void SyncAttributesToBlocks()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("没有可同步的数据", Theme.Warning);
                return;
            }

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
                targetBlocks, GetActiveTag(), prefix, suffix, startNum, numLength);

            if (success)
            {
                RefreshDataGridView();
                AppendLog("属性同步完成", Theme.Success);
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
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                Plugin._pendingGenerateAfterPick = true;
                doc.SendStringToExecute("_BCPICKPOS ", true, false, true);
            }
        }

        public void OnBlocksSelected(ExtractionResult result)
        {
            try
            {
                // 应用当前排序模式，让缓冲区和输出保持一致
                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(result.Blocks, sortType, _sortTolerance, chkReverse.Checked);

                // 用排序后的块替换原始数据
                result.Blocks.Clear();
                result.Blocks.AddRange(sortedBlocks);

                _currentResult = result;
                _selectedBlockNames = result.Blocks.Select(b => b.BlockName).Distinct().ToList();
                _selectedTags = result.AllTags.ToList();
                RefreshDataGridView();
                AppendLog($"已提取 {result.Blocks.Count} 个块（{GetSortTypeName(sortType)}）", Theme.Success);
            }
            catch (Exception ex)
            {
                AppendLog($"处理提取结果失败: {ex.Message}", Theme.Error);
            }
        }

        private string GetSortTypeName(SortEngine.SortType sortType)
        {
            return sortType switch
            {
                SortEngine.SortType.LeftRight_TopBottom => "左右→上下",
                SortEngine.SortType.TopBottom_LeftRight => "上下→左右",
                SortEngine.SortType.SelectionOrder => "选择序",
                SortEngine.SortType.NumericOrder => "数值序",
                _ => "未知"
            };
        }

        /// <summary>
        /// 合并相同前缀的图名/图号
        /// 例如：建施-01, 建施-02, 建施-03 → 建施-01~03
        /// 例如：构造图(一)、构造图(二) → 构造图(一～二)
        /// </summary>
        private List<AttributeBlockData> MergeSamePrefixBlocks(List<AttributeBlockData> blocks)
        {
            if (blocks == null || blocks.Count <= 1) return blocks;

            var result = new List<AttributeBlockData>();
            var merged = new HashSet<int>();

            for (int i = 0; i < blocks.Count; i++)
            {
                if (merged.Contains(i)) continue;

                var current = blocks[i];
                var group = new List<AttributeBlockData> { current };
                merged.Add(i);

                // 获取当前块的图名和图号
                string tmCurrent = GetAttributeMulti(current, "图名", "NAME", "TM", "DRAWINGNAME") ?? "";
                string thCurrent = GetAttributeMulti(current, "图号", "TH", "XH", "BH", "编号") ?? "";

                // 找出前缀相同、末尾编号连续的条目
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    if (merged.Contains(j)) continue;

                    var next = blocks[j];
                    string tmNext = GetAttributeMulti(next, "图名", "NAME", "TM", "DRAWINGNAME") ?? "";
                    string thNext = GetAttributeMulti(next, "图号", "TH", "XH", "BH", "编号") ?? "";

                    // 检查图名是否只是末尾编号不同
                    bool tmMatch = HasSamePrefixWithDifferentSuffix(tmCurrent, tmNext);
                    bool thMatch = HasSamePrefixWithDifferentSuffix(thCurrent, thNext);

                    if (tmMatch || thMatch)
                    {
                        group.Add(next);
                        merged.Add(j);
                    }
                }

                // 如果一组有多于1个条目，执行合并
                if (group.Count > 1)
                {
                    var mergedBlock = CreateMergedBlock(group[0], group);
                    result.Add(mergedBlock);
                }
                else
                {
                    result.Add(current);
                }
            }

            return result;
        }

        /// <summary>
        /// 检查两个字符串是否只是末尾编号不同
        /// </summary>
        private bool HasSamePrefixWithDifferentSuffix(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return false;

            // 提取末尾的编号
            var (prefixA, suffixA) = ExtractPrefixAndSuffix(a);
            var (prefixB, suffixB) = ExtractPrefixAndSuffix(b);

            // 前缀必须相同，编号必须不同
            return prefixA == prefixB && suffixA != suffixB;
        }

        /// <summary>
        /// 提取字符串的前缀（不含编号部分）和末尾编号
        /// </summary>
        private (string prefix, string suffix) ExtractPrefixAndSuffix(string s)
        {
            if (string.IsNullOrEmpty(s)) return (s, "");

            // 尝试提取中文数字编号：如 (一)、(二)、(甲)、(乙)
            var chineseNumPattern = System.Text.RegularExpressions.Regex.Match(s, @"^(.+?)([一二三四五六七八九十甲乙丙丁]+)$");
            if (chineseNumPattern.Success)
            {
                return (chineseNumPattern.Groups[1].Value, chineseNumPattern.Groups[2].Value);
            }

            // 尝试提取括号内的中文编号：如 (1)、(2)
            var parenPattern = System.Text.RegularExpressions.Regex.Match(s, @"^(.+?)（(.+)）$");
            if (parenPattern.Success)
            {
                return (parenPattern.Groups[1].Value + "（" + parenPattern.Groups[2].Value + "）", "");
            }

            // 尝试提取末尾的数字编号：如 -01、_2、03
            var numPattern = System.Text.RegularExpressions.Regex.Match(s, @"^(.+?)[-_]?(\d+)$");
            if (numPattern.Success)
            {
                return (numPattern.Groups[1].Value, numPattern.Groups[2].Value);
            }

            return (s, "");
        }

        /// <summary>
        /// 创建合并后的块数据
        /// </summary>
        private AttributeBlockData CreateMergedBlock(AttributeBlockData first, List<AttributeBlockData> group)
        {
            string tmFirst = GetAttributeMulti(first, "图名", "NAME", "TM", "DRAWINGNAME") ?? "";
            string thFirst = GetAttributeMulti(first, "图号", "TH", "XH", "BH", "编号") ?? "";

            var merged = new AttributeBlockData
            {
                BlockId = first.BlockId,
                BlockName = first.BlockName,
                Position = first.Position,
                SelectionOrder = first.SelectionOrder,
                Attributes = new Dictionary<string, string>(first.Attributes, StringComparer.OrdinalIgnoreCase)
            };

            // 合并图名
            if (!string.IsNullOrEmpty(tmFirst))
            {
                var (prefix, suffix) = ExtractPrefixAndSuffix(tmFirst);
                if (!string.IsNullOrEmpty(suffix))
                {
                    // 收集所有编号
                    var suffixes = new List<string> { suffix };
                    foreach (var block in group.Skip(1))
                    {
                        string tm = GetAttributeMulti(block, "图名", "NAME", "TM", "DRAWINGNAME") ?? "";
                        var (_, suf) = ExtractPrefixAndSuffix(tm);
                        if (!string.IsNullOrEmpty(suf)) suffixes.Add(suf);
                    }
                    suffixes = suffixes.Distinct().OrderBy(x => x).ToList();
                    merged.SetAttribute("图名", prefix + "(" + string.Join("～", suffixes) + ")");
                }
            }

            // 合并图号
            if (!string.IsNullOrEmpty(thFirst))
            {
                var (prefix, suffix) = ExtractPrefixAndSuffix(thFirst);
                if (!string.IsNullOrEmpty(suffix))
                {
                    var suffixes = new List<string> { suffix };
                    foreach (var block in group.Skip(1))
                    {
                        string th = GetAttributeMulti(block, "图号", "TH", "XH", "BH", "编号") ?? "";
                        var (_, suf) = ExtractPrefixAndSuffix(th);
                        if (!string.IsNullOrEmpty(suf)) suffixes.Add(suf);
                    }
                    suffixes = suffixes.Distinct().OrderBy(x => x).ToList();
                    merged.SetAttribute("图号", prefix + "(" + string.Join("～", suffixes) + ")");
                }
            }

            return merged;
        }

        public void OnInsertPointSelected(Point3d pos)
        {
            AppendLog($"插入点已设置: ({pos.X:F1}, {pos.Y:F1})", Theme.Success);

            try
            {
                string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";
                var targetBlocks = (selectedFilter != "(全部)")
                    ? _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList()
                    : _currentResult.Blocks;

                if (targetBlocks.Count == 0)
                {
                    AppendLog("筛选范围内没有可生成的块数据", Theme.Warning);
                    return;
                }

                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(targetBlocks, sortType, _sortTolerance, chkReverse.Checked);

                // 如果勾选了合并选项，则合并相同前缀的条目
                if (chkMergeSameName?.Checked == true)
                {
                    sortedBlocks = MergeSamePrefixBlocks(sortedBlocks);
                    AppendLog($"已合并相同前缀的条目，剩余 {sortedBlocks.Count} 项", Theme.TextDim);
                }

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                    targetLayout = cmbLayoutName.SelectedItem.ToString();

                var blockDataResult = new BlockDataResult();
                foreach (var attrBlock in sortedBlocks)
                {
                    blockDataResult.Blocks.Add(new BlockData
                    {
                        BlockName = attrBlock.BlockName,
                        ObjectId = attrBlock.BlockId,
                        Attributes = attrBlock.Attributes?
                            .Select(kv => new BlockAttribute { Tag = kv.Key, Value = kv.Value }).ToList()
                            ?? new List<BlockAttribute>()
                    });
                }

                var entities = _generator.Generate(blockDataResult, _currentStyle, pos, targetLayout);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (var docLock = doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
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

        public void OnBlockPicked(string blockName)
        {
            AppendLog($"已选择块: {blockName}", Theme.TextDim);
            if (!string.IsNullOrEmpty(blockName) && !_selectedBlockNames.Contains(blockName))
                _selectedBlockNames.Add(blockName);
        }

        private SortEngine.SortType GetSelectedSortType()
        {
            if (radSortLR_TB.Checked) return SortEngine.SortType.TopBottom_LeftRight;
            if (radSortTB_LR.Checked) return SortEngine.SortType.LeftRight_TopBottom;
            if (radSortSelection.Checked) return SortEngine.SortType.SelectionOrder;
            if (radSortNumeric.Checked) return SortEngine.SortType.NumericOrder;
            return SortEngine.SortType.SelectionOrder;
        }

        public void SafeInvoke(Action action)
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }

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

        public CatalogStyle GetCurrentStyle() => _currentStyle;
        public int GetBlockCount() => _currentResult?.Blocks?.Count ?? 0;

        public BlockDataResult GetCurrentBlockData()
        {
            if (_currentResult == null) return null;
            var result = new BlockDataResult
            {
                AllTags = _currentResult.AllTags,
                BlockNames = _currentResult.Blocks.Select(b => b.BlockName).Distinct().ToList(),
                LayerNames = new List<string>()
            };
            foreach (var attrBlock in _currentResult.Blocks)
            {
                result.Blocks.Add(new BlockData
                {
                    BlockName = attrBlock.BlockName,
                    ObjectId = attrBlock.BlockId,
                    Attributes = attrBlock.Attributes?
                        .Select(kv => new BlockAttribute { Tag = kv.Key, Value = kv.Value }).ToList()
                        ?? new List<BlockAttribute>()
                });
            }
            return result;
        }

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

        public bool GetLayoutRadioState()
        {
            return radLayout != null && radLayout.Checked;
        }

        public void DoGenerateCatalogDirect()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0)
            {
                AppendLog("没有可生成的块数据", Theme.Warning);
                return;
            }

            try
            {
                string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";
                var targetBlocks = (selectedFilter != "(全部)")
                    ? _currentResult.Blocks.Where(b => b.BlockName == selectedFilter).ToList()
                    : _currentResult.Blocks;

                if (targetBlocks.Count == 0)
                {
                    AppendLog("筛选范围内没有可生成的块数据", Theme.Warning);
                    return;
                }

                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(targetBlocks, sortType, _sortTolerance, chkReverse.Checked);

                // 如果勾选了合并选项，则合并相同前缀的条目
                if (chkMergeSameName?.Checked == true)
                {
                    sortedBlocks = MergeSamePrefixBlocks(sortedBlocks);
                    AppendLog($"已合并相同前缀的条目，剩余 {sortedBlocks.Count} 项", Theme.TextDim);
                }

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                    targetLayout = cmbLayoutName.SelectedItem.ToString();

                var blockDataResult = new BlockDataResult();
                foreach (var attrBlock in sortedBlocks)
                {
                    blockDataResult.Blocks.Add(new BlockData
                    {
                        BlockName = attrBlock.BlockName,
                        ObjectId = attrBlock.BlockId,
                        Attributes = attrBlock.Attributes?
                            .Select(kv => new BlockAttribute { Tag = kv.Key, Value = kv.Value }).ToList()
                            ?? new List<BlockAttribute>()
                    });
                }

                var pos = Plugin._pendingInsertPoint ?? new Point3d(0, 0, 0);
                var entities = _generator.Generate(blockDataResult, _currentStyle, pos, targetLayout);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (var docLock = doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
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
    }
}
