using Autodesk.AutoCAD.DatabaseServices;
using DINNO.DO3D.MEP.InputHandler;
using DINNO.DO3D.SceneGraph.Graphics.Scene.Object;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Navigation;
using static DINNO.HU3D.WPF.HookUpDesigner.FormBatchImport;
using static PipeInfo.FileWatcher;
using static PipeInfo.PipeInfo;

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
        public event recive_SpoolList _recive_SpoolList_event;
        
        private Thread _thread;

        private List<string> _spool_Li = new List<string>();
        private List<string> _handle_Li = new List<string>();
        private List<double> _spoolLength_Li = new List<double>();
        private FileWatcher fiw = new FileWatcher();

        // 그리드뷰 버튼 클릭을 위한 인덱스.
        private PipeInfo pipeInfo = new PipeInfo();
        private DatabaseIO db = new DatabaseIO();
        private string db_path = "";
        private string stepFileSave_path = "";
        private string groupName = "";
        
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
            _recive_SpoolList_event += fiw.ReceiveSpoolList;
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
            dataGridView_GroupList.SelectionMode = DataGridViewSelectionMode.CellSelect;
            if (dataGridView_GroupList.RowCount.ToString() != "0")
            {
                // STEP파일을 내보내기 전(Pipe를 그리기전) 3D 객체들을 모두 지우고 시작
                // STEP에 스풀 정보를 저장할때 객체 갯수와 스풀 정보갯수의 차이가 발생한다.
                pipeInfo.delete_All_Object();
                List<string> pipeInstance_li = new List<string>();
                List<double> pipeList = new List<double>();
                //DataGridView 행을 돌며 선택이 된 행의 그룹 이름을 가져온다.
                dataGridView_PipesList.Rows.Clear();
                groupName = dataGridView_GroupList.Rows[e.RowIndex].Cells[1].Value.ToString();
                if (groupName.Length > 0)
                {
                    button_Export_PipeList.Enabled = true;
                }
                // pipeInstance_li의 정보는 Instance, 파이프 사이즈, 등의 정보를 5개단위로 가지고 있다.
                // 그리드뷰에 파이프 정보를 표시한다.
                int count_Hole = 0;
                int count_Pipe = 0;
                double total_Length = 0;

                pipeInstance_li = db.Get_PipeInstances_Infor_By_GroupName(db_path, groupName);

                if (pipeInstance_li.Count >= 5)
                {
                    for (int i = 0; i < pipeInstance_li.Count; i += 5)
                    {
                        dataGridView_PipesList.Rows.Add(
                        //pipeInstance_li[i + (int)stepPipeInfo.InstanceId],
                        (dataGridView_PipesList.RowCount+1).ToString(),
                        pipeInstance_li[i + (int)stepPipeInfo.PipeSize],
                        pipeInstance_li[i + (int)stepPipeInfo.PipeMaterial],
                        pipeInstance_li[i + (int)stepPipeInfo.PipeLength] + " mm",
                        pipeInstance_li[i + (int)stepPipeInfo.IsHole]
                        ) ;
                        //그리브뷰에서 파이프일때 파이프 갯수와 파이프 길이를 저장.
                        count_Pipe += 1;
                        total_Length += Double.Parse(pipeInstance_li[i + (int)stepPipeInfo.PipeLength]);
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
                    // 사용중인 Thread 종료.
                    if(_thread != null)
                    {
                        _thread.Abort();
                    }
                    if (is_SaveFile == true && is_PathInBlank == false)
                    {
                        (_spool_Li, _handle_Li, _spoolLength_Li) = pipeInfo.export_Pipes_StepFiles(groupName, stepFileSave_path);
                        fiw.ReceiveSpoolList(_spool_Li, _handle_Li, _spoolLength_Li);
                        
                        // STEP파일 사용확인. STEP파일 생성 후 수정.
                        _thread = new Thread(check_StepFile);
                        _thread.Start();
                    }
                    else
                    {
                        MessageBox.Show("경고:경로에 빈칸이 존재합니다. 다른곳에 저장해주세요.", "STEP File Export");
                    }
                }
            }
        }

        // STEP파일이 생성이 되고나서 사용해제가 되고 나면 스풀길이 적용.
        public void check_StepFile()
        {
            bool file_used = true;
            while (file_used)
            {
                file_used = fiw.CheckFileLocked(stepFileSave_path);
                if (file_used == false)
                {
                    MessageBox.Show("STEP파일이 생성 되었습니다.", "STEP File Export");
                    //STEP 파일 수정 : 길이정보및 스풀이름 적어주기.
                    fiw.stepFileWriteSpoolNumber();
                    break;
                }
                Thread.Sleep(100);
            }
        }

        // Chech Stepfile 함수 적용으로 기능 삭제.
        //private void button1_Click_1(object sender, EventArgs e)
        //{
        //    bool isWrite = false;
        //    isWrite = fiw.stepFileWriteSpoolNumber();
        //    button_Set_SpoolNumber.Enabled = false;
        //    button_Set_SpoolNumber.BackColor = Color.LightGray;
        //    if (isWrite == true)
        //    {
        //        MessageBox.Show("스풀정보 입력이 완료되었습니다.", "STEP File Export");
        //    }
        //}

    
        private void button_Export_PipeList_Click(object sender,EventArgs a)
        {
            // 파이프 리스트가 0이상일때.. 작동하도록.. 
            List<string> header = new List<string>()
            { "번호","관경","재질","파이프 길이","스풀이름","Hole" };
            ExcelObject excel = new ExcelObject(header);
            Compare comparePoint = new Compare();

            // pipeInstance_li의 정보는 Instance, 파이프 사이즈, 등의 정보를 5개단위로 가지고 있다.
            // 그리드뷰에 파이프 정보를 표시한다.
            List<string> pipeInstance_li = new List<string>();
            string spoolNambe = "";
            if (groupName != null)
            {
                pipeInstance_li = db.Get_PipeInstances_Infor_By_GroupName(db_path, groupName);
                
                for (int i = 0; i < pipeInstance_li.Count; i+=5)
                {
                    int j = 0;
                    int num = 1;
                    if (i > 0) { 
                        j = (i / 5);
                        num = j + 1;
                        excel.excel_InsertData(j, 1, num.ToString(), false);
                    }
                    else
                    {
                        excel.excel_InsertData(j, 1, num.ToString(), false);
                    }
                    excel.excel_InsertData(j, 2, pipeInstance_li[i + (int)stepPipeInfo.PipeSize], false);
                    excel.excel_InsertData(j, 3, pipeInstance_li[i + (int)stepPipeInfo.PipeMaterial], false);
                    excel.excel_InsertData(j, 4, pipeInstance_li[i + (int)stepPipeInfo.PipeLength], false);

                    //스풀 정보 
                    spoolNambe = db.Get_SpoolInfo_By_InstanceID(pipeInstance_li[i]);
                    excel.excel_InsertData(j, 5, spoolNambe, false);

                    excel.excel_InsertData(j, 6, pipeInstance_li[i + (int)stepPipeInfo.IsHole], false);
                }
                excel.excel_save();
            }
        }
    }

}
