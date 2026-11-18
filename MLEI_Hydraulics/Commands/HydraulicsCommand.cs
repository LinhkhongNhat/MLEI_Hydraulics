using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using MLEI_Hydraulics.UI;

// Keep this namespace: Main.cs refers to "MLEI_Hydraulics.HydraulicsCommand" by name.
namespace MLEI_Hydraulics
{
    [Transaction(TransactionMode.Manual)]
    public class HydraulicsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ToolContext context = new ToolContext(commandData.Application);
            ToolCenterWindow window = new ToolCenterWindow(context);

            // Owned by Revit's main window so it opens on the same screen and stays on top of it
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            // Modal: Revit API calls (and their transactions) are valid while it is open,
            // so each tool opens and commits its own transaction when its button is pressed.
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
