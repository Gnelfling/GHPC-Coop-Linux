using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using GHPC;using GHPC.Player;using GHPC.Mission.Data;
namespace GhpcCoop {public sealed partial class CoopLabMod {
 sealed class SweepCase {public MissionTheaterScriptable Theater;public MissionMetaData Mission;public Faction Side;}
 bool sweepEnabled{get{return Environment.GetCommandLineArgs().Contains("--coop-mission-sweep");}}
 List<SweepCase> sweepCases;int sweepIndex=-1,sweepPhase,sweepStride=1;float sweepSince,sweepNext;DateTime sweepEnd;string sweepRoot;
 string SweepClean(string s){return (s??"").Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');}
 void SweepFinish(string result,string detail){
  var c=sweepCases[sweepIndex];var row=sweepIndex+"\t"+c.Theater.Key+"\t"+c.Mission.MissionSceneReference.Name+"\t"+c.Side+"\t"+result+"\t"+SweepClean(detail)+"\n";
  File.AppendAllText(Path.Combine(sweepRoot,"results.tsv"),row);GameBridge.Log("SWEEP "+row.Trim());
  if(File.Exists(Path.Combine(DataDir,"roster-diagnostic.txt")))File.Copy(Path.Combine(DataDir,"roster-diagnostic.txt"),Path.Combine(sweepRoot,"roster-"+sweepIndex+".tsv"),true);
  Stop();sweepPhase=0;sweepNext=Time.realtimeSinceStartup+2;
 }
 void RunMissionSweep(){
  if(!sweepEnabled||Time.realtimeSinceStartup<35)return;
  try{
   if(sweepCases==null){
    sweepRoot=Path.Combine(DataDir,"mission-sweep");Directory.CreateDirectory(sweepRoot);
    var endFile=Path.Combine(sweepRoot,"deadline.txt");if(File.Exists(endFile))sweepEnd=DateTime.Parse(File.ReadAllText(endFile),null,System.Globalization.DateTimeStyles.RoundtripKind);else{sweepEnd=DateTime.UtcNow.AddHours(1);File.WriteAllText(endFile,sweepEnd.ToString("O"));}
    var workerArg=Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith("--coop-sweep-worker="));
    if(workerArg!=null){int worker=int.Parse(workerArg.Substring("--coop-sweep-worker=".Length));if(worker<0||worker>2)throw new Exception("Invalid sweep worker");sweepStride=3;sweepIndex=worker-3;}
    sweepCases=new List<SweepCase>();
    foreach(var t in Resources.FindObjectsOfTypeAll<MissionTheaterScriptable>().Where(x=>x.Missions!=null).OrderBy(x=>x.Key,StringComparer.Ordinal))foreach(var m in t.Missions.Where(x=>x!=null&&!x.IsCategory).OrderBy(x=>x.MissionSceneReference.Name,StringComparer.Ordinal)){
     var sides=new[]{MissionPolicy.DefaultFaction(m)}.Concat(m.FactionInfo==null?new Faction[0]:m.FactionInfo.Select(x=>x.Allegiance)).Distinct();
     foreach(var side in sides){MissionPolicy.ValidateFaction(m,side);sweepCases.Add(new SweepCase{Theater=t,Mission=m,Side=side});}
    }
    File.WriteAllLines(Path.Combine(sweepRoot,"plan.tsv"),sweepCases.Select((c,i)=>i+"\t"+c.Theater.Key+"\t"+c.Mission.MissionSceneReference.Name+"\t"+c.Side).ToArray());
    if(!File.Exists(Path.Combine(sweepRoot,"results.tsv")))File.WriteAllText(Path.Combine(sweepRoot,"results.tsv"),"Index\tTheater\tMission\tFaction\tResult\tDetail\n");
    var cursor=Path.Combine(sweepRoot,"cursor.txt");if(File.Exists(cursor))sweepIndex=int.Parse(File.ReadAllText(cursor));
    Application.runInBackground=true;GameBridge.Log("SWEEP initialized cases="+sweepCases.Count);
   }
   if(DateTime.UtcNow>=sweepEnd){if(sweepPhase!=0)SweepFinish("TIME_LIMIT","One-hour deadline reached during case");File.WriteAllText(Path.Combine(sweepRoot,"complete.txt"),"Time limit; remaining cases are untested. "+DateTime.UtcNow.ToString("O"));sweepPhase=9;return;}
   if(sweepPhase==9)return;
   if(Time.realtimeSinceStartup<sweepNext)return;
   sweepNext=Time.realtimeSinceStartup+1;File.WriteAllText(Path.Combine(sweepRoot,"heartbeat.txt"),DateTime.UtcNow.ToString("O"));
   if(sweepPhase==0){
    if((sweepIndex+=sweepStride)>=sweepCases.Count){File.WriteAllText(Path.Combine(sweepRoot,"complete.txt"),"All planned host-creation cases attempted. "+DateTime.UtcNow.ToString("O"));sweepPhase=9;return;}
    File.WriteAllText(Path.Combine(sweepRoot,"cursor.txt"),sweepIndex.ToString());
    Stop();MissionChoices.Reset();var c=sweepCases[sweepIndex];var sc=UnityEngine.Object.FindObjectOfType<SceneController>();if(sc==null)throw new Exception("SceneController missing");
    terrainName=c.Theater.TerrainSceneReference.Name;sceneReadyAt=0;needsReload=false;int terrain=UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(c.Theater.TerrainSceneReference.Path);
    if(terrain<0)throw new Exception("Terrain absent");
    GHPC.State.PersistentDataManager.PersistMissionInfo(new GHPC.State.Data.MissionInfoData("Host-creation audit",c.Mission.MissionName),c.Mission,c.Theater.Key);
    GHPC.Mission.DynamicMissionComposer.CampaignMode=false;GHPC.Mission.DynamicMissionComposer.DynamicMission=c.Mission.IsFlexMission;GHPC.Mission.DynamicMissionComposer.NeedsFlexMissionSetup=c.Mission.IsFlexMission;GHPC.Mission.DynamicMissionComposer.CurrentMissionNameKey=c.Mission.MissionSceneReference.Name;
    SceneController.IsDaytime=c.Mission.IsDefaultDayMission;SceneController.AllowTimeChoice=false;SceneController.TargetSpawningFaction=c.Side;
    sweepPhase=1;sweepSince=Time.realtimeSinceStartup;GameBridge.Log("SWEEP loading index="+sweepIndex+" scene="+c.Mission.MissionSceneReference.Name+" side="+c.Side);
    sc.LoadSceneByMission(terrain+","+c.Mission.MissionSceneReference.Name);return;
   }
   if(sweepPhase==1){
    if(Time.realtimeSinceStartup-sweepSince>180){SweepFinish("LOAD_TIMEOUT","Mission did not reach controllable state");return;}
    var p=PlayerInput.Instance;if(p==null||!p.IsInitialized||p.CurrentPlayerUnit==null||sceneReadyAt<=0||Time.realtimeSinceStartup<sceneReadyAt||p.CurrentPlayerUnit.gameObject.scene.name!=terrainName)return;
    Start(true,true);sweepPhase=2;sweepSince=Time.realtimeSinceStartup;return;
   }
   if(sweepPhase==2){
    if(multiRoom==null){SweepFinish("ROOM_FAILED",status);return;}multiRoom.Pump();if(!String.IsNullOrEmpty(multiRoom.Error))throw new Exception(multiRoom.Error);
    if(multiRoom.RoomId!=""&&multiRoom.Connected&&Time.realtimeSinceStartup-sweepSince>=3){SweepFinish("HOST_ROOM_PASS","units="+game.Vehicles.Count+" capacity="+seats.Capacity+" roster="+game.Roster+"; guest join NOT tested");return;}
    if(Time.realtimeSinceStartup-sweepSince>30)SweepFinish("ROOM_TIMEOUT","Steam lobby/listener not ready");
   }
  }catch(Exception e){if(sweepCases!=null&&sweepIndex>=0&&sweepIndex<sweepCases.Count)SweepFinish("FAIL",e.ToString());else{GameBridge.Log("SWEEP setup failure "+e);sweepPhase=9;}}
 }
}}
