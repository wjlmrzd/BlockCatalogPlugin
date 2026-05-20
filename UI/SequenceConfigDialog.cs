using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace BlockCatalogPlugin.UI
{
    public class SequenceConfigDialog : Form
    {
        private ComboBox cmbTargetTag;
        private ComboBox cmbTemplate;
        private NumericUpDown numStart;
        private NumericUpDown numStep;
        private TextBox txtPrefix;
        private TextBox txtSuffix;
        private ComboBox cmbConditionTag;
        private CheckBox chkResetPerGroup;
        private Button btnOK;
        private Button btnCancel;

        public AttributeModifier.SequenceConfig Config { get; private set; }

        public SequenceConfigDialog(List<string> availableTags)
        {
            InitializeComponents(availableTags);
        }

        private void InitializeComponents(List<string> availableTags)
        {
            // DPI 缩放设置
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F);

            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(320, 280);
            Text = "属性递增配置";
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = ThemeConfig.Bg;
            ForeColor = ThemeConfig.Text;
            Font = new Font("Microsoft YaHei UI", 9F);

            int y = 10;

            // 目标属性
            var lblTarget = new Label
            {
                Text = "目标属性：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblTarget);
            y += 22;

            cmbTargetTag = new ComboBox
            {
                Location = new Point(10, y),
                Width = 200,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            if (availableTags != null)
            {
                foreach (var tag in availableTags)
                    cmbTargetTag.Items.Add(tag);
                if (cmbTargetTag.Items.Count > 0)
                    cmbTargetTag.SelectedIndex = 0;
            }
            Controls.Add(cmbTargetTag);
            y += 28;

            // 序号模板
            var lblTemplate = new Label
            {
                Text = "序号模板：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblTemplate);
            y += 22;

            cmbTemplate = new ComboBox
            {
                Location = new Point(10, y),
                Width = 200,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTemplate.Items.AddRange(new[] { "{n}", "{nn}", "{nnn}", "{A}", "{a}", "{cc}", "{c1}", "图{n}" });
            cmbTemplate.SelectedIndex = 0;
            Controls.Add(cmbTemplate);
            y += 28;

            // 起始号
            var lblStart = new Label
            {
                Text = "起始号：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblStart);

            numStart = new NumericUpDown
            {
                Location = new Point(80, y),
                Width = 60,
                Minimum = 1,
                Maximum = 9999,
                Value = 1,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text
            };
            Controls.Add(numStart);

            // 步长
            var lblStep = new Label
            {
                Text = "步长：",
                Location = new Point(150, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblStep);

            numStep = new NumericUpDown
            {
                Location = new Point(200, y),
                Width = 60,
                Minimum = 1,
                Maximum = 100,
                Value = 1,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text
            };
            Controls.Add(numStep);
            y += 28;

            // 前缀
            var lblPrefix = new Label
            {
                Text = "前缀：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblPrefix);

            txtPrefix = new TextBox
            {
                Location = new Point(60, y),
                Width = 80,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text
            };
            Controls.Add(txtPrefix);

            // 后缀
            var lblSuffix = new Label
            {
                Text = "后缀：",
                Location = new Point(150, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblSuffix);

            txtSuffix = new TextBox
            {
                Location = new Point(200, y),
                Width = 80,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text
            };
            Controls.Add(txtSuffix);
            y += 28;

            // 分组条件
            var lblCondition = new Label
            {
                Text = "分组条件（可选）：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lblCondition);
            y += 22;

            cmbConditionTag = new ComboBox
            {
                Location = new Point(10, y),
                Width = 200,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbConditionTag.Items.Add("(不分组)");
            if (availableTags != null)
            {
                foreach (var tag in availableTags)
                    cmbConditionTag.Items.Add(tag);
            }
            cmbConditionTag.SelectedIndex = 0;
            Controls.Add(cmbConditionTag);
            y += 28;

            // 每组重新计数
            chkResetPerGroup = new CheckBox
            {
                Text = "每组重新计数",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                BackColor = ThemeConfig.Bg,
                Checked = false
            };
            Controls.Add(chkResetPerGroup);
            y += 35;

            // 按钮
            btnOK = new Button
            {
                Text = "应用",
                Location = new Point(100, y),
                Width = 90,
                Height = 28,
                BackColor = ThemeConfig.Success,
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
                BackColor = ThemeConfig.Card,
                ForeColor = ThemeConfig.Text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (cmbTargetTag.SelectedIndex < 0)
            {
                MessageBox.Show("请选择目标属性", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string conditionTag = cmbConditionTag.SelectedIndex > 0
                ? cmbConditionTag.SelectedItem.ToString()
                : "";

            Config = new AttributeModifier.SequenceConfig
            {
                TargetTag = cmbTargetTag.SelectedItem?.ToString() ?? "",
                Template = cmbTemplate.SelectedItem?.ToString() ?? "{n}",
                StartNum = (int)numStart.Value,
                Step = (int)numStep.Value,
                Prefix = txtPrefix.Text,
                Suffix = txtSuffix.Text,
                ConditionTag = conditionTag,
                ResetPerGroup = chkResetPerGroup.Checked && !string.IsNullOrEmpty(conditionTag)
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}