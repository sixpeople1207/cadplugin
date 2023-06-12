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

namespace PipeInfo
{
    public class PipeInfo
    {
        string db_path="";

        [CommandMethod("fence")]
        public void selectFence()
        {
            if (db_path == "")
            {
                DB_Path_Winform win = new DB_Path_Winform();
                win.DataSendEvent += new DataGetEventHandler(this.DataGet);
                win.Show();
            }
            else
            {
                Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;
                List<Point3d> final_Point = new List<Point3d>();

                //클릭할 좌표점을 계속해서 입력받아 3D Collection으로 반환
                Point3dCollection pointCollection = InteractivePolyLine.CollectPointsInteractive();
                ObjectId id;
                PromptSelectionResult prSelRes = ed.SelectFence(pointCollection);
                SelectionSet ss = prSelRes.Value;
                ObjectId[] obIds = ss.GetObjectIds();

                //string strConn = @"Data Source=D:\프로젝트_제작도면\도면\DINNO 요청 DB (1)\DKG3705\DInno.HU3D.db";
                //string strConn = @"Data Source=C:\Users\sixpe\Downloads\DKG3705\DInno.HU3D.db";

                foreach (Point3d point in pointCollection)
                {
                    final_Point.Add(point);
                }

                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    ObjectId[] oId = new ObjectId[1];

                    BlockTable acBlk;
                    acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
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
                          
                            //오브젝트 ID를 이용해서 객체의 정보를 가져온다.
                            //파이프의 백터 필요.
                            foreach (var obid in obIds)
                            {
                                var obj = (Polyline3d)acTrans.GetObject(obid, OpenMode.ForWrite);
                                ed.WriteMessage($"시작좌표 : {obj.StartPoint}");
                                Vector3d vec = obj.EndPoint.GetVectorTo(obj.StartPoint);
                                ed.WriteMessage($"\n벡터 : {vec.GetNormal()}");

                                string sql = String.Format("SELECT * FROM TB_PIPEINSTANCES WHERE POSX = {0} AND POSZ = {1}", Math.Round(obj.StartPoint.X,2),Math.Round(obj.StartPoint.Z,2));
                                SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                                SQLiteDataReader rdr = cmd.ExecuteReader();

                                while (rdr.Read())
                                {
                                    ed.WriteMessage("{0}", rdr["INSTANCE_ID"]);
                                }
                            }
                        }
                    }
                    acTrans.Commit();
                }
            }
        }

        //델리게이트로 이벤트 연결
        public void DataGet(string data)
        {
            db_path = data;
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
            PromptPointOptions pointOptions = new PromptPointOptions("\n첫 번째 점: ")
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
    public class ConnectDatabase
    {
        public void init()
        {

        }  
    }
}
