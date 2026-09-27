namespace GhpcCoop {
 // Shared by the Steam adapter and offline ownership regression tests.
 public static class SteamRoomPolicy {
  public static bool CanAccept(ulong local,ulong candidate,bool member,uint current,uint incoming){return member&&candidate!=0&&candidate!=local&&(current==0||current==incoming);}
  public static bool IsOriginalHost(ulong expected,ulong actual){return expected!=0&&expected==actual;}
  public static bool Compatible(string tag,string wire,string gameBuild,string expectedBuild){return tag=="0.9.0"&&wire==Wire.Version.ToString()&&gameBuild==expectedBuild;}
 }
}
