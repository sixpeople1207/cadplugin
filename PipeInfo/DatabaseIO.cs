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

namespace PipeInfo
{
    class DatabaseIO
    {
        private string db_path = "";
        private string ownerType_Component = "768"; //TB_POCINSTANCES:OWNER_TYPE 기자재.
        private string ownerType_Pipe = "256"; //TB_POCINSTANCES:OWNER_TYPE 파이프.
        private string pipeInsType_Pipe = "17301760"; //
        public DatabaseIO()
        {
        }
        public DatabaseIO(string acDB_path)
        {
            db_path = acDB_path;
        }
      
        public List<string> get_DB_GroupList()
        {
            List<String> groupList = new List<string>();
            if(db_path != "")
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
        /// DDWorks Database에서 파이프 인스턴스 정보를 불러온다.(배열 정보(0~4):인스턴스아이디,파이프사이즈,재질,길이,파이프타입(Takeoff인지))
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
                        string sql = string.Format("SELECT DISTINCT PO.OWNER_INSTANCE_ID ,PS.OUTERDIAMETER, PIPESIZE_NM, MATERIAL_NM ,LENGTH1, PO.CONNECTION_ORDER From TB_INSTANCEGROUPMEMBERS as GM " +
                                "INNER JOIN TB_PIPEINSTANCES as PI " +
                                    "ON PI.INSTANCE_ID = GM.INSTANCE_ID " +
                                      "AND GM.INSTANCE_GROUP_ID = " +
                                        "(SELECT INSTANCE_GROUP_ID From TB_INSTANCEGROUPS " +
                                            "WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM = '{0}') " +
                                "INNER JOIN TB_POCINSTANCES as PO  " +
                                    "ON PI.INSTANCE_ID = PO.OWNER_INSTANCE_ID AND PI.PIPE_TYPE= '{1}' " +
                                "INNER JOIN TB_PIPESIZE as PS  " +
                                    "ON PO.PIPESIZE_ID = PS.PIPESIZE_ID " +
                                "INNER JOIN TB_MATERIALS as MG " +
                                    "ON PO.MATERIAL_ID = MG.MATERIAL_ID;",groupName, pipeInsType_Pipe);
                        conn.Open();
                        if (sql != "")
                        {
                            SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                            while (rdr_ready.Read())
                            {
                                if (rdr_ready["CONNECTION_ORDER"].ToString() != "1") //그리드뷰에서 POC1개의 정보만 보여주기 위해 1개는 걸러냄.
                                {
                                    string instanceId = BitConverter.ToString((byte[])rdr_ready["OWNER_INSTANCE_ID"]).Replace("-", "");
                                    string legnth = Math.Round((double)rdr_ready["LENGTH1"],1).ToString();
                                    string hole = "-";
                                    string pipeSize = "";
                                    string material_Nm = "";
                                  
                                    // Take Off 객체인지 리스트에 표시.
                                    string connect_id = "";
                                    //float dia = (float)rdr_ready["OUTERDIAMETER"];
                                    
                                    Double outDia = Double.Parse(rdr_ready["OUTERDIAMETER"].ToString());
                                
                                        connect_id = rdr_ready["CONNECTION_ORDER"].ToString();
                                        pipeSize = rdr_ready["PIPESIZE_NM"].ToString();
                                        material_Nm = rdr_ready["MATERIAL_NM"].ToString();
                                    
                                   

                                    //TakeOff객체인지 걸러내기 위해 Connect Id중에 0과 1은 파이프의 POC이고 2이상은 TakeOff임.
                                    if (connect_id != "0" && connect_id != "1")
                                    {
                                        hole="Hole";
                                        legnth = "0"; //Hole은 길이값 없어서 길이값 수정.
                                    }
                                    else
                                    {
                                        hole ="Pipe";
                                    }

                                    if ((outDia > 25.4 && hole=="Pipe") || hole == "Hole") //Pipe인데 25.4이거나 Hole만 그리드 뷰에 적는다.
                                    {
                                        pipeInsInfo.Add(instanceId);
                                        pipeInsInfo.Add(pipeSize);
                                        pipeInsInfo.Add(material_Nm);
                                        pipeInsInfo.Add(legnth);
                                        pipeInsInfo.Add(hole);
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
    }
}
