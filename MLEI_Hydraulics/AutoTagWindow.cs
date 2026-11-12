using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MLEI_Hydraulics
{
    public class AutoTagWindow : Window
    {
        private ComboBox _horizontalBox;
        private ComboBox _slopedBox;
        private ComboBox _verticalBox;
        private Button _okButton;

        /// <summary>The choices made in the window. Only valid when ShowDialog() returns true.</summary>
        public AutoTagSettings ChosenSettings { get; private set; }

        public AutoTagWindow(IEnumerable<TagTypeRef> tagTypes, AutoTagSettings current)
        {
            Title = "Auto Tag Settings";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            List<TagTypeRef> types = tagTypes
                .OrderBy(t => t.FamilyName)
                .ThenBy(t => t.TypeName)
                .ToList();

            StackPanel root = new StackPanel { Margin = new Thickness(16) };

            _horizontalBox = AddTagRow(root, "Horizontal pipes", types, current.Horizontal);
            _slopedBox = AddTagRow(root, "Sloped pipes", types, current.Sloped);
            _verticalBox = AddTagRow(root, "Vertical pipes", types, current.Vertical);

            root.Children.Add(BuildButtons());

            Content = root;
            UpdateOkState();
        }

        private ComboBox AddTagRow(Panel parent, string label, List<TagTypeRef> types, TagTypeRef preferred)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            ComboBox box = new ComboBox
            {
                ItemsSource = types,   // shown as "Family : Type" through TagTypeRef.ToString()
                Margin = new Thickness(0, 0, 0, 12)
            };

            // Preselect the saved choice if the project has it, otherwise leave empty
            if (preferred != null)
            {
                box.SelectedItem = types.FirstOrDefault(
                    t => t.FamilyName == preferred.FamilyName && t.TypeName == preferred.TypeName);
            }

            box.SelectionChanged += (s, e) => UpdateOkState();
            parent.Children.Add(box);
            return box;
        }

        private StackPanel BuildButtons()
        {
            _okButton = new Button
            {
                Content = "OK",
                Width = 80,
                IsDefault = true,
                Margin = new Thickness(0, 4, 8, 0)
            };
            _okButton.Click += (s, e) =>
            {
                ChosenSettings = ReadSettings();
                DialogResult = true;
            };

            Button cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true, Margin = new Thickness(0, 4, 0, 0) };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { _okButton, cancel }
            };
        }

        private void UpdateOkState()
        {
            if (_okButton == null || _horizontalBox == null || _slopedBox == null || _verticalBox == null)
                return;

            _okButton.IsEnabled =
                _horizontalBox.SelectedItem != null &&
                _slopedBox.SelectedItem != null &&
                _verticalBox.SelectedItem != null;
        }

        private AutoTagSettings ReadSettings()
        {
            return new AutoTagSettings
            {
                Horizontal = (TagTypeRef)_horizontalBox.SelectedItem,
                Sloped = (TagTypeRef)_slopedBox.SelectedItem,
                Vertical = (TagTypeRef)_verticalBox.SelectedItem
            };
        }
    }
}