using System;using System.IO;using System.Linq;using GHPC;using GHPC.Player;using UnityEngine;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  bool menuJoin,awaitingRoom,loadingGuest,lobbySent;string offeredGuest="";Message missionOffer;float lobbyNextPing,loadingSince;DateTime launchUtc=DateTime.UtcNow;
  Message MakeMissionOffer(Message reply){
   var state=GHPC.State.PersistentDataManager.GetStateData();if(state==null||state.MetaData==null)throw new InvalidOperationException("Host mission information unavailable");
   var choices=game.PlatoonChoices().ToArray();if(choices.Length==0)throw new InvalidOperationException("No surviving AI vehicle in your platoon. Select a platoon with a free vehicle.");
   offeredGuest=choices[0];reply.Unit=offeredGuest;reply.Text=state.TheaterKey+"\n"+state.MetaData.MissionSceneReference.Name+"\n"+MissionConfiguration.Export(state.MetaData.MissionSceneReference.Name)+"\n"+MissionChoices.Export();reply.Role=(int)game.Vehicles[game.LocalId].Unit.Allegiance;reply.Fire=SceneController.IsDaytime;
   status="Guest is loading the mission. Reserved: "+game.Vehicles[offeredGuest].Unit.FriendlyName;GameBridge.Log("ROOM OFFER mission="+reply.Text.Replace('\n','/')+" platoon="+(game.Vehicles[game.LocalId].Unit.Platoon==null?"ungrouped":game.Vehicles[game.LocalId].Unit.Platoon.Name)+" vehicle="+game.Vehicles[offeredGuest].Unit.FriendlyName);return reply;
  }
  void LoadHostMission(Message offer){
   if(!awaitingRoom||loadingGuest||offer.Build!=build||String.IsNullOrEmpty(offer.Unit))throw new InvalidOperationException("Unexpected mission offer");
   var parts=offer.Text.Split(new[]{'\n'},4);if(parts.Length!=4||offer.Role<0||offer.Role>3)throw new InvalidOperationException("Invalid host mission settings");
   var theater=Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>().FirstOrDefault(t=>t.Key==parts[0]);
   if(theater==null||theater.Missions==null)throw new InvalidOperationException("Host theater is not installed");
   var meta=theater.Missions.FirstOrDefault(m=>m!=null&&!m.IsCategory&&m.MissionSceneReference.Name==parts[1]);if(meta==null)throw new InvalidOperationException("Host mission is not installed");
   MissionPolicy.ValidateFaction(meta,(Faction)offer.Role);
   var sc=UnityEngine.Object.FindObjectOfType<SceneController>();if(sc==null)throw new InvalidOperationException("Mission loader is not ready");
   int terrain=UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(theater.TerrainSceneReference.Path);if(terrain<0)throw new InvalidOperationException("Host terrain is not installed");
   MissionConfiguration.Apply(parts[1],parts[2]);MissionChoices.Import(parts[3]);missionOffer=offer;offeredGuest=offer.Unit;terrainName=theater.TerrainSceneReference.Name;sceneReadyAt=0;loadingGuest=true;awaitingRoom=false;loadingSince=Time.realtimeSinceStartup;
   game.Dispose();game=new GameBridge();needsReload=false;DamageSync.Loading=true;
   GHPC.State.PersistentDataManager.PersistMissionInfo(new GHPC.State.Data.MissionInfoData(meta.MissionName,"Co-op host mission"),meta,theater.Key);
   GHPC.Mission.DynamicMissionComposer.CampaignMode=false;GHPC.Mission.DynamicMissionComposer.DynamicMission=meta.IsFlexMission;GHPC.Mission.DynamicMissionComposer.NeedsFlexMissionSetup=meta.IsFlexMission;GHPC.Mission.DynamicMissionComposer.CurrentMissionNameKey=parts[1];
   SceneController.IsDaytime=offer.Fire;SceneController.AllowTimeChoice=false;SceneController.TargetSpawningFaction=(Faction)offer.Role;
   status="Loading host mission: "+meta.MissionName;GameBridge.Log("ROOM LOAD "+offer.Text.Replace('\n','/')+" faction="+offer.Role+" day="+offer.Fire);sc.LoadSceneByMission(terrain+","+parts[1]);
  }
  void UpdateGuestLobby(float now){
   if(!lobbySent){link.Send(new Message{Kind=Kind.JoinRoom,Token=token,Build=build,Roster="names-v1",Text=LocalDisplayName()});lobbySent=true;status="Checking room code...";}
   Message m;int count=0;while(count++<32&&link.TryRead(out m)){lastReceive=now;if(m.Kind==Kind.Error)throw new IOException(m.Text);if(m.Kind==Kind.Mission)LoadHostMission(m);else if(m.Kind!=Kind.Ping)throw new IOException("Unexpected room message");}
   if(now-lastReceive>180)throw new IOException("Host did not answer during mission loading");
   if(now>=lobbyNextPing){lobbyNextPing=now+1;link.Send(new Message{Kind=Kind.Ping,Sequence=++seq});}
   if(!loadingGuest)return;if(now-loadingSince>180)throw new IOException("Host mission loading timed out");
   var p=PlayerInput.Instance;if(p==null||!p.IsInitialized||p.CurrentPlayerUnit==null||p.CurrentPlayerUnit.gameObject.scene.name!=terrainName||sceneReadyAt<=0||now<sceneReadyAt)return;
   if(MissionChoices.Error!="")throw new IOException(MissionChoices.Error);SceneController.IsDaytime=missionOffer.Fire;game.Capture();VehicleRecord chosen;
   if(!Wire.SameMissionWorld(game.World,missionOffer.World))throw new IOException("Mission world differs: host="+missionOffer.World+" guest="+game.World);
   if(game.Roster!=missionOffer.Roster)throw new IOException("Vehicle roster differs after mission load ("+game.Vehicles.Count+" guest vehicles). Compare UserData/GhpcCoop/roster-diagnostic.txt on both PCs.");
   if(!game.Vehicles.TryGetValue(offeredGuest,out chosen))throw new IOException("Reserved host vehicle is missing from the guest mission: "+offeredGuest);
   // Preserve the room's original handshake token after independently validating
   // mission/terrain, complete roster, and the reserved vehicle. This also allows
   // a fixed guest to join an already-running wire-15 host without restarting it.
   if(game.World!=missionOffer.World)GameBridge.Log("ROOM day/night changed; mission and roster verified host="+missionOffer.World+" guest="+game.World);
   game.World=missionOffer.World;
   MissionChoices.Reset();p.SetPlayerUnit(chosen.Unit);game.LocalId=offeredGuest;loadingGuest=false;hello=true;
   link.Send(new Message{Kind=Kind.Hello,Token=token,Build=build,World=game.World,Roster=game.Roster});status="Taking over your platoon vehicle...";GameBridge.Log("ROOM LOADED vehicle="+chosen.Unit.FriendlyName);
  }
 }
}


