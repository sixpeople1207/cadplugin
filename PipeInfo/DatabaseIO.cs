using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Data.SqlClient;
using Autodesk.Internal.Windows;
using static PipeInfo.PipeInfo;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;
using DINNO.HU3D.ViewModel.STD;
using System.Security.Cryptography;
using System.Windows.Shapes;
using DINNO.HU3D.Workspace.ProductionDrawing;

namespace PipeInfo
{
    class DatabaseIO
    {
        private string db_path = "";
        private string connstr = "";
        //private string ownerType_Component = "768"; //TB_POCINSTANCES:OWNER_TYPE 기자재.
        //private string ownerType_Pipe = "256"; //TB_POCINSTANCES:OWNER_TYPE 파이프.
        private string pipeInsType_Pipe = "17301760"; //
        private double pipeSize_Limit = 260;

        public DatabaseIO()
        {
        }
        public DatabaseIO(string acDB_path)
        {
            db_path = acDB_path;
            connstr= "Data Source = " + db_path;
        }
      
        public List<string> get_DB_GroupList()
        {
            List<String> groupList = new List<string>();
            try
            {
                if (db_path != "")
                {
                    string connStr = "Data Source=" + db_path;
                    using (SQLiteConnection conn = new SQLiteConnection(connStr))
                    {
                        conn.Open();
                        string sql = "SELECT INSTANCE_GROUP_NM " +
                            "FROM TB_INSTANCEGROUPS " +
                            "WHERE NOT hex(INSTANCE_GROUP_PARENT_ID) like '00000000000000000000000000000000';";
                        SQLiteCommand command = new SQLiteCommand(sql, conn);
                        SQLiteDataReader rdr = command.ExecuteReader();
                        while (rdr.Read())
                        {
                            groupList.Add(rdr["INSTANCE_GROUP_NM"].ToString());
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Error : 데이터베이스 파일이 없습니다.\n 경로를 확인해주세요.");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
             return groupList;
        }
        public (List<string>, List<Point3d>, List<double>, List<double>) Get_POCInformation_By_GroupName(string groupName, string instanceID)
        {
            List<string> pipeInfor = new List<string>();
            List<Point3d> pipePos = new List<Point3d>();
            List<double> pipeLength = new List<double>();
            List<double> pipeDia = new List<double>();
            try
            {
                    if (db_path != null)
                    {
                        string connstr = "Data Source=" + db_path;
                        using (SQLiteConnection conn = new SQLiteConnection(connstr))
                        {
                            conn.Open();
                            string sql = string.Format("SELECT PO.POSX, PO.POSY, PO.POSZ, PI.LENGTH1, PS.OUTERDIAMETER, PI.INSTANCE_ID " +
                                        " From TB_INSTANCEGROUPMEMBERS as GM " +
                                        " INNER JOIN TB_PIPEINSTANCES as PI " +
                                        "ON PI.INSTANCE_ID = GM.INSTANCE_ID " +
                                        "  AND GM.INSTANCE_GROUP_ID = " +
                                        "(SELECT INSTANCE_GROUP_ID From TB_INSTANCEGROUPS  " +
                                        "WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM = '{0}') " +
                                        "INNER JOIN TB_POCINSTANCES as PO " +
                                        "   ON PI.INSTANCE_ID = PO.OWNER_INSTANCE_ID AND PI.PIPE_TYPE='{1}' " +
                                        "INNER JOIN TB_PIPESIZE as PS " +
                                        " ON PO.PIPESIZE_ID = PS.PIPESIZE_ID " +
                                        "WHERE hex(PI.INSTANCE_ID) like '{2}';", groupName, pipeInsType_Pipe, instanceID);
                            if (sql != "")
                            {
                                SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                                SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                                string instanceId = "";
                                while (rdr_ready.Read())
                                {
                                    instanceId = BitConverter.ToString((byte[])rdr_ready["INSTANCE_ID"]).Replace("-", "");
                                    pipeInfor.Add(instanceId);
                                    pipePos.Add(new Point3d((double)rdr_ready["POSX"], (double)rdr_ready["POSY"], (double)rdr_ready["POSZ"]));
                                    pipeLength.Add((double)rdr_ready["LENGTH1"]);
                                    pipeDia.Add((double)rdr_ready["OUTERDIAMETER"]);
                                }
                            }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            return (pipeInfor, pipePos, pipeLength, pipeDia);

        }
        public List<string> Get_PipeInstances_By_GroupName(string groupName)
        {
            List<string> pipeIns = new List<string>();
            string connstr = "Data Source=" + db_path;
            try
            {

                if (db_path != "")
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        string sql = string.Format("SELECT INSTANCE_ID, INSTANCE_GROUP_NM FROM TB_INSTANCEGROUPMEMBERS as IM" +
                            " INNER JOIN TB_INSTANCEGROUPS as IG " +
                            "ON IM.INSTANCE_GROUP_ID = IG.INSTANCE_GROUP_ID " +
                            "WHERE INSTANCE_GROUP_NM='{0}';", groupName);

                        conn.Open();
                        if (sql != "")
                        {
                            SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                            while (rdr_ready.Read())
                            {
                                string instanceId = BitConverter.ToString((byte[])rdr_ready["INSTANCE_ID"]).Replace("-", "");
                                pipeIns.Add(instanceId);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Error : DDWorks Database 136 파일을 로드해주세요.");
                }

            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            return pipeIns;
        }

        /// <summary>
        /// DDWorks Database에서 파이프 인스턴스 정보를 불러온다. UI 파싱용 (배열 정보(0~4):인스턴스아이디,파이프사이즈,재질,길이,파이프타입(Takeoff인지))
        /// </summary>
        public List<string> Get_PipeInstances_Infor_By_GroupName(string db_path, string groupName)
        {
            //그룹정보에 해당하는 TB_PIPEINSTANCES의 정보를 가져온다. 파이프, 테이크 오프, 길이, 관경, 재질
            List<string> pipeInsInfo = new List<string>();
            string connstr = "Data Source=" + db_path;
            try
            {
                if (db_path != "")
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        string sql = string.Format("SELECT DISTINCT PD.PIPESTD_NM ,PO.OWNER_INSTANCE_ID , PS.OUTERDIAMETER, PIPESIZE_NM, MATERIAL_NM ,LENGTH1, PO.CONNECTION_ORDER From TB_INSTANCEGROUPMEMBERS as GM " +
                                "INNER JOIN " +
                                "TB_PIPEINSTANCES as PI "+
                                "ON PI.INSTANCE_ID = GM.INSTANCE_ID " +
                                "AND GM.INSTANCE_GROUP_ID = " +
                                "(SELECT INSTANCE_GROUP_ID From TB_INSTANCEGROUPS " +
                                "WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM = '{0}') " +
                                "INNER JOIN TB_POCINSTANCES as PO  " +
                                "ON PI.INSTANCE_ID = PO.OWNER_INSTANCE_ID AND PI.PIPE_TYPE= '{1}' " +
                                "INNER JOIN TB_PIPESIZE as PS  " +
                                "ON PO.PIPESIZE_ID = PS.PIPESIZE_ID " +
                                "INNER JOIN TB_PIPESTD as PD "+
                                "ON PS.PIPESTD_ID = PD.PIPESTD_ID "+ //파이프 여부 확인 24.8.2추가
                                "INNER JOIN TB_MATERIALS as MG " +
                                "ON PO.MATERIAL_ID = MG.MATERIAL_ID;",groupName, pipeInsType_Pipe);
                        conn.Open();
                        if (sql != "")
                        {
                            SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                            int index = 0;
                            int takeOffCount = 0;

                            while (rdr_ready.Read())
                            {
                                if (rdr_ready["CONNECTION_ORDER"].ToString() != "1") //그리드뷰에서 POC1개의 정보만 보여주기 위해 1개는 걸러냄.
                                {
                                    string instanceId = BitConverter.ToString((byte[])rdr_ready["OWNER_INSTANCE_ID"]).Replace("-", "");
                                    double length = Math.Round((double)rdr_ready["LENGTH1"], 1);
                                    double pipeDia = (double)rdr_ready["OUTERDIAMETER"];
                                    string hole = "Hole"; 
                                    string pipeSize = "";
                                    string material_Nm = "";
                                    double pipeDepth = 0;
                                    //파이프에 Depth값을 적용(Pipe와 연결된 기자재에 Depth값이 있으면 절대값을 모두 더해서 파이프 길이에서 빼준다)
                                    pipeDepth = Get_PipeDepth_Info_By_PipInstace(instanceId);
                                    if (pipeDepth > 0)
                                    {
                                        //ABS풀고 더해주는 방식으로 변경 24.10.18
                                        length = length + pipeDepth;
                                    }
                                    //Pipe그룹인지 검사 => Take Off를 뚫을 수 없어서 제외
                                    string isPipe = rdr_ready["PIPESTD_NM"].ToString().ToUpper();
                                    Int64 connectInt = (Int64)rdr_ready["CONNECTION_ORDER"];
                                    material_Nm = rdr_ready["MATERIAL_NM"].ToString();
                                    pipeSize = rdr_ready["PIPESIZE_NM"].ToString();
                                    

                                    //파이프 STD객체중 Pipe인 객체만 걸러낸다. POC 2개이상은 Takeoff
                                    if (connectInt < 2)
                                    {
                                        if ((isPipe.Contains("PIPE") || isPipe.Contains("NW")) && length > 0 && pipeDia < pipeSize_Limit) //Pipe인데 25.4이거나 Hole만 그리드 뷰에 적는다. -> Hole은 제외, 250A 이상은 사이즈 리미트
                                        {
                                            pipeInsInfo.Add(instanceId);
                                            pipeInsInfo.Add(pipeSize);
                                            pipeInsInfo.Add(material_Nm);
                                            pipeInsInfo.Add(length.ToString());
                                            pipeInsInfo.Add("-"); //추후 삭제(Hole인지 여부는 필요없음)
                                            index += 5;
                                            takeOffCount = 0;
                                        }
                                    }

                                    //한 파이프에 POC Order가 2개 이상인 Take-off 객체만 걸러낸다. 
                                    if(connectInt > 1)
                                    {
                                        pipeInsInfo.Add(instanceId);
                                        pipeInsInfo.Add(pipeSize);
                                        pipeInsInfo.Add(material_Nm);
                                        pipeInsInfo.Add("0");
                                        pipeInsInfo.Add(hole); //추후 삭제(Hole인지 여부는 필요없음)
                                        index += 5;
                                        takeOffCount += 1;
                                       
                                    }

                                    if (takeOffCount == 1) //TakeOff갯수에 상관없이 첫번째 TakeOff일때 한번만 진입. TakeOff번호는 순서대로 들어갔다가 지우면 중간에 번호빠짐. count로 진행.
                                    {
                                        pipeInsInfo[index - 6] = hole;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Error : DDWorks Database 파일을 로드해주세요.");
                }

            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

            return pipeInsInfo;
        }
        
        /// <summary>
        /// 용접공정에 파이프피팅(플랜지등)에 Depth값을 반환. 통상 Depth는 파이프 끝단에 있으나 좌.우 Depth(절대값)을 더해서 반환. 
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        public double Get_PipeDepth_Info_By_PipInstace(string instanceId)
        {
            //아래 두 쿼리는 문제가 발생. 조인에서는 중복이 너무 많이 뜨고, 아래쿼리는 파이프양옆에 피팅이 붙어서 같은 TemplateId가 붙으면 1개만 나오는현상.
            //string sql = string.Format("SELECT DEPTH FROM TB_POCTEMPLATES " +
            //    "WHERE POC_TEMPLATE_ID " +
            //    "IN " +
            //    "(SELECT POC_TEMPLATE_ID " +
            //    "FROM TB_POCINSTANCES " +
            //    "WHERE INSTANCE_ID " +
            //    "IN " +
            //    "(SELECT CONNECTED_POC_ID " +
            //    "FROM TB_POCINSTANCES " +
            //    "WHERE hex(OWNER_INSTANCE_ID) like '{0}'));",instanceId);

            //Inner조인을 하고 POC TEMPLATE에 맞는 Depth값을 때 중복값이 생기게 되기 때문에 Select뒤에 DISTINCT옵션을 써서 제거.
            //string sql = string.Format("SELECT DISTINCT DEPTH FROM TB_POCTEMPLATES " +
            //    " as PM INNER JOIN TB_POCINSTANCES as PI WHERE PM.POC_TEMPLATE_ID = " +
            //    " PI.POC_TEMPLATE_ID " +
            //    " AND " +
            //    " PM.POC_TEMPLATE_ID" +
            //    " IN(SELECT POC_TEMPLATE_ID FROM TB_POCINSTANCES WHERE INSTANCE_ID " +
            //    " IN " +
            //    " (SELECT CONNECTED_POC_ID FROM TB_POCINSTANCES WHERE hex(OWNER_INSTANCE_ID) like '{0}'));"
            //    , instanceId);

            //쿼리를 아래와 같이 진행하고 결과값으로 수동으로 쿼리를 돌리기로 함.
            string sql = string.Format("SELECT POC_TEMPLATE_ID " +
                "FROM TB_POCINSTANCES " +
                "WHERE INSTANCE_ID " +
                "IN " +
                "(SELECT CONNECTED_POC_ID " +
                "FROM TB_POCINSTANCES " +
                "WHERE hex(OWNER_INSTANCE_ID) like '{0}');", instanceId);


            double depth = 0;
            string connstr = this.connstr;
            try
            {
                if (db_path != "")
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        SQLiteCommand comm = new SQLiteCommand(sql, conn);
                        SQLiteDataReader rdr_ready = comm.ExecuteReader();
                        
                        //POC TEMPLATE ID를 여러개 찾고 IN으로 붙였더니 가끔 같은 결과값이 있으면 1개만 출력되는 현상이 있어서 Command를 두 번사용.[24.9.27]
                        while (rdr_ready.Read()) {
                            string id = BitConverter.ToString((byte[])rdr_ready["POC_TEMPLATE_ID"]).Replace("-", "");
                            string sql_2 = string.Format("SELECT DEPTH FROM TB_POCTEMPLATES WHERE hex(POC_TEMPLATE_ID) like '{0}'; ", id);
                            SQLiteCommand comm_2 = new SQLiteCommand(sql_2, conn);
                            SQLiteDataReader rdr_ready_2 = comm_2.ExecuteReader();
                            
                            while (rdr_ready_2.Read()) 
                            { 
                                //ABS로 절대값을 가져와 빼주는 것으로 하려고 했으나 Depth의 방향이 있어서 ABS풀고 파이프길이에 빼주는 것에서 더해주는것으로 수정.
                                depth += (double)rdr_ready_2["DEPTH"];
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(),"DDWorks CAD PlugIn");
            }

            return depth;
        }

        /// <summary>
        /// STEP을 그리기 위한 그룹 이름을 기준으로 파이프 인스턴스를 가져오고
        /// 파이프 인스턴스중 PIPE TYPE이 17301760인 객체만 가져와서 POC위치, 길이, 파이프 사이즈정보를 리스트로 넘겨준다.
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="instanceID"></param>
        /// <returns></returns>
        public (List<string>, List<Point3d>, List<double>, List<double>, List<double>) Get_PipeInformation_By_GroupName(string groupName, string instanceID)
        {
            List<string> pipesInfor = new List<string>();
            List<Point3d> pipesPos = new List<Point3d>();
            List<double> pipesLength = new List<double>();
            List<double> pipesDia = new List<double>();
            List<double> xyzrAngle = new List<double>();

            //파이프의 Depth값을 가져와 파이프 길이에서 빼준다. 
            double depth = Get_PipeDepth_Info_By_PipInstace(instanceID);

            try
            {
                if (this.db_path != null)
                {
                    using (SQLiteConnection conn = new SQLiteConnection(this.connstr))
                    {
                        conn.Open();

                        //24.11.05 중복된 항목 제거함. 예전에 중복된 항목 했다가 문제있었는데 좀 더 테스트 필요. 
                        string sql = string.Format("SELECT DISTINCT PO.POSX, PO.POSY, PO.POSZ, PI.LENGTH1, PS.OUTERDIAMETER, PI.INSTANCE_ID, PI.XANGLE,PI.YANGLE,PI.ZANGLE,PO.RADIAN,PD.PIPESTD_NM,PO.CONNECTION_ORDER " +
                                    "From TB_INSTANCEGROUPMEMBERS as GM " +
                                    "INNER JOIN TB_PIPEINSTANCES as PI " +
                                    "ON PI.INSTANCE_ID = GM.INSTANCE_ID " +
                                    "AND GM.INSTANCE_GROUP_ID = " +
                                    "(SELECT INSTANCE_GROUP_ID From TB_INSTANCEGROUPS  " +
                                    "WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM = '{0}') " +
                                    "INNER JOIN TB_POCINSTANCES as PO " +
                                    "ON PI.INSTANCE_ID = PO.OWNER_INSTANCE_ID AND PI.PIPE_TYPE='{1}' " +
                                    "INNER JOIN TB_PIPESIZE as PS " +
                                    "ON PO.PIPESIZE_ID = PS.PIPESIZE_ID " +
                                    "INNER JOIN TB_PIPESTD as PD " +
                                    "ON PS.PIPESTD_ID = PD.PIPESTD_ID " +
                                    "WHERE hex(PI.INSTANCE_ID) like '{2}' " +
                                    "ORDER by PS.OUTERDIAMETER DESC;", groupName, pipeInsType_Pipe, instanceID);
                        int i = 0;
                        List<Int64> connectIntLi = new List<Int64>();
                        if (sql != "")
                        {
                            SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                            string instanceId = "";
                            while (rdr_ready.Read())
                            {
                                //Pipe그룹인지 검사 => Take Off를 뚫을 수 없어서 제외
                                string isPipe = rdr_ready["PIPESTD_NM"].ToString().ToUpper();
                                Int64 connectInt = (Int64)rdr_ready["CONNECTION_ORDER"];                                

                                double length = Math.Round((double)rdr_ready["LENGTH1"], 1);
                                //Depth값을 파이프 길이에서 빼준다.
                                if (depth > 0)
                                {
                                    length = length + depth;
                                }

                                double pipeDia = (double)rdr_ready["OUTERDIAMETER"];
                                double pipeSizeLimit = 260;
                                //파이프 STD객체중 Pipe인 객체와 Take off객체만 가져온다.  Take off는 CONNECTION_ORDER가 1이상. 250A 이상은 사이즈 리미트
                                if (connectInt < 2 )
                                {
                                    if ((isPipe.Contains("PIPE") || isPipe.Contains("NW")) && length > 0 && pipeDia < pipeSizeLimit)
                                    {
                                        instanceId = BitConverter.ToString((byte[])rdr_ready["INSTANCE_ID"]).Replace("-", "");
                                        pipesInfor.Add(instanceId);
                                        pipesPos.Add(new Point3d((double)rdr_ready["POSX"], (double)rdr_ready["POSY"], (double)rdr_ready["POSZ"]));
                                        pipesLength.Add(length);
                                        pipesDia.Add((double)rdr_ready["OUTERDIAMETER"]);
                                        xyzrAngle.Add((double)rdr_ready["XANGLE"]);
                                        xyzrAngle.Add((double)rdr_ready["YANGLE"]);
                                        xyzrAngle.Add((double)rdr_ready["ZANGLE"]);
                                        xyzrAngle.Add((double)rdr_ready["RADIAN"]);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else if (connectInt > 1)
                                {
                                    instanceId = BitConverter.ToString((byte[])rdr_ready["INSTANCE_ID"]).Replace("-", "");
                                    pipesInfor.Add(instanceId);
                                    pipesPos.Add(new Point3d((double)rdr_ready["POSX"], (double)rdr_ready["POSY"], (double)rdr_ready["POSZ"]));
                                    pipesLength.Add(length);
                                    pipesDia.Add((double)rdr_ready["OUTERDIAMETER"]);
                                    //xyzrAngle.Add((double)rdr_ready["XANGLE"]);
                                    //xyzrAngle.Add((double)rdr_ready["YANGLE"]);
                                    //xyzrAngle.Add((double)rdr_ready["ZANGLE"]);
                                    //xyzrAngle.Add((double)rdr_ready["RADIAN"]);

                                }

                                i += 1;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "DDWorks Cad Plug-In");
            }
            // pipeInfor : 인스턴스 아이디 , 포지션, 길이, 다이어미터
            return (pipesInfor, pipesPos, pipesLength, pipesDia, xyzrAngle);

        }

        public List<double> Get_PipeList_By_GroupName(string groupName)
        {
            List<string> pipeInfor = new List<string>();
            List<Point3d> pipePos = new List<Point3d>();
            List<double> pipeLength = new List<double>();
            List<double> pipeDia = new List<double>();
            List<double> xyzrAngle = new List<double>();

            try
            {
                if (db_path != null)
                    {
                        string connstr = "Data Source=" + db_path;
                        using (SQLiteConnection conn = new SQLiteConnection(connstr))
                        {
                            conn.Open();
                            string sql = string.Format("SELECT PO.POSX, PO.POSY, PO.POSZ, PI.LENGTH1, PS.OUTERDIAMETER, PI.INSTANCE_ID, PO.XANGLE,PO.YANGLE,PO.ZANGLE,PO.RADIAN,PD.PIPESTD_NM,PO.CONNECTION_ORDER " +
                                        "From TB_INSTANCEGROUPMEMBERS as GM " +
                                        "INNER JOIN TB_PIPEINSTANCES as PI " +
                                        "ON PI.INSTANCE_ID = GM.INSTANCE_ID " +
                                        "AND GM.INSTANCE_GROUP_ID = " +
                                        "(SELECT INSTANCE_GROUP_ID From TB_INSTANCEGROUPS  " +
                                        "WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM = '{0}') " +
                                        "INNER JOIN TB_POCINSTANCES as PO " +
                                        "ON PI.INSTANCE_ID = PO.OWNER_INSTANCE_ID AND PI.PIPE_TYPE='{1}' " +
                                        "INNER JOIN TB_PIPESIZE as PS " +
                                        "ON PO.PIPESIZE_ID = PS.PIPESIZE_ID " +
                                        "INNER JOIN TB_PIPESTD as PD " +
                                        "ON PS.PIPESTD_ID = PD.PIPESTD_ID " +
                                        "ORDER by PS.OUTERDIAMETER DESC;", groupName, pipeInsType_Pipe);
                            if (sql != "")
                            {
                                SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                                SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                                string instanceId = "";
                                while (rdr_ready.Read())
                                {
                                    //Pipe그룹인지 검사 => Take Off를 뚫을 수 없어서 제외
                                    string isPipe = rdr_ready["PIPESTD_NM"].ToString().ToUpper();
                                    Int64 connectInt = (Int64)rdr_ready["CONNECTION_ORDER"];

                                    if ((isPipe.Contains("PIPE") || isPipe.Contains("NW")) && connectInt == 0) //온전히 파이프 갯수를 구하기 위해 POC번호 0번
                                    {
                                        instanceId = BitConverter.ToString((byte[])rdr_ready["INSTANCE_ID"]).Replace("-", "");
                                        pipeInfor.Add(instanceId);
                                        pipePos.Add(new Point3d((double)rdr_ready["POSX"], (double)rdr_ready["POSY"], (double)rdr_ready["POSZ"]));
                                        pipeLength.Add((double)rdr_ready["LENGTH1"]);
                                        pipeDia.Add((double)rdr_ready["OUTERDIAMETER"]);
                                        xyzrAngle.Add((double)rdr_ready["XANGLE"]);
                                        xyzrAngle.Add((double)rdr_ready["YANGLE"]);
                                        xyzrAngle.Add((double)rdr_ready["ZANGLE"]);
                                        xyzrAngle.Add((double)rdr_ready["RADIAN"]);
                                    }
                                }
                            }
                        }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "DDWorks Cad Plug-In");
            }
            // pipeInfor : 인스턴스 아이디 , 포지션, 길이, 다이어미터
            return pipeLength;
        }
        /// <summary>
        /// DDWorks Database에서 입력받은 그룹에 해당하는 모든 Instance의 Spool이름을 리스트로 반환한다.
        /// </summary>
        public List<string> Get_SpoolList_By_GroupName(string db_path, string groupName)
        {
            List<string> spool_list = new List<string>();
            string sql = string.Format(@"SELECT DISTINCT PIPESIZE_NM,UTILITY_NM,MATERIAL_NM,PRODUCTION_DRAWING_GROUP_NM,SPOOL_NUMBER 
            From TB_INSTANCEGROUPMEMBERS as GM
	            INNER JOIN
	            TB_PIPEINSTANCES as PI
		            ON PI.INSTANCE_ID = GM.INSTANCE_ID
		            AND GM.INSTANCE_GROUP_ID =
			        (SELECT INSTANCE_GROUP_ID From TB_INSTANCEGROUPS 
				        WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM = '{0}')
	            INNER JOIN TB_POCINSTANCES as PO
		            ON PI.INSTANCE_ID = PO.OWNER_INSTANCE_ID AND PI.PIPE_TYPE= '{1}'
	            INNER JOIN TB_PIPESIZE as PS
		            ON PO.PIPESIZE_ID = PS.PIPESIZE_ID
	            INNER JOIN TB_PRODUCTION_DRAWING as PD
		            ON PD.PRODUCTION_DRAWING_GROUP_ID = PDG.PRODUCTION_DRAWING_GROUP_ID
	            INNER JOIN TB_PRODUCTION_DRAWING_GROUPS as PDG
		            ON PDG.INSTANCE_GROUP_ID = GM.INSTANCE_GROUP_ID AND PDG.INSTANCE_GROUP_ID = GM.INSTANCE_GROUP_ID
	            INNER JOIN TB_MATERIALS as MG
		            ON PO.MATERIAL_ID = MG.MATERIAL_ID
	            INNER JOIN TB_UTILITIES as UT
		            ON PO.UTILITY_ID = UT.UTILITY_ID;",groupName, pipeInsType_Pipe);

            List<string> pipeInsInfo = new List<string>();
            string connstr = "Data Source=" + db_path;
            try
            {
                if (db_path != "")
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        SQLiteCommand comm = new SQLiteCommand(sql, conn);
                        SQLiteDataReader rdr = comm.ExecuteReader();
                        if (rdr.HasRows) //rdr 반환값이 있을때만 Read
                        {
                            rdr.Read();
                            string instance_GroupId = BitConverter.ToString((byte[])rdr["INSTANCE_GROUP_ID"]).Replace("-", "");
                            rdr.Close();
                            comm.Dispose();
                            comm = new SQLiteCommand(sql, conn);
                            rdr = comm.ExecuteReader();
                            string material_NM = "";
                            string spool_num = "";
                            while (rdr.Read())
                            {
                                string rdr_instanceGroupId = BitConverter.ToString((byte[])rdr["INSTANCE_GROUP_ID"]).Replace("-", "");
                                //찾은 객체의 첫번째 항목만 불러온다. -> 7.10수정 DWG 파일 이름이 곧 INSTANCE GORUP NM이기때문에 INSTANCEGROUP ID와 동일한 SpoolNM을 가져온다.
                                //함수 추가 필요. 파일이름 -> INSTANCE GROUP NM -> ID DB연결할때 가져와야한다.
                                if (rdr_instanceGroupId == instance_GroupId)
                                {
                                    material_NM = rdr["MATERIAL_NM"].ToString();
                                    spool_num = rdr["SPOOL_NUMBER"].ToString();
                                    if (material_NM.Contains("SUS"))
                                    {
                                        string[] split_material = material_NM.Split(' ');
                                        if (split_material.Length > 1)
                                        {
                                            material_NM = split_material[1];
                                        }
                                    }
                                    if (spool_num.Length == 1)
                                    {
                                        spool_num = "0" + spool_num;
                                    }
                                    spool_list.Add(rdr["PIPESIZE_NM"] + "_" + rdr["UTILITY_NM"] + "_" + material_NM + "_" + rdr["PRODUCTION_DRAWING_GROUP_NM"] + "_" + spool_num);
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error : 해당 배관에 대한 데이터가 없습니다. Line:4381");
                        }
                        conn.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
            }
            
            return spool_list;

        }
        /// <summary>
        /// DDWorks Database에서 한 개의 인스턴스의 Spool이름을 반환.
        /// </summary>
        public string Get_SpoolInfo_By_InstanceID(string instanceID)
        {
            string spool = String.Empty;
            // 스풀 정보 찾을때 OuterDiameter를 오름차순으로 정렬 추가
            string sql = string.Format(
                "SELECT DISTINCT PS.OUTERDIAMETER, PDG.INSTANCE_GROUP_ID, PIPESIZE_NM, UTILITY_NM, PRODUCTION_DRAWING_GROUP_NM, MATERIAL_NM, SPOOL_NUMBER, IGM.INSTANCE_GROUP_ID " +
                "FROM TB_POCINSTANCES as PI " +
                "INNER JOIN TB_PIPEINSTANCES as PT " +
                "on PI.OWNER_INSTANCE_ID = PT.INSTANCE_ID " +
                "INNER JOIN TB_PIPESIZE as PS " +
                "on PI.PIPESIZE_ID = PS.PIPESIZE_ID " +
                "INNER JOIN TB_MATERIALS as MR " +
                "on MR.MATERIAL_ID = PI.MATERIAL_ID " +
                "INNER JOIN TB_UTILITIES as UT " +
                "on PI.UTILITY_ID = UT.UTILITY_ID " +
                "INNER JOIN TB_PRODUCTION_DRAWING as PD " +
                "on PD.INSTANCE_ID = PI.OWNER_INSTANCE_ID " +
                "INNER JOIN TB_PRODUCTION_DRAWING_GROUPS as PDG " +
                "on PDG.PRODUCTION_DRAWING_GROUP_ID = PD.PRODUCTION_DRAWING_GROUP_ID " +
                "INNER JOIN TB_INSTANCEGROUPMEMBERS as IGM " +
                "on IGM.INSTANCE_GROUP_ID = PDG.INSTANCE_GROUP_ID WHERE PD.SPOOL_NUMBER > -1 AND hex(PI.OWNER_INSTANCE_ID) like '{0}' ORDER BY PS.OUTERDIAMETER DESC LIMIT 1;"
                , instanceID);

            string connstr = "Data Source=" + db_path;
            try
            {
                if (db_path != "")
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        SQLiteCommand comm = new SQLiteCommand(sql, conn);
                        SQLiteDataReader rdr = comm.ExecuteReader();
                        if (rdr.HasRows) //rdr 반환값이 있을때만 Read
                        {
                            rdr.Read();
                            string instance_GroupId = BitConverter.ToString((byte[])rdr["INSTANCE_GROUP_ID"]).Replace("-", "");
                            //rdr.Close();
                            //comm.Dispose();
                            comm = new SQLiteCommand(sql, conn);
                            rdr = comm.ExecuteReader();
                            string material_NM = "";
                            string spool_num = "";
                            while (rdr.Read())
                            {
                                material_NM = rdr["MATERIAL_NM"].ToString();
                                spool_num = rdr["SPOOL_NUMBER"].ToString();
                                double size = Double.Parse(rdr["OUTERDIAMETER"].ToString());
                                //double limit_Dia = 30;
                                if (material_NM.Contains("SUS"))
                                {
                                    string[] split_material = material_NM.Split(' ');
                                    if (split_material.Length > 1)
                                    {
                                        material_NM = split_material[1];
                                    }
                                }
                                if (spool_num.Length == 1)
                                {
                                    spool_num = "0" + spool_num;
                                }

                                //if (size > limit_Dia) //
                                //{
                                //spool = (rdr["PIPESIZE_NM"] + "_" + rdr["UTILITY_NM"] + "_" + material_NM + "_" + rdr["PRODUCTION_DRAWING_GROUP_NM"] + "_" + spool_num);
                                string groupNm = (string)rdr["PRODUCTION_DRAWING_GROUP_NM"];
                                if (groupNm.Contains("__"))
                                {
                                    groupNm = groupNm.Replace("__", "_");
                                }

                                spool = (groupNm + spool_num);
                            //}
                            }
                        }
                        else
                        {
                            spool = "Nodefined";
                            //MessageBox.Show("Error : 해당 배관에 대한 데이터가 없습니다.");
                        }
                        conn.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
            }
            return spool;

        }
        /// <summary>
        /// DDWorks Database에서 Pipe에 뚫린 Take off 홀 사이즈들을 리스트로 반환한다.
        /// </summary>
        public List<string> Get_TakeOff_Size_By_InstanceId(string instanceID)
        {
            List<string> holeSize_Li = new List<string>();
            string sql = string.Format(
                        "SELECT PI.INSTANCE_ID, DIAMETER1, PO.OWNER_INSTANCE_ID FROM TB_PIPEINSTANCES PI INNER JOIN TB_POCINSTANCES as PO "+
                        "WHERE  round(PI.POSX) = round(PO.POSX) "+
                        "AND round(PI.POSY) = round(PO.POSY) "+
                        "AND round(PI.POSZ) = round(PO.POSZ) "+
                        "AND Pi.PIPE_TYPE = '17301768' AND PO.CONNECTION_ORDER > 1 AND hex(PO.OWNER_INSTANCE_ID) like '{0}';", instanceID);
            string connstr = "Data Source=" + db_path;

            try
            {
                if (db_path != "")
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        SQLiteCommand comm = new SQLiteCommand(sql, conn);
                        SQLiteDataReader rdr = comm.ExecuteReader();
                        if (rdr.HasRows) //rdr 반환값이 있을때만 Read
                        {
                           // rdr.Read();
                            while (rdr.Read())
                            {

                                holeSize_Li.Add(rdr["DIAMETER1"].ToString());
                                
                            }
                        }
                            conn.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
            }
            return holeSize_Li;
        }
        /// <summary>
        /// DDWorks Database에서 PipeInstance의 방향정보인 POSX,POSY,POSZ,Radian값을 반환한다. 
        /// </summary>
        //public List<string> Get_TakeOff_Vector(string instanceID)
        //{
        //    List<string> spool = new List<string>();
        //    string sql = string.Format(
        //                "SELECT PI.INSTANCE_ID, DIAMETER1, PO.OWNER_INSTANCE_ID FROM TB_PIPEINSTANCES PI INNER JOIN TB_POCINSTANCES as PO " +
        //                "WHERE  round(PI.POSX) = round(PO.POSX) " +
        //                "AND round(PI.POSY) = round(PO.POSY) " +
        //                "AND round(PI.POSZ) = round(PO.POSZ) " +
        //                "AND Pi.PIPE_TYPE = '17301768' AND PO.CONNECTION_ORDER > 1 AND hex(PO.OWNER_INSTANCE_ID) like '{0}';", instanceID);
        //    string connstr = "Data Source=" + db_path;

        //    try
        //    {
        //        if (db_path != "")
        //        {
        //            using (SQLiteConnection conn = new SQLiteConnection(connstr))
        //            {
        //                conn.Open();
        //                SQLiteCommand comm = new SQLiteCommand(sql, conn);
        //                SQLiteDataReader rdr = comm.ExecuteReader();
        //                if (rdr.HasRows) //rdr 반환값이 있을때만 Read
        //                {
        //                    rdr.Read();
        //                    spool.Add(rdr["DIAMETER1"].ToString());
        //                }
        //                conn.Dispose();
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //    }
        //    return spool;
        //}
        public List<string> Get_TakeOff_XYZRAngle_By_InstanceId(string  pipeInstanceID)
        {
            //리스트의 값은 POSX,POSY,POSZ,RADIAN 값 4개가 1개의 POC 정보
            List<string> xyzrAngle = new List<string>();
            //PipeInstance에 Takeoff가 있는 ConnOrder가 0,1이상인 POC가 있는 파이프크기가 큰 순서대로만 반환.
            string sql = string.Format(
                        "SELECT * FROM TB_POCINSTANCES as PI " +
                        "JOIN TB_PIPESIZE as PS " +
                        "on PI.PIPESIZE_ID = PS.PIPESIZE_ID " +
                        "WHERE hex(OWNER_INSTANCE_ID) like '{0}' AND " +
                        "CONNECTION_ORDER > 1 ORDER BY PS.OUTERDIAMETER DESC;", pipeInstanceID);
            string connstr = "Data Source=" + db_path;
            using (SQLiteConnection conn = new SQLiteConnection(connstr))
            {
                conn.Open();
                SQLiteCommand comm = new SQLiteCommand(sql, conn);
                SQLiteDataReader rdr = comm.ExecuteReader();
                if (rdr.HasRows) //rdr 반환값이 있을때만 Read
                {
                    rdr.Read();
                    xyzrAngle.Add(rdr["POSX"].ToString());
                    xyzrAngle.Add(rdr["POSY"].ToString());
                    xyzrAngle.Add(rdr["POSZ"].ToString());
                    xyzrAngle.Add(rdr["RADIAN"].ToString());
                    
                }
                conn.Dispose();
            }
            return xyzrAngle;
        }
        /// <summary>
        /// 파이프의 두께를 반환(Pipe Out과 Inner 두께를 뺀 값) 
        /// </summary>
        /// <param name="pipeInstanceID"></param>
        /// <returns></returns>
        public double Get_PipeThickness_By_InstanceId(string pipeInstanceID)
        {
            double thk = 0;

            //string sql = String.Format("SELECT INNERDIAMETER,OUTERDIAMETER FROM TB_POCINSTANCES as PI " +
            //    "INNER JOIN TB_PIPESIZE as PS ON PI.PIPESIZE_ID " +
            //    "= PS.PIPESIZE_ID WHERE PI.OWNER_INSTANCE_ID = x'{0}';", pipeInstanceID);

            // 24.7.16 ThickessID테이블 추가 후 SQL구문 업데이트
            string sql = String.Format("SELECT (OUTERDIAMETER-INNERDIAMETER),THICKNESS FROM TB_POCINSTANCES as PI " +
                "INNER JOIN TB_PIPESIZE as PS ON PI.PIPESIZE_ID = PS.PIPESIZE_ID " +
                "INNER JOIN TB_RULE_THICKNESS as TH ON PI.PIPESIZE_ID = TH.PIPESIZE_ID AND PI.MATERIAL_ID = TH.MATERIAL_ID " +
                "WHERE PI.OWNER_INSTANCE_ID=x'{0}';",pipeInstanceID);

            using (SQLiteConnection conn = new SQLiteConnection(connstr))
            {
                conn.Open();
                SQLiteCommand comm = new SQLiteCommand(sql, conn);

                try
                {
                    SQLiteDataReader rdr = comm.ExecuteReader();
                    if (rdr.HasRows)
                    {
                        rdr.Read();
                        thk = (double)rdr["THICKNESS"];
                    }
                }
                catch(Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
                finally
                {
                    conn.Close();
                }
            }
                return thk;
        }
        public double Get_PipeThk_SubOutInnerDia_By_InstanceId(string pipeInstanceID)
        {
            double thk = 0;

            string sql = String.Format("SELECT INNERDIAMETER,OUTERDIAMETER FROM TB_POCINSTANCES as PI " +
                "INNER JOIN TB_PIPESIZE as PS ON PI.PIPESIZE_ID " +
                "= PS.PIPESIZE_ID WHERE PI.OWNER_INSTANCE_ID = x'{0}';", pipeInstanceID);

            using (SQLiteConnection conn = new SQLiteConnection(connstr))
            {
                conn.Open();
                SQLiteCommand comm = new SQLiteCommand(sql, conn);
                SQLiteDataReader rdr = comm.ExecuteReader();
                if (rdr.HasRows)
                {
                    rdr.Read();
                    thk = (double)rdr["OUTERDIAMETER"] - (double)rdr["INNERDIAMETER"];
                }
            }
            return thk;
        }
    }
}
