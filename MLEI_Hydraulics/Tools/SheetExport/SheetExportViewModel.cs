using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MLEI_Hydraulics.Tools.SheetExport
{
    public sealed class SheetExportViewModel : ViewModelBase
    {
        private const string DefaultSetupLabel = "<Default export options>";

        private readonly Document _doc;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _saveTimer;
        private readonly HashSet<string> _sheetFieldNames;
        private readonly HashSet<string> _titleBlockFieldNames;
        private readonly HashSet<string> _projectFieldNames;
        private readonly Dictionary<SheetExportItem, string> _uniqueIds = new Dictionary<SheetExportItem, string>();
        private readonly string _projectKey;
        private List<SheetSetSetting> _otherProjectSets = new List<SheetSetSetting>();
        private bool _suspendRefresh;
        private bool _suspendSetApply;

        /// <summary>The first entry of the set box. Choosing it unticks every sheet. It is never saved.</summary>
        private static readonly SheetSetSetting NoneSet = new SheetSetSetting { Name = "(none)" };

        public SheetExportViewModel(Document doc)
        {
            _doc = doc;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _projectKey = GetProjectKey(doc);

            // Options are saved shortly after the last change, so closing the tool never loses them
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                SaveSettings();
            };

            Sheets = new ObservableCollection<SheetExportItem>(SheetExportService.GetSheets(doc));
            foreach (SheetExportItem item in Sheets)
            {
                item.PropertyChanged += OnItemChanged;
                _uniqueIds[item] = doc.GetElement(item.Id)?.UniqueId ?? "";
            }

            SheetsView = CollectionViewSource.GetDefaultView(Sheets);
            SheetsView.Filter = FilterSheet;

            DwgSetups = new List<string> { DefaultSetupLabel };
            DwgSetups.AddRange(SheetExportService.GetDwgSetups(doc));

            AvailableFields = SheetExportService.GetAvailableFields(Sheets);
            _sheetFieldNames = new HashSet<string>(
                AvailableFields.Where(f => f.Kind == NamePartKind.SheetParameter).Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);
            _titleBlockFieldNames = new HashSet<string>(
                AvailableFields.Where(f => f.Kind == NamePartKind.TitleBlockParameter).Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);
            _projectFieldNames = new HashSet<string>(
                AvailableFields.Where(f => f.Kind == NamePartKind.ProjectParameter).Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);
            _selectedField = AvailableFields.FirstOrDefault();

            Parts = new ObservableCollection<NamePart>();
            Parts.CollectionChanged += (s, e) =>
            {
                RefreshNames();
                ScheduleSave();
            };

            SheetSets = new ObservableCollection<SheetSetSetting> { NoneSet };
            _selectedSet = NoneSet;
            _setName = NoneSet.Name;

            _suspendRefresh = true;
            LoadSettings();
            _suspendRefresh = false;

            SelectAllCommand = new RelayCommand(_ => SetVisibleSelection(true));
            SelectNoneCommand = new RelayCommand(_ => SetVisibleSelection(false));
            ExportCommand = new RelayCommand(_ => Export(), _ => CanExport());
            OpenFolderCommand = new RelayCommand(
                _ => System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = OutputFolder, UseShellExecute = true }),
                _ => !string.IsNullOrWhiteSpace(OutputFolder) && System.IO.Directory.Exists(OutputFolder));

            AddFieldCommand = new RelayCommand(_ => AddField(), _ => SelectedField != null);
            AddTextCommand = new RelayCommand(_ => AddText(), _ => !string.IsNullOrEmpty(NewText));
            MoveUpCommand = new RelayCommand(_ => MovePart(-1), _ => CanMovePart(-1));
            MoveDownCommand = new RelayCommand(_ => MovePart(1), _ => CanMovePart(1));
            RemovePartCommand = new RelayCommand(_ => RemovePart(), _ => SelectedPart != null);

            SaveSetCommand = new RelayCommand(_ => SaveSet(), _ => CanSaveSet());
            DeleteSetCommand = new RelayCommand(_ => DeleteSet(), _ => FindSet(SetName) != null);

            RefreshNames();
        }

        #region Data the view binds to

        public ObservableCollection<SheetExportItem> Sheets { get; }
        public ICollectionView SheetsView { get; }
        public List<string> DwgSetups { get; }

        /// <summary>The pieces of the file name, in order.</summary>
        public ObservableCollection<NamePart> Parts { get; }

        /// <summary>The parameters that can be added as a piece.</summary>
        public List<AvailableField> AvailableFields { get; }

        /// <summary>The saved sheet sets of the current project.</summary>
        public ObservableCollection<SheetSetSetting> SheetSets { get; }

        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand AddFieldCommand { get; }
        public ICommand AddTextCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand RemovePartCommand { get; }
        public ICommand SaveSetCommand { get; }
        public ICommand DeleteSetCommand { get; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                    SheetsView.Refresh();
            }
        }

        private bool _isPdf = true;
        public bool IsPdf
        {
            get => _isPdf;
            set
            {
                if (Set(ref _isPdf, value))
                {
                    OnPropertyChanged(nameof(IsDwg));
                    RefreshNames();
                    ScheduleSave();
                }
            }
        }

        public bool IsDwg
        {
            get => !_isPdf;
            set => IsPdf = !value;
        }

        private string _selectedDwgSetup = DefaultSetupLabel;
        public string SelectedDwgSetup
        {
            get => _selectedDwgSetup;
            set { if (Set(ref _selectedDwgSetup, value)) ScheduleSave(); }
        }

        private bool _mergeViews = true;
        public bool MergeViews
        {
            get => _mergeViews;
            set { if (Set(ref _mergeViews, value)) ScheduleSave(); }
        }

        private bool _overwrite;
        public bool Overwrite
        {
            get => _overwrite;
            set { if (Set(ref _overwrite, value)) ScheduleSave(); }
        }

        private string _outputFolder = "";
        public string OutputFolder
        {
            get => _outputFolder;
            set { if (Set(ref _outputFolder, value)) ScheduleSave(); }
        }

        private string _prefix = "";
        public string Prefix
        {
            get => _prefix;
            set { if (Set(ref _prefix, value)) { RefreshNames(); ScheduleSave(); } }
        }

        private string _suffix = "";
        public string Suffix
        {
            get => _suffix;
            set { if (Set(ref _suffix, value)) { RefreshNames(); ScheduleSave(); } }
        }

        private string _separator = " - ";
        public string Separator
        {
            get => _separator;
            set { if (Set(ref _separator, value)) { RefreshNames(); ScheduleSave(); } }
        }

        private AvailableField _selectedField;
        public AvailableField SelectedField
        {
            get => _selectedField;
            set => Set(ref _selectedField, value);
        }

        private string _newText = "";
        public string NewText
        {
            get => _newText;
            set => Set(ref _newText, value);
        }

        private NamePart _selectedPart;
        public NamePart SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (Set(ref _selectedPart, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        private SheetSetSetting _selectedSet;

        /// <summary>Picking a saved set ticks exactly the sheets in it.</summary>
        public SheetSetSetting SelectedSet
        {
            get => _selectedSet;
            set
            {
                if (Set(ref _selectedSet, value) && value != null && !_suspendSetApply)
                    ApplySet(value);
            }
        }

        private string _setName = "";

        /// <summary>The text in the set box: the name a set is saved under.</summary>
        public string SetName
        {
            get => _setName;
            set
            {
                if (Set(ref _setName, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _selectionText = "";
        public string SelectionText
        {
            get => _selectionText;
            private set => Set(ref _selectionText, value);
        }

        private string _statusText = "Tick the sheets to export.";
        public string StatusText
        {
            get => _statusText;
            private set => Set(ref _statusText, value);
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            private set => Set(ref _progressValue, value);
        }

        private int _progressMax = 1;
        public int ProgressMax
        {
            get => _progressMax;
            private set => Set(ref _progressMax, value);
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

        #endregion

        #region Saved sheet sets

        private SheetSetSetting FindSet(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return SheetSets.FirstOrDefault(s =>
                !ReferenceEquals(s, NoneSet) &&
                s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private bool CanSaveSet()
            => !IsBusy
               && !string.IsNullOrWhiteSpace(SetName)
               && !SetName.Trim().Equals(NoneSet.Name, StringComparison.OrdinalIgnoreCase)
               && Sheets.Any(s => s.IsSelected);

        /// <summary>Saves the ticked sheets under the typed name (replacing a set with the same name).</summary>
        private void SaveSet()
        {
            string name = SetName.Trim();

            List<SheetRefSetting> refs = Sheets
                .Where(s => s.IsSelected)
                .Select(s => new SheetRefSetting { UniqueId = _uniqueIds[s], Number = s.SheetNumber })
                .ToList();

            SheetSetSetting set = FindSet(name);
            if (set != null)
            {
                set.Sheets = refs;
                StatusText = $"Updated the set \"{set.Name}\" ({refs.Count} sheets).";
            }
            else
            {
                set = new SheetSetSetting { Name = name, ProjectKey = _projectKey, Sheets = refs };

                int index = 1; // position 0 is "(none)"
                while (index < SheetSets.Count &&
                       string.Compare(SheetSets[index].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
                    index++;
                SheetSets.Insert(index, set);

                StatusText = $"Saved the set \"{name}\" ({refs.Count} sheets).";
            }

            _suspendSetApply = true;
            SelectedSet = set;
            _suspendSetApply = false;

            SaveSettings();
        }

        private void DeleteSet()
        {
            SheetSetSetting set = FindSet(SetName);
            if (set == null)
                return;

            TaskDialog confirm = new TaskDialog("Sheet export")
            {
                MainInstruction = $"Delete the set \"{set.Name}\"?",
                MainContent = "The sheets themselves are not affected.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
            };

            if (confirm.Show() != TaskDialogResult.Yes)
                return;

            SheetSets.Remove(set);

            _suspendSetApply = true;
            SelectedSet = null;
            _suspendSetApply = false;
            SetName = "";

            SaveSettings();
            StatusText = $"Deleted the set \"{set.Name}\".";
        }

        /// <summary>Ticks exactly the sheets of the set. Sheets are found by unique id first, then by number.</summary>
        private void ApplySet(SheetSetSetting set)
        {
            if (ReferenceEquals(set, NoneSet))
            {
                _suspendRefresh = true;
                foreach (SheetExportItem item in Sheets)
                    item.IsSelected = false;
                _suspendRefresh = false;

                RefreshNames();
                StatusText = "Selection cleared.";
                return;
            }

            Dictionary<string, SheetExportItem> byUniqueId = new Dictionary<string, SheetExportItem>();
            Dictionary<string, SheetExportItem> byNumber = new Dictionary<string, SheetExportItem>(StringComparer.OrdinalIgnoreCase);

            foreach (SheetExportItem item in Sheets)
            {
                byUniqueId[_uniqueIds[item]] = item;
                byNumber[item.SheetNumber] = item;
            }

            int found = 0, missing = 0;

            _suspendRefresh = true;
            foreach (SheetExportItem item in Sheets)
                item.IsSelected = false;

            foreach (SheetRefSetting r in set.Sheets)
            {
                SheetExportItem match;
                if (byUniqueId.TryGetValue(r.UniqueId ?? "", out match) || byNumber.TryGetValue(r.Number ?? "", out match))
                {
                    match.IsSelected = true;
                    found++;
                }
                else
                {
                    missing++;
                }
            }
            _suspendRefresh = false;

            RefreshNames();

            StatusText = missing == 0
                ? $"Set \"{set.Name}\": {found} sheets ticked."
                : $"Set \"{set.Name}\": {found} sheets ticked, {missing} no longer in the project.";
        }

        /// <summary>Identifies the project so saved sets only show up in the project they were made in.</summary>
        private static string GetProjectKey(Document doc)
        {
            try
            {
                if (doc.IsWorkshared)
                {
                    ModelPath central = doc.GetWorksharingCentralModelPath();
                    if (central != null)
                        return ModelPathUtils.ConvertModelPathToUserVisiblePath(central).ToLowerInvariant();
                }
            }
            catch
            {
                // Fall through to the file path
            }

            string path = string.IsNullOrEmpty(doc.PathName) ? doc.Title : doc.PathName;
            return (path ?? "").ToLowerInvariant();
        }

        #endregion

        #region File name parts

        private void AddField()
        {
            NamePart part = new NamePart(SelectedField.Kind, SelectedField.Name);
            Parts.Add(part);
            SelectedPart = part;
        }

        private void AddText()
        {
            NamePart part = new NamePart(NamePartKind.Text, NewText);
            Parts.Add(part);
            SelectedPart = part;
            NewText = "";
        }

        private bool CanMovePart(int direction)
        {
            if (SelectedPart == null)
                return false;

            int target = Parts.IndexOf(SelectedPart) + direction;
            return target >= 0 && target < Parts.Count;
        }

        private void MovePart(int direction)
        {
            int index = Parts.IndexOf(SelectedPart);
            Parts.Move(index, index + direction);
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemovePart()
        {
            Parts.Remove(SelectedPart);
            CommandManager.InvalidateRequerySuggested();
        }

        private bool IsMissing(NamePart part)
        {
            switch (part.Kind)
            {
                case NamePartKind.SheetParameter: return !_sheetFieldNames.Contains(part.Value);
                case NamePartKind.TitleBlockParameter: return !_titleBlockFieldNames.Contains(part.Value);
                case NamePartKind.ProjectParameter: return !_projectFieldNames.Contains(part.Value);
                default: return false;
            }
        }

        #endregion

        #region Selection and naming

        private bool FilterSheet(object o)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            // Searches "number name" as one text, so a query can span both ("0105 level 4")
            SheetExportItem item = (SheetExportItem)o;
            return SearchMatcher.MatchesInOrder(item.SheetNumber + " " + item.SheetName, _searchText);
        }

        /// <summary>Ticks or unticks the sheets currently visible (so it respects the search box).</summary>
        private void SetVisibleSelection(bool selected)
        {
            bool changed = false;

            _suspendRefresh = true;
            foreach (SheetExportItem item in SheetsView.Cast<SheetExportItem>())
            {
                if (item.IsSelected != selected)
                {
                    item.IsSelected = selected;
                    changed = true;
                }
            }
            _suspendRefresh = false;

            RefreshNames();

            if (changed)
                ClearNoneChoice();
        }

        private void OnItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SheetExportItem.IsSelected) && !_suspendRefresh)
            {
                RefreshNames();
                ClearNoneChoice();
            }
        }

        /// <summary>
        /// Once the user ticks sheets by hand, "(none)" no longer describes the selection. Emptying the box
        /// (ready for a new set name) also means choosing "(none)" again later counts as a change and unticks everything.
        /// </summary>
        private void ClearNoneChoice()
        {
            if (!ReferenceEquals(_selectedSet, NoneSet))
                return;

            _suspendSetApply = true;
            SelectedSet = null;
            _suspendSetApply = false;
            SetName = "";
        }

        /// <summary>Recomputes every file name, flags duplicates, and updates the selection counter.</summary>
        private void RefreshNames()
        {
            if (_suspendRefresh)
                return;

            foreach (NamePart part in Parts)
                part.IsMissing = IsMissing(part);

            FileNameOptions options = new FileNameOptions
            {
                Prefix = _prefix,
                Suffix = _suffix,
                Separator = _separator,
                Parts = Parts.ToList()
            };
            string ext = _isPdf ? ".pdf" : ".dwg";

            foreach (SheetExportItem item in Sheets)
            {
                item.FileName = SheetExportService.BuildFileName(item, options);
                item.FileNameDisplay = item.FileName.Length == 0 ? "(empty name)" : item.FileName + ext;
            }

            HashSet<string> duplicates = new HashSet<string>(
                Sheets.Where(s => s.IsSelected)
                      .GroupBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
                      .Where(g => g.Count() > 1)
                      .Select(g => g.Key),
                StringComparer.OrdinalIgnoreCase);

            foreach (SheetExportItem item in Sheets)
                item.IsDuplicate = item.IsSelected && duplicates.Contains(item.FileName);

            int selected = Sheets.Count(s => s.IsSelected);
            SelectionText = $"{selected} of {Sheets.Count} sheets selected";

            if (duplicates.Count > 0)
                StatusText = "Some ticked sheets would get the same file name (shown in red). Change the name parts.";
            else if (!IsBusy)
                StatusText = selected == 0 ? "Tick the sheets to export." : "Ready.";

            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region Export

        private bool CanExport()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(OutputFolder))
                return false;

            List<SheetExportItem> selected = Sheets.Where(s => s.IsSelected).ToList();
            return selected.Count > 0 && selected.All(s => !s.IsDuplicate && s.FileName.Length > 0);
        }

        private void Export()
        {
            SaveSettings();

            List<SheetExportItem> selected = Sheets.Where(s => s.IsSelected).ToList();
            SheetExportRequest request = new SheetExportRequest
            {
                Format = IsPdf ? SheetExportFormat.Pdf : SheetExportFormat.Dwg,
                Folder = OutputFolder.Trim(),
                DwgSetup = SelectedDwgSetup == DefaultSetupLabel ? "" : SelectedDwgSetup,
                MergeViews = MergeViews,
                Overwrite = Overwrite
            };

            ProgressMax = selected.Count;
            ProgressValue = 0;
            IsBusy = true;

            SheetExportResult result;
            try
            {
                result = SheetExportService.Export(_doc, selected, request, (done, total, current) =>
                {
                    ProgressValue = done;
                    StatusText = done < total ? $"Exporting {done + 1} of {total}: {current}" : "Finishing...";
                    PumpMessages();
                });
            }
            finally
            {
                IsBusy = false;
            }

            StatusText = $"Done: {result.Exported} exported, {result.Skipped.Count} skipped, {result.Failed.Count} failed.";
            ShowProblems(result);
        }

        /// <summary>Lets WPF repaint the progress bar while Revit is busy exporting on the same thread.</summary>
        private void PumpMessages()
        {
            _dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        private static void ShowProblems(SheetExportResult result)
        {
            if (result.Skipped.Count == 0 && result.Failed.Count == 0)
                return;

            StringBuilder sb = new StringBuilder();

            if (result.Skipped.Count > 0)
            {
                sb.AppendLine("Skipped (file already exists, overwrite is off):");
                foreach (string s in result.Skipped.Take(15)) sb.AppendLine("  " + s);
                if (result.Skipped.Count > 15) sb.AppendLine($"  ... and {result.Skipped.Count - 15} more");
                sb.AppendLine();
            }

            if (result.Failed.Count > 0)
            {
                sb.AppendLine("Failed:");
                foreach (string s in result.Failed.Take(15)) sb.AppendLine("  " + s);
                if (result.Failed.Count > 15) sb.AppendLine($"  ... and {result.Failed.Count - 15} more");
            }

            TaskDialog.Show("Sheet export", sb.ToString());
        }

        #endregion

        #region Settings

        /// <summary>Saves a moment after the last change. Called whenever an option changes.</summary>
        private void ScheduleSave()
        {
            if (_suspendRefresh)
                return; // still loading

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>Saves right now. The view calls this when it is closed or switched away from.</summary>
        public void FlushSettings()
        {
            _saveTimer.Stop();
            SaveSettings();
        }

        private void LoadSettings()
        {
            SheetExportSettings s = SheetExportSettings.Load();

            _isPdf = s.Format == SheetExportFormat.Pdf;
            _outputFolder = s.OutputFolder ?? "";
            _prefix = s.Prefix ?? "";
            _suffix = s.Suffix ?? "";
            _separator = s.Separator ?? " - ";
            _mergeViews = s.MergeViews;
            _overwrite = s.Overwrite;
            _selectedDwgSetup = !string.IsNullOrEmpty(s.DwgSetup) && DwgSetups.Contains(s.DwgSetup)
                ? s.DwgSetup
                : DefaultSetupLabel;

            Parts.Clear();

            List<NamePartSetting> saved = s.Parts != null && s.Parts.Count > 0
                ? s.Parts
                : new List<NamePartSetting>
                {
                    new NamePartSetting(NamePartKind.SheetParameter, "Sheet Number"),
                    new NamePartSetting(NamePartKind.SheetParameter, "Sheet Name")
                };

            foreach (NamePartSetting p in saved)
                Parts.Add(new NamePart(p.Kind, p.Value));

            // Sets of other projects are kept untouched; only this project's sets are shown
            List<SheetSetSetting> allSets = s.Sets ?? new List<SheetSetSetting>();
            _otherProjectSets = allSets.Where(x => x.ProjectKey != _projectKey).ToList();

            foreach (SheetSetSetting set in allSets
                .Where(x => x.ProjectKey == _projectKey)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                SheetSets.Add(set);
            }
        }

        private void SaveSettings()
        {
            new SheetExportSettings
            {
                Format = IsPdf ? SheetExportFormat.Pdf : SheetExportFormat.Dwg,
                OutputFolder = OutputFolder,
                Prefix = Prefix,
                Suffix = Suffix,
                Separator = Separator,
                MergeViews = MergeViews,
                Overwrite = Overwrite,
                DwgSetup = SelectedDwgSetup == DefaultSetupLabel ? "" : SelectedDwgSetup,
                Parts = Parts.Select(p => new NamePartSetting(p.Kind, p.Value)).ToList(),
                Sets = _otherProjectSets.Concat(SheetSets.Where(s => !ReferenceEquals(s, NoneSet))).ToList()
            }.Save();
        }

        #endregion
    }
}
