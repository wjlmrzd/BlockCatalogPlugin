using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockCatalogPlugin.UI
{
    public class EditColumnHeaderDialog : Form
    {
        private TextBox txtHeader;
        private Button btnOK;
        private Button btnCancel;

        public string HeaderName { get; private set; }

        public EditColumnHeaderDialog(string currentHeader)
        {
            InitializeComponents(currentHeader);
        }

        private void InitializeComponents(string currentHeader)
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F);

            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(280, 100);
            Text = "编辑列标题";
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = ThemeConfig.Bg;
            ForeColor = ThemeConfig.Text;
            Font = new Font("Microsoft YaHei UI", 9F);

            var lbl = new Label
            {
                Text = "列标题：",
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(lbl);

            txtHeader = new TextBox
            {
                Location = new Point(10, 35),
                Width = 260,
                Text = currentHeader,
                BackColor = ThemeConfig.InputBg,
                ForeColor = ThemeConfig.Text,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            Controls.Add(txtHeader);

            btnOK = new Button
            {
                Text = "确定",
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
            if (string.IsNullOrWhiteSpace(txtHeader.Text))
            {
                MessageBox.Show("请输入列标题", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            HeaderName = txtHeader.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
