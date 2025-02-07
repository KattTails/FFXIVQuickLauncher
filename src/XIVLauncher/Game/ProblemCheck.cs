﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using CheapLoc;
using Microsoft.Win32;
using Serilog;
using XIVLauncher.Windows;

namespace XIVLauncher.Game
{
    static class ProblemCheck
    {
        public static void RunCheck()
        {
            var runningAsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            var compatFlagKey = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true);

            if (compatFlagKey != null && !EnvironmentSettings.IsWine)
            {
                var compatEntries = compatFlagKey.GetValueNames();

                var entriesToFix = new Stack<string>();
                foreach (var compatEntry in compatEntries)
                {
                    if ((compatEntry.Contains("ffxiv") || compatEntry.Contains("XIVLauncher")) && ((string) compatFlagKey.GetValue(compatEntry, string.Empty)).Contains("RUNASADMIN"))
                        entriesToFix.Push(compatEntry);
                }

                if (entriesToFix.Count > 0)
                {
                    var result = MessageBox.Show(Loc.Localize("AdminCheck", "XIVLauncher and/or FINAL FANTASY XIV are set to run as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nDo you want to fix this issue automatically?"), "XIVLauncher", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);

                    if (result != MessageBoxResult.OK) 
                        return;

                    while (entriesToFix.Count > 0)
                    {
                        compatFlagKey.DeleteValue(entriesToFix.Pop());
                    }

                    return;
                }
            }

            if (runningAsAdmin && !App.Settings.HasComplainedAboutAdmin.GetValueOrDefault(false) && !EnvironmentSettings.IsWine)
            {
                CustomMessageBox.Show(Loc.Localize("AdminCheckNag", "XIVLauncher is running as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nPlease take care to avoid running XIVLauncher as admin."), "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                App.Settings.HasComplainedAboutAdmin = true;
            }

            var procModules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>();
            if (procModules.Any(x => x.ModuleName == "MacType.dll" || x.ModuleName == "MacType64.dll"))
            {
                CustomMessageBox.Show(Loc.Localize("MacTypeNag", "MacType was detected on this PC.\nIt will cause problems with FFXIV; both the official launcher and XIVLauncher.\n\nPlease exclude XIVLauncher, ffxivboot, ffxivlauncher, ffxivupdater and ffxiv_dx11 from MacType."), "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            }


            if (App.Settings.GamePath == null)
                return;

            var d3d11 = new FileInfo(Path.Combine(App.Settings.GamePath.FullName, "game", "d3d11.dll"));
            var dxgi = new FileInfo(Path.Combine(App.Settings.GamePath.FullName, "game", "dxgi.dll"));

            if (!CheckSymlinkValid(d3d11) || !CheckSymlinkValid(dxgi))
            {
                if (MessageBox.Show(
                    Loc.Localize("GShadeError", "A broken GShade installation was detected.\n\nThe game cannot start. Do you want XIVLauncher to fix this? You will need to reinstall GShade."),
                    "XIVLauncher Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    try
                    {
                        d3d11.Delete();
                        dxgi.Delete();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not delete broken GShade.");
                    }
                }
            }

            if (d3d11.Exists && dxgi.Exists)
            {
                var dxgiInfo = FileVersionInfo.GetVersionInfo(dxgi.FullName);
                var d3d11Info = FileVersionInfo.GetVersionInfo(d3d11.FullName);
                if (dxgiInfo.ProductName.Equals("GShade", StringComparison.OrdinalIgnoreCase) &&
                    d3d11Info.ProductName.Equals("GShade", StringComparison.OrdinalIgnoreCase))
                {
                    if (MessageBox.Show(
                        Loc.Localize("GShadeError", "A broken GShade installation was detected.\n\nThe game cannot start. Do you want XIVLauncher to fix this? You will need to reinstall GShade."),
                        "XIVLauncher Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            dxgi.Delete();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Could not delete duplicate GShade.");
                        }
                    }
                }
            }
        }

        private static bool CheckSymlinkValid(FileInfo file)
        {
            if (!file.Exists)
                return true;

            try
            {
                file.OpenRead();
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }
    }
}
