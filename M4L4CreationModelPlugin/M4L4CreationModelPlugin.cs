using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace M4L4CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]//
    public class M4L4CreationModelPlugin : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;//получение ссылки на Юай документ
            Document doc = uiDoc.Document;//получение ссылки на экземпляр класса документ, со ссылкой на бд открытого документа

            //Create walls
            List<Level> listlevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            Level level1 = listlevel
                 .Where(x => x.Name.Equals("Уровень 1"))
                 .FirstOrDefault();

            Level level2 = listlevel
                 .Where(x => x.Name.Equals("Уровень 2"))
                 .FirstOrDefault();

            //Задание габаритов 
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);

            //транзакция с циклом для построения стен
            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            List<Wall> walls = WallCreator(doc, level1, level2, depth, width);
            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);
            AddRoof(doc, level2, walls, depth, width);
            transaction.Commit();
            return Result.Succeeded;
        }

        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()//превращение в символ
                .Where(x => x.Name.Equals("0915 x 1830 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();
            //поиск места установки
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);//точка начала кривой
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);//точка конца кривой
            XYZ point = (point1 + point2) / 2;//средняя точка. место установки двери
            //активация элемента
            if (!windowType.IsActive)
                windowType.Activate();

            //создание окна
           FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);
            window.flipFacing();
            //сдвиг от уровня
            double height= UnitUtils.ConvertToInternalUnits(1070, UnitTypeId.Millimeters);
            window.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters));
        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
           FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()//превращение в символ
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();
            //поиск места установки
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);//точка начала кривой
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);//точка конца кривой
            XYZ point = (point1 + point2) / 2;//средняя точка. место установки двери
            //активация элемента
            if (!doorType.IsActive)
                doorType.Activate();
            
            //создание двери
            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }

        public List<Wall> WallCreator(Document doc, Level levelone, Level leveltwo, double depth, double width)
        {            
            double dx = width / 2;
            double dy = depth / 2;
            //коллекция точек
            List<XYZ> points = new List<XYZ>();

            //добавление точек"зацикленно"
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));
            //массив из стен
            List<Wall> walls = new List<Wall>();


            //транзакция с циклом для построения стен

            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, levelone.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(leveltwo.Id);
            }
            return walls;
        }
        private void AddRoof(Document doc, Level level2, List<Wall> walls, double depth, double width)
        {
            //выбор крыши
            RoofType roofType = new FilteredElementCollector(doc)
                 .OfClass(typeof(RoofType))
                 .OfType<RoofType>()
                 .Where(x => x.Name.Equals("Типовой - 125мм"))
                 .Where(x => x.FamilyName.Equals("Базовая крыша"))
                 .FirstOrDefault();

            View viewPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && v.Name.Equals("Уровень 1"));//Выбор конкретного плана этажа


            //получение ширины стены
            double wallWidth = walls[0].Width;

            //получение смещения
            double dt = (wallWidth / 2)+0.5;//косая крыша, как правило немного выстыпает за пределы стен
            //задание начали и конца для ExtrusionRoof
            double extrStart = -width / 2 - dt;
            double extrEnd = width / 2 + dt;
            //задание начала и конца для массива кривых
            double curveStart = -depth / 2 - dt;
            double curveEnd = +depth / 2 + dt;

            //задание массива кривых для построения

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level2.Elevation), new XYZ(0, 0, level2.Elevation + 5)));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, level2.Elevation + 5), new XYZ(0, curveEnd, level2.Elevation)));



            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), viewPlan);
            ExtrusionRoof extrusionRoof=doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, extrStart, extrEnd);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;//обрезка по карнизу


        }
        //private void AddRoof(Document doc, Level level2, List<Wall> walls)
        //{
        //    //выбор крыши
        //   RoofType roofType=new FilteredElementCollector(doc)
        //        .OfClass(typeof(RoofType))
        //        .OfType<RoofType>()
        //        .Where(x=>x.Name.Equals("Типовой - 400мм"))
        //        .Where(x => x.FamilyName.Equals("Базовая крыша"))
        //        .FirstOrDefault();

        //    //получение ширины стены
        //    double wallWidth = walls[0].Width;

        //    //получение смещения
        //    double dt = wallWidth / 2;
        //    //коллекция точек
        //    List<XYZ> points = new List<XYZ>();

        //    //добавление точек"зацикленно"
        //    points.Add(new XYZ(-dt, -dt, 0));
        //    points.Add(new XYZ(dt, -dt, 0));
        //    points.Add(new XYZ(dt, dt, 0));
        //    points.Add(new XYZ(-dt, dt, 0));
        //    points.Add(new XYZ(-dt, -dt, 0));
        //    //создание отпечатка
        //    Application application = doc.Application;
        //    CurveArray footprint = application.Create.NewCurveArray();
        //    //привязка отпечатка к контуру стен
        //    for (int i = 0; i < 4; i++)
        //    {
        //        LocationCurve curve = walls[i].Location as LocationCurve;
        //        //получение линии со смещением
        //        XYZ pt1 = curve.Curve.GetEndPoint(0);//точка начала кривой
        //        XYZ pt2 = curve.Curve.GetEndPoint(1);//точка конца кривой
        //        //создание новой линии со смещением
        //        Line line = Line.CreateBound(pt1 + points[i], pt2 + points[i+1]);

        //        footprint.Append(line);
        //    }

        //    ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
        //    FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);
        //    //наклон крыши
        //    //ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
        //    //iterator.Reset();
        //    //while (iterator.MoveNext())
        //    //{
        //    //    ModelCurve modelCurve = iterator.Current as ModelCurve;
        //    //    footprintRoof.set_DefinesSlope(modelCurve, true);
        //    //    footprintRoof.set_SlopeAngle(modelCurve, 0.5);
        //    //}
        //    foreach (ModelCurve m in footPrintToModelCurveMapping)
        //    {
        //        footprintRoof.set_DefinesSlope(m, true);
        //        footprintRoof.set_SlopeAngle(m, 0.75);
        //    }

        //}

    }


}
