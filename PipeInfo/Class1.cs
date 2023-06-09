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

namespace PipeInfo
{
    public class Class1
    {

        [CommandMethod("fence")]
        public void selectFence()
        {
            
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            List<Point3d> final_Point = new List<Point3d>();

            //클릭할 좌표점을 계속해서 입력받아 3D Collection으로 반환
            Point3dCollection pointCollection = InteractivePolyLine.CollectPointsInteractive();
            ObjectId id;
            PromptSelectionResult prSelRes = ed.SelectFence(pointCollection);
            string strConn = @"Data Source=D:\프로젝트_제작도면\도면\DINNO 요청 DB (1)\DKG3705\DInno.HU3D.db";

            using (SQLiteConnection conn = new SQLiteConnection(strConn))
            {
                conn.Open();
                string sql = "SELECT * FROM ";
                SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DELETE FROM member WHERE Id=1";
                cmd.ExecuteNonQuery();
                ed.WriteMessage("DB연결");
            }
            foreach (Point3d point in pointCollection)
            {
                final_Point.Add(point);
            }

            using (Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                //SelectionSet ss = prSelRes.Value;
                ObjectId[] oId = new ObjectId[1];
                //if (ss != null)
                //    ed.WriteMessage("\nThe SS is good and has {0} entities.", ss.GetObjectIds());
                //else
                //    ed.WriteMessage("\nThe SS is bad!");
                //foreach (SelectedObject s in ss)
                //{
                //ed.WriteMessage(s.ObjectId.ToString());
                //oId.Append(s.ObjectId);
                //ed.WriteMessage(s.ObjectId.ToString());
                //}
                BlockTable acBlk;
                acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkRec;
                acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                /* 최초 사용자가 원하는 치수 Vector
                 + + + : 오른쪽 상단
                 - + + : 왼쪽 상단 
                 - - + : 오른쪽 하단 
                 + - + : 왼쪽 하단 */

                Vector3d vec = final_Point[1] - final_Point[0];
                ed.WriteMessage(vec.GetNormal().ToString());

                for (int i = 0; i < 3; i++)
                {
                    DBText acText = new DBText();
                    //acText.SetDatabaseDefaults();
                    acText.Normal = Vector3d.ZAxis;
                    //acText.Position = Point3d.Origin;
                    acText.HorizontalMode = (TextHorizontalMode)(int)TextHorizontalMode.TextRight;
                    acText.TextString = "13A_PN2_T3703_CA05_002";
                    //AlignmentPoint로 수정하니 됨.(Text 기준을 오른쪽으로 맞추면 원점으로 이동하는 현상발생함)
                    acText.AlignmentPoint = new Point3d(final_Point[1].X + (-4 * i), final_Point[1].Y + (2.5 * i), final_Point[1].Z);
                    acText.Rotation = Math.PI / 180 * 30;
                    acText.Oblique = Math.PI / 180 * 330;
                    //acText.AlignmentPoint = new Point3d(final_Point);
                    id = acBlkRec.AppendEntity(acText);
                    acTrans.AddNewlyCreatedDBObject(acText, true);
                }

                acTrans.Commit();


            }

            //Commit후에 Text수정했을때도 안됨.
            //using(Transaction acTrans = db.TransactionManager.StartTransaction())
            //{

            //    BlockTable acBlk;
            //    acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            //    BlockTableRecord acBlkRec;
            //    acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            //    ObjectId[] ids = new ObjectId[] { id }; //한개를 선택하기 위해 배열에 값 하나 집어넣음
            //    var d = Autodesk.AutoCAD.Internal.Utils.SelectObjects(ids);
            //    DBText acEnt = acTrans.GetObject(id, OpenMode.ForWrite) as DBText;
            //    ed.SetImpliedSelection(ids);

            //    acTrans.Commit();
            //}


        }

        [CommandMethod("win")]
        public void winform()
        {
            var win = new DB_Path_Winform();
            win.Show();
        }
    }
    public class InteractivePolyLine
    {
        /// <summary>Collects the points interactively, Temporarily joining vertexes.</summary>
        /// <returns>Point3dCollection</returns>
        public static Point3dCollection CollectPointsInteractive()
        {
            Document Active = Application.DocumentManager.MdiActiveDocument;
            Point3dCollection pointCollection = new Point3dCollection();
            Color color = Active.Database.Cecolor;
            PromptPointOptions pointOptions = new PromptPointOptions("\n첫 번째 점: ")
            {
                AllowNone = true
            };

            // Get the start point
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
                      2,
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
        public DrawHuText(int textDisBetween, int textSize)
        {

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
