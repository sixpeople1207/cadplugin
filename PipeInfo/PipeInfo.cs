using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Customization;
using System.Data.SQLite;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using System.Drawing.Text;
using System.Data.Entity;
using Database = Autodesk.AutoCAD.DatabaseServices.Database;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using System.Data.Entity.ModelConfiguration.Configuration;
using System.Runtime.CompilerServices;
using System.IO.Pipes;
using System.Security.Cryptography;
using static System.Windows.Forms.LinkLabel;
using Autodesk.AutoCAD.Internal;

namespace PipeInfo
{
    public class PipeInfo
    {
        public string db_path ="";
        [CommandMethod("fence")] 
        public void selectFence()
        {
            if (db_path != "")
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;

                //클릭할 좌표점을 계속해서 입력받아 3D Collection으로 반환
                Point3dCollection pointCollection = InteractivePolyLine.CollectPointsInteractive();
                PromptSelectionResult prSelRes = ed.SelectFence(pointCollection);
                
                if(prSelRes.Status == PromptStatus.OK)
                {
                using(Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockTable blk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord blkRec = acTrans.GetObject(blk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    Polyline3d line = new Polyline3d(Poly3dType.SimplePoly, pointCollection, false);
                    foreach(var point in pointCollection)
                    {
                        ed.WriteMessage(point.ToString());
                    }
                    blkRec.AppendEntity(line);
                    acTrans.AddNewlyCreatedDBObject(line, true);
                    acTrans.Commit();
                }

                /*-----------------------------------------DataBase Scope--------------------------------------------------
                 * 1. DB객체 생성 [O]
                 * 2. OBJ IDs (Fence to Selection return objIds) [O]
                 * 3. OBJ IDs to DB_Information(튜플로 DB InstanceID 적용) [O]
                 * 4. acDrawText 기능 구현.(3번에서 반환된 튜플 객체를 Fence EndPoint 에서 부터 시작해서 객체를 생성) [ㅁ]
                 * 5. OBJ IDs To Connected PipeInformation 구현 예정(OBJ IDs를 입력하면 전 후 PipeInformation). []
                 ----------------------------------------------------------------------------------------------------------*/
                var pipeInfo_cls = new Database_Get_PipeInfo(ed, db, db_path);
                List<string> pipe_instance_IDs = pipeInfo_cls.db_Get_Pipes_InstanceIDs(prSelRes, pointCollection);
                List<Tuple<string,string>> pipe_Information_li = pipeInfo_cls.db_Get_Pipes_Production_Infomation(pipe_instance_IDs);
                ed.WriteMessage(pipe_Information_li[0].Item1);

                /*-------------------------------------------Editor Scope----------------------------------------------------
                 * 1. Prev 객체를 넣을건지 Next객체를 표현할지 입력. []
                 * 2. End Pipe 객체를 넣을 건지 옵션. []
                 * 3. Valve 객체를 찾아서 길이를 줄이기. []
                 * 4. Text를 그리는 기능(라인 포함) []
                 * 5. 배관 Group의 Vector를 파악.  [] -> 6.15
                 * 6. ICON 과 버튼 적용.  []
                 * 7. SetUp 파일.  []
                 * 8. Get Two Point 내부에 Text 객체내용 가져오기. []
                 * 9. Text내용을 Excel로 Export하기. []
                 ----------------------------------------------------------------------------------------------------------*/

                List<Point3d> final_Point = new List<Point3d>();
                //Fence Select 의 마지막 Point를 기준으로 Text
                foreach (Point3d point in pointCollection)
                {
                    final_Point.Add(point);
                }

                var draw_Text = new DrawText(ed, db);
                var pipe = new Pipe(ed,db);
                //배관의 Vector와 마지막 객체의 좌표도 필요. 좌표를 기준으로 Fence 좌표를 보정.
                var pipe_Group_Vector = pipe.get_Pipe_Group_Vector(prSelRes);
                draw_Text.ed_Draw_Text(pipe_Information_li, final_Point, 25, 12);
            }
                else
                {
                    ed.WriteMessage("선택된 객체가 없습니다.");
                }
            }
            else
            {
                DB_Path_Winform win = new DB_Path_Winform();
                win.DataSendEvent += new DataGetEventHandler(this.DataGet);//데이터가 들어가면 실행.
                win.Show();
            }
        }
        //델리게이트함수 DataGetEventHandler로 DataGet함수 주소를 보내고 콜백함수 등록.
        public void DataGet(string data)
        {
            db_path = data;
        }


    }
    public class Pipe
    {
        Editor ed;
        Database db;

        public Pipe(Editor acEd, Database acDB)
        {
            ed = acEd;
            db = acDB;
        }

        //파이프의 벡터 방향을 가져온다.
        public (string[], double[]) getPipeVector(Vector3d vector, Polyline3d obj)
        {
            /* Pipe 진행방향(Line)은 삽입값이나 Elbow값이 포함되어 있음. 그래서 DB에서 정확한 비교가 어렵다. 그래서 진행방향이 아닌 나머지 두 축으로 비교.
             * 0,90,180,270,360도만 적용(직각이 아닌 라인들은 다시 지정)
               X축 이면 Y축과 Z축
               Y축 이면 X축과 Z축
               Z축 이면 X축과 Z축 */

            string[] db_column_name = { "", "" };
            double[] line_trans = { 0.0, 0.0 };
            if (vector.GetNormal().X == 1 || vector.GetNormal().X == -1)
            {
                db_column_name[0] = "POSY";
                db_column_name[1] = "POSZ";
                line_trans[0] = obj.StartPoint.Y;
                line_trans[1] = obj.StartPoint.Z;
            }
            if (vector.GetNormal().Y == 1 || vector.GetNormal().Y == -1)
            {
                db_column_name[0] = "POSX";
                db_column_name[1] = "POSZ";
                line_trans[0] = obj.StartPoint.X;
                line_trans[1] = obj.StartPoint.Z;
            }
            if (vector.GetNormal().Z == 1 || vector.GetNormal().Z == -1)
            {
                db_column_name[0] = "POSX";
                db_column_name[1] = "POSY";
                line_trans[0] = obj.StartPoint.X;
                line_trans[1] = obj.StartPoint.Y;
            }
            return (db_column_name, line_trans);
        }
        public Vector3d get_Pipe_Group_Vector(PromptSelectionResult prSelRes)
        {
            using(Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                SelectionSet ss = prSelRes.Value;
                ObjectId[] obIds = ss.GetObjectIds();
                BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkRec;
                List<Vector3d> lines_Vec = new List<Vector3d>();
                List<Point3d> lines_Point = new List<Point3d>();

                acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                foreach(var obid in obIds)
                {
                    //객체 타입 확인
                    var objd = acTrans.GetObject(obid, OpenMode.ForWrite);
                    if (objd.ObjectId.ObjectClass.GetRuntimeType() == typeof(Polyline3d))
                    {
                        //객체 타입이 Polyline이면 형변환해서 좌표 사용.
                        Polyline3d li = (Polyline3d)acTrans.GetObject(obid, OpenMode.ForWrite);
                        Vector3d vec = li.StartPoint.GetVectorTo(li.EndPoint).GetNormal();
                        lines_Point.Add(li.StartPoint);
                        lines_Vec.Add(vec);
                    }
                    else
                    {
                    }
                }
                
                foreach(var line_vec in lines_Vec)
                {
                    ed.WriteMessage(line_vec.ToString());
                }
                
                acTrans.Commit();
            }
            return vec;
        }
    }
    public class InteractivePolyLine
    {
        /// <summary>Collects the points interactively, Temporarily joining vertexes.</summary>
        /// <returns>Point3dCollection</returns>
        public static Point3dCollection CollectPointsInteractive()
        {
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Point3dCollection pointCollection = new Point3dCollection();
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Color color = acDoc.Database.Cecolor;
            PromptPointOptions pointOptions = new PromptPointOptions("\n 첫 번째 점: ")
            {
                AllowNone = true
            };

            // acDoc Document에서 Fence시작 포인트를 가져온다. 
                PromptPointResult pointResult = acDoc.Editor.GetPoint(pointOptions);

            while (pointResult.Status == PromptStatus.OK)
            {
                pointCollection.Add(pointResult.Value);

                //6.13 추후 실시간 선택되는것 활성화(블루) 시각화 기능 추가
                //PromptSelectionResult pSr = ed.SelectImplied();
                //SelectionSet ss = pSr.Value;

                // Select subsequent points
                pointOptions.UseBasePoint = true;
                pointOptions.BasePoint = pointResult.Value;
                pointResult = acDoc.Editor.GetPoint(pointOptions);
                
                if (pointResult.Status == PromptStatus.OK)
                {
                    // Draw a temporary segment
                      acDoc.Editor.DrawVector(
                      pointCollection[pointCollection.Count - 1], // start point
                      pointResult.Value,          // end point
                      4,
                      true);                     // highlighted?
                                    
                  }
            }
            if (pointResult.Status == PromptStatus.None)
            {
                return pointCollection;
            }
            else
            {
                return new Point3dCollection();
            }
        }
    }
    public class DrawText
    {
        private Editor ed;
        private Database db;
        public DrawText(Editor aced, Database acdb)
        {
            /* acTrans : 
               acBlkRec : 
               final_Point : 
               textDisBetween : 
               textSize : 작업자 설정 필요
               oblique : 배관 그룹의 Vector에 때라 조정 필요
               Rotate : 배관 그룹의 Vector에 때라 조정 필요 */
            ed = aced;
            db = acdb;
        }
        public void ed_Draw_Text(List<Tuple<string, string>> pipe_Information_li, List<Point3d> final_Points, int textDisBetween, int textSize)
        {
            using (Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                BlockTable edBLK = acTrans.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                BlockTableRecord edBLKrec = acTrans.GetObject(edBLK[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                var final_Point = new Point3d();
                // View Port 방향에 따라서 Isometric에 따라 Text 3D Angle각도,Rotate값 변경.
                string view_Name = "";

                using (var view = ed.GetCurrentView())
                {
                    view_Name = GetViewName(view.ViewDirection);
                }

                for (int idx = 0; idx < pipe_Information_li.Count; idx++)
                {
                    DBText acText = new DBText();
                 
                    //Text Init
                    acText.HorizontalMode = (TextHorizontalMode)(int)TextHorizontalMode.TextRight;
                    acText.TextString = pipe_Information_li[idx].Item2;
                    Vector3d final_Points_Vec = (final_Points[final_Points.Count - 1] - final_Points[0]).GetNormal();
                    int text_3d_Angle = 0;
                    int text_oblique = 0;
                    int text_Rotate = 0;

                    //Text Set Rotate
                    if(view_Name == "NW Isometric")
                    {
                        text_3d_Angle = -90;
                        text_oblique = 0;
                        text_Rotate = 270;
                    }
                    else if (view_Name == "NE Isometric")
                    {
                        text_3d_Angle = 90;
                        text_oblique = 0;
                        text_Rotate = 360;
                    }
                    else if(view_Name == "SW Isometric")
                    {

                    }
                    else if (view_Name == "SE Isometric")
                    {

                    }
                    else
                    {

                    }
                    acText.Height = textSize;
                    acText.Rotation = Math.PI / 180 * text_Rotate;
                    acText.Oblique = Math.PI / 180 * text_oblique;

                    //텍스트 지시선 벡터의 마지막 포인트에 따라 Pipe Spool 정보를 배치한다. -> 추후 두개의 스풀 정보를 넣는 왼쪽 오른쪽 알고리즘이 필요.
                    if (final_Points_Vec.Z == 1 || final_Points_Vec.Z == -1)
                    {
                        acText.Normal = Vector3d.ZAxis;
                        acText.TransformBy(Matrix3d.Rotation(Math.PI / 180 * text_3d_Angle, Vector3d.YAxis, Point3d.Origin));
                        acText.Justify = AttachmentPoint.BaseLeft;
                        //AlignmentPoint로 수정.(Text 기준을 오른쪽으로 맞추면 원점으로 이동하는 현상발생함)
                        final_Point = new Point3d(final_Points[final_Points.Count-1].X, final_Points[final_Points.Count - 1].Y, final_Points[final_Points.Count - 1].Z-(textDisBetween * idx));
                    }
                    else if ((final_Points_Vec.X == 1 || final_Points_Vec.X == -1) || (final_Points_Vec.X == 1 || final_Points_Vec.X == -1))
                    {
                        acText.Normal = Vector3d.YAxis;
                        acText.TransformBy(Matrix3d.Rotation(Math.PI / 4, Vector3d.ZAxis, Point3d.Origin));
                        //AlignmentPoint로 수정.(Text 기준을 오른쪽으로 맞추면 원점으로 이동하는 현상발생함)
                        final_Point = new Point3d(final_Points[final_Points.Count - 1].X, final_Points[final_Points.Count - 1].Y, final_Points[final_Points.Count - 1].Z - (textDisBetween * idx));
                    }
                    else
                    {
                        ed.WriteMessage("기준 라인을 다시 그려주시길 바랍니다.");
                    }

                    acText.AlignmentPoint = final_Point; 
                    edBLKrec.AppendEntity(acText);
                    acTrans.AddNewlyCreatedDBObject(acText, true);
                }
                acTrans.Commit(); 
            }
        }
        public string GetViewName(Vector3d viewDirection)
        {
            //Vector 값 가져오는 알고리즘 참고. sqprt033.. 
            double sqrt033 = Math.Sqrt(1.0 / 3.0);
            switch (viewDirection.GetNormal())
            {
                case Vector3d v when v.IsEqualTo(Vector3d.ZAxis): return "Top";
                case Vector3d v when v.IsEqualTo(Vector3d.ZAxis.Negate()): return "Bottom";
                case Vector3d v when v.IsEqualTo(Vector3d.XAxis): return "Right";
                case Vector3d v when v.IsEqualTo(Vector3d.XAxis.Negate()): return "Left";
                case Vector3d v when v.IsEqualTo(Vector3d.YAxis): return "Back";
                case Vector3d v when v.IsEqualTo(Vector3d.YAxis.Negate()): return "Front";
                case Vector3d v when v.IsEqualTo(new Vector3d(sqrt033, sqrt033, sqrt033)): return "NE Isometric";
                case Vector3d v when v.IsEqualTo(new Vector3d(-sqrt033, sqrt033, sqrt033)): return "NW Isometric";
                case Vector3d v when v.IsEqualTo(new Vector3d(-sqrt033, -sqrt033, sqrt033)): return "SW Isometric";
                case Vector3d v when v.IsEqualTo(new Vector3d(sqrt033, -sqrt033, sqrt033)): return "SE Isometric";
                default: return $"Custom View";
            }
        }
    }
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
            Pipe pi = new Pipe(db_ed,db_acDB);
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
                         if(obIds.Length > 0)
                            {
                            foreach (var obid in obIds)
                            {
                                //PolyLine3d 로 형변환. 
                                var objd = acTrans.GetObject(obid, OpenMode.ForWrite);
                                if(objd.ObjectId.ObjectClass.GetRuntimeType() == typeof(Polyline3d))
                                {
                                    db_ed.WriteMessage("라인객체"+objd.ObjectId.ObjectClass.ToString());
                                    Polyline3d obj = (Polyline3d)acTrans.GetObject(obid, OpenMode.ForWrite);
                                    //Line의 Vec방향.
                                    Vector3d vec = obj.StartPoint.GetVectorTo(obj.EndPoint).GetNormal();

                                    //DB Select문에 사용할 Line Vector에 따른 Obj방향설정. 진행되는 Vector는 비교하지 않음.
                                    (string[] db_column_name, double[] line_trans) = pi.getPipeVector(vec, obj);

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
            List<Tuple<string,string>> production_Info = new List<Tuple<string, string>>();
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

                string[] sql_li = {"","","",""};
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
                        foreach(var obj in pipe_InstanceIDS)
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
                            db_COL_UTILITY_NM,db_TB_UTILITIES,
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
                                db_COL_SPOOLNUM,db_TB_PRODUCTION_DRAWING,obj.ToString());

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
                                str_pipe_Info += "_"+reader[0].ToString();
                            }
                            production_Info.Add(new Tuple<string,string>(obj, str_pipe_Info));
                        }
                    }
                }
            }
        return production_Info;
        }

    }
}
