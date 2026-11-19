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
        private TextBox _minimumLengthBox;
        private TextBox _tagOffsetBox;
        private Button _okButton;

        /// <summary>The choices made in the window. Only valid when ShowDialog() returns true.</summary>
        public AutoTagSettings ChosenSettings { get; private set; }

        public AutoTagWindow(IEnumerable<TagTypeRef> tagTypes, AutoTagSettings current)
        {
            Title = "Auto Tag Settings";
            Width = 450;
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

            // Add minimum pipe length setting
            AddSettingRow(root, "Min. Length (minimum pipe length for auto tag) - mm", out _minimumLengthBox, current.MinimumPipeLength.ToString("F0"));

            // Add tag offset distance setting
            AddSettingRow(root, "Tag Offset Distance (distance from pipe) - mm", out _tagOffsetBox, current.TagOffsetDistance.ToString("F1"));

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

            bool valid = _horizontalBox.SelectedItem != null &&
                         _slopedBox.SelectedItem != null &&
                         _verticalBox.SelectedItem != null &&
                         ValidateNumericInput(_minimumLengthBox.Text) &&
                         ValidateNumericInput(_tagOffsetBox.Text);

            _okButton.IsEnabled = valid;
        }

        private bool ValidateNumericInput(string text)
        {
            return double.TryParse(text, out double value) && value > 0;
        }

        private void AddSettingRow(Panel parent, string label, out TextBox textBox, string initialValue)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Normal,
                Margin = new Thickness(0, 8, 0, 4)
            });

            textBox = new TextBox
            {
                Text = initialValue,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(4)
            };
            textBox.TextChanged += (s, e) => UpdateOkState();
            parent.Children.Add(textBox);
        }

        private AutoTagSettings ReadSettings()
        {
            double minLength = double.TryParse(_minimumLengthBox.Text, out double ml) && ml > 0 ? ml : 2000.0;
            double tagOffset = double.TryParse(_tagOffsetBox.Text, out double to) && to > 0 ? to : 20.0;

            return new AutoTagSettings
            {
                Horizontal = (TagTypeRef)_horizontalBox.SelectedItem,
                Sloped = (TagTypeRef)_slopedBox.SelectedItem,
                Vertical = (TagTypeRef)_verticalBox.SelectedItem,
                MinimumPipeLength = minLength,
                TagOffsetDistance = tagOffset
            };
        }
    }
}