using System;using Steamworks;using UnityEngine;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  bool invitePicker;Vector2 friendScroll;string inviteNotice="";
  void OpenFriendPicker(SteamLink steam){
   invitePicker=true;inviteNotice="Choose a friend to send a Steam invitation.";
   try{GameBridge.Log("INVITE clicked overlayEnabled="+SteamUtils.IsOverlayEnabled());steam.InviteOverlay();}
   catch(Exception e){inviteNotice="Overlay unavailable. You can invite below.";GameBridge.Log("INVITE overlay: "+e.Message);}
  }
  void DrawFriendPicker(){
   if(!invitePicker)return;
   var steam=link as SteamLink;if(steam==null||steam.RoomId==""){invitePicker=false;return;}
   GUI.enabled=true;GUI.Box(new Rect(90,240,860,510),"");
   GUI.Label(new Rect(110,250,680,32),"INVITE A STEAM FRIEND",menuHeading);
   if(GUI.Button(new Rect(855,250,75,32),"CLOSE")){invitePicker=false;return;}
   GUI.Label(new Rect(110,295,800,52),inviteNotice,menuSmall);
   try{
    var count=SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
    friendScroll=GUI.BeginScrollView(new Rect(110,352,820,320),friendScroll,new Rect(0,0,785,Mathf.Max(310,count*42)));
    try{for(int i=0;i<count;i++){
     var id=SteamFriends.GetFriendByIndex(i,EFriendFlags.k_EFriendFlagImmediate);
     GUI.Label(new Rect(5,i*42,600,36),SteamFriends.GetFriendPersonaName(id),menuText);
     if(GUI.Button(new Rect(620,i*42,155,35),"SEND INVITE")){
      bool sent=SteamMatchmaking.InviteUserToLobby(new CSteamID(ulong.Parse(steam.RoomId)),id);
      inviteNotice=sent?"Steam accepted the invitation request. Waiting for your friend.":"Steam could not send the invitation. Try again or share the Room ID.";
      GameBridge.Log("INVITE request accepted="+sent);
     }
    }}finally{GUI.EndScrollView();}
    if(count==0)inviteNotice="No Steam friends found. Share the Room ID instead.";
   }catch(Exception e){inviteNotice="Friend list unavailable. Share the Room ID.";GameBridge.Log("INVITE picker: "+e.Message);}
   if(GUI.Button(new Rect(110,690,810,38),"COPY STEAM ROOM ID")){GUIUtility.systemCopyBuffer=steam.RoomId;inviteNotice="Room ID copied.";}
  }
  bool steamLaunchChecked;
  string steamRoom="",steamNotice="Steam: waiting for GHPC initialization";Callback<GameLobbyJoinRequested_t> steamInvite;float nextSteamCheck;ulong invitedRoom;
  void PumpSteamMenu(){
   try{
    if(SteamAPI.GetHSteamUser().m_HSteamUser==0)return;
    if(steamInvite==null)steamInvite=Callback<GameLobbyJoinRequested_t>.Create(delegate(GameLobbyJoinRequested_t r){invitedRoom=r.m_steamIDLobby.m_SteamID;panel=true;});
    SteamAPI.RunCallbacks();
    if(!steamLaunchChecked){steamLaunchChecked=true;var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="+connect_lobby"){ulong room;if(ulong.TryParse(args[i+1],out room))invitedRoom=room;}}

    if(Time.realtimeSinceStartup>nextSteamCheck){nextSteamCheck=Time.realtimeSinceStartup+3;SteamRelayNetworkStatus_t relay;steamNotice="Steam: "+(SteamUser.BLoggedOn()?"online":"offline")+" | Relay: "+SteamNetworkingUtils.GetRelayNetworkStatus(out relay);}
    if(invitedRoom!=0){if(link!=null){steamNotice="Disconnect before joining another Steam room.";invitedRoom=0;}else{steamRoom=invitedRoom.ToString();invitedRoom=0;Guard(delegate{Start(false,true);});}}
   }catch(Exception e){steamNotice="Steam unavailable: "+e.Message;}
  }
  void DrawSteamMenu(){
   GUI.enabled=link==null;GUILayout.Label(steamNotice);GUILayout.Label("Steam co-op: up to 4 players.");
   GUILayout.BeginHorizontal();GUILayout.Label("Steam Room ID",GUILayout.Width(110));steamRoom=GUILayout.TextField(steamRoom,20);GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();if(GUILayout.Button("Create Steam Room"))Guard(delegate{Start(true,true);});if(GUILayout.Button("Join Steam Room"))Guard(delegate{Start(false,true);});GUILayout.EndHorizontal();
   var steam=link as SteamLink;if(steam!=null&&steam.RoomId!=""){GUI.enabled=true;GUILayout.Label("Steam Room ID: "+steam.RoomId);GUILayout.BeginHorizontal();if(GUILayout.Button("Copy Steam Room ID"))GUIUtility.systemCopyBuffer=steam.RoomId;if(hosting&&GUILayout.Button("Invite Steam Friend"))OpenFriendPicker(steam);GUILayout.EndHorizontal();}
  }
 }
}
