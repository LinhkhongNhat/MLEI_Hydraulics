using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MLEI_Hydraulics.Tools.ScheduleSync;
using MLEI_Hydraulics.Tools.SheetCreate;
using MLEI_Hydraulics.Tools.SheetExport;
using MLEI_Hydraulics.UI;

namespace MLEI_Hydraulics
{
    /// <summary>What every tool needs from Revit. Passed to each tool view when it is created.</summary>
    public sealed class ToolContext
    {
        public UIApplication UiApp { get; }
        public UIDocument UiDoc => UiApp.ActiveUIDocument;
        public Document Doc => UiDoc.Document;

        public ToolContext(UIApplication uiApp)
        {
            UiApp = uiApp;
        }
    }

    /// <summary>One tab in the tool center: a title and a factory that builds its view.</summary>
    public sealed class ToolDefinition
    {
        public string Title { get; }
        public Func<ToolContext, UserControl> CreateView { get; }

        public ToolDefinition(string title, Func<ToolContext, UserControl> createView)
        {
            Title = title;
            CreateView = createView;
        }
    }

    /// <summary>
    /// The single place that lists the tools. To add a tool: create its view (a UserControl)
    /// and add one line here. When a tool is built, replace its ComingSoonView line.
    /// </summary>
    public static class ToolRegistry
    {
        public static IReadOnlyList<ToolDefinition> GetTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition("Sheet create", ctx => new SheetCreateView(ctx)),

                new ToolDefinition("Sheet export", ctx => new SheetExportView(ctx)),

                new ToolDefinition("Schedule export / import", ctx => new ScheduleSyncView(ctx))
            };
        }
    }
}
