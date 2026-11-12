using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Plumbing;
using System.Windows.Interop;


namespace MLEI_Hydraulics
{
    public class AutoTag
    {
        /// <summary>Tags the selected pipes in the active view; vertical pipe tags go in the given direction.</summary>
        public static Result Run(ExternalCommandData commandData, VerticalTagDirection verticalDirection)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            // Get current selection
            ICollection<ElementId> selectedIds =
                uiDoc.Selection.GetElementIds();

            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Auto Tag", "Please select one or more pipes first.");
                return Result.Cancelled;
            }

            // Filter selection to pipes only
            List<Pipe> pipes = selectedIds
                .Select(id => doc.GetElement(id))
                .OfType<Pipe>()
                .ToList();

            if (pipes.Count == 0)
            {
                TaskDialog.Show("Auto Tag", "No pipes found in the selection.");
                return Result.Cancelled;
            }

            int taggedCount = 0;

            List<FamilySymbol> pipeTagTypes = GetPipeTagTypes(doc);

            // Use the saved tag types; if one is missing in this project, let the user pick
            AutoTagSettings settings = AutoTagSettings.Load();
            if (FindTagType(pipeTagTypes, settings.Horizontal) == null ||
                FindTagType(pipeTagTypes, settings.Sloped) == null ||
                FindTagType(pipeTagTypes, settings.Vertical) == null)
            {
                settings = ShowSettingsWindow(commandData, pipeTagTypes, settings);
                if (settings == null)
                    return Result.Cancelled;
            }

            using (Transaction trans = new Transaction(doc, "Auto Tag Pipes"))
            {
                trans.Start();

                FamilySymbol horizontalTagType = FindTagType(pipeTagTypes, settings.Horizontal);
                FamilySymbol slopedTagType = FindTagType(pipeTagTypes, settings.Sloped);
                FamilySymbol verticalTagType = FindTagType(pipeTagTypes, settings.Vertical);

                if (horizontalTagType == null)
                {
                    TaskDialog.Show("Auto Tag", $"Cannot find {settings.Horizontal}");
                    return Result.Failed;
                }

                if (slopedTagType == null)
                {
                    TaskDialog.Show("Auto Tag", $"Cannot find {settings.Sloped}");
                    return Result.Failed;
                }

                if (verticalTagType == null)
                {
                    TaskDialog.Show("Auto Tag", $"Cannot find {settings.Vertical}");
                    return Result.Failed;
                }

                // A tag type that is loaded but never used can be inactive; activate the ones we will use
                foreach (FamilySymbol s in new[] { horizontalTagType, slopedTagType, verticalTagType })
                {
                    if (!s.IsActive) s.Activate();
                }

                HashSet<ElementId> taggedPipeIds = new HashSet<ElementId>();

                var existingTags = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>();

                foreach (IndependentTag tag in existingTags)
                {
                    foreach (LinkElementId taggedId in tag.GetTaggedElementIds())
                    {
                        taggedPipeIds.Add(taggedId.HostElementId);
                    }
                }

                List<VPipe> verticalPipes = new List<VPipe>();

                foreach (Pipe pipe in pipes)
                {
                    if (taggedPipeIds.Contains(pipe.Id))
                        continue;

                    // get the location of the pipe
                    LocationCurve locationCurve =
                        pipe.Location as LocationCurve;

                    if (locationCurve == null)
                        continue;

                    // check the pipe length and only tag if the length is greater than 2.5 meters
                    double minLength = UnitUtils.ConvertToInternalUnits(2000, UnitTypeId.Millimeters); // 2.0 meters in feet
                    if (locationCurve.Curve.Length < minLength)
                    {
                        continue; // Skip this pipe if it's too short
                    }

                    Curve curve = locationCurve.Curve;

                    // get the pipe's diamter and calculate the offset for the tag

                    double diameter = pipe.Diameter; // Diameter in feet

                    // Find the perpendicular direction to the pipe's curve for the tag offset
                    Line line = curve as Line;

                    // Skip if the curve is not a line (e.g., if it's an arc)
                    if (line == null)
                        continue;

                    XYZ direction = (line.GetEndPoint(1)-line.GetEndPoint(0)).Normalize();
                    // separate horizontal, sloped, and vertical pipes
                    bool isVertical = Math.Abs(direction.Z) > 0.99;

                    bool isSloped = Math.Abs(direction.Z) > 0.001 && Math.Abs(direction.Z) < 0.99;

                    FamilySymbol selectedTagType;

                    if (isVertical)
                    {
                        selectedTagType = verticalTagType;
                    }
                    else if (isSloped)
                    {
                        selectedTagType = slopedTagType;
                    }
                    else
                    {
                        selectedTagType = horizontalTagType;
                    }

                    //Create a perpendicular vector in the XY plane
                    XYZ offsetDirection = new XYZ(-direction.Y, direction.X, 0).Normalize();

                    // Midpoint of pipe
                    XYZ midPoint = curve.Evaluate(0.5, true);

                    // create a tag for vertical pipe

                    if (isVertical)
                    {
                        verticalPipes.Add(new VPipe
                        {
                            Pipe = pipe,
                            Pt = midPoint,
                            R = midPoint.DotProduct(doc.ActiveView.RightDirection),
                            U = midPoint.DotProduct(doc.ActiveView.UpDirection)
                        });
                        continue;
                    }

                    // Create a tag for horizontal and sloped pipe
                    try
                    {
                        IndependentTag tag = IndependentTag.Create(
                            doc,
                            doc.ActiveView.Id,
                            new Reference(pipe),
                            false,
                            TagMode.TM_ADDBY_CATEGORY,
                            TagOrientation.Horizontal,
                            midPoint);
                        tag.ChangeTypeId(selectedTagType.Id);
                        taggedCount++;

                        // Define the distance to move the tag away from the pipe based on its diameter, right now its 0.7 feet away from the pipe, you can adjust this value as needed

                        double moveDistance = diameter +0.7;
                        tag.TagHeadPosition = midPoint + offsetDirection * moveDistance;
                    }

                    catch
                    {
                        // Skip any pipe that fails to tag
                    }
                }
                // OLD:  ... verticalTagType, settings.VerticalDirection, ref taggedCount);
                PlaceVerticalTags(doc, doc.ActiveView, verticalPipes, verticalTagType, verticalDirection, ref taggedCount);
                trans.Commit();
            }

            return Result.Succeeded;
        }
        #region handle vertical pipes
        private class VPipe
        {
            public Pipe Pipe;
            public XYZ Pt;
            public double R;   // position along view right axis
            public double U;   // position along view up axis
        }
        private static void PlaceVerticalTags( Document doc, View view, List<VPipe> vPipes, FamilySymbol tagType, VerticalTagDirection direction, ref int taggedCount)
        {
            if (vPipes.Count == 0) return;

            XYZ right = view.RightDirection;
            XYZ up = view.UpDirection;
            double mm = view.Scale * UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);
            double groupDist = UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters); // model distance

            // Direction as signs
            int sx = (direction == VerticalTagDirection.UpRight || direction == VerticalTagDirection.DownRight) ? 1 : -1;
            int sy = (direction == VerticalTagDirection.UpRight || direction == VerticalTagDirection.UpLeft) ? 1 : -1;

            // Cluster pipes: within 500 mm in plan (chained)
            List<List<VPipe>> clusters = new List<List<VPipe>>();
            foreach (VPipe v in vPipes)
            {
                List<VPipe> merged = new List<VPipe> { v };
                for (int i = clusters.Count -1; i>= 0; i--)
                {
                    bool near = clusters[i].Any(o =>Math.Sqrt(Math.Pow(o.R - v.R, 2) + Math.Pow(o.U - v.U, 2)) <= groupDist);
                    if (near)
                    {
                        merged.AddRange(clusters[i]);
                        clusters.RemoveAt(i);
                    }

                }    
                clusters.Add(merged);
            }

            // Place each cluster as a touching row of tags
            foreach (List<VPipe> cluster in clusters)
            {
                cluster.Sort((a, b) => a.R.CompareTo(b.R));   // left to right in the view

                // Create tags with the leader off to measure clean widths
                List<IndependentTag> tags = new List<IndependentTag>();
                foreach (VPipe v in cluster)
                {
                    IndependentTag t = IndependentTag.Create(
                        doc, tagType.Id, view.Id, new Reference(v.Pipe),
                        false, TagOrientation.Horizontal, v.Pt);
                    tags.Add(t);
                }
                doc.Regenerate();

                const double tagWidthMm = 9.0;   // adjust, see below
                List<double> widths = tags.Select(t => tagWidthMm * mm).ToList();
                double total = widths.Sum();

                // Index of the tag/pipe that keeps the leader:
                // leftmost when going right, rightmost when going left (mirror)
                int leaderIdx = sx > 0 ? 0 : cluster.Count - 1;
                VPipe anchor = cluster[leaderIdx];

                // Horizontal: where the row's left edge starts (view coordinates)
                double leftEdgeR = sx > 0
                    ? anchor.R + 11 * mm               // row starts just right of the anchor pipe
                    : anchor.R - 11 * mm - total;      // row ends just left of the anchor pipe

                // Vertical: above or below
                double rowU;
                if (cluster.Count == 1)
                    rowU = anchor.U + sy * 8 * mm;
                else
                    rowU = sy > 0
                        ? cluster.Max(c => c.U) + 30 * mm
                        : cluster.Min(c => c.U) - 30 * mm;

                // Head positions: edges touching
                XYZ basePt = cluster[0].Pt;
                double baseR = cluster[0].R, baseU = cluster[0].U;
                List<XYZ> heads = new List<XYZ>();
                double cursor = leftEdgeR;
                for (int i = 0; i < tags.Count; i++)
                {
                    double centerR = cursor + widths[i] / 2.0;
                    heads.Add(basePt + right * (centerR - baseR) + up * (rowU - baseU));
                    cursor += widths[i];
                }

                // Only the leader tag gets a leader (attached to its pipe)
                tags[leaderIdx].HasLeader = true;
                tags[leaderIdx].LeaderEndCondition = LeaderEndCondition.Attached;
                doc.Regenerate();

                for (int i = 0; i < tags.Count; i++)
                    tags[i].TagHeadPosition = heads[i];
                doc.Regenerate();

                // Short horizontal run into the tag's near edge:
                // left edge when going right, right edge when going left
                XYZ elbow = heads[leaderIdx] - right * (sx * (widths[leaderIdx] / 2.0 + 3 * mm));
                tags[leaderIdx].SetLeaderElbow(new Reference(anchor.Pipe), elbow);

                taggedCount += tags.Count;

            }
        }
        #endregion

        #region helper methods

        private static FamilySymbol FindTagType(IEnumerable<FamilySymbol> types, TagTypeRef r)
        {
            if (r == null) return null;
            return types.FirstOrDefault(x => x.FamilyName == r.FamilyName && x.Name == r.TypeName);
        }
        internal static List<FamilySymbol> GetPipeTagTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.Category != null && x.Category.Id.Value == (int)BuiltInCategory.OST_PipeTags)
                .ToList();
        }

        /// <summary>Shows the settings window. Returns the saved choices, or null if cancelled.</summary>
        internal static AutoTagSettings ShowSettingsWindow(
            ExternalCommandData commandData, IEnumerable<FamilySymbol> pipeTagTypes, AutoTagSettings current)
        {
            AutoTagWindow window = new AutoTagWindow(
                pipeTagTypes.Select(s => new TagTypeRef(s.FamilyName, s.Name)),
                current);
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            if (window.ShowDialog() != true)
                return null;

            AutoTagSettings chosen = window.ChosenSettings;
            chosen.Save();
            return chosen;
        }
        #endregion
    }

}