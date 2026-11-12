using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;


namespace MLEI_Hydraulics
{
    public static class SheetDataService
    {
        public static List<SheetRow> GetProjectSheets(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(vs => !vs.IsPlaceholder)
                .OrderBy(vs => vs.SheetNumber)
                .Select(vs => new SheetRow
                {
                    SheetNumber = vs.SheetNumber,
                    SheetName = vs.Name
                })
                .ToList();

        }
    }
        public class SheetRow
    {
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
    }

}
