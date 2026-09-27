using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using Steamworks;using GHPC.Player;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  const string PlateKey="coop_nameplates_v1";
  bool platesEnabled=true,selfPlateEnabled=true,platesLoaded;string selfPlateName="";float nextPlates,plateToast;string publishedPlates="";ulong plateLobby;
  readonly Dictionary<string,string> plateNames=new Dictionary<string,string>();
  GUIStyle plateStyle;
  void TogglePlates(){
   platesEnabled=!platesEnabled;plateToast=Time.realtimeSinceStartup+2;
   try{File.WriteAllText(Path.Combine(DataDir,"nameplates.txt"),platesEnabled?"on":"off");}
   catch(Exception e){GameBridge.Log("NAMEPLATES setting not saved: "+e.Message);}
   GameBridge.Log("NAMEPLATES "+(platesEnabled?"ON":"OFF"));
  }
  void ToggleSelfPlate(){
   selfPlateEnabled=!selfPlateEnabled;
   try{File.WriteAllText(Path.Combine(DataDir,"nameplates-self.txt"),selfPlateEnabled?"on":"off");}catch(Exception e){GameBridge.Log("NAMEPLATES self setting not saved: "+e.Message);}
   GameBridge.Log("NAMEPLATES SELF "+(selfPlateEnabled?"ON":"OFF"));
  }
  void UpdateNameplates(){
   try{
    if(!platesLoaded){platesLoaded=true;var p=Path.Combine(DataDir,"nameplates.txt");if(File.Exists(p))platesEnabled=File.ReadAllText(p).Trim()!="off";p=Path.Combine(DataDir,"nameplates-self.txt");if(File.Exists(p))selfPlateEnabled=File.ReadAllText(p).Trim()!="off";}
    if(Input.GetKeyDown(KeyCode.F9)){if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))ToggleSelfPlate();else TogglePlates();}
    if(Time.realtimeSinceStartup<nextPlates)return;nextPlates=Time.realtimeSinceStartup+.5f;
    selfPlateName=LocalDisplayName();plateNames.Clear();
    if(directMode&&link!=null){if(hosting&&seats!=null)ReceivePlayerNames(RoomNameRows());foreach(var pair in roomPlayerNames)if(pair.Key!=game.LocalId&&(hosting||occupiedVehicles.Contains(pair.Key)))plateNames[pair.Key]=pair.Value;return;}
    var steam=link as SteamLink;ulong room;
    if(steam==null||!ulong.TryParse(steam.RoomId,out room)||room==0){plateLobby=0;publishedPlates="";return;}
    if(plateLobby!=room){plateLobby=room;publishedPlates="";}
    var lobby=new CSteamID(room);var local=SteamUser.GetSteamID();
    // Only the Steam lobby owner writes the vehicle-to-player mapping. No wire protocol changes.
    string data;
    if(hosting&&multiRoom!=null&&seats!=null){
     var rows=new List<string>{game.LocalId+":"+local.m_SteamID};
     foreach(var peer in guests.Where(x=>x.Claimed)){
      var sp=peer.Link as SteamPeer;var vehicle=seats.Vehicle(peer.Id);
      if(sp!=null&&sp.Connected&&!sp.Closed&&!String.IsNullOrEmpty(vehicle))rows.Add(vehicle+":"+sp.Identity.m_SteamID);
     }
     data=String.Join("\n",rows.ToArray());
     if(data!=publishedPlates&&SteamMatchmaking.SetLobbyData(lobby,PlateKey,data))publishedPlates=data;
    }else{
     if(!claimed)return;
     data=SteamMatchmaking.GetLobbyData(lobby,PlateKey);
     // An older host can still be labelled using its authenticated assignment.
     if(String.IsNullOrEmpty(data)&&!String.IsNullOrEmpty(remote))data=remote+":"+SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
    }
    if(data.Length>1024)return;
    var members=new HashSet<ulong>();int count=Math.Min(4,SteamMatchmaking.GetNumLobbyMembers(lobby));
    for(int i=0;i<count;i++)members.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby,i).m_SteamID);
    var seen=new HashSet<ulong>();
    foreach(var row in data.Split('\n').Take(4)){
     var parts=row.Split(':');ulong id;
     if(parts.Length!=2||!ulong.TryParse(parts[1],out id)||id==local.m_SteamID||!members.Contains(id)||!seen.Add(id))continue;
     if(!game.Vehicles.ContainsKey(parts[0])||parts[0]==game.LocalId)continue;
     if(!hosting&&!occupiedVehicles.Contains(parts[0]))continue;
     var name=SteamFriends.GetFriendPersonaName(new CSteamID(id));
     name=new string((name??"Player").Where(c=>!Char.IsControl(c)&&Char.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.Format).Take(32).ToArray());
     plateNames[parts[0]]=String.IsNullOrWhiteSpace(name)?"Player":name;
    }
   }catch(Exception e){plateNames.Clear();nextPlates=Time.realtimeSinceStartup+5;GameBridge.Log("NAMEPLATES unavailable: "+e.Message);}
  }
  void DrawNameplates(){
   if(Event.current.type!=EventType.Repaint)return;
   var old=GUI.color;var matrix=GUI.matrix;var content=GUI.contentColor;
   try{
    GUI.matrix=Matrix4x4.identity;GUI.color=GUI.contentColor=Color.white;
    if(plateStyle==null)plateStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,richText=false,wordWrap=false,fontSize=18};
    if(Time.realtimeSinceStartup<plateToast){plateStyle.normal.textColor=Color.cyan;GUI.Label(new Rect(Screen.width-270,Screen.height-55,250,30),"F9  NAMEPLATES: "+(platesEnabled?"ON":"OFF"),plateStyle);}
    if(!platesEnabled||panel||game==null)return;
    var player=PlayerInput.Instance;var cam=GHPC.Camera.CameraManager.MainCam;
    if(player==null||!player.IsInitialized||player.CurrentPlayerUnit==null||cam==null||!cam.isActiveAndEnabled)return;
    var labels=new List<KeyValuePair<GHPC.Unit,string>>();
    if(link!=null)foreach(var entry in plateNames){VehicleRecord record;if(game.Vehicles.TryGetValue(entry.Key,out record))labels.Add(new KeyValuePair<GHPC.Unit,string>(record.Unit,entry.Value));}
    if(selfPlateEnabled&&!String.IsNullOrEmpty(selfPlateName))labels.Add(new KeyValuePair<GHPC.Unit,string>(player.CurrentPlayerUnit,selfPlateName));
    foreach(var pair in labels){
     var u=pair.Key;
     if(u==null||!u.gameObject.activeInHierarchy||u.Destroyed||u.Abandoned||u.UnitIncapacitated||u.Allegiance!=player.CurrentPlayerUnit.Allegiance)continue;
     var point=u.transform.position+Vector3.up*3.2f;
     float distance=Vector3.Distance(cam.transform.position,point);if(distance>1500)continue;
     var screen=cam.WorldToScreenPoint(point);if(screen.z<=0||screen.x<15||screen.x>Screen.width-15||screen.y<30||screen.y>Screen.height-15)continue;
     // Cast to hull height, ignoring observer and target colliders. Terrain/buildings hide labels.
     var end=u.transform.position+Vector3.up*1.5f;var delta=end-cam.transform.position;bool hidden=false;
     foreach(var hit in Physics.RaycastAll(cam.transform.position,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){
      if(hit.transform.IsChildOf(u.transform)||hit.transform.IsChildOf(player.CurrentPlayerUnit.transform))continue;hidden=true;break;
     }
     if(hidden)continue;
     float scale=Mathf.Clamp(Screen.height/1080f,.75f,1.5f);float t=Mathf.Clamp01(distance/1500);
     plateStyle.fontSize=Mathf.RoundToInt(Mathf.Lerp(19,13,t)*scale);float alpha=Mathf.Lerp(1,.4f,t);
     var rect=new Rect(screen.x-150,Screen.height-screen.y-32*scale,300,30*scale);
     plateStyle.normal.textColor=new Color(0,0,0,.9f*alpha);GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),pair.Value,plateStyle);
     plateStyle.normal.textColor=new Color(.35f,.85f,1,alpha);GUI.Label(rect,pair.Value,plateStyle);
    }
   }catch(Exception e){plateNames.Clear();GameBridge.Log("NAMEPLATES render skipped: "+e.Message);}
   finally{GUI.color=old;GUI.matrix=matrix;GUI.contentColor=content;}
  }
 }
}
