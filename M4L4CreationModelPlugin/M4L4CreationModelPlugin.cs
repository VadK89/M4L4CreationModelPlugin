using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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


            WallCreator(doc, level1, level2);
            return Result.Succeeded;
        }
        public void WallCreator(Document doc, Level levelone, Level leveltwo)
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
            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, levelone.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(leveltwo.Id);
            }
            transaction.Commit();
        }

    }

     
}
