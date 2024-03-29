﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CgStairFinder
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        #region dll import
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
        #endregion

        static readonly IDictionary<string, DetectLog> logs = new Dictionary<string, DetectLog>();

        private void Main_Load(object sender, EventArgs e)
        {
            SetCgDirDisplayText();
            CgListReload(true);
        }

        private void SetCgDirDisplayText()
        {
            var cgDir = Settings.Default.cgDir;

            if (string.IsNullOrEmpty(cgDir))
            {
                linkLabel2.Text = "未設定魔力寶貝目錄";
                return;
            }

            linkLabel2.Text = cgDir;
            toolTip1.SetToolTip(linkLabel2, linkLabel2.Text);
        }

        private void LinkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FolderBrowserDialog folderSelectionDialog = new FolderBrowserDialog
            {
                Description = "選擇魔力寶貝資料夾"
            };

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
            comboBox1.SelectedIndex = Convert.ToInt32(selectFirstItem && comboBox1.Items.Count > 1);
        }

        /// <summary>
        /// 按下開始偵測按鈕觸發
        /// </summary>
        private void Button1_Click(object sender, EventArgs e)
        {
            if (timer1.Enabled)
            {
                Stop();
                return;
            }

            var cgDir = Settings.Default.cgDir;
            if (string.IsNullOrEmpty(cgDir) ||
                !Directory.Exists($@"{cgDir}\map"))
            {
                MessageBox.Show(this, "啟動失敗, 請確認路徑是否正確", "訊息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Start();
        }

        private void Start()
        {
            timer1.Interval = 10;
            timer1.Start();
            button1.Text = "停止偵測";
            button2.Enabled = false;
            comboBox1.Enabled = false;
        }

        private void Stop()
        {
            timer1.Stop();
            label2.ResetText();
            listBox1.Items.Clear();
            button1.Text = "開始偵測";
            button2.Enabled = true;
            comboBox1.Enabled = true;
        }

        /// <summary>
        /// 啟動偵測後, 計時器每次的觸發行為
        /// </summary>
        private void Timer1_Tick(object sender, EventArgs e)
        {
            timer1.Interval = 500;

            int? hProcess = null;
            var mapName = string.Empty; // e.g. 法蘭城
            var mapFile = default(FileInfo);
            var isSelectdWindow = comboBox1.SelectedIndex > 0; // 0 is 不選擇

            try
            {
                // 有選擇視窗
                if (isSelectdWindow)
                {
                    var p = (Process)comboBox1.SelectedItem;
                    if (p.HasExited)
                    {
                        throw new Exception("視窗偵測失敗");
                    }

                    hProcess = OpenProcess(0x1F0FFF, false, p.Id);

                    // 取地圖名
                    var readMapNameBuffer = new byte[32];
                    ReadProcessMemory(hProcess.Value, 0x95C870, readMapNameBuffer, readMapNameBuffer.Length, 0);
                    mapName = Encoding.Default.GetString(readMapNameBuffer.TakeWhile(x => x != 0).ToArray());

                    // 取當前地圖檔名
                    var readMapFileBuffer = new byte[32];
                    ReadProcessMemory(hProcess.Value, 0x18CCC8, readMapFileBuffer, readMapFileBuffer.Length, 0);
                    var path = Encoding.Default.GetString(readMapFileBuffer.TakeWhile(x => x != 0).ToArray());
                    mapFile = new FileInfo(Path.Combine(Settings.Default.cgDir, path));
                    if (!mapFile.Exists)
                    {
                        throw new Exception("無法讀取地圖檔");
                    }
                }
                else
                {
                    mapFile =
                        new DirectoryInfo($@"{Settings.Default.cgDir}\map")
                        .GetFiles("*.dat", SearchOption.AllDirectories)
                        .OrderByDescending(f => f.LastWriteTime)
                        .FirstOrDefault();
                    Text = mapFile.Name;
                }

                label2.Text = mapFile.FullName;
                if (label2.Text.Length > 18)
                {
                    var c = mapFile.FullName.ToCharArray();
                    Array.Reverse(c);
                    Array.Resize(ref c, 16);
                    Array.Reverse(c);
                    label2.Text = $"...{new string(c)}";
                }

                listBox1.Items.Clear();
                var cgStairs = new CgMapStairFinder(mapFile).GetStairs();

                if (cgStairs.Count == 0)
                {
                    listBox1.Items.Add("沒有找到任何樓梯");
                    return;
                }

                logs[mapFile.Name] = new DetectLog { MapCode = mapFile.Name, MapName = mapName, CgStairs = cgStairs, DetectTime = DateTime.Now };

                foreach (var c in cgStairs)
                {
                    var type = CgStair.Translate(c.Type);
                    if (!isSelectdWindow)
                    {
                        listBox1.Items.Add($"東{c.East}, 南{c.South} -- {type}");
                        continue;
                    }

                    // 取當前座標
                    var east = default(int?);
                    var south = default(int?);
                    var buffer = new byte[4];
                    try
                    {
                        ReadProcessMemory(hProcess.Value, 0x95C88C, buffer, 4, 0);
                        east = (int)(BitConverter.ToSingle(buffer, 0) / 64);
                        ReadProcessMemory(hProcess.Value, 0x95C890, buffer, 4, 0);
                        south = (int)BitConverter.ToSingle(buffer, 0) / 64;
                    }
                    catch { /* 吃掉 */ }

                    var direction = string.Empty;
                    if (east.HasValue && south.HasValue)
                    {
                        #region 計算樓梯方向
                        var r = Math.Atan2(c.East - east.Value, c.South - south.Value) / Math.PI * 180;

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
                        #endregion
                    }

                    listBox1.Items.Add($"東{c.East}, 南{c.South} {direction} -- {type}");
                }
            }
            catch (IOException) { return; }
            catch (Exception ex)
            {
                Stop();
                MessageBox.Show(this, $"發生錯誤, 自動偵測已停止\n\n{ex.Message}", "訊息", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                if (hProcess.HasValue)
                {
                    CloseHandle(hProcess.Value);
                }
            }
        }

        /// <summary>
        /// list box 顏色
        /// </summary>
        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index == -1)
            {
                return;
            }

            Brush itemColor = Brushes.White;
            if (((ListBox)sender).Items[e.Index].ToString().Contains(Defined.STAIR_TYPE_UP_DISPLAY_TEXT))
            {
                itemColor = Brushes.SpringGreen;
            }
            else if (((ListBox)sender).Items[e.Index].ToString().Contains(Defined.STAIR_TYPE_DOWN_DISPLAY_TEXT))
            {
                itemColor = Brushes.OrangeRed;
            }
            else if (((ListBox)sender).Items[e.Index].ToString().Contains(Defined.STAIR_TYPE_MOVEABLE_DISPLAY_TEXT))
            {
                itemColor = Brushes.Silver;
            }

            e.Graphics.FillRectangle(itemColor, e.Bounds);
            e.Graphics.DrawString(((ListBox)sender).Items[e.Index].ToString(), Font, Brushes.Black, e.Bounds);
            e.DrawFocusRectangle();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            CgListReload(false);
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            var process = (Process)comboBox1.SelectedItem;
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
                var text = $"{kv.Value.DetectTime:yyyy-MM-dd HH:mm:ss} {kv.Key}";
                var mapName = kv.Value.MapName;
                text += $"({mapName})";
                text += ": ";
                text += string.Join(" | ", from a in kv.Value.CgStairs
                                           orderby a.Type
                                           select $"{a.East}, {a.South} -- {CgStair.Translate(a.Type)}");
                sb.AppendLine(text);
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("powered by CgStairFinder (https://github.com/WindOfNet/CgStairFinder/releases/latest)");
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

        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/WindOfNet/CgStairFinder");
        }
    }
}
