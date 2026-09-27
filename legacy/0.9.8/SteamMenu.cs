using System;using Steamworks;using UnityEngine;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
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
   GUI.enabled=link==null;GUILayout.Label(steamNotice);GUILayout.Label("Direct IP: up to 4 players. Steam test mode: 2 players.");
   GUILayout.BeginHorizontal();GUILayout.Label("Steam Room ID",GUILayout.Width(110));steamRoom=GUILayout.TextField(steamRoom,20);GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();if(GUILayout.Button("Create Steam Room"))Guard(delegate{Start(true,true);});if(GUILayout.Button("Join Steam Room"))Guard(delegate{Start(false,true);});GUILayout.EndHorizontal();
   var steam=link as SteamLink;if(steam!=null&&steam.RoomId!=""){GUI.enabled=true;GUILayout.Label("Steam Room ID: "+steam.RoomId);GUILayout.BeginHorizontal();if(GUILayout.Button("Copy Steam Room ID"))GUIUtility.systemCopyBuffer=steam.RoomId;if(hosting&&GUILayout.Button("Invite Steam Friend"))Guard(steam.InviteOverlay);GUILayout.EndHorizontal();}
  }
 }
}
