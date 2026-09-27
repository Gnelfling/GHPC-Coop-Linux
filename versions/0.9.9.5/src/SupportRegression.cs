using System;using System.Linq;using UnityEngine;using GHPC.Weapons.Artillery;using HarmonyLib;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  bool supportCheckStarted,supportCheckDone;float supportCheckTime;SupportSync checkSupport;TracerSync checkTracer;int supportEffects,traceEffects;
  void RunSupportRegression(){
   if(!Environment.GetCommandLineArgs().Contains("--coop-supportcheck")||supportCheckDone)return;
   var player=GHPC.Player.PlayerInput.Instance;if(player==null||player.CurrentPlayerUnit==null||sceneReadyAt<=0||Time.realtimeSinceStartup<sceneReadyAt)return;
   if(!supportCheckStarted){
    supportCheckStarted=true;supportCheckTime=Time.realtimeSinceStartup;checkSupport=new SupportSync(true){Ready=true};checkTracer=new TracerSync(true){Ready=true};
    var fm=FireMissionManager.Instance;var batteries=fm==null?new ArtilleryBattery[0]:fm.BlueArtilleryBatteries??new ArtilleryBattery[0];
    GameBridge.Log("SUPPORT TEST batteries="+batteries.Length+" states="+checkSupport.Capture().Length);
    var smoke=batteries.FirstOrDefault(b=>b.HasMunitionType(IndirectFireMunitionType.Smoke));
    if(smoke!=null){AccessTools.Field(typeof(ArtilleryBattery),"_onCallImpactDelay").SetValue(smoke,1f);AccessTools.Field(typeof(ArtilleryBattery),"_shots").SetValue(smoke,1);var target=player.CurrentPlayerUnit.RootTransform.position+player.CurrentPlayerUnit.RootTransform.forward*80;bool accepted=fm.SendFireMissionOnCall(target,smoke,IndirectFireMunitionType.Smoke).IsSuccess;GameBridge.Log("SUPPORT TEST smoke accepted="+accepted);}
    else GameBridge.Log("SUPPORT TEST no smoke battery");
    var origin=player.CurrentPlayerUnit.RootTransform.position+Vector3.up*3;checkTracer.Receive(new[]{new TracerState{Id=900000,Visual=1,X=origin.x,Y=origin.y,Z=origin.z,EndX=origin.x+50,EndY=origin.y,EndZ=origin.z,Duration=.1f},new TracerState{Id=900001,Visual=5,X=origin.x,Y=origin.y+2,Z=origin.z,EndX=origin.x+50,EndY=origin.y+2,EndZ=origin.z,Duration=.1f}});
   }
   checkTracer.Render();var traces=checkTracer.Drain();traceEffects+=traces.Length;
   foreach(var m in checkSupport.Drain()){if(m.Kind==Kind.SupportVisual){supportEffects++;checkSupport.Visual(m);}else if(m.Kind==Kind.SupportResult)GameBridge.Log("SUPPORT TEST accepted event="+m.SupportId);}
   if(Time.realtimeSinceStartup-supportCheckTime>45){supportCheckDone=true;GameBridge.Log("SUPPORT TEST finished smokeVisualEvents="+supportEffects+" capturedTracerSegments="+traceEffects);checkSupport.Dispose();checkTracer.Dispose();}
  }
 }
}
