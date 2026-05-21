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
        private GroupBox _grpSort;       // 空间矩阵排序模式
        private GroupBox _grpBuffer;     // 图形缓冲区
        private GroupBox _grpSuffix;     // 缀参数
        private GroupBox _grpFormula;    // 列宽表达式（列配置）
        private GroupBox _grpOutput;     // 输出参数
        private Label _lblSpacing;      // 间距公式标签
        private Button _btnPreview;      // 预览目录表格
        private Button _btnGenerate;     // 指定点绘制目录
        private Button _btnSyncAttrs;   // 一键反向同步
        private bool _isEditMode = false;
        private int _grpSuffix_baseY;  // 记录 _grpSuffix 原始 Y（用于编辑模式压缩布局）

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
        private CheckedListBox lstColumns; // 列管理列表框（CheckedListBox，支持可见性切换和双击编辑）
        private ComboBox cmbAddColumn; // 添加列下拉框
        private Dictionary<int, ColumnDef> _lstColumnMap = new Dictionary<int, ColumnDef>(); // 列表索引→ColumnDef 映射
        private Button _btnRemove; // 删块按钮
        private Button _btnMoveUp; // 上移按钮
        private Button _btnMoveDown; // 下移按钮
        private Button _btnReselect; // 重选按钮
        private Button _btnRemoveColumn; // 删列按钮

        // 列宽/行高拖拽相关
        private bool _isResizingColumn = false;
        private bool _isResizingRow = false;
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
                        AppendLog($"快捷键已保存: {key}（重启CAD后生效）", Theme.Success);
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
                Bounds = new Rectangle(0, 50, 340, 590),
                BackColor = Theme.Bg,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            BuildFixedLayoutCanvas();
            Controls.Add(_canvasPanel);

            // === 底部工具栏（固定在面板底部，不随滚动）===
            var bottomBar = new Panel
            {
                Location = new Point(10, 640),
                Size = new Size(312, 36),
                BackColor = Theme.Card
            };
            bottomBar.Controls.AddRange(new Control[] { btnReset, btnImport, btnExport });
            Controls.Add(bottomBar);

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
                Text = "工作模式",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 55),
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
            // 工作模式切换联动界面
            radCatalogMode.CheckedChanged += (s, e) => { if (radCatalogMode.Checked) SetUIMode(false); };
            radEditMode.CheckedChanged += (s, e) => { if (radEditMode.Checked) SetUIMode(true); };
            grpMode.Controls.Add(radCatalogMode);
            grpMode.Controls.Add(radEditMode);

            _canvasPanel.Controls.Add(grpMode);
            curY += 65;

            // 底部工具栏各按钮（稍后添加到 UserControl Controls 而非 scrollable canvas）
            btnReset = CreateFlatButton("重置面板", 0, 4, 75, Theme.Warning);
            btnReset.Height = 28;
            btnReset.Click += (s, e) => ResetPanel();
            btnImport = CreateFlatButton("导入配置", 82, 4, 75, Theme.Primary);
            btnImport.Height = 28;
            btnImport.Click += (s, e) => ImportSettings();
            btnExport = CreateFlatButton("导出当前", 164, 4, 75, Theme.AccentLight);
            btnExport.Height = 28;
            btnExport.Click += (s, e) => ExportSettings();

            // === Box 2: 空间矩阵排序模式 ===
            _grpSort = new GroupBox
            {
                Text = "排序模式",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 115),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            // 左子区：空间坐标排序
            var sortSpatial = new Panel
            {
                Location = new Point(8, 18),
                Size = new Size(145, 90),
                BackColor = Theme.Card
            };
            var lblSpatial = new Label { Text = "空间排序", Location = new Point(0, 0), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F) };
            radSortTB_LR = new RadioButton { Text = "上下 → 左右", Location = new Point(0, 18), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            radSortLR_TB = new RadioButton { Text = "左右 → 上下", Location = new Point(0, 40), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            sortSpatial.Controls.AddRange(new Control[] { lblSpatial, radSortTB_LR, radSortLR_TB });

            // 右子区：辅助排序
            var sortAux = new Panel
            {
                Location = new Point(158, 18),
                Size = new Size(145, 90),
                BackColor = Theme.Card
            };
            var lblAux = new Label { Text = "辅助排序", Location = new Point(0, 0), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F) };
            radSortSelection = new RadioButton { Text = "选择序", Location = new Point(0, 18), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            radSortNumeric = new RadioButton { Text = "数值序", Location = new Point(0, 40), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            chkReverse = new CheckBox { Text = "反序", Location = new Point(0, 62), AutoSize = true, ForeColor = Theme.Warning, BackColor = Theme.Card };
            var lblTol2 = new Label { Text = "容差", Location = new Point(55, 64), AutoSize = true, ForeColor = Theme.TextDim };
            var numTolerance = new NumericUpDown { Location = new Point(85, 62), Width = 55, Minimum = 10, Maximum = 5000, Value = 500, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            sortAux.Controls.AddRange(new Control[] { lblAux, radSortSelection, radSortNumeric, chkReverse, lblTol2, numTolerance });

            numTolerance.ValueChanged += (s, e) => { _sortTolerance = (double)numTolerance.Value; };
            _sortTolerance = (double)numTolerance.Value;

            // 排序模式切换时触发重排
            radSortTB_LR.CheckedChanged += (s, e) => { if (radSortTB_LR.Checked) ApplyCurrentSort(); };
            radSortLR_TB.CheckedChanged += (s, e) => { if (radSortLR_TB.Checked) ApplyCurrentSort(); };
            radSortSelection.CheckedChanged += (s, e) => { if (radSortSelection.Checked) ApplyCurrentSort(); };
            radSortNumeric.CheckedChanged += (s, e) => { if (radSortNumeric.Checked) ApplyCurrentSort(); };
            chkReverse.CheckedChanged += (s, e) => ApplyCurrentSort();

            // 添加控件到分组
            _grpSort.Controls.Add(sortSpatial);
            _grpSort.Controls.Add(sortAux);
            _canvasPanel.Controls.Add(_grpSort); curY += 125;

            // === Box 3: 图形缓冲区与手动调序 ===
            _grpBuffer = new GroupBox
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
            _grpBuffer.Controls.Add(cmbBlockNameFilter);

            var btnSelect = CreateFlatButton("框选图块", 200, 18, 98, Theme.Primary);
            btnSelect.Height = 24;
            btnSelect.Click += (s, e) => ExecuteCommand("_BCSELECT");
            _grpBuffer.Controls.Add(btnSelect);

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

            _grpBuffer.Controls.Add(dgvBlocks);

            _btnRemove = CreateFlatButton("删块", 258, 50, 42, Theme.Warning);
            _btnRemove.Height = 24;
            _btnRemove.Click += (s, e) => RemoveSelectedBlock();
            _btnMoveUp = CreateFlatButton("▲", 258, 90, 42, Theme.Primary);
            _btnMoveUp.Height = 24;
            _btnMoveUp.Click += (s, e) => MoveBlockUp();
            _btnMoveDown = CreateFlatButton("▼", 258, 120, 42, Theme.Primary);
            _btnMoveDown.Height = 24;
            _btnMoveDown.Click += (s, e) => MoveBlockDown();
            _btnReselect = CreateFlatButton("重选", 258, 150, 42, Theme.Accent);  // 新增重选按钮
            _btnReselect.Height = 24;
            _btnReselect.Click += (s, e) => ReselectBlocks();
            _grpBuffer.Controls.Add(_btnReselect);

            _grpBuffer.Controls.Add(_btnRemove);
            _grpBuffer.Controls.Add(_btnMoveUp);
            _grpBuffer.Controls.Add(_btnMoveDown);

            _canvasPanel.Controls.Add(_grpBuffer);
            curY += 270;  // 调整以匹配新的 _grpBuffer 高度(260)

            // === Box 4: 缀参数规律重编属性 ===
            _grpSuffix = new GroupBox
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

            _grpSuffix.Controls.AddRange(new Control[] { lblStart, txtSuffixStart, lblLen, txtSuffixLength, chkSuffixContinuous });

            var lblPrefix = new Label { Text = "前缀", Location = new Point(12, 48), AutoSize = true, ForeColor = Theme.TextDim };
            txtSuffixPrefix = new TextBox { Location = new Point(45, 45), Width = 75, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            var lblSuffix = new Label { Text = "后缀", Location = new Point(130, 48), AutoSize = true, ForeColor = Theme.TextDim };
            txtSuffixSuffix = new TextBox { Location = new Point(162, 45), Width = 65, BackColor = Theme.InputBg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };

            _grpSuffix.Controls.AddRange(new Control[] { lblPrefix, txtSuffixPrefix, lblSuffix, txtSuffixSuffix });

            // 缀参数变化时实时刷新预览列
            txtSuffixStart.TextChanged += (s, e) => RefreshDataGridView();
            txtSuffixLength.TextChanged += (s, e) => RefreshDataGridView();
            txtSuffixPrefix.TextChanged += (s, e) => RefreshDataGridView();
            txtSuffixSuffix.TextChanged += (s, e) => RefreshDataGridView();

            var btnApplySuffix = CreateFlatButton("应用缀编号", 12, 73, 90, Theme.Success);
            btnApplySuffix.Height = 22;
            btnApplySuffix.Click += (s, e) => ApplySuffixRename();
            var btnPreviewSuffix = CreateFlatButton("预览序列", 110, 73, 65, Theme.Primary);
            btnPreviewSuffix.Height = 22;
            btnPreviewSuffix.Click += (s, e) => PreviewSuffixRename();

            _grpSuffix.Controls.AddRange(new Control[] { btnApplySuffix, btnPreviewSuffix });

            _canvasPanel.Controls.Add(_grpSuffix);
            curY += 115;

            // === Box 5a: 列配置 ===
            _grpFormula = new GroupBox
            {
                Text = "列配置",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 200),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            // 第一行：列管理标签
            int row1Y = 20;
            var lblColMgr = new Label { Text = "列管理:", Location = new Point(12, row1Y), AutoSize = true, ForeColor = Theme.TextDim, Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold) };
            _grpFormula.Controls.Add(lblColMgr);

            // 第二行：下拉框 + 按钮
            int row2Y = 40;
            cmbAddColumn = new ComboBox
            {
                Location = new Point(12, row2Y),
                Width = 90,
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
            _grpFormula.Controls.Add(cmbAddColumn);

            int btnX = 108;
            var btnAddColumn = CreateFlatButton("增加列", btnX, row2Y - 2, 50, Theme.Success);
            btnAddColumn.Height = 22;
            btnAddColumn.Click += (s, e) =>
            {
                if (cmbAddColumn.SelectedItem != null)
                {
                    string selected = cmbAddColumn.SelectedItem.ToString();
                    string tag, header;
                    if (selected.StartsWith("__CUSTOM__|"))
                    {
                        var dialog = new AddColumnDialog();
                        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        tag = dialog.ColumnName.Trim();
                        header = dialog.ColumnName.Trim();
                        if (string.IsNullOrEmpty(tag)) return;
                    }
                    else
                    {
                        string[] parts = selected.Replace("|", "-").Split('-');
                        tag = parts[0].Trim();
                        header = parts.Length > 1 ? parts[1].Trim() : tag;
                    }
                    AddColumn(tag, header);
                }
            };
            _grpFormula.Controls.Add(btnAddColumn);

            btnX += 55;
            _btnRemoveColumn = CreateFlatButton("删除列", btnX, row2Y - 2, 50, Theme.Warning);
            _btnRemoveColumn.Height = 22;
            _btnRemoveColumn.Click += (s, e) => RemoveSelectedColumn();
            _grpFormula.Controls.Add(_btnRemoveColumn);

            btnX += 55;
            var btnColUp = CreateFlatButton("▲", btnX, row2Y - 2, 24, Theme.Primary);
            btnColUp.Height = 22;
            btnColUp.Click += (s, e) => MoveColumnUp();
            _grpFormula.Controls.Add(btnColUp);

            btnX += 27;
            var btnColDown = CreateFlatButton("▼", btnX, row2Y - 2, 24, Theme.Primary);
            btnColDown.Height = 22;
            btnColDown.Click += (s, e) => MoveColumnDown();
            _grpFormula.Controls.Add(btnColDown);

            // 第三行：列列表（CheckedListBox）
            int row3Y = 65;
            lstColumns = new CheckedListBox
            {
                Location = new Point(12, row3Y),
                Size = new Size(288, 50),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 8F),
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                HorizontalScrollbar = true
            };
            lstColumns.ItemCheck += LstColumns_ItemCheck;
            lstColumns.MouseDoubleClick += (s, e) =>
            {
                var info = lstColumns.IndexFromPoint(e.Location);
                if (info < 0 || info >= lstColumns.Items.Count) return;
                var col = _lstColumnMap[info];
                var dialog = new EditColumnHeaderDialog(col.Header);
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    col.Header = dialog.HeaderName;
                    RefreshColumnsList();
                }
            };
            _grpFormula.Controls.Add(lstColumns);

            // 第四行：列宽公式
            int row4Y = 120;
            var lblForm = new Label { Text = "公式:", Location = new Point(12, row4Y + 2), AutoSize = true, ForeColor = Theme.TextDim };
            _grpFormula.Controls.Add(lblForm);

            txtColumnFormula = new TextBox
            {
                Location = new Point(45, row4Y),
                Width = 120,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                Text = "20+40+60"
            };
            _grpFormula.Controls.Add(txtColumnFormula);

            int formBtnX = 170;
            var btnApplyForm = CreateFlatButton("应用", formBtnX, row4Y - 2, 38, Theme.Primary);
            btnApplyForm.Height = 20;
            btnApplyForm.Click += (s, e) => ApplyColumnFormula();
            _grpFormula.Controls.Add(btnApplyForm);

            formBtnX += 43;
            var btnGetForm = CreateFlatButton("获取", formBtnX, row4Y - 2, 38, Theme.Accent);
            btnGetForm.Height = 20;
            btnGetForm.Click += (s, e) => txtColumnFormula.Text = _currentStyle.GetFormulaWidths();
            _grpFormula.Controls.Add(btnGetForm);

            formBtnX += 43;
            var btnPickColWidth = CreateFlatButton("拖拽列宽", formBtnX, row4Y - 2, 44, Theme.AccentLight);
            btnPickColWidth.Height = 20;
            btnPickColWidth.Click += (s, e) => StartPickColumnWidthMode();
            _grpFormula.Controls.Add(btnPickColWidth);

            _canvasPanel.Controls.Add(_grpFormula);
            curY += 205;

            // === Box 5b: 输出参数 ===
            _grpOutput = new GroupBox
            {
                Text = "输出参数",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 90),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            // 第一行：字高、行高、表头
            int outRow1Y = 20;
            var lblFontH = new Label { Text = "字高", Location = new Point(12, outRow1Y + 2), AutoSize = true, ForeColor = Theme.TextDim };
            _grpOutput.Controls.Add(lblFontH);
            numFontHeight = new NumericUpDown { Location = new Point(45, outRow1Y), Width = 40, Minimum = 1, Maximum = 20, Value = 3.5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            _grpOutput.Controls.Add(numFontHeight);

            var lblRowH = new Label { Text = "行高", Location = new Point(95, outRow1Y + 2), AutoSize = true, ForeColor = Theme.TextDim };
            _grpOutput.Controls.Add(lblRowH);
            numRowHeight = new NumericUpDown { Location = new Point(128, outRow1Y), Width = 40, Minimum = 1, Maximum = 50, Value = 5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            _grpOutput.Controls.Add(numRowHeight);

            chkShowHeader = new CheckBox { Text = "表头", Location = new Point(180, outRow1Y + 2), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            _grpOutput.Controls.Add(chkShowHeader);

            // 第二行：目标空间 + 拖拽尺寸按钮
            int outRow2Y = 48;
            var lblOut = new Label { Text = "目标:", Location = new Point(12, outRow2Y + 2), AutoSize = true, ForeColor = Theme.TextDim };
            _grpOutput.Controls.Add(lblOut);
            radModelSpace = new RadioButton { Text = "模型", Location = new Point(48, outRow2Y), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            _grpOutput.Controls.Add(radModelSpace);
            radLayout = new RadioButton { Text = "布局", Location = new Point(90, outRow2Y), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            _grpOutput.Controls.Add(radLayout);
            cmbLayoutName = new ComboBox
            {
                Location = new Point(120, outRow2Y - 2),
                Width = 65,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            cmbLayoutName.Items.Add("Model");
            cmbLayoutName.SelectedIndex = 0;
            radLayout.CheckedChanged += (s, e) =>
            {
                cmbLayoutName.Enabled = radLayout.Checked;
                if (radLayout.Checked) RefreshLayoutNames();
            };
            _grpOutput.Controls.Add(cmbLayoutName);

            var btnPickDimSize = CreateFlatButton("拖拽获取尺寸", 200, outRow2Y - 2, 80, Theme.Primary);
            btnPickDimSize.Height = 22;
            btnPickDimSize.Click += (s, e) =>
            {
                var style = GetCurrentStyle();
                style.UseMouseDefineSize = true;
                AppendLog("请在CAD中拖拽定义表格尺寸...", Theme.Primary);
                ExecuteCommand("_BCGENPOS");
            };
            _grpOutput.Controls.Add(btnPickDimSize);

            _canvasPanel.Controls.Add(_grpOutput);
            curY += 95;

            // === 间距公式 ===
            _lblSpacing = new Label { Text = "间距公式:", Location = new Point(leftX + 6, curY + 4), AutoSize = true, ForeColor = Theme.TextDim };
            txtSpacingExpression = new TextBox
            {
                Location = new Point(leftX + 70, curY + 2),
                Width = 238,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "5"
            };
            _canvasPanel.Controls.Add(_lblSpacing);
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
            _btnPreview = CreateFlatButton("预览目录表格", boxW, Theme.Primary);
            _btnPreview.Location = new Point(leftX, curY);
            _btnPreview.Height = 30;
            _btnPreview.Click += (s, e) => ShowPreview();
            _canvasPanel.Controls.Add(_btnPreview);
            curY += 36;

            _btnGenerate = CreateFlatButton("指定点绘制目录", boxW, Theme.Success);
            _btnGenerate.Location = new Point(leftX, curY);
            _btnGenerate.Height = 36;
            _btnGenerate.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            _btnGenerate.Click += (s, e) => GenerateToPosition();
            _canvasPanel.Controls.Add(_btnGenerate);
            curY += 42;

            _btnSyncAttrs = CreateFlatButton("一键反向同步至图纸块属性", boxW, Theme.AccentLight);
            _btnSyncAttrs.Location = new Point(leftX, curY);
            _btnSyncAttrs.Height = 30;
            _btnSyncAttrs.Click += (s, e) => SyncAttributesToBlocks();
            _canvasPanel.Controls.Add(_btnSyncAttrs);

            // 确保面板可滚动（内容总高度约1200px）
            _canvasPanel.AutoScrollMinSize = new Size(0, 1200);

            // 记录各组原始 Y（用于编辑模式压缩布局）
            if (_grpSuffix != null) _grpSuffix_baseY = _grpSuffix.Location.Y;
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
            UpdateButtonStates();
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
            UpdateButtonStates();
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
            if (IsFilterActive()) { AppendLog("过滤模式下不支持删除，请切换到\"(全部)\"后操作", Theme.Warning); return; }
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
            if (IsFilterActive()) { AppendLog("过滤模式下不支持移动，请切换到\"(全部)\"后操作", Theme.Warning); return; }
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
            if (IsFilterActive()) { AppendLog("过滤模式下不支持移动，请切换到\"(全部)\"后操作", Theme.Warning); return; }
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
            if (_isUpdatingFilter) return;
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

        private bool IsFilterActive()
        {
            string selected = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "";
            return !string.IsNullOrEmpty(selected) && selected != "(全部)";
        }

        /// <summary>
        /// 根据当前排序模式重新排序并刷新视图
        /// </summary>
        private void ApplyCurrentSort()
        {
            if (_currentResult == null || _currentResult.Blocks.Count == 0) return;
            var sortType = GetSelectedSortType();
            var sorted = _sortEngine.Sort(_currentResult.Blocks, sortType, _sortTolerance, chkReverse.Checked);
            _currentResult.Blocks.Clear();
            _currentResult.Blocks.AddRange(sorted);
            RefreshDataGridView();
        }

        /// <summary>
        /// 设置工作模式界面联动
        /// </summary>
        /// <param name="isEditMode">true=更改图号模式，false=生成目录模式</param>
        private void SetUIMode(bool isEditMode)
        {
            _isEditMode = isEditMode;
            // 生成目录模式：显示所有目录生成相关控件
            // 更改图号模式：隐藏列配置、公式、输出按钮等，压缩布局填补空白

            // 切换可见性
            if (_grpSort != null) _grpSort.Visible = !isEditMode;
            if (_grpFormula != null) _grpFormula.Visible = !isEditMode;
            if (_grpOutput != null) _grpOutput.Visible = !isEditMode;
            if (_lblSpacing != null) _lblSpacing.Visible = !isEditMode;
            if (txtSpacingExpression != null) txtSpacingExpression.Visible = !isEditMode;
            if (_btnPreview != null) _btnPreview.Visible = !isEditMode;
            if (_btnGenerate != null) _btnGenerate.Visible = !isEditMode;
            if (_grpBuffer != null) _grpBuffer.Visible = true;
            if (_grpSuffix != null) _grpSuffix.Visible = true;
            if (_btnSyncAttrs != null)
            {
                _btnSyncAttrs.Visible = isEditMode; // 一键反向同步仅在更改图号模式显示
            }

            // 编辑模式：将 _grpSuffix 上移填补 grpSort 隐藏后的空白
            // shift = grpSort 高度 + grpSort 与 grpBuffer 之间的间距(5px)
            int sortHeight = (_grpSort != null) ? _grpSort.Height : 0;
            int sortToBufferGap = 5;
            int suffixShift = isEditMode ? sortHeight + sortToBufferGap : 0;
            if (_grpSuffix != null)
            {
                _grpSuffix.Location = new Point(_grpSuffix.Location.X, _grpSuffix_baseY - suffixShift);
            }

            // 更新底栏和日志面板位置（编辑模式也上移同距离）
            foreach (Control c in this.Controls)
            {
                if (c is Panel && c.Location.Y == 640) // 底栏
                    c.Location = new Point(c.Location.X, 640 - suffixShift);
                if (c is Panel && c.Location.Y == 680) // 日志面板
                    c.Location = new Point(c.Location.X, 680 - suffixShift);
            }

            AppendLog(isEditMode ? "已切换到：更改图号模式" : "已切换到：生成目录模式", Theme.TextDim);
            UpdateButtonStates();
        }

        /// <summary>
        /// 根据当前数据状态更新按钮启用/禁用状态
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasData = _currentResult != null && _currentResult.Blocks.Count > 0;
            bool isEditMode = radEditMode?.Checked == true;

            // 数据相关按钮
            if (_btnPreview != null) _btnPreview.Enabled = hasData && !isEditMode;
            if (_btnGenerate != null) _btnGenerate.Enabled = hasData && !isEditMode;
            if (_btnSyncAttrs != null) _btnSyncAttrs.Enabled = hasData && isEditMode;
            // 缓冲区操作按钮
            SetControlEnabled(_btnRemove, hasData);
            SetControlEnabled(_btnMoveUp, hasData);
            SetControlEnabled(_btnMoveDown, hasData);
            SetControlEnabled(_btnReselect, hasData);
        }

        private void SetControlEnabled(Control c, bool enabled)
        {
            if (c != null) c.Enabled = enabled;
        }

        /// <summary>
        /// 动态刷新布局名下拉框：从当前文档获取所有布局
        /// </summary>
        private void RefreshLayoutNames()
        {
            if (cmbLayoutName == null) return;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            cmbLayoutName.Items.Clear();
            cmbLayoutName.Items.Add("Model");

            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var layoutDict = doc.Database.LayoutDictionaryId;
                    var dict = tr.GetObject(layoutDict, OpenMode.ForRead) as DBDictionary;
                    if (dict != null)
                    {
                        foreach (var entry in dict)
                        {
                            if (entry.Key != "Model")
                                cmbLayoutName.Items.Add(entry.Key);
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }

            if (cmbLayoutName.Items.Count > 0)
                cmbLayoutName.SelectedIndex = 0;
        }

        /// <summary>
        /// 动态刷新添加列下拉框：从当前提取结果的 AllTags 生成
        /// </summary>
        private void RefreshAddColumnCombo()
        {
            if (cmbAddColumn == null) return;
            cmbAddColumn.Items.Clear();
            // 内置常用列
            cmbAddColumn.Items.Add("XH|序号");
            cmbAddColumn.Items.Add("TH|图号");
            cmbAddColumn.Items.Add("TM|图名");
            cmbAddColumn.Items.Add("BL|比例");
            cmbAddColumn.Items.Add("DH|图别");
            cmbAddColumn.Items.Add("JZ|建筑");
            cmbAddColumn.Items.Add("GC|工程");
            // 从实际提取结果追加 Tag
            if (_currentResult?.AllTags != null)
            {
                foreach (var tag in _currentResult.AllTags)
                {
                    if (cmbAddColumn.Items.Cast<string>().Any(i => i.StartsWith(tag + "|"))) continue;
                    cmbAddColumn.Items.Add($"{tag}|{tag}");
                }
            }
            // 添加自定义项
            cmbAddColumn.Items.Add("__CUSTOM__|自定义...");
            cmbAddColumn.SelectedIndex = 0;
        }

        private void FilterBlocksByName()
        {
            if (_isUpdatingFilter) return;
            if (_currentResult == null || dgvBlocks == null) return;

            string selectedFilter = cmbBlockNameFilter?.SelectedItem?.ToString() ?? "(全部)";

            if (string.IsNullOrEmpty(selectedFilter) || selectedFilter == "(全部)")
            {
                RefreshDataGridView();
                UpdateButtonStates();
            }
            else
            {
                var filteredBlocks = _currentResult.Blocks.Where(b => string.Equals(b.BlockName, selectedFilter, StringComparison.OrdinalIgnoreCase)).ToList();
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
                    // 找到该块在原始列表中的索引
                    OriginalIndex = _currentResult.Blocks.IndexOf(b),
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
            UpdateButtonStates();
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
            if (!_lstColumnMap.ContainsKey(idx)) return;
            var colToRemove = _lstColumnMap[idx];
            _currentStyle.Columns.Remove(colToRemove);
            RefreshColumnsList();
            UpdateColumnFormula();
            UpdateButtonStates();
            AppendLog($"已删除列: {colToRemove.Header}", Theme.Warning);
        }

        /// <summary>
        /// 上移选中列
        /// </summary>
        private void MoveColumnUp()
        {
            if (lstColumns == null || _currentStyle == null) return;
            if (lstColumns.SelectedIndex <= 0) return;

            int idx = lstColumns.SelectedIndex;
            if (!_lstColumnMap.ContainsKey(idx) || !_lstColumnMap.ContainsKey(idx - 1)) return;

            // 通过 _lstColumnMap 取列，保证与列表索引一致
            var colCurr = _lstColumnMap[idx];
            var colPrev = _lstColumnMap[idx - 1];
            int tmpOrder = colCurr.Order;
            colCurr.Order = colPrev.Order;
            colPrev.Order = tmpOrder;

            RefreshColumnsList();
            UpdateColumnFormula();
            UpdateButtonStates();
            lstColumns.SelectedIndex = idx - 1;
        }

        /// <summary>
        /// 下移选中列
        /// </summary>
        private void MoveColumnDown()
        {
            if (lstColumns == null || _currentStyle == null) return;
            int idx = lstColumns.SelectedIndex;
            if (idx < 0 || !_lstColumnMap.ContainsKey(idx) || !_lstColumnMap.ContainsKey(idx + 1)) return;

            // 通过 _lstColumnMap 取列，保证与列表索引一致
            var colCurr = _lstColumnMap[idx];
            var colNext = _lstColumnMap[idx + 1];
            int tmpOrder = colCurr.Order;
            colCurr.Order = colNext.Order;
            colNext.Order = tmpOrder;

            RefreshColumnsList();
            UpdateColumnFormula();
            UpdateButtonStates();
            lstColumns.SelectedIndex = idx + 1;
        }

        /// <summary>
        /// 刷新列列表显示
        /// </summary>
        private void RefreshColumnsList()
        {
            if (lstColumns == null || _currentStyle == null) return;

            int selIdx = lstColumns.SelectedIndex;
            // 临时禁用 ItemCheck 事件，避免刷新时触发 Visible 同步
            lstColumns.ItemCheck -= LstColumns_ItemCheck;
            lstColumns.Items.Clear();
            _lstColumnMap.Clear();

            var ordered = _currentStyle.Columns.OrderBy(c => c.Order).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var col = ordered[i];
                lstColumns.Items.Add($"[{col.Width:F0}]{col.Header}({col.Tag})");
                lstColumns.SetItemChecked(i, col.Visible);
                _lstColumnMap[i] = col;
            }

            lstColumns.ItemCheck += LstColumns_ItemCheck;

            if (selIdx >= 0 && selIdx < lstColumns.Items.Count)
                lstColumns.SelectedIndex = selIdx;
            else if (lstColumns.Items.Count > 0)
                lstColumns.SelectedIndex = 0;
        }

        private void LstColumns_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstColumns.Items.Count || !_lstColumnMap.ContainsKey(e.Index)) return;
            // 捕获 ColumnDef 引用而非索引，避免 BeginInvoke 回调时 _lstColumnMap 已被刷新
            var col = _lstColumnMap[e.Index];
            bool wasChecked = lstColumns.GetItemChecked(e.Index);
            this.BeginInvoke(new Action(() =>
            {
                col.Visible = wasChecked;
                UpdateColumnFormula();
            }));
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
            if (!_lstColumnMap.ContainsKey(lstColumns.SelectedIndex))
            {
                AppendLog("列索引无效", Theme.Warning);
                return;
            }
            // 记录目标列在有序列表中的位置
            _selectedColumnIndexForPick = lstColumns.SelectedIndex;
            AppendLog("请在CAD中框选两个点来获取列宽...", Theme.Primary);
            ExecuteCommand("_BCPICKCOLWIDTH");
        }

        /// <summary>
        /// 应用获取到的列宽到选中的列
        /// </summary>
        internal void ApplyPickedColumnWidth(double width)
        {
            if (width <= 0)
            {
                AppendLog("列宽无效", Theme.Warning);
                return;
            }

            int targetIdx = _selectedColumnIndexForPick >= 0 ? _selectedColumnIndexForPick : 0;
            if (_lstColumnMap.ContainsKey(targetIdx))
            {
                _lstColumnMap[targetIdx].Width = width;
                RefreshColumnsList();
                UpdateColumnFormula();
                AppendLog($"已设置列宽: {width:F1}", Theme.Success);
            }
            else
            {
                AppendLog("未找到对应列", Theme.Warning);
            }
            _selectedColumnIndexForPick = -1;
        }

        internal void ApplyPickedRowHeight(double height)
        {
            if (height <= 0)
            {
                AppendLog("行高无效", Theme.Warning);
                return;
            }

            _currentStyle.RowHeight = height;
            if (numRowHeight != null) numRowHeight.Value = (decimal)Math.Min(height, 50);
            AppendLog($"已设置行高: {height:F1}", Theme.Success);
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
            if (IsFilterActive()) { AppendLog("过滤模式下不支持拖拽排序，请切换到\"(全部)\"后操作", Theme.Warning); return; }

            var hit = dgvBlocks.HitTest(
                dgvBlocks.PointToClient(new Point(e.X, e.Y)).X,
                dgvBlocks.PointToClient(new Point(e.X, e.Y)).Y);
            int targetIndex = hit.RowIndex;

            if (targetIndex < 0 || _dragRowIndex < 0 || targetIndex == _dragRowIndex) return;

            // 使用 BlockRowData 中保存的原始索引来获取正确的块
            int originalIdx = _dragRowIndex < _currentBlockRows.Count ? _currentBlockRows[_dragRowIndex].OriginalIndex : _dragRowIndex;
            if (originalIdx < 0 || originalIdx >= _currentResult.Blocks.Count) return;

            var draggedBlock = _currentResult.Blocks[originalIdx];
            _currentResult.Blocks.RemoveAt(originalIdx);
            _currentResult.Blocks.Insert(targetIndex, draggedBlock);

            RefreshDataGridView();

            // 选中移动后的行（因为 RefreshDataGridView 会重建控件，所以直接选 targetIndex）
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
                // 保存原始选择顺序，不在这里排序
                _currentResult = result;
                _selectedBlockNames = result.Blocks.Select(b => b.BlockName).Distinct().ToList();
                _selectedTags = result.AllTags.ToList();

                // 动态更新列下拉框（从实际提取的 Tag 生成）
                RefreshAddColumnCombo();

                // 刷新缓冲区显示
                RefreshDataGridView();
                UpdateButtonStates();
                AppendLog($"已提取 {result.Blocks.Count} 个块（原始选择顺序）", Theme.Success);
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
                return (parenPattern.Groups[1].Value, parenPattern.Groups[2].Value);
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

                // 直接使用当前已排序的 _currentResult.Blocks（单次排序在 OnBlocksSelected 或排序按钮变化时完成）
                // 如果勾选了合并选项，则合并相同前缀的条目
                if (chkMergeSameName?.Checked == true)
                {
                    targetBlocks = MergeSamePrefixBlocks(targetBlocks);
                    AppendLog($"已合并相同前缀的条目，剩余 {targetBlocks.Count} 项", Theme.TextDim);
                }

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                    targetLayout = cmbLayoutName.SelectedItem.ToString();

                var blockDataResult = new BlockDataResult();
                foreach (var attrBlock in targetBlocks)
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
            if (radSortLR_TB.Checked) return SortEngine.SortType.LeftRight_TopBottom;
            if (radSortTB_LR.Checked) return SortEngine.SortType.TopBottom_LeftRight;
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

                // 直接使用当前已排序的数据（单次排序在排序按钮变化时完成）
                if (chkMergeSameName?.Checked == true)
                {
                    targetBlocks = MergeSamePrefixBlocks(targetBlocks);
                    AppendLog($"已合并相同前缀的条目，剩余 {targetBlocks.Count} 项", Theme.TextDim);
                }

                string targetLayout = null;
                if (radLayout.Checked && cmbLayoutName.SelectedItem != null)
                    targetLayout = cmbLayoutName.SelectedItem.ToString();

                var blockDataResult = new BlockDataResult();
                foreach (var attrBlock in targetBlocks)
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
