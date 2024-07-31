namespace PipeInfo
{
    partial class WinForm_STEP
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WinForm_STEP));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.textBox_DBPath = new System.Windows.Forms.TextBox();
            this.button_DBpath = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.button_db_pathOk = new System.Windows.Forms.Button();
            this.groupBox_GroupList = new System.Windows.Forms.GroupBox();
            this.dataGridView_GroupList = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.GroupName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Export = new System.Windows.Forms.DataGridViewButtonColumn();
            this.dataGridView_PipesList = new System.Windows.Forms.DataGridView();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox_PipeList = new System.Windows.Forms.GroupBox();
            this.groupBox_PipeLengthInfo = new System.Windows.Forms.GroupBox();
            this.button_Set_SpoolNumber = new System.Windows.Forms.Button();
            this.label_PipeCount = new System.Windows.Forms.Label();
            this.label_PipesLength = new System.Windows.Forms.Label();
            this.label_PipeTHK = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.toolTip2 = new System.Windows.Forms.ToolTip(this.components);
            this.groupBox1.SuspendLayout();
            this.groupBox_GroupList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_GroupList)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_PipesList)).BeginInit();
            this.groupBox_PipeList.SuspendLayout();
            this.groupBox_PipeLengthInfo.SuspendLayout();
            this.SuspendLayout();
            // 
            // textBox_DBPath
            // 
            this.textBox_DBPath.Location = new System.Drawing.Point(8, 23);
            this.textBox_DBPath.Name = "textBox_DBPath";
            this.textBox_DBPath.Size = new System.Drawing.Size(216, 21);
            this.textBox_DBPath.TabIndex = 0;
            this.textBox_DBPath.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // button_DBpath
            // 
            this.button_DBpath.Location = new System.Drawing.Point(230, 23);
            this.button_DBpath.Name = "button_DBpath";
            this.button_DBpath.Size = new System.Drawing.Size(80, 23);
            this.button_DBpath.TabIndex = 1;
            this.button_DBpath.Text = "...";
            this.toolTip1.SetToolTip(this.button_DBpath, resources.GetString("button_DBpath.ToolTip"));
            this.button_DBpath.UseVisualStyleBackColor = true;
            this.button_DBpath.Click += new System.EventHandler(this.button1_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button_db_pathOk);
            this.groupBox1.Controls.Add(this.button_DBpath);
            this.groupBox1.Controls.Add(this.textBox_DBPath);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(404, 57);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Database 파일 경로";
            // 
            // button_db_pathOk
            // 
            this.button_db_pathOk.Location = new System.Drawing.Point(316, 23);
            this.button_db_pathOk.Name = "button_db_pathOk";
            this.button_db_pathOk.Size = new System.Drawing.Size(80, 23);
            this.button_db_pathOk.TabIndex = 2;
            this.button_db_pathOk.Text = "확인";
            this.button_db_pathOk.UseVisualStyleBackColor = true;
            this.button_db_pathOk.Click += new System.EventHandler(this.button_db_pathOk_Click);
            // 
            // groupBox_GroupList
            // 
            this.groupBox_GroupList.Controls.Add(this.dataGridView_GroupList);
            this.groupBox_GroupList.Location = new System.Drawing.Point(12, 87);
            this.groupBox_GroupList.Name = "groupBox_GroupList";
            this.groupBox_GroupList.Size = new System.Drawing.Size(404, 151);
            this.groupBox_GroupList.TabIndex = 3;
            this.groupBox_GroupList.TabStop = false;
            this.groupBox_GroupList.Text = "설계 그룹 리스트";
            // 
            // dataGridView_GroupList
            // 
            this.dataGridView_GroupList.AllowUserToAddRows = false;
            this.dataGridView_GroupList.AllowUserToDeleteRows = false;
            this.dataGridView_GroupList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_GroupList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.GroupName,
            this.Export});
            this.dataGridView_GroupList.GridColor = System.Drawing.SystemColors.Control;
            this.dataGridView_GroupList.Location = new System.Drawing.Point(6, 20);
            this.dataGridView_GroupList.MultiSelect = false;
            this.dataGridView_GroupList.Name = "dataGridView_GroupList";
            this.dataGridView_GroupList.ReadOnly = true;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("굴림", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Info;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView_GroupList.RowHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView_GroupList.RowHeadersVisible = false;
            this.dataGridView_GroupList.RowTemplate.Height = 23;
            this.dataGridView_GroupList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView_GroupList.Size = new System.Drawing.Size(389, 120);
            this.dataGridView_GroupList.TabIndex = 0;
            this.toolTip1.SetToolTip(this.dataGridView_GroupList, resources.GetString("dataGridView_GroupList.ToolTip"));
            this.dataGridView_GroupList.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_GroupList_CellContentClick);
            // 
            // Column1
            // 
            this.Column1.FillWeight = 50F;
            this.Column1.Frozen = true;
            this.Column1.HeaderText = "선택";
            this.Column1.Name = "Column1";
            this.Column1.ReadOnly = true;
            this.Column1.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column1.Width = 50;
            // 
            // GroupName
            // 
            this.GroupName.Frozen = true;
            this.GroupName.HeaderText = "그룹이름";
            this.GroupName.Name = "GroupName";
            this.GroupName.ReadOnly = true;
            this.GroupName.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.GroupName.Width = 195;
            // 
            // Export
            // 
            this.Export.HeaderText = "내보내기";
            this.Export.Name = "Export";
            this.Export.ReadOnly = true;
            this.Export.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Export.Text = "내보내기";
            this.Export.ToolTipText = "STEP파일로 내보내기";
            this.Export.UseColumnTextForButtonValue = true;
            this.Export.Width = 140;
            // 
            // dataGridView_PipesList
            // 
            this.dataGridView_PipesList.AllowUserToAddRows = false;
            this.dataGridView_PipesList.AllowUserToDeleteRows = false;
            this.dataGridView_PipesList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView_PipesList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column2,
            this.Column5,
            this.Column6,
            this.Column3,
            this.Column4});
            this.dataGridView_PipesList.Cursor = System.Windows.Forms.Cursors.Default;
            this.dataGridView_PipesList.Location = new System.Drawing.Point(6, 19);
            this.dataGridView_PipesList.Name = "dataGridView_PipesList";
            this.dataGridView_PipesList.ReadOnly = true;
            this.dataGridView_PipesList.RowHeadersVisible = false;
            this.dataGridView_PipesList.RowTemplate.Height = 23;
            this.dataGridView_PipesList.Size = new System.Drawing.Size(389, 257);
            this.dataGridView_PipesList.TabIndex = 1;
            this.dataGridView_PipesList.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_PipesList_CellContentClick);
            // 
            // Column2
            // 
            this.Column2.FillWeight = 40F;
            this.Column2.HeaderText = "번호";
            this.Column2.Name = "Column2";
            this.Column2.ReadOnly = true;
            this.Column2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column2.Width = 40;
            // 
            // Column5
            // 
            this.Column5.HeaderText = "관경";
            this.Column5.Name = "Column5";
            this.Column5.ReadOnly = true;
            this.Column5.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column5.Width = 60;
            // 
            // Column6
            // 
            this.Column6.HeaderText = "재질";
            this.Column6.Name = "Column6";
            this.Column6.ReadOnly = true;
            this.Column6.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column6.Width = 120;
            // 
            // Column3
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.Column3.DefaultCellStyle = dataGridViewCellStyle2;
            this.Column3.HeaderText = "파이프 길이";
            this.Column3.Name = "Column3";
            this.Column3.ReadOnly = true;
            this.Column3.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // Column4
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.Column4.DefaultCellStyle = dataGridViewCellStyle3;
            this.Column4.FillWeight = 40F;
            this.Column4.HeaderText = "Hole";
            this.Column4.Name = "Column4";
            this.Column4.ReadOnly = true;
            this.Column4.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Column4.Width = 65;
            // 
            // groupBox_PipeList
            // 
            this.groupBox_PipeList.Controls.Add(this.dataGridView_PipesList);
            this.groupBox_PipeList.Location = new System.Drawing.Point(12, 248);
            this.groupBox_PipeList.Name = "groupBox_PipeList";
            this.groupBox_PipeList.Size = new System.Drawing.Size(404, 287);
            this.groupBox_PipeList.TabIndex = 4;
            this.groupBox_PipeList.TabStop = false;
            this.groupBox_PipeList.Text = "파이프 리스트";
            // 
            // groupBox_PipeLengthInfo
            // 
            this.groupBox_PipeLengthInfo.Controls.Add(this.button_Set_SpoolNumber);
            this.groupBox_PipeLengthInfo.Controls.Add(this.label_PipeCount);
            this.groupBox_PipeLengthInfo.Controls.Add(this.label_PipesLength);
            this.groupBox_PipeLengthInfo.Controls.Add(this.label_PipeTHK);
            this.groupBox_PipeLengthInfo.Controls.Add(this.label3);
            this.groupBox_PipeLengthInfo.Controls.Add(this.label2);
            this.groupBox_PipeLengthInfo.Controls.Add(this.label1);
            this.groupBox_PipeLengthInfo.Location = new System.Drawing.Point(12, 541);
            this.groupBox_PipeLengthInfo.Name = "groupBox_PipeLengthInfo";
            this.groupBox_PipeLengthInfo.Size = new System.Drawing.Size(404, 89);
            this.groupBox_PipeLengthInfo.TabIndex = 6;
            this.groupBox_PipeLengthInfo.TabStop = false;
            this.groupBox_PipeLengthInfo.Text = "Information";
            // 
            // button_Set_SpoolNumber
            // 
            this.button_Set_SpoolNumber.Enabled = false;
            this.button_Set_SpoolNumber.Location = new System.Drawing.Point(284, 20);
            this.button_Set_SpoolNumber.Name = "button_Set_SpoolNumber";
            this.button_Set_SpoolNumber.Size = new System.Drawing.Size(111, 51);
            this.button_Set_SpoolNumber.TabIndex = 3;
            this.button_Set_SpoolNumber.Text = "스풀번호반영";
            this.button_Set_SpoolNumber.UseVisualStyleBackColor = true;
            this.button_Set_SpoolNumber.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // label_PipeCount
            // 
            this.label_PipeCount.AutoSize = true;
            this.label_PipeCount.Location = new System.Drawing.Point(93, 59);
            this.label_PipeCount.Name = "label_PipeCount";
            this.label_PipeCount.Size = new System.Drawing.Size(11, 12);
            this.label_PipeCount.TabIndex = 5;
            this.label_PipeCount.Text = "0";
            // 
            // label_PipesLength
            // 
            this.label_PipesLength.AutoSize = true;
            this.label_PipesLength.Location = new System.Drawing.Point(138, 38);
            this.label_PipesLength.Name = "label_PipesLength";
            this.label_PipesLength.Size = new System.Drawing.Size(11, 12);
            this.label_PipesLength.TabIndex = 4;
            this.label_PipesLength.Text = "0";
            // 
            // label_PipeTHK
            // 
            this.label_PipeTHK.AutoSize = true;
            this.label_PipeTHK.Location = new System.Drawing.Point(93, 23);
            this.label_PipeTHK.Name = "label_PipeTHK";
            this.label_PipeTHK.Size = new System.Drawing.Size(35, 12);
            this.label_PipeTHK.TabIndex = 3;
            this.label_PipeTHK.Text = "None";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 59);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(85, 12);
            this.label3.TabIndex = 2;
            this.label3.Text = "단관 총 갯수 : ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(131, 12);
            this.label2.TabIndex = 1;
            this.label2.Text = "파이프(단관) 총 길이 : ";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 23);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(81, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "파이프 두께 : ";
            // 
            // toolTip1
            // 
            this.toolTip1.Popup += new System.Windows.Forms.PopupEventHandler(this.toolTip1_Popup);
            // 
            // WinForm_STEP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(419, 638);
            this.Controls.Add(this.groupBox_PipeLengthInfo);
            this.Controls.Add(this.groupBox_GroupList);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox_PipeList);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "WinForm_STEP";
            this.Text = "제작도면 : DDWorks To STEP";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.WinForm_STEP_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox_GroupList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_GroupList)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_PipesList)).EndInit();
            this.groupBox_PipeList.ResumeLayout(false);
            this.groupBox_PipeLengthInfo.ResumeLayout(false);
            this.groupBox_PipeLengthInfo.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox textBox_DBPath;
        private System.Windows.Forms.Button button_DBpath;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox_GroupList;
        private System.Windows.Forms.GroupBox groupBox_PipeList;
        private System.Windows.Forms.GroupBox groupBox_PipeLengthInfo;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label_PipesLength;
        private System.Windows.Forms.Label label_PipeTHK;
        private System.Windows.Forms.Label label_PipeCount;
        private System.Windows.Forms.Button button_db_pathOk;
        public System.Windows.Forms.DataGridView dataGridView_GroupList;
        public System.Windows.Forms.DataGridView dataGridView_PipesList;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn GroupName;
        private System.Windows.Forms.DataGridViewButtonColumn Export;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column5;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column6;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.Button button_Set_SpoolNumber;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ToolTip toolTip2;
    }
}