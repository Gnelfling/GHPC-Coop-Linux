using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GhpcCoop;
namespace HarmonyLib { public static class AccessTools {public static FieldInfo Field(Type t,string n){return t.GetField(n,BindingFlags.NonPublic|BindingFlags.Instance);}} }
namespace UnityEngine {public static class Resources {public static T[] FindObjectsOfTypeAll<T>(){return new T[0];}}}
namespace GHPC.Mission {
 public enum UnitClass { Tank=0 }
 public class AmmoLogisticsScriptable { public string name;public AmmoLogisticsData AmmoLogistics; }
 public class AmmoLogisticsData {public object[] AmmoOptions;}
 public class DynamicMissionAmmoAdjustment {public AmmoLogisticsScriptable AmmoSet; public int SelectedIndex;}
 public class DynamicMissionMetadataOverrides {
  public struct UnitReplacementData {public bool Friendly;public UnitClass UClass;public int VariantIndex;public string NewUnitKey;}
  readonly IList _unitReplacements=new List<UnitReplacementData>();
  public string MissionName,FriendlyInfantryArmyOverride,EnemyInfantryArmyOverride;
  public List<DynamicMissionAmmoAdjustment> FriendlyAmmoOverrides=new List<DynamicMissionAmmoAdjustment>(),EnemyAmmoOverrides=new List<DynamicMissionAmmoAdjustment>();
  public int Count {get{return _unitReplacements.Count;}}
  public void AddUnitReplacement(bool f,UnitClass c,int v,string k,bool replace){_unitReplacements.Add(new UnitReplacementData{Friendly=f,UClass=c,VariantIndex=v,NewUnitKey=k});}
  public void AddAmmoReplacement(bool f,AmmoLogisticsScriptable s,int i){(f?FriendlyAmmoOverrides:EnemyAmmoOverrides).Add(new DynamicMissionAmmoAdjustment{AmmoSet=s,SelectedIndex=i});}
 }
 public static class DynamicMissionLauncher {
  public static Dictionary<string,DynamicMissionMetadataOverrides> Values=new Dictionary<string,DynamicMissionMetadataOverrides>();
  public static DynamicMissionMetadataOverrides GetFlexOverrides(string m){DynamicMissionMetadataOverrides x;return Values.TryGetValue(m,out x)?x:null;}
  public static void SaveFlexOverrides(DynamicMissionMetadataOverrides x){Values[x.MissionName]=x;}
  public static void ClearFlexOverrides(string m){Values.Remove(m);}
 }
}
class MissionConfigurationTests {
 static int count;
 static void Check(bool ok){if(!ok)throw new Exception("Mission configuration test failed "+count);count++;}
 static void Main(){
  var original=new GHPC.Mission.DynamicMissionMetadataOverrides{MissionName="mission",FriendlyInfantryArmyOverride="army"};
  original.AddUnitReplacement(true,GHPC.Mission.UnitClass.Tank,0,"T72",false);
  GHPC.Mission.DynamicMissionLauncher.SaveFlexOverrides(original);
  string payload=MissionConfiguration.Export("mission");
  MissionConfiguration.Apply("mission",payload);
  var copy=GHPC.Mission.DynamicMissionLauncher.GetFlexOverrides("mission");
  Check(copy!=original&&copy.Count==1&&copy.FriendlyInfantryArmyOverride=="army");
  MissionConfiguration.Restore();Check(object.ReferenceEquals(original,GHPC.Mission.DynamicMissionLauncher.GetFlexOverrides("mission")));
  string empty=MissionConfiguration.Export("other");MissionConfiguration.Apply("mission",empty);
  Check(GHPC.Mission.DynamicMissionLauncher.GetFlexOverrides("mission").Count==0);
  MissionConfiguration.Restore();Check(object.ReferenceEquals(original,GHPC.Mission.DynamicMissionLauncher.GetFlexOverrides("mission")));
  foreach(string bad in new[]{"!",new string('A',3300),Convert.ToBase64String(new byte[]{99,0,0,0}),payload+"AAAA"}){
   bool rejected=false;try{MissionConfiguration.Apply("mission",bad);}catch{rejected=true;}
   Check(rejected);Check(object.ReferenceEquals(original,GHPC.Mission.DynamicMissionLauncher.GetFlexOverrides("mission")));
  }
  MissionConfiguration.Apply("new",empty);MissionConfiguration.Restore();Check(GHPC.Mission.DynamicMissionLauncher.GetFlexOverrides("new")==null);
  Console.WriteLine("PASS "+count+" mission configuration fixture checks; not native game loading");
 }
}
