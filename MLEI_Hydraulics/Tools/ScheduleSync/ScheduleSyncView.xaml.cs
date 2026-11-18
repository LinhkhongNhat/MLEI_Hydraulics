using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MLEI_Hydraulics.Tools.ScheduleSync
{
    public partial class ScheduleSyncView : UserControl
    {
        private readonly ScheduleSyncViewModel _viewModel;

        public ScheduleSyncView(ToolContext context)
        {
            InitializeComponent();
            _viewModel = new ScheduleSyncViewModel(context.Doc);
            DataContext = _viewModel;
        }

        /// <summary>Only the file dialogs live here. Everything else is in the view model.</summary>
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedSchedule == null)
                return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export the schedule",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = SafeFileName(_viewModel.SelectedSchedule.Name) + ".csv"
            };

            if (!string.IsNullOrWhiteSpace(_viewModel.LastFolder) && Directory.Exists(_viewModel.LastFolder))
                dialog.InitialDirectory = _viewModel.LastFolder;

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                _viewModel.ExportTo(dialog.FileName);
        }

        private void ChooseImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Choose the edited CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (!string.IsNullOrWhiteSpace(_viewModel.LastFolder) && Directory.Exists(_viewModel.LastFolder))
                dialog.InitialDirectory = _viewModel.LastFolder;

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                _viewModel.LoadImport(dialog.FileName);
        }

        private static string SafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
