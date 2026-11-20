using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MLEI_Hydraulics.UI
{
    /// <summary>The window size from the last time the tool center was closed.</summary>
    public class ToolCenterSettings
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>
    /// The shell of the tool center. It knows nothing about individual tools:
    /// it just builds one tab per entry in <see cref="ToolRegistry"/>.
    /// </summary>
    public partial class ToolCenterWindow : Window
    {
        private const string SettingsFile = "ToolCenterSettings.xml";

        private readonly ToolContext _context;

        public ToolCenterWindow(ToolContext context)
        {
            _context = context;
            InitializeComponent();

            RestoreSize();
            Closing += (s, e) => SaveSize();

            // Subscribe first so the automatically selected first tab gets loaded too
            ToolTabs.SelectionChanged += OnTabSelectionChanged;

            foreach (ToolDefinition tool in ToolRegistry.GetTools())
            {
                ToolTabs.Items.Add(new TabItem { Header = tool.Title, Tag = tool });
            }
        }

        /// <summary>Uses the size from last time (or the default), never bigger than the screen or smaller than the minimum.</summary>
        private void RestoreSize()
        {
            ToolCenterSettings saved = SettingsStore.Load<ToolCenterSettings>(SettingsFile);

            double width = saved.Width > 0 ? saved.Width : Width;
            double height = saved.Height > 0 ? saved.Height : Height;

            Rect screen = SystemParameters.WorkArea;
            Width = Math.Max(MinWidth, Math.Min(width, screen.Width));
            Height = Math.Max(MinHeight, Math.Min(height, screen.Height));
        }

        private void SaveSize()
        {
            if (WindowState == WindowState.Normal)
                SettingsStore.Save(SettingsFile, new ToolCenterSettings { Width = ActualWidth, Height = ActualHeight });
        }

        /// <summary>A tool's view is only built the first time its tab is opened.</summary>
        private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore selection events bubbling up from controls inside a tool (grids, combo boxes...)
            if (!ReferenceEquals(e.OriginalSource, ToolTabs))
                return;

            if (ToolTabs.SelectedItem is TabItem tab && tab.Content == null && tab.Tag is ToolDefinition tool)
            {
                tab.Content = BuildView(tool);
            }
        }

        private UserControl BuildView(ToolDefinition tool)
        {
            try
            {
                return tool.CreateView(_context);
            }
            catch (System.Exception ex)
            {
                // A broken tool must never take Revit down with it: show the error inside its own tab
                return new ErrorView(tool.Title, ex);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
