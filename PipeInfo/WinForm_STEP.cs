using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Navigation;
using static PipeInfo.FileWatcher;

namespace PipeInfo
{
    public partial class WinForm_STEP : Form
    {
        enum stepPipeInfo
        {
            InstanceId,
            PipeSize,
            PipeMaterial,
            PipeLength,
            IsHole
        }
        public delegate void recive_SpoolList(List<string> spool_Li, List<string> handle_Li, List<double> spoolLength_Li);
        public event recive_SpoolList recive_SpoolList_event;

        List<string> _spool_Li = new List<string>();
        List<string> _handle_Li = new List<string>();
        List<double> _spoolLength_Li = new List<double>();
        FileWatcher fiw = new FileWatcher();
         
        // 그리드뷰 버튼 클릭을 위한 인덱스.
        PipeInfo pipeInfo = new PipeInfo();
        DatabaseIO db = new DatabaseIO();
        string db_path = "";
        string stepFileSave_path = "";
        public WinForm_STEP()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            showFileDialog();
        }

        public bool showFileDialog()
        {
            var ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "DDWorks DatabaseFile(*.db)|*.db";
            if (ofd.ShowDialog().ToString() == "OK")
            {
                textBox_DBPath.Text = ofd.FileName;
                db_path = ofd.FileName;
                return true;
            }
            else
            {
                return false;
            }

        }

        public bool saveFaileDialog()
        {
            var ofd = new System.Windows.Forms.SaveFileDialog();
            ofd.Filter = "(*.STP) | *.STP";
            if (ofd.ShowDialog().ToString() == "OK")
            {
                stepFileSave_path = ofd.FileName;
                string path_split = Path.GetDirectoryName(stepFileSave_path);
                //파일 감시 
                fiw.initWatcher(stepFileSave_path);
                return true;
            }
            else
            {
                return false;
            }

        }

        private void WinForm_STEP_Load(object sender, EventArgs e)
        {
            //FileWatcher와 이벤트 연결하기.
            recive_SpoolList_event += fiw.ReceiveSpoolList;
            button_Set_SpoolNumber.Enabled = false;
        }

        private void button_db_pathOk_Click(object sender, EventArgs e)
        {
            bool db_pathOk = false;
            pipeInfo.db_path = db_path;
            if (textBox_DBPath.Text != "")
            {
                this.DialogResult = DialogResult.OK;
                
                //this.Close();
                List<string> group_li = new List<string>();
                List<string> pipeLen_li = new List<string>();
                List<string> pipeLSize_li = new List<string>();

                db = new DatabaseIO(db_path);
                group_li = db.get_DB_GroupList();

                if (group_li.Count > 0)
                {
                    dataGridView_GroupList.Rows.Clear();
                    foreach (var group in group_li)
                    {
                        dataGridView_GroupList.Rows.Add(false, group);
                    }
                }
                else
                {
                    MessageBox.Show("경고 : 설계 그룹 정보가 0개입니다.");
                }
            }
            else
            {
                MessageBox.Show("경고 : 올바른 경로를 넣어주세요");
            }
        }


        private void dataGridView_PipesList_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
     
        private void dataGridView_GroupList_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

            if (dataGridView_GroupList.RowCount.ToString() != "0")
            {
                // STEP파일을 내보내기 전(Pipe를 그리기전) 3D 객체들을 모두 지우고 시작
                // STEP에 스풀 정보를 저장할때 객체 갯수와 스풀 정보갯수의 차이가 발생한다.
                pipeInfo.delete_All_Object();
                string groupName = "";
                List<string> pipeInstance_li = new List<string>();

                //DataGridView 행을 돌며 선택이 된 행의 그룹 이름을 가져온다.
                dataGridView_PipesList.Rows.Clear();
                groupName = dataGridView_GroupList.Rows[e.RowIndex].Cells[1].Value.ToString();

                // pipeInstance_li의 정보는 Instance, 파이프 사이즈, 등의 정보를 5개단위로 가지고 있다.
                // 그리드뷰에 파이프 정보를 표시한다.
                int count_Hole = 0;
                int count_Pipe = 0;
                double total_Length = 0;

                pipeInstance_li = db.Get_PipeInstances_Infor_By_GroupName(db_path, groupName);
                if (pipeInstance_li.Count > 5)
                {
                    for (int i = 0; i < pipeInstance_li.Count; i += 5)
                    {
                        dataGridView_PipesList.Rows.Add(
                            pipeInstance_li[i+ (int)stepPipeInfo.InstanceId], 
                            pipeInstance_li[i + (int)stepPipeInfo.PipeSize], 
                            pipeInstance_li[i + (int)stepPipeInfo.PipeMaterial], 
                            pipeInstance_li[i + (int)stepPipeInfo.PipeLength]+" mm", 
                            pipeInstance_li[i + (int)stepPipeInfo.IsHole]
                            );

                        //그리브뷰에서 파이프일때 파이프 갯수와 파이프 길이를 저장.
                        if(pipeInstance_li[i + (int)stepPipeInfo.IsHole] == "Pipe")
                        {
                            //dataGridView_PipesList.Rows[i].Cells[4].Style.ForeColor = Color.DarkBlue;
                            count_Pipe += 1;
                            total_Length += Double.Parse(pipeInstance_li[i + (int)stepPipeInfo.PipeLength]);
                        }
                        else if(pipeInstance_li[i + (int)stepPipeInfo.IsHole] == "Hole")
                        {
                            //dataGridView_PipesList.Rows[i].Cells[4].Style.ForeColor = Color.DarkGreen;
                        }
                    }
                }

                //단관 총 갯수 표시
                label_PipeCount.Text = (count_Pipe).ToString()+" 개";
                label_PipesLength.Text = total_Length.ToString() + " mm";

                string is_Checked = "";
                string is_Clicked_CheckBox = "";
                string is_StepOut_Button = "";
             

                is_Checked = dataGridView_GroupList.Rows[e.RowIndex].Cells[0].Value.ToString();
                is_Clicked_CheckBox = dataGridView_GroupList.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
                is_StepOut_Button = dataGridView_GroupList.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();

                // 체크 박스 클릭시 값 바꿔주기.
                if (is_Clicked_CheckBox == "False")
                {
                    dataGridView_GroupList.Rows[e.RowIndex].Cells[0].Value = true;
                }
                else if (is_Clicked_CheckBox == "True")
                {
                    dataGridView_GroupList.Rows[e.RowIndex].Cells[0].Value = false;
                }
               // MessageBox.Show(is_Clicked_CheckBox.ToString());

                // STEP 내보내기 버튼 눌렀을때 처리.
                if (is_Checked == "True" && is_StepOut_Button == "내보내기")
                {
                    bool is_SaveFile = false;
                    is_SaveFile = saveFaileDialog();
                    
                    bool is_PathInBlank = stepFileSave_path.Contains(" ");

                    if (is_SaveFile == true && is_PathInBlank == false)
                    {
                       (_spool_Li, _handle_Li, _spoolLength_Li) = pipeInfo.export_Pipes_StepFiles(groupName, stepFileSave_path);
                        
                        fiw.ReceiveSpoolList(_spool_Li, _handle_Li, _spoolLength_Li);
                        button_Set_SpoolNumber.Enabled = true;
                       
                        //string spooli = "";
                        //string hanli = "";

                        //foreach (var d in _spool_Li)
                        //{
                        //    spooli += d+"\n";
                        //}
                        //foreach (var d in _handle_Li)
                        //{
                        //    hanli += d + "\n";
                        //}
                        //MessageBox.Show("스풀정보 ㅣ " + spooli + "핸들: " + hanli.ToString());
                    }
                    else
                    {
                        MessageBox.Show("경고:경로에 빈칸이 존재합니다. 다른곳에 저장해주세요.");
                    }
                }
            }
        }


        private void button1_Click_1(object sender, EventArgs e)
        {
            fiw.stepFileWriteSpoolNumber();
            button_Set_SpoolNumber.Enabled = false;
           
        }

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }
    }

}
