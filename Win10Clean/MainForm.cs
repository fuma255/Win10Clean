﻿using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Management.Automation;
using Microsoft.Win32;
using System.Net.NetworkInformation;

/*
 * Win10Clean - Cleanup your Windows 10 environment
 * Copyright (C) 2017 Hawaii_Beach & deadmoon
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the license, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
*/

namespace Win10Clean
{
    public partial class MainForm : Form
    {
        public Version offlineVer = new Version(Application.ProductVersion);
        public Version onlineVer;
        string serverUrl = "https://ElPumpo.github.io/Win10Clean/version2.txt";
        string releasesUrl = "https://github.com/ElPumpo/Win10Clean/releases";

        bool amd64 = Environment.Is64BitOperatingSystem;
        int _goBack;
        string selectedApps;

        bool _defenderSwitch = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            lblVersion.Text += offlineVer;
            CheckTweaks();
        }

        /* Buttons / Main stuff */

        private void OneDriveBtn_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {

                string processName = "OneDrive";

                byte[] byteArray = BitConverter.GetBytes(0xb090010d);
                int oneDriveSwitch = BitConverter.ToInt32(byteArray, 0);
                string onePath;

                try {
                    Process.GetProcessesByName(processName)[0].Kill();
                } catch (Exception) {
                    Log("Could not kill process: " + processName);
                    // ignore errors
                }

                if (amd64) {
                    onePath = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86) + "\\OneDriveSetup.exe";
                } else {
                    onePath = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\OneDriveSetup.exe";
                }

                Process.Start(onePath, "/uninstall");
                Log("Uninstalled OneDrive using the setup!");

                // All the folders to be deleted
                string[] onePaths = {
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\OneDrive",
                    Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) + "OneDriveTemp",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\OneDrive",
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\Microsoft OneDrive"
                };

                foreach (string dir in onePaths) {
                    if (Directory.Exists(dir)) {
                        try {
                            Directory.Delete(dir, true);
                            Log("Folder deleted: " + dir);
                        } catch (Exception) {
                            Log("Could not delete folder: " + dir);
                            // ignore errors
                        }
                    }
                }

                // Remove OneDrive from Explorer
                string oneKey = @"CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}";
                Registry.ClassesRoot.CreateSubKey(oneKey);

                var baseReg = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64);

                try {
                    // Remove from the Explorer file dialog
                    using (var key = Registry.ClassesRoot.OpenSubKey(oneKey, true)) {
                        key.SetValue("System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord);
                        Log("OneDrive removed from Explorer (FileDialog)!");
                    }

                    // amd64 system fix
                    if (amd64) {
                        using (var key = baseReg.OpenSubKey(oneKey, true)) {
                            key.SetValue("System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord);
                            Log("OneDrive removed from Explorer (FileDialog, amd64)!");
                        }
                    }

                    // Remove from the alternative file dialog (legacy)
                    using (var key = Registry.ClassesRoot.OpenSubKey(oneKey + "\\ShellFolder", true)) {
                        key.SetValue("Attributes", oneDriveSwitch, RegistryValueKind.DWord);
                        Log("OneDrive removed from Explorer (Legacy FileDialog)!");
                    }

                    // amd64 system fix
                    if (amd64) {
                        using (var key = baseReg.OpenSubKey(oneKey + "\\ShellFolder", true)) {
                            key.SetValue("Attributes", oneDriveSwitch, RegistryValueKind.DWord);
                            Log("OneDrive removed from Explorer (Legacy FileDialog, amd64)!");
                        }
                    }
                } catch (Exception ex) {
                    Log(ex.ToString());
                    MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                baseReg.Dispose();

                // Delete scheduled leftovers
                RunCommand("SCHTASKS /Delete /TN \"OneDrive Standalone Update Task\" /F");
                RunCommand("SCHTASKS /Delete /TN \"OneDrive Standalone Update Task v2\" /F");
                Log("OneDrive scheduled tasks deleted!");

                MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            btnUpdate.Enabled = false;
            try {
                Log("Checking for updates...");

                WebRequest req = WebRequest.Create(serverUrl);
                req.Timeout = 10000;
                req.Headers.Set("Cache-Control", "no-cache, no-store, must-revalidate");

                WebResponse res = req.GetResponse();
                StreamReader reader = new StreamReader(res.GetResponseStream());

                onlineVer = new Version(reader.ReadToEnd().Trim());

                // Dispone when done
                reader.Dispose();
                res.Dispose();
            } catch (Exception ex) {
                Log(ex.ToString());
                MessageBox.Show("Could not check for updates!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            var compare = onlineVer.CompareTo(offlineVer);
            if (compare == 0) {
                Log("Client up-to-date");
                MessageBox.Show("No new updates were found", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else if (compare < 0) {
                Log("Client > Remote!");
                MessageBox.Show("If we're correct you are running a newer version than remote! This may very well be a sign that there is a new version out there, so please do your homework.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } else {
                Log("Update available!");
                if (MessageBox.Show("There is a new update available for download, do you want to visit the GitHub releases website?", "Information", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                    Process.Start(releasesUrl);
                }
            }
            btnUpdate.Enabled = true;
        }

        private void btnDefender_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                var baseReg = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                if (!_defenderSwitch) {
                    try {
                        // Disable engine
                        using (var key = baseReg.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender", true)) {
                            key.SetValue("DisableAntiSpyware", 1, RegistryValueKind.DWord);
                            Log("Disabled main Defender functions!");
                        }

                        // Delete Defender from startup / tray icons
                        using (var key = baseReg.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)) {
                            key.DeleteValue("WindowsDefender", false);
                            key.DeleteValue("SecurityHealth", false);
                            Log("Windows Defender removed from startup!");
                        }

                        // Unregister Defender shell extension
                        RunCommand("regsvr32 /u /s \"C:\\Program Files\\Windows Defender\\shellext.dll\"");
                        Log("Windows Defender shell addons unregistered!");

                        MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) {
                        Log(ex.Message);
                        MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                } else { // re-enable Defender
                    try {
                        using (var key = baseReg.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender", true)) {
                            key.SetValue("DisableAntiSpyware", 0, RegistryValueKind.DWord);
                        }

                        Log("Main Windows Defender functions enabled!");
                        MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) {
                        Log(ex.Message);
                        MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                baseReg.Dispose();
            }
        }

        private void HomeGroupBtn_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                RunCommand("sc config \"HomeGroupProvider\" start=disabled"); // stop autorun
                RunCommand("sc stop \"HomeGroupProvider\""); // stop process now
                Log("HomeGroup disabled!");

                MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnApps_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", true))
                    {
                        key.SetValue("SilentInstalledAppsEnabled", 0, RegistryValueKind.DWord);
                    }

                    Log("Silent Modern App install disabled");
                    MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                    MessageBox.Show(ex.ToString(), ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnStartAds_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", true))
                    {
                        key.SetValue("SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord);
                    }

                    Log("Start menu ads disabled!");
                    MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                    MessageBox.Show(ex.ToString(), ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Revert7Btn_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                
                // Get ride of libary folders in My PC
                string libKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions\";
                string[] guidArray = {
                    "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", // Desktop
                    "{7d83ee9b-2244-4e70-b1f5-5393042af1e4}", // Downloads
                    "{f42ee2d3-909f-4907-8871-4c22fc0bf756}", // Documents
                    "{0ddd015d-b06c-45d5-8c4c-f59713854639}", // Pictures
                    "{a0c69a99-21c8-4671-8703-7934162fcf1d}", // Music
                    "{35286a68-3c57-41a1-bbb1-0eae73d76c95}", // Videos
                    "{31C0DD25-9439-4F12-BF41-7FF4EDA38722}"  // 3D builder
                };
                string finalKey;
                var baseReg = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64); // we don't want Wow6432Node

                foreach (string guid in guidArray) {
                    try {
                        finalKey = libKey + guid + @"\PropertyBag";
                        using (var key = baseReg.CreateSubKey(finalKey, true)) {
                            key.SetValue("ThisPCPolicy", "Hide");
                            Log(string.Format("Value of {0} modified", guid));
                        }
                    } catch (Exception ex) {
                        Log(ex.GetType().Name + " - Couldn't modify the value of: " + guid);
                        MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                
                string pinLib = @"Software\Classes\CLSID\{031E4825-7B94-4dc3-B131-E946B44C8DD5}";
                byte[] bytes = { 2, 0, 0, 0, 64, 31, 210, 5, 170, 22, 211, 1, 0, 0, 0, 0, 67, 66, 1, 0, 194, 10, 1, 203, 50, 10, 2, 5, 134, 145, 204, 147, 5, 36, 170, 163, 1, 68, 195, 132, 1, 102, 159, 247, 157, 177, 135, 203, 209, 172, 212, 1, 0, 5, 188, 201, 168, 164, 1, 36, 140, 172, 3, 68, 137, 133, 1, 102, 160, 129, 186, 203, 189, 215, 168, 164, 130, 1, 0, 194, 60, 1, 0 };
                Registry.CurrentUser.CreateSubKey(pinLib); // doesn't exist as default, normal behaviour

                try {
                    // Pin libary folders
                    using (var key = Registry.CurrentUser.OpenSubKey(pinLib, true)) {
                        key.SetValue("System.IsPinnedToNameSpaceTree", 1, RegistryValueKind.DWord);
                        Log("Pinned the libary folders in Explorer!");
                    }

                    // Stop quick access from filling up with folders and files
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer", true)) {
                        key.SetValue("ShowFrequent", 0, RegistryValueKind.DWord); // folders
                        key.SetValue("ShowRecent", 0, RegistryValueKind.DWord);   // files
                        Log("Disabled quick access filling up!");
                    }

                    // Make explorer open 'My PC' by default
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true)){
                        key.SetValue("LaunchTo", 1, RegistryValueKind.DWord);
                        Log("Open explorer to: This PC!");
                    }

                    // Add explorer on start menu
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$$windows.data.unifiedtile.startglobalproperties\Current", true)) {
                        key.SetValue("Data", bytes, RegistryValueKind.Binary);
                        Log("File Explorer from Start Menu enabled!");
                    }
                    
                    // Hide OneDrive popup in Explorer
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true)) {
                        key.SetValue("ShowSyncProviderNotifications", 0, RegistryValueKind.DWord);
                        Log("Hide OneDrive popup in Explorer!");
                    }

                    // Hide My People in taskbar
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People", true)) {
                        key.SetValue("PeopleBand", 0, RegistryValueKind.DWord);
                        Log("Hide My People from taskbar");
                    }
                } catch (Exception ex) {
                    Log(ex.Message);
                    MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                RestartExplorer();
                MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(consoleBox.Text))
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.FileName = "Win10Clean - v" + offlineVer + " - " + DateTime.Now.ToString("yyyy/MM/dd HH-mm-ss");
                dialog.Filter = "Text files | *.txt";
                dialog.DefaultExt = "txt";
                dialog.Title = "Exporting log...";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(dialog.FileName, consoleBox.Text);
                }
            }
            else
            {
                MessageBox.Show("There is nothing to export!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnContext_Click(object sender, EventArgs e)
        {
            // Extended = only when SHIFT is pressed
            // LegacyDisable = item disabled

            if (MessageBox.Show("Are you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var baseReg = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64);
                // Provided by http://fragme.blogspot.se/2007/07/windows-tip-18-remove-unnecessary-right.html
                string[] extensions = {
                    "batfile",
                    "cmdfile",
                    "docfile",
                    "fonfile",
                    "htmlfile",
                    "inffile",
                    "inifile",
                    "JSEFile",
                    "JSFile",
                    "MSInfo.Document",
                    "otffile",
                    "pfmfile",
                    "regfile",
                    "rtffile",
                    "ttcfile",
                    "ttffile",
                    "txtfile",
                    "VBEFile",
                    "VBSFile",
                    "Wordpad.Document.1",
                    "WSFFile"
                };

                // Disable print
                foreach (string ext in extensions)
                {
                    try
                    {
                        string finalKey = ext + @"\shell\print";
                        using (var key = baseReg.OpenSubKey(finalKey, true))
                        {
                            key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                            Log("Print disabled for: " + ext);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex.GetType().ToString() + " - couldn't disable print for: " + ext);
                        // Ignore errors
                    }
                }

                // Disable edit
                foreach (string ext in extensions)
                {
                    try
                    {
                        string finalKey = ext + @"\shell\edit";
                        using (var key = baseReg.OpenSubKey(finalKey, true))
                        {
                            key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                            Log("Edit disabled for: " + ext);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex.GetType().ToString() + " - couldn't disable edit for: " + ext);
                        // Ignore errors
                    }
                }

                // Extra things
                try {
                    // Manual fix for txt
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\text\shell\edit", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Edit disabled for: TXT files");
                    }

                    // WMP #1 - add to list
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\audio\shell\Enqueue", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Disabled add to play list for: audio files!");
                    }

                    // WMP #2 - play
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\audio\shell\Play", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Disabled play song for: audio files!");
                    }

                    // WMP #3 - add to list (audio folder)
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\Directory.Audio\shell\Enqueue", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Disabled add to play list for: audio directories!");
                    }

                    // WMP #4 - play (audio folder)
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\Directory.Audio\shell\Play", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Disabled play song for: audio directories!");
                    }

                    // WMP #5 - add to list (image folder?!)
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\Directory.Image\shell\Enqueue", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Disabled add to play list for: image directories!");
                    }

                    // WMP #6 - play (image folder?!)
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\Directory.Image\shell\Play", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
                        Log("Disabled play song for: image directories!");
                    }

                    // Include in library context
                    using (var key = baseReg.OpenSubKey(@"Folder\shellex\ContextMenuHandlers\Library Location", true))
                    {
                        key.SetValue(string.Empty, "-{3dad6c5d-2167-4cae-9914-f99e41c12cfa}");
                        Log("Disabled include in library menu!");
                    }

                    // Buy music?
                    using (var key = baseReg.OpenSubKey(@"SystemFileAssociations\Directory.Audio\shellex\ContextMenuHandlers\WMPShopMusic", true))
                    {
                        key.SetValue(string.Empty, "-{8A734961-C4AA-4741-AC1E-791ACEBF5B39}");
                        Log("Disabled buying music online context menu!");
                    }

                    // Troubleshoot compability EXE
                    using (var key = baseReg.OpenSubKey(@"exefile\shellex\ContextMenuHandlers\Compatibility", true))
                    {
                        key.SetValue(string.Empty, "-{1d27f844-3a1f-4410-85ac-14651078412d}");
                        Log("Disabled troubleshooting compability (EXE)!");
                    }

                    // Troubleshoot compability MSI
                    using (var key = baseReg.OpenSubKey(@"Msi.Package\shellex\ContextMenuHandlers\Compatibility", true))
                    {
                        key.SetValue(string.Empty, "-{1d27f844-3a1f-4410-85ac-14651078412d}");
                        Log("Disabled troubleshooting compability (MSI)!");
                    }

                    // Disable printing .url files
                    RegistryUtilities.TakeOwnership(@"InternetShortcut\shell\print", RegistryHive.ClassesRoot);
                    using (var key = baseReg.OpenSubKey(@"InternetShortcut\shell\print", true))
                    {
                        key.SetValue("LegacyDisable", string.Empty);
                        Log("Disabled print for: InternetShortcut!");
                    }

                    // Restore previous version (file)
                    baseReg.DeleteSubKey(@"AllFilesystemObjects\shellex\ContextMenuHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}", false);
                    Log("Removed restoring previous version menu! (files)");

                    // Restore previous version (directory)
                    baseReg.DeleteSubKey(@"Directory\shellex\ContextMenuHandlers\{596AB062-B4D2-4215-9F74-E9109B0A8153}", false);
                    Log("Removed restoring previous version menu! (directories)");

                    // https://superuser.com/a/808730
                    // Pin to Start on recycle bin
                    RegistryUtilities.TakeOwnership(@"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}", RegistryHive.ClassesRoot);
                    RegistryUtilities.TakeOwnership(@"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell", RegistryHive.ClassesRoot);
                    RegistryUtilities.TakeOwnership(@"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell\empty", RegistryHive.ClassesRoot);
                    RegistryUtilities.TakeOwnership(@"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell\empty\command", RegistryHive.ClassesRoot);
                    var regHelper = new RegistryUtilities();
                    regHelper.RenameSubKey(baseReg, @"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell\empty", @"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\shell\pintostartscreen");
                    Log("Disabled Pin to Start for: Recycle Bin!");

                    // Disable modern share
                    using (var key = baseReg.OpenSubKey(@"*\shellex\ContextMenuHandlers\ModernSharing", true)) {
                        key.SetValue(string.Empty, "-{1d27f844-3a1f-4410-85ac-14651078412d}");
                        Log("Disabled modern share!");
                    }

                }
                catch (Exception ex)
                {
                    Log(ex.GetType().ToString() + " - something went wrong!");
                    // Ignore errors
                }
         

                baseReg.Dispose();

                MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /* Other stuff */
        private void RestartExplorer()
        {
            Process[] explorerProcess = Process.GetProcessesByName("explorer");
            foreach (var process in explorerProcess)
            {
                process.Kill();
            }
        }

        private void Log(string msg)
        {
            if (!string.IsNullOrEmpty(msg)) {
                consoleBox.Text += msg + Environment.NewLine;
            }
        }

        private void CheckTweaks()
        {
            try
            {
                //  check defender state
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender", false))
                {
                    if ((int)key.GetValue("DisableAntiSpyware", 0) == 1)
                    {
                        _defenderSwitch = true;
                        btnDefender.Text = "Enable Windows Defender";
                    }
                }
            }
            catch { }

            // check internet connection
            if (!NetworkInterface.GetIsNetworkAvailable()) {
                btnUpdate.Enabled = false;
                Log("Checking for updates is disabled because no internet connection were found!");
            }
        }

        private void RunCommand(string command)
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;

                try
                {
                    p.Start(); // start a command prompt

                    p.StandardInput.WriteLine(command); // run the command
                    p.StandardInput.Close();
                    p.WaitForExit();
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                }

            }
        }

        /* Metro related */
        private void UninstallBtn_Click(object sender, EventArgs e)
        {
            selectedApps = null;

            // Displays all the apps to be uninstalled
            if (appBox.CheckedItems.Count > 0) {
                foreach (string app in appBox.CheckedItems) {
                    if (string.IsNullOrEmpty(selectedApps)) {
                        selectedApps = app;
                    } else {
                        selectedApps += Environment.NewLine + app;
                    }
                }

                if (MessageBox.Show("Are you sure you want to uninstall the following app(s)?" + Environment.NewLine + selectedApps, "Confirm uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                    foreach (string app in appBox.CheckedItems) {
                        Task.Run(() => UninstallApp(app));
                    }

                    RefreshAppList(true); // refresh list when we're done
                    MessageBox.Show("OK!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            } else {
                MessageBox.Show("You haven't selected anything!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void RetrieveApps()
        {
            using (PowerShell script = PowerShell.Create()) {
                if (chkAll.Checked) {
                    script.AddScript("Get-AppxPackage -AllUsers | Select Name | Out-String -Stream");
                } else {
                    script.AddScript("Get-AppxPackage | Select Name | Out-String -Stream");
                }

                string trimmed = string.Empty;
                foreach (PSObject x in script.Invoke()) {
                    trimmed = x.ToString().Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.Contains("---")) {
                        if (trimmed != "Name") appBox.Items.Add(trimmed);
                    }
                }
            }
        }

        private void RefreshAppList(bool minusOne)
        {
            _goBack = appBox.SelectedIndex;
            appBox.Items.Clear();
            RetrieveApps();

            try {
                if (minusOne) {
                    appBox.SelectedIndex = _goBack - 1;
                } else {
                    appBox.SelectedIndex = _goBack;
                }
            } catch { }
        }

        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async void UninstallApp(string app)
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            bool error = false;

            using (PowerShell script = PowerShell.Create())
            {
                if (chkAll.Checked)
                {
                    script.AddScript("Get-AppxPackage -AllUsers " + app + " | Remove-AppxPackage");
                }
                else
                {
                    script.AddScript("Get-AppxPackage " + app + " | Remove-AppxPackage");
                }

                script.Invoke();
                error = script.HadErrors;
            }

            if (error)
            {
                Log("Could not uninstall app: " + app);
            }
            else
            {
                Log("App uninstalled: " + app);
            }

            return;
        }

        private void chkAll_CheckedChanged(object sender, EventArgs e)
        {
            RefreshAppList(false);
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshAppList(false);
        }

        private void tabMetro_Enter(object sender, EventArgs e)
        {
            this.Enabled = false;
            RefreshAppList(false);
            this.Enabled = true;
        }

    }
}
