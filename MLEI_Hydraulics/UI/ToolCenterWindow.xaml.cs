using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MLEI_Hydraulics.UI
{
    /// <summary>
    /// The shell of the tool center. It knows nothing about individual tools:
    /// it just builds one tab per entry in <see cref="ToolRegistry"/>.
    /// </summary>
    public partial class ToolCenterWindow : Window
    {
        private readonly ToolContext _context;

        public ToolCenterWindow(ToolContext context)
        {
            _context = context;
            InitializeComponent();

            // Subscribe first so the automatically selected first tab gets loaded too
            ToolTabs.SelectionChanged += OnTabSelectionChanged;

            foreach (ToolDefinition tool in ToolRegistry.GetTools())
            {
                ToolTabs.Items.Add(new TabItem { Header = tool.Title, Tag = tool });
            }
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
