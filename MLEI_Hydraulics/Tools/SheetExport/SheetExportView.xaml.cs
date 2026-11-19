using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        /// <summary>The only UI-specific logic: showing the folder dialog. Everything else lives in the view model.</summary>
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
