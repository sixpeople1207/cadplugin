using Autodesk.AutoCAD.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace PipeInfo
{
    public delegate void DataGetEventHandler(string data);
    public partial class DB_Path_Winform : Form
    {
        public DataGetEventHandler DataSendEvent;
        public string db_path;
        public DB_Path_Winform()
        {
            InitializeComponent();
        }
        private void button_db_find_path_Click(object sender, EventArgs e)
        {
            //db파일 경로를 변수에 넘겨준다. 
            db_path = showFileDialog();
        }

        private void button_db_path_ok_Click(object sender, EventArgs e)
        {
            MessageBox.Show(db_path);
            this.DialogResult = DialogResult.OK;
            DataSendEvent(db_path);
            this.Close();
        }
  
 
        public string showFileDialog()
        {
            var ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "DDWorks DatabaseFile(*.db)|*.db";
            ofd.ShowDialog();
            if (ofd.ShowDialog().ToString() == "OK")
            {
                textBox_db.Text = ofd.FileName;
                return ofd.FileName;
            }
            else if (ofd.ShowDialog().ToString() == "CANCEL")
            {
                return "";
            }
            return "";
        } 
    }
}
