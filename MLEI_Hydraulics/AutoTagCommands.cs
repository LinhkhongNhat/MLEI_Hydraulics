using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MLEI_Hydraulics
{
    [Transaction(TransactionMode.Manual)]
    public class AutoTagUpLeft : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => AutoTag.Run(commandData, VerticalTagDirection.UpLeft);
    }

    [Transaction(TransactionMode.Manual)]
    public class AutoTagUpRight : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => AutoTag.Run(commandData, VerticalTagDirection.UpRight);
    }

    [Transaction(TransactionMode.Manual)]
    public class AutoTagDownLeft : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => AutoTag.Run(commandData, VerticalTagDirection.DownLeft);
    }

    [Transaction(TransactionMode.Manual)]
    public class AutoTagDownRight : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => AutoTag.Run(commandData, VerticalTagDirection.DownRight);
    }

    [Transaction(TransactionMode.Manual)]
    public class AutoTagSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            AutoTagSettings chosen = AutoTag.ShowSettingsWindow(
                commandData, AutoTag.GetPipeTagTypes(doc), AutoTagSettings.Load());

            return chosen == null ? Result.Cancelled : Result.Succeeded;
        }
    }
}