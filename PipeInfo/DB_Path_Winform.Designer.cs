using System.Windows.Forms;

namespace PipeInfo
{
    partial class DB_Path_Winform
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DB_Path_Winform));
            this.OpenFileDialog_DB = new System.Windows.Forms.OpenFileDialog();
            this.button_db_find_path = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.button_db_path_ok = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox_db = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // OpenFileDialog_DB
            // 
            this.OpenFileDialog_DB.FileName = "OpenFileDialog_DB";
            // 
            // button_db_find_path
            // 
            this.button_db_find_path.Location = new System.Drawing.Point(546, 80);
            this.button_db_find_path.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button_db_find_path.Name = "button_db_find_path";
            this.button_db_find_path.Size = new System.Drawing.Size(63, 29);
            this.button_db_find_path.TabIndex = 0;
            this.button_db_find_path.Text = "...";
            this.button_db_find_path.UseVisualStyleBackColor = true;
            this.button_db_find_path.Click += new System.EventHandler(this.button_db_find_path_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button_db_path_ok);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.textBox_db);
            this.groupBox1.Controls.Add(this.button_db_find_path);
            this.groupBox1.Location = new System.Drawing.Point(14, 15);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Size = new System.Drawing.Size(690, 140);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "File Path";
            // 
            // button_db_path_ok
            // 
            this.button_db_path_ok.Location = new System.Drawing.Point(616, 80);
            this.button_db_path_ok.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button_db_path_ok.Name = "button_db_path_ok";
            this.button_db_path_ok.Size = new System.Drawing.Size(63, 29);
            this.button_db_path_ok.TabIndex = 5;
            this.button_db_path_ok.Text = "확인";
            this.button_db_path_ok.UseVisualStyleBackColor = true;
            this.button_db_path_ok.Click += new System.EventHandler(this.button_db_path_ok_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 46);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(52, 15);
            this.label3.TabIndex = 4;
            this.label3.Text = "설명 : ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(17, 86);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 15);
            this.label2.TabIndex = 3;
            this.label2.Text = "파일 경로 : ";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(66, 46);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(334, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "DDWorks 설계 파일을 불러옵니다(확장자 : *.db)";
            // 
            // textBox_db
            // 
            this.textBox_db.Location = new System.Drawing.Point(98, 81);
            this.textBox_db.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBox_db.Name = "textBox_db";
            this.textBox_db.Size = new System.Drawing.Size(441, 25);
            this.textBox_db.TabIndex = 1;
            // 
            // DB_Path_Winform
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(721, 185);
            this.Controls.Add(this.groupBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "DB_Path_Winform";
            this.Text = "제작도면";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.OpenFileDialog OpenFileDialog_DB;
        private System.Windows.Forms.Button button_db_find_path;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button button_db_path_ok;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        public TextBox textBox_db;
    }
}