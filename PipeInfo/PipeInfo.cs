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
               
                var db_pipeInfo = new Database_Get_PipeInfo(ed, db);
                var pipe_instanceIDs = db_pipeInfo.get_selection_To_PipeInstacesId(db_path, prSelRes, pointCollection);
            }
            else
            {
                DB_Path_Winform win = new DB_Path_Winform();
                win.DataSendEvent += new DataGetEventHandler(this.DataGet);
                win.Show();
            }
        }

        //델리게이트로 이벤트 연결
        public void DataGet(string data)
        {
            db_path = data;
        }


    }
    public class Pipe
    {
        //파이프의 벡터 방향을 가져온다.
        public (string[], double[]) getPipeVector(Vector3d vector, Polyline3d obj)
        {
            /*Pipe 진행방향(Line)은 삽입값이나 Elbow값이 포함되어 있음. 그래서 DB에서 정확한 비교가 어렵다. 그래서 진행방향이 아닌 나머지 두 축으로 비교.
             *0,90,180,270,360도만 적용(직각이 아닌 라인들은 다시 지정)
              X축 이면 Y축과 Z축
              Y축 이면 X축과 Z축
              Z축 이면 X축과 Z축*/
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
    }
    public class InteractivePolyLine
    {
        /// <summary>Collects the points interactively, Temporarily joining vertexes.</summary>
        /// <returns>Point3dCollection</returns>
        public static Point3dCollection CollectPointsInteractive()
        {
            Document Active = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Point3dCollection pointCollection = new Point3dCollection();
            Color color = Active.Database.Cecolor;
            PromptPointOptions pointOptions = new PromptPointOptions("\n 첫 번째 점: ")
            {
                AllowNone = true
            };
            // Active Document에서 Fence시작 포인트를 가져온다. 
            PromptPointResult pointResult = Active.Editor.GetPoint(pointOptions);

            while (pointResult.Status == PromptStatus.OK)
            {
                pointCollection.Add(pointResult.Value);
                // Select subsequent points
                pointOptions.UseBasePoint = true;
                pointOptions.BasePoint = pointResult.Value;
                pointResult = Active.Editor.GetPoint(pointOptions);

                if (pointResult.Status == PromptStatus.OK)
                {
                    // Draw a temporary segment
                    Active.Editor.DrawVector(
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
    public class DrawHuText
    {
        public DrawHuText(Transaction acTrans, BlockTableRecord acBlkRec, Point3d final_Point,int textDisBetween, int textSize, int oblique, int Rotate)
        {
            /* acTrans : 
               acBlkRec : 
               final_Point : 
               textDisBetween : 
               textSize : 작업자 설정 필요
               oblique : 배관 그룹의 Vector에 때라 조정 필요
               Rotate : 배관 그룹의 Vector에 때라 조정 필요 */

            for (int i = 0; i < 3; i++)
            {
                DBText acText = new DBText();
                //acText.SetDatabaseDefaults();
                acText.Normal = Vector3d.ZAxis;
                //acText.Position = Point3d.Origin;
                acText.HorizontalMode = (TextHorizontalMode)(int)TextHorizontalMode.TextRight;
                acText.TextString = "13A_PN2_T3703_CA05_002";
                //AlignmentPoint로 수정하니 됨.(Text 기준을 오른쪽으로 맞추면 원점으로 이동하는 현상발생함)
                acText.AlignmentPoint = final_Point;
                acText.Rotation = Math.PI / 180 * Rotate;
                acText.Oblique = Math.PI / 180 * oblique;
                //acText.AlignmentPoint = new Point3d(final_Point);
                var id = acBlkRec.AppendEntity(acText);
                acTrans.AddNewlyCreatedDBObject(acText, true);
            }
        }
        public bool TextForLayerName(SelectionSet ss, Vector3d vec)
        {
            bool res = false;
            return res;
        }
    }
    public class Database_Get_PipeInfo
    {
        private string db_TB_PIPEINSTANCES = "TB_PIPEINSTANCES";
        private string db_TB_POCINSTANCES = "TB_POCINSTANCES";
        private Editor db_ed;
        private Database db_acDB;
        public Database_Get_PipeInfo(Editor ed, Database db)
        {
            this.db_ed = ed;
            this.db_acDB = db;
        }
        public string[] get_selection_To_PipeInstacesId(string db_path, PromptSelectionResult prSelRes, Point3dCollection pointCollection)
        {
            Pipe pi = new Pipe();
            string[] ids = {};
            //선택한 객체가 존재할때만 명령 실행.
            if (prSelRes.Status == PromptStatus.OK)
            {
                //객체를 가져오는 순서. PromptSelectionResult -> SelectionSet -> ObjectIds
                SelectionSet ss = prSelRes.Value;
                ObjectId[] obIds = ss.GetObjectIds();
                List<Point3d> final_Point = new List<Point3d>();
                
                //Fence Select 의 마지막 Point를 기준으로 Text
                foreach (Point3d point in pointCollection)
                {
                    final_Point.Add(point);
                }

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
                            foreach (var obid in obIds)
                            {
                                //PolyLine3d 로 형변환. 
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
                                    //BitConverter에 '-'하이픈 Replace로 제거. 
                                    db_ed.WriteMessage("인스턴스 ID : {0} {1}\n", rdr["POSX"], BitConverter.ToString((byte[])rdr["INSTANCE_ID"]).Replace("-", ""));
                                    string comm = String.Format("SELECT * FROM {0} WHERE hex(INSTANCE_ID) = {1}", db_TB_PIPEINSTANCES, rdr["INSTANCE_ID"]);
                                    rdr.Close();
                                }
                                else
                                {
                                    MessageBox.Show("데이터가 없습니다.");
                                }

                            }
                        }
                    }
                    acTrans.Commit();
                }
            }
            else
            {
                db_ed.WriteMessage("라인을 선택하세요");
            }
            return ids;
        }
 
    }
}
