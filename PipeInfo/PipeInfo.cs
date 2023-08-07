using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media.Imaging; 
using System.Drawing.Imaging;
using System.Drawing;
using System.Reflection;
using Microsoft.Office.Interop.Excel;

using AutoCAD;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Database = Autodesk.AutoCAD.DatabaseServices.Database;
using Excel = Microsoft.Office.Interop.Excel;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using Autodesk.Windows;
using Autodesk.AutoCAD.ApplicationServices.Core;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Media;
using System.Runtime.InteropServices.ComTypes;
using static Autodesk.AutoCAD.Internal.LayoutContextMenu;
using System.Windows;
using System.Resources;
using MessageBox = System.Windows.Forms.MessageBox;
using System.Windows.Controls;
using Orientation = System.Windows.Controls.Orientation;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Drawing.Printing;
using System.Linq.Expressions;

[assembly: ExtensionApplication(typeof(PipeInfo.App))]
[assembly: CommandClass(typeof(PipeInfo.PipeInfo))]

namespace PipeInfo
{
    public class PipeInfo
    {
        //여기에 CLASS 맴버를 현재 ISO방향, 
        //Valve 연결된 파이프 줄이기 기능
        //db정보
        //등 중복되는 정보들을 하나로 통합하기. 
        //나중에는 Handle과 Spool정보와 길이를 맵핑한다. 
        //추가할 기능은 같은 레벨에 있는 텍스트를 선택하기 정도.. 
        public string db_path = "";
        public void DataGet(string data)
        {
            db_path = data;
        }
        //   [CommandMethod("ff")]
        public void selectFence()
        {
            if (db_path != "")
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;
                // 메뉴 만들기
                //Autodesk.Windows.RibbonControl ribbonControl
                //    = Autodesk.Windows.ComponentManager.Ribbon;
                //RibbonTab Tab = new RibbonTab();

                //Tab.Title = "Test Ribbon";
                //Tab.Id = "TESTRIBBON_TAB_ID";
                //ribbonControl.Tabs.Add(Tab);

                //클릭할 좌표점을 계속해서 입력받아 3D Collection으로 반환
                Point3dCollection pointCollection = InteractivePolyLine.CollectPointsInteractive();
                PromptSelectionResult prSelRes = ed.SelectFence(pointCollection);

                if (prSelRes.Status == PromptStatus.OK)
                {
                    using (Transaction acTrans = db.TransactionManager.StartTransaction())
                    {
                        BlockTable blk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord blkRec = acTrans.GetObject(blk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                        Polyline3d line = new Polyline3d(Poly3dType.SimplePoly, pointCollection, false);
                        foreach (var point in pointCollection)
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
                    var pipeInfo_cls = new DDWorks_Database(db_path);
                    List<string> pipe_instance_IDs = pipeInfo_cls.Get_PipeInstanceIDs_By_ObjIDs(prSelRes, pointCollection);
                    List<Tuple<string, string>> pipe_Information_li = pipeInfo_cls.Get_Spool_Infomation_By_ObjIds(pipe_instance_IDs);
                    //(var finale_POC_points,  var final_ids) = pipeInfo_cls.Get_Final_POC_Instance_Ids(); <- 마지막 POC 단 기능
                    //List<Tuple<string, string>> pipe_Information_li_2 = pipeInfo_cls.Get_Spool_Infomation_By_ObjIds(final_ids); <- 마지막 POC 단 기능
                    //ed.WriteMessage(pipe_Information_li_2[0].Item1); <- 마지막 POC 단 기능

                    /*-------------------------------------------Editor Scope----------------------------------------------------
                     * 1. Prev 객체를 넣을건지 Next객체를 표현할지 입력. []
                     * 2. End Pipe 객체를 넣을 건지 옵션. []
                     * 3. Valve 객체를 찾아서 길이를 줄이기. []
                     * 4. Text를 그리는 기능(라인 포함) []
                     * 5. 배관 Group의 Vector를 파악.  [] -> 6.15
                     * 6. ICON 과 버튼 적용.  []
                     * 7. SetUp 파일.  []^
                     * 8. Get Two Point 내부에 Text 객체내용 가져오기. []
                     * 9. Text내용을 Excel로 Export하기. []
                     ----------------------------------------------------------------------------------------------------------*/

                    List<Point3d> final_Point = new List<Point3d>();
                    //Fence Select 의 마지막 Point를 기준으로 Text
                    foreach (Point3d point in pointCollection)
                    {
                        final_Point.Add(point);
                    }

                    var draw_Text = new TextControl();
                    var pipe = new Pipe();
                    //배관의 Vector와 마지막 객체의 좌표도 필요. 좌표를 기준으로 Fence 좌표를 보정.
                    var pipe_Group_Vector = pipe.get_Pipe_Group_Vector(prSelRes);
                    draw_Text.ed_Draw_Text_To_Line_Vector(pipe_Information_li, final_Point, 25, 12);
                    // draw_Text.ed_Draw_Text(pipe_Information_li_2, finale_POC_points, 25, 12); <- 마지막 POC 단 글씨
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

        /* 함수 이름 : selectBlock_ExportToInnerInformation
         * 기능 설명 : SPOOL도곽 선택, BL도곽 선택, 도곽내 글씨정보 모두 추출. Excel Export기능 등.
         * 명 령 어 : BB
         */
        public void zoomAll()
        {
            AcadApplication app = (AcadApplication)Application.AcadApplication;
            app.ZoomExtents();
        }
        [CommandMethod("bb")]
        public void pipeBetween_Distance()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            Point3dCollection pointCollection = InteractivePolyLine.CollectPointsInteractive();
            PromptSelectionResult prSelRes = ed.SelectFence(pointCollection);

            if (prSelRes.Status == PromptStatus.OK)
            {
                var pipe = new Pipe();
                List<Polyline3d> linePoints = pipe.get_PolyLinePoints_By_PromptSelectResult(prSelRes);
                if (linePoints.Count > 1)
                {
                    List<Vector3d> groupVec = pipe.get_Pipe_SpoolGroup_Vector(linePoints);
                    pipe.set_Distance_By_Pipe_between(groupVec, linePoints, pointCollection);
                }
                else
                {
                    ed.WriteMessage("\nError : 두개 이상의 파이프를 선택해 주세요.");
                }
                //blkRec.AppendEntity(line);
                //acTrans.AddNewlyCreatedDBObject(line, true);
            }

        }

        //도면내 도곽내 MES정보와 용접포인트 번호를 가져온다.e
        [CommandMethod("ee")]
        public void selectBlock_ExportToInnerInformation()
        {
            try
            {

                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    //Zoom(new Point3d(db.Limmin.X, db.Limmin.Y, 0),
                    //new Point3d(db.Limmax.X, db.Limmax.Y, 0),
                    //new Point3d(), 1);
                    // db.UpdateExt(true);
                    //Extents3d ext = (short)Application.GetSystemVariable("cvport") == 1 ? new Extents3d(db.Pextmin, db.Pextmax) :
                    //   new Extents3d(db.Extmin, db.Extmax);
                    //ViewTableRecord view = ed.GetCurrentView();
                    //Point2d min = new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
                    //Point2d max = new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y);
                    //view.CenterPoint = min;
                    //view.Height = ext.MinPoint.Y-ext.MaxPoint.Y;
                    //view.Width = ext.MinPoint.X-ext.MaxPoint.Z;
                    //ed.SetCurrentView(view);
                    //ed.WriteMessage(ext.MaxPoint.Y.ToString());
                    zoomAll();
                    PromptResult res = ed.GetString("화면에 도곽정보가 모두 있습니까?(Y or N):");
                    if (res.Status == PromptStatus.OK)
                    {
                        if (res.StringResult.ToString() == "y" || res.StringResult.ToString() == "Y")
                        {
                            PromptSelectionResult ss = ed.SelectAll();
                            //DBObjectCollection allObjec = acTrans.GetAllObjects();

                            string sheetName = "SPOOL_도곽";
                            string titleBoardName = "BL22";

                            if (ss.Status == PromptStatus.OK)
                            {
                                SelectionSet sSet = ss.Value;
                                ObjectId[] oId = sSet.GetObjectIds();
                                List<Extents3d> sheetPosLi = new List<Extents3d>();
                                List<Extents3d> titleBoardPosLi = new List<Extents3d>();
                                List<string> weldNumber = new List<string>();
                                List<Point3d> cirPosLi = new List<Point3d>();
                                TypedValue[] typeValue = { new TypedValue(0, "TEXT,CIRCLE") };
                                List<DBText> textAllLi = new List<DBText>();
                                SelectionFilter selFilter = new SelectionFilter(typeValue);
                                ExcelObject excel = new ExcelObject();
                                Compare comparePoint = new Compare();
                                foreach (var id in oId)
                                {
                                    Entity en = acTrans.GetObject(id, OpenMode.ForRead) as Entity;
                                    if (en.GetType().Name.ToString() == "BlockReference")
                                    {
                                        // 시트 번호와 타이틀 
                                        BlockReference blk = en as BlockReference;
                                        if (blk.Name.ToString() == sheetName)
                                        {
                                            sheetPosLi.Add(en.Bounds.Value);
                                        }
                                        else if (blk.Name.ToString() == titleBoardName)
                                        {
                                            titleBoardPosLi.Add(en.Bounds.Value);
                                        }
                                    }
                                    else if (en.GetType().Name.ToString() == "DBText")
                                    {
                                        DBText te = en as DBText;
                                        if (te.Layer.ToString().Contains("Infomation_Welding_Number"))
                                        {
                                            textAllLi.Add(te);
                                        }
                                    }
                                    else if (en.GetType().Name.ToString() == "Circle")
                                    {
                                        Circle cir = en as Circle;
                                        if (en.Layer.ToString().Contains("Infomation_Welding_Number"))
                                        {
                                            cirPosLi.Add(cir.Center);
                                        }
                                    }
                                }

                                List<DBText> titleBoard_textAllLi = new List<DBText>();
                                //전체 시트 포지션리스트에서 시트별 구역의 Text정보를 가져온다.
                                foreach ((var title, var i) in titleBoardPosLi.Select((value, i) => (value, i)))
                                {
                                    List<DBText> titleBoard_textLi = new List<DBText>();
                                    PromptSelectionResult selWin = ed.SelectCrossingWindow(title.MinPoint, title.MaxPoint, selFilter, false);
                                    if (selWin.Status == PromptStatus.OK)
                                    {
                                        SelectionSet selSetWin = selWin.Value;
                                        ObjectId[] sheetInSelObIds = selSetWin.GetObjectIds();
                                        foreach (ObjectId sId in sheetInSelObIds)
                                        {
                                            DBText te = acTrans.GetObject(sId, OpenMode.ForRead) as DBText;
                                            titleBoard_textLi.Add(te);
                                            titleBoard_textAllLi.Add(te);
                                        }
                                    }
                                }
                                // 기능 이름 : 시트 구역별 용접 번호 가져오기
                                // 구현 순서 : 
                                // 1. select all
                                // 2. Infomation_Welding_Number Layer에 있는 원과 Text정보를 개별 List에 저장.
                                // 3. 두 리스트를 비교하여 Circle안에 Text위치가 있는 정보만 다른 리스트에 저장.
                                // 5. Sheet Number와 TitleBoardNumber위치가 일치하는지 확인.
                                // 6. 일치하면 용접번호와 TitleBoard정보를 엑셀에 쓴다.

                                List<DBText> weld_Numbers_li = new List<DBText>();
                                int weld_Rows_Count = 0;
                                int board_index = 0;
                                int current_Row_Num = 0;
                                bool isRow_Insert = false;
                                int[] columns_2 = { 4, 7, 2, 6, 8, 3, 9, 5, 1 };
                                // Circle위치에 해당하는 Text객체만 가져온다. 용접포인트에 해당하는 용접번호.
                                foreach (var cir in cirPosLi)
                                {
                                    foreach (var te in textAllLi)
                                    {
                                        double deltaDis = cir.DistanceTo(te.Position);
                                        if (deltaDis < 3)
                                        {
                                            weld_Numbers_li.Add(te);
                                        }
                                    }
                                }
                                List<string> textLiStr = new List<string>();
                                List<int> startToEnd_li = new List<int>();
                                //List<Extents3d> sheetPosLiDistinct = sheetPosLi.Distinct().ToList();
                                //전체 TEXT위치에서 Sheet 위치에 해당하는 Text만 차례대로 옉셀에 쓰기를 진행한다. 
                                foreach (var sheet in sheetPosLi)
                                {
                                    //표제란 정보를 한번만 쓰고, 나머지는 Rows를 추가해 빈공간을 만든다.
                                    isRow_Insert = true;
                                    foreach ((var te, var j) in weld_Numbers_li.Select((value, j) => (value, j)))
                                    {
                                        bool is_inOut = comparePoint.isInside_boundary(te.Position, sheet.MinPoint, sheet.MaxPoint);
                                        if (is_inOut) { excel.excel_InsertData(current_Row_Num, 10, te.TextString, isRow_Insert); weld_Rows_Count++; current_Row_Num++; }
                                    }
                                    //행 추가기능 멈춤.
                                    isRow_Insert = false;
                                    //만약 도곽내에 웰딩 포인트가 없다면 엑셀 내에서 한줄밑에 써준다.
                                    if (weld_Rows_Count == 0)
                                    {
                                        current_Row_Num++;
                                    }
                                    //도곽내에 있는 표제란정보가 도곽 영역내부에 있는지 판단.
                                    foreach (var board_Text in titleBoard_textAllLi)
                                    {
                                        bool is_inOut = comparePoint.isInside_boundary(board_Text.Position, sheet.MinPoint, sheet.MaxPoint);
                                        //현재행에서 웰딩포인트 갯수를 빼주면 첫 행의 번호를 알 수 있다.
                                        if (is_inOut) { excel.excel_InsertData(current_Row_Num - weld_Rows_Count, columns_2[board_index], board_Text.TextString, isRow_Insert); board_index++; }
                                        if (board_index > 8) { board_index = 0; }
                                    }
                                    //웰딩 포인트를 발견한다면 표제란의 시작행번호와 끝 행번호를 리스트에 저장한다.
                                    if (weld_Rows_Count > 0)
                                    {
                                        startToEnd_li.Add(current_Row_Num - weld_Rows_Count);
                                        startToEnd_li.Add(current_Row_Num);
                                    }
                                    //다음 도곽정보에서 웰딩포이트 카운트를 위해 초기화
                                    weld_Rows_Count = 0;
                                }

                                //도곽을 모두 순회완료하고나서 엑셀 표제란에 빈공간을 채워준다. 표제란 영역복사 붙여넣기.
                                for (int k = 1; k < startToEnd_li.Count; k += 2)
                                {
                                    excel.excel_CopyTo_StartEnd(startToEnd_li[k - 1], startToEnd_li[k]);
                                }
                                excel.excel_save();
                                //}
                            }
                            acTrans.Commit();
                            acTrans.Dispose();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.
                Editor.WriteMessage(ex.Message);
            }
        }
        static void Zoom(Point3d pMin, Point3d pMax, Point3d pCenter, double dFactor)
        {
            // Get the current document and database
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;
            int nCurVport = System.Convert.ToInt32(Application.GetSystemVariable("CVPORT"));
            // Get the extents of the current space no points
            // or only a center point is provided
            // Check to see if Model space is current
            if (acCurDb.TileMode == true)
            {
                if (pMin.Equals(new Point3d()) == true &&
               pMax.Equals(new Point3d()) == true)
                {
                    pMin = acCurDb.Extmin;
                    pMax = acCurDb.Extmax;
                }
            }
            else
            {
                // Check to see if Paper space is current
                if (nCurVport == 1)
                {
                    // Get the extents of Paper space
                    if (pMin.Equals(new Point3d()) == true &&
                    pMax.Equals(new Point3d()) == true)
                    {
                        pMin = acCurDb.Pextmin;
                        pMax = acCurDb.Pextmax;
                    }
                }
                else
                {
                    // Get the extents of Model space
                    if (pMin.Equals(new Point3d()) == true &&
                    pMax.Equals(new Point3d()) == true)
                    {
                        pMin = acCurDb.Extmin;
                        pMax = acCurDb.Extmax;
                    }
                }
            }
            // Start a transaction
            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                // Get the current view
                using (ViewTableRecord acView = acDoc.Editor.GetCurrentView())
                {
                    Extents3d eExtents;
                    // Translate WCS coordinates to DCS
                    Matrix3d matWCS2DCS;
                    matWCS2DCS = Matrix3d.PlaneToWorld(acView.ViewDirection);
                    matWCS2DCS = Matrix3d.Displacement(acView.Target - Point3d.Origin) *
                    matWCS2DCS;
                    matWCS2DCS = Matrix3d.Rotation(-acView.ViewTwist,
                    acView.ViewDirection,
                    acView.Target) * matWCS2DCS;
                    // If a center point is specified, define the min and max
                    // point of the extents
                    // for Center and Scale modes
                    if (pCenter.DistanceTo(Point3d.Origin) != 0)
                    {
                        pMin = new Point3d(pCenter.X - (acView.Width / 2),
                        pCenter.Y - (acView.Height / 2), 0);
                        pMax = new Point3d((acView.Width / 2) + pCenter.X,
                        (acView.Height / 2) + pCenter.Y, 0);
                    }
                    // Create an extents object using a line
                    using (Line acLine = new Line(pMin, pMax))
                    {
                        eExtents = new Extents3d(acLine.Bounds.Value.MinPoint,
                        acLine.Bounds.Value.MaxPoint);
                    }
                    // Calculate the ratio between the width and height of the current view
                    double dViewRatio;
                    dViewRatio = (acView.Width / acView.Height);
                    // Tranform the extents of the view
                    matWCS2DCS = matWCS2DCS.Inverse();
                    eExtents.TransformBy(matWCS2DCS);
                    double dWidth;
                    double dHeight;
                    Point2d pNewCentPt;
                    // Check to see if a center point was provided (Center and Scale modes)
                    if (pCenter.DistanceTo(Point3d.Origin) != 0)
                    {
                        dWidth = acView.Width;
                        dHeight = acView.Height;
                        if (dFactor == 0)
                        {
                            pCenter = pCenter.TransformBy(matWCS2DCS);
                        }
                        pNewCentPt = new Point2d(pCenter.X, pCenter.Y);
                    }
                    else // Working in Window, Extents and Limits mode
                    {
                        // Calculate the new width and height of the current view
                        dWidth = eExtents.MaxPoint.X - eExtents.MinPoint.X;
                        dHeight = eExtents.MaxPoint.Y - eExtents.MinPoint.Y;
                        // Get the center of the view
                        pNewCentPt = new Point2d(((eExtents.MaxPoint.X +
                        eExtents.MinPoint.X) * 0.5),
                        ((eExtents.MaxPoint.Y +
                        eExtents.MinPoint.Y) * 0.5));
                    }
                    // Check to see if the new width fits in current window
                    if (dWidth > (dHeight * dViewRatio)) dHeight = dWidth / dViewRatio;
                    // Resize and scale the view
                    if (dFactor != 0)
                    {
                        acView.Height = dHeight * dFactor;
                        acView.Width = dWidth * dFactor;
                    }
                    // Set the center of the view
                    acView.CenterPoint = pNewCentPt;
                    // Set the current view
                    acDoc.Editor.SetCurrentView(acView);
                }
                // Commit the changes
                acTrans.Commit();
            }
        }
        static Dictionary<ObjectId, string> GetAllObjects(Database db)
        {
            var dict = new Dictionary<ObjectId, string>();
            for (long i = 0; i < db.Handseed.Value; i++)
            {
                if (db.TryGetObjectId(new Handle(i), out ObjectId id))
                    dict.Add(id, id.ObjectClass.Name);
            }
            return dict;
        }
        static List<ObjectId> GetallObjectIds()
        {
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            List<ObjectId> objIdAll = new List<ObjectId>();

            using (Transaction acTrans = db.TransactionManager.StartTransaction())
            {

                BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                foreach (var blk in acBlk)
                {
                    var en = (BlockTable)acTrans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (var id in en)
                    {
                        var btr = (BlockTableRecord)acTrans.GetObject(id, OpenMode.ForRead);
                        if (btr.IsLayout)
                        {
                            foreach (var d in btr)
                            {
                                objIdAll.Add(d);
                            }
                        }
                    }
                }
                acTrans.Commit();
            }
            return objIdAll;
        }

        // [CommandMethod("rr")]
        public void draw_rectangle()
        {
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            Editor ed = acDoc.Editor;

            using (Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                BlockTable blk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkRec = acTrans.GetObject(blk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Polyline3d poly = new Polyline3d();

                poly.SetDatabaseDefaults();
                poly.ColorIndex = 5;

                Point3dCollection acP3dCol = new Point3dCollection();
                acP3dCol.Add(new Point3d(0, 0, 0));
                acP3dCol.Add(new Point3d(1, 0, 0));
                acP3dCol.Add(new Point3d(1, 0, 1));
                acP3dCol.Add(new Point3d(0, 0, 1));

                Matrix3d matrix = ed.CurrentUserCoordinateSystem;
                CoordinateSystem3d curUCS = matrix.CoordinateSystem3d;
                poly.Closed = true;
                acBlkRec.AppendEntity(poly);
                acTrans.AddNewlyCreatedDBObject(poly, true);

                foreach (Point3d acPoint in acP3dCol)
                {
                    PolylineVertex3d acPoly3d = new PolylineVertex3d(acPoint);
                    poly.AppendVertex(acPoly3d);
                    acTrans.AddNewlyCreatedDBObject(acPoly3d, true);
                }
                poly.TransformBy(Matrix3d.Rotation(0.5236, curUCS.Zaxis, new Point3d(0, 0, 0)));

                ProgressMeter pm = new ProgressMeter();

                pm.Start("Long process");

                pm.SetLimit(100);

                try

                {

                    //start a long process

                    for (int i = 0; i < 100; i++)

                    {

                        //did user press ESCAPE?

                        if (HostApplicationServices.Current.UserBreak())

                            throw new Autodesk.AutoCAD.Runtime.Exception(

                               Autodesk.AutoCAD.Runtime.ErrorStatus.UserBreak, "ESCAPE pressed");

                        //update progress bar

                        pm.MeterProgress();

                        //delay 10 miliseconds

                        System.Threading.Thread.Sleep(10);

                    }

                }

                catch (System.Exception ex)

                {

                    //some error

                    Application.DocumentManager.MdiActiveDocument.

                        Editor.WriteMessage(ex.Message);

                }

                finally

                {

                    pm.Stop();

                }
                acTrans.Commit();
            }
        }
        /* 함수 이름 : select_Welding_Point
         * 기능 설명 : 현재 View방향, Pipe Vector, PipeGroup Vector, DrawText, TextDirection, TextAliments(등간격 배치)
         * 명 령 어 : ss
         */
        [CommandMethod("ss")]
        public void select_Welding_Point()
        {
            //스풀 경계부분의 정보를 가져와 도면상에 보여준다.
            try
            {
                if (db_path != "")
                {
                    Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                    Document acDoc = Application.DocumentManager.MdiActiveDocument;
                    Database db = acDoc.Database;
                    TextControl tControl = new TextControl();
                    ed.WriteMessage("\nWeldPoint들을 선택해주세요.(CrossingWindow).");
                    List<Point3d> pFaceMeshPoints = Select.selectPolyFaceMeshToPoints();
                    if (pFaceMeshPoints.Count > 0)
                    {
                        string[] filter = { "Tee", "Reducer", "Reducing" };
                        var ddworks_Database = new DDWorks_Database(db_path);
                        // -> 필터 적용. 7.24 등등
                        // 선택한 용접포인트의 위치가 필터에 해당한다면 Point를 삭제.
                        List<Point3d> weldPoints = ddworks_Database.FilterWeldGroup_By_ComponentType(pFaceMeshPoints, filter);
                        if (weldPoints.Count > 0)
                        {
                            Vector3d groupVec = new Vector3d(0, 0, 0);
                            // 23.6.23 함수 추가 Get_Pipe_Vector_By_SpoolList와 거의 동일.. 조금 수정해야할 것 같다. 함수안에 함수로. Get_Pipe_Info하고 -> Vector, Spool, WELD맞대기좌표추가한 리스트 반환기능 등
                            List<Vector3d> vec = ddworks_Database.Get_Pipe_Vector_By_Points(weldPoints);
                            (List<Point3d> orderPoints, string groupVecstr) = Points.orderWeldPoints_By_GroupVector(weldPoints, vec);
                            // SpoolTexts Base PolyLine
                            // weldPoints.의 중간지점
                            double averX = weldPoints.Average(p => p.X);
                            double averY = weldPoints.Average(p => p.Y);
                            double averZ = weldPoints.Average(p => p.Z);
                            Point3d averPoint = new Point3d(averX, averY, averZ);
                            string commandLine = String.Format("{0},{1},{2}", averPoint.X, averPoint.Y, averPoint.Z);
                            //Cmd5(averPoint);
                            ed.WriteMessage("Spool정보 기준라인을 그려주세요.");
                            ed.Command("_.LINE", commandLine);
                            //    Point3dCollection spoolLines = InteractivePolyLine.CollectPointsInteractive();
                            //       if (spoolLines.Count > 0)
                            //{
                            //   Point3d spoolFinalPoint = spoolLines[spoolLines.Count];
                            bool isSpoolLine = false;
                            Line li = new Line();
                            // Cmd5(averPoint);
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                //예외처리 필요 Line이 
                                Entity ent = (Entity)tr.GetObject(Utils.EntLast(), OpenMode.ForRead);
                                Type type = ent.GetType();
                                if (type.Name.ToString() == "Line")
                                {
                                    li = (Line)ent;
                                    isSpoolLine = true;
                                }
                                else
                                {
                                    ed.WriteMessage("error : 마지막 객체가 라인이 아닙니다.");
                                }
                                tr.Commit();
                            }
                            if (isSpoolLine == true)
                            {
                                ed.WriteMessage("\n Spool정보 Text 회전 : G , 취소 : Esc");
                                (List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints) = ddworks_Database.Get_Pipe_Vector_By_SpoolList(orderPoints);
                                List<ObjectId> spoolTexts = tControl.Draw_Text_WeldPoints(li, spoolInfo_li, vec_li, newPoints, groupVecstr); // 라인 끝점을 입력받음.
                                keyFilter keyFilter = new keyFilter();
                                //23.7.25
                                //현재 뷰에 따라 디폴트값 정해야함
                                //엔터키로 돌리고 SpoolGroup 방향 기준
                                //스페이스바로 글씨 회전. 배관 진행방향.
                                System.Windows.Forms.Application.AddMessageFilter(keyFilter);
                                while (true)
                                {
                                    //메세지 처리 문제는 나중에 진행.
                                    // Check for user input events
                                    System.Windows.Forms.Application.DoEvents();
                                    // Check whether the filter has set the flag
                                    if (keyFilter.bCanceled == true)
                                    {
                                        break;
                                    }
                                    if (keyFilter.bEntered == true)
                                    {
                                        using (Transaction actras = db.TransactionManager.StartTransaction())
                                        {
                                            foreach (var id in spoolTexts)
                                            {
                                                DBText text = actras.GetObject(id, OpenMode.ForWrite) as DBText;
                                                Point3d pos = text.Position;
                                                Point3d alig = text.AlignmentPoint;
                                                TextHorizontalMode hor = text.HorizontalMode;
                                                text.SetDatabaseDefaults();
                                                // 배관의 Spool Vector에 따라 기준점 바꾸기.
                                                // 라인의 끝점부터 그리기.
                                                if (Math.Round(vec_li[0].GetNormal().X, 1) == 1 || Math.Round(vec_li[0].GetNormal().X, 1) == -1)
                                                {
                                                    text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 90, Vector3d.XAxis, Point3d.Origin));
                                                }
                                                else if (Math.Round(vec_li[0].GetNormal().Y, 1) == 1 || Math.Round(vec_li[0].GetNormal().Y, 1) == -1)
                                                {
                                                    text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 90, Vector3d.YAxis, Point3d.Origin));
                                                }
                                                else if (Math.Round(vec_li[0].GetNormal().Z, 1) == 1 || Math.Round(vec_li[0].GetNormal().Z, 1) == -1)
                                                {
                                                    text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 90, Vector3d.ZAxis, Point3d.Origin));
                                                }
                                                text.Position = pos;
                                                if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                {
                                                    text.AlignmentPoint = alig;
                                                }
                                            }
                                            ed.Regen();
                                            actras.Commit();
                                            // acText.Normal = Vector3d.ZAxis;
                                            // acText.Justify = AttachmentPoint.BaseLeft;
                                            keyFilter.bEntered = false;
                                        }
                                    }
                                }
                                // We're done - remove the message filter
                                System.Windows.Forms.Application.RemoveMessageFilter(keyFilter);
                            }
                            else
                            {
                                ed.WriteMessage("라인이 그려지지 않았습니다.");
                            }
                        }
                        else
                        {
                            ed.WriteMessage("라인이 그려지지 않았습니다.");
                        }
                    }
                    else
                    {
                        ed.WriteMessage("Error : Database에서 객체가 검색되지 않습니다");
                    }
                }
                else
                {
                    DB_Path_Winform win = new DB_Path_Winform();
                    win.DataSendEvent += new DataGetEventHandler(this.DataGet);//데이터가 들어가면 실행.
                    win.Show();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }
        public async void Cmd5(Point3d point)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            HashSet<ObjectId> ids = new HashSet<ObjectId>();
            string commandLine = String.Format("{0},{1},{2}", point.X, point.Y, point.Z);

            ed.Command("_.LINE", commandLine);
            await ed.CommandAsync("_.LINE", Editor.PauseToken);
            while (((string)Application.GetSystemVariable("CMDNAMES")).Contains("LINE"))
            {
                try
                {
                    await ed.CommandAsync(Editor.PauseToken);
                    ids.Add(Autodesk.AutoCAD.Internal.Utils.EntLast());
                }
                catch { break; } // eUserBreak (Cancel) handling
            }

            Database db = HostApplicationServices.WorkingDatabase;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                    ent.ColorIndex = 1;
                }
                tr.Commit();
            }
        }

        /* 함수 이름 : edit_PipeLength_ConnOfValve
         * 기능 설명 : DB(Valve위치, 이름) CAD(연결된 파이프 객체의 중심점, 중심점과 동일한 Text위치, Text값 조정(길이))
         * 명 령 어 : vv
         * 비 고 : 추후 DB에서 정확한 길이를 반환하는 기능개발필요.
         */
        //  [CommandMethod("vv")]
        public void edit_PipeLength_ConnOfValve()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            int pipeOfSubDim = 0;
            int[] valve_Size = { 131, 100, 80, 70, 65 }; //1" , 3/4", 1/2", 3/8", 1/4"
            ed.WriteMessage("Valve 크기 지정(기본값)\n1인치:131mm\n3/4인치:100mm\n1/2인치:80mm\n3/8인치:70mm\n1/4인치:65mm");
            try
            {
                zoomAll();
                PromptResult res = ed.GetString("현재 뷰에 Valve정보가 모두 있습니까?(Y or N):");
                if (res.Status == PromptStatus.OK && pipeOfSubDim == 0)
                {
                    if (res.StringResult.ToString() == "y" || res.StringResult.ToString() == "Y")
                    {
                        if (db_path != "")
                        {

                            //DDWorksDabase 클래스 생성
                            var ddworks_Database = new DDWorks_Database(db_path);
                            //DDWorksDabase 클래스에서 저장할 valve 위치들
                            List<Point3d> valve_Positions = new List<Point3d>();
                            //DDWorksDabase 클래스에서 저장할 valve 이름들
                            List<string> valve_Name = new List<string>();
                            (valve_Positions, valve_Name) = ddworks_Database.Get_Valve_Position_By_DDWDB();
                            //도면상에서 Polyline과 Text정보들을 가져올 TypeValue객체와 SelectionFilter를 생성해 선택할 필터 준비.
                            TypedValue[] acTypValPoly = new TypedValue[1];
                            TypedValue[] acTypText = new TypedValue[1];
                            acTypValPoly.SetValue(new TypedValue((int)DxfCode.Start, "Polyline"), 0);
                            acTypText.SetValue(new TypedValue((int)DxfCode.Start, "Text"), 0);
                            SelectionFilter acSelFtrPoly = new SelectionFilter(acTypValPoly);
                            SelectionFilter acSelFtrText = new SelectionFilter(acTypText);
                            /*알고리즘 순서
                             Data 준비 : valve위치, valve이름 -> 관련함수 Get_Valve_Position_By_DDWDB()
                             1. valve위치에서 ClossingSelection으로 Poly라인을 선택(CAD)
                             2. objectId를 검색해서 파이프의 좌표를 저장한다.
                             3. 파이프의 시작점과 끝점을 통해 중간좌표를 얻는다.
                             4. 중간좌표에 해당하는 TEXT가 있는지 CAD상에서 검색한다. 
                             5. Text발견시에는 DB에서 찾아온 valve이름에서 사이즈를 찾아 사이즈에 맞는 Vavle값을 Text에서 뺀다.*/
                            foreach (var (valve_pos, i) in valve_Positions.Select((value, i) => (value, i)))
                            {
                                // valve주위를 []모양으로 선택. 선택하기전 Zoom All 필수.
                                PromptSelectionResult pre = ed.SelectCrossingWindow(new Point3d(valve_pos.X - 2, valve_pos.Y - 2, valve_pos.Z - 2),
                                new Point3d(valve_pos.X + 2, valve_pos.Y + 2, valve_pos.Z + 2), acSelFtrPoly);
                                // 1. Valve와 연결된 Poly라인에 중간좌표에 해당하는 Text를 찾는다.
                                if (pre.Status == PromptStatus.OK)
                                {///////////////////////////////////// 
                                    SelectionSet ss = pre.Value;
                                    ObjectId[] pipe_ids = ss.GetObjectIds();
                                    //-------------------추가----------------------------------
                                    using (Transaction acTrans = db.TransactionManager.StartTransaction())
                                    {
                                        DBText text = new DBText();
                                        BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                                        BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                                        foreach (var id in pipe_ids)
                                        {
                                            Polyline3d pipe_PolyLine = acTrans.GetObject(id, OpenMode.ForRead) as Polyline3d;
                                            if (pipe_PolyLine != null)
                                            {
                                                Point3d pipe_Position = new Point3d((double)(pipe_PolyLine.StartPoint.X + pipe_PolyLine.EndPoint.X) / 2,
                                                   (double)(pipe_PolyLine.StartPoint.Y + pipe_PolyLine.EndPoint.Y) / 2,
                                                   (double)(pipe_PolyLine.StartPoint.Z + pipe_PolyLine.EndPoint.Z) / 2);
                                                // ValvePostion과 Text위치를 검색한다.
                                                PromptSelectionResult resText = ed.SelectAll(acSelFtrText);
                                                if (resText.Status == PromptStatus.OK)
                                                {
                                                    SelectionSet ssText = resText.Value;
                                                    ObjectId[] idsText = ssText.GetObjectIds();
                                                    foreach (var idText in idsText)
                                                    {
                                                        text = acTrans.GetObject(idText, OpenMode.ForWrite) as DBText;
                                                        var dd = Math.Abs(text.AlignmentPoint.X - Math.Truncate(pipe_Position.X));
                                                        if (Math.Abs(text.AlignmentPoint.X - Math.Truncate(pipe_Position.X)) < 1 &&
                                                           Math.Abs(text.AlignmentPoint.Y - Math.Truncate(pipe_Position.Y)) < 1 &&
                                                           Math.Abs(text.AlignmentPoint.Z - Math.Truncate(pipe_Position.Z)) < 1)
                                                        {
                                                            if (valve_Name[i].Contains("25A"))
                                                            {
                                                                //131, 100, 80, 70, 65
                                                                int val = Convert.ToInt32(text.TextString) - (valve_Size[0] / 2);
                                                                text.TextString = val.ToString();
                                                            }
                                                            else if (valve_Name[i].Contains("19A"))
                                                            {
                                                                int val = Convert.ToInt32(text.TextString) - (valve_Size[1] / 2);
                                                                text.TextString = val.ToString();
                                                            }
                                                            else if (valve_Name[i].Contains("13A"))
                                                            {
                                                                int val = Convert.ToInt32(text.TextString) - (valve_Size[2] / 2);
                                                                text.TextString = val.ToString();
                                                            }
                                                            else if (valve_Name[i].Contains("10A"))
                                                            {
                                                                int val = Convert.ToInt32(text.TextString) - (valve_Size[2] / 2);
                                                                text.TextString = val.ToString();
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        acTrans.Commit();
                                    }
                                    //-------------------추가----------------------------------
                                }
                                else
                                {
                                    ed.WriteMessage("\nError : Valve와 연결된 파이프를 찾지 못했습니다.");
                                }
                            }

                        }
                        else
                        {
                            ed.WriteMessage("DBFile을 확인해주세요.");
                        }
                        ed.WriteMessage("\nPipe의 길이 조정 완료 되었습니다.");
                        pipeOfSubDim++;
                    }
                }
                else
                {
                    ed.WriteMessage("\nValve길이가 이미 반영 되었습니다.");
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            //CAD에서 하는 방법 1개 DB에서 하는 방법 1개. 진행.(db에서 vavle위치 가져오면서 연결된 객체들도)

        }
        [CommandMethod("ui")]
        public void ui()
        {
            Autodesk.Windows.RibbonControl ribbonControl = Autodesk.Windows.ComponentManager.Ribbon;
            //RibbonControl >> RibbonTab >> RibbonPanel >> 
            RibbonTab tab = new RibbonTab();
            tab.Title = "DDWORKS";
            tab.Id = "Tab_ID";

            RibbonPanelSource panelSor = new RibbonPanelSource();
            panelSor.Title = "Spool Information";

            RibbonPanel panel = new RibbonPanel();
            RibbonPanel panel_1 = new RibbonPanel();

            //RibbonTextBox textbox = new RibbonTextBox();
            //textbox.Width = 100;
            //textbox.IsEmptyTextValid = false;
            //textbox.AcceptTextOnLostFocus = true;
            //textbox.InvokesCommand = true;
            //textbox.Text = "1\" Vavle길이";
            //textbox.Size = RibbonItemSize.Standard;
            //textbox.TextValue = "40";
            //panelSor.Items.Add(textbox);
            //panelSor.Items.Add(new RibbonRowBreak());

            //RibbonTextBox textbox1 = new RibbonTextBox();
            //textbox1.Width = 100;
            //textbox1.IsEmptyTextValid = false;
            //textbox1.AcceptTextOnLostFocus = true;
            //textbox1.InvokesCommand = true;
            //textbox1.Size = RibbonItemSize.Standard;
            //textbox1.Text = "1\" Vavle길이";
            //textbox1.TextValue = "40";
            //panelSor.Items.Add(textbox1);

            RibbonCombo cmd = new RibbonCombo();
            cmd.Name = "cmd1";
            cmd.Id = "Mycmd1";
            cmd.Text = "Template Size";
            cmd.IsEnabled = true;
            cmd.ShowText = true;
            panelSor.Items.Add(cmd);

            RibbonButton button = new RibbonButton();
            button.Orientation = Orientation.Vertical;
            button.Text = "Lines\nDistance";
            button.Id = "LineBetween_distance";
            button.Size = RibbonItemSize.Large;
            button.ShowText = true;
            button.ShowImage = true;
            button.LargeImage = Images.getBitmap(Properties.Resources.line);
            button.CommandParameter = "LineChecked ";
            button.CommandHandler = new RibbonCommandHandler();
            panelSor.Items.Add(button);
            
            RibbonButton button2 = new RibbonButton();
            button2.Orientation = Orientation.Vertical;
            button2.Size = RibbonItemSize.Large;
            button2.Id = "Spool_Information";
            button2.ShowImage = true;
            button2.ShowText = true;
            button2.LargeImage = Images.getBitmap(Properties.Resources.CLOUD);
            button2.Text = "Spool\nText";
            button2.CommandHandler = new RibbonCommandHandler();
            panelSor.Items.Add(button2);

            RibbonButton button3 = new RibbonButton();
            button3.Orientation = Orientation.Vertical;
            button3.Size = RibbonItemSize.Large;
            button3.Id = "Export_WIR";
            button3.ShowImage = true;
            button3.ShowText = true;
            button3.LargeImage = Images.getBitmap(Properties.Resources.excel);
            button3.Text = "Export\nexcel";
            button3.CommandHandler = new RibbonCommandHandler();
            panelSor.Items.Add(button3);

            panel.Source = panelSor;
            tab.Panels.Add(panel);

            ribbonControl.Tabs.Add(tab);

        }
        public class RibbonCommandHandler : System.Windows.Input.ICommand
        {
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public event EventHandler CanExecuteChanged;
            public void Execute(object parameter)
            {
                Document doc = acadApp.DocumentManager.MdiActiveDocument;
                PipeInfo pi = new PipeInfo();
                if (parameter is RibbonButton)
                {
                    RibbonButton button = parameter as RibbonButton;
                    doc.Editor.WriteMessage("\n기능: " + button.Id + "\n");
                    switch (button.Id)
                    {
                        case "LineBetween_distance":
                            pi.pipeBetween_Distance();
                            break;
                        case "Spool_Information":
                            pi.select_Welding_Point();
                            break;
                        case "Export_WIR":
                            pi.selectBlock_ExportToInnerInformation();
                            break;
                    }
                }
            }
        }

        public class Images
        {
            public static BitmapImage getBitmap(Bitmap image)
            {
                MemoryStream stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.EndInit();

                return bmp;
            }
        }
        /* --------------- [CLASS START]-------------------*/
        /* 클래스 이름 : Pipe
         * 기능 설명 : Pipe에 관련된 기능.*/
        public class Pipe
        {
            Editor ed;
            Database db;
            Document doc;
            private Vector3d vec;

            public Pipe()
            {
                doc = Application.DocumentManager.MdiActiveDocument;
                ed = Application.DocumentManager.MdiActiveDocument.Editor; ;
                db = doc.Database;
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
                if (Math.Round(vector.GetNormal().X) == 1 || Math.Round(vector.GetNormal().X) == -1)
                {
                    db_column_name[0] = "POSY";
                    db_column_name[1] = "POSZ";
                    line_trans[0] = obj.StartPoint.Y;
                    line_trans[1] = obj.StartPoint.Z;
                }
                if (Math.Round(vector.GetNormal().Y) == 1 || Math.Round(vector.GetNormal().Y) == -1)
                {
                    db_column_name[0] = "POSX";
                    db_column_name[1] = "POSZ";
                    line_trans[0] = obj.StartPoint.X;
                    line_trans[1] = obj.StartPoint.Z;
                }
                if (Math.Round(vector.GetNormal().Z, 1) == 1 || Math.Round(vector.GetNormal().Z, 1) == -1)
                {
                    db_column_name[0] = "POSX";
                    db_column_name[1] = "POSY";
                    line_trans[0] = obj.StartPoint.X;
                    line_trans[1] = obj.StartPoint.Y;
                }
                return (db_column_name, line_trans);
            }

            public List<Polyline3d> get_PolyLinePoints_By_PromptSelectResult(PromptSelectionResult prSelRes)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    SelectionSet ss = prSelRes.Value;
                    ObjectId[] obIds = ss.GetObjectIds();
                    List<Vector3d> lines_Vec = new List<Vector3d>();
                    List<Polyline3d> polyLine_Point = new List<Polyline3d>();
                    foreach (var obid in obIds)
                    {
                        //객체 타입 확인
                        var objd = acTrans.GetObject(obid, OpenMode.ForWrite);
                        if (objd.ObjectId.ObjectClass.GetRuntimeType() == typeof(Polyline3d))
                        {
                            //객체 타입이 Polyline이면 형변환해서 좌표 사용.
                            Polyline3d polyLine = (Polyline3d)acTrans.GetObject(obid, OpenMode.ForWrite);
                            Vector3d vec = polyLine.StartPoint.GetVectorTo(polyLine.EndPoint).GetNormal();
                            polyLine_Point.Add(polyLine);
                            lines_Vec.Add(vec);
                        }
                    }
                    acTrans.Commit();
                    return polyLine_Point;
                }
            }
            public Vector3d get_Pipe_Group_Vector(PromptSelectionResult prSelRes)
            {
                //벡터만 주면 되는데 라인 포인트와 다른 기능들이 많다.
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    SelectionSet ss = prSelRes.Value;
                    ObjectId[] obIds = ss.GetObjectIds();
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec;
                    List<Vector3d> lines_Vec = new List<Vector3d>();
                    List<Point3d> lines_Point = new List<Point3d>();
                    acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    foreach (var obid in obIds)
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
                    }
                    acTrans.Commit();
                    return vec;
                }
            }

            public List<Vector3d> get_Pipe_SpoolGroup_Vector(List<Polyline3d> lines_Point)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    List<Vector3d> lineVecs = new List<Vector3d>();
                    for (int i = 1; i < lines_Point.Count; i++)
                    {
                        Point3d fromVec = new Point3d(lines_Point[0].StartPoint.X, lines_Point[0].StartPoint.Y, lines_Point[0].StartPoint.Z);
                        Point3d toVec = new Point3d(lines_Point[i].EndPoint.X, lines_Point[i].EndPoint.Y, lines_Point[i].EndPoint.Z);
                        Vector3d vec = fromVec.GetVectorTo(toVec).GetNormal();
                        lineVecs.Add(new Vector3d(Math.Round(vec.X), Math.Round(vec.Y), Math.Round(vec.Z)));
                    }
                    bool equal = false;
                    if (lineVecs.Count == 1)
                    {
                        equal = true;
                    }
                    for (int i = 1; i < lineVecs.Count; i++)
                    {
                        equal = lineVecs[0].IsEqualTo(lineVecs[i]);
                        if (!equal) { ed.WriteMessage("라인이 잘못 선택되었습니다."); }
                    }

                    List<Vector3d> groupVecs = new List<Vector3d>();
                    Vector3d vecTrans = new Vector3d(1, 1, 1); //좌표에 곱했을때 숫자가 변하지 않는값.
                    if (lineVecs[0].X == 1 || lineVecs[0].X == -1) vecTrans = new Vector3d(0, 1, 1);
                    else if (lineVecs[0].Y == 1 || lineVecs[0].Y == -1) vecTrans = new Vector3d(1, 0, 1);
                    else if (lineVecs[0].Z == 1 || lineVecs[0].Z == -1) vecTrans = new Vector3d(1, 1, 0);

                    if (equal) //모든 백터가 같을때만 진행.
                    {
                        if (lines_Point.Count > 0)
                        {
                            for (int i = 1; i < lines_Point.Count; i++)
                            {
                                // 파이프 진행방향은 좌표를 무효시켜서 Vector에 영향이 없도록.
                                Point3d fromVec = new Point3d(lines_Point[0].StartPoint.X * vecTrans.X, lines_Point[0].StartPoint.Y * vecTrans.Y, lines_Point[0].StartPoint.Z * vecTrans.Z);
                                Point3d toVec = new Point3d(lines_Point[i].StartPoint.X * vecTrans.X, lines_Point[i].StartPoint.Y * vecTrans.Y, lines_Point[i].StartPoint.Z * vecTrans.Z);
                                Vector3d vec = fromVec.GetVectorTo(toVec).GetNormal();
                                groupVecs.Add(new Vector3d(Math.Round(vec.X), Math.Round(vec.Y), Math.Round(vec.Z)));
                            }
                            acTrans.Commit();
                        }
                    }
                    return groupVecs;
                }
            }

            //라인과 교차점 필요.
            public void set_Distance_By_Pipe_between(List<Vector3d> groupVecs, List<Polyline3d> lines_Point, Point3dCollection leaderLine_Points)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec;
                    acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // 라인 벡터 구하기. 중복
                    List<Vector3d> lineVecs = new List<Vector3d>();
                    for (int i = 1; i < lines_Point.Count; i++)
                    {
                        Point3d fromVec = new Point3d(lines_Point[0].StartPoint.X, lines_Point[0].StartPoint.Y, lines_Point[0].StartPoint.Z);
                        Point3d toVec = new Point3d(lines_Point[i].EndPoint.X, lines_Point[i].EndPoint.Y, lines_Point[i].EndPoint.Z);
                        Vector3d vec = fromVec.GetVectorTo(toVec).GetNormal();
                        lineVecs.Add(new Vector3d(Math.Round(vec.X), Math.Round(vec.Y), Math.Round(vec.Z)));
                    }
                    Vector3d vecTrans = new Vector3d(1, 1, 1); //좌표에 곱했을때 숫자가 변하지 않는값.
                    string lineVecStr = "";
                    if (lineVecs[0].X == 1) { lineVecStr = "X"; vecTrans = new Vector3d(0, 1, 1); }
                    else if (lineVecs[0].X == -1) { lineVecStr = "X"; vecTrans = new Vector3d(0, 1, 1); }
                    else if (lineVecs[0].Y == 1) { lineVecStr = "Y"; vecTrans = new Vector3d(1, 0, 1); }
                    else if (lineVecs[0].Y == -1) { lineVecStr = "Y"; vecTrans = new Vector3d(1, 0, 1); }
                    else if (lineVecs[0].Z == 1) { lineVecStr = "Z"; vecTrans = new Vector3d(1, 1, 0); }
                    else if (lineVecs[0].Z == -1) { lineVecStr = "Z"; vecTrans = new Vector3d(1, 1, 0); }

                    Vector3d vecSum = new Vector3d(0, 0, 0);
                    foreach (var vec in groupVecs)
                    {
                        vecSum += vec;
                    }
                    double[] vecSumValue = { Math.Abs(vecSum.X), Math.Abs(vecSum.Y), Math.Abs(vecSum.Z) };
                    double max = vecSumValue.Max();
                    int vecXYZ = 0;
                    foreach (var vec in vecSumValue)
                    {
                        if (vec == max)
                        {
                            break;
                        }
                        vecXYZ++;
                    }
                    // 여기서 가장 큰 값을 제외한 LinePoint는 삭제 후 Order. 그룹으로 묶을 수 있다면.. 두개의 리스트로 나누기.
                    string gorupVecStr = "";
                    if (vecXYZ == 0) { gorupVecStr = "X"; lines_Point = lines_Point.OrderBy(p => p.StartPoint.X).ToList(); }
                    else if (vecXYZ == 1) { gorupVecStr = "Y"; lines_Point = lines_Point.OrderBy(p => p.StartPoint.Y).ToList(); }
                    else if (vecXYZ == 2) { gorupVecStr = "Z"; lines_Point = lines_Point.OrderBy(p => p.StartPoint.Z).ToList(); }

                    //차례대로 쓰기.
                    if (groupVecs.Count == lines_Point.Count - 1)
                    {
                        for (int i = 1; i < lines_Point.Count; i++)
                        {
                            DBText text = new DBText();

                            Point3d midlePoint = new Point3d(
                                 lines_Point[0].StartPoint.X + lineVecs[0].X * Math.Abs(lines_Point[0].StartPoint.X - lines_Point[0].EndPoint.X) / 2,
                                 lines_Point[0].StartPoint.Y + lineVecs[0].Y * Math.Abs(lines_Point[0].StartPoint.Y - lines_Point[0].EndPoint.Y) / 2,
                                 lines_Point[0].StartPoint.Z + lineVecs[0].Z * Math.Abs(lines_Point[0].StartPoint.Z - lines_Point[0].EndPoint.Z) / 2);

                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 180, Vector3d.ZAxis, Point3d.Origin));
                            text.Height = 12;
                            //gouprvecstr X LineVec Y일때 정확히 맞음.
                            if (gorupVecStr == "X" && lineVecStr == "Z")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X + (groupVecs[0].X * 20.0),
                                lines_Point[i].StartPoint.Y,
                                midlePoint.Z);
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.X - lines_Point[i - 1].StartPoint.X, 1)).ToString();
                            }
                            else if (gorupVecStr == "X" && lineVecStr == "Y")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X + (groupVecs[0].X * 20.0),
                                midlePoint.Y,
                                lines_Point[i].StartPoint.Z);
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.X - lines_Point[i - 1].StartPoint.X, 1)).ToString();
                            }
                            else if (gorupVecStr == "Y" && lineVecStr == "Z")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X + (groupVecs[0].X * 20.0),
                                lines_Point[i].StartPoint.Y,
                                 midlePoint.Z
                                );
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.Y - lines_Point[i - 1].StartPoint.Y, 1)).ToString();
                            }
                            else if (gorupVecStr == "Y" && lineVecStr == "X")
                            {
                                text.Position = new Point3d(
                                 midlePoint.X,
                                lines_Point[i].StartPoint.Y,
                                lines_Point[i].StartPoint.Z
                                );
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.Y - lines_Point[i - 1].StartPoint.Y, 1)).ToString();
                            }
                            else if (gorupVecStr == "Z" && lineVecStr == "Y")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X,
                                midlePoint.Y,
                                lines_Point[i].StartPoint.Z);
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.Z - lines_Point[i - 1].StartPoint.Z, 1)).ToString();
                            }
                            else if (gorupVecStr == "Z" && lineVecStr == "X")
                            {
                                text.Position = new Point3d(
                                    midlePoint.X,
                                lines_Point[i].StartPoint.Y,
                                lines_Point[i].StartPoint.Z);
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.Z - lines_Point[i - 1].StartPoint.Z, 1)).ToString();
                            }
                            else
                            {
                                text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * -180, Vector3d.ZAxis, Point3d.Origin));
                                text.Height = 1;
                            }

                            acBlkRec.AppendEntity(text);
                            acTrans.AddNewlyCreatedDBObject(text, true);
                        }
                    }
                    acTrans.Commit();
                }
            }
            // 포인트의 폴라 포인트 구하는 법 by Tony Tanzillo
            public Point3d PolarPoint(Point3d basepoint, double angle, double distance)
            {
                return new Point3d(
                    basepoint.X + (distance * Math.Cos(angle)),
                    basepoint.Y + (distance * Math.Sin(angle)),
                    basepoint.Z);
            }
        }
        public class ExcelObject
        {
            Excel.Application excelApp = null;
            Excel.Workbook wb = null;
            Excel.Worksheet ws = null;
            public ExcelObject()
            {
                List<string> header = new List<string>()
            { "설비", "PROJECT", "공정", "접수일", "관리번호","도면번호","배관사","모델러","제도사","용접번호" };

                excelApp = new Excel.Application();
                excelApp.Visible = false;
                wb = excelApp.Workbooks.Add();
                ws = wb.Worksheets.get_Item(1) as Excel.Worksheet;
                // 데이타 넣기
                int r = 1;
                foreach (var h in header)
                {
                    ws.Cells[1, r] = h;
                    r++;
                }
            }
            public void excel_InsertData(int row, int column, string data, bool isRow_Insert)
            {
                //표제도곽의 정보가 복사될 빈 공간을 만들어 준다. 도곽정보는 첫 번째 행에만 들어있고 
                //excel_CopyTo_StartEnd로 빈 공간에 쓰기를 한다.
                if (isRow_Insert)
                {
                    ws.Rows[row + 2].Insert(XlDirection.xlDown);
                }
                //기본적으로 도곽의 헤더값이 첫 행에 들어가니 2번째 행부터 값 시작.
                ws.Cells[row + 2, column] = data;

            }
            // 기능 엑셀의 첫 지점과 끝지점을 주면 첫 번째 줄을 끝줄까지 모두 복사한다.
            public void excel_CopyTo_StartEnd(int start, int end)
            {
                //기본적으로 도곽의 헤더값이 첫 행에 들어가니 2번째 행부터 값 시작.
                int header_Count = 2;
                //입력 받는 end값은 0부터 시작카운트한 값이니 1을 더 더해준다.
                int paste_Rows = 1;
                ws.Range[string.Format("A{0}:I{0}", start + header_Count)].Copy(ws.Range[string.Format("A{0}:I{1}", start + header_Count + paste_Rows, end + paste_Rows)]);
            }
            public void excel_save()
            {
                try
                {
                    if (wb != null)
                    {
                        Document doc = Application.DocumentManager.MdiActiveDocument;
                        Editor ed = doc.Editor;
                        System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
                        dlg.Filter = "EXCEL 파일(*.xlsx)|*.xls";
                        dlg.FileName = "제목없음" + ".xls";
                        if (dlg.ShowDialog() == DialogResult.Cancel) return;
                        DateTime currentTime = DateTime.Now;
                        wb.SaveAs(dlg.FileName, Excel.XlFileFormat.xlWorkbookNormal, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                               Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlExclusive, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                        MessageBox.Show("저장되었습니다.");
                        wb.Close(true);
                        excelApp.Quit();
                    }
                }
                finally
                { // Clean up
                    ReleaseExcelObject(ws);
                    ReleaseExcelObject(wb);
                    ReleaseExcelObject(excelApp);
                }
            }
            private static void ReleaseExcelObject(object obj)
            {
                try
                {
                    if (obj != null)
                    {
                        Marshal.ReleaseComObject(obj);
                        obj = null;
                    }
                }
                catch (System.Exception ex)
                {
                    obj = null;
                    throw ex;
                }
                finally
                {
                    GC.Collect();
                }
            }
        }
        /* 클래스 이름 : Points
        * 기능 설명 : Points 에 정렬,배치,삭제 등 기능.*/
        public class Points
        {
            // 함수 기능 : 선택된 WeldPoints들을 그룹 벡터방향에따라 정렬시킨다.
            // 함수 설명 : vec => 단일 배관의 진행방향
            //            groupVec => 배관과 배관의 Vector(배관 진행방향은 Y축이고 AL를 채우는 방향은 Z이면 groupVec는 Z)
            // 반환 : WeldPoint를 groupVec와 동일하게 정렬(spool정보가 쓰여질 순서)
            public static (List<Point3d>, string) orderWeldPoints_By_GroupVector(List<Point3d> oldPoints, List<Vector3d> vec)
            {
                Vector3d groupVec = new Vector3d();
                string groupVecstr = "";
                List<Point3d> orderPoints = new List<Point3d>();
                if (vec.Count > 0)
                {
                    if (Math.Round(vec[0].GetNormal().X, 1) == 1 || Math.Round(vec[0].GetNormal().X, 1) == -1)
                    {
                        orderPoints = oldPoints.OrderByDescending(p => p.X).ToList();
                        for (int i = 0; i < oldPoints.Count; i++)
                        {
                            //7.12추가
                            //Pipe가 두 가닥이고 웰드가 지그재그일때는 성립하지 않는 알고리즘이어서 파이프의 백터방향과 같지 않는 방향을 찾을때까지 반복.
                            //파이프 방향이 Z이면 파이프그룹을 구할때는 Z축을 뺴버린다.
                            Point3d fromVec = new Point3d(0, orderPoints[0].Y, orderPoints[0].Z);
                            Point3d toVec = new Point3d(0, orderPoints[i].Y, orderPoints[i].Z);
                            groupVec = fromVec.GetVectorTo(toVec).GetNormal();
                            if (Math.Round(groupVec.GetNormal().Y, 1) == 1 || Math.Round(groupVec.GetNormal().Y, 1) == -1) { orderPoints = oldPoints.OrderByDescending(p => p.Y).ToList(); groupVecstr = "Y"; break; }
                            else if (Math.Round(groupVec.GetNormal().Z, 1) == 1 || Math.Round(groupVec.GetNormal().Z, 1) == -1) { orderPoints = oldPoints.OrderByDescending(p => p.Z).ToList(); groupVecstr = "Z"; break; }
                        }
                    }
                    if (Math.Round(vec[0].GetNormal().Y, 1) == 1 || Math.Round(vec[0].GetNormal().Y, 1) == -1)
                    {
                        orderPoints = oldPoints.OrderByDescending(p => p.Y).ToList();
                        for (int i = 0; i < oldPoints.Count; i++)
                        {
                            Point3d fromVec = new Point3d(orderPoints[0].X, 0, orderPoints[0].Z);
                            Point3d toVec = new Point3d(orderPoints[i].X, 0, orderPoints[i].Z);
                            groupVec = fromVec.GetVectorTo(toVec).GetNormal();
                            if (Math.Round(groupVec.GetNormal().X, 1) == 1 || Math.Round(groupVec.GetNormal().X, 1) == -1) { orderPoints = oldPoints.OrderByDescending(p => p.X).ToList(); groupVecstr = "X"; break; }
                            else if (Math.Round(groupVec.GetNormal().Z, 1) == 1 || Math.Round(groupVec.GetNormal().Z, 1) == -1) { orderPoints = oldPoints.OrderByDescending(p => p.Z).ToList(); groupVecstr = "Z"; break; }
                        }
                    }
                    if (Math.Round(vec[0].GetNormal().Z, 1) == 1 || Math.Round(vec[0].GetNormal().Z, 1) == -1)
                    {
                        orderPoints = oldPoints.OrderByDescending(p => p.Z).ToList();
                        for (int i = 0; i < oldPoints.Count; i++)
                        {
                            Point3d fromVec = new Point3d(orderPoints[0].X, orderPoints[0].Y, 0);
                            Point3d toVec = new Point3d(orderPoints[i].X, orderPoints[i].Y, 0);
                            groupVec = fromVec.GetVectorTo(toVec).GetNormal();
                            if (Math.Round(groupVec.GetNormal().X, 1) == 1 || Math.Round(groupVec.GetNormal().X, 1) == -1) { orderPoints = oldPoints.OrderByDescending(p => p.X).ToList(); groupVecstr = "X"; break; }
                            else if (Math.Round(groupVec.GetNormal().Y, 1) == 1 || Math.Round(groupVec.GetNormal().Y, 1) == -1) { orderPoints = oldPoints.OrderByDescending(p => p.Y).ToList(); groupVecstr = "Y"; break; }
                        }
                    }
                }
                return (orderPoints, groupVecstr);
            }
        }
        public class Compare
        {
            public bool isInside_boundary(Point3d points, Point3d min, Point3d max)
            {
                bool isInner = false;
                if (points.X > min.X && points.Y > min.Y && points.X < max.X && points.Y < max.Y)
                {
                    isInner = true;
                }
                return isInner;
            }
        }
        /* 클래스 이름 : Rectangle
        * 기능 설명 : Rectangel 에 관련된 기능.*/
        public class InteractionControl
        {
            //겹치는 Text선택하기(RayCast기능).
            //빈공간 찾기(Interaction없는 Area) 배관 주변에.
            //선택한 Entity.
        }
        /* 클래스 이름 : InteractivePolyLine.
        * 기능 설명 : InteractivePolyLine 에 관련된 기능.*/
        public class MyDrawOverrule : DrawableOverrule
        {
            public override bool WorldDraw(Drawable drawable, WorldDraw wd)
            {
                // Cast Drawable to Line so we can access its methods and
                // properties
                Line ln = (Line)drawable;
                // Draw some graphics primitives
                wd.Geometry.Circle(
                  ln.StartPoint + 0.5 * ln.Delta,
                  ln.Length / 5,
                  ln.Normal
                );
                // In this case we don't want the line to draw itself, nor do
                // we want ViewportDraw called
                return true;
            }
        }
        public class InteractivePolyLine
        {
            // 기능 : 사용자에게 포인트를 입력받아 PolyLine 작성.
            // 반환 : Point3dCollection
            public static Point3dCollection CollectPointsInteractive()
            {
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Point3dCollection pointCollection = new Point3dCollection();
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Autodesk.AutoCAD.Colors.Color color = acDoc.Database.Cecolor;

                PromptPointOptions pointOptions = new PromptPointOptions("\n 포인트 점: ")
                {
                    AllowNone = true
                };

                PromptPointResult pointResult = acDoc.Editor.GetPoint(pointOptions);
                while (pointResult.Status == PromptStatus.OK)
                {
                    pointCollection.Add(pointResult.Value);
                    //6.13 추후 실시간 선택되는것 활성화(블루) 시각화 기능 추가
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
                        true);
                        ed.UpdateScreen();
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
            public static void RectangleInteractive(List<Point3d> point_minmax)
            {
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Point3dCollection pointCollection = new Point3dCollection();
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Database db = acDoc.Database;
                Autodesk.AutoCAD.Colors.Color color = acDoc.Database.Cecolor;


                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    Line li = new Line(point_minmax[0], point_minmax[1]);
                    li.ColorIndex = 4;
                    acBlkRec.AppendEntity(li);
                    acTrans.AddNewlyCreatedDBObject(li, true);

                    acTrans.Commit();
                }
            }
        }
        public class Select
        {
            public static List<Point3d> selectPolyFaceMeshToPoints()
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    List<Point3d> point3Ds = new List<Point3d>();
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    PromptSelectionOptions pso = new PromptSelectionOptions();
                    TypedValue[] tvs = { new TypedValue(0, "Polyline") }; //Polyline (Polyline2D, Polyline3D, PolyfaceMesh, and PolygonMesh)
                    SelectionFilter sf = new SelectionFilter(tvs);
                    PromptSelectionResult pRes = ed.GetSelection(pso, sf);
                    try
                    {
                        if (pRes.Status == PromptStatus.OK)
                        {
                            SelectionSet ss = pRes.Value;
                            ObjectId[] oIds = ss.GetObjectIds();
                            var objs = new ObjectIdCollection();
                            int count = 0;

                            foreach (ObjectId oId in oIds)
                            {
                                Entity ent = acTrans.GetObject(oId, OpenMode.ForRead) as Entity;
                                Entity pF = new PolyFaceMesh() as Entity;
                                Type type = ent.GetType();
                                if (type.Name == "PolyFaceMesh")
                                {
                                    objs.Add(oId);
                                    count++;
                                    ent.Highlight();

                                    Extents3d et = ent.GeometricExtents;
                                    //실 좌표를 구하기 위해서 폴리라인의 크기를 구한다. 
                                    double x = et.MaxPoint.X - et.MinPoint.X;
                                    double y = et.MaxPoint.Y - et.MinPoint.Y;
                                    double z = et.MaxPoint.Z - et.MinPoint.Z;

                                    //et min좌표에서 구한 크기를 빼서 더해준다. 원객체는 xy 가 반지름과 같기 때문에 아무 값이나 상관없다. 
                                    double posX = Math.Round(et.MinPoint.X + (x / 2), 4);
                                    double posY = Math.Round(et.MinPoint.Y + (y / 2), 4);
                                    double posZ = Math.Round(et.MinPoint.Z + (z / 2), 4);
                                    point3Ds.Add(new Point3d(posX, posY, posZ));
                                    //ed.WriteMessage(posX.ToString()+"__"+posY.ToString()+"__"+ posZ.ToString());
                                    //용접 포인트를 DB에서 가져오는 알고리즘 추가. 지금은 PolyLine임.
                                    //다시 말하면 좌표로 파이프 정보를 가져오는 알고리즘.
                                    //좌표로 Area 그룹 
                                }
                            }
                        }
                        acTrans.Commit();
                        acTrans.Dispose();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        MessageBox.Show("*에러 : " + ex.ToString());
                    }
                    return point3Ds;
                }
            }
        }
        public class View
        {
            private Document _doc = null;
            private ViewTableRecord _vtr = null;
            private ViewTableRecord _initial = null;
            public View(Document doc)
            {
                _doc = doc;
                _initial = doc.Editor.GetCurrentView();
                _vtr = (ViewTableRecord)_initial.Clone();
            }
            public void Reset()
            {
                _doc.Editor.SetCurrentView(_initial);
                _doc.Editor.Regen();
            }
            // 현재 View의 이름을 반환.
            // 종속성 : TextControl Class와 연관.
            public string GetViewName(Vector3d viewDirection)
            {
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
        /* 클래스 이름 : TextControl
        * 기능 설명 : TextControl 에 관련된 기능.*/
        public class TextControl
        {
            private Editor ed = null;
            private Database db = null;
            private Document doc = null;
            // 종속성 Class : View
            private View curView = null;
            public TextControl()
            {
                /* acTrans : 
                   acBlkRec : 
                   final_Point : 
                   textDisBetween : 
                   textSize : 작업자 설정 필요
                   oblique : 배관 그룹의 Vector에 때라 조정 필요
                   Rotate : 배관 그룹의 Vector에 때라 조정 필요 */
                ed = Application.DocumentManager.MdiActiveDocument.Editor;
                doc = Application.DocumentManager.MdiActiveDocument;
                db = doc.Database;
                curView = new View(doc);
            }
            public void ed_Draw_Text_To_Line_Vector(List<Tuple<string, string>> pipe_Information_li, List<Point3d> line_final_Points, int textDisBetween, int textSize)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockTable edBLK = acTrans.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    BlockTableRecord edBLKrec = acTrans.GetObject(edBLK[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    var final_Point = new Point3d();
                    // View Port 방향에 따라서 Isometric에 따라 Text 3D Angle각도,Rotate값 변경.
                    string view_Name = "";
                    //연관성을 없애고 함수에서 사용할때 view이름을 넣어주는 방식으로 진행..
                    using (var view = ed.GetCurrentView())
                    {
                        view_Name = curView.GetViewName(view.ViewDirection);
                    }

                    for (int idx = 0; idx < pipe_Information_li.Count; idx++)
                    {
                        DBText acText = new DBText();

                        /* Text Init
                         * Text 사용자 편의에 따라 3D Rotation이 필요. 지시선 방향이 Ver, Hor 에 따라 각도가 다름.
                         * */
                        acText.HorizontalMode = (TextHorizontalMode)(int)TextHorizontalMode.TextRight;
                        acText.TextString = pipe_Information_li[idx].Item2;
                        Vector3d final_Points_Vec = (line_final_Points[line_final_Points.Count - 1] - line_final_Points[0]).GetNormal();

                        int text_3d_Ver_Angle = 0;
                        int text_3d_Hor_Angle = 0;
                        int text_oblique = 0;
                        int text_Rotate = 0;

                        /* Text Init END  */

                        /* Text Set Rotate (CurrentView) */
                        if (view_Name == "NW Isometric")
                        {
                            //Text 기본 각도는 한개로 적용해도 됨.
                            text_3d_Ver_Angle = -90;
                            text_3d_Hor_Angle = 90;
                            text_oblique = 0;
                            text_Rotate = 270;
                        }
                        else if (view_Name == "NE Isometric")
                        {
                            text_3d_Ver_Angle = 90;
                            text_3d_Hor_Angle = -90;
                            text_oblique = 0;
                            text_Rotate = 0;
                        }
                        else if (view_Name == "SW Isometric")
                        {

                        }
                        else if (view_Name == "SE Isometric")
                        {

                        }
                        else
                        {

                        }
                        //acText.SetDatabaseDefaults();
                        acText.Height = textSize;
                        acText.Rotation = Math.PI / 180 * text_Rotate;
                        acText.Oblique = Math.PI / 180 * text_oblique;

                        /* set Rotate 적용 끝 */

                        /*
                         * 지시선에 따른 Text 값 적용
                         * 텍스트 지시선 벡터의 마지막 포인트에 따라 Pipe Spool 정보를 배치한다. -> 추후 두개의 스풀 정보를 넣는 왼쪽 오른쪽 알고리즘이 필요.
                         */
                        if (final_Points_Vec.Z == 1 || final_Points_Vec.Z == -1)
                        {
                            //지시선 VEC에 따라 TEXT 기준 AXIS가 다르게 적용.(ROTATE 기준)
                            acText.Normal = Vector3d.ZAxis;
                            acText.TransformBy(Matrix3d.Rotation(Math.PI / 180 * text_3d_Ver_Angle, Vector3d.YAxis, Point3d.Origin));
                            acText.Justify = AttachmentPoint.BaseLeft;
                            //지시선 Vec방향에 따라 Text가 점점 멀어져야 하기 때문에 진행 방향에 Vec를 곱해서 거리가 점점 멀어지게 해줌.
                            final_Point = new Point3d(line_final_Points[line_final_Points.Count - 1].X, line_final_Points[line_final_Points.Count - 1].Y, line_final_Points[line_final_Points.Count - 1].Z + (textDisBetween * idx * final_Points_Vec.Z));
                        }
                        else if ((final_Points_Vec.X == 1 || final_Points_Vec.X == -1) || (final_Points_Vec.X == 1 || final_Points_Vec.X == -1))
                        {
                            //지시선 VEC에 따라 TEXT 기준 AXIS가 다르게 적용.(ROTATE 기준)
                            acText.Normal = Vector3d.XAxis;
                            acText.TransformBy(Matrix3d.Rotation(Math.PI / 180 * text_3d_Hor_Angle, Vector3d.ZAxis, Point3d.Origin));
                            acText.Justify = AttachmentPoint.BaseLeft;
                            //지시선 Vec방향에 따라 Text가 점점 멀어져야 하기 때문에 진행 방향에 Vec를 곱해서 거리가 점점 멀어지게 해줌.
                            final_Point = new Point3d(line_final_Points[line_final_Points.Count - 1].X + (textDisBetween * idx * final_Points_Vec.X), line_final_Points[line_final_Points.Count - 1].Y, line_final_Points[line_final_Points.Count - 1].Z);
                        }
                        else
                        {
                            ed.WriteMessage("\nError : 기준 라인을 다시 그려주시길 바랍니다.");
                        }
                        //TEXT 위치는 ALIGNMENT로 적용했다가 계속해서 에러 발생. 다시 POSITION으로 적용하니 문제 없음.
                        acText.Position = final_Point;
                        edBLKrec.AppendEntity(acText);
                        acTrans.AddNewlyCreatedDBObject(acText, true);
                    }
                    acTrans.Commit();
                    acTrans.Dispose();
                }
            }
            public void ed_Draw_Text(List<Tuple<string, string>> pipe_Information_li, List<Point3d> poc_final_Points, int textDisBetween, int textSize)
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
                        view_Name = curView.GetViewName(view.ViewDirection);
                    }

                    for (int idx = 0; idx < poc_final_Points.Count; idx++)
                    {
                        DBText acText = new DBText();

                        /* Text Init
                         * Text 사용자 편의에 따라 3D Rotation이 필요. 지시선 방향이 Ver, Hor 에 따라 각도가 다름.
                         * */
                        acText.HorizontalMode = (TextHorizontalMode)(int)TextHorizontalMode.TextRight;
                        acText.TextString = pipe_Information_li[idx].Item2;

                        int text_3d_Ver_Angle = 0;
                        int text_3d_Hor_Angle = 0;
                        int text_oblique = 0;
                        int text_Rotate = 0;

                        /* Text Init END  */

                        /* Text Set Rotate (CurrentView) */
                        if (view_Name == "NW Isometric")
                        {
                            //Text 기본 각도는 한개로 적용해도 됨.
                            text_3d_Ver_Angle = -90;
                            text_3d_Hor_Angle = 90;
                            text_oblique = 0;
                            text_Rotate = 270;
                        }
                        else if (view_Name == "NE Isometric")
                        {
                            text_3d_Ver_Angle = 90;
                            text_3d_Hor_Angle = -90;
                            text_oblique = 0;
                            text_Rotate = 0;
                        }

                        //acText.SetDatabaseDefaults();
                        acText.Height = textSize;
                        acText.Rotation = Math.PI / 180 * text_Rotate;
                        acText.Oblique = Math.PI / 180 * text_oblique;

                        /* set Rotate 적용 끝 */

                        /*
                         * 지시선에 따른 Text 값 적용
                         * 텍스트 지시선 벡터의 마지막 포인트에 따라 Pipe Spool 정보를 배치한다. -> 추후 두개의 스풀 정보를 넣는 왼쪽 오른쪽 알고리즘이 필요.
                         */

                        //지시선 VEC에 따라 TEXT 기준 AXIS가 다르게 적용.(ROTATE 기준)
                        acText.Normal = Vector3d.ZAxis;
                        acText.TransformBy(Matrix3d.Rotation(Math.PI / 180 * text_3d_Ver_Angle, Vector3d.YAxis, Point3d.Origin));
                        acText.Justify = AttachmentPoint.BaseLeft;
                        //지시선 Vec방향에 따라 Text가 점점 멀어져야 하기 때문에 진행 방향에 Vec를 곱해서 거리가 점점 멀어지게 해줌.
                        final_Point = new Point3d(poc_final_Points[idx].X, poc_final_Points[idx].Y, poc_final_Points[idx].Z);
                        //TEXT 위치는 ALIGNMENT로 적용했다가 계속해서 에러 발생. 다시 POSITION으로 적용하니 문제 없음.
                        acText.Position = final_Point;
                        edBLKrec.AppendEntity(acText);
                        acTrans.AddNewlyCreatedDBObject(acText, true);
                    }
                    acTrans.Commit();

                }
            }
            public void Draw_Text_SpoolInformationByPoints(List<Tuple<int, Point3d>> near_Points, List<Vector3d> vec_li, string viewName, List<string> spoolInfo_li, List<Point3d> newPoints, Vector3d spoolGroupVec)
            {
                // 7.17 정리된 기능으로 e다시..
                //각 조건을 다 만들지 말고 최조에 0,0에 그려서 3d Rotation으로 돌려준다. 단 ISOMERIC별로는 Text마다 3DRotation을 돌리고. Vertical은 Group을 돌린다. 
                //Near_Points : 각 포인트의 그룹을 지정
                //Vec_Li : 파이프의 진행방향.

                for (int k = 0; k < near_Points.Count; k++)
                {
                    string[] textString = new string[3];
                    textString[0] = "Left";
                    textString[1] = "Center";
                    textString[2] = "Right";

                    int[] textAlign = new int[3];
                    textAlign[0] = (int)TextHorizontalMode.TextLeft;
                    textAlign[1] = (int)TextHorizontalMode.TextCenter;
                    textAlign[2] = (int)TextHorizontalMode.TextRight;
                }
            }
            //이 기능에서 상위 기능을 만들어서 끝단인지 중간인지(/2로떨어지는지) 확인하고 이 기능에 들어가기.
            public List<ObjectId> Draw_Text_WeldPoints(Line line, List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints, string groupVecstr)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    List<ObjectId> spoolTexts = new List<ObjectId>(); //반환값
                    try
                    {
                        // 웰딩 포인트에 연결된 파이프를 찾아 Vector방향을 알아낸다.
                        // Spool 정보도 같이 불러온다. (맞대기 용접은 좌표를 더해서 반환)
                        //(List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints) = ddworks_Database.Get_Pipe_Vector_By_SpoolList(orderPoints);
                        // WeldPoint들과 최소 거리에 있는 (현재는 300) WeldPoint들을 모두 그룹으로 묶는다.
                        if (newPoints.Count > 0)
                        {
                            List<Tuple<int, Point3d>> near_Points = new List<Tuple<int, Point3d>>();
                            List<Point3d> point_Groups = new List<Point3d>();
                            List<int> key = new List<int>();
                            int group_index = 0;
                            if (newPoints.Count > 1)
                            {
                                near_Points.Add(new Tuple<int, Point3d>(group_index, newPoints[0]));
                                for (int i = 0; i < newPoints.Count; i++)
                                {
                                    // 용접 포인트의 Area별 그룹을 선택하기 위해 Tuple 자료형(중복키)
                                    // newPoints를 서로 다른 인덱스로 탐색 i , j
                                    // Group_index : Group Key로 구분
                                    // key 배열 : i와 가까운 포인트의 배열의 인덱스들을 저장. 
                                    // key 배열 : i가 중복 탐색하는 것을 방지.
                                    if (key.Contains(i) == false)
                                    {
                                        for (int j = 1; j < newPoints.Count; j++)
                                        {
                                            if (key.Contains(j) == false)
                                            {
                                                var dis = newPoints[i].DistanceTo(newPoints[j]);
                                                //ed.WriteMessage("좌표 : {0}\n", newPoints[j]);
                                                //ed.WriteMessage("거리값 : {0} \n", dis);
                                                if (dis < 300)
                                                {
                                                    key.Add(j);
                                                    //ed.WriteMessage(group_index.ToString(), newPoints[j].ToString());
                                                    near_Points.Add(new Tuple<int, Point3d>(group_index, newPoints[j]));
                                                }
                                            }
                                        }
                                        key.Add(i);
                                        group_index++;
                                    }
                                }
                                List<Point3d> group = new List<Point3d>();
                                double aver_X = 0.0;
                                double aver_Y = 0.0;
                                double aver_Z = 0.0;

                                int group_count = 0;
                                for (int j = 0; j < near_Points.Count; j++)
                                    if (near_Points[j].Item1 == 0)
                                    {
                                        group_count++;
                                        group.Add(near_Points[j].Item2);
                                        aver_X += near_Points[j].Item2.X;
                                        aver_Y += near_Points[j].Item2.Y;
                                        aver_Z += near_Points[j].Item2.Z;
                                    }
                                aver_X = aver_X / group_count;
                                aver_Y = aver_Y / group_count;
                                aver_Z = aver_Z / group_count;
                                double basePoint = 0;

                                // ****************** 스풀 정보의 베이스 포인트 지정 **********************
                                //입력 받은 라인 끝점과 Spool포인트의 Vec에 따라 음수이면 그대로.
                                //양수이면 Spool Line의 글씨 갯수/2 * 15 해서 플러스

                                Vector3d lineVec = line.StartPoint.GetVectorTo(line.EndPoint).GetNormal();
                                lineVec = new Vector3d(Math.Round(lineVec.X, 1), Math.Round(lineVec.Y), Math.Round(lineVec.Z));
                                int text_SideBetween_Dis = 20;
                                int text_TopDownBetween_Dis = 25;
                                int textTrans_Ang = 90;
                                Point3d befor_TextPosition = new Point3d();

                                if (groupVecstr == "X")
                                {
                                    // 스풀 지시선 벡터가 양수일때는 스풀정보 전체 크기를 계산해 반영.
                                    // Spool 순서가 섞이면 안되기 때문에 그대로 반영하기 위함.
                                    if (lineVec.X == 1) basePoint = line.EndPoint.X + (spoolInfo_li.Count / 2) * text_TopDownBetween_Dis;
                                    else basePoint = line.EndPoint.X + (50 * lineVec.X); // 이 값을 조정
                                }
                                else if (groupVecstr == "Y")
                                {
                                    if (lineVec.Y == 1) basePoint = line.EndPoint.Y + (spoolInfo_li.Count / 2) * text_TopDownBetween_Dis;
                                    else basePoint = line.EndPoint.Y + (50 * lineVec.Y);
                                }
                                else
                                {
                                    if (lineVec.Z == 1) basePoint = line.EndPoint.Z + (spoolInfo_li.Count / 2) * text_TopDownBetween_Dis;
                                    else basePoint = line.EndPoint.Z + (50 * lineVec.Z);
                                }

                                List<double> basePointLi = new List<double>();
                                for (int i = 0; i < spoolInfo_li.Count / 2; i++)
                                {
                                    basePointLi.Add(aver_Y + (i * 15));
                                }
                                if (near_Points.Count == spoolInfo_li.Count)
                                {
                                    // Vector 에 따른 TEXT의 회전값과 정렬.
                                    // 유의 : text.Normal = Vector3d.ZAxis; 은 꼭 text.Position 앞에 지정을 한다. 
                                    // 이아래 기능을 Text Control Class에 들어가야한다. DrawText_BY_Vector로..Veclist와 PointList
                                    for (int k = 0; k < near_Points.Count; k++)
                                    {
                                        string[] textString = new string[3];
                                        textString[0] = "Left";
                                        textString[1] = "Center";
                                        textString[2] = "Right";

                                        int[] textAlign = new int[3];
                                        textAlign[0] = (int)TextHorizontalMode.TextLeft;
                                        textAlign[1] = (int)TextHorizontalMode.TextCenter;
                                        textAlign[2] = (int)TextHorizontalMode.TextRight;

                                        DBText text = new DBText();
                                        text.SetDatabaseDefaults();

                                        //K와 SpoolInfo가 숫자가 다르면 들어가면 에러. 체크필요.
                                        text.TextString = spoolInfo_li[k].ToString();
                                        text.Normal = Vector3d.YAxis;
                                        //text.Position = new Point3d(basePoint.X, basePoint.Y - (k*15), basePoint.Z);
                                        text.Height = 12.0;
                                        int nCnt = 0;
                                       
                                        Point3d textPosition = new Point3d();

                                        if (k > 0)
                                        {
                                            string[] beforeText = spoolInfo_li[k - 1].Split('_');
                                            string[] afterText = spoolInfo_li[k].Split('_');
                                            if (beforeText[beforeText.Count()-2] != afterText[beforeText.Count() - 2])
                                            {
                                                basePoint -= text_TopDownBetween_Dis;
                                            }
                                        }

                                        //if (k % 2 != 0 && k != 0)
                                        //{
                                        //   basePoint -= text_TopDownBetween_Dis;
                                        //}

                                        if (Math.Round(vec_li[k].GetNormal().X, 1) + Math.Round(vec_li[k].GetNormal().Y, 1) + Math.Round(vec_li[k].GetNormal().Z, 1) > 0)
                                        {
                                            //벡터가 양수이면 오른쪽 배치
                                            nCnt = 2;
                                        }
                                        else
                                        {
                                            //백터가 음수이면 왼쪽 배치
                                            nCnt = 0;
                                        }

                                        if (Math.Round(vec_li[k].GetNormal().X, 1) == 1 || Math.Round(vec_li[k].GetNormal().X, 1) == -1)
                                        {
                                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * textTrans_Ang, Vector3d.XAxis, Point3d.Origin));
                                            if (nCnt == 2)
                                            {
                                                textPosition = new Point3d(line.EndPoint.X + text_SideBetween_Dis, basePoint, line.EndPoint.Z);
                                                if (groupVecstr == "Z") textPosition = new Point3d(line.EndPoint.X + text_SideBetween_Dis, line.EndPoint.Y, basePoint);
                                            }
                                            else if (nCnt == 0)
                                            {
                                                textPosition = new Point3d(line.EndPoint.X - text_SideBetween_Dis, basePoint, line.EndPoint.Z);
                                                if (groupVecstr == "Z") textPosition = new Point3d(line.EndPoint.X - text_SideBetween_Dis, line.EndPoint.Y, basePoint);
                                            }
                                        }
                                        if (Math.Round(vec_li[k].GetNormal().Y, 1) == 1 || Math.Round(vec_li[k].GetNormal().Y, 1) == -1)
                                        {
                                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * textTrans_Ang, Vector3d.ZAxis, Point3d.Origin));
                                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * textTrans_Ang, Vector3d.YAxis, Point3d.Origin));
                                            if (nCnt == 2)
                                            {
                                                textPosition = new Point3d(basePoint, line.EndPoint.Y + text_SideBetween_Dis, line.EndPoint.Z);
                                                //그룹 벡터가 Z일때 조건
                                                if (groupVecstr == "Z") textPosition = new Point3d(line.EndPoint.X, line.EndPoint.Y + text_SideBetween_Dis, basePoint); text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * -textTrans_Ang, Vector3d.YAxis, Point3d.Origin));
                                            }
                                            else if (nCnt == 0)
                                            {
                                                textPosition = new Point3d(basePoint, line.EndPoint.Y - text_SideBetween_Dis, line.EndPoint.Z);
                                                if (groupVecstr == "Z") textPosition = new Point3d(line.EndPoint.X, line.EndPoint.Y - text_SideBetween_Dis, basePoint); text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * -textTrans_Ang, Vector3d.YAxis, Point3d.Origin));
                                            }
                                        }
                                        if (Math.Round(vec_li[k].GetNormal().Z, 1) == 1 || Math.Round(vec_li[k].GetNormal().Z, 1) == -1)
                                        {
                                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 180, Vector3d.YAxis, Point3d.Origin));
                                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * textTrans_Ang, Vector3d.YAxis, Point3d.Origin));
                                            if (nCnt == 0)
                                            {
                                                if (groupVecstr == "X") textPosition = new Point3d(basePoint, line.EndPoint.Y, line.EndPoint.Z - text_SideBetween_Dis);
                                                else if (groupVecstr == "Y") textPosition = new Point3d(line.EndPoint.X, basePoint, line.EndPoint.Z - text_SideBetween_Dis);
                                            }
                                            else if (nCnt == 2)
                                            {
                                                if (groupVecstr == "X") textPosition = new Point3d(basePoint, line.EndPoint.Y, line.EndPoint.Z + text_SideBetween_Dis);
                                                else if (groupVecstr == "Y") textPosition = new Point3d(line.EndPoint.X, basePoint, line.EndPoint.Z + text_SideBetween_Dis);
                                            }

                                        }
                                        if (textPosition.IsEqualTo(befor_TextPosition))
                                        {
                                            if (groupVecstr == "X") textPosition = new Point3d(textPosition.X - text_TopDownBetween_Dis, textPosition.Y, textPosition.Z);
                                            else if (groupVecstr == "Y") textPosition = new Point3d(textPosition.X, textPosition.Y - text_TopDownBetween_Dis, textPosition.Z);
                                            else if (groupVecstr == "Z") textPosition = new Point3d(textPosition.X, textPosition.Y , textPosition.Z - text_TopDownBetween_Dis);
                                        }
                                        text.Position = textPosition;
                                        befor_TextPosition = new Point3d(textPosition.X, textPosition.Y, textPosition.Z);
                                        text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                        if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                        {
                                            text.AlignmentPoint = textPosition;
                                        }
                                        acBlkRec.AppendEntity(text);
                                        spoolTexts.Add(text.ObjectId);
                                        acTrans.AddNewlyCreatedDBObject(text, true);
                                    }
                                }
                                //삭제 예정.
                                List<Point3d> li = new List<Point3d>();
                                foreach (var d in near_Points)
                                {
                                    if (d.Item1 == 1)
                                    {
                                        li.Add(d.Item2);
                                    }
                                }
                                //
                            }
                            else
                            {
                                ed.WriteMessage("\nError : 파이프 정보가 없습니다.");
                            }
                        }
                        else
                        {
                            ed.WriteMessage("\nError : 선택된 객체가 없습니다.");
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                    acTrans.Commit();
                    acTrans.Dispose();
                    return spoolTexts;
                }
            }
        }
        /* 클래스 이름 : DDWorks_Database
        * 기능 설명 : DDWorks_Database 에 관련된 기능.*/
        public class DDWorks_Database
        {
            private string db_path = "";
            private string db_TB_PIPEINSTANCES = "TB_PIPEINSTANCES";
            private Editor db_ed;
            static Document db_doc;
            private string drawingName = "";
            private Database db_acDB;
            private string ownerType_Component = "768"; //TB_POCINSTANCES:OWNER_TYPE 기자재.
            private string ownerType_Pipe = "256"; //TB_POCINSTANCES:OWNER_TYPE 파이프.
            private string valveType = "valve"; //valve타입.
            public DDWorks_Database(string acDB_path)
            {
                db_ed = Application.DocumentManager.MdiActiveDocument.Editor;
                db_doc = Application.DocumentManager.MdiActiveDocument;
                db_acDB = db_doc.Database;
                db_path = acDB_path;
                if (db_doc.Name != null)
                {
                    drawingName = Path.GetFileName(db_doc.Name).Split('.')[0];
                }
                else
                {
                    db_ed.WriteMessage("None DocumentName");
                }
            }
            /* 쿼리문 시작*/
            public string SqlStr_TB_POCINSTANCES_By_Point(Point3d point)
            {
                //DB 테이블 요약 : POC 연결정보 연결 타입 등 
                string sql = string.Format(
                                 "SELECT * ," +
                                 "abs(POSX - {0})" +
                                 "as disx, abs(POSY - {1})" +
                                 "as disy, abs(POSZ - {2})" +
                                 "as disz FROM TB_POCINSTANCES " +
                                 "WHERE 1 > disx AND 1> disy AND 1 > disz ;", point.X, point.Y, point.Z);
                return sql;
            }
            public string SqlStr_TB_MODELINSTANCES(string owner_id, string ownerType)
            {
                //DB 테이블 요약 : 
                //  1.MODEL_TEMPLATE_NM,DISPLAY_NM 등 Fitting에 대한 정보를 얻을 수 있음.
                //  2.TB_POCINSTANCES에는 Owner_ID를 대상으로 한 연결 정보가 들어있음. 타입이 768이면 ModelInstances와 연결. 256이면 파이프 

                string sql = "";
                if (ownerType_Component == ownerType)
                {
                    sql = string.Format(
                                    "SELECT * " +
                                    "FROM TB_MODELINSTANCES INNER JOIN TB_MODELTEMPLATES " +
                                    "on TB_MODELINSTANCES.MODEL_TEMPLATE_ID = TB_MODELTEMPLATES.MODEL_TEMPLATE_ID " +
                                    "AND hex(TB_MODELINSTANCES.INSTANCE_ID) like '{0}';"
                                    , owner_id);
                }
                return sql;

            }
            public string SqlStr_TB_POINSTANCES_By_OWNER_INS_ID(string own_ins_id)
            {
                string sql = string.Format("SELECT * FROM TB_POCINSTANCES WHERE hex(OWNER_INSTANCE_ID) like '{0}';", own_ins_id);
                return sql;
            }
            public List<SQLiteDataReader> SQLite_ExcuteReader_By_Points(List<Point3d> points)
            {
                List<SQLiteDataReader> sqlReaderli = new List<SQLiteDataReader>();
                if (db_path != "")
                {
                    string connstr = "Data Source=" + db_path;
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();

                        foreach (var point in points)
                        {
                            string sql = SqlStr_TB_POCINSTANCES_By_Point(point);
                            SQLiteCommand command = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr = command.ExecuteReader();
                            sqlReaderli.Add(rdr);
                        }

                        conn.Close();
                    }
                }
                return sqlReaderli;
            }
            /* 쿼리문 끝 */

            /* DDWorks Database Class 함수 시작 */
            /*
            * 함수 이름 : Get_PipeInstanceIDs_By_ObjIDs
            * 기능 설명 : Point를 기준으로 Pipe의 Spool정보를 반환.
            */
            public List<string> Get_PipeInstanceIDs_By_ObjIDs(PromptSelectionResult prSelRes, Point3dCollection pointCollection)
            {
                Pipe pi = new Pipe();
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
            /*
            * 함수 이름 : Get_Spool_Infomation_By_ObjIds
            * 기능 설명 : OBJECT IDS리스트를 기준으로 Pipe의 Spool정보를 반환.
            * 반환 값 : Tuple<OwnerId, Spool정보>
            * 비고 : 삭제  -> Get_Pipe_Spool_Info_By_OwnerInsId로 대체.
            */
            public List<Tuple<string, string>> Get_Spool_Infomation_By_ObjIds(List<string> pipe_InstanceIDS)
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

            /*
            * 함수 이름 : Get_Final_POC_Instance_Ids
            * 기능 설명 : PIPE의 마지막 POC.
            * 관련 테이블 : TB_POCINSTANCES.
            * 입력 타입 : 없음.(DB에서 바로 검색).
            * 반환 값 : List(Point3d:POC의 위치), List(string:마지막 객체의 OWNER_INSTANCE_ID). <- 앞뒤객체를 검색해서 Pipe 앞뒤로 배치가능.
            */
            public (List<Point3d>, List<string>) Get_Final_POC_Instance_Ids()
            {
                // 마지막 찾은 Owner ID에서 객체 타입이 모델이라면 PIPE를 다시 찾는다. 
                List<string> final_objs_ID = new List<string>(); //Get_Spool_Infomation_By_ObjIds 와 연동필요
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
                                string prev_id = POC_prev(connstr, BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", ""), "INSTANCE_ID");
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

            public string POC_prev(string connstr, string current_POC, string column_name)
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

            public string POC_next(string current_POC)
            {
                //Owner ID가 1에 연결된 InstanceID를 하나 넘겨준다. 
                string next_poc_id = "";
                return next_poc_id;
            }
            /*
             * 함수 이름 : Get_Pipe_Spool_Info_By_OwnerInsId
             * 기능 설명 : Pipe의 Spool정보를 반환.
             * 관련 테이블 : TB_POCINSTANCES,TB_PIPESIZE,TB_PRODUCTION_DRAWING,TB_PRODUCTION_DRAWING_GROUPS,TB_INSTANCEGROUPS.
             * 입력 타입 : String 객체. TB_POCINSTANCES(OWNER_INSTANCE_ID).
             * 비고 : 같은 파일내에 같은 객체를 Group을 여러개 나눌경우 Spool정보가 유일한 값이 아니게 된다. 2개이상 존재. 그러므로 Instance Group ID 확인필요(파일이름).
             */
            public string Get_Pipe_Spool_Info_By_OwnerInsId(string ownerInsId)
            {
                string spool_info = "";
                string sql_spoolInfo = string.Format(
                        "SELECT PIPESIZE_NM,UTILITY_NM,PRODUCTION_DRAWING_GROUP_NM,SPOOL_NUMBER,IGM.INSTANCE_GROUP_ID " +
                        "FROM TB_POCINSTANCES as PI INNER JOIN " +
                        "TB_PIPESIZE as PS," +
                        "TB_UTILITIES as UT," +
                        "TB_PRODUCTION_DRAWING as PD," +
                        "TB_PRODUCTION_DRAWING_GROUPS as PDG," +
                        "TB_INSTANCEGROUPMEMBERS as IGM " +
                        "on PI.PIPESIZE_ID = PS.PIPESIZE_ID AND " +
                        "PI.UTILITY_ID = UT.UTILITY_ID AND " +
                        "PD.PRODUCTION_DRAWING_GROUP_ID = PDG.PRODUCTION_DRAWING_GROUP_ID AND " +
                        "PDG.INSTANCE_GROUP_ID = IGM.INSTANCE_GROUP_ID AND " +
                        "IGM.INSTANCE_ID = PI.OWNER_INSTANCE_ID AND " +
                       "hex(PD.INSTANCE_ID) like '{0}' AND " +
                       "hex(PI.OWNER_INSTANCE_ID) like '{0}';", ownerInsId);

                string sql_InstanceGroup = string.Format("SELECT IG.INSTANCE_GROUP_ID FROM TB_INSTANCEGROUPS as IG INNER JOIN TB_INSTANCEGROUPMEMBERS as IGM on IG.INSTANCE_GROUP_ID=IGM.INSTANCE_GROUP_ID AND IG.INSTANCE_GROUP_ID=" +
                    "(SELECT INSTANCE_GROUP_ID FROM TB_INSTANCEGROUPS WHERE TB_INSTANCEGROUPS.INSTANCE_GROUP_NM='{0}') AND hex(INSTANCE_ID) like '{1}';", drawingName, ownerInsId);

                string connstr = "Data Source=" + db_path;
                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    conn.Open();
                    SQLiteCommand comm = new SQLiteCommand(sql_InstanceGroup, conn);
                    SQLiteDataReader rdr = comm.ExecuteReader();
                    rdr.Read();
                    string instance_GroupId = BitConverter.ToString((byte[])rdr["INSTANCE_GROUP_ID"]).Replace("-", "");
                    rdr.Close();
                    comm.Dispose();
                    comm = new SQLiteCommand(sql_spoolInfo, conn);
                    rdr = comm.ExecuteReader();

                    while (rdr.Read())
                    {
                        string rdr_instanceGroupId = BitConverter.ToString((byte[])rdr["INSTANCE_GROUP_ID"]).Replace("-", "");
                        //찾은 객체의 첫번째 항목만 불러온다. -> 7.10수정 DWG 파일 이름이 곧 INSTANCE GORUP NM이기때문에 INSTANCEGROUP ID와 동일한 SpoolNM을 가져온다.
                        //함수 추가 필요. 파일이름 -> INSTANCE GROUP NM -> ID DB연결할때 가져와야한다.
                        if (rdr_instanceGroupId == instance_GroupId)
                        {
                            spool_info = rdr["PIPESIZE_NM"] + "_" + rdr["UTILITY_NM"] + "_" + rdr["PRODUCTION_DRAWING_GROUP_NM"] + "_" + rdr["SPOOL_NUMBER"];
                        }
                    }
                    conn.Dispose();
                }
                return spool_info;

            }

            /*
             * 함수 이름 : FilterWeldGroup_By_ComponentType
             * 기능 설명 : 선택된 WELD POINT의 연결객체가 Tee,Reducer등 제작도면과 관련없는 객체들을 선택리스트에서 제외시킴.
             * 관련 테이블 : TB_POCINSTANCES.TB_MODELINSTANCES.
             * 입력 타입 : List(선택된 용접포인트 좌표들). String 필터배열(사용예: Model_NM에 필터 내용이 포함된다면 WeldingPointList에서 삭제).
             */
            public List<Point3d> FilterWeldGroup_By_ComponentType(List<Point3d> weldGroup, string[] filters)
            {
                using (Transaction acTrans = db_acDB.TransactionManager.StartTransaction())
                {
                    List<int> indexes = new List<int>();
                    List<Point3d> filter_weldGroup = new List<Point3d>();
                    if (db_path != "" && weldGroup.Count > 0)
                    {
                        string connstr = "Data Source=" + db_path;
                        using (SQLiteConnection conn = new SQLiteConnection(connstr))
                        {
                            conn.Open();
                            for (int i = 0; i < weldGroup.Count; i++)
                            {
                                Point3d point = weldGroup[i];
                                string sql = SqlStr_TB_POCINSTANCES_By_Point(point);

                                SQLiteCommand command = new SQLiteCommand(sql, conn);
                                SQLiteDataReader rdr = command.ExecuteReader();
                                string owner_id = "";

                                // 좌표를 찾지 못하는 객체는 스풀정보 포함하지 않음 : Tee+Reducer 용접포인트는 DB에 POC좌표가 나오지 않음. 단관거리를 계산해서 넣어야함. 
                                // Tee. Reducer. Reducing 객체는 제외한다.
                                if (rdr.HasRows) //rdr 반환값이 있을때만 Read
                                {
                                    while (rdr.Read())
                                    {
                                        if (rdr["OWNER_TYPE"].ToString() == ownerType_Component)
                                        {
                                            owner_id = BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", "");
                                            string sql_model_template_nm = SqlStr_TB_MODELINSTANCES(owner_id, rdr["OWNER_TYPE"].ToString());
                                            SQLiteCommand command_1 = new SQLiteCommand(sql_model_template_nm, conn);
                                            SQLiteDataReader rdr_1 = command_1.ExecuteReader();

                                            if (rdr_1.HasRows) //rdr 반환값이 있을때만 Read
                                            {
                                                while (rdr_1.Read())
                                                {
                                                    // 선택된 weldGroup에서 라이브러리 이름이 Filter이름과 동일하면 Index를 저장한다.
                                                    foreach (string filter in filters)
                                                    {
                                                        if (rdr_1["MODEL_TEMPLATE_NM"].ToString().Contains(filter))
                                                        {
                                                            indexes.Add(i);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    indexes.Add(i); //rdr 좌표를 못찾은 좌표들도 indexes에 포함 가끔 Reducer와 Tee사이 객체는 파이프 중간점에 WeldPoint를 잡아서 DB에서 좌표로 찾을 수 없음.
                                }

                            }
                            conn.Close();
                        }

                        // weldGroup에서 필터로 걸러진 좌표를 제외한 좌표들을 복사한다.
                        for (int j = 0; j < weldGroup.Count; j++)
                        {
                            if (indexes.Contains(j) == false) //Filter에 속한 객체들을 제외하고 리스트 추가.
                            {
                                filter_weldGroup.Add(weldGroup[j]);
                            }
                        }
                    }
                    return filter_weldGroup;
                }
            }

            /*
             * 함수 이름 : Get_Pipe_Vector_By_WeldGroupPoints
             * 기능 설명 : 선택된 WELD POINT의 연결객체 중 PIPE의 OWNER_INSTANCE_ID -> SPOOL정보 -> VECTOR3D 정보(SPOOL정보를 배치할때사용) 
             * 관련 테이블 : TB_POCINSTANCES, 
             * 관련 함수 : Get_Pipe_Spool_Info_By_OwnerInsId사용(클래스 내부 함수 사용) : 스풀정보 반환하도록.
             * 입력 타입 : List(선택된 용접포인트 좌표들).
             * 반환 값 : List<string> / List<Vector3d>. 
             * 비고 : 1. PIPE의 맞대기 용접(WELD Point는 하나인데 Spool정보는 2개인 객체들은 weldGroup에서 좌표를 하나씩 늘려준다. 
             *        2. Vector 예시 (0,0,1)(0,0,-1)이 쌍으로 배치.
            */
            public (List<string>, List<Vector3d>, List<Point3d>) Get_Pipe_Vector_By_SpoolList(List<Point3d> weldGroup)
            {
                List<Vector3d> vec_li = new List<Vector3d>();
                List<string> spool_info_li = new List<string>();

                if (db_path != "")
                {
                    string connstr = "Data Source=" + db_path;
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        List<int> index_li = new List<int>(); // 최종적으로 맞대기 용접 index를 저장해서 weldGroup에서 좌표를 더해준다.
                        foreach (var point in weldGroup)
                        {
                            string sql = SqlStr_TB_POCINSTANCES_By_Point(point);
                            SQLiteCommand command = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr = command.ExecuteReader();
                            int count = 0; //파이프객체가 몇개인지 카운트.
                            while (rdr.Read())
                            {
                                // 파이프인 객체의 오너아이디를 가져와서 연결된 POC정보를 가져온다.(2포인트가 나옴)
                                if (rdr["OWNER_TYPE"].ToString() == ownerType_Pipe)
                                {
                                    count++;
                                    List<Point3d> points = new List<Point3d>();
                                    string instance_id = BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", "");

                                    string sql_ins = SqlStr_TB_POINSTANCES_By_OWNER_INS_ID(instance_id);
                                    spool_info_li.Add(Get_Pipe_Spool_Info_By_OwnerInsId(instance_id));

                                    //Get_Pipe_Spool_Info_By_OwnerInsId 로 스풀정보.
                                    SQLiteCommand command_1 = new SQLiteCommand(sql_ins, conn);
                                    SQLiteDataReader rdr_1 = command_1.ExecuteReader();

                                    if (rdr_1.HasRows)
                                    {
                                        while (rdr_1.Read())
                                        {
                                            points.Add(new Point3d((double)rdr_1["POSX"], (double)rdr_1["POSY"], (double)rdr_1["POSZ"]));
                                        }
                                        if (points.Count == 2)
                                        {
                                            int index = 0; //맞대기 용접 인덱스
                                            foreach (var weldPoint in weldGroup)
                                            {
                                                //CAD좌표는 DDWORKS 좌표에서 4번째에서 반올림한 좌표.
                                                //오너 아이디에서 반환된 Points와 weldPoint가 일치하면 
                                                if (Math.Abs(weldPoint.X - points[0].X) < 0.5 && Math.Abs(weldPoint.Y - points[0].Y) < 0.5 && Math.Abs(weldPoint.Z - points[0].Z) < 0.5)
                                                {
                                                    Vector3d vec = (points[1] - points[0]).GetNormal();
                                                    vec_li.Add(vec);
                                                    //맞대기 용접일때처리. 2번째 파이프 객체를 찾았을때(256) 좌표의 인덱스에 해당하는 좌표를 넣어준다.
                                                    if (count == 2)
                                                    {
                                                        index_li.Add(index);
                                                    }
                                                }
                                                else if (Math.Abs(weldPoint.X - points[1].X) < 0.5 && Math.Abs(weldPoint.Y - points[1].Y) < 0.5 && Math.Abs(weldPoint.Z - points[1].Z) < 0.5)
                                                {
                                                    Vector3d vec = (points[0] - points[1]).GetNormal();
                                                    vec_li.Add(vec);
                                                    //맞대기 용접일때처리. 2번째 파이프 객체를 찾았을때(256) 좌표의 인덱스에 해당하는 좌표를 넣어준다.
                                                    if (count == 2)
                                                    {
                                                        index_li.Add(index);
                                                    }
                                                }
                                                index++;
                                            }
                                        }
                                    }

                                }
                                // WeldGroup의 좌표를 받아서 Group 중간점 구하기 파이프의 Vector의 방향대로 글자를 배치.
                            }
                        }
                        foreach (int index in index_li)
                        {
                            weldGroup.Insert(index, weldGroup[index]);
                        }
                        conn.Close();
                    }
                }
                return (spool_info_li, vec_li, weldGroup);
            }
            /* DDWorks Database Class 함수 끝 */

            public List<Vector3d> Get_Pipe_Vector_By_Points(List<Point3d> weldGroup)
            {
                List<Vector3d> vec_li = new List<Vector3d>();

                if (db_path != "" && weldGroup.Count > 0)
                {
                    string connstr = "Data Source=" + db_path;
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {
                        conn.Open();
                        List<int> index_li = new List<int>(); // 최종적으로 맞대기 용접 index를 저장해서 weldGroup에서 좌표를 더해준다.
                        foreach (var point in weldGroup)
                        {
                            string sql = SqlStr_TB_POCINSTANCES_By_Point(point);
                            SQLiteCommand command = new SQLiteCommand(sql, conn);
                            SQLiteDataReader rdr = command.ExecuteReader();
                            int count = 0; //파이프객체가 몇개인지 카운트.
                            while (rdr.Read())
                            {
                                // 파이프인 객체의 오너아이디를 가져와서 연결된 POC정보를 가져온다.(2포인트가 나옴)
                                if (rdr["OWNER_TYPE"].ToString() == ownerType_Pipe)
                                {
                                    count++;
                                    List<Point3d> points = new List<Point3d>();
                                    string instance_id = BitConverter.ToString((byte[])rdr["OWNER_INSTANCE_ID"]).Replace("-", "");

                                    string sql_ins = SqlStr_TB_POINSTANCES_By_OWNER_INS_ID(instance_id);

                                    //Get_Pipe_Spool_Info_By_OwnerInsId 로 스풀정보.
                                    SQLiteCommand command_1 = new SQLiteCommand(sql_ins, conn);
                                    SQLiteDataReader rdr_1 = command_1.ExecuteReader();

                                    if (rdr_1.HasRows)
                                    {
                                        while (rdr_1.Read())
                                        {
                                            points.Add(new Point3d((double)rdr_1["POSX"], (double)rdr_1["POSY"], (double)rdr_1["POSZ"]));
                                        }
                                        if (points.Count == 2)
                                        {
                                            int index = 0; //맞대기 용접 인덱스
                                            foreach (var weldPoint in weldGroup)
                                            {
                                                //CAD좌표는 DDWORKS 좌표에서 4번째에서 반올림한 좌표.
                                                //오너 아이디에서 반환된 Points와 weldPoint가 일치하면 ^
                                                if (Math.Abs(weldPoint.X - points[0].X) < 0.5 && Math.Abs(weldPoint.Y - points[0].Y) < 0.5 && Math.Abs(weldPoint.Z - points[0].Z) < 0.5)
                                                {
                                                    Vector3d vec = (points[1] - points[0]).GetNormal();
                                                    vec_li.Add(vec);
                                                    //맞대기 용접일때처리. 2번째 파이프 객체를 찾았을때(256) 좌표의 인덱스에 해당하는 좌표를 넣어준다.
                                                    if (count == 2)
                                                    {
                                                        index_li.Add(index);
                                                    }
                                                }
                                                else if (Math.Abs(weldPoint.X - points[1].X) < 0.5 && Math.Abs(weldPoint.Y - points[1].Y) < 0.5 && Math.Abs(weldPoint.Z - points[1].Z) < 0.5)
                                                {
                                                    Vector3d vec = (points[0] - points[1]).GetNormal();
                                                    vec_li.Add(vec);
                                                    //맞대기 용접일때처리. 2번째 파이프 객체를 찾았을때(256) 좌표의 인덱스에 해당하는 좌표를 넣어준다.
                                                    if (count == 2)
                                                    {
                                                        index_li.Add(index);
                                                    }
                                                }
                                                index++;
                                            }
                                        }
                                    }

                                }
                                // WeldGroup의 좌표를 받아서 Group 중간점 구하기 파이프의 Vector의 방향대로 글자를 배치.
                            }
                        }

                        conn.Close();
                    }
                }
                return vec_li;
            }
            //Valve위치.
            public (List<Point3d>, List<string>) Get_Valve_Position_By_DDWDB()
            {
                List<Point3d> valve_Positions = new List<Point3d>();
                List<string> valve_Name = new List<string>();
                string connstr = "Data Source=" + db_path;
                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    conn.Open();
                    string sql = String.Format("SELECT distinct " + //중복된 결과값 제거(Valve한개당 POC2개이기 때문에) 
                    "DISPLAY_NM,PI.POSX,PI.POSY,PI.POSZ " +
                    "FROM " +
                        "TB_POCTEMPLATES as PT " +
                    "INNER JOIN " +
                        "TB_POCINSTANCES as PI , TB_MODELTEMPLATES as MT " +
                    "on " +
                        "PT.POC_TEMPLATE_ID = PI.POC_TEMPLATE_ID AND PT.MODEL_TEMPLATE_ID = MT.MODEL_TEMPLATE_ID " +
                    "AND " +
                        "OWNER_TYPE = '{0}' AND POC_TEMPLATE_NM like '%{1}%'", ownerType_Component, valveType);
                    SQLiteCommand comm = new SQLiteCommand(sql, conn);
                    SQLiteDataReader rdr = comm.ExecuteReader();
                    while (rdr.Read())
                    {
                        valve_Positions.Add(new Point3d((double)rdr["POSX"], (double)rdr["POSY"], (double)rdr["POSZ"]));
                        valve_Name.Add(rdr["DISPLAY_NM"].ToString());
                    }
                    rdr.Close();
                    conn.Dispose();
                }
                return (valve_Positions, valve_Name);
            }
            //Valve INSTANCE_ID에 연결된 파이프위치들을 반환한다.
            //CAD에서 이 좌표로 Pipe를 찾아 MidPoint에 있는 Text의 값을(PolyLine의 길이조정).
            public List<Point3d> Get_PipePoints_By_WithValve()
            {
                List<Point3d> pipePoints = new List<Point3d>();
                List<string> connectd_Poc_Id = new List<string>();
                string connstr = "Data Source=" + db_path;
                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    conn.Open();
                    string sql_ConId = String.Format(
                        "SELECT CONNECTED_POC_ID " +
                        "FROM TB_POCTEMPLATES as PT " +
                        "INNER JOIN TB_POCINSTANCES as PI " +
                        "on PT.POC_TEMPLATE_ID = PI.POC_TEMPLATE_ID " +
                        "AND OWNER_TYPE = '{0}' AND POC_TEMPLATE_NM like '%{1}%';", ownerType_Component, valveType);
                    SQLiteCommand comm = new SQLiteCommand(sql_ConId, conn);
                    SQLiteDataReader rdr = comm.ExecuteReader();
                    while (rdr.Read())
                    {
                        connectd_Poc_Id.Add(BitConverter.ToString((byte[])rdr["CONNECTED_POC_ID"]).Replace("-", ""));
                    }

                    foreach (var id in connectd_Poc_Id)
                    {
                        string sql_Pos = string.Format("SELECT POSX,POSY,POSZ FROM TB_POCINSTANCES WHERE OWNER_INSTANCE_ID=(SELECT OWNER_INSTANCE_ID FROM TB_POCINSTANCES WHERE hex(INSTANCE_ID) = '{0}');", id);
                        SQLiteCommand comm_Conn = new SQLiteCommand(sql_Pos, conn);
                        SQLiteDataReader rdr_ins = comm_Conn.ExecuteReader();
                        while (rdr_ins.Read())
                        {
                            Point3d point = new Point3d((double)rdr_ins["POSX"], (double)rdr_ins["POSY"], (double)rdr_ins["POSZ"]);
                            pipePoints.Add(point);
                        }
                    }
                    conn.Dispose();
                }
                return pipePoints;
            }
            // 중간 지점 그려주기. 배관 방향과 수평되게 그려주기.. 
            // 빈공간.. 찾기.. RAYTRAY.. 
            // 수평되게 그려주기 된다면 네모 그려서 그룹 아이디 
            // 4방향으로 Rec 회전 알고리즘 
            // EnterKey 누르면 거기에 파이프 정보. 
            /* --------------- [CLASS END] -------------------*/

        }
        public class keyFilter : IMessageFilter
        {
            public const int WM_KEYDOWN = 0x0100;
            public bool bCanceled = false;
            public bool bEntered = false;
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_KEYDOWN)
                {
                    // Check for the Escape keypress
                    Keys kc = (Keys)(int)m.WParam & Keys.KeyCode;
                    if (m.Msg == WM_KEYDOWN && kc == Keys.Escape)
                    {
                        bCanceled = true;
                    }
                    if (m.Msg == WM_KEYDOWN && kc == Keys.G)
                    {
                        bEntered = true;
                    }
                    // Return true to filter all keypresses
                    return true;
                }
                // Return false to let other messages through
                return false;
            }
        }
    }
    public class App : IExtensionApplication
    {
        static PipeInfo _palette;

        public void Initialize()
        {
            // ... other stuff
            // Create a hook on the "Idle" event of the application class
            Application.Idle += new EventHandler(Application_Idle);

        }

        public void Terminate()
        {
            Console.WriteLine("Cleaning up...");
        }
        void Application_Idle(object sender, EventArgs e)
        {

            // Remove the event handler as it is no longer needed
            Application.Idle -= Application_Idle;

          _palette = new PipeInfo();
          _palette.ui();

        }

   
    }

}

//단축키 Ctrl+K -> Ctrl+E
//Ctrl + M + O