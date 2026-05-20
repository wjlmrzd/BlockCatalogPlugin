using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BlockCatalogPlugin.UI
{
    /// <summary>
    /// 样式设置对话框
    /// 支持设置字体、行高、列宽，保存/加载样式模板
    /// </summary>
    public class StyleSettingsDialog : Form
    {
        private ComboBox cmbFontName;
        private NumericUpDown numFontHeight;
        private NumericUpDown numRowHeight;
        private NumericUpDown numHeaderHeight;
        private CheckBox chkDrawBorder;
        private DataGridView dgvColumns;
        private ComboBox cmbTemplate;
        private Button btnSaveTemplate;
        private Button btnLoadTemplate;
        private Button btnDeleteTemplate;
        private Button btnOK;
        private Button btnCancel;

        private CatalogStyle _style;
        private StyleTemplateManager _templateManager;
        private List<ColumnWidthConfig> _customWidths;

        public CatalogStyle ResultStyle { get; private set; }
        public List<ColumnWidthConfig> CustomWidths { get; private set; }

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
            public static Color Error => ThemeConfig.Error;
            public static Color Border => ThemeConfig.Border;
        }

        public StyleSettingsDialog(CatalogStyle style, List<ColumnWidthConfig> customWidths = null)
        {
            _style = style ?? new CatalogStyle();
            _customWidths = customWidths ?? new List<ColumnWidthConfig>();
            _templateManager = StyleTemplateManager.Instance;

            InitializeComponents();
            LoadTemplatesToComboBox();
            LoadCurrentStyle();
        }

        private void InitializeComponents()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F);

            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(450, 400);
            Text = "样式设置";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(400, 350);

            int y = 10;

            // === 模板选择 ===
            var lblTemplate = new Label
            {
                Text = "样式模板：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            Controls.Add(lblTemplate);

            cmbTemplate = new ComboBox
            {
                Location = new Point(90, y),
                Width = 200,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTemplate.Items.Add("(当前样式)");
            cmbTemplate.SelectedIndex = 0;
            Controls.Add(cmbTemplate);

            btnLoadTemplate = CreateButton("加载", 300, y, 60, Theme.Accent);
            btnLoadTemplate.Click += BtnLoadTemplate_Click;
            Controls.Add(btnLoadTemplate);

            y += 30;

            btnSaveTemplate = CreateButton("保存模板", 90, y, 80, Theme.Success);
            btnSaveTemplate.Click += BtnSaveTemplate_Click;
            Controls.Add(btnSaveTemplate);

            btnDeleteTemplate = CreateButton("删除", 175, y, 60, Theme.Error);
            btnDeleteTemplate.Click += BtnDeleteTemplate_Click;
            Controls.Add(btnDeleteTemplate);

            y += 40;

            // === 字体设置 ===
            var grpFont = new GroupBox
            {
                Text = "字体设置",
                Location = new Point(10, y),
                Size = new Size(430, 70),
                BackColor = Theme.Card,
                ForeColor = Theme.Text
            };
            Controls.Add(grpFont);

            var lblFontName = new Label
            {
                Text = "字体：",
                Location = new Point(10, 20),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            grpFont.Controls.Add(lblFontName);

            cmbFontName = new ComboBox
            {
                Location = new Point(50, 18),
                Width = 120,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // 添加常用字体
            cmbFontName.Items.AddRange(new object[] { "宋体", "黑体", "仿宋", "楷体", "Arial", "Times New Roman" });
            cmbFontName.SelectedIndex = 0;
            grpFont.Controls.Add(cmbFontName);

            var lblFontHeight = new Label
            {
                Text = "字高：",
                Location = new Point(180, 20),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            grpFont.Controls.Add(lblFontHeight);

            numFontHeight = new NumericUpDown
            {
                Location = new Point(220, 18),
                Width = 60,
                Minimum = 1m,
                Maximum = 20m,
                Value = 3.5m,
                DecimalPlaces = 1,
                Increment = 0.5m,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            grpFont.Controls.Add(numFontHeight);

            chkDrawBorder = new CheckBox
            {
                Text = "绘制边框",
                Location = new Point(300, 18),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Checked = true
            };
            grpFont.Controls.Add(chkDrawBorder);

            y += 80;

            // === 行高设置 ===
            var grpRow = new GroupBox
            {
                Text = "行高设置",
                Location = new Point(10, y),
                Size = new Size(430, 50),
                BackColor = Theme.Card,
                ForeColor = Theme.Text
            };
            Controls.Add(grpRow);

            var lblRowHeight = new Label
            {
                Text = "行高：",
                Location = new Point(10, 20),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            grpRow.Controls.Add(lblRowHeight);

            numRowHeight = new NumericUpDown
            {
                Location = new Point(50, 18),
                Width = 60,
                Minimum = 4m,
                Maximum = 30m,
                Value = 8m,
                DecimalPlaces = 1,
                Increment = 1m,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            grpRow.Controls.Add(numRowHeight);

            var lblHeaderHeight = new Label
            {
                Text = "表头高度：",
                Location = new Point(120, 20),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            grpRow.Controls.Add(lblHeaderHeight);

            numHeaderHeight = new NumericUpDown
            {
                Location = new Point(190, 18),
                Width = 60,
                Minimum = 6m,
                Maximum = 40m,
                Value = 10m,
                DecimalPlaces = 1,
                Increment = 1m,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text
            };
            grpRow.Controls.Add(numHeaderHeight);

            y += 60;

            // === 列宽设置 ===
            var lblColumns = new Label
            {
                Text = "列宽设置（可拖拽调整）：",
                Location = new Point(10, y),
                AutoSize = true,
                ForeColor = Theme.Text
            };
            Controls.Add(lblColumns);
            y += 22;

            dgvColumns = new DataGridView
            {
                Location = new Point(10, y),
                Size = new Size(430, 150),
                BackgroundColor = Theme.InputBg,
                ForeColor = Theme.Text,
                GridColor = Theme.Border,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                ReadOnly = false,
                Font = new Font("Microsoft YaHei UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Theme.InputBg,
                    ForeColor = Theme.Text
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Theme.Card,
                    ForeColor = Theme.TextBright,
                    Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
                }
            };

            dgvColumns.Columns.Add("Tag", "属性标签");
            dgvColumns.Columns.Add("Header", "列标题");
            dgvColumns.Columns.Add("Width", "宽度(mm)");
            dgvColumns.Columns["Tag"].Width = 100;
            dgvColumns.Columns["Header"].Width = 120;
            dgvColumns.Columns["Width"].Width = 80;

            Controls.Add(dgvColumns);
            y += 160;

            // === 底部按钮 ===
            btnOK = CreateButton("确定", ClientSize.Width - 180, y, 80, Theme.Success);
            btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOK.Click += BtnOK_Click;
            Controls.Add(btnOK);

            btnCancel = CreateButton("取消", ClientSize.Width - 90, y, 80, Theme.Card);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        private Button CreateButton(string text, int x, int y, int width, Color bgColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = width,
                Height = 26,
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void LoadTemplatesToComboBox()
        {
            var names = _templateManager.GetTemplateNames();
            foreach (var name in names)
            {
                cmbTemplate.Items.Add(name);
            }
        }

        private void LoadCurrentStyle()
        {
            cmbFontName.SelectedItem = _style.FontName ?? "宋体";
            numFontHeight.Value = (decimal)_style.FontHeight;
            numRowHeight.Value = (decimal)_style.RowHeight;
            numHeaderHeight.Value = (decimal)_style.HeaderHeight;
            chkDrawBorder.Checked = _style.DrawBorder;

            dgvColumns.Rows.Clear();
            foreach (var col in _style.Columns)
            {
                // 检查是否有自定义宽度
                double width = col.Width;
                var custom = _customWidths.FirstOrDefault(c => c.Tag == col.Tag);
                if (custom != null)
                    width = custom.Width;

                dgvColumns.Rows.Add(col.Tag, col.Header, width);
            }
        }

        private void BtnLoadTemplate_Click(object sender, EventArgs e)
        {
            if (cmbTemplate.SelectedIndex <= 0)
            {
                MessageBox.Show("请选择一个模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string templateName = cmbTemplate.SelectedItem.ToString();
            var template = _templateManager.LoadTemplate(templateName);
            if (template != null)
            {
                cmbFontName.SelectedItem = template.FontName ?? "宋体";
                numFontHeight.Value = (decimal)template.FontHeight;
                numRowHeight.Value = (decimal)template.RowHeight;
                numHeaderHeight.Value = (decimal)template.HeaderHeight;
                chkDrawBorder.Checked = template.DrawBorder;

                dgvColumns.Rows.Clear();
                foreach (var col in template.Columns)
                {
                    double width = col.Width;
                    var custom = template.CustomColumnWidths?.FirstOrDefault(c => c.Tag == col.Tag);
                    if (custom != null)
                        width = custom.Width;
                    dgvColumns.Rows.Add(col.Tag, col.Header, width);
                }

                MessageBox.Show($"已加载模板: {templateName}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnSaveTemplate_Click(object sender, EventArgs e)
        {
            string templateName = cmbTemplate.SelectedIndex > 0
                ? cmbTemplate.SelectedItem.ToString()
                : "";

            using (var dlg = new SaveStyleTemplateDialog(templateName))
            {
                if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(dlg.TemplateName))
                {
                    CollectCurrentStyle();
                    if (_templateManager.SaveTemplate(dlg.TemplateName, ResultStyle, CustomWidths))
                    {
                        MessageBox.Show($"模板已保存: {dlg.TemplateName}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // 刷新模板列表
                        cmbTemplate.Items.Clear();
                        cmbTemplate.Items.Add("(当前样式)");
                        foreach (var name in _templateManager.GetTemplateNames())
                            cmbTemplate.Items.Add(name);
                        cmbTemplate.SelectedItem = dlg.TemplateName;
                    }
                }
            }
        }

        private void BtnDeleteTemplate_Click(object sender, EventArgs e)
        {
            if (cmbTemplate.SelectedIndex <= 0)
            {
                MessageBox.Show("请选择一个模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string templateName = cmbTemplate.SelectedItem.ToString();
            if (MessageBox.Show($"确定删除模板 '{templateName}'?", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (_templateManager.DeleteTemplate(templateName))
                {
                    cmbTemplate.Items.RemoveAt(cmbTemplate.SelectedIndex);
                    cmbTemplate.SelectedIndex = 0;
                    MessageBox.Show("模板已删除", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            CollectCurrentStyle();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CollectCurrentStyle()
        {
            ResultStyle = new CatalogStyle
            {
                FontName = cmbFontName.SelectedItem?.ToString() ?? "宋体",
                FontHeight = (double)numFontHeight.Value,
                RowHeight = (double)numRowHeight.Value,
                HeaderHeight = (double)numHeaderHeight.Value,
                DrawBorder = chkDrawBorder.Checked
            };

            CustomWidths = new List<ColumnWidthConfig>();
            foreach (DataGridViewRow row in dgvColumns.Rows)
            {
                if (row.Cells["Tag"].Value != null)
                {
                    ResultStyle.Columns.Add(new ColumnDef
                    {
                        Tag = row.Cells["Tag"].Value.ToString(),
                        Header = row.Cells["Header"].Value?.ToString() ?? "",
                        Width = Convert.ToDouble(row.Cells["Width"].Value ?? 40),
                        Visible = true
                    });

                    CustomWidths.Add(new ColumnWidthConfig
                    {
                        Tag = row.Cells["Tag"].Value.ToString(),
                        Width = Convert.ToDouble(row.Cells["Width"].Value ?? 40)
                    });
                }
            }
        }
    }

    /// <summary>
    /// 保存样式模板对话框
    /// </summary>
    public class SaveStyleTemplateDialog : Form
    {
        private TextBox txtTemplateName;
        private Button btnOK;
        private Button btnCancel;

        public string TemplateName => txtTemplateName.Text.Trim();

        public SaveStyleTemplateDialog(string defaultName = "")
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F);

            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(300, 100);
            Text = "保存样式模板";
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = ThemeConfig.Bg;
            ForeColor = ThemeConfig.Text;
            Font = new Font("Microsoft YaHei UI", 9F);

            var lbl = new Label
            {
                Text = "模板名称：",
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = ThemeConfig.Text
            };
            Controls.Add(lbl);

            txtTemplateName = new TextBox
            {
                Location = new Point(10, 35),
                Width = 280,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text,
                Text = defaultName
            };
            Controls.Add(txtTemplateName);

            btnOK = new Button
            {
                Text = "保存",
                Location = new Point(100, 65),
                Width = 80,
                Height = 26,
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
                Location = new Point(190, 65),
                Width = 80,
                Height = 26,
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
            if (string.IsNullOrWhiteSpace(txtTemplateName.Text))
            {
                MessageBox.Show("请输入模板名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}