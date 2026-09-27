using System.Linq;using System.Collections.Generic;
namespace GhpcCoop {
 // Shared by the Steam adapter and offline ownership regression tests.
 public static class SteamRoomPolicy {
  public static bool CanAcceptMulti(ulong local,ulong candidate,bool member,int capacity,IEnumerable<ulong> active){var ids=active.ToArray();return capacity>=2&&capacity<=4&&member&&candidate!=0&&candidate!=local&&!ids.Contains(candidate)&&ids.Length<capacity-1;}
  public static bool CanAccept(ulong local,ulong candidate,bool member,uint current,uint incoming){return member&&candidate!=0&&candidate!=local&&(current==0||current==incoming);}
  public static bool IsOriginalHost(ulong expected,ulong actual){return expected!=0&&expected==actual;}
  public static bool Compatible(string tag,string wire,string gameBuild,string expectedBuild){return tag=="0.9.9.1"&&wire==Wire.Version.ToString()&&gameBuild==expectedBuild;}
 }
}
