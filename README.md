# MLEI Hydraulics for Revit

Revit add-in with hydraulics and documentation tools:

- **Tool Center** (the **Hydraulics** button): sheet create, sheet export, and schedule export / import.
- **Auto Tag for Pipes**: tags the selected pipes in the active view.

Supported: Revit 2025 and Revit 2026 (Windows, 64-bit).

## Install

1. Close Revit, then run `MLEI_Hydraulics_Setup_<version>.exe`. No administrator rights are needed.
   - If Windows shows "Windows protected your PC": right-click the file, choose **Properties**, tick **Unblock**, press OK, then run it again. This happens because the installer is not code-signed.
2. The installer detects Revit 2025 and 2026 and installs for each one it finds. Running a newer installer updates an existing install.
3. Start Revit. The first time, Revit asks about an unsigned add-in: choose **Always Load**.
4. The **MLEI_HYD** tab appears in the ribbon.

To uninstall, use **Settings > Apps > Installed apps > MLEI Hydraulics**.

## Tool Center

Press **Hydraulics** in the MLEI_HYD tab. The window can be resized and has one tab per tool. Your last choices in each tool are remembered per Windows user.

Spreadsheets are exchanged as **CSV**. In Excel, use **Save As > CSV UTF-8** so accents are kept.

### Sheet create

1. **Browse** for a CSV with the sheet number in the first column and the sheet name in the second. A header row such as `Sheet number, Sheet name` is detected automatically. **Save template** writes an example file.
2. Pick the **title block**.
3. Check the preview. Lines with a missing number or name, characters Revit does not allow, a repeated number, or a number already in the project are shown in red and skipped.
4. Press **Create sheets**. All sheets are created in one undoable step.

### Sheet export

1. Tick the sheets (the search box filters the list).
2. Choose **PDF** or **DWG**. For DWG, pick one of the project's export setups.
3. Choose the output folder and build the file name. The name is an ordered list of **parts**, joined with the separator you choose. A part can be a **sheet parameter** (sheet number, sheet name, drawn by, revision...), a **title block parameter**, a **project information parameter** (project number, client name...), or **fixed text**. Use Up / Down to change the order. Parts that are empty for a sheet are left out. A **prefix** and **suffix** can be added around the whole name.
4. The list previews the final file names. Sheets that would get the same name are shown in red and block the export.
5. Press **Export**. Existing files are skipped unless **Overwrite** is ticked.

### Schedule export / import

1. Select a schedule in the list.
2. **Export to CSV**: tick the columns to export. Each column is marked editable or read-only. The file has an **Element ID** column first, and read-only columns get `(read-only)` added to their header.
3. Edit the editable columns in Excel and save as CSV. Do not delete the **Element ID** column. Rows may be sorted or filtered freely.
4. **Import from CSV**: choose the file, with the same schedule selected. The tool lists only the values that differ. Untick any change you do not want, then press **Apply changes**.

Notes:
- Type parameters are read-only unless **Allow editing type parameters** is ticked. A type parameter change affects every element of that type.
- Excel may drop leading zeros or reformat values. Such cells show up as changes in the preview, so they can be unticked.

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

Releases are built by GitHub Actions: publishing a GitHub release builds both versions and attaches the installer. The workflow can also be started by hand to check the build without releasing.

## Layout

- `MLEI_Hydraulics/` - the add-in source
  - `Commands/` - the Revit commands behind the ribbon buttons
  - `Core/` - shared helpers (CSV reader/writer, settings storage, the tool list)
  - `UI/` - the Tool Center window and shared styles
  - `Tools/` - one folder per Tool Center tab (`SheetCreate`, `SheetExport`, `ScheduleSync`)
  - `AutoTag*.cs` - the Auto Tag tool
- `installer/` - Inno Setup script

### Adding a tool to the Tool Center

Create a folder under `Tools/` with a view (a `UserControl`), a view model, and a service for the Revit work, then add one line to `Core/ToolRegistry.cs`.
