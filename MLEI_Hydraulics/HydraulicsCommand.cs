using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;

namespace MLEI_Hydraulics
{
    [Transaction(TransactionMode.Manual)]
    internal class HydraulicsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            mleiWindow window = new mleiWindow(doc);

            // Attach to Revit's main window so it opens on the same screen
            IntPtr revitHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            WindowInteropHelper helper = new WindowInteropHelper(window);
            helper.Owner = revitHandle;

            // Show a modal dialog to the user. Do not keep a Transaction open while waiting on UI.
            bool? dialogResult = window.ShowDialog();

            if (dialogResult == true)
            {
                using (Transaction tx = new Transaction(doc, "Hydraulics Change"))
                {
                    tx.Start();
                    // TODO: perform actual model edits here. This is a placeholder commit.
                    tx.Commit();
                }
            }

            return Result.Succeeded;
        }
    }
}