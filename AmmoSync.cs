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
  public static AmmoState Capture(WeaponSystem w){var f=w.Feed as AmmoFeed;if(f==null)return new AmmoState();return new AmmoState{ClipType=f.ReadyRack==null?-1:Array.IndexOf(f.ReadyRack.ClipTypes,f.Reloading?(AmmoType.AmmoClip)Get(f,"_queuedClipTypeLockedIn"):f.LoadedClipType),Breech=f.AmmoTypeInBreech==null?"":f.AmmoTypeInBreech.Name,Reloading=f.Reloading,Cycling=f.Cycling,Clip=f.CurrentClipRemainingCount,Reserve=f.ReadyRack!=null&&f.LoadedClipType!=null?f.ReserveCount:0,Stage=(int)Get(f,"_clipFeedStage"),CycleStage=(int)Get(f,"_roundFeedStage"),Time=(float)Get(f,"_clipFeedTime"),CycleTime=(float)Get(f,"_roundFeedTime")};}
  static void Stage(AmmoFeed f,int index,bool clip){var stages=clip?f.ClipReloadStages:f.RoundCycleStages;if(index<0||index>=stages.Length)return;var stage=stages[index];GameBridge.Log("AUDIO stage "+f.name+" clip="+clip+" index="+index+" event="+stage.StageAudioEvent+" source="+(stage.StageAudio==null?"none":("enabled="+stage.StageAudio.enabled+" active="+stage.StageAudio.gameObject.activeInHierarchy)));Call(f,"DoReloadStage",stage,clip);if(clip)Event(f,"ReloadStageStarted",index);}
  public static void Apply(WeaponSystem w,AmmoState s){
   var f=w.Feed as AmmoFeed;if(f==null)return;AmmoState old;bool known=States.TryGetValue(f,out old);States[f]=s;
   if(Types.Count==0)foreach(var c in Resources.FindObjectsOfTypeAll<GHPC.Weaponry.AmmoCodexScriptable>())if(c.AmmoType!=null)Types[c.AmmoType.Name]=c.AmmoType;
   AmmoType a=null;if(s.Breech!=""&&!Types.TryGetValue(s.Breech,out a))throw new InvalidOperationException("Unknown replicated ammo: "+s.Breech);
   if(a!=null)Last[f]=a;
   var clip=s.ClipType>=0&&f.ReadyRack!=null&&s.ClipType<f.ReadyRack.ClipTypes.Length?f.ReadyRack.ClipTypes[s.ClipType]:null;
   if(clip!=null){Set(f,"_queuedClipTypeLockedIn",clip);if(!s.Reloading)Set(f,"<LoadedClipType>k__BackingField",clip);}
   Set(f,"<AmmoTypeInBreech>k__BackingField",a);Set(f,"<Reloading>k__BackingField",s.Reloading);Set(f,"<Cycling>k__BackingField",s.Cycling);
   Set(f,"_clipFeedTime",s.Time);Set(f,"_roundFeedTime",s.CycleTime);Set(f,"_clipFeedStage",s.Stage);Set(f,"_roundFeedStage",s.CycleStage);
   if(known&&old.Breech!=s.Breech){if(a!=null)Event(f,"LoadedRoundInBreech",a);else if(s.Clip==0)Event(f,"ClipDepleted");GameBridge.Log("AMMO REPLICA "+w.name+" loaded="+(a!=null)+" reload="+s.Reloading+" reserve="+s.Reserve);}
   if(s.Reloading&&(!known||!old.Reloading)){Call(f,"ResetLiveDurations");Call(f,"StartAutoloaderAudio");if(known&&f.AnnounceReloads&&s.Reserve==1&&clip!=null)Event(f,"LoadingFinalClip",clip);Stage(f,s.Stage,true);GameBridge.Log("AUDIO reload start "+w.name+" stage="+s.Stage);}
   else if(known&&old.Reloading&&(old.Stage!=s.Stage||!s.Reloading)){Event(f,"ReloadStageEnded",old.Stage);if(s.Reloading)Stage(f,s.Stage,true);else {Call(f,"StopAutoloaderAudio");if(clip!=null&&(s.Clip>old.Clip||a!=null)){Event(f,"LoadedNewClip",clip);GameBridge.Log("AUDIO reload complete "+w.name);}}}
   if(s.Cycling&&(!known||!old.Cycling||old.CycleStage!=s.CycleStage))Stage(f,s.CycleStage,false);
  }
  public static void Clear(){foreach(var f in States.Keys)if(f!=null)Call(f,"StopAutoloaderAudio");States.Clear();Types.Clear();Last.Clear();}
 }
 // Remote players must not inherit the native non-player/AI auto-reload shortcut.
 // Round cycling stays native; only loading a new clip requires their R request.
 [HarmonyPatch(typeof(AmmoFeed),"get_AutoReload")] static class RemoteManualReload {
  static bool Prefix(GHPC.Crew.CrewManager ____crewManager,ref bool __result){
   if(____crewManager==null||!GameBridge.IsRemoteCrew(____crewManager))return true;
   __result=false;return false;
  }
 }
 [HarmonyPatch(typeof(AmmoFeed),"Update")] static class ReplicaFeedUpdate {static bool Prefix(){return !GameBridge.ReplicaActive;}}
 [HarmonyPatch(typeof(AmmoFeed),"FeedNewClip")] static class ReplicaFeedClip {static bool Prefix(){return !GameBridge.ReplicaActive;}}
 [HarmonyPatch(typeof(AmmoFeed),"FeedNewRound")] static class ReplicaFeedRound {static bool Prefix(){return !GameBridge.ReplicaActive;}}
 [HarmonyPatch(typeof(AmmoFeed),"get_CurrentClipRemainingCount")] static class ReplicaClipCount {static void Postfix(AmmoFeed __instance,ref int __result){AmmoState s;if(GameBridge.ReplicaActive&&AmmoSync.States.TryGetValue(__instance,out s))__result=s.Clip;}}
 [HarmonyPatch(typeof(AmmoFeed),"get_ReserveCount")] static class ReplicaReserveCount {static void Postfix(AmmoFeed __instance,ref int __result){AmmoState s;if(GameBridge.ReplicaActive&&AmmoSync.States.TryGetValue(__instance,out s))__result=s.Reserve;}}
}

