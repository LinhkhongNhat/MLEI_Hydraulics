using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace MLEI_Hydraulics.Tools.SheetExport
{
    public partial class SheetExportView : UserControl
    {
        private readonly SheetExportViewModel _viewModel;

        public SheetExportView(ToolContext context)
        {
            InitializeComponent();
            _viewModel = new SheetExportViewModel(context.Doc);
            DataContext = _viewModel;

            // Options are also saved shortly after every change; this makes sure nothing is lost on close
            Unloaded += (s, e) => _viewModel.FlushSettings();
        }

        /// <summary>Clicking into a part's prefix or suffix box also selects that part, so Up / Down / Remove act on it.</summary>
        private void PartBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is FrameworkElement box && box.DataContext is NamePart part)
                _viewModel.SelectedPart = part;
        }

        /// <summary>The only other UI-specific logic: showing the folder dialog. Everything else lives in the view model.</summary>
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog { Title = "Choose the export folder" };

            if (!string.IsNullOrWhiteSpace(_viewModel.OutputFolder) && Directory.Exists(_viewModel.OutputFolder))
                dialog.InitialDirectory = _viewModel.OutputFolder;

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                _viewModel.OutputFolder = dialog.FolderName;
        }
    }
}
