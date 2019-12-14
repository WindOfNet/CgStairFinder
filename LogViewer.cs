using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static CgStairFinder.Main;

namespace CgStairFinder
{
    public partial class LogViewer : Form
    {
        public LogViewer()
        {
            InitializeComponent();
        }

        public IDictionary<string, Log> Logs { get; set; }

        private void LogViewer_Load(object sender, EventArgs e)
        {
            foreach (var kv in Logs.OrderBy(x => x.Value.MapName))
            {
                var text = $"{kv.Key}";
                if (!string.IsNullOrEmpty(kv.Value.MapName)) { text += $"({kv.Value.MapName})"; }
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

                this.textBox1.AppendText(text + "\r\n");
            }
        }
    }
}
