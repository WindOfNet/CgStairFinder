using System;
using System.Collections.Generic;

namespace CgStairFinder
{
    public class DetectLog
    {
        public string MapCode { get; set; }
        public string MapName { get; set; }
        public IList<CgStair> CgStairs { get; set; }
        public DateTime DetectTime { get; set; }
    }
}
