using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MLEI_Hydraulics.Tools.SheetCreate
{
    public sealed class SheetCreateViewModel : ViewModelBase
    {
        private readonly Document _doc;
        private readonly Dispatcher _dispatcher;
        private readonly SheetCreateSettings _settings;

        public SheetCreateViewModel(Document doc)
        {
            _doc = doc;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _settings = SheetCreateSettings.Load();

            Rows = new ObservableCollection<SheetCreateRow>();
            Rows.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsEmpty));

            TitleBlocks = SheetCreateService.GetTitleBlocks(doc);
            _selectedTitleBlock =
                TitleBlocks.FirstOrDefault(t => t.Display == _settings.TitleBlock)
                ?? TitleBlocks.FirstOrDefault(t => !t.IsNone)
                ?? TitleBlockOption.None;

            CreateCommand = new RelayCommand(_ => Create(), _ => CanCreate());
            ReloadCommand = new RelayCommand(
                _ => LoadFile(CsvPath),
                _ => !IsBusy && !string.IsNullOrWhiteSpace(CsvPath));
        }

        public ObservableCollection<SheetCreateRow> Rows { get; }
        public List<TitleBlockOption> TitleBlocks { get; }
        public ICommand CreateCommand { get; }
        public ICommand ReloadCommand { get; }

        public bool IsEmpty => Rows.Count == 0;
        public string LastFolder => _settings.LastFolder;

        private TitleBlockOption _selectedTitleBlock;
        public TitleBlockOption SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set
            {
                if (Set(ref _selectedTitleBlock, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _csvPath = "";
        public string CsvPath
        {
            get => _csvPath;
            private set => Set(ref _csvPath, value);
        }

        private string _summaryText = "";
        public string SummaryText
        {
            get => _summaryText;
            private set => Set(ref _summaryText, value);
        }

        private string _statusText = "Choose a CSV file with the sheet numbers and names.";
        public string StatusText
        {
            get => _statusText;
            private set => Set(ref _statusText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (Set(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(InputEnabled));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool InputEnabled => !_isBusy;

        /// <summary>Reads and checks the CSV, then fills the preview list.</summary>
        public void LoadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            CsvPath = path;
            Rows.Clear();

            try
            {
                SheetListReadResult result = SheetCreateService.ReadSheetList(
                    path, SheetCreateService.GetExistingSheetNumbers(_doc));

                foreach (SheetCreateRow row in result.Rows)
                    Rows.Add(row);

                _settings.LastFolder = System.IO.Path.GetDirectoryName(path);
                SaveSettings();

                UpdateSummary();
                StatusText = result.Warning
                    ?? (Rows.Count == 0
                        ? "No sheets found in the file."
                        : "Check the list, then press Create sheets.");
            }
            catch (Exception ex)
            {
                SummaryText = "";
                StatusText = "Could not read the file: " + ex.Message;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanCreate()
            => !IsBusy && SelectedTitleBlock != null && Rows.Any(r => r.IsReady);

        private void Create()
        {
            SaveSettings();

            List<SheetCreateRow> ready = Rows.Where(r => r.IsReady).ToList();

            IsBusy = true;
            StatusText = $"Creating {ready.Count} sheets...";
            _dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { })); // repaint before Revit gets busy

            try
            {
                SheetCreateResult result = SheetCreateService.Create(_doc, ready, SelectedTitleBlock);
                StatusText = $"Done: {result.Created} created, {result.Failed} failed.";
            }
            catch (Exception ex)
            {
                StatusText = "Nothing was created.";
                TaskDialog.Show("Sheet create", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int ready = Rows.Count(r => r.IsReady);
            int created = Rows.Count(r => r.IsCreated);
            int problems = Rows.Count(r => r.IsProblem);

            List<string> parts = new List<string>();
            if (created > 0) parts.Add($"{created} created");
            if (ready > 0) parts.Add($"{ready} ready to create");
            if (problems > 0) parts.Add($"{problems} with problems (skipped)");

            SummaryText = string.Join("  |  ", parts);
        }

        private void SaveSettings()
        {
            _settings.TitleBlock = SelectedTitleBlock?.Display ?? "";
            _settings.Save();
        }
    }
}
