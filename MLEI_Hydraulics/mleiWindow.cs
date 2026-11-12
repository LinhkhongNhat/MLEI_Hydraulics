using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using Autodesk.Revit.DB;
using TextElement = System.Windows.Documents.TextElement;
using Panel = System.Windows.Controls.Panel;
using Grid = System.Windows.Controls.Grid;
using Color = System.Windows.Media.Color;

namespace MLEI_Hydraulics
{
    public class mleiWindow : Window
    {
        public string UserInput { get; private set; }

        private readonly Document _doc;

        public mleiWindow(Document doc)
        {
            _doc = doc;
            #region main layout
            this.Title = "MLEI Hydraulics Tool Center";
            this.Width = 1300;
            this.Height = 700;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;

            // Remove default chrome
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent; // important!

            // Wrap everything in a Border with rounded corners
            Border border = new Border
            {
                CornerRadius = new CornerRadius(10),// curve size
                Background = new SolidColorBrush(Color.FromRgb(243, 245, 249)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
            };

            // Main layout (inside border)
            Grid layout = new Grid();

            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 0: title bar
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 1: banner
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // tabs + content

            #endregion

            #region Titlebar with rounded corners 
            Border titleBarBorder = new Border
            {
                CornerRadius = new CornerRadius(10, 10, 0, 0), // round top-left & top-right only
                Background = new SolidColorBrush(Color.FromRgb(43, 46, 50)),
                Height = 30,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Custom title bar
            DockPanel titleBar = new DockPanel
            {
                Height = 30,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Top,
            };

            titleBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
  
            Image logoTitleBar = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/MLEI_Hydraulics;component/Resources/MLEI_Logo_3.png")),
                Stretch = Stretch.Uniform,
                Height = 35,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Thin vertical slash (divider)
            Border divider = new Border
            {
                Width = 1,
                Height = 16, // match text height
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Margin = new Thickness(15, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock titleText = new TextBlock
            {
                Text = "MLEI Hydraulics Tool Center",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold
            };


            #endregion

            #region Close button with hover effect
            Button closeButton = new Button
            {
                Content = "\uE10A",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Width = 40,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15,
                FontStretch = FontStretches.UltraCondensed,
            };
            closeButton.Click += (s, e) => this.Close();

            #region Hover Effect for Close Button
            // Custom style with hover effect
            Style closeStyle = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));

            FrameworkElementFactory borderCloseButton = new FrameworkElementFactory(typeof(Border));
            borderCloseButton.Name = "border"; // give it a name
            borderCloseButton.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 10, 0, 0));
            borderCloseButton.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderCloseButton.AppendChild(contentPresenter);

            template.VisualTree = borderCloseButton;

            // Hover trigger must be a TemplateTrigger
            Trigger hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.Red, "border"));
            template.Triggers.Add(hoverTrigger); // <-- add to template, not style

            closeStyle.Setters.Add(new Setter(Button.TemplateProperty, template));
            closeButton.Style = closeStyle;

            #endregion

            #endregion


            #region Banner for window
            // Add logo image
            Image logo = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/MLEI_Hydraulics;component/Resources/MLEI_Banner.png")),
                Height = 267,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Stretch = Stretch.Fill
            };
            #endregion

            #region Tabs for window
            TabControl tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold
            };

            #region Full-width white header strip
            ControlTemplate tabControlTemplate = new ControlTemplate(typeof(TabControl));

            FrameworkElementFactory rootDock = new FrameworkElementFactory(typeof(DockPanel));
            rootDock.SetValue(DockPanel.LastChildFillProperty, true);

            // White bar behind the tab headers, spanning full width
            FrameworkElementFactory headerBorder = new FrameworkElementFactory(typeof(Border));
            headerBorder.SetValue(DockPanel.DockProperty, Dock.Top);
            headerBorder.SetValue(Border.BackgroundProperty, Brushes.White);

            FrameworkElementFactory tabPanel = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.TabPanel));
            tabPanel.SetValue(Panel.IsItemsHostProperty, true);
            tabPanel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            headerBorder.AppendChild(tabPanel);
            rootDock.AppendChild(headerBorder);

            // Selected tab's content fills the rest
            FrameworkElementFactory mainContentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            mainContentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
            mainContentPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(10));
            rootDock.AppendChild(mainContentPresenter);

            tabControlTemplate.VisualTree = rootDock;
            tabControl.Template = tabControlTemplate;
            #endregion

            #region Flat TabItem style (no native raised border)
            Style tabItemStyle = new Style(typeof(TabItem));
            ControlTemplate tabTemplate = new ControlTemplate(typeof(TabItem));

            FrameworkElementFactory tabBorder = new FrameworkElementFactory(typeof(Border));
            tabBorder.Name = "TabBorder";
            tabBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            tabBorder.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            tabBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 3));
            tabBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));
            tabBorder.SetValue(Border.MarginProperty, new Thickness(0, 0, 2, 0));
            tabBorder.SetValue(Border.PaddingProperty, new Thickness(20, 10, 20, 10));
            tabBorder.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(80, 80, 80)));


            FrameworkElementFactory tabContent = new FrameworkElementFactory(typeof(ContentPresenter));
            tabContent.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            tabContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            tabContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            tabBorder.AppendChild(tabContent);

            tabTemplate.VisualTree = tabBorder;

            // Selected tab: white background + orange text
            Trigger selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White, "TabBorder"));
            selectedTrigger.Setters.Add(new Setter(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(237, 125, 49)), "TabBorder"));
            selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(237, 125, 49)), "TabBorder"));
            tabTemplate.Triggers.Add(selectedTrigger);

            // Hover feedback when not selected
            Trigger tabHoverTrigger = new Trigger { Property = TabItem.IsMouseOverProperty, Value = true };
            tabHoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(238, 240, 244)), "TabBorder"));
            tabTemplate.Triggers.Add(tabHoverTrigger);

            tabItemStyle.Setters.Add(new Setter(TabItem.TemplateProperty, tabTemplate));
            tabControl.Resources.Add(typeof(TabItem), tabItemStyle);
            #endregion

            // Tab 1: Sheet export
            TabItem sheetExportTab = new TabItem
            {
                Header = "Sheets Export"
            };
            // Example content: a table (DataGrid) of sheets
            DataGrid sheetGrid = new DataGrid
            {
                AutoGenerateColumns = true,
                Margin = new Thickness(5),
                Background = Brushes.White,
                Foreground = Brushes.Black
            };
            // TODO: bind this DataGrid to your Revit sheet list
            sheetGrid.ItemsSource = SheetDataService.GetProjectSheets(_doc); // add this line
            sheetExportTab.Content = sheetGrid;

            // Tab 2: Schedule export
            TabItem scheduleExportTab = new TabItem
            {
                Header = "Schedule export",
                Content = new TextBlock
                {
                    Text = "Schedule export content goes here",
                    Margin = new Thickness(10)
                }
            };

            // Tab 3: Sheet generate
            TabItem sheetGenerateTab = new TabItem
            {
                Header = "Sheet generate",
                Content = new TextBlock
                {
                    Text = "Sheet generate content goes here",
                    Margin = new Thickness(10)
                }
            };

            // Add tabs to TabControl
            tabControl.Items.Add(sheetExportTab);
            tabControl.Items.Add(scheduleExportTab);
            tabControl.Items.Add(sheetGenerateTab);

            // Place TabControl in layout
            Grid.SetRow(tabControl, 2);
            #endregion

            #region Add things to layout
            // Add children to title bar
            titleBar.Children.Add(logoTitleBar);
            titleBar.Children.Add(divider);
            titleBar.Children.Add(titleText);
            titleBar.Children.Add(closeButton);

            // Put DockPanel inside the beveled title bar border
            titleBarBorder.Child = titleBar;

            // Add title bar border to layout
            Grid.SetRow(titleBarBorder, 0);
            layout.Children.Add(titleBarBorder);

            // Put layout inside outer border
            border.Child = layout;

            // Finally set border as window content
            this.Content = border;


            // Add logo to the layout
            Grid.SetRow(logo, 1);
            layout.Children.Add(logo);

            layout.Children.Add(tabControl);
            #endregion
        }

    }
}
