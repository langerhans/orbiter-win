using Orbiter.Properties;
using Orbiter.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Orbiter
{
    class OrbiterApplicationContext : ApplicationContext
    {
        private IContainer mComponents;
        private NotifyIcon mNotifyIcon;
        private ContextMenuStrip mContextMenu;
        private ToolStripMenuItem mExit;
        private String mDataDir;
        private String mWalletExec;
        private List<String> mWalletFiles;

        public OrbiterApplicationContext()
        {
            mComponents = new Container();

            mNotifyIcon = new NotifyIcon(mComponents);
            mNotifyIcon.Icon = (Icon)Properties.Resources.ResourceManager.GetObject("orbiter");
            mNotifyIcon.Text = Strings.appname;
            mNotifyIcon.Visible = true;

            mContextMenu = new ContextMenuStrip();
            mExit = new ToolStripMenuItem();

            mNotifyIcon.ContextMenuStrip = mContextMenu;

            mExit.Text = Strings.exit;
            mExit.Click += mExit_Click;

            mDataDir = getDataDir();
            mWalletExec = getWalletExec();
            mWalletFiles = getWalletFiles();

            foreach (String file in mWalletFiles)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(file);
                item.Click += (sender, e) => loadWallet(file, item);
                mContextMenu.Items.Add(item);
            }

            mContextMenu.Items.Add(new ToolStripSeparator());
            mContextMenu.Items.Add(mExit);
        }

        private String getWalletExec()
        {
            // Try 1: We have the path saved
            if (!Settings.Default.dogecoinExec.Equals(""))
            {
                if (File.Exists(Settings.Default.dogecoinExec))
                    return Settings.Default.dogecoinExec;
            }

            // Try 2: If it's running, grab the path
            Process[] processes = Process.GetProcessesByName("dogecoin-qt"); // We assume the user didn't rename the executable...
            if (processes.Length != 0)
            {
                Settings.Default.dogecoinExec = processes[0].MainModule.FileName;
                Settings.Default.Save();
                return processes[0].MainModule.FileName;
            }

            // Try 3: Default path
            String defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Dogecoin", "dogecoin-qt.exe");
            if (File.Exists(defaultPath))
            {
                Settings.Default.dogecoinExec = defaultPath;
                Settings.Default.Save();
                return defaultPath;
            }

            // Try 4: Let the user search it
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;
            ofd.Multiselect = false;
            ofd.ValidateNames = true;
            ofd.Filter = "dogecoin-qt.exe|*.exe";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Settings.Default.dogecoinExec = ofd.FileName;
                Settings.Default.Save();
                return ofd.FileName;
            }
            else
            {
                if (MessageBox.Show(Strings.searchExecAgain, Strings.error, MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                    getWalletExec();
                else
                    Environment.Exit(0);
                return null;
            }
        }

        private Process isDogecoinRunning()
        {
            Process[] processes = Process.GetProcessesByName("dogecoin-qt"); // We assume the user didn't rename the executable...
            if (processes.Length != 0)
                return processes[0];
            return null;
        }

        private List<String> getWalletFiles()
        {
            Regex re = new Regex(@"^blk\d{4}");
            return Directory.EnumerateFiles(mDataDir, "*.dat", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileName(f))
                .Where(f => 
                    !re.IsMatch(f) &&
                    !f.StartsWith("blkindex") &&
                    !f.StartsWith("peers") &&
                    f.EndsWith(".dat")) // That's needed because the filter above would let "wallet.dat_2" through...
                .ToList();
        }

        private String getDataDir()
        {
            // Try 1: Load the saved directory from settings
            if (!Settings.Default.dogecoinDir.Equals(""))
            {
                if (Directory.Exists(Settings.Default.dogecoinDir))
                    return Settings.Default.dogecoinDir;
            }

            // Try 2: Check the default directory
            String defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DogeCoin");
            if (Directory.Exists(defaultPath))
            {
                Settings.Default.dogecoinDir = defaultPath;
                Settings.Default.Save();
                return defaultPath;
            }

            // Try 3: Prompt the user and save it
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;
            fbd.Description = Strings.promptDataDir;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(Path.Combine(fbd.SelectedPath, "blkindex.dat"))) // Assume a valid dataDir has that file
                {
                    Settings.Default.dogecoinDir = fbd.SelectedPath;
                    Settings.Default.Save();
                    return fbd.SelectedPath;
                }
                else
                {
                    if (MessageBox.Show(Strings.notDataDir, Strings.error, MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                        getDataDir();
                    else
                        Environment.Exit(0);
                    return null;
                }
            }
            else
            {
                if (MessageBox.Show(Strings.notDataDir, Strings.error, MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                    getDataDir();
                else
                    Environment.Exit(0);
                return null;
            }
        }

        private void loadWallet(String file, ToolStripMenuItem item)
        {
            item.Checked = true;
            file = file.Substring(0, file.LastIndexOf('.'));

            Process runningProcess = isDogecoinRunning();
            if (runningProcess != null)
            {
                runningProcess.EnableRaisingEvents = true;
                runningProcess.Exited += (sender, e) => execWallet(file);
                runningProcess.Kill();
            }
            else
            {
                execWallet(file);
            }
        }

        private void execWallet(String file)
        {
            Process.Start(mWalletExec, " -wallet=" + file);
        }

        void mExit_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
