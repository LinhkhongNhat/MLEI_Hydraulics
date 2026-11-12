# MLEI Hydraulics for Revit

Revit add-in with hydraulics tools. The first tool is **Auto Tag for Pipes**, which tags selected pipes in the active view.

Supported: Revit 2025 and Revit 2026 (Windows, 64-bit).

## Install

1. Run `MLEI_Hydraulics_Setup_<version>.exe`. No administrator rights are needed.
   - If Windows shows "Windows protected your PC": right-click the file, choose **Properties**, tick **Unblock**, press OK, then run it again. This happens because the installer is not code-signed.
2. The installer detects Revit 2025 and 2026 and installs for each one it finds.
3. Start Revit. The first time, Revit asks about an unsigned add-in: choose **Always Load**.
4. The **MLEI_HYD** tab appears in the ribbon.

To uninstall, use **Settings > Apps > Installed apps > MLEI Hydraulics**.

## Auto Tag for Pipes

1. Select the pipes in a plan view, then press one of the four buttons:
   **Tag up-left**, **Tag up-right**, **Tag down-left**, **Tag down-right**.
2. Horizontal and sloped pipes get their tag next to the pipe. Vertical pipes get a tag placed in the chosen direction.
3. Vertical pipes within 500 mm of each other get their tags in a row, edge to edge. Only the tag closest to the pipes keeps a leader.
4. Pipes shorter than 2000 mm and pipes that are already tagged in the view are skipped.

**Auto tag settings** chooses which tag family and type is used for horizontal, sloped and vertical pipes. The choice is remembered per Windows user. The tag types must be loaded in the project.

## Build from source

Requirements: Visual Studio with the .NET 8 SDK, and [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer.

```powershell
cd MLEI_Hydraulics
dotnet build -c Release -p:RevitVersion=2025 -o bin\Release\R2025
dotnet build -c Release -p:RevitVersion=2026 -o bin\Release\R2026
```

Then open `installer\MLEI_Hydraulics.iss` in Inno Setup and compile. The installer is written to `installer\Output\`.

The Revit API comes from NuGet, so Revit does not need to be installed to build. For development, point a `.addin` manifest at `MLEI_Hydraulics\bin\Debug\MLEI_Hydraulics.dll`.

## Layout

- `MLEI_Hydraulics/` - the add-in source
- `installer/` - Inno Setup script
