using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Database = Autodesk.AutoCAD.DatabaseServices.Database;

namespace PipeInfo
{
    public class PipeInfo
    {
        public string db_path = "";
        [CommandMethod("ff")]
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
                    var pipeInfo_cls = new DDWorks_Database(ed, db, db_path);
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

                    var draw_Text = new TextControl(ed, db);
                    var pipe = new Pipe(ed, db);
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
        public void DataGet(string data)
        {
            db_path = data;
        }

        //도면내 도곽내 MES정보와 용접포인트 번호를 가져온다.
        [CommandMethod("bb")]
        public void selectBlock()
        {
            try
            {

                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                Database db = acDoc.Database;
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    BlockTable acBlk = acTrans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkRec = acTrans.GetObject(acBlk[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    PromptSelectionOptions pso = new PromptSelectionOptions();
                    List<SelectionFilter> typedValues = new List<SelectionFilter>();
                    string[] typeValeStrings = { "INSERT", "CIRCLE", "TEXT" };
                    TypedValue[] typedBlock = { new TypedValue(0, typeValeStrings[0]) };
                    SelectionFilter selFilBlk = new SelectionFilter(typedBlock);

                    PromptSelectionResult ss = ed.SelectAll(selFilBlk);


                    string sheetName = "SPOOL_도곽";
                    string titleBoardName = "BL22";


                    if (ss.Status == PromptStatus.OK)
                    {
                        SelectionSet sSet = ss.Value;
                        ObjectId[] oId = sSet.GetObjectIds();
                        List<Extents3d> sheetPosLi = new List<Extents3d>();
                        List<Extents3d> titleBoardPosLi = new List<Extents3d>();

                        List<string> weldNumber = new List<string>();
                        List<string> titleBoardTexts = new List<string>();
                        foreach (ObjectId id in oId)
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
                        }
                        List<Point3d> cirPosLi = new List<Point3d>();
                        List<Point3d> tePosLi = new List<Point3d>();

                        //전체 시트 포지션리스트에서 시트별 구역의 Text정보를 가져온다.
                        foreach (var sheet in sheetPosLi.Select((value, i) => (value, i)))
                        {
                             ed.WriteMessage("도곽{0}\n", sheet.i);
                            for (int j = 1; j < typeValeStrings.Length; j++) //"CIRCLE" 과 "TEXT"를 한번씩 반복.
                            {
                                TypedValue[] typeValue = { new TypedValue(0, typeValeStrings[j]) };
                                SelectionFilter selFilter = new SelectionFilter(typeValue);
                                PromptSelectionResult selWin = ed.SelectCrossingWindow(sheet.value.MinPoint, sheet.value.MaxPoint, selFilter, false);
                                SelectionSet selSetWin = selWin.Value;
                                ObjectId[] sheetInSelObIds = selSetWin.GetObjectIds();

                                foreach (ObjectId sId in sheetInSelObIds)
                                {
                                    Entity en = acTrans.GetObject(sId, OpenMode.ForRead) as Entity;
                                    if (typeValeStrings[j] == "CIRCLE" && en.GetType().Name.ToString() == "Circle")
                                    {
                                        Circle cir = acTrans.GetObject(sId, OpenMode.ForRead) as Circle;
                                        if (cir.Layer.ToString().Contains("Infomation_Welding_Number"))
                                        {
                                            cirPosLi.Add(cir.Center);
                                        }
                                    }
                                    if (typeValeStrings[j] == "TEXT" && en.GetType().Name.ToString() == "DBText")
                                    {
                                        DBText te = acTrans.GetObject(sId, OpenMode.ForRead) as DBText;
                                        if (te.Layer.ToString().Contains("Infomation_Welding_Number"))
                                        {
                                             tePosLi.Add(te.Position);
                                        }
                                    }
                                }
                            }
                        }
                        ed.UpdateScreen();
                        //foreach(var num in tePosLi)
                        //{
                            foreach (var cirPos in cirPosLi)
                            {
                                ed.WriteMessage("\n" + cirPos.ToString());
                              //  var delta = cirPos - num;
                                //if (Math.Abs(delta.X) < 1 && Math.Abs(delta.Y) < 1)
                                //{
                                //    ed.WriteMessage("\n" + num.ToString());
                                //}
                            }
                        //}
                    }
                    acTrans.Commit();
                    acTrans.Dispose();
                }
            }
            catch (System.Exception ex)

            {

                Application.DocumentManager.MdiActiveDocument.

                    Editor.WriteMessage(ex.Message);

            }
        }

        [CommandMethod("rr")]
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
                    var ddworks_Database = new DDWorks_Database(ed, db, db_path);
                    using (Transaction acTrans = db.TransactionManager.StartTransaction())
                    {
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
                                List<Point3d> point3Ds = new List<Point3d>();
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

                                // Spool Area Divied 
                                // Concept :
                                // 1. 첫 번째 점부터 마지막 점까지 거리를 모두 구한다. 
                                // 2. 가까운 객체들의 좌표와 저장하고 인덱스를 쌍으로 저장.
                                // 3. 다시 배열을 탐색할때는 한 번 찾은 객체는 다시 비교하지 않는다. 
                                List<Point3d> points = new List<Point3d>();

                                // Tee객체를 필터링한다. //끝객체 //Reducer
                                string[] filter = { "Tee", "Reducer", "Reducing" };

                                // 선택한 객체에서 필터를 걸러낸다.
                                List<Point3d> oldPoints = ddworks_Database.FilterWeldGroup_By_ComponentType(point3Ds, filter);

                                // 23.7.23 함수 추가 Get_Pipe_Vector_By_SpoolList와 거의 동일.. 조금 수정해야할 것 같다. 함수안에 함수로. Get_Pipe_Info하고 -> Vector, Spool, WELD맞대기좌표추가한 리스트 반환기능 등
                                List<Vector3d> vec = ddworks_Database.Get_Pipe_Vector_By_Points(oldPoints);

                                //List<Vector3d> vec = Get_SpoolVector_By_Points(oldPoints);

                                // 파이프와 파이프의 벡터(Pipe가 두개 이상일때만) 
                                Vector3d spool_vec = new Vector3d();
                                double min_Z = oldPoints.Min(p => p.Z);
                                double max_Z = oldPoints.Max(p => p.Z);
                                bool multi_mode = false; //배관 : 가로 스풀 : 세로(버티칼)

                                if (max_Z - min_Z > 200 && (Math.Round(vec[0].GetNormal().X, 1) != 0 || Math.Round(vec[0].GetNormal().Y, 1) != 0))
                                {
                                    multi_mode = true;
                                    oldPoints = oldPoints.OrderByDescending(p => p.Z).ToList();
                                }

                                if (vec.Count > 0)
                                { //Spool의 진행방향이 Vectical일때를 제외하고는 좌표를 진행 방향의 반대로 정렬을 한다. X진행방향 -> Y
                                    if (Math.Round(vec[0].GetNormal().X, 1) == 1 || Math.Round(vec[0].GetNormal().X, 1) == -1)
                                    {
                                        oldPoints = oldPoints.OrderByDescending(p => p.Y).ToList();
                                    }
                                    if (Math.Round(vec[0].GetNormal().Y, 1) == 1 || Math.Round(vec[0].GetNormal().Y, 1) == -1)
                                    {
                                        oldPoints = oldPoints.OrderByDescending(p => p.X).ToList();
                                    }
                                    if (Math.Round(vec[0].GetNormal().Z, 1) == 1 || Math.Round(vec[0].GetNormal().Z, 1) == -1)
                                    {
                                        oldPoints = oldPoints.OrderByDescending(p => p.X).ToList();
                                    }
                                }
                                // 파이프의 벡터 
                                //if (Math.Round(vec[0].GetNormal().X, 1) == 1 || Math.Round(vec[0].GetNormal().X, 1) == -1)
                                //{
                                //   oldPoints = oldPoints.OrderByDescending(p => p.Y).ToList();
                                //}
                                //if (Math.Round(vec[0].GetNormal().Y, 1) == 1 || Math.Round(vec[0].GetNormal().Y, 1) == -1)
                                //{

                                //      oldPoints = oldPoints.OrderByDescending(p => p.X).ToList();
                                //}


                                // 웰딩 포인트에 연결된 파이프를 찾아 Vector방향을 알아낸다.
                                // Spool 정보도 같이 불러온다. (맞대기 용접은 좌표를 더해서 반환)
                                (List<string> spoolInfo_li, List<Vector3d> vec_li, List<Point3d> newPoints) = ddworks_Database.Get_Pipe_Vector_By_SpoolList(oldPoints);
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
                                                            ed.WriteMessage(group_index.ToString(), newPoints[j].ToString());
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


                                        Line info_line = new Line();
                                        info_line.StartPoint = new Point3d(aver_X, aver_Y, newPoints[0].Z);
                                        info_line.EndPoint = new Point3d(aver_X, aver_Y + 300, newPoints[0].Z);
                                        acBlkRec.AppendEntity(info_line);
                                        acTrans.AddNewlyCreatedDBObject(info_line, true);

                                        double basePoint = 0;

                                        if (multi_mode == false)
                                        {
                                            if (Math.Round(vec_li[0].GetNormal().X, 1) == 1 || Math.Round(vec_li[0].GetNormal().X, 1) == -1)
                                            {
                                                //basePoint를 적용시킬때 aver_Y 는 text.position = new Point3d(,(여기),);
                                                basePoint = aver_Y - 300; //<- 구룹
                                            }
                                            if (Math.Round(vec_li[0].GetNormal().Y, 1) == 1 || Math.Round(vec_li[0].GetNormal().Y, 1) == -1)
                                            {
                                                basePoint = aver_X - 300; //<- 구룹
                                            }
                                            if (Math.Round(vec_li[0].GetNormal().Z, 1) == 1 || Math.Round(vec_li[0].GetNormal().Z, 1) == -1)
                                            {
                                                basePoint = aver_X - 300; //<- 구룹
                                            }
                                        }
                                        else
                                        {
                                            basePoint = min_Z;
                                        }


                                        List<double> basePointLi = new List<double>();
                                        for (int i = 0; i < spoolInfo_li.Count / 2; i++)
                                        {
                                            basePointLi.Add(aver_Y + (i * 15));
                                        }
                                        //Group별로 Vector를 파악해서 basePoint를 진행

                                        //newPoints = newPoints.OrderByDescending(p => p.X).ToList();

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
                                            //text.TextString = near_Points[k].Item1.ToString();
                                            //text.Position = near_Points[k].Item2;
                                            text.TextString = spoolInfo_li[k].ToString();
                                            text.Normal = Vector3d.ZAxis;
                                            //text.Position = new Point3d(basePoint.X, basePoint.Y - (k*15), basePoint.Z);
                                            text.Height = 12.0;
                                            int nCnt = 0;

                                            if (multi_mode == false)
                                            {
                                                if (Math.Round(vec_li[k].GetNormal().X, 1) == 1)
                                                {
                                                    //SE,WS는 정렬 : 0, Rotation : 0
                                                    //NW,NE은 정렬 : 2, Rotation : 180
                                                    nCnt = 2;
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(newPoints[0].X, basePoint, newPoints[k].Z);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(newPoints[0].X, basePoint, newPoints[k].Z);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 180;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(newPoints[0].X + 30, basePoint, newPoints[k].Z);
                                                    }
                                                }
                                                else if (Math.Round(vec_li[k].GetNormal().X, 1) == -1)
                                                {
                                                    //SE,WS는 정렬 : 2, Rotation : 0
                                                    //NW,NE은 정렬 : 0, Rotation : 180
                                                    nCnt = 0;
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(newPoints[0].X, basePoint, newPoints[k].Z);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(newPoints[0].X, basePoint, newPoints[k].Z);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 180;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(newPoints[0].X, basePoint, newPoints[k].Z);
                                                    }
                                                }
                                                else if (Math.Round(vec_li[k].GetNormal().Y, 1) == 1)
                                                {
                                                    //NE,ES 정렬 : 0, Rotation : 90
                                                    //NW,WS 정렬 : 2, Rotation : 270
                                                    nCnt = 2;
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15; //마이너스로 해야 순서 맞음.
                                                                         //newPoints[k].X-300
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[k].Z);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[k].Z);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 270;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[k].Z);
                                                    }
                                                }
                                                else if (Math.Round(vec_li[k].GetNormal().Y, 1) == -1)
                                                {
                                                    //NE,ES 정렬 : 0, Rotation : 90
                                                    //NW,WS 정렬 : 2, Rotation : 270
                                                    nCnt = 0;
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[k].Z);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[k].Z);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 270;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(basePoint, newPoints[0].Y, newPoints[k].Z);
                                                    }

                                                }
                                                else if (Math.Round(vec_li[k].GetNormal().Z, 1) == 1)
                                                {
                                                    //이건 3D회전도 필요하다. Z축을 기준으로 Flip시켜야함.
                                                    nCnt = 0;
                                                    text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 90, Vector3d.YAxis, Point3d.Origin));
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[0].Z);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[0].Z);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 90;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[0].Z);
                                                    }
                                                }
                                                else if (Math.Round(vec_li[k].GetNormal().Z, 1) == -1)
                                                {
                                                    nCnt = 2;
                                                    text.TransformBy(Matrix3d.Rotation(Math.PI / 180 * 90, Vector3d.YAxis, Point3d.Origin));
                                                    //text.Normal = Vector3d.YAxis;
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[0].Z);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[0].Z);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 90;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(basePoint, near_Points[0].Item2.Y, newPoints[0].Z);
                                                    }
                                                }
                                            }
                                            else if (multi_mode == true)
                                            {
                                                if (Math.Round(vec_li[k].GetNormal().Y, 1) == -1)
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        nCnt = 0;
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(near_Points[0].Item2.X, near_Points[0].Item2.Y, basePoint);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(near_Points[0].Item2.X, near_Points[0].Item2.Y, basePoint);
                                                    }
                                                text.Rotation = Math.PI / 180 * 270;
                                                text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                {
                                                    text.AlignmentPoint = new Point3d(near_Points[0].Item2.X, near_Points[0].Item2.Y, basePoint);
                                                }
                                                else if (Math.Round(vec_li[k].GetNormal().Y, 1) == 1)
                                                {
                                                    nCnt = 2;
                                                    if (k % 2 == 0 && k != 0)
                                                    {
                                                        basePoint -= 15;
                                                        text.Position = new Point3d(near_Points[0].Item2.X, near_Points[0].Item2.Y, basePoint);
                                                    }
                                                    else
                                                    {
                                                        text.Position = new Point3d(near_Points[0].Item2.X, near_Points[0].Item2.Y, basePoint);
                                                    }
                                                    text.Rotation = Math.PI / 180 * 270;
                                                    text.HorizontalMode = (TextHorizontalMode)textAlign[nCnt];
                                                    if (text.HorizontalMode != TextHorizontalMode.TextLeft)
                                                    {
                                                        text.AlignmentPoint = new Point3d(near_Points[0].Item2.X, near_Points[0].Item2.Y, basePoint);
                                                    }
                                                }

                                            }

                                            acBlkRec.AppendEntity(text);
                                            acTrans.AddNewlyCreatedDBObject(text, true);
                                        }

                                    }

                                    List<Point3d> li = new List<Point3d>();
                                    foreach (var d in near_Points)
                                    {
                                        if (d.Item1 == 1)
                                        {
                                            li.Add(d.Item2);
                                        }
                                    }

                                    //ed.WriteMessage("\nX 최대값 {0} 최소값 {1}\n Y 최대값 {2} 최소값 {3}\n Z 최대값 {4} 최소값 {5}",
                                    //    li.Max(p => p.X).ToString(), li.Min(p => p.X).ToString(),
                                    //    li.Max(p => p.Y).ToString(), li.Min(p => p.Y).ToString(),
                                    //    li.Max(p => p.Z).ToString(), li.Min(p => p.Z).ToString()
                                    //    );

                                    // InteractivePolyLine.RectangleInteractive(li);

                                    //Tee가 제외된 IDS반환필요
                                    //ObjectId[] ids = new ObjectId[count];
                                    //objs.CopyTo(ids, 0);
                                    //ed.SetImpliedSelection(ids);
                                }
                                else
                                {
                                    ed.WriteMessage("파이프 정보가 없습니다.");
                                }
                                // 그룹이 선택되면 그룹과 그룹의 거리가 가까우면 머지한다. 
                                // 그룹이 선택되었으면 배관그룹의 Vector방향과 파이프의 진행방향을 구한다. 이건 포인트별로 배관정보를 가져와 진행  Vector를 구한다. 
                                // 그 다음객체가 파이프,파이프 or 유니온 or 그랜드 까지 OK tee는 제외 마지막 객체 제외. 용접포인트가 tee에 있는애인지 마지막인지만 판단하면 될 거 같다. 
                                // 도면의 여러 탭을 사용할때 각 DB를 따로 불러와야하는 불편함도 있음.
                                // 현재 도면에서 DB를 사용하지 않고 리터치하는 방향. 
                                // 네모그리기. 회전하기 움직이기 등 네모와 화면상에 배관이 겹치는지 RAYCAST알고리즘 필요. 

                            }
                            else
                            {
                                ed.WriteMessage("선택된 객체가 없습니다.");
                            }
                            acTrans.Commit();
                            acTrans.Dispose();




                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                        }
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

    }


    /* --------------- [CLASS START]-------------------*/
    /* 클래스 이름 : Pipe
     * 기능 설명 : Pipe에 관련된 기능.*/
    public class Pipe
    {
        Editor ed;
        Database db;
        private Vector3d vec;

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
        public Vector3d get_Pipe_Group_Vector(PromptSelectionResult prSelRes)
        {
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
                    else
                    {
                    }
                }

                foreach (var line_vec in lines_Vec)
                {
                    ed.WriteMessage(line_vec.ToString());
                }

                acTrans.Commit();
            }
            return vec;
        }
        public void selection_Pipe_Interection()
        {

        }
        // 포인트의 폴라 포인트 구하는 법 by Tony Tanzillo
        public static Point3d PolarPoint(Point3d basepoint, double angle, double distance)
        {
            return new Point3d(
                basepoint.X + (distance * Math.Cos(angle)),
                basepoint.Y + (distance * Math.Sin(angle)),
                basepoint.Z);
        }
    }


    /* 클래스 이름 : Points
    * 기능 설명 : Points 에 관련된 기능.*/
    public class Points
    {
        // 1. 포인트 그룹을 나누는 기능.
        // 2. 포인트들의 중간점을 반환.
        // 3. 포인트들의 최소 최대값 위치를 리스트로 반환.(포인트의 Area를 판단)
        // 4. 포인트의 그룹 단일화 (그룹과 그룹의 거리가 최소거리 이하면 한 그룹으로 합침).
    }

    /* 클래스 이름 : Rectangle
    * 기능 설명 : Rectangel 에 관련된 기능.*/
    public class Rectangle
    {
        //그리기
        //회전하기
        //움직이기.. 등등
    }

    public class InteractionControl
    {
        //겹치는 Text선택하기(RayCast기능).
        //빈공간 찾기(Interaction없는 Area) 배관 주변에.
        //선택한 Entity.
    }

    /* 클래스 이름 : InteractivePolyLine.
    * 기능 설명 : InteractivePolyLine 에 관련된 기능.*/
    public class InteractivePolyLine
    {
        /// <summary>Collects the points interactively, Temporarily joining vertexes.</summary>
        /// <returns>Point3dCollection</returns>
        public static Point3dCollection CollectPointsInteractive()
        {
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Point3dCollection pointCollection = new Point3dCollection();
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Autodesk.AutoCAD.Colors.Color color = acDoc.Database.Cecolor;
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

    /* 클래스 이름 : TextControl
    * 기능 설명 : TextControl 에 관련된 기능.*/
    public class TextControl
    {
        private Editor ed;
        private Database db;
        public TextControl(Editor aced, Database acdb)
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
        public void ed_Draw_Text_To_Line_Vector(List<Tuple<string, string>> pipe_Information_li, List<Point3d> line_final_Points, int textDisBetween, int textSize)
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
                        ed.WriteMessage("기준 라인을 다시 그려주시길 바랍니다.");
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
                    view_Name = GetViewName(view.ViewDirection);
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

        //Vector 값 가져오는 알고리즘 참고. sqprt033.. 
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

        public void view_Text_Group()
        {

        }

        public void rotate_Text_Group()
        {

        }

        //텍스트 정렬하기 기능(3d에서도 움직이게)
        //같은 레벨에 있는 객체들 선택하는 기능. x축 y축 z축 선택하게만들기.
    }

    /* 클래스 이름 : DDWorks_Database
    * 기능 설명 : DDWorks_Database 에 관련된 기능.*/
    public class DDWorks_Database
    {
        private string db_path = "";
        private string db_TB_PIPEINSTANCES = "TB_PIPEINSTANCES";
        private Editor db_ed;
        private Database db_acDB;
        private string ownerType_Component = "768"; //TB_POCINSTANCES:OWNER_TYPE 기자재
        private string ownerType_Pipe = "256"; //TB_POCINSTANCES:OWNER_TYPE 파이프
        public DDWorks_Database(Editor ed, Database db, string acDB_path)
        {
            db_ed = ed;
            db_acDB = db;
            db_path = acDB_path;
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
         * 관련 테이블 : TB_POCINSTANCES,TB_PIPESIZE,TB_PRODUCTION_DRAWING,TB_PRODUCTION_DRAWING_GROUPS.
         * 입력 타입 : String 객체. TB_POCINSTANCES(OWNER_INSTANCE_ID).
         */
        public string Get_Pipe_Spool_Info_By_OwnerInsId(string ownerInsId)
        {
            string spool_info = "";
            string sql = string.Format("SELECT " +
                    "PIPESIZE_NM," +
                   "UTILITY_NM," +
                   "PRODUCTION_DRAWING_GROUP_NM, " +
                   "SPOOL_NUMBER " +
               "FROM " +
                  "TB_POCINSTANCES " +
               "INNER JOIN " +
                  "TB_PIPESIZE," +
                  "TB_UTILITIES," +
                  "TB_PRODUCTION_DRAWING," +
                  "TB_PRODUCTION_DRAWING_GROUPS " +
               "on " +
                  "TB_POCINSTANCES.PIPESIZE_ID = TB_PIPESIZE.PIPESIZE_ID " +
               "AND " +
                  "TB_POCINSTANCES.UTILITY_ID = TB_UTILITIES.UTILITY_ID " +
               "AND " +
                  "TB_PRODUCTION_DRAWING.PRODUCTION_DRAWING_GROUP_ID = TB_PRODUCTION_DRAWING_GROUPS.PRODUCTION_DRAWING_GROUP_ID " +
               "AND " +
                  "hex(TB_PRODUCTION_DRAWING.INSTANCE_ID) like '{0}' " +
               "AND " +
                  "hex(TB_POCINSTANCES.OWNER_INSTANCE_ID) like '{0}';", ownerInsId);

            string connstr = "Data Source=" + db_path;
            using (SQLiteConnection conn = new SQLiteConnection(connstr))
            {
                conn.Open();
                SQLiteCommand comm = new SQLiteCommand(sql, conn);
                SQLiteDataReader rdr = comm.ExecuteReader();
                if (rdr.Read())
                {
                    rdr.Read();//찾은 객체의 첫번째 항목만 불러온다. 
                    spool_info = rdr["PIPESIZE_NM"] + "_" + rdr["UTILITY_NM"] + "_" + rdr["PRODUCTION_DRAWING_GROUP_NM"] + "_" + rdr["SPOOL_NUMBER"];
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

        // 중간 지점 그려주기. 배관 방향과 수평되게 그려주기.. 
        // 빈공간.. 찾기.. RAYTRAY.. 
        // 수평되게 그려주기 된다면 네모 그려서 그룹 아이디 
        // 4방향으로 Rec 회전 알고리즘 
        // EnterKey 누르면 거기에 파이프 정보. 
    }
    /* --------------- [CLASS END] -------------------*/

    /*
     * 
     */
    public class Block
    {
        //
        //선택한 블록에서 원하는 정보 가져오기
    }

    public class Excel
    {
        //원하는 정보를 엑셀로 Export
    }
}

//단축키 Ctrl+K -> Ctrl+E
//Ctrl + M + O