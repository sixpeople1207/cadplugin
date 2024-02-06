using AutoCAD;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Database = Autodesk.AutoCAD.DatabaseServices.Database;
using Excel = Microsoft.Office.Interop.Excel;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using MessageBox = System.Windows.Forms.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

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

        //DWG 경로만으로 파일을 열어 도면 안에 있는 Block정보를 Copy하는 예제. 23.11.23 
        [CommandMethod("dwg")]
        public void ReadDWG()
        {
            DocumentCollection dm = Application.DocumentManager;
            Editor ed = dm.MdiActiveDocument.Editor;
            Database destDb = dm.MdiActiveDocument.Database;
            Database sourceDb = new Database(false, true);
            PromptResult sourceFileName;
            try
            {
                // Get name of DWG from which to copy blocks CAD에서 ""를 붙여 경로 입력
                sourceFileName =
                ed.GetString("\nEnter the name of the source drawing: ");
                // Read the DWG into a side database
                sourceDb.ReadDwgFile(sourceFileName.StringResult, System.IO.FileShare.Read, true, "");
                ObjectIdCollection blockIds = new ObjectIdCollection();
                Autodesk.AutoCAD.DatabaseServices.TransactionManager tm =
                sourceDb.TransactionManager;
                using (Transaction myT = tm.StartTransaction())
                {
                    // Open the block table
                    BlockTable bt =
                        (BlockTable)tm.GetObject(sourceDb.BlockTableId,
                                                OpenMode.ForRead,
                                                false);
                    // Check each block in the block table
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr =
                          (BlockTableRecord)tm.GetObject(btrId, OpenMode.ForRead, false);
                        // Only add named & non-layout blocks to the copy list
                        if (!btr.IsAnonymous && !btr.IsLayout)
                            blockIds.Add(btrId);
                        btr.Dispose();
                    }
                }
                // Copy blocks from source to destination database
                IdMapping mapping = new IdMapping();
                sourceDb.WblockCloneObjects(blockIds,
                                            destDb.BlockTableId,
                                            mapping,
                                            DuplicateRecordCloning.Replace,
                                            false);
                ed.WriteMessage("\nCopied "
                                + blockIds.Count.ToString()
                                + " block definitions from "
                                + sourceFileName.StringResult
                                + " to the current drawing.");

                foreach (var id in blockIds)
                {
                    ed.WriteMessage(id.ToString());
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage("\nError during copy: " + ex.Message);
            }
            sourceDb.Dispose();
        }

        [CommandMethod("ff")]
        public void selectFence()
        {
            if (db_path != "")
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;
                Application.SetSystemVariable("TEXTSTYLE", "arial");

                Dictionary<string, int> text_Dic = new Dictionary<string, int>()
                    {
                        {"text_height", 18 },
                        {"SideBetween_Dis", 40},
                        {"TopDownBetween_Dis", 36 }
                    };
                // 메뉴 만들기
                //Autodesk.Windows.RibbonControl ribbonControl
                //    = Autodesk.Windows.ComponentManager.Ribbon;
                //RibbonTab Tab = new RibbonTab();

                //Tab.Title = "Test Ribbon";
                //Tab.Id = "TESTRIBBON_TAB_ID";
                //ribbonControl.Tabs.Add(Tab);

                //클릭할 좌표점을 계속해서 입력받아 3D Collection으로 반환
                //Point3dCollection pointCollection = InteractivePolyLine.CollectPointsInteractive();
                //PromptSelectionResult prSelRes = ed.SelectFence(pointCollection);
                //Point3dCollection pc = new Point3dCollection();
                //TypedValue[] filter = { new TypedValue(0, "Polyline") };
                //SelectionFilter selFilter = new SelectionFilter(filter);
                PromptSelectionResult prSelRes = ed.GetSelection();

                List<Vector3d> vec_li = new List<Vector3d>(); // 파이프 라인의 진행방향.
                List<Polyline3d> li_PolyLines = new List<Polyline3d>(); // 파이프 객체 polyline3d
                string groupVecstr = "";

                if (prSelRes.Status == PromptStatus.OK)
                {
                    Point3dCollection pointCollection = new Point3dCollection();
                    bool isSpoolLine = false;
                    Polyline3d po_li = new Polyline3d();
                    Line li = new Line();
                    List<string> spoolInfo_li = new List<string>();
                    List<ObjectId> spoolTexts_objIDs = new List<ObjectId>();

                    using (Transaction acTrans = db.TransactionManager.StartTransaction())
                    {
                        BlockTable blk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord blkRec = acTrans.GetObject(blk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;


                        ed.WriteMessage("[Spool Information] Spool정보 기준라인을 그려주세요.\n");
                        // 23.12.19 pline에서 3dpoly로 변경
                        ed.Command("_.3dpoly");

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            //마지막 그린 객체 결정. 텍스트의 지시라인 폴리라인만 필터해서 잡아냄. 

                            Entity ent = (Entity)tr.GetObject(Utils.EntLast(), OpenMode.ForRead);
                            //마지막 그린 라인의 버텍스를 잡아내서 Vector를 알아낸다.
                            Type type = ent.GetType();

                            if (type.Name.ToString() == "Polyline3d")
                            {
                                po_li = (Polyline3d)ent;
                                isSpoolLine = true;
                                pointCollection.Add(po_li.StartPoint);
                                pointCollection.Add(po_li.EndPoint);
                            }
                            else
                            {
                                ed.WriteMessage("error : 마지막 객체가 'Polyline'이 아닙니다.");
                            }
                            tr.Commit();
                        }
                        //Polyline3d line = new Polyline3d(Poly3dType.SimplePoly, pointCollection, false);

                        //foreach (var point in pointCollection)
                        //{
                        //    ed.WriteMessage(point.ToString());
                        //}

                        //blkRec.AppendEntity(line);
                        //acTrans.AddNewlyCreatedDBObject(line, true);

                        // 선택한 파이프 라인들의 처리 
                        // 24.1.2 추가 그룹 백터는 이제 지시선 벡터로 대체. 
                        // 1. 라인들의 진행방향을 vec_li에 저장.
                        SelectionSet ss = prSelRes.Value;
                        ObjectId[] ss_ObjIds = ss.GetObjectIds();

                        // if 문 polyline3d이면 추가 필요.
                        foreach (ObjectId objId in ss_ObjIds)
                        {
                            Entity ent_2 = (Entity)acTrans.GetObject(objId, OpenMode.ForRead);
                            Type type_2 = ent_2.GetType();
                            Polyline3d po_li_2 = new Polyline3d();
                            if (type_2.Name.ToString() == "Polyline3d")
                            {
                                po_li_2 = (Polyline3d)ent_2;
                                li_PolyLines.Add(po_li_2);
                            }
                            //좌표 가져와서 벡터 vecli에 저장.
                        }

                        // 배관의 백터는 일반화하여 절대값으로 넣어준다. 벡터에 따라 텍스트의 방향이 결정된다.
                        Vector3d line_vec = new Vector3d();
                        if (li_PolyLines.Count > 0)
                        {
                            foreach (Polyline3d polyLine in li_PolyLines)
                            {
                                line_vec = polyLine.StartPoint.GetVectorTo(polyLine.EndPoint).GetNormal();
                                if (Math.Round(line_vec.GetNormal().X, 1) == 1 || Math.Round(line_vec.GetNormal().X, 1) == -1)
                                {
                                    vec_li.Add(new Vector3d(Math.Abs(line_vec.GetNormal().X), 0, 0));
                                }
                                if (Math.Round(line_vec.GetNormal().Y, 1) == 1 || Math.Round(line_vec.GetNormal().Y, 1) == -1)
                                {
                                    vec_li.Add(new Vector3d(0, Math.Abs(line_vec.GetNormal().Y), 0));
                                }
                                if (Math.Round(line_vec.GetNormal().Z, 1) == 1 || Math.Round(line_vec.GetNormal().Z, 1) == -1)
                                {
                                    vec_li.Add(new Vector3d(0, 0, Math.Abs(line_vec.GetNormal().Z)));
                                }
                            }
                        }

                        acTrans.Commit();
                    }

                    if (isSpoolLine == true)
                    {
                        /*-----------------------------------------DataBase Scope--------------------------------------------------
                         * 1. DB객체 생성 [O]
                         * 2. OBJ IDs (Fence to Selection return objIds) [O]
                         * 3. OBJ IDs to DB_Information(튜플로 DB InstanceID 적용) [O]
                         * 4. acDrawText 기능 구현.(3번에서 반환된 튜플 객체를 Fence EndPoint 에서 부터 시작해서 객체를 생성) [ㅁ]
                         * 5. OBJ IDs To Connected PipeInformation 구현 예정(OBJ IDs를 입력하면 전 후 PipeInformation). []
                         ----------------------------------------------------------------------------------------------------------*/


                        // 여기까지 GroupVec를 찾지 못했다면 지시선의 방향에 맞춰 진행한다. 추가 24.1.2

                        // 텍스트 중간 지시선
                        Point3d po_start_point = po_li.StartPoint;
                        var tControl = new TextControl();
                        Vector3d po_vec = po_start_point.GetVectorTo(po_li.EndPoint).GetNormal();

                        if (Math.Round(po_vec.GetNormal().X, 1) == 1 || Math.Round(po_vec.GetNormal().X, 1) == -1)
                        {
                            groupVecstr = "X";
                        }
                        if (Math.Round(po_vec.GetNormal().Y, 1) == 1 || Math.Round(po_vec.GetNormal().Y, 1) == -1)
                        {
                            groupVecstr = "Y";
                        }
                        if (Math.Round(po_vec.GetNormal().Z, 1) == 1 || Math.Round(po_vec.GetNormal().Z, 1) == -1)
                        {
                            groupVecstr = "Z";
                        }

                        ed.WriteMessage("파이프의 그룹 진행 방향은 {0}축입니다. 스풀정보의 기준선을 {0}축으로 그려주세요.", groupVecstr);
                        Order order = new Order();
                        List<Polyline3d> li_orderPolyline = new List<Polyline3d>();
                        li_orderPolyline = order.orderObjectByGroupVector(li_PolyLines, groupVecstr);

                        var pipeInfo_cls = new DDWorks_Database(db_path);
                        List<string> pipe_instance_IDs = pipeInfo_cls.Get_PipeInstanceIDs_By_ObjIDs(li_orderPolyline, groupVecstr);
                        List<Tuple<string, string>> pipe_Information_li = new List<Tuple<string, string>>();
                        List<string> pipeInfo_NotFind_Li = new List<string>();
                        spoolInfo_li = pipeInfo_cls.Get_Spool_Infomation_By_insIds(pipe_instance_IDs);
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
                        // 스풀 정보만 추출
                        //foreach (var info in pipe_Information_li)
                        //{
                        //    spoolInfo_li.Add(info.Item2);
                        //}

                        spoolTexts_objIDs = tControl.Draw_Text_WeldPoints_2(po_li, spoolInfo_li, vec_li, groupVecstr, text_Dic); // 라인 끝점을 입력받음.

                        List<Point3d> final_Point = new List<Point3d>();
                        //Fence Select 의 마지막 Point를 기준으로 Text
                        foreach (Point3d point in pointCollection)
                        {
                            final_Point.Add(point);
                        }

                        var pipe = new Pipe();
                        //배관의 Vector와 마지막 객체의 좌표도 필요. 좌표를 기준으로 Fence 좌표를 보정.
                        var pipe_Group_Vector = pipe.get_Pipe_Group_Vector(prSelRes);
                        //spoolTexts_objIDs = tControl.ed_Draw_Text_To_Line_Vector(pipe_Information_li, final_Point, 25, 12);


                        // FF (Fnece Selection) 모드에서는 끝단 객체일 가능성이 크기 때문에 라인 벡트의 반대 방향으로 글씨를 배치해서 끝단에 자연스럽게 배치한다.

                        int text_height = 0;
                        text_height = text_Dic["text_height"];

                        if (isSpoolLine == true)
                        {
                            //폴리라인 끝점부터 텍스트를 Vector에 맞게 배치한다. 
                            //회전 기능 안내 메세지
                            string message = "\n[Options] 회전 : G,좌우 반전 : Z,완료 : Esc(계속하려면 Enter)";
                            PromptStringOptions pSo = new PromptStringOptions(message);
                            PromptResult result = ed.GetString(pSo);
                            //ed.WriteMessage(result.ToString());

                            //Rotate기능 실행
                            keyFilter keyFilter = new keyFilter();
                            tControl.RotateFlip_Texts(vec_li, keyFilter, spoolTexts_objIDs, po_li, text_height, groupVecstr);

                        }
                        else
                        {
                            ed.WriteMessage("\n라인이 그려지지 않았습니다.");
                        }
                    }
                    else
                    {
                        ed.WriteMessage("\n라인이 그려지지 않았습니다.");
                    }
                    /************************************************* SS 기능 코드 부분 추가 *******************************************
                     end --- */
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
            //AUTOCAD.DLL이 없어서 주석처리함. 231206 본사에서 확인 필요
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
            try
            {
                if (prSelRes.Status == PromptStatus.OK)
                {
                    using (var lck = acDoc.LockDocument())
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
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.ToString());
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
                    PromptResult res = ed.GetString("[WIR 정보 추출] 화면에 도곽정보가 모두 있습니까?(Y or N):");
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
                                TypedValue[] typeValue = { new TypedValue(0, "Polyline") };
                                List<DBText> textAllLi = new List<DBText>();
                                SelectionFilter selFilter = new SelectionFilter(typeValue);
                                List<string> header = new List<string>()
                                { "설비", "PROJECT", "공정", "접수일", "관리번호","도면번호","배관사","모델러","제도사","용접번호" };
                                ExcelObject excel = new ExcelObject(header);
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

                    // 아래 내용은 UI로 변경가능하게 만들어 줘야함. 24.2.2
                    Application.SetSystemVariable("TEXTSTYLE", "arial");
                    int textFlip_Count = 0; // Z키를 누르면 텍스트를 뒤집는데 이를 카운트.
                    Dictionary<string, int> text_Dic = new Dictionary<string, int>()
                    {
                        {"text_height", 18 },
                        {"SideBetween_Dis", 40},
                        {"TopDownBetween_Dis", 36 }
                    };

                    ed.WriteMessage("\n[Spool Information] WeldPoint들을 선택해주세요.(CrossingWindow).");
                    List<Point3d> pFaceMeshPoints = Select.selectPolyFaceMeshToPoints();
                    if (pFaceMeshPoints.Count > 0)
                    {
                        string[] filter = { "Tee", "Reducer", "Reducing", "End Cap", "Regulator" };
                        var ddworks_Database = new DDWorks_Database(db_path);
                        // -> 필터 적용. 7.24 등등
                        // 선택한 용접포인트의 위치가 필터에 해당한다면 Point를 삭제.
                        List<Point3d> weldPoints_Filtered = ddworks_Database.FilterWeldGroup_By_ComponentType(pFaceMeshPoints, filter);

                        if (weldPoints_Filtered.Count > 0)
                        {
                            Vector3d groupVec = new Vector3d(0, 0, 0);
                            // 23.6.23 함수 추가 Get_Pipe_Vector_By_SpoolList와 거의 동일.. 조금 수정해야할 것 같다. 함수안에 함수로. Get_Pipe_Info하고 -> Vector, Spool, WELD맞대기좌표추가한 리스트 반환기능 등
                            List<Vector3d> vec = ddworks_Database.Get_Pipe_Vector_By_Points(weldPoints_Filtered);
                            // 마지막 POC 객체 위치와 Object Ids반환(중간 객체와 분리를 위해서 반환)
                            //(List<Point3d> final_poc_pos, List<string> final_poc_str) = ddworks_Database.Get_Final_POC_Instance_Ids();

                            // 마지막 객체의 삭제
                            // 마지막 객체만 있을때 제외

                            List<Point3d> remove_Pos_Li = new List<Point3d>();
                            //foreach (var final_poc in final_poc_pos)
                            //{
                            //    // Filter가 적용된 후의 값인 weldPoints의 값을 진행(pFaceMeshPoints -> weldPoints_Filtered)
                            //    // 버림을 하다보니 다른 좌표와 혼동되는 경우 발생한듯.. 하다. 3번째 자리까지 올림할까? 디버깅 필요. ESTPH53_PCW  231215 일
                            //    foreach (var points in weldPoints_Filtered)
                            //    {
                            //        if (Math.Abs(final_poc.X - points.X) < 1 && Math.Abs(final_poc.Y - points.Y) < 1 && Math.Abs(final_poc.Z - points.Z) < 1)
                            //        {
                            //            if (remove_Pos_Li.Contains(points) == false) remove_Pos_Li.Add(points);
                            //        }
                            //    }

                            //    // 마지막 객체 확인용 라인
                            //    //lid = new Line(new Point3d(final_poc.X, final_poc.Y, final_poc.Z), new Point3d(final_poc.X, final_poc.Y, final_poc.Z + 50));
                            //    //blkRec.AppendEntity(lid);
                            //    //tr.AddNewlyCreatedDBObject(lid,true);
                            //}

                            //ed.WriteMessage("지울애들 {0}", remove_Pos_Li.Count);
                            // weldPoints도 지워줘야한다.
                            // 마지막 POC 객체만 있을때는 지우지 않는다.
                            //if (remove_Pos_Li.Count != weldPoints_Filtered.Count)
                            //{
                            //    foreach (var pos in remove_Pos_Li)
                            //    {
                            //        pFaceMeshPoints.Remove(pos);
                            //        weldPoints_Filtered.Remove(pos);
                            //    }
                            //}
                            if (weldPoints_Filtered.Count != 0)
                            {
                                // 용접포인트의 GroupVector방향을 알아내고 포인트들을 정렬시킨다. 
                                // 만약 단일 배관들은 gourpVecstr값이 ""이 나올 수 있기때문에 이는 Text를 그려줄때(Draw_Text_WeldPoints) Line의 백터값을 따르기로 한다.
                                (List<Point3d> orderPoints, string groupVecstr) = Points.orderWeldPoints_By_GroupVector(weldPoints_Filtered, vec);

                                // SpoolTexts Base PolyLine
                                // weldPoints.의 중간지점을 잡아 지시선 라인을 그려준다. 
                                double averX = weldPoints_Filtered.Average(p => p.X);
                                double averY = weldPoints_Filtered.Average(p => p.Y);
                                double averZ = weldPoints_Filtered.Average(p => p.Z);

                                Point3d averPoint = new Point3d(averX, averY, averZ);
                                string commandLine = String.Format("{0},{1},{2}", averPoint.X, averPoint.Y, averPoint.Z);

                                ed.WriteMessage("\n[Spool Information] Spool정보 기준라인을 그려주세요.\n");
                                // 23.12.19 pline에서 3dpoly로 변경
                                ed.Command("_.3dpoly", commandLine);

                                bool isSpoolLine = false;
                                Polyline3d po_li = new Polyline3d();
                                Line li = new Line();

                                // Cmd5(averPoint);
                                using (Transaction tr = db.TransactionManager.StartTransaction())
                                {
                                    //마지막 그린 객체 결정. 폴리라인만 필터해서 잡아냄. 

                                    Entity ent = (Entity)tr.GetObject(Utils.EntLast(), OpenMode.ForRead);
                                    //마지막 그린 라인의 버텍스를 잡아내서 Vector를 알아낸다.
                                    Type type = ent.GetType();

                                    if (type.Name.ToString() == "Polyline3d")
                                    {
                                        po_li = (Polyline3d)ent;
                                        isSpoolLine = true;
                                    }
                                    else
                                    {
                                        ed.WriteMessage("error : 마지막 객체가 'Polyline'이 아닙니다.");
                                    }
                                    tr.Commit();
                                }

                                if (isSpoolLine == true)
                                {
                                    //정렬된 용접포인트를 받아 Database에서 스풀 리스트를 가져온다. 
                                    (List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints) = ddworks_Database.Get_Pipe_Vector_By_SpoolList(orderPoints);


                                    //폴리라인 끝점부터 텍스트를 Vector에 맞게 배치한다. 

                                    // 여기까지 GroupVec를 찾지 못했다면 지시선의 방향에 맞춰 진행한다. 추가 24.1.2
                                    if (groupVecstr == "")
                                    {
                                        Point3d po_start_point = po_li.StartPoint;
                                        Vector3d po_vec = po_start_point.GetVectorTo(po_li.EndPoint).GetNormal();
                                        if (Math.Round(po_vec.GetNormal().X, 1) == 1 || Math.Round(po_vec.GetNormal().X, 1) == -1)
                                        {
                                            groupVecstr = "X";
                                        }
                                        if (Math.Round(po_vec.GetNormal().Y, 1) == 1 || Math.Round(po_vec.GetNormal().Y, 1) == -1)
                                        {
                                            groupVecstr = "Y";
                                        }
                                        if (Math.Round(po_vec.GetNormal().Z, 1) == 1 || Math.Round(po_vec.GetNormal().Z, 1) == -1)
                                        {
                                            groupVecstr = "Z";
                                        }
                                    }


                                    List<ObjectId> spoolTexts_objIDs = tControl.Draw_Text_WeldPoints(po_li, spoolInfo_li, vec_li, newPoints, groupVecstr, text_Dic); // 라인 끝점을 입력받음.
                                    keyFilter keyFilter = new keyFilter();

                                    //SS 기능 안내 메세지
                                    string message = "\n[Options] 회전 : G,좌우 반전 : Z,완료 : Esc(계속하려면 Enter)";
                                    PromptStringOptions pSo = new PromptStringOptions(message);
                                    PromptResult result = ed.GetString(pSo);
                                    //ed.WriteMessage(result.ToString());

                                    //23.7.25 키를 입력받아 텍스트 회전
                                    //현재 뷰에 따라 디폴트값 정해야함
                                    //엔터키로 돌리고 SpoolGroup 방향 기준
                                    tControl.RotateFlip_Texts(vec_li, keyFilter, spoolTexts_objIDs, po_li, text_Dic["text_height"], groupVecstr);
                                }
                                else
                                {
                                    ed.WriteMessage("\n라인이 그려지지 않았습니다.");
                                }
                            }
                            else
                            {
                                ed.WriteMessage("\nError :웰딩 포인트 갯수가 0입니다.");
                            }
                        }
                        else
                        {
                            ed.WriteMessage("\nError :weldPoints_Filtered에서 값이 0입니다.");
                        }
                    }
                    else
                    {
                        ed.WriteMessage("\nError : Database에서 객체가 검색되지 않습니다");
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

        //미사용 삭제 예정
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
        [CommandMethod("vv")]
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

        /* 함수 이름 : components_Change
         * 기능 설명 : DB에 768인 객체들을 모두 가져온다. 
         * 명 령 어 : CC
         * 비 고 : 23.11.03 심볼 매칭 (기능 만드는중)
         */
        [CommandMethod("cc")]
        public void components_Change()
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
                            //DB에서 오너 아이디가 768인 애들만 가져온다.
                            //
                        }
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        // [CommandMethod("ui")]
        public void ui()
        {
            Autodesk.Windows.RibbonControl ribbonControl = Autodesk.Windows.ComponentManager.Ribbon;
            //RibbonControl >> RibbonTab >> RibbonPanel >> 
            RibbonTab tab = new RibbonTab();
            tab.Title = "DDWORKS";
            tab.Id = "Tab_ID";

            RibbonPanelSource panelSpool_Sor = new RibbonPanelSource();
            panelSpool_Sor.Title = "Spool Information";

            RibbonPanelSource panelWir_Sor = new RibbonPanelSource();
            panelWir_Sor.Title = "Layout Title Text";

            RibbonPanelSource panelMES_Sor = new RibbonPanelSource();
            panelMES_Sor.Title = "Spool Text";

            RibbonPanelSource panelSetting_Sor = new RibbonPanelSource();
            panelSetting_Sor.Title = "Setting";

            RibbonPanel panel = new RibbonPanel();
            RibbonPanel panel_wir = new RibbonPanel();
            RibbonPanel panel_Setting = new RibbonPanel();
            RibbonPanel panel_mes = new RibbonPanel();

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


            //RibbonCombo cmd = new RibbonCombo();
            //cmd.Name = "cmd1";
            //cmd.Id = "Mycmd1";
            //cmd.Text = "Template Size";
            //cmd.IsEnabled = true;
            //cmd.ShowText = true;
            //panelSpool_Sor.Items.Add(cmd);

            RibbonButton button = new RibbonButton();
            button.Orientation = Orientation.Vertical;
            button.Text = "Lines Distance\n(BB)";
            button.Id = "LineBetween_distance";
            button.Size = RibbonItemSize.Large;
            button.ShowText = true;
            button.ShowImage = true;
            button.LargeImage = Images.getBitmap(Properties.Resources.line);
            button.CommandParameter = "LineChecked";
            button.CommandHandler = new RibbonCommandHandler();
            panelSpool_Sor.Items.Add(button);

            RibbonButton button2 = new RibbonButton();
            button2.Orientation = Orientation.Vertical;
            button2.Size = RibbonItemSize.Large;
            button2.Id = "Spool_Information";
            button2.ShowImage = true;
            button2.ShowText = true;
            button2.LargeImage = Images.getBitmap(Properties.Resources.CLOUD);
            button2.Text = "Spool Text\n(SS)";
            button2.CommandHandler = new RibbonCommandHandler();
            panelSpool_Sor.Items.Add(button2);

            RibbonButton button3 = new RibbonButton();
            button3.Orientation = Orientation.Vertical;
            button3.Size = RibbonItemSize.Large;
            button3.Id = "Export_WIR";
            button3.ShowImage = true;
            button3.ShowText = true;
            button3.LargeImage = Images.getBitmap(Properties.Resources.excel);
            button3.Text = "WIR Excel\n(EE)";
            button3.CommandHandler = new RibbonCommandHandler();
            panelWir_Sor.Items.Add(button3);

            RibbonButton button4 = new RibbonButton();
            button4.Orientation = Orientation.Vertical;
            button4.Size = RibbonItemSize.Large;
            button4.Id = "Export_MES";
            button4.ShowImage = true;
            button4.ShowText = true;
            button4.LargeImage = Images.getBitmap(Properties.Resources.excel);
            button4.Text = "MES Excel\n(AA)";
            button4.CommandHandler = new RibbonCommandHandler();
            panelMES_Sor.Items.Add(button4);

            //RibbonTextBox textbox1 = new RibbonTextBox();
            //textbox1.Width = 100;
            //textbox1.IsEmptyTextValid = false;
            //textbox1.AcceptTextOnLostFocus = true;
            //textbox1.InvokesCommand = true;
            //textbox1.Size = RibbonItemSize.Standard;
            //textbox1.TextValue = "DDWorks CAD Ver1.4";
            //panelSetting_Sor.Items.Add(textbox1);

            //panel_Setting.Source = panelSetting_Sor;
            panel.Source = panelSpool_Sor;
            panel_wir.Source = panelWir_Sor;
            panel_mes.Source = panelMES_Sor;

            tab.Panels.Add(panel);
            tab.Panels.Add(panel_wir);
            tab.Panels.Add(panel_mes);
            //tab.Panels.Add(panel_Setting);
            ribbonControl.Tabs.Add(tab);
        }

        /* 기능 이름 : SA
         * 기능 설명 : 객체의 ObjectID를 출력해준다. 
         */

        [CommandMethod("sa")]
        public void get_handle_objId()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            using (Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                PromptSelectionOptions poOpt = new PromptSelectionOptions();
                PromptSelectionResult poRes = ed.GetSelection(poOpt);

                if (poRes.Status == PromptStatus.OK)
                {
                    SelectionSet ss = poRes.Value;
                    ObjectId[] objId = ss.GetObjectIds();
                    foreach (ObjectId id in objId)
                    {
                        Entity en = acTrans.GetObject(id, OpenMode.ForRead) as Entity;
                        Type type = en.GetType();
                        ed.WriteMessage("오브젝트 아이디 : {0}\n오브젝트 타입 : {1}\n타입 이름 : {2}\n", id, type, type.Name);

                    }
                }
            }
        }
        /* 기능 이름 : VA
       * 기능 설명 : Database내에 모든 컴포넌트의 위치를 읽어온다. 추후에는 심볼로 대체하기.
       */
        [CommandMethod("zz")]
        public void get_Components_Positions()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database db = acDoc.Database;
            DDWorks_Database ddworks_database = new DDWorks_Database(db_path);

            using (Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                BlockTable acBlkTbl;
                acBlkTbl = acTrans.GetObject(db.BlockTableId,
                                                OpenMode.ForRead) as BlockTable;

                // Open the Block table record Model space for write
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = acTrans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                OpenMode.ForWrite) as BlockTableRecord;

                List<Point3d> component_Positions = new List<Point3d>();
                List<Extents3d> blk_Positions = new List<Extents3d>();
                component_Positions = ddworks_database.Get_Components_Positions();
                List<ObjectId> allObjIds = GetallObjectIds();

                //테스트. 모든 블록 지우는 기능
                foreach (ObjectId id in allObjIds)
                {
                    Entity en = acTrans.GetObject(id, OpenMode.ForWrite) as Entity;
                    Type ty = en.GetType();
                    if (ty.Name == "BlockReference")
                    {
                        BlockReference bl = en as BlockReference;
                        blk_Positions.Add((Extents3d)bl.Bounds);
                        bl.Erase();
                        //BlockReference blkRef = (BlockReference)acTrans.GetObject(bl.BlockId, OpenMode.ForWrite);
                        //blkRef.Erase();           
                    }
                }

                blk_Positions = blk_Positions.OrderBy(p => p.MinPoint.X).ToList();
                component_Positions = component_Positions.OrderBy(p => p.X).ToList();

                // DB 기자재 위치와 도면 블록 위치 찾는 기능
                foreach (Point3d point in component_Positions)
                {
                    foreach (Extents3d bd in blk_Positions)
                    {
                        if (bd.MinPoint.X < point.X && bd.MinPoint.Y < point.Y && bd.MinPoint.Z < point.Z && bd.MaxPoint.X > point.X && bd.MaxPoint.Y > point.Y && bd.MaxPoint.Z > point.Z)
                        {
                            Zoom(bd.MinPoint, bd.MaxPoint, point, 1.0);
                            break;
                        }
                    }
                }
                acTrans.Commit();
            }
        }

        [CommandMethod("aa")]
        public void spool_Export()
        {
            try
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;
                Point3d first = new Point3d();
                Point3d second = new Point3d();
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    PromptPointOptions poOpt = new PromptPointOptions("\n[MES 정보추출] 도곽의 왼쪽 상단을 선택해주세요.");
                    poOpt.AllowNone = false;
                    PromptPointResult poRes = ed.GetPoint(poOpt);
                    if (poRes.Status == PromptStatus.OK)
                    {

                        first = poRes.Value;
                        //ed.WriteMessage(first.ToString());

                        PromptCornerOptions coOpt = new PromptCornerOptions("\n[MES 정보추출] 도곽의 오른쪽 하단을 선택해주세요.", first);
                        poRes = ed.GetCorner(coOpt);
                        second = poRes.Value;
                        //ed.WriteMessage(second.ToString());

                        PromptSelectionResult selSet = ed.SelectCrossingWindow(first, second);
                        SelectionSet sel = selSet.Value;
                        ObjectId[] objs = sel.GetObjectIds();
                        //스풀번호만 가져올 수 있는 정규식 진행중.. 
                        //Regex patten_A = new Regex(@"^[0-9A-Za-z]{1,3}_[A-Za-z0-9]{1,3}_[A-Za-z0-9]{1,10}_[A-Za-z0-9]{1,10}_[0-9]{1,3}$");
                        //Regex patten_B = new Regex(@"^[0-9A-Za-z]{1,3}_[A-Za-z0-9]{1,3}_[A-Za-z0-9]{1,10}-[A-Za-z0-9]{1,10}_[0-9]{1,3}$");
                        //Regex patten_C = new Regex(@"^[0-9A-Za-z]{1,3}_[A-Za-z0-9]{1,3}_[A-Za-z0-9]{1,3}_[A-Za-z0-9]{1,10}-[A-Za-z0-9]{1,10}_[0-9]{1,3}$");
                        //Regex patten_D = new Regex(@"^[0-9A-Za-z]{1,5}_[A-Za-z0-9]{1,10}_[A-Za-z0-9]{1,5}_[A-Za-z0-9]{1,10}_[A-Za-z0-9]{1,10}_[0-9]{1,3}$");
                        //Regex patten_E = new Regex(@"^[0-9A-Za-z]{1,3}_[A-Za-z]{1,3}_[A-Za-z]{1,3}_[A-Za-z0-9]{1,10}_[A-Za-z0-9]{1,10}_[0-9]{1,3}$");
                        //Regex patten_F = new Regex(@"^[0-9A-Za-z]{1,5}_[A-Za-z]{1,10}_[A-Za-z]{1,5}_[A-Za-z0-9]{1,10}_[A-Za-z0-9]{1,10}_[0-9]{1,3}$");
                        //Regex patten_G = new Regex(@"^[0-9A-Za-z]{1,5}_[A-Za-z]{1,10}_[A-Za-z]{1,5}_[A-Za-z0-9]{1,10}_[A-Za-z]{1,10}_[0-9]{1,3}$");
                        Regex patten_T = new Regex(@"^[0-9A]{1,3}");



                        ExcelObject excelObject = new ExcelObject();
                        int excel_write_count = 0;
                        foreach (var obj in objs)
                        {
                            Entity ent = acTrans.GetObject(obj, OpenMode.ForRead) as Entity;
                            Type type = ent.GetType();
                            if (type.Name == "DBText" || type.Name == "MText") //Mtext일때도 진행될 수 있게 추가 필요. 23.11.27일 제작도면 
                            {
                                string tx = "";
                                if (type.Name == "DBText")
                                {
                                    DBText text = (DBText)ent;
                                    tx = text.TextString;
                                }
                                else if (type.Name == "MText")
                                {
                                    MText text = (MText)ent;
                                    tx = text.Text;
                                }

                                string[] dash_split = tx.Split('-');
                                string[] underbar_split = tx.Split('_');

                                // 정규식에서 "-" , "_"를 포함한 Text와 첫 시작이 사이즈 "19A"인 Text를 검출하는 방법으로 변경(23.11.28)
                                if ((underbar_split.Length > 2 && dash_split.Length == 0 || underbar_split.Length > 2 && dash_split.Length > 0) && patten_T.IsMatch(underbar_split[0]))
                                {
                                    excelObject.excel_InsertData(1, 1, tx, true);
                                    excel_write_count += 1;
                                    //ed.WriteMessage("\n" + tx);
                                }
                                //if (patten_A.IsMatch(tx) || patten_B.IsMatch(tx) || patten_C.IsMatch(tx) || patten_D.IsMatch(tx) || patten_E.IsMatch(tx))
                                //{
                                //    excelObject.excel_InsertData(1, 1, tx, true);
                                //    ed.WriteMessage("\n"+ tx);
                                //}
                                //ed.WriteMessage(text.TextString.ToString());
                            }
                        }
                        ed.WriteMessage("\n[MES 정보추출] {0}개의 Spool 정보가 저장되었습니다.", excel_write_count.ToString());
                        excelObject.excel_save();
                        acTrans.Commit();
                        acTrans.Dispose();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.
                Editor.WriteMessage(ex.Message);
            }
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
                        case "Export_MES":
                            pi.spool_Export();
                            break;
                    }
                }
            }
        }

        /* --------------- [CLASS START]-------------------*/

        //이미지 관련 클래스
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

        //객체의 순서를 정렬
        public class Order
        {

            public List<Polyline3d> orderObjectByGroupVector(List<Polyline3d> pLine_li, string groupVector)
            {
                if (groupVector == "X")
                {
                    pLine_li = pLine_li.OrderByDescending(p => p.StartPoint.X).ToList();
                }
                else if (groupVector == "Y")
                {
                    pLine_li = pLine_li.OrderByDescending(p => p.StartPoint.Y).ToList();
                }
                else if (groupVector == "Z")
                {
                    pLine_li = pLine_li.OrderByDescending(p => p.StartPoint.Z).ToList();
                }
                return pLine_li;

            }
        }

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

                    // 차례대로 쓰기.
                    // 라인을 하나씩 돌면서 중간지점을 찾는다. 
                    // 치수의 크기와 방향을 지정한다. 
                    // Middle Point 위치에 치수를 적는다. 
                    if (groupVecs.Count == lines_Point.Count - 1)
                    {
                        for (int i = 1; i < lines_Point.Count; i++)
                        {
                            DBText text = new DBText();

                            // 훅업 라인의 시작점과 끝점을 나누어 라인의 중간지점을 찾는다.  
                            // 중간 지점이 곧 치수의 위치가 됨. 
                            Point3d dimText_Postion = new Point3d(
                                 lines_Point[0].StartPoint.X + lineVecs[0].X * Math.Abs(lines_Point[0].StartPoint.X - lines_Point[0].EndPoint.X) / 2,
                                 lines_Point[0].StartPoint.Y + lineVecs[0].Y * Math.Abs(lines_Point[0].StartPoint.Y - lines_Point[0].EndPoint.Y) / 2,
                                 lines_Point[0].StartPoint.Z + lineVecs[0].Z * Math.Abs(lines_Point[0].StartPoint.Z - lines_Point[0].EndPoint.Z) / 2);

                            // 치수의 크기와 회전을 지정.
                            text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 180, Vector3d.ZAxis, Point3d.Origin));
                            int text_Height = 18;
                            text.Height = text_Height;

                            // 라인 사이 간격 (빼는 순서 바꿈. 전 객체에서 현재 겍체를 빼주는게 더 정확함) 23.12.11
                            double distance = Math.Round(lines_Point[i - 1].StartPoint.Y - lines_Point[i].StartPoint.Y, 1);
                            Vector3d dis_Vector = (lines_Point[i - 1].StartPoint - lines_Point[i].StartPoint).GetNormal();
                            //ed.WriteMessage(dis_Vector.Y.ToString());

                            // 사이 간격 치수의 위치를 라인의 벡터에 따라 지정한다. 
                            //gouprvecstr X LineVec Y일때 정확히 맞음.
                            if (gorupVecStr == "X" && lineVecStr == "Z")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X + (groupVecs[0].X * 18.0),
                                lines_Point[i].StartPoint.Y,
                                dimText_Postion.Z);
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.X - lines_Point[i - 1].StartPoint.X, 1)).ToString();
                            }
                            else if (gorupVecStr == "X" && lineVecStr == "Y")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X - (Math.Round(dis_Vector.Y, 1) * (distance / 2)) + (text_Height / 2),
                                dimText_Postion.Y,
                                lines_Point[i].StartPoint.Z);
                                text.TextString = Math.Abs(distance).ToString();
                            }
                            else if (gorupVecStr == "Y" && lineVecStr == "Z")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X,
                                // 그룹 벡터의 반대 방향이니 -마이너스를 적용. 그룹 벡터는 ABS적용해서 무조건 양수 마이너스 방향이 필요함으로 linevecs를 사용.
                                // 라인의 간격에 바로 배치하면 글씨크기가 적용이 되지 않기 때문에 Text의 크기와 라인 사이를 더해서 /2를 해준다. 그래야 정중앙으로 배치된 것 처럼 보임. 
                                lines_Point[i].StartPoint.Y - (Math.Round(dis_Vector.Y, 1) * (distance / 2)) + (text_Height / 2),
                                 dimText_Postion.Z
                                );
                                text.TextString = Math.Abs(distance).ToString();
                            }
                            else if (gorupVecStr == "Y" && lineVecStr == "X")
                            {
                                text.Position = new Point3d(
                                 dimText_Postion.X,
                                lines_Point[i].StartPoint.Y,
                                lines_Point[i].StartPoint.Z
                                );
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.Y - lines_Point[i - 1].StartPoint.Y, 1)).ToString();
                            }
                            else if (gorupVecStr == "Z" && lineVecStr == "Y")
                            {
                                text.Position = new Point3d(
                                lines_Point[i].StartPoint.X,
                                dimText_Postion.Y,
                                lines_Point[i].StartPoint.Z);
                                text.TextString = Math.Abs(Math.Round(lines_Point[i].StartPoint.Z - lines_Point[i - 1].StartPoint.Z, 1)).ToString();
                            }
                            else if (gorupVecStr == "Z" && lineVecStr == "X")
                            {
                                text.Position = new Point3d(
                                    dimText_Postion.X,
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
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                wb = excelApp.Workbooks.Add();
                ws = wb.Worksheets.get_Item(1) as Excel.Worksheet;
            }
            public ExcelObject(List<string> header)
            {
                //list를 받아서 넣는것 하나 만들고 아예 헤더가 없는것도 하나 해야할 것 같다 23.10.25

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
                        dlg.Filter = "EXCEL 파일(*.xlsx)|*.xlsx";
                        dlg.FileName = "제목없음" + ".xlsx";
                        if (dlg.ShowDialog() == DialogResult.Cancel) return;
                        DateTime currentTime = DateTime.Now;
                        //MessageBox.Show(dlg.FileName.ToString());
                        wb.SaveAs(dlg.FileName, XlFileFormat.xlWorkbookDefault);
                        MessageBox.Show("저장되었습니다.");
                        wb.Close(true);
                        excelApp.Quit();
                    }
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message);
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

                PromptPointOptions pointOptions = new PromptPointOptions("\n 라인간격을 측정할 라인들을 선택(포인트 점2P): ")
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
            public List<ObjectId> ed_Draw_Text_To_Line_Vector(List<Tuple<string, string>> pipe_Information_li, List<Point3d> line_final_Points, int textDisBetween, int textSize)
            {
                List<ObjectId> text_ObjectIDs = new List<ObjectId>();
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
                        text_ObjectIDs.Add(acText.ObjectId);
                        acTrans.AddNewlyCreatedDBObject(acText, true);
                    }
                    acTrans.Commit();
                    acTrans.Dispose();
                }
                return text_ObjectIDs;
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
                         */
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
            public void RotateFlip_Texts(List<Vector3d> vec_li, keyFilter keyFilter, List<ObjectId> spoolTexts_objIDs, Polyline3d po_li, int text_height, string groupVecstr)
            {
                System.Windows.Forms.Application.AddMessageFilter(keyFilter);

                int textFlip_Count = 0;
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
                            foreach (var id in spoolTexts_objIDs)
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
                            keyFilter.bEntered = false;
                        }
                    }
                    // Text의 위치를 가져온다.
                    // AutoCAD에서는 Left정렬일때는 AlignmentPoint에 좌표를 저장 나머지는 Position에서 저장.
                    List<Point3d> textPositions = new List<Point3d>();
                    List<Extents3d> textBoundLi = new List<Extents3d>();
                    // 텍스트의 본래의 좌표를 저장.
                    using (Transaction actras = db.TransactionManager.StartTransaction())
                    {
                        for (int i = 0; i < spoolTexts_objIDs.Count; i++)
                        {
                            DBText textA = actras.GetObject(spoolTexts_objIDs[i], OpenMode.ForWrite) as DBText;
                            if (textA.HorizontalMode != TextHorizontalMode.TextLeft)
                            {
                                textPositions.Add(textA.AlignmentPoint);
                                textBoundLi.Add(textA.Bounds.Value);
                            }
                            else
                            {
                                textPositions.Add(textA.Position);
                                textBoundLi.Add(textA.Bounds.Value);
                            }
                        }
                    }

                    // 글씨 좌우 뒤집기 좌표를 바꾸기 때문에 본래의 좌표를 이용하여 제자리를 찾아준다.
                    if (keyFilter.bZaxis == true)
                    {
                        if (spoolTexts_objIDs.Count != 0)
                        {
                            using (Transaction actras = db.TransactionManager.StartTransaction())
                            {
                                DBText text = actras.GetObject(spoolTexts_objIDs[0], OpenMode.ForWrite) as DBText;
                                // 마지막 그린 라인의 벡터 23.12.27작업중.. 
                                Vector3d poli_vec = po_li.EndPoint - po_li.StartPoint;
                                List<Extents3d> bound_li = new List<Extents3d>();

                                for (int i = 0; i < spoolTexts_objIDs.Count; i += 1)
                                {
                                    DBText textA = actras.GetObject(spoolTexts_objIDs[i], OpenMode.ForWrite) as DBText;

                                    Extents3d bound = textBoundLi[i];
                                    Point3d min = bound.MinPoint;
                                    Point3d max = bound.MaxPoint;

                                    Point3d posA = textA.Position;
                                    Point3d aligA = textA.AlignmentPoint;

                                    //ed.WriteMessage("min:{0}\nmax:{1}", min, max);

                                    if (aligA.X == 0 || aligA.Y == 0 || aligA.Z == 0)
                                    {
                                        aligA = min;
                                    }

                                    TextHorizontalMode horA = textA.HorizontalMode;
                                    AttachmentPoint justifyA = textA.Justify;

                                    textA.SetDatabaseDefaults();
                                    if (textFlip_Count % 2 == 0)
                                    {
                                        if (textA.HorizontalMode == TextHorizontalMode.TextRight)
                                        {
                                            textA.Justify = AttachmentPoint.BaseLeft;
                                        }
                                        else
                                        {
                                            textA.Justify = AttachmentPoint.BaseRight;
                                        }

                                    }

                                    // Text의 Min Max와 TextHeight의 오차가 존재함으로 오차 값을 구해서 TextHeight값에 적용.
                                    double textTerm_X = Math.Abs((max.X - min.X) - text_height);
                                    double textTerm_Y = Math.Abs((max.Y - min.Y) - text_height);
                                    double textTerm_Z = Math.Abs((max.Z - min.Z) - text_height);
                                    //ed.WriteMessage("\ntext크기 " + text_height.ToString());
                                    //ed.WriteMessage("\n의 차이 "+ textTerm_X.ToString());


                                    // 그룹 벡터에 따른 글씨의 회전값 적용. 
                                    // 글씨의 Flip 할때 글씨의 좌표값을 Bound에 Max로 적용시 Bound바깥으로 넘어가기때문에 글씨 크기만큼 빼준다.(1017에서 부호 결정)
                                    if (groupVecstr == "X")
                                    {
                                        max = new Point3d(max.X - (text_height - textTerm_X), max.Y, max.Z);
                                        textA.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 180, Vector3d.XAxis, Point3d.Origin));
                                    }
                                    else if (groupVecstr == "Y")
                                    {
                                        max = new Point3d(max.X, max.Y - (text_height - textTerm_Y), max.Z);
                                        textA.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 180, Vector3d.YAxis, Point3d.Origin));
                                    }
                                    else if (groupVecstr == "Z")
                                    {
                                        max = new Point3d(max.X, max.Y, max.Z - (text_height - textTerm_Z));
                                        textA.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 180, Vector3d.ZAxis, Point3d.Origin));
                                    }
                                    // 배관의 Spool Vector에 따라 기준점 바꾸기.
                                    // 글씨의 회전값 적용
                                    // 라인의 끝점부터 그리기.
                                    // 텍스트의 Max Bound 값에서 글씨 값을 빼주거나(지시선 Vec가 +) 더해주거나(지시선 Vec -) 진행.

                                    // 2번째 부터는 제자리로 찾아와야 함으로 미리 저장해놨던 좌표로 지정.
                                    if (textFlip_Count % 2 == 0)
                                    {
                                        if (textA.HorizontalMode != TextHorizontalMode.TextLeft)
                                        {
                                            textA.AlignmentPoint = textPositions[i]; //Right일때만 AligmentPoint에 Min값을
                                        }
                                        else
                                        {
                                            textA.Position = textPositions[i];
                                        }
                                    }
                                    else
                                    {
                                        if (textA.HorizontalMode != TextHorizontalMode.TextLeft)
                                        {
                                            textA.AlignmentPoint = min; //Right일때만 AligmentPoint에 Min값을
                                        }
                                        else
                                        {
                                            textA.Position = max;
                                        }
                                    }
                                }

                                ed.Regen();
                                actras.Commit();
                                keyFilter.bZaxis = false;
                                textFlip_Count += 1;
                            }
                        }
                        else
                        {
                            ed.WriteMessage("선택된 객체가 없습니다.");
                        }
                    }
                }
                // We're done - remove the message filter
                System.Windows.Forms.Application.RemoveMessageFilter(keyFilter);
            }
            // 기능 설명 : 지시선과 스플정보들을 배치하고, 스풀정보들 사이에 지시선과 연장선을 그린다.
            // 반환 값 : 스풀정보의 AutoCAD상의 Text객체의 ObjectIds를 반환.(회전이나 속성 정보 바꾸는데 사용)
            // line의 속성을 Line에서 Polyline으로 변경(23.12.07) 
            public List<ObjectId> Draw_Text_WeldPoints(Polyline3d line, List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints, string groupVecstr, Dictionary<string, int> text_Dic)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    DocumentCollection doc = Application.DocumentManager;
                    Editor ed = doc.MdiActiveDocument.Editor;
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    List<Point3d> text_Positions_Li = new List<Point3d>();
                    List<ObjectId> drawText_spoolTexts_objIDs = new List<ObjectId>(); //반환값
                    Point3d line_finalPoint = new Point3d();

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
                                Vector3d line_vec = line.StartPoint - line.EndPoint;

                                //예외처리 23.12.12 groupVecstr값이 없다면 (단일배관일 경우 값이 없음) 지시선의 벡터를 따른다.
                                if (groupVecstr == "")
                                {
                                    if (line_vec.GetNormal().X == 1 || line_vec.GetNormal().X == -1)
                                    {
                                        groupVecstr = "X";
                                    }
                                    if (line_vec.GetNormal().Y == 1 || line_vec.GetNormal().Y == -1)
                                    {
                                        groupVecstr = "Y";
                                    }
                                    if (line_vec.GetNormal().Z == 1 || line_vec.GetNormal().Z == -1)
                                    {
                                        groupVecstr = "Z";
                                    }
                                }

                                near_Points.Add(new Tuple<int, Point3d>(group_index, newPoints[0]));
                                for (int i = 0; i < newPoints.Count; i++)
                                {
                                    // 용접 포인트의 Area별 그룹을 선택하기 위해 Tuple 자료형(중복키)
                                    // newPoints를 서로 다른 인덱스로 탐색 i , j
                                    // Group_index : Group Key로 구분
                                    // key 배열 : i와 가까운 포인트의 배열의 인덱스들을 저장. 
                                    // key 배열 : i가 중복 탐색하는 것을 방지
                                    if (key.Contains(i) == false)
                                    {
                                        for (int j = 1; j < newPoints.Count; j++)
                                        {
                                            if (key.Contains(j) == false)
                                            {
                                                var dis = newPoints[i].DistanceTo(newPoints[j]);
                                                if (dis < 300)
                                                {
                                                    key.Add(j);
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
                                lineVec = new Vector3d(Math.Round(lineVec.X, 1), Math.Round(lineVec.Y, 1), Math.Round(lineVec.Z, 1));

                                //ed.WriteMessage("\n라인 벡터의 방향{0}", line_vec);

                                int text_SideBetween_Dis = 0;
                                int text_TopDownBetween_Dis = 0;
                                int textTrans_Ang = 90;

                                text_SideBetween_Dis = text_Dic["SideBetween_Dis"];
                                text_TopDownBetween_Dis = text_Dic["TopDownBetween_Dis"];

                                Point3d befor_TextPosition = new Point3d();

                                //스풀의 그룹방향
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
                                        text.Height = text_Dic["text_height"];
                                        int nCnt = 0;

                                        Point3d textPosition = new Point3d();

                                        if (k > 0)
                                        {
                                            string[] beforeText = spoolInfo_li[k - 1].Split('_');
                                            string[] afterText = spoolInfo_li[k].Split('_');
                                            if (beforeText.Count() > 2 && afterText.Count() > 2)
                                            {
                                                if (beforeText[beforeText.Count() - 2] != afterText[beforeText.Count() - 2])
                                                {
                                                    basePoint -= text_TopDownBetween_Dis;
                                                }
                                            }
                                            else
                                            {
                                                ed.WriteMessage("\n스풀 정보에 끝단 객체가 포함되어 있습니다.");
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
                                            else if (groupVecstr == "Z") textPosition = new Point3d(textPosition.X, textPosition.Y, textPosition.Z - text_TopDownBetween_Dis);
                                        }

                                        //Text 위치 저장을 위해 TextPosition도 리스트에 넣어준다.
                                        text.Position = textPosition;

                                        befor_TextPosition = new Point3d(textPosition.X, textPosition.Y, textPosition.Z);
                                        text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                        if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                        {
                                            text.AlignmentPoint = textPosition;
                                        }

                                        text_Positions_Li.Add(textPosition);
                                        acBlkRec.AppendEntity(text);
                                        drawText_spoolTexts_objIDs.Add(text.ObjectId);
                                        acTrans.AddNewlyCreatedDBObject(text, true);
                                    }
                                }
                            }
                            else
                            {
                                ed.WriteMessage("\nError : 파이프 정보가 없습니다.");
                            }
                        }
                        else
                        {
                            ed.WriteMessage("\nError : Weldpoint Text정보가 없습니다.");
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    //***************************  텍스트 사이 라인그리기 *******************************
                    try
                    {
                        int vec_X_count = 0;
                        int vec_Y_count = 0;
                        int vec_Z_count = 0;
                        string line_vectors = "";

                        //라인의 벡터방향 같은지 확인
                        foreach (var vec in vec_li)
                        {
                            if (Math.Round(vec.GetNormal().X, 1) == 1 || Math.Round(vec.GetNormal().X, 1) == -1)
                            {
                                vec_X_count += 1;
                            }
                            else if (Math.Round(vec.GetNormal().Y, 1) == 1 || Math.Round(vec.GetNormal().Y, 1) == -1)
                            {
                                vec_Y_count += 1;
                            }
                            else if (Math.Round(vec.GetNormal().Z, 1) == 1 || Math.Round(vec.GetNormal().Z, 1) == -1)
                            {
                                vec_Z_count += 1;
                            }
                        }
                        // 배관 라인의 벡터 중에 가장 많은 벡터가 무엇인지 판단.
                        if (vec_X_count > vec_Y_count && vec_X_count > vec_Z_count)
                        {
                            line_vectors = "X";
                        }
                        else if (vec_Y_count > vec_X_count && vec_Y_count > vec_Z_count)
                        {
                            line_vectors = "Y";
                        }
                        else if (vec_Z_count > vec_X_count && vec_Z_count > vec_Y_count)
                        {
                            line_vectors = "Z";
                        }
                        else
                        {
                            ed.WriteMessage("라인의 스풀 방향을 알 수 없습니다. 같은 방향의 스풀을 선택해 주세요.");
                            //ed.WriteMessage("X{0} Y{1} Z{2}", vec_X_count, vec_Y_count, vec_Z_count);
                        }


                        // 이 Vector가 부호의 기준이 된다.

                        // 1. 지시선의 벡터 방향.
                        Line text_between_li = new Line();
                        Point3d start_point = new Point3d();
                        Point3d end_point = new Point3d();
                        int text_start_dis = 5; // 텍스트와 텍스트사이 라인과 거리
                        Line center_line = new Line();


                        if (text_Positions_Li.Count > 1) // TEXT가 하나 이상일때 적용
                        {
                            //최대값으로 정렬 필요. 
                            text_Positions_Li = text_Positions_Li.OrderBy(p => p.Y).OrderBy(p => p.X).OrderBy(p => p.Z).ToList();


                            for (int i = 0; i < text_Positions_Li.Count; i += 1)
                            {
                                Vector3d lineTo_Text_vec = text_Positions_Li[i] - line.EndPoint;

                                // 텍스트 사이의 라인을 하나씩 그려준다. 반개씩 -- 한쪽이 없으면 안그려주기 위함.
                                // 라인 끝점 구하는 알고리즘은 반복문에서 빼줘도 될 것 같다. 23.12.07
                                if (line_vectors == "X" && groupVecstr == "Y")
                                {

                                    // 사이 라인 표시
                                    /* 3. 텍스트 사이 선 그리기.
                                    * 라인 방향과 라입 스풀 방향별로 그리기를 구현.
                                    * 지시선의 좌우로 Vector방향을 찾아서 텍스트의 포지션부터 지시선까지 라인을 그린다. 5는 글씨좌표에서 살짝 떨어져 라인을 그리기 위함.
                                    * */

                                    if (lineTo_Text_vec.X > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X - text_start_dis, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.X < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X + text_start_dis, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                //위 코드 반복(더해주는 위치만 바꾸면서) => 함수로 만들어서 진행 필요 23.12.01
                                else if (line_vectors == "Y" && groupVecstr == "X")
                                {
                                    // 사이 라인 표시
                                    if (lineTo_Text_vec.Y > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Y < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y + 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "Z" && groupVecstr == "Y")
                                {

                                    if (lineTo_Text_vec.Z > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Z < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z + 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "Z" && groupVecstr == "X")
                                {
                                    if (lineTo_Text_vec.Z > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Z < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z + 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "Y" && groupVecstr == "Z")
                                {

                                    if (lineTo_Text_vec.Y > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Y < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y + 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "X" && groupVecstr == "Z")
                                {

                                    if (lineTo_Text_vec.X > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X - 5, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.X < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X + 5, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("\n스풀의 진행방향을 찾지 못했습니다.");
                                    break;
                                }

                                acBlkRec.AppendEntity(text_between_li);
                                acTrans.AddNewlyCreatedDBObject(text_between_li, true);

                                // 텍스트 중앙 지시선을 그리고 사용자가 그려준 마지막 라인과 조인해서 한 객체로 만든다. 
                                if (i == 0)
                                {
                                    center_line = new Line(line.EndPoint, text_between_li.StartPoint);
                                    Entity en = acTrans.GetObject(line.ObjectId, OpenMode.ForWrite) as Entity;
                                    en.UpgradeOpen();
                                    en.JoinEntity(center_line);
                                }
                                else if (i == text_Positions_Li.Count - 1)
                                {
                                    center_line = new Line(line.EndPoint, text_between_li.StartPoint);
                                    Entity en = acTrans.GetObject(line.ObjectId, OpenMode.ForWrite) as Entity;
                                    en.UpgradeOpen();
                                    en.JoinEntity(center_line);
                                }
                            }
                            //마지막 그리는 라인은 바로 전 라인(인풋으로 들어온 라인)과 조인해준다.

                        }
                        acTrans.Commit();
                        acTrans.Dispose();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    return drawText_spoolTexts_objIDs;
                }
            }
            public List<ObjectId> Draw_Text_WeldPoints_2(Polyline3d line, List<string> spoolInfo_li, List<Vector3d> vec_li, string groupVecstr, Dictionary<string, int> text_Dic)
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    DocumentCollection doc = Application.DocumentManager;
                    Editor ed = doc.MdiActiveDocument.Editor;
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    List<Point3d> text_Positions_Li = new List<Point3d>();
                    List<ObjectId> drawText_spoolTexts_objIDs = new List<ObjectId>(); //반환값
                    try
                    {
                        // 웰딩 포인트에 연결된 파이프를 찾아 Vector방향을 알아낸다.
                        // Spool 정보도 같이 불러온다. (맞대기 용접은 좌표를 더해서 반환)
                        //(List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints) = ddworks_Database.Get_Pipe_Vector_By_SpoolList(orderPoints);
                        // WeldPoint들과 최소 거리에 있는 (현재는 300) WeldPoint들을 모두 그룹으로 묶는다.

                        List<Tuple<int, Point3d>> near_Points = new List<Tuple<int, Point3d>>();
                        List<Point3d> point_Groups = new List<Point3d>();
                        List<int> key = new List<int>();

                        Vector3d line_vec = line.StartPoint - line.EndPoint;

                        //예외처리 23.12.12 groupVecstr값이 없다면 (단일배관일 경우 값이 없음) 지시선의 벡터를 따른다.
                        if (groupVecstr == "")
                        {
                            if (line_vec.GetNormal().X == 1 || line_vec.GetNormal().X == -1)
                            {
                                groupVecstr = "X";
                            }
                            if (line_vec.GetNormal().Y == 1 || line_vec.GetNormal().Y == -1)
                            {
                                groupVecstr = "Y";
                            }
                            if (line_vec.GetNormal().Z == 1 || line_vec.GetNormal().Z == -1)
                            {
                                groupVecstr = "Z";
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

                        Vector3d lineVec = new Vector3d();
                        lineVec = line.StartPoint.GetVectorTo(line.EndPoint).GetNormal();

                        //ed.WriteMessage("\n라인 벡터의 방향{0}", line_vec);

                        int text_SideBetween_Dis = 0;
                        int text_TopDownBetween_Dis = 0;
                        int textTrans_Ang = 90;

                        text_SideBetween_Dis = text_Dic["SideBetween_Dis"];
                        text_TopDownBetween_Dis = text_Dic["TopDownBetween_Dis"];

                        Point3d befor_TextPosition = new Point3d();

                        //스풀의 그룹방향에 따른 글씨의 베이스 포인트 
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
                        // Vector 에 따른 TEXT의 회전값과 정렬.
                        // 유의 : text.Normal = Vector3d.ZAxis; 은 꼭 text.Position 앞에 지정을 한다. 
                        // 이아래 기능을 Text Control Class에 들어가야한다. DrawText_BY_Vector로..Veclist와 PointList
                        for (int k = 0; k < spoolInfo_li.Count; k++) //Count와 밑에 VecLi가 같지 않을때 에러 // FF기능에서 선택된 모든 파이프의 정보를 찾아오니 중복객체 삭제 필요
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
                            text.Height = text_Dic["text_height"];
                            int nCnt = 0;

                            Point3d textPosition = new Point3d();

                            if (k > 0)
                            {
                                //string[] beforeText = spoolInfo_li[k - 1].Split('_');
                                //string[] afterText = spoolInfo_li[k].Split('_');
                                //if (beforeText.Count() > 2 && afterText.Count() > 2)
                                //{
                                //    if (beforeText[beforeText.Count() - 2] != afterText[beforeText.Count() - 2])
                                //    {
                                basePoint -= text_TopDownBetween_Dis;
                                //    }
                                //}
                                //else
                                //{
                                //    ed.WriteMessage("\n스풀 정보에 끝단 객체가 포함되어 있습니다.");
                                //}
                            }

                            //if (k % 2 != 0 && k != 0)
                            //{
                            //    basePoint -= text_TopDownBetween_Dis;
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
                                else if (groupVecstr == "Z") textPosition = new Point3d(textPosition.X, textPosition.Y, textPosition.Z - text_TopDownBetween_Dis);
                            }
                            //Text 위치 저장을 위해 TextPosition도 리스트에 넣어준다.
                            text.Position = textPosition;

                            befor_TextPosition = new Point3d(textPosition.X, textPosition.Y, textPosition.Z);
                            text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                            if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                            {
                                text.AlignmentPoint = textPosition;
                            }
                            text_Positions_Li.Add(textPosition);
                            acBlkRec.AppendEntity(text);
                            drawText_spoolTexts_objIDs.Add(text.ObjectId);
                            acTrans.AddNewlyCreatedDBObject(text, true);
                        }


                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    //***************************  텍스트 사이 라인그리기 *******************************
                    try
                    {
                        int vec_X_count = 0;
                        int vec_Y_count = 0;
                        int vec_Z_count = 0;
                        string line_vectors = "";

                        //라인의 벡터방향 같은지 확인
                        foreach (var vec in vec_li)
                        {
                            if (Math.Round(vec.GetNormal().X, 1) == 1 || Math.Round(vec.GetNormal().X, 1) == -1)
                            {
                                vec_X_count += 1;
                            }
                            else if (Math.Round(vec.GetNormal().Y, 1) == 1 || Math.Round(vec.GetNormal().Y, 1) == -1)
                            {
                                vec_Y_count += 1;
                            }
                            else if (Math.Round(vec.GetNormal().Z, 1) == 1 || Math.Round(vec.GetNormal().Z, 1) == -1)
                            {
                                vec_Z_count += 1;
                            }
                        }
                        // 배관 라인의 벡터 중에 가장 많은 벡터가 무엇인지 판단.
                        if (vec_X_count > vec_Y_count && vec_X_count > vec_Z_count)
                        {
                            line_vectors = "X";
                        }
                        else if (vec_Y_count > vec_X_count && vec_Y_count > vec_Z_count)
                        {
                            line_vectors = "Y";
                        }
                        else if (vec_Z_count > vec_X_count && vec_Z_count > vec_Y_count)
                        {
                            line_vectors = "Z";
                        }
                        else
                        {
                            ed.WriteMessage("라인의 스풀 방향을 알 수 없습니다. 같은 방향의 스풀을 선택해 주세요.");
                            //ed.WriteMessage("X{0} Y{1} Z{2}", vec_X_count, vec_Y_count, vec_Z_count);
                        }


                        // 이 Vector가 부호의 기준이 된다.

                        // 1. 지시선의 벡터 방향.
                        Line text_between_li = new Line();
                        Point3d start_point = new Point3d();
                        Point3d end_point = new Point3d();
                        int text_start_dis = 5; // 텍스트와 텍스트사이 라인과 거리
                        Line center_line = new Line();


                        if (text_Positions_Li.Count > 1) // TEXT가 하나 이상일때 적용
                        {
                            //최대값으로 정렬 필요. 
                            text_Positions_Li = text_Positions_Li.OrderBy(p => p.Y).OrderBy(p => p.X).OrderBy(p => p.Z).ToList();


                            for (int i = 0; i < text_Positions_Li.Count; i += 1)
                            {
                                Vector3d lineTo_Text_vec = text_Positions_Li[i] - line.EndPoint;

                                // 텍스트 사이의 라인을 하나씩 그려준다. 반개씩 -- 한쪽이 없으면 안그려주기 위함.
                                // 라인 끝점 구하는 알고리즘은 반복문에서 빼줘도 될 것 같다. 23.12.07
                                if (line_vectors == "X" && groupVecstr == "Y")
                                {
                                    // 사이 라인 표시
                                    /* 3. 텍스트 사이 선 그리기.
                                    * 라인 방향과 라입 스풀 방향별로 그리기를 구현.
                                    * 지시선의 좌우로 Vector방향을 찾아서 텍스트의 포지션부터 지시선까지 라인을 그린다. 5는 글씨좌표에서 살짝 떨어져 라인을 그리기 위함.
                                    * */
                                    if (lineTo_Text_vec.X > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X - text_start_dis, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.X < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X + text_start_dis, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                //위 코드 반복(더해주는 위치만 바꾸면서) => 함수로 만들어서 진행 필요 23.12.01
                                else if (line_vectors == "Y" && groupVecstr == "X")
                                {
                                    // 사이 라인 표시
                                    if (lineTo_Text_vec.Y > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Y < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y + 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "Z" && groupVecstr == "Y")
                                {

                                    if (lineTo_Text_vec.Z > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Z < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z + 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "Z" && groupVecstr == "X")
                                {
                                    if (lineTo_Text_vec.Z > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Z < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z - lineTo_Text_vec.Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y, text_Positions_Li[i].Z + 5);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "Y" && groupVecstr == "Z")
                                {

                                    if (lineTo_Text_vec.Y > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.Y < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y - lineTo_Text_vec.Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X, text_Positions_Li[i].Y + 5, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else if (line_vectors == "X" && groupVecstr == "Z")
                                {

                                    if (lineTo_Text_vec.X > 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X - 5, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                    else if (lineTo_Text_vec.X < 0)
                                    {
                                        start_point = new Point3d(text_Positions_Li[i].X - lineTo_Text_vec.X, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        end_point = new Point3d(text_Positions_Li[i].X + 5, text_Positions_Li[i].Y, text_Positions_Li[i].Z);
                                        text_between_li = new Line(start_point, end_point);
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("\n스풀의 진행방향을 찾지 못했습니다.");
                                    break;
                                }

                                acBlkRec.AppendEntity(text_between_li);
                                acTrans.AddNewlyCreatedDBObject(text_between_li, true);

                                // 텍스트 중앙 지시선을 그리고 사용자가 그려준 마지막 라인과 조인해서 한 객체로 만든다. 
                                if (i == 0)
                                {
                                    center_line = new Line(line.EndPoint, text_between_li.StartPoint);
                                    Entity en = acTrans.GetObject(line.ObjectId, OpenMode.ForWrite) as Entity;
                                    en.UpgradeOpen();
                                    en.JoinEntity(center_line);
                                }
                                else if (i == text_Positions_Li.Count - 1)
                                {
                                    center_line = new Line(line.EndPoint, text_between_li.StartPoint);
                                    Entity en = acTrans.GetObject(line.ObjectId, OpenMode.ForWrite) as Entity;
                                    en.UpgradeOpen();
                                    en.JoinEntity(center_line);
                                }
                            }
                            //마지막 그리는 라인은 바로 전 라인(인풋으로 들어온 라인)과 조인해준다.

                        }
                        acTrans.Commit();
                        acTrans.Dispose();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    return drawText_spoolTexts_objIDs;
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
            public int pipe_CompareTor = 5; // Db와 CAD 좌표를 비교할때 오차 범위
            public int vec_ComareTor = 4; // Vector List를 만들때 Db와 CAD에서 좌표 비교시 오차 범위

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
                                 "WHERE {3} > disx AND {3}> disy AND {3} > disz ;", point.X, point.Y, point.Z, pipe_CompareTor);
                //string sql = string.Format(
                // "SELECT * ," +
                // "abs(POSX - {0})" +
                // "as disx, abs(POSY - {1})" +
                // "as disy, abs(POSZ - {2})" +
                // "as disz FROM TB_POCINSTANCES " +
                // "Order by disx ASC, disy, disz;", point.X, point.Y, point.Z);
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
            public List<string> Get_PipeInstanceIDs_By_ObjIDs(List<Polyline3d> pli, string groupVecstr)
            {
                Pipe pi = new Pipe();
                List<string> ids = new List<string>();


                using (Transaction acTrans = db_acDB.TransactionManager.StartTransaction())
                {
                    ObjectId[] oId = { };
                    BlockTable acBlk;
                    acBlk = acTrans.GetObject(db_acDB.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec;
                    acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    DocumentCollection dm = Application.DocumentManager;
                    Editor ed = dm.MdiActiveDocument.Editor;
                    Database destDb = dm.MdiActiveDocument.Database;
                    if (db_path != null)
                    {
                        string connstr = "Data Source=" + db_path;
                        using (SQLiteConnection conn = new SQLiteConnection(connstr))
                        {
                            conn.Open();
                            //오브젝트 ID를 이용해서 객체의 정보를 가져온다. 배관의 순서를 위해 배관이 놓인 순서필요.
                            //파이프의 백터 필요.
                            foreach (var li in pli)
                            {
                                //Line의 Vec방향.
                                Vector3d vec = li.StartPoint.GetVectorTo(li.EndPoint).GetNormal();

                                //DB Select문에 사용할 Line Vector에 따른 Obj방향설정. 진행되는 Vector는 비교하지 않음.
                                (string[] db_column_name, double[] line_trans) = pi.getPipeVector(vec, li);

                                if (db_column_name[0] != "")
                                {
                                    //DB TB_PIPINSTANCES에서 POS에서 CAD Line좌표를 빼준 리스트에서 가장 상위 객체의 INSTANCE_ID를 가져온다.
                                    //배관 좌표에서 가장 근접한 값을 가져오기 위해 DB좌표와 CAD 좌표를 뺀 값 중 가장 작은 값을 상위에 위치 시키고, 추가로 Length값도 비교.
                                    //Line의 StartPoint와 EndPoint가 규칙적이지 않아 중간지점으로 변경(24.1.31)

                                    string sql = "";
                                    string pos_Axis = "";
                                    double li_pos_by_gVector = 0.0;

                                    // 라인의 좌표는 CAD 라인 좌표와 Databse 의 좌표를 비교한다.
                                    // 라인의 검출은 그룹 벡터의 축의 방향과 0이 되는 좌표가 1순위
                                    // 그 다음은 Length가 가장 비슷한 라인이 2순위 1,2순위를 내림차순해서 가장 상위 객체를 가져온다. 
                                    // 라인의 진행방향은 비교에서 제외해도 된다. Start End포인트가 정해져있지 않아 정확한 검출이 안됨.
                                    // 참고.CAD라인은 엘보 길이를 포함하고 있어 Length와 파이프 진행방향의 좌표는 정확히 일치할 수 없다. 그러나 GroupVector의 좌표만큼은 Key값이 된다.

                                    if (groupVecstr == "X")
                                    {
                                        pos_Axis = "POSX";
                                        li_pos_by_gVector = li.StartPoint.X;
                                    }
                                    else if (groupVecstr == "Y")
                                    {
                                        pos_Axis = "POSY";
                                        li_pos_by_gVector = li.StartPoint.Y;
                                    }
                                    else if (groupVecstr == "Z")
                                    {
                                        pos_Axis = "POSZ";
                                        li_pos_by_gVector = li.StartPoint.Z;
                                    }

                                    sql = String.Format("SELECT *,abs({0}-{1}) as pos, abs({2}-LENGTH1) as distance FROM {3} ORDER by pos,distance;",
                                                       li_pos_by_gVector,
                                                       pos_Axis,
                                                       li.Length,
                                                       db_TB_PIPEINSTANCES
                                                       );

                                    if (sql != "")
                                    {
                                        SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                                        SQLiteDataReader rdr_ready = cmd.ExecuteReader();
                                        rdr_ready.Read();

                                        // Polyline3d의 진행방향으로 뺀 값이 배관의 본래 길이만큼 길다면 길다면 Polyline3d의 Endpolint로 다시 쿼리문을 적용한다.
                                        double dis = (double)rdr_ready["distance"];

                                        SQLiteCommand cmd_1 = new SQLiteCommand(sql, conn);
                                        SQLiteDataReader rdr = cmd_1.ExecuteReader();

                                        if (rdr.HasRows)
                                        {
                                            //Read를 한번만 실행해서 내림차순의 가장 상위 객체를 가져온다.
                                            rdr.Read();
                                            string bitToStr_Instance_Id = BitConverter.ToString((byte[])rdr["INSTANCE_ID"]).Replace("-", "");
                                            ids.Add(bitToStr_Instance_Id);
                                            //BitConverter에 '-'하이픈 Replace로 제거. 
                                            //db_ed.WriteMessage("인스턴스 ID : {0} {1}\n", rdr["POSX"], bitToStr_Instance_Id);
                                            string comm = String.Format("SELECT * FROM {0} WHERE hex(INSTANCE_ID) = {1}", db_TB_PIPEINSTANCES, rdr["INSTANCE_ID"]);
                                            rdr.Close();
                                        }
                                        else
                                        {
                                            ids.Add("Undefined");
                                            MessageBox.Show("Error : 해당 배관에 대한 데이터가 없습니다.");
                                        }
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("[Error] sql 쿼리가 비어있습니다. Line:4131");
                                }
                            }
                        }
                    }
                    acTrans.Commit();
                }
                return ids;
            }
            /*
            * 함수 이름 : Get_Spool_Infomation_By_ObjIds
            * 기능 설명 : OBJECT IDS리스트를 기준으로 Pipe의 Spool정보를 반환.
            * 반환 값 : Tuple<OwnerId, Spool정보>
            * 비고 : 삭제  -> Get_Pipe_Spool_Info_By_OwnerInsId로 대체.
            */
            public List<string> Get_Spool_Infomation_By_insIds(List<string> pipe_InstanceIDS)
            {
                //List<Tuple<string, string>> production_Info = new List<Tuple<string, string>>();
                //List<string> pipeInfo_NotFind_li = new List<string>();
                List<string> pipeInfo_Li = new List<string>();
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
                            foreach (var InstanceID in pipe_InstanceIDS)
                            {
                                ////스풀이름
                                //sql_li[0] = String.Format("SELECT {0} " +
                                //"FROM {1} " +
                                //"WHERE {2} = " +
                                //"(SELECT {3} " +
                                //"FROM {4} " +
                                //"INNER JOIN {5} " +
                                //"ON " +
                                //"{6}.INSTANCE_ID = " +
                                //"{7}.INSTANCE_ID AND " +
                                //"hex({8}.INSTANCE_ID) = '{9}');",
                                //db_COL_Production_Group_NM,
                                //db_TB_PRODUCTION_GROUP,
                                //db_COL_Production_Group_ID,
                                //db_COL_Production_Group_ID,
                                //db_TB_PIPEINSTANCES,
                                //db_TB_PRODUCTION_DRAWING,
                                //db_TB_PIPEINSTANCES,
                                //db_TB_PRODUCTION_DRAWING,
                                //db_TB_PIPEINSTANCES,
                                //obj.ToString()
                                //   );

                                ////유틸이름
                                //sql_li[1] = String.Format(
                                //"SELECT {0} " +
                                //"from {1} " +
                                //"INNER JOIN " +
                                //"{2} " +
                                //"ON " +
                                //"{3}.UTILITY_ID = " +
                                //"{4}.UTILITY_ID " +
                                //"AND " +
                                //"hex({5}.INSTANCE_ID) = '{6}';",
                                //db_COL_UTILITY_NM, db_TB_UTILITIES,
                                //db_TB_PIPEINSTANCES,
                                //db_TB_UTILITIES,
                                //db_TB_PIPEINSTANCES,
                                //db_TB_PIPEINSTANCES,
                                //obj.ToString()
                                //);

                                //sql_li[2] = String.Format(
                                //    "SELECT {0} " +
                                //    "FROM {1} " +
                                //    "WHERE hex(INSTANCE_ID) = '{2}';",
                                //    db_COL_SPOOLNUM, db_TB_PRODUCTION_DRAWING, obj.ToString());
                                string pipeInfo = Get_Pipe_Spool_Info_By_OwnerInsId(InstanceID);
                                if (pipeInfo != "")
                                {
                                    pipeInfo_Li.Add(pipeInfo);
                                }
                                else if (pipeInfo == "Undefined")
                                {
                                    pipeInfo_Li.Add("Undefined");
                                }
                                else
                                {
                                    pipeInfo_Li.Add("[Error] PipeInfor NotFind");
                                }
                                //쿼리문 실행
                                //SQLiteCommand comm = new SQLiteCommand(sql_li[0], conn);
                                // SQLiteCommand comm = new SQLiteCommand(sql, conn);
                                //SQLiteDataReader reader = comm.ExecuteReader();


                                //while (reader.Read())
                                //{
                                //    str_pipe_Info += reader[0].ToString();
                                //}

                                //reader.Close();
                                //comm = new SQLiteCommand(sql_li[1], conn);
                                //reader = comm.ExecuteReader();
                                //while (reader.Read())
                                //{
                                //    str_pipe_Info += "_" + reader[0].ToString();
                                //}

                                //reader.Close();
                                //comm = new SQLiteCommand(sql_li[2], conn);
                                //reader = comm.ExecuteReader();
                                //while (reader.Read())
                                //{
                                //    str_pipe_Info += "_" + reader[0].ToString();
                                //}
                                //if (str_pipe_Info != "")
                                //{
                                //    production_Info.Add(new Tuple<string, string>(obj, str_pipe_Info));
                                //}
                                //else
                                //{
                                //    pipeInfo_NotFind_li.Add(obj);
                                //}
                            }
                            conn.Close();
                        }
                    }
                }
                return pipeInfo_Li;
            }

            /*
            * 함수 이름 : Get_Final_POC_Instance_Ids
            * 기능 설명 : PIPE의 마지막 POC.
            * 관련 테이블 : TB_POCINSTANCES.
            * 입력 타입 : 없음.(DB에서 바로 검색).
            * 반환 값 : List(Point3d:마지막 POC의 위치), List(string:마지막 객체의 OWNER_INSTANCE_ID). <- 앞뒤객체를 검색해서 Pipe 앞뒤로 배치가능.
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
                        "SELECT PIPESIZE_NM,UTILITY_NM,PRODUCTION_DRAWING_GROUP_NM,MATERIAL_NM,SPOOL_NUMBER,IGM.INSTANCE_GROUP_ID " +
                        "FROM TB_POCINSTANCES as PI INNER JOIN " +
                        "TB_PIPESIZE as PS," +
                        "TB_MATERIALS as MR," +
                        "TB_UTILITIES as UT," +
                        "TB_PRODUCTION_DRAWING as PD," +
                        "TB_PRODUCTION_DRAWING_GROUPS as PDG," +
                        "TB_INSTANCEGROUPMEMBERS as IGM " +
                        "on PI.PIPESIZE_ID = PS.PIPESIZE_ID AND " +
                        "PI.UTILITY_ID = UT.UTILITY_ID AND " +
                        "PD.PRODUCTION_DRAWING_GROUP_ID = PDG.PRODUCTION_DRAWING_GROUP_ID AND " +
                        "PDG.INSTANCE_GROUP_ID = IGM.INSTANCE_GROUP_ID AND " +
                        "IGM.INSTANCE_ID = PI.OWNER_INSTANCE_ID AND " +
                        "MR.MATERIAL_ID=PI.MATERIAL_ID AND " +
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
                    if (rdr.HasRows) //rdr 반환값이 있을때만 Read
                    {
                        rdr.Read();
                        string instance_GroupId = BitConverter.ToString((byte[])rdr["INSTANCE_GROUP_ID"]).Replace("-", "");
                        rdr.Close();
                        comm.Dispose();
                        comm = new SQLiteCommand(sql_spoolInfo, conn);
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
                                if(spool_num.Length == 1)
                                {
                                    spool_num = "0" + spool_num;
                                }

                                spool_info = rdr["PIPESIZE_NM"] + "_" + rdr["UTILITY_NM"] + "_" + material_NM + "_" + rdr["PRODUCTION_DRAWING_GROUP_NM"] + "_" + spool_num;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Error : 해당 배관에 대한 데이터가 없습니다. Line:4381");
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
                    DocumentCollection doc = Application.DocumentManager;
                    Editor ed = doc.MdiActiveDocument.Editor;
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
                                            //ed.WriteMessage(owner_id + "\n");
                                            if (rdr_1.HasRows) //rdr 반환값이 있을때만 Read
                                            {
                                                while (rdr_1.Read())
                                                {
                                                    // 선택된 weldGroup에서 라이브러리 이름이 Filter이름과 동일하면 Index를 저장한다.
                                                    foreach (string filter in filters)
                                                    {
                                                        if (rdr_1["MODEL_TEMPLATE_NM"].ToString().Contains(filter))
                                                        {
                                                            ed.WriteMessage(rdr_1["MODEL_TEMPLATE_NM"].ToString());
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
                            //POC Instances 가져오는 SQL문 작성해서 반환. 
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
                                    string spool_info = Get_Pipe_Spool_Info_By_OwnerInsId(instance_id);
                                    if (spool_info != null && spool_info != "") { spool_info_li.Add(spool_info); };

                                    //Get_Pipe_Spool_Info_By_OwnerInsId 로 스풀정보.
                                    SQLiteCommand command_1 = new SQLiteCommand(sql_ins, conn);
                                    SQLiteDataReader rdr_1 = command_1.ExecuteReader();

                                    if (rdr_1.HasRows)
                                    {
                                        while (rdr_1.Read())
                                        {
                                            if (spool_info != "")
                                            {
                                                points.Add(new Point3d((double)rdr_1["POSX"], (double)rdr_1["POSY"], (double)rdr_1["POSZ"]));
                                            }
                                        }
                                        if (points.Count == 2)
                                        {
                                            int index = 0; //맞대기 용접 인덱스
                                            foreach (var weldPoint in weldGroup)
                                            {
                                                //CAD좌표는 DDWORKS 좌표에서 4번째에서 반올림한 좌표.
                                                //오너 아이디에서 반환된 Points와 weldPoint가 일치하면 
                                                if (Math.Abs(weldPoint.X - points[0].X) < vec_ComareTor && Math.Abs(weldPoint.Y - points[0].Y) < vec_ComareTor && Math.Abs(weldPoint.Z - points[0].Z) < vec_ComareTor)
                                                {
                                                    Vector3d vec = (points[1] - points[0]).GetNormal();
                                                    vec_li.Add(vec);
                                                    //맞대기 용접일때처리. 2번째 파이프 객체를 찾았을때(256) 좌표의 인덱스에 해당하는 좌표를 넣어준다.
                                                    if (count == 2)
                                                    {
                                                        index_li.Add(index);
                                                    }
                                                }
                                                else if (Math.Abs(weldPoint.X - points[1].X) < vec_ComareTor && Math.Abs(weldPoint.Y - points[1].Y) < vec_ComareTor && Math.Abs(weldPoint.Z - points[1].Z) < vec_ComareTor)
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
                        if (weldGroup.Count < spool_info_li.Count)
                        {
                            foreach (int index in index_li)
                            {
                                weldGroup.Insert(index, weldGroup[index]);
                            }
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
                DocumentCollection dm = Application.DocumentManager;
                Editor ed = dm.MdiActiveDocument.Editor;

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
                                                if (Math.Abs(weldPoint.X - points[0].X) < vec_ComareTor && Math.Abs(weldPoint.Y - points[0].Y) < vec_ComareTor && Math.Abs(weldPoint.Z - points[0].Z) < vec_ComareTor)
                                                {
                                                    Vector3d vec = (points[1] - points[0]).GetNormal();
                                                    vec_li.Add(vec);
                                                    //맞대기 용접일때처리. 2번째 파이프 객체를 찾았을때(256) 좌표의 인덱스에 해당하는 좌표를 넣어준다.
                                                    if (count == 2)
                                                    {
                                                        index_li.Add(index);
                                                    }
                                                }
                                                else if (Math.Abs(weldPoint.X - points[1].X) < vec_ComareTor && Math.Abs(weldPoint.Y - points[1].Y) < vec_ComareTor && Math.Abs(weldPoint.Z - points[1].Z) < vec_ComareTor)
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
            //기능 설명 : Valve와 연결된 파이프를 찾아 삽입값을 빼준다.
            //Valve INSTANCE_ID에 연결된 파이프위치들을 반환한다.
            //CAD에서 밸브와 연결된 파이프를 찾아 MidPoint에 있는 Text의 값을(PolyLine의 길이조정).
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
                        "OWNER_TYPE = '{0}' AND POC_TEMPLATE_NM like '%{1}%'", ownerType_Component, valveType); //연결된 객체가 컴포넌트이고, POC템플릿에 밸브랑 이름이 들어간 객체
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

            /*
             * 함수 이름 : 
             * 기능 설명 : Model Instance객체의 좌표를 모두 찾아 반환한다.
            * */
            public List<Point3d> Get_Components_Positions()
            {
                List<Point3d> vavle_Positions = new List<Point3d>();
                string connstr = "Data Source=" + db_path;
                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    conn.Open();
                    string sql_command = string.Format("SELECT POSX,POSY,POSZ FROM TB_MODELTEMPLATES " +
                        "INNER JOIN TB_MODELINSTANCES on TB_MODELTEMPLATES.MODEL_TEMPLATE_ID " +
                        "= TB_MODELINSTANCES.MODEL_TEMPLATE_ID;");
                    SQLiteCommand comm = new SQLiteCommand(sql_command, conn);
                    SQLiteDataReader rdr = comm.ExecuteReader();

                    while (rdr.Read())
                    {
                        vavle_Positions.Add(new Point3d((double)rdr["POSX"], (double)rdr["POSY"], (double)rdr["POSZ"]));
                    }
                    rdr.Close();
                    conn.Dispose();
                }
                return vavle_Positions;
            }

            //기능 설명 : Valve와 연결된 파이프를 찾아 삽입값을 빼준다.
            //Valve INSTANCE_ID에 연결된 파이프위치들을 반환한다.
            //CAD에서 밸브와 연결된 파이프를 찾아 MidPoint에 있는 Text의 값을(PolyLine의 길이조정).
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
            public bool bZaxis = false;
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
                    if (m.Msg == WM_KEYDOWN && kc == Keys.Z)
                    {
                        bZaxis = true;
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