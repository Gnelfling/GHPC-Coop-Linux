using System;using System.IO;using System.Net;using System.Runtime.InteropServices;using Steamworks;
namespace GhpcCoop {
 // All Steam calls run on the game thread. GHPC owns Steam initialization/shutdown.
 public sealed class SteamLink:ILink {
  const int VirtualPort=29761;readonly string build,hostToken;readonly bool privateRoom;bool hosting,disposed;CSteamID lobby,owner,peer;
  HSteamListenSocket listener;HSteamNetConnection connection;
  CallResult<LobbyCreated_t> created;CallResult<LobbyEnter_t> joined;Callback<SteamNetConnectionStatusChangedCallback_t> changed;
  public bool Connected {get;private set;}public string Error{get;private set;}public string RoomToken{get;private set;}
  public string RoomId {get{return lobby.m_SteamID==0?"":lobby.ToString();}}
  public SteamLink(string token,string gameBuild,bool privateTestRoom=false){privateRoom=privateTestRoom;hostToken=token;build=gameBuild;Error="";CheckSteam();changed=Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnChanged);SteamNetworkingUtils.InitRelayNetworkAccess();}
  public static void CheckSteam(){if(SteamAPI.GetHSteamUser().m_HSteamUser==0||!SteamUser.BLoggedOn())throw new IOException("Steam is not ready. Launch GHPC through Steam while logged in.");if(SteamUtils.GetAppID().m_AppId!=1705180)throw new IOException("GHPC Steam app context required");}
  void Safe(Action action){try{action();}catch(Exception e){Error=e.Message;Connected=false;}}
  public void Host(IPAddress unused,int port){hosting=true;created=CallResult<LobbyCreated_t>.Create(delegate(LobbyCreated_t r,bool io){
   if(r.m_eResult==EResult.k_EResultOK){lobby=new CSteamID(r.m_ulSteamIDLobby);if(disposed){SteamMatchmaking.LeaveLobby(lobby);created.Dispose();return;}}
   Safe(delegate{if(io||r.m_eResult!=EResult.k_EResultOK)throw new IOException("Steam room creation failed: "+r.m_eResult);owner=SteamUser.GetSteamID();RoomToken=hostToken;
    listener=SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort,0,new SteamNetworkingConfigValue_t[0]);if(listener.m_HSteamListenSocket==0)throw new IOException("Steam P2P listener unavailable");
    Set("ghpc_coop","0.9.0");Set("wire",Wire.Version.ToString());Set("build",build);Set("token",hostToken);Set("owner",owner.ToString());
   });
  });created.Set(SteamMatchmaking.CreateLobby(privateRoom?ELobbyType.k_ELobbyTypePrivate:ELobbyType.k_ELobbyTypeFriendsOnly,2));}
  void Set(string key,string value){if(!SteamMatchmaking.SetLobbyData(lobby,key,value))throw new IOException("Steam room metadata failed");}
  public void Join(string room,int unused){ulong id;if(!ulong.TryParse(room,out id)||!new CSteamID(id).IsLobby())throw new IOException("Enter the host's numeric Steam Room ID");
   joined=CallResult<LobbyEnter_t>.Create(delegate(LobbyEnter_t r,bool io){
    if(r.m_EChatRoomEnterResponse==1){lobby=new CSteamID(r.m_ulSteamIDLobby);if(disposed){SteamMatchmaking.LeaveLobby(lobby);joined.Dispose();return;}}
    Safe(delegate{if(io||r.m_EChatRoomEnterResponse!=1)throw new IOException("Steam room join failed: "+r.m_EChatRoomEnterResponse);
     if(!SteamRoomPolicy.Compatible(SteamMatchmaking.GetLobbyData(lobby,"ghpc_coop"),SteamMatchmaking.GetLobbyData(lobby,"wire"),SteamMatchmaking.GetLobbyData(lobby,"build"),build))throw new IOException("Steam room mod/game version differs");
     owner=SteamMatchmaking.GetLobbyOwner(lobby);if(owner==SteamUser.GetSteamID()||SteamMatchmaking.GetLobbyData(lobby,"owner")!=owner.ToString())throw new IOException("Original host has left");
     RoomToken=SteamMatchmaking.GetLobbyData(lobby,"token");if(RoomToken.Length!=32)throw new IOException("Invalid room token");peer=owner;
     var identity=new SteamNetworkingIdentity();identity.SetSteamID(owner);connection=SteamNetworkingSockets.ConnectP2P(ref identity,VirtualPort,0,new SteamNetworkingConfigValue_t[0]);if(connection.m_HSteamNetConnection==0)throw new IOException("Steam P2P connection unavailable");
    });
   });joined.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));
  }
  bool Member(CSteamID id){for(int i=0;i<SteamMatchmaking.GetNumLobbyMembers(lobby);i++)if(SteamMatchmaking.GetLobbyMemberByIndex(lobby,i)==id)return true;return false;}
  void OnChanged(SteamNetConnectionStatusChangedCallback_t r){if(disposed)return;Safe(delegate{
   if(hosting&&listener.m_HSteamListenSocket!=0&&r.m_info.m_hListenSocket==listener&&r.m_info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting){
    var candidate=r.m_info.m_identityRemote.GetSteamID();
    if(lobby.m_SteamID==0||!SteamRoomPolicy.CanAccept(owner.m_SteamID,candidate.m_SteamID,Member(candidate),connection.m_HSteamNetConnection,r.m_hConn.m_HSteamNetConnection)){SteamNetworkingSockets.CloseConnection(r.m_hConn,0,"Room membership required",false);return;}
    connection=r.m_hConn;peer=candidate;if(SteamNetworkingSockets.AcceptConnection(connection)!=EResult.k_EResultOK)throw new IOException("Steam session acceptance failed");
   }
   if(connection.m_HSteamNetConnection==0||r.m_hConn!=connection)return;
   if(r.m_info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)Connected=true;
   if(r.m_info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer||r.m_info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally){Connected=false;Error="Steam peer disconnected: "+r.m_info.m_szEndDebug;}
  });}
  public void Pump(){if(disposed)return;Safe(delegate{if(!SteamUser.BLoggedOn())throw new IOException("Steam signed out");if(lobby.m_SteamID!=0){if(!SteamRoomPolicy.IsOriginalHost(owner.m_SteamID,SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID))throw new IOException("Host left the Steam room");if(Connected&&!Member(peer))throw new IOException("Peer left the Steam room");}});}
  public void Send(Message m){if(!Connected||disposed)throw new IOException("Steam peer not connected");var bytes=Wire.Encode(m);var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);try{long message;var result=SteamNetworkingSockets.SendMessageToConnection(connection,pin.AddrOfPinnedObject(),(uint)bytes.Length,9,out message);if(result!=EResult.k_EResultOK)throw new IOException("Steam send failed: "+result);}finally{pin.Free();}}
  public bool TryRead(out Message m){m=null;if(!Connected||disposed)return false;var pointers=new IntPtr[1];int count=SteamNetworkingSockets.ReceiveMessagesOnConnection(connection,pointers,1);if(count<0)throw new IOException("Steam receive failed");if(count==0)return false;
   try{var packet=(SteamNetworkingMessage_t)Marshal.PtrToStructure(pointers[0],typeof(SteamNetworkingMessage_t));if(packet.m_identityPeer.GetSteamID()!=peer||packet.m_cbSize<1||packet.m_cbSize>Wire.MaxFrame)throw new IOException("Invalid Steam packet");var bytes=new byte[packet.m_cbSize];Marshal.Copy(packet.m_pData,bytes,0,bytes.Length);m=Wire.Decode(bytes);return true;}finally{SteamNetworkingMessage_t.Release(pointers[0]);}
  }
  public void InviteOverlay(){if(lobby.m_SteamID!=0)SteamFriends.ActivateGameOverlayInviteDialog(lobby);}
  public void Dispose(){if(disposed)return;disposed=true;Connected=false;if(connection.m_HSteamNetConnection!=0)SteamNetworkingSockets.CloseConnection(connection,0,"Co-op room closed",false);if(listener.m_HSteamListenSocket!=0)SteamNetworkingSockets.CloseListenSocket(listener);if(lobby.m_SteamID!=0)SteamMatchmaking.LeaveLobby(lobby);if(changed!=null)changed.Dispose();if(created!=null&&!created.IsActive())created.Dispose();if(joined!=null&&!joined.IsActive())joined.Dispose();}
 }
}

