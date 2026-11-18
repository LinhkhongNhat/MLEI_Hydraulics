using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MLEI_Hydraulics.UI
{
    /// <summary>Shown in a tab when its tool failed to load, so one bad tool cannot crash the window.</summary>
    public class ErrorView : UserControl
    {
        public ErrorView(string toolTitle, Exception ex)
        {
            StackPanel panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 600
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"\"{toolTitle}\" could not be loaded",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Firebrick,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = ex.Message,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            Content = panel;
        }
    }
}
