using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipeInfo
{
    public partial class WinForm_STEP : Form
    {
        public WinForm_STEP()
        {
            InitializeComponent();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridView1.Columns.Clear();
            DataGridViewCheckBoxColumn dgvCmb = new DataGridViewCheckBoxColumn();
            dgvCmb.ValueType = typeof(bool);
            dgvCmb.Name = "Chk";
            dgvCmb.HeaderText = "선택";

            dataGridView1.Columns.Add(dgvCmb);
            List<string> data = new List<string>();
            data.Add("Column1");
            dataGridView1.DataSource = data;
            
        }
    }
}
