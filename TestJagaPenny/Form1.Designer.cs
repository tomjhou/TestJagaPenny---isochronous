namespace TestJagaPenny
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.buttonRead = new System.Windows.Forms.Button();
            this.buttonEnd = new System.Windows.Forms.Button();
            this.buttonBrowseLogs = new System.Windows.Forms.Button();
            this.buttonReadIso = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageText = new System.Windows.Forms.TabPage();
            this.tabPageGraphs = new System.Windows.Forms.TabPage();
            this.panelGraphs = new System.Windows.Forms.Panel();
            this.textBoxCount = new System.Windows.Forms.TextBox();
            this.buttonStopIso = new System.Windows.Forms.Button();
            this.buttonReadMono = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBoxShowTrailingZeros = new System.Windows.Forms.CheckBox();
            this.tabControl1.SuspendLayout();
            this.tabPageText.SuspendLayout();
            this.tabPageGraphs.SuspendLayout();
            this.SuspendLayout();
            // 
            // richTextBox1
            // 
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBox1.Location = new System.Drawing.Point(3, 3);
            this.richTextBox1.Margin = new System.Windows.Forms.Padding(2);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(1185, 536);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            // 
            // buttonRead
            // 
            this.buttonRead.Location = new System.Drawing.Point(778, 8);
            this.buttonRead.Margin = new System.Windows.Forms.Padding(2);
            this.buttonRead.Name = "buttonRead";
            this.buttonRead.Size = new System.Drawing.Size(74, 34);
            this.buttonRead.TabIndex = 1;
            this.buttonRead.Text = "Read old version";
            this.buttonRead.UseVisualStyleBackColor = true;
            this.buttonRead.Visible = false;
            this.buttonRead.Click += new System.EventHandler(this.buttonRead_Click);
            // 
            // buttonEnd
            // 
            this.buttonEnd.Location = new System.Drawing.Point(856, 8);
            this.buttonEnd.Margin = new System.Windows.Forms.Padding(2);
            this.buttonEnd.Name = "buttonEnd";
            this.buttonEnd.Size = new System.Drawing.Size(74, 34);
            this.buttonEnd.TabIndex = 2;
            this.buttonEnd.Text = "Stop old version";
            this.buttonEnd.UseVisualStyleBackColor = true;
            this.buttonEnd.Visible = false;
            this.buttonEnd.Click += new System.EventHandler(this.buttonEnd_Click);
            // 
            // buttonBrowseLogs
            // 
            this.buttonBrowseLogs.Location = new System.Drawing.Point(658, 8);
            this.buttonBrowseLogs.Margin = new System.Windows.Forms.Padding(2);
            this.buttonBrowseLogs.Name = "buttonBrowseLogs";
            this.buttonBrowseLogs.Size = new System.Drawing.Size(91, 34);
            this.buttonBrowseLogs.TabIndex = 3;
            this.buttonBrowseLogs.Text = "Browse log folder";
            this.buttonBrowseLogs.UseVisualStyleBackColor = true;
            this.buttonBrowseLogs.Visible = false;
            this.buttonBrowseLogs.Click += new System.EventHandler(this.buttonBrowseLogs_Click);
            // 
            // buttonReadIso
            // 
            this.buttonReadIso.Location = new System.Drawing.Point(11, 8);
            this.buttonReadIso.Margin = new System.Windows.Forms.Padding(2);
            this.buttonReadIso.Name = "buttonReadIso";
            this.buttonReadIso.Size = new System.Drawing.Size(74, 34);
            this.buttonReadIso.TabIndex = 4;
            this.buttonReadIso.Text = "Read Isochronous";
            this.buttonReadIso.UseVisualStyleBackColor = true;
            this.buttonReadIso.Click += new System.EventHandler(this.buttonReadIso_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPageText);
            this.tabControl1.Controls.Add(this.tabPageGraphs);
            this.tabControl1.Location = new System.Drawing.Point(2, 47);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1199, 568);
            this.tabControl1.TabIndex = 5;
            // 
            // tabPageText
            // 
            this.tabPageText.Controls.Add(this.richTextBox1);
            this.tabPageText.Location = new System.Drawing.Point(4, 22);
            this.tabPageText.Name = "tabPageText";
            this.tabPageText.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageText.Size = new System.Drawing.Size(1191, 542);
            this.tabPageText.TabIndex = 0;
            this.tabPageText.Text = "Text";
            this.tabPageText.UseVisualStyleBackColor = true;
            // 
            // tabPageGraphs
            // 
            this.tabPageGraphs.Controls.Add(this.panelGraphs);
            this.tabPageGraphs.Location = new System.Drawing.Point(4, 22);
            this.tabPageGraphs.Name = "tabPageGraphs";
            this.tabPageGraphs.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageGraphs.Size = new System.Drawing.Size(1191, 542);
            this.tabPageGraphs.TabIndex = 1;
            this.tabPageGraphs.Text = "Graphs";
            this.tabPageGraphs.UseVisualStyleBackColor = true;
            // 
            // panelGraphs
            // 
            this.panelGraphs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelGraphs.Location = new System.Drawing.Point(3, 3);
            this.panelGraphs.Name = "panelGraphs";
            this.panelGraphs.Size = new System.Drawing.Size(1185, 536);
            this.panelGraphs.TabIndex = 0;
            // 
            // textBoxCount
            // 
            this.textBoxCount.Location = new System.Drawing.Point(274, 16);
            this.textBoxCount.Name = "textBoxCount";
            this.textBoxCount.Size = new System.Drawing.Size(100, 20);
            this.textBoxCount.TabIndex = 6;
            // 
            // buttonStopIso
            // 
            this.buttonStopIso.Location = new System.Drawing.Point(89, 8);
            this.buttonStopIso.Margin = new System.Windows.Forms.Padding(2);
            this.buttonStopIso.Name = "buttonStopIso";
            this.buttonStopIso.Size = new System.Drawing.Size(74, 34);
            this.buttonStopIso.TabIndex = 7;
            this.buttonStopIso.Text = "Stop Isochronous";
            this.buttonStopIso.UseVisualStyleBackColor = true;
            this.buttonStopIso.Click += new System.EventHandler(this.buttonStopIso_Click);
            // 
            // buttonReadMono
            // 
            this.buttonReadMono.Location = new System.Drawing.Point(934, 8);
            this.buttonReadMono.Margin = new System.Windows.Forms.Padding(2);
            this.buttonReadMono.Name = "buttonReadMono";
            this.buttonReadMono.Size = new System.Drawing.Size(74, 34);
            this.buttonReadMono.TabIndex = 8;
            this.buttonReadMono.Text = "Read Mono";
            this.buttonReadMono.UseVisualStyleBackColor = true;
            this.buttonReadMono.Visible = false;
            this.buttonReadMono.Click += new System.EventHandler(this.buttonReadMono_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(170, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(99, 13);
            this.label1.TabIndex = 9;
            this.label1.Text = "Samples processed";
            // 
            // checkBoxShowTrailingZeros
            // 
            this.checkBoxShowTrailingZeros.AutoSize = true;
            this.checkBoxShowTrailingZeros.Checked = true;
            this.checkBoxShowTrailingZeros.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxShowTrailingZeros.Location = new System.Drawing.Point(394, 18);
            this.checkBoxShowTrailingZeros.Name = "checkBoxShowTrailingZeros";
            this.checkBoxShowTrailingZeros.Size = new System.Drawing.Size(172, 17);
            this.checkBoxShowTrailingZeros.TabIndex = 10;
            this.checkBoxShowTrailingZeros.Text = "Show zero padding in raw data";
            this.checkBoxShowTrailingZeros.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1202, 617);
            this.Controls.Add(this.checkBoxShowTrailingZeros);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonReadMono);
            this.Controls.Add(this.buttonStopIso);
            this.Controls.Add(this.textBoxCount);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.buttonReadIso);
            this.Controls.Add(this.buttonBrowseLogs);
            this.Controls.Add(this.buttonEnd);
            this.Controls.Add(this.buttonRead);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "Form1";
            this.Text = "Form1";
            this.tabControl1.ResumeLayout(false);
            this.tabPageText.ResumeLayout(false);
            this.tabPageGraphs.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button buttonRead;
        private System.Windows.Forms.Button buttonEnd;
        private System.Windows.Forms.Button buttonBrowseLogs;
        private System.Windows.Forms.Button buttonReadIso;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageText;
        private System.Windows.Forms.TabPage tabPageGraphs;
        private System.Windows.Forms.Panel panelGraphs;
        private System.Windows.Forms.TextBox textBoxCount;
        private System.Windows.Forms.Button buttonStopIso;
        private System.Windows.Forms.Button buttonReadMono;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBoxShowTrailingZeros;
    }
}

