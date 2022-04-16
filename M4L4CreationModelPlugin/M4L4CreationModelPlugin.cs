using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            //транзакция с циклом для построения стен
            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            List<Wall> walls = WallCreator(doc, level1, level2);
            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

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

        public List<Wall> WallCreator(Document doc, Level levelone, Level leveltwo)
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
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

    }


}
