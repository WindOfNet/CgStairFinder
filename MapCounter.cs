using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace CgStairFinder
{
    /// <summary>
    /// 計數偵測到的地圖檔與地圖名的對應資訊
    /// 因偵測到的檔案與視窗實際讀取到的地圖名會有時間差, 以計數較高者的地圖名為主
    /// </summary>
    public static class MapCounter
    {
        struct countData
        {
            public string MapFileName { get; set; }
            public string MapName { get; set; }

            public countData(string mapFileName, string mapName)
            {
                MapFileName = mapFileName;
                MapName = mapName;
            }
        }

        class mapCount
        {
            public countData CountData { get; set; }
            public int Count { get; set; }
        }

        static IList<mapCount> countLog = new List<mapCount>();

        public static void Count(string mapFileName, string mapName)
        {
            var countData = countLog.Where(x => x.CountData.MapFileName == mapFileName)
                                    .Where(x => x.CountData.MapName == mapName)
                                    .SingleOrDefault();

            if (countData == null)
            {
                var mapCount = new mapCount { CountData = new countData(mapFileName, mapName), Count = 1 };
                countLog.Add(mapCount);
            }
            else
            {
                countData.Count++;
            }
        }

        public static string GetMapName(string mapFileName)
        {
            var mapCount = countLog.Where(x => x.CountData.MapFileName == mapFileName)
                                   .OrderByDescending(x => x.Count)
                                   .FirstOrDefault();

            return mapCount?.CountData.MapName ?? mapFileName;
        }
    }
}
