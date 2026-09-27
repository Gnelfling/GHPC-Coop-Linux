using System;using System.Collections.Generic;using GHPC.Weapons;using UnityEngine;using HarmonyLib;
namespace GhpcCoop {
 public static class AmmoSync {
  public static readonly Dictionary<AmmoFeed,AmmoState> States=new Dictionary<AmmoFeed,AmmoState>();
  static readonly Dictionary<AmmoFeed,AmmoType> Last=new Dictionary<AmmoFeed,AmmoType>();
  public static AmmoType LastAmmo(AmmoFeed f){AmmoType a;return Last.TryGetValue(f,out a)?a:null;}
  static readonly Dictionary<string,AmmoType> Types=new Dictionary<string,AmmoType>();
  static object Get(AmmoFeed f,string n){return AccessTools.Field(typeof(AmmoFeed),n).GetValue(f);}
  static void Set(AmmoFeed f,string n,object v){AccessTools.Field(typeof(AmmoFeed),n).SetValue(f,v);}
  static void Call(AmmoFeed f,string n,params object[] args){AccessTools.Method(typeof(AmmoFeed),n).Invoke(f,args);}
  static void Event(AmmoFeed f,string n,params object[] args){var d=Get(f,n) as Delegate;if(d!=null)d.DynamicInvoke(args);}
  public static AmmoState Capture(WeaponSystem w){var f=w.Feed as AmmoFeed;if(f==null)return new AmmoState();return new AmmoState{Ballistic=w.CurrentAmmoType==null?"":w.CurrentAmmoType.Name,ClipType=f.ReadyRack==null?-1:Array.IndexOf(f.ReadyRack.ClipTypes,f.Reloading?(AmmoType.AmmoClip)Get(f,"_queuedClipTypeLockedIn"):f.LoadedClipType),Breech=f.AmmoTypeInBreech==null?"":f.AmmoTypeInBreech.Name,Reloading=f.Reloading,Cycling=f.Cycling,Clip=f.CurrentClipRemainingCount,Reserve=f.ReadyRack!=null&&f.LoadedClipType!=null?f.ReserveCount:0,Stage=(int)Get(f,"_clipFeedStage"),CycleStage=(int)Get(f,"_roundFeedStage"),Time=(float)Get(f,"_clipFeedTime"),CycleTime=(float)Get(f,"_roundFeedTime")};}
  static void Stage(AmmoFeed f,int index,bool clip){var stages=clip?f.ClipReloadStages:f.RoundCycleStages;if(index<0||index>=stages.Length)return;var stage=stages[index];GameBridge.Log("AUDIO stage "+f.name+" clip="+clip+" index="+index+" event="+stage.StageAudioEvent+" source="+(stage.StageAudio==null?"none":("enabled="+stage.StageAudio.enabled+" active="+stage.StageAudio.gameObject.activeInHierarchy)));Call(f,"DoReloadStage",stage,clip);if(clip)Event(f,"ReloadStageStarted",index);}
  public static void Apply(WeaponSystem w,AmmoState s){
   var f=w.Feed as AmmoFeed;if(f==null)return;AmmoState old;bool known=States.TryGetValue(f,out old);States[f]=s;
   if(Types.Count==0)foreach(var c in Resources.FindObjectsOfTypeAll<GHPC.Weaponry.AmmoCodexScriptable>())if(c.AmmoType!=null)Types[c.AmmoType.Name]=c.AmmoType;
   AmmoType a=null;if(s.Breech!=""&&!Types.TryGetValue(s.Breech,out a))throw new InvalidOperationException("Unknown replicated ammo: "+s.Breech);
   if(a!=null)Last[f]=a;
   AmmoType ballistic=null;
   if(s.Ballistic!=""&&!Types.TryGetValue(s.Ballistic,out ballistic))throw new InvalidOperationException("Unknown ballistic ammo: "+s.Ballistic);
   // A loaded round can be fired between snapshots. Replicate the weapon's last
   // ballistic type independently of whether the breech is currently empty.
   if(ballistic!=null&&w.CurrentAmmoType!=ballistic){
    AccessTools.Field(typeof(WeaponSystem),"<CurrentAmmoType>k__BackingField").SetValue(w,ballistic);
    var changed=AccessTools.Field(typeof(WeaponSystem),"AmmoTypeChanged").GetValue(w) as Action<AmmoType>;
    if(changed!=null)changed(ballistic);
    GameBridge.Log("BALLISTIC SYNC weapon="+w.name+" ammo="+ballistic.Name+" breechEmpty="+(a==null));
   }
   var clip=s.ClipType>=0&&f.ReadyRack!=null&&s.ClipType<f.ReadyRack.ClipTypes.Length?f.ReadyRack.ClipTypes[s.ClipType]:null;
   if(clip!=null){Set(f,"_queuedClipTypeLockedIn",clip);if(!s.Reloading)Set(f,"<LoadedClipType>k__BackingField",clip);}
   Set(f,"<AmmoTypeInBreech>k__BackingField",a);Set(f,"<Reloading>k__BackingField",s.Reloading);Set(f,"<Cycling>k__BackingField",s.Cycling);
   Set(f,"_clipFeedTime",s.Time);Set(f,"_roundFeedTime",s.CycleTime);Set(f,"_clipFeedStage",s.Stage);Set(f,"_roundFeedStage",s.CycleStage);
   // Initial snapshots and stale weapon caches need the same native ammo notification as a reload.
   if(a!=null&&(!known||old.Breech!=s.Breech||w.CurrentAmmoType!=a)){Event(f,"LoadedRoundInBreech",a);}
   if(known&&old.Breech!=s.Breech){if(a==null&&s.Clip==0)Event(f,"ClipDepleted");GameBridge.Log("AMMO REPLICA "+w.name+" loaded="+(a!=null)+" reload="+s.Reloading+" reserve="+s.Reserve);}
   if(s.Reloading&&(!known||!old.Reloading)){Call(f,"ResetLiveDurations");Call(f,"StartAutoloaderAudio");if(known&&f.AnnounceReloads&&s.Reserve==1&&clip!=null)Event(f,"LoadingFinalClip",clip);Stage(f,s.Stage,true);GameBridge.Log("AUDIO reload start "+w.name+" stage="+s.Stage);}
   else if(known&&old.Reloading&&(old.Stage!=s.Stage||!s.Reloading)){Event(f,"ReloadStageEnded",old.Stage);if(s.Reloading)Stage(f,s.Stage,true);else {Call(f,"StopAutoloaderAudio");if(clip!=null&&(s.Clip>old.Clip||a!=null)){Event(f,"LoadedNewClip",clip);GameBridge.Log("AUDIO reload complete "+w.name);}}}
   if(s.Cycling&&(!known||!old.Cycling||old.CycleStage!=s.CycleStage))Stage(f,s.CycleStage,false);
  }
  public static int NextClipIndex(WeaponSystem weapon){
   var feed=weapon==null?null:weapon.Feed;
   return feed==null||feed.ReadyRack==null?-1:Array.IndexOf(feed.ReadyRack.ClipTypes,feed.QueuedClipType);
  }
  public static void ApplyNextClip(IEnumerable<WeaponSystemInfo> weapons,int role,string text){
   if(String.IsNullOrEmpty(text))return;
   int marker=text.IndexOf(";clip:",StringComparison.Ordinal),index;
   if(marker<0)return;
   if(!int.TryParse(text.Substring(marker+6),out index)||index< -1)throw new InvalidOperationException("Invalid next ammo selection");
   if(index== -1)return;
   foreach(var info in weapons){
    if(info==null||(int)info.Role!=role||info.Weapon==null)continue; var weapon=info.Weapon;
    var feed=weapon.Feed;
    if(feed==null||feed.ReadyRack==null||index>=feed.ReadyRack.ClipTypes.Length)throw new InvalidOperationException("Next ammo selection outside weapon rack");
    var clip=feed.ReadyRack.ClipTypes[index];
    if(feed.QueuedClipType!=clip){feed.SetNextClipType(clip);GameBridge.Log("AMMO NEXT role="+role+" clip="+index);}
    return;
   }
  }
  public static void Clear(){foreach(var f in States.Keys)if(f!=null)Call(f,"StopAutoloaderAudio");States.Clear();Types.Clear();Last.Clear();RemoteReloadSettings.Preferences.Clear();}
 }
 static class RemoteReloadSettings { public static readonly Dictionary<GHPC.Crew.CrewManager,int> Preferences=new Dictionary<GHPC.Crew.CrewManager,int>(); }
 // Remote players must not inherit the native non-player/AI auto-reload shortcut.
 // Remote feeds follow their owner's reload preference and native forced modes.
 [HarmonyPatch(typeof(AmmoFeed),"get_AutoReload")] static class RemoteManualReload {
  static bool Prefix(AmmoFeed __instance,GHPC.Crew.CrewManager ____crewManager,ref bool __result){
   if(____crewManager==null)return true;
   if(GameBridge.IsRemoteCrew(____crewManager)){
    // Native AutoReload unconditionally returns true for AI-owned units, so
    // remote vehicles need the player rules rather than that AI shortcut.
    int preference; if(!RemoteReloadSettings.Preferences.TryGetValue(____crewManager,out preference)) preference=0;
    __result=ReloadPolicy.Automatic((int)__instance.ReloadMode,preference,__instance.AutoReloadSwitchedOn);
    return false;
   }
   return true; // Local players retain the native game reload setting.
  }
 }
 [HarmonyPatch(typeof(AmmoFeed),"Update")] static class ReplicaFeedUpdate {static bool Prefix(){return !GameBridge.ReplicaActive;}}
 [HarmonyPatch(typeof(AmmoFeed),"FeedNewClip")] static class ReplicaFeedClip {static bool Prefix(){return !GameBridge.ReplicaActive;}}
 [HarmonyPatch(typeof(AmmoFeed),"FeedNewRound")] static class ReplicaFeedRound {static bool Prefix(){return !GameBridge.ReplicaActive;}}
 [HarmonyPatch(typeof(AmmoFeed),"get_CurrentClipRemainingCount")] static class ReplicaClipCount {static void Postfix(AmmoFeed __instance,ref int __result){AmmoState s;if(GameBridge.ReplicaActive&&AmmoSync.States.TryGetValue(__instance,out s))__result=s.Clip;}}
 [HarmonyPatch(typeof(AmmoFeed),"get_ReserveCount")] static class ReplicaReserveCount {static void Postfix(AmmoFeed __instance,ref int __result){AmmoState s;if(GameBridge.ReplicaActive&&AmmoSync.States.TryGetValue(__instance,out s))__result=s.Reserve;}}
}



