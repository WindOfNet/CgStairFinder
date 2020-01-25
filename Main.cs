using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CgStairFinder
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hwnd, int nCmdShow);
        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int nSize, int lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]
        private static extern int OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        private static extern void CloseHandle(int hObject);

        static IDictionary<string, DetectLog> logs = new Dictionary<string, DetectLog>();

        private void Main_Load(object sender, EventArgs e)
        {

#if !DEBUG
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var currentVersion = Application.ProductVersion.Replace(".0.0", string.Empty);

                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/windofnet/CgStairFinder/releases/latest");
                    request.UserAgent = nameof(CgStairFinder);
                    using (var response = request.GetResponse())
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        var json = sr.ReadToEnd();
                        var js = new DataContractJsonSerializer(typeof(GithubRelease));
                        var githubRelease = (GithubRelease)js.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(json)));
                        if (githubRelease.Name != currentVersion)
                        {
                            var result = MessageBox.Show("發現有更新版本, 是否要前往下載？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (result == DialogResult.Yes)
                            {
                                Process.Start("https://github.com/WindOfNet/CgStairFinder/releases/latest");
                            }
                        }
                    }
                }
                catch { }
            });
#endif

            SetCgDirDisplayText();
            CgListReload(true);
        }

        private void SetCgDirDisplayText()
        {
            var cgDir = Settings.Default.cgDir;

            if (string.IsNullOrEmpty(cgDir))
            {
                this.linkLabel2.Text = "未設定魔力寶貝目錄";
                return;
            }
            else
            {
                this.linkLabel2.Text = cgDir;
            }

            toolTip1.SetToolTip(this.linkLabel2, this.linkLabel2.Text);
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FolderBrowserDialog folderSelectionDialog = new FolderBrowserDialog();
            folderSelectionDialog.Description = "選擇魔力寶貝資料夾";
            folderSelectionDialog.ShowDialog();
            Settings.Default.cgDir = folderSelectionDialog.SelectedPath;
            Settings.Default.Save();
            SetCgDirDisplayText();
        }

        private void CgListReload(bool selectFirstItem)
        {
            comboBox1.Items.Clear();
            comboBox1.Items.Add("不選擇視窗");
            comboBox1.Items.AddRange(Process.GetProcessesByName("bluecg"));
            comboBox1.Items.AddRange(Process.GetProcessesByName("bluehd"));
            comboBox1.Items.AddRange(Process.GetProcessesByName("cg"));
            comboBox1.DisplayMember = "MainWindowTitle";
            this.comboBox1.SelectedIndex = Convert.ToInt32(selectFirstItem && comboBox1.Items.Count > 1);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (this.timer1.Enabled)
            {
                stop();
                return;
            }

            var cgDir = Settings.Default.cgDir;
            if (string.IsNullOrEmpty(cgDir) ||
                !Directory.Exists($"{cgDir}\\map"))
            {
                MessageBox.Show(this, "啟動失敗, 請確認路徑是否正確", "訊息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            start();
        }

        private void start()
        {
            this.timer1.Interval = 10;
            this.timer1.Start();
            this.button1.Text = "停止偵測";
            this.numericUpDown1.Enabled = false;
            button2.Enabled = false;
            comboBox1.Enabled = false;
        }

        private void stop()
        {
            this.timer1.Stop();
            this.label2.ResetText();
            this.listBox1.Items.Clear();
            this.button1.Text = "開始偵測";
            this.numericUpDown1.Enabled = true;
            button2.Enabled = true;
            comboBox1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.timer1.Interval = (int)this.numericUpDown1.Value * 1000;

            int? hProcess = null;
            var mapName = string.Empty; // e.g. 法蘭城
            var mapFile = default(FileInfo);

            try
            {
                if (comboBox1.SelectedIndex > 0)
                {
                    var p = (Process)comboBox1.SelectedItem;
                    if (p.HasExited)
                    {
                        throw new Exception();
                    }

                    hProcess = OpenProcess(0x1F0FFF, false, p.Id);

                    if (hProcess.HasValue)
                    {
                        var readMapNameBuffer = new byte[32];
                        ReadProcessMemory(hProcess.Value, 0x95C870, readMapNameBuffer, readMapNameBuffer.Length, 0);
                        mapName = Encoding.Default.GetString(readMapNameBuffer.TakeWhile(x => x != 0).ToArray());
                    }
                }

                mapFile = new DirectoryInfo($"{Settings.Default.cgDir}\\map").GetFiles("*.dat", SearchOption.AllDirectories).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                this.Text = string.IsNullOrEmpty(mapName) ? mapFile.Name : mapName;

                if (mapFile.FullName.Length > 18)
                {
                    char[] c = mapFile.FullName.ToCharArray();
                    Array.Reverse(c);
                    Array.Resize(ref c, 16);
                    Array.Reverse(c);
                    this.label2.Text = $"..{new string(c)}";
                }
                else
                {
                    this.label2.Text = $"{mapFile.FullName}";
                }

                listBox1.Items.Clear();
                var cgStairs = new CgMapStairFinder(mapFile).GetStairs();

                if (cgStairs.Count == 0)
                {
                    listBox1.Items.Add("沒有找到任何樓梯");
                }
                else
                {
                    if (!string.IsNullOrEmpty(mapName))
                    {
                        MapCounter.Count(mapFile.Name, mapName);
                    }

                    logs[mapFile.Name] = new DetectLog { MapCode = mapFile.Name, CgStairs = cgStairs, DetectTime = DateTime.Now };

                    foreach (var c in cgStairs)
                    {
                        string type = string.Empty;
                        switch (c.Type)
                        {
                            case StairType.Down: type = "下樓"; break;
                            case StairType.Up: type = "上樓"; break;
                            case StairType.Jump: type = "可移動"; break;
                            case StairType.Unknow: type = "不明"; break;
                        }

                        if (!hProcess.HasValue)
                        {
                            listBox1.Items.Add($"東{c.East}, 南{c.South} -- {type}");
                        }
                        else
                        {
                            int east = 0, south = 0;
                            byte[] buffer = new byte[32];
                            ReadProcessMemory(hProcess.Value, 0xBF6B54, buffer, 2, 0);
                            east = BitConverter.ToInt16(buffer, 0);
                            ReadProcessMemory(hProcess.Value, 0xBF6C1C, buffer, 2, 0);
                            south = BitConverter.ToInt16(buffer, 0);

                            string direction = string.Empty;

                            var r = Math.Atan2(c.East - east, c.South - south) / Math.PI * 180;

                            if (r <= -135 + 22.5 && r >= -135 - 22.5)
                            {
                                direction = "←";
                            }
                            if (r <= -90 + 22.5 && r >= -90 - 22.5)
                            {
                                direction = "↙";
                            }
                            if (r <= -45 + 22.5 && r >= -45 - 22.5)
                            {
                                direction = "↓";
                            }
                            if (r <= 0 + 22.5 && r >= 0 - 22.5)
                            {
                                direction = "↘";
                            }
                            if (r <= 45 + 22.5 && r >= 45 - 22.5)
                            {
                                direction = "→";
                            }
                            if (r <= 90 + 22.5 && r >= 90 - 22.5)
                            {
                                direction = "↗";
                            }
                            if (r <= 135 + 22.5 && r >= 135 - 22.5)
                            {
                                direction = "↑";
                            }
                            if (r < -135 - 22.5 || (r <= 180 + 22.5 && r >= 180 - 22.5))
                            {
                                direction = "↖";
                            }

                            listBox1.Items.Add($"東{c.East}, 南{c.South} {direction} -- {type}");
                        }
                    }
                }
            }
            catch (IOException) { return; }
            catch (Exception)
            {
                stop();
                MessageBox.Show(this, $"發生錯誤, 自動偵測已停止", "訊息", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                if (hProcess.HasValue)
                {
                    CloseHandle(hProcess.Value);
                }
            }
        }

        private void listBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index == -1)
            {
                return;
            }

            Brush itemColor = Brushes.White;
            if (((ListBox)sender).Items[e.Index].ToString().Contains("上樓"))
            {
                itemColor = Brushes.SpringGreen;
            }
            else if (((ListBox)sender).Items[e.Index].ToString().Contains("下樓"))
            {
                itemColor = Brushes.OrangeRed;
            }
            else if (((ListBox)sender).Items[e.Index].ToString().Contains("可移動"))
            {
                itemColor = Brushes.Silver;
            }

            e.Graphics.FillRectangle(itemColor, e.Bounds);

            e.Graphics.DrawString(((ListBox)sender).Items[e.Index].ToString(), this.Font, Brushes.Black, e.Bounds);
            e.DrawFocusRectangle();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            CgListReload(false);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Process process = comboBox1.SelectedItem as Process;
            if (process is null)
            {
                return;
            }

            ShowWindow(process.MainWindowHandle, 9);
            SetForegroundWindow(process.MainWindowHandle);
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            if (!logs.Any())
            {
                MessageBox.Show("沒有任何紀錄", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tmpPath = Path.GetTempFileName();
            var sb = new StringBuilder();

            foreach (var kv in logs.OrderBy(x => x.Value.DetectTime))
            {
                var text = $"{kv.Value.DetectTime.ToString("yyyy-MM-dd HH:mm:ss")} {kv.Key}";
                var mapName = MapCounter.GetMapName(kv.Value.MapCode);
                text += $"({mapName})";
                text += ": ";

                text += string.Join(" | ", kv.Value.CgStairs.OrderBy(x => x.Type).Select(x =>
                {
                    string type = string.Empty;
                    switch (x.Type)
                    {
                        case StairType.Down: type = "下樓"; break;
                        case StairType.Up: type = "上樓"; break;
                        case StairType.Jump: type = "可移動"; break;
                        case StairType.Unknow: type = "不明"; break;
                    }

                    return $"{x.East}, {x.South} -- {type}";
                }));

                sb.AppendLine(text);
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("power by CgStairFinder (https://github.com/WindOfNet/CgStairFinder/releases/latest)");
            File.WriteAllText(tmpPath, sb.ToString());

            Process.Start("notepad.exe", tmpPath);
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (logs.Count > 0)
            {
                e.Cancel = MessageBox.Show(this, "是否要結束程式？ (紀錄將會清除)", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No;
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/WindOfNet/CgStairFinder");
        }
    }
}
