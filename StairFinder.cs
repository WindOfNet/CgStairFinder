using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CgStairFinder
{
    public enum StairType
    {
        [Description("上樓")]
        Up, // 上樓
        [Description("下樓")]
        Down, // 下樓
        [Description("可移動")]
        Jump, // 可移動
        [Description("不明")]
        Unknow
    }

    public struct CgStair
    {
        public int East { get; set; } // 座標 東
        public int South { get; set; } // 座標 南
        public StairType Type { get; set; }
        public static string Translate(StairType stairType)
        {
            var prop = typeof(StairType).GetProperty(Enum.GetName(typeof(StairType), stairType));
            var attr = (DescriptionAttribute)prop.GetCustomAttributes(typeof(DescriptionAttribute), false)[0];
            return attr.Description;
        }
    }

    public class CgMapStairFinder
    {
        private readonly MemoryStream ms;

        public CgMapStairFinder(FileInfo file)
        {
            using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ms = new MemoryStream();
                stream.CopyTo(ms);
            }
        }

        static StairType GetStType(ushort gnum)
        {
            // 從物件編號(gnum)得知物件圖片(從bin取出)是上樓或下樓
            switch (gnum)
            {
                case 12000:
                case 12001:
                case 13268:
                case 13270:
                case 13272:
                case 13274:
                case 13996:
                case 13998:
                case 15561:
                case 15887:
                case 15889:
                case 15891:
                case 17952:
                case 17954:
                case 17956:
                case 17958:
                case 17960:
                case 17962:
                case 17964:
                case 17966:
                case 17968:
                case 17970:
                case 17972:
                case 17974:
                case 17976:
                case 17978:
                case 17980:
                case 17982:
                case 17984:
                case 17986:
                case 17988:
                case 17990:
                case 17992:
                case 17994:
                case 17996:
                case 17998:
                case 16610:
                case 16611:
                case 16626:
                case 16627:
                case 16628:
                case 16629:
                    return StairType.Up;

                case 12002:
                case 12003:
                case 13269:
                case 13271:
                case 13273:
                case 13275:
                case 13997:
                case 13999:
                case 15562:
                case 15888:
                case 15890:
                case 15892:
                case 17953:
                case 17955:
                case 17957:
                case 17959:
                case 17961:
                case 17963:
                case 17965:
                case 17967:
                case 17969:
                case 17971:
                case 17973:
                case 17975:
                case 17977:
                case 17979:
                case 17981:
                case 17983:
                case 17985:
                case 17987:
                case 17989:
                case 17991:
                case 17993:
                case 17995:
                case 17997:
                case 17999:
                case 16612:
                case 16613:
                case 16614:
                case 16615:
                    return StairType.Down;

                case 14676:
                case 0:
                    return StairType.Jump;

                default:
                    return StairType.Unknow;
            }
        }

        public IList<CgStair> GetStairs()
        {
            var result = new List<CgStair>();

            // http://cgsword.com/filesystem_graphicmap.htm#mapdat 地圖檔解析
            int width, height, sectionOffset;
            using (ms)
            using (var br = new BinaryReader(ms))
            {
                // 檔頭的頭3字節為固定字符MAP，隨後9字節均為0/空白
                // start: 12
                ms.Seek(12, SeekOrigin.Begin);
                // 2個DWORD(4字節)的數據，第1個表示地圖長度-東(W)，第2個表示地圖長度-南(H)
                width = br.ReadInt32();
                height = br.ReadInt32();
                // 每個數據塊的 section
                sectionOffset = width * height * 2;

                for (var i = 0; i < height; i++)
                {
                    for (var j = 0; j < width; j++)
                    {
                        // 移到場景轉換數據塊
                        ms.Seek(20 + (j + i * width) * 2, SeekOrigin.Begin);
                        ms.Seek(sectionOffset * 2, SeekOrigin.Current);

                        /*
                         * 49154 怪物?
                         * 49155 迷宮樓梯
                         * 49162 可過地圖
                         * 49163 法蘭城租屋
                         */
                        ushort target = br.ReadUInt16();

                        if (target == 49155)
                        {
                            // 回物件數據塊取樓梯編號
                            ms.Seek(-sectionOffset - 2, SeekOrigin.Current);
                            ushort gnum = br.ReadUInt16();
                            result.Add(new CgStair { East = j, South = i, Type = GetStType(gnum) });
                        }
                    }
                }
            }

            return result;
        }
    }
}
