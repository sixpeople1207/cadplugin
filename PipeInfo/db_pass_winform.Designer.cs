namespace PipeInfo
{
    partial class db_pass_winform
    {
        /// <summary> 
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 구성 요소 디자이너에서 생성한 코드

        /// <summary> 
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.db_path = new System.Windows.Forms.Button();
            this.db_path_groupbox = new System.Windows.Forms.GroupBox();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(54, 42);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(253, 21);
            this.textBox1.TabIndex = 0;
            // 
            // db_path
            // 
            this.db_path.Location = new System.Drawing.Point(313, 40);
            this.db_path.Name = "db_path";
            this.db_path.Size = new System.Drawing.Size(42, 23);
            this.db_path.TabIndex = 1;
            this.db_path.Text = "...";
            this.db_path.UseVisualStyleBackColor = true;
            this.db_path.Click += new System.EventHandler(this.button1_Click);
            // 
            // db_path_groupbox
            // 
            this.db_path_groupbox.Font = new System.Drawing.Font("Arial Narrow", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.db_path_groupbox.Location = new System.Drawing.Point(18, 16);
            this.db_path_groupbox.Name = "db_path_groupbox";
            this.db_path_groupbox.Size = new System.Drawing.Size(390, 76);
            this.db_path_groupbox.TabIndex = 2;
            this.db_path_groupbox.TabStop = false;
            this.db_path_groupbox.Text = "그룹경로";
            // 
            // UserControl1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.db_path);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.db_path_groupbox);
            this.Name = "UserControl1";
            this.Size = new System.Drawing.Size(428, 108);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button db_path;
        public System.Windows.Forms.GroupBox db_path_groupbox;
    }
}
