using System;using Steamworks;using UnityEngine;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  bool invitePicker;Vector2 friendScroll;string inviteNotice="";
  void OpenFriendPicker(SteamLink steam){
   invitePicker=true;inviteNotice="Choose a friend to send a Steam invitation.";
   try{GameBridge.Log("INVITE clicked overlayEnabled="+SteamUtils.IsOverlayEnabled());steam.InviteOverlay();}
   catch(Exception e){inviteNotice="Overlay unavailable. You can invite below.";GameBridge.Log("INVITE overlay: "+e.Message);}
  }
  readonly System.Collections.Generic.Dictionary<ulong,FriendPortrait> portraits=new System.Collections.Generic.Dictionary<ulong,FriendPortrait>();
  sealed class FriendPortrait {public Texture2D Texture;public int Handle;public float Retry,Used;}
  Texture2D FriendAvatar(CSteamID id){
   FriendPortrait entry;float now=Time.realtimeSinceStartup;
   if(!portraits.TryGetValue(id.m_SteamID,out entry)){
    if(portraits.Count>=64){ulong oldest=0;float age=float.MaxValue;foreach(var pair in portraits)if(pair.Value.Used<age){age=pair.Value.Used;oldest=pair.Key;}var old=portraits[oldest];if(old.Texture!=null)UnityEngine.Object.Destroy(old.Texture);portraits.Remove(oldest);}
    entry=new FriendPortrait();portraits.Add(id.m_SteamID,entry);
   }
   entry.Used=now;if(now<entry.Retry)return entry.Texture;entry.Retry=now+5;
   try{
    int handle=SteamFriends.GetMediumFriendAvatar(id);
    if(handle<=0){if(entry.Texture!=null)UnityEngine.Object.Destroy(entry.Texture);entry.Texture=null;entry.Handle=handle;return null;}
    if(handle==entry.Handle&&entry.Texture!=null)return entry.Texture;
    uint w,h;if(!SteamUtils.GetImageSize(handle,out w,out h)||w==0||h==0||w>256||h>256)return entry.Texture;
    var bytes=new byte[checked((int)(w*h*4))];if(!SteamUtils.GetImageRGBA(handle,bytes,bytes.Length))return entry.Texture;
    // Steam RGBA rows are top-down; Unity texture rows are bottom-up.
    int stride=(int)w*4;var flipped=new byte[bytes.Length];for(int y=0;y<(int)h;y++)Array.Copy(bytes,y*stride,flipped,((int)h-1-y)*stride,stride);
    var texture=new Texture2D((int)w,(int)h,TextureFormat.RGBA32,false);texture.hideFlags=HideFlags.HideAndDontSave;texture.LoadRawTextureData(flipped);texture.Apply(false,true);
    if(entry.Texture!=null)UnityEngine.Object.Destroy(entry.Texture);entry.Texture=texture;entry.Handle=handle;
   }catch {entry.Retry=now+15;}
   return entry.Texture;
  }
  void ClearFriendPortraits(){foreach(var e in portraits.Values)if(e.Texture!=null)UnityEngine.Object.Destroy(e.Texture);portraits.Clear();}
  static string FriendPresence(EPersonaState state){switch(state){case EPersonaState.k_EPersonaStateOnline:return "ONLINE";case EPersonaState.k_EPersonaStateBusy:return "BUSY";case EPersonaState.k_EPersonaStateAway:return "AWAY";case EPersonaState.k_EPersonaStateSnooze:return "SNOOZE";case EPersonaState.k_EPersonaStateLookingToTrade:return "LOOKING TO TRADE";case EPersonaState.k_EPersonaStateLookingToPlay:return "LOOKING TO PLAY";default:return "OFFLINE";}}
  void DrawFriendPicker(){
   if(!invitePicker)return;
   var steam=link as SteamLink;if(steam==null||steam.RoomId==""){invitePicker=false;return;}
   GUI.enabled=true;GUI.DrawTexture(new Rect(90,230,860,525),menuField);
   GUI.DrawTexture(new Rect(90,230,860,4),menuPrimary);
   GUI.Label(new Rect(114,250,680,32),"INVITE YOUR CREW",menuHeading);
   if(GUI.Button(new Rect(840,249,86,32),"CLOSE",menuButton)){invitePicker=false;return;}
   GUI.Label(new Rect(114,291,790,48),inviteNotice,menuSmall);
   var nameStyle=new GUIStyle(menuText){wordWrap=false,clipping=TextClipping.Clip,fontSize=17};
   var presenceStyle=new GUIStyle(menuSmall){fontSize=12,wordWrap=false};
   try{
    int count=SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
    friendScroll=GUI.BeginScrollView(new Rect(114,348,812,326),friendScroll,new Rect(0,0,787,Mathf.Max(326,count*68)));
    try{for(int i=0;i<count;i++){
     float y=i*68;if(y+64<friendScroll.y||y>friendScroll.y+326)continue;
     var id=SteamFriends.GetFriendByIndex(i,EFriendFlags.k_EFriendFlagImmediate);var state=SteamFriends.GetFriendPersonaState(id);
     GUI.DrawTexture(new Rect(0,y,782,62),menuIdle);
     var avatar=FriendAvatar(id);GUI.DrawTexture(new Rect(9,y+7,48,48),avatar!=null?avatar:menuField,ScaleMode.ScaleToFit);
     if(avatar==null)GUI.Label(new Rect(25,y+17,26,28),"?",menuHeading);
     string name=SteamFriends.GetFriendPersonaName(id).Replace("\n"," ").Replace("\r"," ");
     var info=new System.Globalization.StringInfo(name);int n=info.LengthInTextElements;
     if(n>42)name=info.SubstringByTextElements(0,42)+"…";
     GUI.Label(new Rect(72,y+8,475,27),new GUIContent(name,SteamFriends.GetFriendPersonaName(id)),nameStyle);
     presenceStyle.normal.textColor=state==EPersonaState.k_EPersonaStateOffline?MenuMuted:MenuAccent;
     GUI.Label(new Rect(73,y+35,470,21),FriendPresence(state),presenceStyle);
     if(GUI.Button(new Rect(591,y+12,179,38),"SEND INVITE",menuPrimaryButton)){
      bool sent=SteamMatchmaking.InviteUserToLobby(new CSteamID(ulong.Parse(steam.RoomId)),id);
      inviteNotice=sent?"Invitation sent. Waiting for your friend.":"Invitation unavailable. Try again or share the Room ID.";
      GameBridge.Log("INVITE request accepted="+sent);
     }
    }}finally{GUI.EndScrollView();}
    if(count==0)inviteNotice="No Steam friends found. Share the Room ID instead.";
   }catch(Exception e){inviteNotice="Friend list unavailable. Share the Room ID.";GameBridge.Log("INVITE picker: "+e.Message);}
   if(GUI.Button(new Rect(114,699,812,36),"COPY STEAM ROOM ID",menuButton)){GUIUtility.systemCopyBuffer=steam.RoomId;inviteNotice="Room ID copied.";}
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
