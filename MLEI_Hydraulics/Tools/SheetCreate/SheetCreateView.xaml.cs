using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MLEI_Hydraulics.Tools.SheetCreate
{
    public partial class SheetCreateView : UserControl
    {
        private readonly SheetCreateViewModel _viewModel;

        public SheetCreateView(ToolContext context)
        {
            InitializeComponent();
            _viewModel = new SheetCreateViewModel(context.Doc);
            DataContext = _viewModel;
        }

        /// <summary>Only the file dialogs live here. Everything else is in the view model.</summary>
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Choose the sheet list",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (!string.IsNullOrWhiteSpace(_viewModel.LastFolder) && Directory.Exists(_viewModel.LastFolder))
                dialog.InitialDirectory = _viewModel.LastFolder;

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                _viewModel.LoadFile(dialog.FileName);
        }

        private void Template_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save a sheet list template",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "SheetList_template.csv"
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            try
            {
                SheetCreateService.WriteTemplate(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the template: " + ex.Message, "Sheet create");
            }
        }
    }
}
