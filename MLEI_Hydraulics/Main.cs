using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using System.Windows.Media.Imaging;


namespace MLEI_Hydraulics
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Create a ribbon panel
            string tabName = "MLEI_HYD";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Tab already exists, ignore the exception
            }

            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "MLEI");
            string asm = typeof(App).Assembly.Location;

            // 1. Hydraulics
            Uri iconUri = new Uri("pack://application:,,,/MLEI_Hydraulics;component/Resources/MLEI_Logo.png");
            BitmapImage iconImage = new BitmapImage(iconUri);

            PushButtonData hydraulicsData = new PushButtonData(
                "HydraulicsCommand",
                "Hydraulics",
                asm,
                "MLEI_Hydraulics.HydraulicsCommand");

            PushButton hydraulicsButton = ribbonPanel.AddItem(hydraulicsData) as PushButton;
            hydraulicsButton.ToolTip = "Run the Hydraulics command";
            hydraulicsButton.LargeImage = iconImage;

            // 2. Auto tag settings
            PushButtonData tagSettings = new PushButtonData(
                "AutoTagSettings",
                "Auto tag settings",
                asm,
                "MLEI_Hydraulics.AutoTagSettingsCommand")
            {
                ToolTip = "Choose the tag types used by the Auto tag buttons.",
                LargeImage = new BitmapImage(new Uri(
                    "pack://application:,,,/MLEI_Hydraulics;component/Resources/AutoTagSettings.png"))
            };

            ribbonPanel.AddItem(tagSettings);

            // 3. Four tag buttons in a 2x2 block that mirrors the directions:
            //    left column  = up-left (top), down-left (bottom)
            //    right column = up-right (top), down-right (bottom)
            PushButtonData tagUpLeft = new PushButtonData("AutoTagUpLeft", "Tag up-left", asm, "MLEI_Hydraulics.AutoTagUpLeft")
            { ToolTip = "Auto tag the selected pipes. Vertical pipe tags go up and to the left." };
            PushButtonData tagDownLeft = new PushButtonData("AutoTagDownLeft", "Tag down-left", asm, "MLEI_Hydraulics.AutoTagDownLeft")
            { ToolTip = "Auto tag the selected pipes. Vertical pipe tags go down and to the left." };
            PushButtonData tagUpRight = new PushButtonData("AutoTagUpRight", "Tag up-right", asm, "MLEI_Hydraulics.AutoTagUpRight")
            { ToolTip = "Auto tag the selected pipes. Vertical pipe tags go up and to the right." };
            PushButtonData tagDownRight = new PushButtonData("AutoTagDownRight", "Tag down-right", asm, "MLEI_Hydraulics.AutoTagDownRight")
            { ToolTip = "Auto tag the selected pipes. Vertical pipe tags go down and to the right." };

            ribbonPanel.AddStackedItems(tagUpLeft, tagDownLeft);
            ribbonPanel.AddStackedItems(tagUpRight, tagDownRight);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
