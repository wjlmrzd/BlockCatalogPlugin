using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BlockCatalogPlugin.UI
{
    /// <summary>
    /// 批量属性修改配置对话框（完整版）
    /// 支持多属性同时修改、条件修改、复制属性、模板保存等功能
    /// </summary>
    public class BatchModifyDialog : Form
    {
        // 左侧：属性列表区
        private CheckedListBox chkAttributeList;
        private Button btnSelectAll;
        private Button btnSelectNone;
        private Button btnAddCustom;
        private Label lblSelectedCount;

        // 右侧：规则配置区
        private Label lblTargetAttribute;
        private ComboBox cmbRuleType;
        private Panel pnlRuleConfig;

        // 递增/递减参数
        private Label lblStart;
        private NumericUpDown numStart;
        private Label lblStep;
        private NumericUpDown numStep;
        private Label lblFormat;
        private ComboBox cmbFormat;
        private TextBox txtCustomExpr;  // 自定义表达式
        private Button btnExprHelp;     // 表达式帮助
        private Label lblPrefix;
        private TextBox txtPrefix;
        private Label lblSuffix;
        private TextBox txtSuffix;

        // 替换参数
        private Label lblReplaceValue;
        private TextBox txtReplaceValue;
        private Label lblReplaceExpr;
        private TextBox txtReplaceExpr;

        // 数值运算参数
        private Label lblOperand;
        private NumericUpDown numOperand;

        // 正则参数
        private Label lblRegexPattern;
        private TextBox txtRegexPattern;
        private Label lblRegexReplacement;
        private TextBox txtRegexReplacement;

        // 复制参数
        private Label lblCopySource;
        private ComboBox cmbCopySource;
        private Label lblCopyMode;
        private ComboBox cmbCopyMode;
        private Label lblCopyFormat;
        private TextBox txtCopyFormat;

        // 条件参数
        private Label lblConditionType;
        private ComboBox cmbConditionType;
        private Label lblConditionAttribute;
        private ComboBox cmbConditionAttribute;
        private Label lblConditionValue;
        private TextBox txtConditionValue;
        private CheckBox chkExcludeMode;

        // 数值范围参数
        private Label lblRangeMin;
        private NumericUpDown numRangeMin;
        private Label lblRangeMax;
        private NumericUpDown numRangeMax;

        // 分组参数
        private Label lblGroupBy;
        private ComboBox cmbGroupBy;
        private CheckBox chkResetPerGroup;

        // 类型缩写
        private Label lblAbbr;
        private TextBox txtTypeAbbreviations;

        // 同步复制目标
        private Label lblSync;
        private CheckedListBox chkSyncTargets;
        private Label lblSyncFormat;
        private TextBox txtSyncFormat;

        // 模板选择
        private ComboBox cmbTemplate;
        private Button btnLoadTemplate;
        private Button btnSaveTemplate;
        private Button btnDeleteTemplate;

        // 预览区
        private ListBox lstPreview;

        // 底部按钮
        private Button btnPreview;
        private Button btnApply;
        private Button btnCancel;
        private Label lblStatus;

        // 数据
        private List<string> _availableTags;
        private List<AttributeBlockData> _blocks;
        private BatchModifyConfig _config;
        private Dictionary<string, AttributeRuleConfig> _ruleConfigs;
        private StyleTemplateManager _templateManager;

        public BatchModifyConfig ResultConfig { get; private set; }

        // 深色主题颜色（使用公共配置）
        private static class Theme
        {
            public static Color Bg => ThemeConfig.Bg;
            public static Color Card => ThemeConfig.Card;
            public static Color InputBg => ThemeConfig.InputBg;
            public static Color Text => ThemeConfig.Text;
            public static Color TextDim => ThemeConfig.TextDim;
            public static Color Accent => ThemeConfig.Accent;
            public static Color Success => ThemeConfig.Success;
            public static Color Warning => ThemeConfig.Warning;
            public static Color Error => ThemeConfig.Error;
            public static Color Border => ThemeConfig.Border;
        }

        public BatchModifyDialog(List<string> availableTags, List<AttributeBlockData> blocks)
        {
            _availableTags = availableTags ?? new List<string>();
            _blocks = blocks ?? new List<AttributeBlockData>();
            _config = new BatchModifyConfig();
            _ruleConfigs = new Dictionary<string, AttributeRuleConfig>();
            _templateManager = StyleTemplateManager.Instance;

            // 初始化每个属性的默认配置
            foreach (var tag in _availableTags)
            {
                _ruleConfigs[tag] = new AttributeRuleConfig
                {
                    TargetTag = tag,
                    RuleType = RuleType.Increment,
                    Format = "{nn}"
                };
            }

            InitializeComponents();
            LoadTemplatesToComboBox();
        }

        private void InitializeComponents()
        {
            // DPI 缩放设置 - WinForms 会自动处理控件缩放
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F); // 标准 96 DPI 基准

            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(950, 700);
            Text = "批量属性修改配置";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(800, 600);

            int leftWidth = 280;
            int rightStartX = leftWidth + 20;
            int rightWidth = ClientSize.Width - rightStartX - 20;
            int topHeight = 450;

            // ===== 左侧：属性列表区 =====
            var pnlLeft = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(leftWidth, topHeight),
                BackColor = Theme.Card,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };
            Controls.Add(pnlLeft);

            int ly = 10;

            // 标题
            var lblTitle = new Label
            {
                Text = "属性列表（勾选要修改的）",
                Location = new Point(10, ly),
                AutoSize = true,
                ForeColor = Theme.Text,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            pnlLeft.Controls.Add(lblTitle);
            ly += 25;

            // 属性列表
            chkAttributeList = new CheckedListBox
            {
                Location = new Point(10, ly),
                Size = new Size(leftWidth - 20, 200),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            foreach (var tag in _availableTags)
            {
                chkAttributeList.Items.Add($"{tag} [{GetRuleTypeName(_ruleConfigs[tag].RuleType)}]", false);
            }
            chkAttributeList.SelectedIndexChanged += ChkAttributeList_SelectedIndexChanged;
            chkAttributeList.ItemCheck += ChkAttributeList_ItemCheck;
            pnlLeft.Controls.Add(chkAttributeList);
            ly += 210;

            // 按钮
            btnSelectAll = CreateButton("全选", 10, ly, 70);
            btnSelectAll.Click += (s, e) => { for (int i = 0; i < chkAttributeList.Items.Count; i++) chkAttributeList.SetItemChecked(i, true); UpdateSelectedCount(); };
            pnlLeft.Controls.Add(btnSelectAll);

            btnSelectNone = CreateButton("全不选", 85, ly, 70);
            btnSelectNone.Click += (s, e) => { for (int i = 0; i < chkAttributeList.Items.Count; i++) chkAttributeList.SetItemChecked(i, false); UpdateSelectedCount(); };
            pnlLeft.Controls.Add(btnSelectNone);

            btnAddCustom = CreateButton("添加自定义", 160, ly, 100);
            btnAddCustom.Click += BtnAddCustom_Click;
            pnlLeft.Controls.Add(btnAddCustom);
            ly += 30;

            // 已选择计数
            lblSelectedCount = new Label
            {
                Text = "已选择: 0个属性",
                Location = new Point(10, ly),
                AutoSize = true,
                ForeColor = Theme.Accent
            };
            pnlLeft.Controls.Add(lblSelectedCount);
            ly += 25;

            // ===== 模板区 =====
            var lblTemplateTitle = new Label
            {
                Text = "模板管理",
                Location = new Point(10, ly),
                AutoSize = true,
                ForeColor = Theme.Text,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            pnlLeft.Controls.Add(lblTemplateTitle);
            ly += 22;

            cmbTemplate = new ComboBox
            {
                Location = new Point(10, ly),
                Width = leftWidth - 90,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTemplate.Items.Add("(选择模板)");
            cmbTemplate.SelectedIndex = 0;
            pnlLeft.Controls.Add(cmbTemplate);

            btnLoadTemplate = CreateButton("加载", leftWidth - 80, ly, 60, Theme.Accent);
            btnLoadTemplate.Click += BtnLoadTemplate_Click;
            pnlLeft.Controls.Add(btnLoadTemplate);
            ly += 28;

            btnSaveTemplate = CreateButton("保存模板", 10, ly, 90, Theme.Success);
            btnSaveTemplate.Click += BtnSaveTemplate_Click;
            pnlLeft.Controls.Add(btnSaveTemplate);

            btnDeleteTemplate = CreateButton("删除", 105, ly, 60, Theme.Error);
            btnDeleteTemplate.Click += BtnDeleteTemplate_Click;
            pnlLeft.Controls.Add(btnDeleteTemplate);

            // ===== 右侧：规则配置区 =====
            var pnlRight = new Panel
            {
                Location = new Point(rightStartX, 10),
                Size = new Size(rightWidth, topHeight),
                BackColor = Theme.Card,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(pnlRight);

            int ry = 10;

            // 目标属性
            lblTargetAttribute = new Label
            {
                Text = "目标属性: (请选择属性)",
                Location = new Point(10, ry),
                AutoSize = true,
                ForeColor = Theme.Accent,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            pnlRight.Controls.Add(lblTargetAttribute);
            ry += 25;

            // 规则类型
            var lblRuleTypeTitle = new Label { Text = "规则类型:", Location = new Point(10, ry), AutoSize = true, ForeColor = Theme.Text };
            pnlRight.Controls.Add(lblRuleTypeTitle);

            cmbRuleType = new ComboBox
            {
                Location = new Point(100, ry),
                Width = 180,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbRuleType.Items.AddRange(new object[] {
                "递增序号", "递减序号", "替换为固定值", "替换为表达式",
                "追加前缀", "追加后缀", "数值加法", "数值减法",
                "数值乘法", "数值除法", "正则替换", "清空属性", "复制自其他属性"
            });
            cmbRuleType.SelectedIndex = 0;
            cmbRuleType.SelectedIndexChanged += CmbRuleType_SelectedIndexChanged;
            pnlRight.Controls.Add(cmbRuleType);
            ry += 30;

            // 规则配置面板（动态显示）
            pnlRuleConfig = new Panel
            {
                Location = new Point(10, ry),
                Size = new Size(rightWidth - 20, 360),
                BackColor = Theme.Card,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            pnlRight.Controls.Add(pnlRuleConfig);

            // 创建所有规则配置控件
            CreateAllRuleConfigControls();

            // ===== 预览区 =====
            var pnlPreview = new Panel
            {
                Location = new Point(10, topHeight + 20),
                Size = new Size(ClientSize.Width - 20, 150),
                BackColor = Theme.Card,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(pnlPreview);

            var lblPreviewTitle = new Label
            {
                Text = "修改预览（显示前50条）",
                Location = new Point(10, 5),
                AutoSize = true,
                ForeColor = Theme.Text,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            pnlPreview.Controls.Add(lblPreviewTitle);

            lstPreview = new ListBox
            {
                Location = new Point(10, 25),
                Size = new Size(ClientSize.Width - 40, 115),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlPreview.Controls.Add(lstPreview);

            // ===== 底部按钮 =====
            int by = ClientSize.Height - 45;

            btnPreview = CreateButton("预览全部修改", 10, by, 130, Theme.Warning);
            btnPreview.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnPreview.Click += BtnPreview_Click;
            Controls.Add(btnPreview);

            btnApply = CreateButton("应用修改", 150, by, 110, Theme.Success);
            btnApply.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnApply.Click += BtnApply_Click;
            Controls.Add(btnApply);

            btnCancel = CreateButton("取消", 270, by, 80);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            lblStatus = new Label
            {
                Text = "状态: 就绪",
                Location = new Point(360, by + 5),
                AutoSize = true,
                ForeColor = Theme.Text,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            Controls.Add(lblStatus);

            AcceptButton = btnApply;
            CancelButton = btnCancel;

            // 初始显示
            UpdateRuleConfigVisibility();
        }

        private void CreateAllRuleConfigControls()
        {
            pnlRuleConfig.Controls.Clear();
            int y = 5;

            // === 递增/递减参数 ===
            lblStart = new Label { Text = "起始值:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblStart);

            numStart = new NumericUpDown
            {
                Location = new Point(80, y),
                Width = 70,
                Minimum = 0,
                Maximum = 99999,
                Value = 1,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Visible = true
            };
            numStart.ValueChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(numStart);

            lblStep = new Label { Text = "步长:", Location = new Point(160, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblStep);

            numStep = new NumericUpDown
            {
                Location = new Point(210, y),
                Width = 70,
                Minimum = 1,
                Maximum = 100,
                Value = 1,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Visible = true
            };
            numStep.ValueChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(numStep);
            y += 28;

            lblFormat = new Label { Text = "格式模板:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblFormat);

            cmbFormat = new ComboBox
            {
                Location = new Point(80, y),
                Width = 130,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = true
            };
            cmbFormat.Items.AddRange(new object[] { "{n}", "{nn}", "{nnn}", "{nnnn}", "{A}", "{a}", "{cc}", "{c1}", "图{n}", "自定义..." });
            cmbFormat.SelectedIndex = 1;
            cmbFormat.SelectedIndexChanged += CmbFormat_SelectedIndexChanged;
            pnlRuleConfig.Controls.Add(cmbFormat);

            // 自定义表达式输入框
            txtCustomExpr = new TextBox
            {
                Location = new Point(220, y),
                Width = 120,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Text = "{前缀}-{nn}"
            };
            txtCustomExpr.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtCustomExpr);

            // 表达式帮助按钮
            btnExprHelp = new Button
            {
                Text = "?",
                Location = new Point(350, y - 2),
                Size = new Size(24, 22),
                BackColor = Theme.Card,
                ForeColor = Theme.TextDim,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Help,
                Visible = false
            };
            btnExprHelp.FlatAppearance.BorderSize = 0;
            btnExprHelp.Click += BtnExprHelp_Click;
            pnlRuleConfig.Controls.Add(btnExprHelp);
            y += 28;

            lblPrefix = new Label { Text = "前缀:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblPrefix);

            txtPrefix = new TextBox
            {
                Location = new Point(80, y),
                Width = 120,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = true
            };
            txtPrefix.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtPrefix);

            lblSuffix = new Label { Text = "后缀:", Location = new Point(210, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblSuffix);

            txtSuffix = new TextBox
            {
                Location = new Point(270, y),
                Width = 120,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = true
            };
            txtSuffix.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtSuffix);
            y += 30;

            // === 替换参数 ===
            lblReplaceValue = new Label { Text = "替换值:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblReplaceValue);

            txtReplaceValue = new TextBox
            {
                Location = new Point(80, y),
                Width = 310,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            txtReplaceValue.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtReplaceValue);
            y += 28;

            lblReplaceExpr = new Label { Text = "表达式:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblReplaceExpr);

            txtReplaceExpr = new TextBox
            {
                Location = new Point(80, y),
                Width = 310,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            txtReplaceExpr.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtReplaceExpr);
            y += 28;

            // === 数值运算参数 ===
            lblOperand = new Label { Text = "运算值:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblOperand);

            numOperand = new NumericUpDown
            {
                Location = new Point(80, y),
                Width = 100,
                Minimum = -99999,
                Maximum = 99999,
                Value = 0,
                DecimalPlaces = 2,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Visible = false
            };
            numOperand.ValueChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(numOperand);
            y += 28;

            // === 正则参数 ===
            lblRegexPattern = new Label { Text = "正则表达式:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblRegexPattern);

            txtRegexPattern = new TextBox
            {
                Location = new Point(80, y),
                Width = 310,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            txtRegexPattern.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtRegexPattern);
            y += 28;

            lblRegexReplacement = new Label { Text = "替换为:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblRegexReplacement);

            txtRegexReplacement = new TextBox
            {
                Location = new Point(80, y),
                Width = 310,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            txtRegexReplacement.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtRegexReplacement);
            y += 28;

            // === 复制参数 ===
            lblCopySource = new Label { Text = "源属性:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblCopySource);

            cmbCopySource = new ComboBox
            {
                Location = new Point(80, y),
                Width = 150,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            foreach (var tag in _availableTags)
                cmbCopySource.Items.Add(tag);
            if (cmbCopySource.Items.Count > 0) cmbCopySource.SelectedIndex = 0;
            cmbCopySource.SelectedIndexChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(cmbCopySource);
            y += 28;

            lblCopyMode = new Label { Text = "复制方式:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblCopyMode);

            cmbCopyMode = new ComboBox
            {
                Location = new Point(80, y),
                Width = 150,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            cmbCopyMode.Items.AddRange(new object[] { "直接复制", "格式化复制", "表达式计算" });
            cmbCopyMode.SelectedIndex = 0;
            cmbCopyMode.SelectedIndexChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(cmbCopyMode);
            y += 28;

            lblCopyFormat = new Label { Text = "格式模板:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblCopyFormat);

            txtCopyFormat = new TextBox
            {
                Location = new Point(80, y),
                Width = 200,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "第{值}页",
                Visible = false
            };
            txtCopyFormat.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtCopyFormat);
            y += 35;

            // === 分组参数 ===
            var lblGroupTitle = new Label
            {
                Text = "分组选项（用于按图纸类型分组编号）",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            pnlRuleConfig.Controls.Add(lblGroupTitle);
            y += 20;

            lblGroupBy = new Label { Text = "分组属性:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblGroupBy);

            cmbGroupBy = new ComboBox
            {
                Location = new Point(80, y),
                Width = 150,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = true
            };
            cmbGroupBy.Items.Add("(不分组)");
            foreach (var tag in _availableTags)
                cmbGroupBy.Items.Add(tag);
            cmbGroupBy.SelectedIndex = 0;
            cmbGroupBy.SelectedIndexChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(cmbGroupBy);

            chkResetPerGroup = new CheckBox
            {
                Text = "每组重新计数",
                Location = new Point(240, y),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Visible = true
            };
            chkResetPerGroup.CheckedChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(chkResetPerGroup);
            y += 28;

            lblAbbr = new Label { Text = "类型缩写:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblAbbr);

            txtTypeAbbreviations = new TextBox
            {
                Location = new Point(80, y),
                Width = 310,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "平面图=PL,立面图=LM,剖面图=PM",
                Visible = true
            };
            txtTypeAbbreviations.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtTypeAbbreviations);
            y += 35;

            // === 条件参数 ===
            var lblConditionTitle = new Label
            {
                Text = "修改条件（只修改满足条件的块）",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            pnlRuleConfig.Controls.Add(lblConditionTitle);
            y += 20;

            lblConditionType = new Label { Text = "条件类型:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblConditionType);

            cmbConditionType = new ComboBox
            {
                Location = new Point(80, y),
                Width = 100,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = true
            };
            cmbConditionType.Items.AddRange(new object[] { "(无条件)", "包含", "等于", "不等于", "正则匹配", "数值范围", "布局名包含" });
            cmbConditionType.SelectedIndex = 0;
            cmbConditionType.SelectedIndexChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(cmbConditionType);

            lblConditionAttribute = new Label { Text = "属性:", Location = new Point(190, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblConditionAttribute);

            cmbConditionAttribute = new ComboBox
            {
                Location = new Point(230, y),
                Width = 100,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = true
            };
            foreach (var tag in _availableTags)
                cmbConditionAttribute.Items.Add(tag);
            if (cmbConditionAttribute.Items.Count > 0) cmbConditionAttribute.SelectedIndex = 0;
            cmbConditionAttribute.SelectedIndexChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(cmbConditionAttribute);
            y += 28;

            lblConditionValue = new Label { Text = "条件值:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblConditionValue);

            txtConditionValue = new TextBox
            {
                Location = new Point(80, y),
                Width = 200,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = true
            };
            txtConditionValue.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtConditionValue);

            // 数值范围控件
            lblRangeMin = new Label { Text = "最小值:", Location = new Point(80, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblRangeMin);

            numRangeMin = new NumericUpDown
            {
                Location = new Point(130, y),
                Width = 60,
                Minimum = -999999,
                Maximum = 999999,
                Value = 0,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Visible = false
            };
            numRangeMin.ValueChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(numRangeMin);

            lblRangeMax = new Label { Text = "最大值:", Location = new Point(200, y), AutoSize = true, ForeColor = Theme.Text, Visible = false };
            pnlRuleConfig.Controls.Add(lblRangeMax);

            numRangeMax = new NumericUpDown
            {
                Location = new Point(250, y),
                Width = 60,
                Minimum = -999999,
                Maximum = 999999,
                Value = 100,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Visible = false
            };
            numRangeMax.ValueChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(numRangeMax);

            chkExcludeMode = new CheckBox
            {
                Text = "排除模式",
                Location = new Point(290, y),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Visible = true
            };
            chkExcludeMode.CheckedChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(chkExcludeMode);
            y += 35;

            // === 同步复制目标 ===
            var lblSyncTitle = new Label
            {
                Text = "同步复制到其他属性（修改当前属性时同时写入）",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = Theme.TextDim,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            pnlRuleConfig.Controls.Add(lblSyncTitle);
            y += 20;

            lblSync = new Label { Text = "目标属性:", Location = new Point(10, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblSync);

            chkSyncTargets = new CheckedListBox
            {
                Location = new Point(80, y),
                Size = new Size(150, 80),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = true
            };
            foreach (var tag in _availableTags)
                chkSyncTargets.Items.Add(tag, false);
            chkSyncTargets.ItemCheck += (s, e) => { BeginInvoke(new Action(UpdateCurrentRuleConfig)); };
            pnlRuleConfig.Controls.Add(chkSyncTargets);

            lblSyncFormat = new Label { Text = "格式:", Location = new Point(240, y), AutoSize = true, ForeColor = Theme.Text, Visible = true };
            pnlRuleConfig.Controls.Add(lblSyncFormat);

            txtSyncFormat = new TextBox
            {
                Location = new Point(280, y),
                Width = 110,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "第{值}页",
                Visible = true
            };
            txtSyncFormat.TextChanged += (s, e) => UpdateCurrentRuleConfig();
            pnlRuleConfig.Controls.Add(txtSyncFormat);
        }

        private void CmbFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isCustom = cmbFormat.SelectedIndex == cmbFormat.Items.Count - 1; // "自定义..."
            txtCustomExpr.Visible = isCustom;
            btnExprHelp.Visible = isCustom;
            UpdateCurrentRuleConfig();
        }

        private void BtnExprHelp_Click(object sender, EventArgs e)
        {
            var helpText = @"表达式变量说明：
{n}     - 纯数字序号 (1, 2, 3...)
{nn}    - 带前导零序号 (01, 02, 03...)
{nnn}   - 三位序号 (001, 002...)

{值}    - 当前属性原值
{图名}  - 读取图名属性值
{布局}  - 布局名称
{块名}  - 块定义名称

{类型缩写(图名)} - 根据图名自动缩写
  平面图→PL, 立面图→LM, 剖面图→PM

示例表达式：
  {前缀}-{nn}-{类型缩写(图名)}
  结果: 建施-01-PL

  {布局}-{n}
  结果: Layout1-1";

            MessageBox.Show(helpText, "表达式编辑器帮助", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateRuleConfigVisibility()
        {
            RuleType type = GetRuleTypeFromIndex(cmbRuleType.SelectedIndex);

            // 递增/递减参数
            bool showIncrement = type == RuleType.Increment || type == RuleType.Decrement;
            lblStart.Visible = showIncrement;
            numStart.Visible = showIncrement;
            lblStep.Visible = showIncrement;
            numStep.Visible = showIncrement;
            lblFormat.Visible = showIncrement;
            cmbFormat.Visible = showIncrement;
            bool isCustomFormat = showIncrement && cmbFormat.SelectedIndex == cmbFormat.Items.Count - 1;
            txtCustomExpr.Visible = isCustomFormat;
            btnExprHelp.Visible = isCustomFormat;
            lblPrefix.Visible = showIncrement || type == RuleType.AppendPrefix;
            txtPrefix.Visible = showIncrement || type == RuleType.AppendPrefix;
            lblSuffix.Visible = showIncrement || type == RuleType.AppendSuffix;
            txtSuffix.Visible = showIncrement || type == RuleType.AppendSuffix;

            // 替换参数
            lblReplaceValue.Visible = type == RuleType.ReplaceFixed;
            txtReplaceValue.Visible = type == RuleType.ReplaceFixed;
            lblReplaceExpr.Visible = type == RuleType.ReplaceExpr;
            txtReplaceExpr.Visible = type == RuleType.ReplaceExpr;

            // 数值运算参数
            bool showNumeric = type == RuleType.NumericAdd || type == RuleType.NumericSub ||
                              type == RuleType.NumericMultiply || type == RuleType.NumericDivide;
            lblOperand.Visible = showNumeric;
            numOperand.Visible = showNumeric;

            // 正则参数
            lblRegexPattern.Visible = type == RuleType.RegexReplace;
            txtRegexPattern.Visible = type == RuleType.RegexReplace;
            lblRegexReplacement.Visible = type == RuleType.RegexReplace;
            txtRegexReplacement.Visible = type == RuleType.RegexReplace;

            // 复制参数
            bool showCopy = type == RuleType.CopyFrom;
            lblCopySource.Visible = showCopy;
            cmbCopySource.Visible = showCopy;
            lblCopyMode.Visible = showCopy;
            cmbCopyMode.Visible = showCopy;
            lblCopyFormat.Visible = showCopy;
            txtCopyFormat.Visible = showCopy;

            // 分组和同步复制始终显示（对所有规则类型都可用）
            lblGroupBy.Visible = true;
            cmbGroupBy.Visible = true;
            chkResetPerGroup.Visible = true;
            lblAbbr.Visible = true;
            txtTypeAbbreviations.Visible = true;
            lblConditionType.Visible = true;
            cmbConditionType.Visible = true;
            lblConditionAttribute.Visible = true;
            cmbConditionAttribute.Visible = true;

            // 条件值控件：普通条件显示文本框，Range显示数值范围
            bool isRangeCondition = cmbConditionType.SelectedIndex == 5;
            lblConditionValue.Visible = !isRangeCondition;
            txtConditionValue.Visible = !isRangeCondition;
            lblRangeMin.Visible = isRangeCondition;
            numRangeMin.Visible = isRangeCondition;
            lblRangeMax.Visible = isRangeCondition;
            numRangeMax.Visible = isRangeCondition;

            chkExcludeMode.Visible = true;
            lblSync.Visible = true;
            chkSyncTargets.Visible = true;
            lblSyncFormat.Visible = true;
            txtSyncFormat.Visible = true;
        }

        private Button CreateButton(string text, int x, int y, int width, Color? bgColor = null)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 26),
                BackColor = bgColor ?? Theme.InputBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private string GetRuleTypeName(RuleType type)
        {
            return type switch
            {
                RuleType.Increment => "递增",
                RuleType.Decrement => "递减",
                RuleType.ReplaceFixed => "替换",
                RuleType.ReplaceExpr => "表达式",
                RuleType.AppendPrefix => "前缀",
                RuleType.AppendSuffix => "后缀",
                RuleType.NumericAdd => "加法",
                RuleType.NumericSub => "减法",
                RuleType.NumericMultiply => "乘法",
                RuleType.NumericDivide => "除法",
                RuleType.RegexReplace => "正则",
                RuleType.Clear => "清空",
                RuleType.CopyFrom => "复制",
                _ => "未知"
            };
        }

        private RuleType GetRuleTypeFromIndex(int index)
        {
            return index switch
            {
                0 => RuleType.Increment,
                1 => RuleType.Decrement,
                2 => RuleType.ReplaceFixed,
                3 => RuleType.ReplaceExpr,
                4 => RuleType.AppendPrefix,
                5 => RuleType.AppendSuffix,
                6 => RuleType.NumericAdd,
                7 => RuleType.NumericSub,
                8 => RuleType.NumericMultiply,
                9 => RuleType.NumericDivide,
                10 => RuleType.RegexReplace,
                11 => RuleType.Clear,
                12 => RuleType.CopyFrom,
                _ => RuleType.Increment
            };
        }

        private void ChkAttributeList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (chkAttributeList.SelectedIndex >= 0)
            {
                string selectedTag = _availableTags[chkAttributeList.SelectedIndex];
                lblTargetAttribute.Text = $"目标属性: {selectedTag}";

                var config = _ruleConfigs[selectedTag];
                cmbRuleType.SelectedIndex = (int)config.RuleType;
                numStart.Value = config.StartNum;
                numStep.Value = Math.Abs(config.Step);
                // 处理格式：预设或自定义
                if (cmbFormat.Items.Contains(config.Format))
                    cmbFormat.SelectedItem = config.Format;
                else
                {
                    cmbFormat.SelectedIndex = cmbFormat.Items.Count - 1; // "自定义..."
                    txtCustomExpr.Text = config.Format;
                }
                txtPrefix.Text = config.Prefix;
                txtSuffix.Text = config.Suffix;
                numOperand.Value = (decimal)config.Operand;
                txtReplaceValue.Text = config.ReplaceValue;
                txtRegexPattern.Text = config.RegexPattern;
                txtRegexReplacement.Text = config.RegexReplacement;

                if (cmbGroupBy.Items.Contains(config.GroupByTag))
                    cmbGroupBy.SelectedItem = config.GroupByTag;
                else
                    cmbGroupBy.SelectedIndex = 0;

                chkResetPerGroup.Checked = config.ResetPerGroup;
                txtTypeAbbreviations.Text = FormatTypeAbbreviations(config.TypeAbbreviations);

                // 条件
                if (config.Condition != null)
                {
                    cmbConditionType.SelectedIndex = (int)config.Condition.Type;
                    if (cmbConditionAttribute.Items.Contains(config.Condition.TargetAttribute))
                        cmbConditionAttribute.SelectedItem = config.Condition.TargetAttribute;
                    txtConditionValue.Text = config.Condition.ConditionValue;
                    numRangeMin.Value = (decimal)config.Condition.RangeMin;
                    numRangeMax.Value = (decimal)config.Condition.RangeMax;
                    chkExcludeMode.Checked = config.Condition.ExcludeMode;
                }

                // 同步复制目标
                for (int i = 0; i < chkSyncTargets.Items.Count; i++)
                {
                    bool isTarget = config.SyncCopyTargets.Any(t => t.Attribute == _availableTags[i]);
                    chkSyncTargets.SetItemChecked(i, isTarget);
                }
                txtSyncFormat.Text = config.SyncCopyTargets.FirstOrDefault()?.FormatTemplate ?? "第{值}页";

                UpdateRuleConfigVisibility();
            }
        }

        private void ChkAttributeList_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke(new Action(UpdateSelectedCount));
        }

        private void UpdateSelectedCount()
        {
            int count = chkAttributeList.CheckedItems.Count;
            lblSelectedCount.Text = $"已选择: {count}个属性";
        }

        private void UpdateCurrentRuleConfig()
        {
            if (chkAttributeList.SelectedIndex < 0) return;

            string selectedTag = _availableTags[chkAttributeList.SelectedIndex];
            var config = _ruleConfigs[selectedTag];

            config.RuleType = GetRuleTypeFromIndex(cmbRuleType.SelectedIndex);
            config.StartNum = (int)numStart.Value;
            config.Step = (int)numStep.Value;
            // 使用自定义表达式或预设格式
            if (cmbFormat.SelectedIndex == cmbFormat.Items.Count - 1 && txtCustomExpr.Visible)
                config.Format = txtCustomExpr.Text;
            else
                config.Format = cmbFormat.SelectedItem?.ToString() ?? "{nn}";
            config.Prefix = txtPrefix.Text;
            config.Suffix = txtSuffix.Text;
            config.Operand = (double)numOperand.Value;
            config.ReplaceValue = txtReplaceValue.Text;
            config.RegexPattern = txtRegexPattern.Text;
            config.RegexReplacement = txtRegexReplacement.Text;
            config.GroupByTag = cmbGroupBy.SelectedIndex > 0 ? cmbGroupBy.SelectedItem.ToString() : "";
            config.ResetPerGroup = chkResetPerGroup.Checked;
            config.TypeAbbreviations = ParseTypeAbbreviations(txtTypeAbbreviations.Text);

            // 条件
            config.Condition = new ConditionConfig
            {
                Type = GetConditionTypeFromIndex(cmbConditionType.SelectedIndex),
                TargetAttribute = cmbConditionAttribute.SelectedItem?.ToString() ?? "",
                ConditionValue = txtConditionValue.Text,
                RangeMin = (double)numRangeMin.Value,
                RangeMax = (double)numRangeMax.Value,
                ExcludeMode = chkExcludeMode.Checked
            };

            // 同步复制目标
            config.SyncCopyTargets = GetSyncCopyTargets();

            // 更新列表显示
            chkAttributeList.Items[chkAttributeList.SelectedIndex] =
                $"{selectedTag} [{GetRuleTypeName(config.RuleType)}]";
        }

        private void CmbRuleType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRuleConfigVisibility();
            UpdateCurrentRuleConfig();
        }

        private ConditionType GetConditionTypeFromIndex(int index)
        {
            return index switch
            {
                0 => ConditionType.None,
                1 => ConditionType.Contains,
                2 => ConditionType.Equals,
                3 => ConditionType.NotEquals,
                4 => ConditionType.Regex,
                5 => ConditionType.Range,
                6 => ConditionType.LayoutContains,
                _ => ConditionType.None
            };
        }

        private Dictionary<string, string> ParseTypeAbbreviations(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;

            var parts = text.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                {
                    result[kv[0].Trim()] = kv[1].Trim();
                }
            }
            return result;
        }

        private string FormatTypeAbbreviations(Dictionary<string, string> abbrs)
        {
            if (abbrs == null || abbrs.Count == 0) return "";
            return abbrs.Select(kv => $"{kv.Key}={kv.Value}").Aggregate((a, b) => $"{a},{b}");
        }

        private List<CopyTarget> GetSyncCopyTargets()
        {
            var targets = new List<CopyTarget>();
            foreach (int index in chkSyncTargets.CheckedIndices)
            {
                targets.Add(new CopyTarget
                {
                    Attribute = _availableTags[index],
                    FormatTemplate = txtSyncFormat.Text
                });
            }
            return targets;
        }

        private void BtnAddCustom_Click(object sender, EventArgs e)
        {
            var inputDialog = new AddColumnDialog();
            if (inputDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(inputDialog.ColumnName))
            {
                string customTag = inputDialog.ColumnName.ToUpperInvariant();
                if (!_availableTags.Contains(customTag))
                {
                    _availableTags.Add(customTag);
                    _ruleConfigs[customTag] = new AttributeRuleConfig { TargetTag = customTag };
                    chkAttributeList.Items.Add($"{customTag} [递增]", false);

                    // 同时更新其他下拉框
                    cmbConditionAttribute.Items.Add(customTag);
                    cmbGroupBy.Items.Add(customTag);
                    cmbCopySource.Items.Add(customTag);
                    chkSyncTargets.Items.Add(customTag, false);
                }
            }
        }

        private void LoadTemplatesToComboBox()
        {
            var templates = _templateManager.GetTemplateNames();
            cmbTemplate.Items.Clear();
            cmbTemplate.Items.Add("(选择模板)");
            foreach (var name in templates)
            {
                cmbTemplate.Items.Add(name);
            }
            cmbTemplate.SelectedIndex = 0;
        }

        private void BtnLoadTemplate_Click(object sender, EventArgs e)
        {
            if (cmbTemplate.SelectedIndex <= 0)
            {
                MessageBox.Show("请先选择一个模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string templateName = cmbTemplate.SelectedItem.ToString();
            var templateConfig = _templateManager.LoadTemplate(templateName);

            if (templateConfig == null)
            {
                MessageBox.Show($"加载模板 '{templateName}' 失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 应用模板配置（Rules 属性已移除，跳过）
            // TODO: 重新设计模板加载逻辑

            lblStatus.Text = $"状态: 已加载模板 '{templateName}'（Rules 已移除，功能待重新设计）";
            MessageBox.Show($"模板 '{templateName}' 已加载（模板加载功能正在重新设计中）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSaveTemplate_Click(object sender, EventArgs e)
        {
            MessageBox.Show("保存模板功能正在重新设计中", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDeleteTemplate_Click(object sender, EventArgs e)
        {
            if (cmbTemplate.SelectedIndex <= 0)
            {
                MessageBox.Show("请先选择一个模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string templateName = cmbTemplate.SelectedItem.ToString();
            if (MessageBox.Show($"确定删除模板 '{templateName}'？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            if (_templateManager.DeleteTemplate(templateName))
            {
                LoadTemplatesToComboBox();
                lblStatus.Text = $"状态: 已删除模板 '{templateName}'";
            }
            else
            {
                MessageBox.Show($"删除模板 '{templateName}' 失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "状态: 正在预览...";
                lstPreview.Items.Clear();

                var previewConfig = BuildConfigFromUI();
                var modifier = new BatchAttributeModifier();
                var previews = modifier.Preview(_blocks, previewConfig);

                foreach (var item in previews.Take(50))
                {
                    string changeMark = item.HasChanged ? " ✓" : "";
                    string groupInfo = !string.IsNullOrEmpty(item.GroupValue) ? $" [{item.GroupValue}]" : "";
                    lstPreview.Items.Add($"[{item.BlockIndex + 1}] {item.TagName}{groupInfo}: \"{item.OriginalValue}\" → \"{item.NewValue}\"{changeMark}");
                }

                int changedCount = previews.Count(p => p.HasChanged);
                lblStatus.Text = $"状态: 预览完成，共 {previews.Count} 条，其中 {changedCount} 条有变化";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"状态: 预览出错 - {ex.Message}";
                MessageBox.Show($"预览失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "状态: 正在应用修改...";

                var applyConfig = BuildConfigFromUI();
                var modifier = new BatchAttributeModifier();
                var previews = modifier.Execute(_blocks, applyConfig);

                var attrModifier = new AttributeModifier();
                int successCount = attrModifier.WriteBackToCad(_blocks);

                lblStatus.Text = $"状态: 已修改 {successCount} 个属性块";

                ResultConfig = applyConfig;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"状态: 应用出错 - {ex.Message}";
                MessageBox.Show($"应用修改失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private BatchModifyConfig BuildConfigFromUI()
        {
            var config = new BatchModifyConfig();

            for (int i = 0; i < chkAttributeList.Items.Count; i++)
            {
                if (chkAttributeList.GetItemChecked(i))
                {
                    string tag = _availableTags[i];
                    var ruleConfig = _ruleConfigs[tag];

                    // 确保所有参数已更新
                    ruleConfig.StartNum = (int)numStart.Value;
                    ruleConfig.Step = (int)numStep.Value;
                    ruleConfig.Format = cmbFormat.SelectedItem?.ToString() ?? "{nn}";
                    ruleConfig.Prefix = txtPrefix.Text;
                    ruleConfig.Suffix = txtSuffix.Text;
                    ruleConfig.Operand = (double)numOperand.Value;
                    ruleConfig.ReplaceValue = txtReplaceValue.Text;
                    ruleConfig.RegexPattern = txtRegexPattern.Text;
                    ruleConfig.RegexReplacement = txtRegexReplacement.Text;
                    ruleConfig.GroupByTag = cmbGroupBy.SelectedIndex > 0 ? cmbGroupBy.SelectedItem.ToString() : "";
                    ruleConfig.ResetPerGroup = chkResetPerGroup.Checked;
                    ruleConfig.Enabled = true;

                    ruleConfig.TypeAbbreviations = ParseTypeAbbreviations(txtTypeAbbreviations.Text);

                    if (cmbConditionType.SelectedIndex > 0)
                    {
                        ruleConfig.Condition = new ConditionConfig
                        {
                            Type = GetConditionTypeFromIndex(cmbConditionType.SelectedIndex),
                            TargetAttribute = cmbConditionAttribute.SelectedItem?.ToString() ?? "",
                            ConditionValue = txtConditionValue.Text,
                            ExcludeMode = chkExcludeMode.Checked
                        };
                    }

                    ruleConfig.SyncCopyTargets = GetSyncCopyTargets();

                    config.AddRule(ruleConfig);
                }
            }

            return config;
        }
    }

    /// <summary>
    /// 保存模板名称输入对话框
    /// </summary>
    public class SaveTemplateDialog : Form
    {
        private TextBox txtTemplateName;
        private Button btnOK;
        private Button btnCancel;

        public string TemplateName => txtTemplateName.Text;

        private static class Theme
        {
            public static Color Bg => ThemeConfig.Bg;
            public static Color InputBg => ThemeConfig.InputBg;
            public static Color Text => ThemeConfig.Text;
            public static Color Success => ThemeConfig.Success;
        }

        public SaveTemplateDialog()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(300, 100);
            Text = "保存模板";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;

            var lbl = new Label
            {
                Text = "模板名称:",
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            Controls.Add(lbl);

            txtTemplateName = new TextBox
            {
                Location = new Point(10, 35),
                Width = 280,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(txtTemplateName);

            btnOK = new Button
            {
                Text = "确定",
                Location = new Point(100, 65),
                Width = 80,
                Height = 25,
                BackColor = Theme.Success,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnOK);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(190, 65),
                Width = 80,
                Height = 25,
                BackColor = Theme.InputBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }
    }
}