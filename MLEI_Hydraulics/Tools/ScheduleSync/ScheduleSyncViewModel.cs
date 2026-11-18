using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MLEI_Hydraulics.Tools.ScheduleSync
{
    public sealed class ScheduleSyncViewModel : ViewModelBase
    {
        private readonly Document _doc;
        private readonly ScheduleSyncSettings _settings;
        private List<Element> _elements = new List<Element>();
        private bool _suspendSummary;

        public ScheduleSyncViewModel(Document doc)
        {
            _doc = doc;
            _settings = ScheduleSyncSettings.Load();
            _allowTypeEdit = _settings.AllowTypeEdit;

            Schedules = new ObservableCollection<ScheduleOption>(ScheduleSyncService.GetSchedules(doc));
            SchedulesView = CollectionViewSource.GetDefaultView(Schedules);
            SchedulesView.Filter = FilterSchedule;

            Columns = new ObservableCollection<ScheduleColumn>();
            Changes = new ObservableCollection<ScheduleChange>();

            ApplyCommand = new RelayCommand(_ => Apply(), _ => CanApply());
            ReloadImportCommand = new RelayCommand(
                _ => LoadImport(ImportPath),
                _ => SelectedSchedule != null && !string.IsNullOrWhiteSpace(ImportPath));
            SelectAllChangesCommand = new RelayCommand(_ => SetAllChanges(true));
            SelectNoChangesCommand = new RelayCommand(_ => SetAllChanges(false));

            StatusText = "Select a schedule on the left.";
        }

        #region Data the view binds to

        public ObservableCollection<ScheduleOption> Schedules { get; }
        public ICollectionView SchedulesView { get; }
        public ObservableCollection<ScheduleColumn> Columns { get; }
        public ObservableCollection<ScheduleChange> Changes { get; }

        public ICommand ApplyCommand { get; }
        public ICommand ReloadImportCommand { get; }
        public ICommand SelectAllChangesCommand { get; }
        public ICommand SelectNoChangesCommand { get; }

        public string LastFolder => _settings.LastFolder;

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                    SchedulesView.Refresh();
            }
        }

        private ScheduleOption _selectedSchedule;
        public ScheduleOption SelectedSchedule
        {
            get => _selectedSchedule;
            set
            {
                if (Set(ref _selectedSchedule, value))
                    LoadSchedule();
            }
        }

        private bool _allowTypeEdit;
        public bool AllowTypeEdit
        {
            get => _allowTypeEdit;
            set
            {
                if (Set(ref _allowTypeEdit, value))
                {
                    _settings.AllowTypeEdit = value;
                    _settings.Save();
                    LoadSchedule(); // editable columns change, so the lists are rebuilt
                }
            }
        }

        private bool _isExportMode = true;
        public bool IsExportMode
        {
            get => _isExportMode;
            set
            {
                if (Set(ref _isExportMode, value))
                {
                    OnPropertyChanged(nameof(IsImportMode));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsImportMode
        {
            get => !_isExportMode;
            set => IsExportMode = !value;
        }

        private string _elementCountText = "";
        public string ElementCountText
        {
            get => _elementCountText;
            private set => Set(ref _elementCountText, value);
        }

        private string _importPath = "";
        public string ImportPath
        {
            get => _importPath;
            private set => Set(ref _importPath, value);
        }

        private string _summaryText = "";
        public string SummaryText
        {
            get => _summaryText;
            private set => Set(ref _summaryText, value);
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set => Set(ref _statusText, value);
        }

        public bool CanExport => _selectedSchedule != null && _elements.Count > 0 && Columns.Any(c => c.IsSupported && c.IsIncluded);

        #endregion

        #region Schedule selection

        private bool FilterSchedule(object o)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            ScheduleOption item = (ScheduleOption)o;
            return item.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || item.CategoryName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadSchedule()
        {
            foreach (ScheduleColumn c in Columns)
                c.PropertyChanged -= OnColumnChanged;

            Columns.Clear();
            Changes.Clear();
            _elements = new List<Element>();
            SummaryText = "";

            if (_selectedSchedule != null)
            {
                try
                {
                    ViewSchedule schedule = (ViewSchedule)_doc.GetElement(_selectedSchedule.Id);
                    _elements = ScheduleSyncService.GetElements(_doc, schedule);

                    foreach (ScheduleColumn column in ScheduleSyncService.BuildColumns(_doc, schedule, _elements, _allowTypeEdit))
                    {
                        column.PropertyChanged += OnColumnChanged;
                        Columns.Add(column);
                    }

                    ElementCountText = $"{_elements.Count} elements in this schedule";
                    StatusText = _elements.Count == 0
                        ? "This schedule shows no elements."
                        : "Tick the columns to export, then press Export.";
                }
                catch (Exception ex)
                {
                    ElementCountText = "";
                    StatusText = "Could not read the schedule: " + ex.Message;
                }
            }
            else
            {
                ElementCountText = "";
                StatusText = "Select a schedule on the left.";
            }

            OnPropertyChanged(nameof(CanExport));
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnColumnChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanExport));
        }

        #endregion

        #region Export

        /// <summary>Called by the view after the user chose where to save.</summary>
        public void ExportTo(string path)
        {
            try
            {
                ScheduleSyncService.Export(_doc, _elements, Columns, path);

                _settings.LastFolder = System.IO.Path.GetDirectoryName(path);
                _settings.Save();

                StatusText = $"Exported {_elements.Count} elements to {System.IO.Path.GetFileName(path)}. " +
                             "Edit the editable columns, then use Import.";
            }
            catch (Exception ex)
            {
                // Most often: the file is open in Excel
                StatusText = "Could not write the file: " + ex.Message;
            }
        }

        #endregion

        #region Import

        /// <summary>Reads the edited CSV and lists what would change. Nothing is applied yet.</summary>
        public void LoadImport(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            ImportPath = path;
            ClearChanges();

            if (_selectedSchedule == null)
            {
                StatusText = "Select the schedule you exported from on the left first.";
                return;
            }

            try
            {
                ScheduleImportResult result = ScheduleSyncService.ReadImport(_doc, path, Columns, _elements);

                _suspendSummary = true;
                foreach (ScheduleChange change in result.Changes)
                {
                    change.PropertyChanged += OnChangeChanged;
                    Changes.Add(change);
                }
                _suspendSummary = false;

                _settings.LastFolder = System.IO.Path.GetDirectoryName(path);
                _settings.Save();

                UpdateSummary();

                if (result.Warning != null)
                    StatusText = result.Warning;
                else if (Changes.Count == 0)
                    StatusText = $"No differences found in {result.RowCount} rows.";
                else
                    StatusText = "Untick any change you do not want, then press Apply.";
            }
            catch (Exception ex)
            {
                SummaryText = "";
                StatusText = ex.Message;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearChanges()
        {
            foreach (ScheduleChange c in Changes)
                c.PropertyChanged -= OnChangeChanged;

            Changes.Clear();
            SummaryText = "";
        }

        private void OnChangeChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScheduleChange.IsSelected) && !_suspendSummary)
                UpdateSummary();
        }

        private void SetAllChanges(bool selected)
        {
            _suspendSummary = true;
            foreach (ScheduleChange c in Changes.Where(c => c.IsReady))
                c.IsSelected = selected;
            _suspendSummary = false;

            UpdateSummary();
        }

        private bool CanApply()
            => IsImportMode && Changes.Any(c => c.IsReady && c.IsSelected);

        private void Apply()
        {
            List<ScheduleChange> toApply = Changes.Where(c => c.IsReady && c.IsSelected).ToList();

            try
            {
                ScheduleSyncService.Apply(_doc, toApply);

                int applied = toApply.Count(c => c.IsApplied);
                int failed = toApply.Count - applied;
                StatusText = $"Done: {applied} values applied, {failed} failed.";
            }
            catch (Exception ex)
            {
                StatusText = "Nothing was applied.";
                TaskDialog.Show("Schedule import", ex.Message);
            }

            UpdateSummary();
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateSummary()
        {
            int selected = Changes.Count(c => c.IsReady && c.IsSelected);
            int applied = Changes.Count(c => c.IsApplied);
            int problems = Changes.Count(c => c.IsProblem);

            List<string> parts = new List<string>();
            if (applied > 0) parts.Add($"{applied} applied");
            if (selected > 0) parts.Add($"{selected} to apply");
            if (problems > 0) parts.Add($"{problems} with problems (skipped)");

            SummaryText = string.Join("  |  ", parts);
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion
    }
}
