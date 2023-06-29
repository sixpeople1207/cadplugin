using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipeInfo
{
    internal class Contral
    {
        public class Database_Get_PipeInfo
        {
            private string db_path = "";
            private string db_TB_PIPEINSTANCES = "TB_PIPEINSTANCES";
            //private string db_TB_POCINSTANCES = "TB_POCINSTANCES";
            private Editor db_ed;
            private Database db_acDB;

            public Database_Get_PipeInfo(Editor ed, Database db, string acDB_path)
            {
                db_ed = ed;
                db_acDB = db;
                db_path = acDB_path;
            }
            public List<string> db_Get_Pipes_InstanceIDs(PromptSelectionResult prSelRes, Point3dCollection pointCollection)
            {
                Pipe pi = new Pipe(db_ed, db_acDB);
                List<string> ids = new List<string>();
                //선택한 객체가 존재할때만 명령 실행.
                if (prSelRes.Status == PromptStatus.OK)
                {
                    //객체를 가져오는 순서. PromptSelectionResult -> SelectionSet -> ObjectIds
                    SelectionSet ss = prSelRes.Value;
                    ObjectId[] obIds = ss.GetObjectIds();

                    using (Transaction acTrans = db_acDB.TransactionManager.StartTransaction())
                    {
                        ObjectId[] oId = { };
                        BlockTable acBlk;
                        acBlk = acTrans.GetObject(db_acDB.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord acBlkRec;
                        acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                        /* 최초 사용자가 원하는 치수 Vector
                         + + + : 오른쪽 상단
                         - + + : 왼쪽 상단 
                         - - + : 오른쪽 하단 
                         + - + : 왼쪽 하단 */
                        if (db_path != null)
                        {
                            string connstr = "Data Source=" + db_path;
                            using (SQLiteConnection conn = new SQLiteConnection(connstr))
                            {
                                conn.Open();
                                //오브젝트 ID를 이용해서 객체의 정보를 가져온다. 배관의 순서를 위해 배관이 놓인 순서필요.
                                //파이프의 백터 필요.
                                if (obIds.Length > 0)
                                {
                                    foreach (var obid in obIds)
                                    {
                                        //PolyLine3d 로 형변환. 
                                        var objd = acTrans.GetObject(obid, OpenMode.ForWrite);
                                        if (objd.ObjectId.ObjectClass.GetRuntimeType() == typeof(Polyline3d))
                                        {
                                            db_ed.WriteMessage("라인객체" + objd.ObjectId.ObjectClass.ToString());
                                            Polyline3d obj = (Polyline3d)acTrans.GetObject(obid, OpenMode.ForWrite);
                                            //Line의 Vec방향.
                                            Vector3d vec = obj.StartPoint.GetVectorTo(obj.EndPoint).GetNormal();

                                            //DB Select문에 사용할 Line Vector에 따른 Obj방향설정. 진행되는 Vector는 비교하지 않음.
                                            (string[] db_column_name, double[] line_trans) = pi.getPipeVector(vec, obj);

                                            if (db_column_name[0] != "")
                                            {
                                                //DB TB_PIPINSTANCES에서 POS에서 CAD Line좌표를 빼준 리스트에서 가장 상위 객체의 INSTANCE_ID를 가져온다.
                                                //배관 좌표에서 가장 근접한 값을 가져오기 위해 DB좌표와 CAD 좌표를 뺀 값 중 가장 작은 값을 상위에 위치 시키고, 추가로 Length값도 비교. 
                                                string sql = String.Format("SELECT *,abs({0}-{3}) as disposx, abs({1}-{4}) as disposz ,abs({2}-LENGTH1) as distance FROM {5} ORDER by disposx,disposz,distance ASC;",
                                                                Math.Round(line_trans[0], 2),//CAD소숫점은 2자리정도로 비교 
                                                                Math.Round(line_trans[1], 2),
                                                                obj.Length, db_column_name[0],
                                                                db_column_name[1],
                                                                db_TB_PIPEINSTANCES);
                                                SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                                                SQLiteDataReader rdr = cmd.ExecuteReader();
                                                if (rdr.HasRows)
                                                {
                                                    //Read를 한번만 실행해서 내림차순의 가장 상위 객체를 가져온다.
                                                    rdr.Read();
                                                    string bitToStr_Instance_Id = BitConverter.ToString((byte[])rdr["INSTANCE_ID"]).Replace("-", "");
                                                    ids.Add(bitToStr_Instance_Id);
                                                    //BitConverter에 '-'하이픈 Replace로 제거. 
                                                    db_ed.WriteMessage("인스턴스 ID : {0} {1}\n", rdr["POSX"], bitToStr_Instance_Id);
                                                    string comm = String.Format("SELECT * FROM {0} WHERE hex(INSTANCE_ID) = {1}", db_TB_PIPEINSTANCES, rdr["INSTANCE_ID"]);
                                                    rdr.Close();
                                                }
                                                else
                                                {
                                                    MessageBox.Show("Error : 해당 배관에 대한 데이터가 없습니다.");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        acTrans.Commit();
                    }
                }
                else
                {
                    db_ed.WriteMessage("Error : 선택한 객체가 없습니다. 배관라인을 선택하세요.");
                }
                return ids;
            }
            public List<Tuple<string, string>> db_Get_Pipes_Production_Infomation(List<string> pipe_InstanceIDS)
            {
                List<Tuple<string, string>> production_Info = new List<Tuple<string, string>>();
                using (Transaction acTrans = db_acDB.TransactionManager.StartTransaction())
                {
                    string db_COL_Production_Group_NM = "PRODUCTION_DRAWING_GROUP_NM";
                    string db_COL_Production_Group_ID = "PRODUCTION_DRAWING_GROUP_ID";
                    string db_TB_PRODUCTION_GROUP = "TB_PRODUCTION_DRAWING_GROUPS";
                    string db_TB_PIPEINSTANCES = "TB_PIPEINSTANCES";
                    string db_TB_PRODUCTION_DRAWING = "TB_PRODUCTION_DRAWING";
                    string db_COL_SPOOLNUM = "SPOOL_NUMBER";
                    string db_COL_UTILITY_NM = "UTILITY_NM";
                    string db_TB_UTILITIES = "TB_UTILITIES";

                    string[] sql_li = { "", "", "", "" };
                    BlockTable acBlk;
                    acBlk = acTrans.GetObject(db_acDB.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec;
                    acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    if (db_path != null)
                    {
                        string connstr = "Data Source=" + db_path;
                        using (SQLiteConnection conn = new SQLiteConnection(connstr))
                        {
                            conn.Open();
                            foreach (var obj in pipe_InstanceIDS)
                            {
                                //스풀이름
                                sql_li[0] = String.Format("SELECT {0} " +
                                "FROM {1} " +
                                "WHERE {2} = " +
                                "(SELECT {3} " +
                                "FROM {4} " +
                                "INNER JOIN {5} " +
                                "ON " +
                                "{6}.INSTANCE_ID = " +
                                "{7}.INSTANCE_ID AND " +
                                "hex({8}.INSTANCE_ID) = '{9}');",
                                db_COL_Production_Group_NM,
                                db_TB_PRODUCTION_GROUP,
                                db_COL_Production_Group_ID,
                                db_COL_Production_Group_ID,
                                db_TB_PIPEINSTANCES,
                                db_TB_PRODUCTION_DRAWING,
                                db_TB_PIPEINSTANCES,
                                db_TB_PRODUCTION_DRAWING,
                                db_TB_PIPEINSTANCES,
                                obj.ToString()
                                   );

                                //유틸이름
                                sql_li[1] = String.Format(
                                "SELECT {0} " +
                                "from {1} " +
                                "INNER JOIN " +
                                "{2} " +
                                "ON " +
                                "{3}.UTILITY_ID = " +
                                "{4}.UTILITY_ID " +
                                "AND " +
                                "hex({5}.INSTANCE_ID) = '{6}';",
                                db_COL_UTILITY_NM, db_TB_UTILITIES,
                                db_TB_PIPEINSTANCES,
                                db_TB_UTILITIES,
                                db_TB_PIPEINSTANCES,
                                db_TB_PIPEINSTANCES,
                                obj.ToString()
                                );

                                sql_li[2] = String.Format(
                                    "SELECT {0} " +
                                    "FROM {1} " +
                                    "WHERE hex(INSTANCE_ID) = '{2}';",
                                    db_COL_SPOOLNUM, db_TB_PRODUCTION_DRAWING, obj.ToString());

                                //쿼리문 실행
                                SQLiteCommand comm = new SQLiteCommand(sql_li[0], conn);
                                SQLiteDataReader reader = comm.ExecuteReader();
                                string str_pipe_Info = "";

                                while (reader.Read())
                                {
                                    str_pipe_Info += reader[0].ToString();
                                }

                                reader.Close();
                                comm = new SQLiteCommand(sql_li[1], conn);
                                reader = comm.ExecuteReader();
                                while (reader.Read())
                                {
                                    str_pipe_Info += "_" + reader[0].ToString();
                                }

                                reader.Close();
                                comm = new SQLiteCommand(sql_li[2], conn);
                                reader = comm.ExecuteReader();
                                while (reader.Read())
                                {
                                    str_pipe_Info += "_" + reader[0].ToString();
                                }
                                production_Info.Add(new Tuple<string, string>(obj, str_pipe_Info));
                            }
                            conn.Close();
                        }
                    }
                }
                return production_Info;
            }
            public (List<Point3d>, List<string>) db_Get_Final_POC_Instance_IDS()
            {
                // 마지막 찾은 Owner ID에서 객체 타입이 모델이라면 PIPE를 다시 찾는다. 
                List<string> final_objs_ID = new List<string>(); //db_Get_Pipes_Production_Infomation 와 연동필요
                List<Point3d> final_objs_Pos = new List<Point3d>();

                if (db_path != "")
                {
                    string connstr = "Data Source=" + db_path;
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        string pipe_key = "256";
                        string component_key = "768";
                        List<string> final_Poc_Instance_Ids = new List<string>();

                        string final_poc_key = String.Format("SELECT * From TB_POCINSTANCES WHERE HEX(CONNECTED_POC_ID) = '{0}';", "00000000000000000000000000000000");

                        SQLiteCommand comm = new SQLiteCommand(final_poc_key, conn);
                        SQLiteDataReader rdr = comm.ExecuteReader();
                        while (rdr.Read())
                        {
                            //TB_POCINSTANCE Column Number 0 : "INSTANCE_ID"
                            //TB_POCINSTANCE Column Number 19 : "CONNECTED_POC_ID"v
                            //TB_POCINSTANCE Column Number 21 : "CONNECTION_ORDER"
                            if (rdr["OWNER_TYPE"].ToString() == pipe_key)
                            {
                                final_objs_ID.Add(BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", ""));
                                Point3d fin_pos = new Point3d((double)rdr["POSX"], (double)rdr["POSY"], (double)rdr["POSZ"]);
                                final_objs_Pos.Add(fin_pos);
                            }
                            else if (rdr["OWNER_TYPE"].ToString() == component_key)
                            {
                                //Connected POC Owner 갯수 구하기.
                                //각 POC마다 전객체를 봐서 PIPE인것을 골라내서 정보가져옴.
                                string prev_id = db_POC_prev(connstr, BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", ""), "INSTANCE_ID");
                                final_objs_ID.Add(prev_id);
                                //반환값이 Pipe Key인지 확인필요.
                                Point3d fin_pos = new Point3d((double)rdr["POSX"], (double)rdr["POSY"], (double)rdr["POSZ"]);
                                final_objs_Pos.Add(fin_pos);
                            }
                            else
                            {
                                db_ed.WriteMessage("테이블 값이 다릅니다.");
                            }

                        }
                        rdr.Close();
                        conn.Close();
                    }

                    foreach (var final_obj in final_objs_Pos)
                    {
                        db_ed.WriteMessage("마지막객체: " + final_obj.ToString() + "\n");
                    }
                }
                return (final_objs_Pos, final_objs_ID);
            }
            public string db_POC_prev(string connstr, string current_POC, string column_name)
            {
                //Owner ID를 입력받아 CONNECTION ODER 값이 0에 연결된 InstanceID(column_name)를 하나 넘겨준다.
                //ID 0에 해당하는 INSTANCE ID를 찾는다.
                //option은 rdr[column_name]으로 열을 지정해서 반환값을 정할 수 있다.
                //
                string prev_poc_id = "";

                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    string sql = String.Format("SELECT * FROM TB_POCINSTANCES WHERE INSTANCE_ID = " +
                    "(SELECT CONNECTED_POC_ID FROM TB_POCINSTANCES WHERE hex(OWNER_INSTANCE_ID) like " +
                    "'{0}' and CONNECTION_ORDER = 0)", current_POC);

                    conn.Open();
                    SQLiteCommand comm = new SQLiteCommand(sql, conn);
                    SQLiteDataReader rdr = comm.ExecuteReader();
                    while (rdr.Read())
                    {
                        db_ed.WriteMessage("오너 아이디" + BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", ""));
                        prev_poc_id = BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", "");
                    }
                    conn.Close();
                }
                return prev_poc_id;
            }
            public string db_POC_next(string current_POC)
            {
                //Owner ID가 1에 연결된 InstanceID를 하나 넘겨준다. 
                string next_poc_id = "";
                return next_poc_id;
            }
            public List<string> db_Get_Pipes_Production_Information_Points(List<Point3d> pipe_points)
            {
                List<string> li = new List<string>();
                //연결된 파이프 정보가 두개가 되어야 한다. 연결되었는지 확인하는 메서드. 23.6.26
                //파이프 정보를 차례로 반환한다. 두개가 한쌍. 듀플? 딕션너리. 
                //각 포인트 마다 찾아 256인 객체만 검색. 두 개 일 수 있음. 
                //파이프 마다 그룹을 지정해야함. 번호별로 하면 될 것 같음. 
                string prev_poc_id = "";
                string connstr = db_path;
                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    conn.Open();

                    foreach (var point in pipe_points)
                    {
                        string sql = String.Format("SELECT * FROM TB_POCINSTANCES WHERE POSX = {0} AND POSY = {1} AND POSZ = {2}", point.X, point.Y, point.Z);
                        SQLiteCommand comm = new SQLiteCommand(sql, conn);
                        SQLiteDataReader rdr = comm.ExecuteReader();

                    }

                    conn.Close();
                }
                return li;
            }
            public List<Point3d> db_Get_POC_Instance_IDS_Position(List<string> final_POC_IDS)
            {
                List<Point3d> fianl_obj_pos = new List<Point3d>();
                return fianl_obj_pos;
            }
            //관련 메소드 : select_Welding_Point 
            public List<Tuple<int, Point3d>> db_FilterWeldGroup_By_ComponentType(Tuple<int, Point3d> weldGroup, string filter)
            {
                List<Tuple<int, Point3d>> dd = new List<Tuple<int, Point3d>>();
                return dd;
            }
        }
    }
}
