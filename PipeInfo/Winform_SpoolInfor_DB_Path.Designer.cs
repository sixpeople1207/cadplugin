using System.Windows.Forms;

namespace PipeInfo
{
    partial class Winform_SpoolInfor_DB_Path
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Winform_SpoolInfor_DB_Path));
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
            this.button_db_find_path.Location = new System.Drawing.Point(478, 64);
            this.button_db_find_path.Name = "button_db_find_path";
            this.button_db_find_path.Size = new System.Drawing.Size(55, 23);
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
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(604, 112);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "File Path";
            // 
            // button_db_path_ok
            // 
            this.button_db_path_ok.Location = new System.Drawing.Point(539, 64);
            this.button_db_path_ok.Name = "button_db_path_ok";
            this.button_db_path_ok.Size = new System.Drawing.Size(55, 23);
            this.button_db_path_ok.TabIndex = 5;
            this.button_db_path_ok.Text = "확인";
            this.button_db_path_ok.UseVisualStyleBackColor = true;
            this.button_db_path_ok.Click += new System.EventHandler(this.button_db_path_ok_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 37);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 4;
            this.label3.Text = "설명 : ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 69);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(69, 12);
            this.label2.TabIndex = 3;
            this.label2.Text = "파일 경로 : ";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(58, 37);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(269, 12);
            this.label1.TabIndex = 2;
            this.label1.Text = "DDWorks 설계 파일을 불러옵니다(확장자 : *.db)";
            // 
            // textBox_db
            // 
            this.textBox_db.Location = new System.Drawing.Point(86, 65);
            this.textBox_db.Name = "textBox_db";
            this.textBox_db.Size = new System.Drawing.Size(386, 21);
            this.textBox_db.TabIndex = 1;
            // 
            // Winform_SpoolInfor_DB_Path
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(631, 148);
            this.Controls.Add(this.groupBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Winform_SpoolInfor_DB_Path";
            this.Text = "제작도면 : Spool 정보 불러오기";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Winform_SpoolInfor_DB_Path_Load);
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