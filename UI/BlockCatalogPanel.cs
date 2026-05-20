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
                Size = new Size(boxW, 75),
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
                Location = new Point(180, 20),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            radSortNumeric = new RadioButton
            {
                Text = "数值序",
                Location = new Point(180, 44),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            chkReverse = new CheckBox
            {
                Text = "反序",
                Location = new Point(252, 20),
                AutoSize = true,
                ForeColor = Theme.Warning,
                BackColor = Theme.Card
            };

            grpSort.Controls.Add(radSortTB_LR);
            grpSort.Controls.Add(radSortLR_TB);
            grpSort.Controls.Add(radSortSelection);
            grpSort.Controls.Add(radSortNumeric);
            grpSort.Controls.Add(chkReverse);

            _canvasPanel.Controls.Add(grpSort);
            curY += 85;

            // === Box 3: 图形缓冲区与手动调序 ===
            var grpBuffer = new GroupBox
            {
                Text = "图形缓冲区与手动调序",
                Location = new Point(leftX, curY),
                Size = new Size(boxW, 200),
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
                Size = new Size(240, 138),
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
                ScrollBars = ScrollBars.Vertical,
                AllowDrop = true
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

            grpBuffer.Controls.Add(btnRemove);
            grpBuffer.Controls.Add(btnMoveUp);
            grpBuffer.Controls.Add(btnMoveDown);

            _canvasPanel.Controls.Add(grpBuffer);
            curY += 210;

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
                Size = new Size(boxW, 115),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            var lblForm = new Label { Text = "公式:", Location = new Point(12, 22), AutoSize = true, ForeColor = Theme.TextDim };
            txtColumnFormula = new TextBox
            {
                Location = new Point(50, 19),
                Width = 150,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                Text = "20+40+60"
            };
            var btnApplyForm = CreateFlatButton("应用", 210, 18, 42, Theme.Primary);
            btnApplyForm.Height = 20;
            btnApplyForm.Click += (s, e) => ApplyColumnFormula();
            var btnGetForm = CreateFlatButton("获取", 256, 18, 42, Theme.Accent);
            btnGetForm.Height = 20;
            btnGetForm.Click += (s, e) => txtColumnFormula.Text = _currentStyle.GetFormulaWidths();

            grpFormula.Controls.AddRange(new Control[] { lblForm, txtColumnFormula, btnApplyForm, btnGetForm });

            var lblFontH = new Label { Text = "字高", Location = new Point(12, 48), AutoSize = true, ForeColor = Theme.TextDim };
            numFontHeight = new NumericUpDown { Location = new Point(45, 46), Width = 45, Minimum = 1, Maximum = 20, Value = 3.5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            var lblRowH = new Label { Text = "行高", Location = new Point(100, 48), AutoSize = true, ForeColor = Theme.TextDim };
            numRowHeight = new NumericUpDown { Location = new Point(132, 46), Width = 45, Minimum = 1, Maximum = 50, Value = 5m, BackColor = Theme.InputBg, ForeColor = Theme.Text };
            chkShowHeader = new CheckBox { Text = "绘表头", Location = new Point(190, 47), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };

            grpFormula.Controls.AddRange(new Control[] { lblFontH, numFontHeight, lblRowH, numRowHeight, chkShowHeader });

            var lblOut = new Label { Text = "目标空间", Location = new Point(12, 76), AutoSize = true, ForeColor = Theme.TextDim };
            radModelSpace = new RadioButton { Text = "模型", Location = new Point(70, 74), AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            radLayout = new RadioButton { Text = "布局", Location = new Point(125, 74), AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Card };
            cmbLayoutName = new ComboBox
            {
                Location = new Point(180, 73),
                Width = 75,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            cmbLayoutName.Items.Add("Model");
            cmbLayoutName.SelectedIndex = 0;
            radLayout.CheckedChanged += (s, e) => { cmbLayoutName.Enabled = radLayout.Checked; };

            grpFormula.Controls.AddRange(new Control[] { lblOut, radModelSpace, radLayout, cmbLayoutName });

            _canvasPanel.Controls.Add(grpFormula);
            curY += 125;

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

        private string GetActiveTag()
        {
            if (_currentResult != null && _currentResult.Blocks.Count > 0)
            {
                var firstBlock = _currentResult.Blocks[0];
                if (firstBlock.GetAttribute("TH") != null) return "TH";
                if (firstBlock.GetAttribute("图号") != null) return "图号";
            }
            return "XH";
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

            int startNum = 1, numLength = 2;
            int.TryParse(txtSuffixStart.Text, out startNum);
            int.TryParse(txtSuffixLength.Text, out numLength);
            string prefix = txtSuffixPrefix.Text ?? "";
            string suffix = txtSuffixSuffix.Text ?? "";

            var previewValues = _suffixEngine.GenerateNumberSequence(_currentResult.Blocks.Count, prefix, suffix, startNum, numLength);

            dgvBlocks.DataSource = _currentResult.Blocks.Select((b, idx) => new
            {
                块名 = b.BlockName,
                图号 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                图名 = b.GetAttribute("TM") ?? "",
                重编预览 = idx < previewValues.Count ? previewValues[idx] : ""
            }).ToList();

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

                dgvBlocks.DataSource = filteredBlocks.Select((b, idx) => new
                {
                    块名 = b.BlockName,
                    图号 = b.GetAttribute("XH") ?? b.GetAttribute("TH") ?? "",
                    图名 = b.GetAttribute("TM") ?? "",
                    重编预览 = idx < previewValues.Count ? previewValues[idx] : ""
                }).ToList();
            }
        }

        private void RefreshBlockNameFilter()
        {
            if (cmbBlockNameFilter == null || _currentResult == null) return;

            string currentSelection = cmbBlockNameFilter.SelectedItem?.ToString() ?? "(全部)";

            _isUpdatingFilter = true;
            cmbBlockNameFilter.Items.Clear();
            cmbBlockNameFilter.Items.Add("(全部)");

            var distinctNames = _currentResult.Blocks.Select(b => b.BlockName).Distinct().OrderBy(n => n).ToList();
            cmbBlockNameFilter.Items.AddRange(distinctNames.ToArray());

            if (!string.IsNullOrEmpty(currentSelection) && cmbBlockNameFilter.Items.Contains(currentSelection))
                cmbBlockNameFilter.SelectedItem = currentSelection;
            else
                cmbBlockNameFilter.SelectedIndex = 0;
            _isUpdatingFilter = false;
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
                _currentResult = result;
                _selectedBlockNames = result.Blocks.Select(b => b.BlockName).Distinct().ToList();
                _selectedTags = result.AllTags.ToList();
                RefreshDataGridView();
                AppendLog($"已提取 {result.Blocks.Count} 个块", Theme.Success);
            }
            catch (Exception ex)
            {
                AppendLog($"处理提取结果失败: {ex.Message}", Theme.Error);
            }
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
                var sortedBlocks = _sortEngine.Sort(targetBlocks, sortType, 500.0, chkReverse.Checked);

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

                var sortType = GetSelectedSortType();
                var sortedBlocks = _sortEngine.Sort(targetBlocks, sortType, 500.0, chkReverse.Checked);

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
