using System;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using UnityEngine;using GHPC;using GHPC.Weapons;using GHPC.Weapons.Artillery;using HarmonyLib;
namespace GhpcCoop {
 public sealed class SupportSync:IDisposable {
  public static SupportSync Current;public readonly bool Host;public bool Ready;bool handlingRequest;
  readonly Queue<Message> outgoing=new Queue<Message>();readonly List<GameObject> objects=new List<GameObject>();readonly HashSet<string> pending=new HashSet<string>();
  static readonly FieldInfo Missions=AccessTools.Field(typeof(ArtilleryBattery),"_missionsAvailable"),Firing=AccessTools.Field(typeof(ArtilleryBattery),"<IsFiring>k__BackingField"),Cooldown=AccessTools.Field(typeof(ArtilleryBattery),"<RemainingCooldown>k__BackingField"),Delay=AccessTools.Field(typeof(ArtilleryBattery),"<RemainingDelay>k__BackingField"),Impact=AccessTools.Field(typeof(ArtilleryBattery),"<TimeUntilImpactSeconds>k__BackingField"),GrenadeEffect=AccessTools.Field(typeof(GHPC.Weaponry.Grenade),"_effectPrefab");
  public SupportSync(bool host){Host=host;Current=this;}
  static IEnumerable<KeyValuePair<string,ArtilleryBattery>> Batteries(){var m=FireMissionManager.Instance;if(m==null)yield break;var all=new[]{m.BlueArtilleryBatteries,m.RedArtilleryBatteries};for(int side=0;side<2;side++){var list=all[side];if(list==null)continue;for(int i=0;i<list.Length;i++)if(list[i]!=null)yield return new KeyValuePair<string,ArtilleryBattery>((side==0?"B:":"R:")+i,list[i]);}}
  static ArtilleryBattery Find(string id){return Batteries().Where(x=>x.Key==id).Select(x=>x.Value).FirstOrDefault();}
  public bool Request(ArtilleryBattery b,Vector3 point,IndirectFireMunitionType type){
   if(!Ready)return false;var key=Batteries().FirstOrDefault(x=>object.ReferenceEquals(x.Value,b)).Key;if(key==null||pending.Contains(key))return false;
   pending.Add(key);outgoing.Enqueue(new Message{Kind=Kind.SupportRequest,SupportId=key,Munition=(int)type,SupportX=point.x,SupportY=point.y,SupportZ=point.z});GameBridge.Log("SUPPORT request "+key+" type="+type);return false;
  }
  public Message HandleRequest(Message m,Unit guest){
   var b=Find(m.SupportId);bool owned=false;var manager=FireMissionManager.Instance;
   if(manager!=null&&b!=null&&guest!=null){var own=guest.Allegiance==Faction.Red?manager.RedArtilleryBatteries:guest.Allegiance==Faction.Blue?manager.BlueArtilleryBatteries:null;owned=own!=null&&own.Contains(b);}
   bool ok=owned&&b.HasMunitionType((IndirectFireMunitionType)m.Munition)&&b.IsReadyToFire;
   if(ok){handlingRequest=true;try{ok=FireMissionManager.Instance.SendFireMissionOnCall(new Vector3(m.SupportX,m.SupportY,m.SupportZ),b,(IndirectFireMunitionType)m.Munition).IsSuccess;}finally{handlingRequest=false;}}
   if(ok){var point=new Vector3(m.SupportX,m.SupportY,m.SupportZ);Mark(b,point);var map=GHPC.UI.MapController.Instance;var button=Resources.FindObjectsOfTypeAll<GHPC.UI.Map.MapIconControlType>().FirstOrDefault(x=>x.SupportInfos!=null&&x.SupportInfos.Contains(b));if(map!=null&&button!=null)AccessTools.Method(typeof(GHPC.UI.MapController),"OnMissionCalled").Invoke(map,new object[]{new MapMissionResult(true,b,point),button.MapControlType});}
   GameBridge.Log("SUPPORT host request "+m.SupportId+" accepted="+ok);
   return new Message{Kind=Kind.SupportResult,Supports=Capture(),SupportId=m.SupportId,Munition=m.Munition,SupportX=m.SupportX,SupportY=m.SupportY,SupportZ=m.SupportZ,SupportAccepted=ok,Text=ok?"Fire support accepted":"Fire support unavailable"};
  }
  public void Accepted(ArtilleryBattery b,Vector3 point,IndirectFireMunitionType type){if(!Host||!Ready||handlingRequest)return;var key=Batteries().FirstOrDefault(x=>object.ReferenceEquals(x.Value,b)).Key;if(key!=null)outgoing.Enqueue(new Message{Kind=Kind.SupportResult,SupportId=key,Munition=(int)type,SupportX=point.x,SupportY=point.y,SupportZ=point.z,SupportAccepted=true});}
  public SupportState[] Capture(){return Batteries().Select(x=>new SupportState{Id=x.Key,Missions=x.Value.RemainingMissions,Firing=x.Value.IsFiring,Cooldown=x.Value.RemainingCooldown,Delay=x.Value.RemainingDelay,Impact=x.Value.TimeUntilImpactSeconds}).ToArray();}
  public void Apply(SupportState[] states){foreach(var s in states){var b=Find(s.Id);if(b==null)throw new InvalidOperationException("Fire support battery layout differs: "+s.Id);Missions.SetValue(b,s.Missions);Firing.SetValue(b,s.Firing);Cooldown.SetValue(b,s.Cooldown);Delay.SetValue(b,s.Delay);Impact.SetValue(b,s.Impact);}}
  public void Result(Message m){pending.Remove(m.SupportId);Apply(m.Supports);if(m.SupportAccepted){var b=Find(m.SupportId);if(b!=null)Mark(b,new Vector3(m.SupportX,m.SupportY,m.SupportZ));}GameBridge.Log("SUPPORT result "+m.SupportId+" accepted="+m.SupportAccepted);}
  readonly List<GHPC.UI.Map.MapIcon> icons=new List<GHPC.UI.Map.MapIcon>();
  void Mark(ArtilleryBattery b,Vector3 point){var map=GHPC.UI.MapController.Instance;if(map==null||!map.IsInitialized)return;var icon=map.AddManuallyUpdatedIcon("",GHPC.UI.Map.MapIconType.Artillery,new Color(),map.GetMapLocalPosition(point),Quaternion.identity);icon.IsSelectable=false;icon.Screenspace=false;icons.Add(icon);var cooldown=GHPC.Event.CooldownManager.Instance;if(cooldown!=null){cooldown.TrackCooldown(icon,Mathf.Max(1,b.TotalTimeToComplete),0);foreach(var button in Resources.FindObjectsOfTypeAll<GHPC.UI.Map.MapIconControlType>())if(button.SupportInfos!=null&&button.SupportInfos.Contains(b))cooldown.TrackCooldown(b,button);}}
  static IEnumerable<KeyValuePair<string,GameObject>> Prefabs(){foreach(var pair in Batteries())foreach(var m in pair.Value.MunitionsChoices){if(m==null||m.DefaultProjectile==null)continue;string key=pair.Key+":"+(int)m.Type;yield return new KeyValuePair<string,GameObject>("P:"+key,m.DefaultProjectile);var g=m.DefaultProjectile.GetComponentInChildren<GHPC.Weaponry.Grenade>(true);if(g!=null){var fx=GrenadeEffect.GetValue(g) as GameObject;if(fx!=null)yield return new KeyValuePair<string,GameObject>("E:"+key,fx);}}}
  public static GameObject SpawnAndCapture(GameObject prefab){
   var obj=UnityEngine.Object.Instantiate(prefab);var sync=Current;if(sync==null||!sync.Host||!sync.Ready)return obj;
   var entry=Prefabs().FirstOrDefault(x=>x.Value==prefab&&x.Key.StartsWith("P:",StringComparison.Ordinal));if(entry.Key==null)return obj;
   var grenade=obj.GetComponentInChildren<GHPC.Weaponry.Grenade>(true);
   if(grenade!=null){string key="E:"+entry.Key.Substring(2);grenade.Exploded+=g=>{if(Current==sync)sync.QueueVisual(key,g.transform.position);};}
   else sync.pendingSpawns.Add(new KeyValuePair<string,GameObject>(entry.Key,obj));return obj;
  }
  readonly List<KeyValuePair<string,GameObject>> pendingSpawns=new List<KeyValuePair<string,GameObject>>();
  void QueueVisual(string key,Vector3 p){if(outgoing.Count>=256)return;outgoing.Enqueue(new Message{Kind=Kind.SupportVisual,SupportId=key,SupportX=p.x,SupportY=p.y,SupportZ=p.z});GameBridge.Log("SUPPORT visual "+key+" at="+p);}
  public Message[] Drain(){foreach(var p in pendingSpawns)if(p.Value!=null)QueueVisual(p.Key,p.Value.transform.position);pendingSpawns.Clear();var result=outgoing.ToArray();outgoing.Clear();foreach(var m in result)if(m.Kind==Kind.SupportResult)m.Supports=Capture();return result;}
  public void Visual(Message m){var prefab=Prefabs().Where(x=>x.Key==m.SupportId).Select(x=>x.Value).FirstOrDefault();if(prefab==null){GameBridge.Log("SUPPORT unknown visual "+m.SupportId);return;}var obj=UnityEngine.Object.Instantiate(prefab,new Vector3(m.SupportX,m.SupportY,m.SupportZ),Quaternion.identity);objects.Add(obj);UnityEngine.Object.Destroy(obj,240);GameBridge.Log("SUPPORT visual replay "+m.SupportId);}
  public void Dispose(){if(Current==this)Current=null;foreach(var icon in icons)icon.Dispose();icons.Clear();foreach(var o in objects)if(o!=null)UnityEngine.Object.Destroy(o);objects.Clear();outgoing.Clear();pendingSpawns.Clear();pending.Clear();}
 }
 [HarmonyPatch(typeof(ArtilleryBattery),"SendFireMissionOnCall")]static class SupportRequestPatch {
  static bool Prefix(ArtilleryBattery __instance,Vector3 __0,IndirectFireMunitionType __1,ref bool __result){var s=SupportSync.Current;if(!DamageSync.IsGuest)return true;__result=s!=null&&s.Request(__instance,__0,__1);return false;}
  static void Postfix(ArtilleryBattery __instance,Vector3 __0,IndirectFireMunitionType __1,bool __result){var s=SupportSync.Current;if(__result&&s!=null&&s.Host)s.Accepted(__instance,__0,__1);}
 }
 [HarmonyPatch(typeof(ArtilleryBattery),"DoUpdate")]static class SupportUpdateGuard {static bool Prefix(){return !DamageSync.IsGuest;}}
 [HarmonyPatch(typeof(ArtilleryBattery),"DoSingleShot")]static class SupportSpawnPatch {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){foreach(var c in instructions){var m=c.operand as MethodInfo;if(c.opcode==OpCodes.Call&&m!=null&&m.DeclaringType==typeof(UnityEngine.Object)&&m.Name=="Instantiate"&&m.IsGenericMethod&&m.GetGenericArguments()[0]==typeof(GameObject)&&m.GetParameters().Length==1){c.operand=AccessTools.Method(typeof(SupportSync),"SpawnAndCapture");}yield return c;}}
 }
}
