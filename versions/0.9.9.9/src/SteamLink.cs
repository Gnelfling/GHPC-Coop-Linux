using System;using System.IO;using System.Net;using System.Linq;using System.Collections.Generic;using System.Runtime.InteropServices;using Steamworks;
namespace GhpcCoop {
 // One lobby owns up to three independent authenticated Steam connections.
 // Steam callbacks and all transport calls run on the game thread.
 public sealed class SteamLink:IRoom {
  const int VirtualPort=29761;readonly string build,hostToken;readonly bool privateRoom;
  readonly Dictionary<uint,SteamPeer> peers=new Dictionary<uint,SteamPeer>();readonly Queue<SteamPeer> accepted=new Queue<SteamPeer>();
  bool hosting,disposed;int capacity=4;CSteamID lobby,owner;HSteamListenSocket listener;SteamPeer client;
  CallResult<LobbyCreated_t> created;CallResult<LobbyEnter_t> joined;Callback<SteamNetConnectionStatusChangedCallback_t> changed;
  public bool Connected{get{return !disposed&&(hosting?listener.m_HSteamListenSocket!=0:client!=null&&client.Connected);}}
  public string Error{get;private set;}public string RoomToken{get;private set;}
  public string RoomId{get{return lobby.m_SteamID==0?"":lobby.ToString();}}
  public SteamLink(string token,string gameBuild,bool privateTestRoom=false){privateRoom=privateTestRoom;hostToken=token;build=gameBuild;Error="";CheckSteam();changed=Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnChanged);SteamNetworkingUtils.InitRelayNetworkAccess();}
  public static void CheckSteam(){if(SteamAPI.GetHSteamUser().m_HSteamUser==0||!SteamUser.BLoggedOn())throw new IOException("Steam is not ready. Sign in and launch GHPC.");if(SteamUtils.GetAppID().m_AppId!=1705180)throw new IOException("GHPC Steam app context required");}
  public void SetCapacity(int count){if(count<2||count>4)throw new ArgumentOutOfRangeException("count");capacity=count;}
  void Safe(Action action){try{action();}catch(Exception e){Error=e.Message;}}
  void Set(string key,string value){if(!SteamMatchmaking.SetLobbyData(lobby,key,value))throw new IOException("Steam lobby metadata failed");}
  // Disable ICE direct routes on these sockets only; leave GHPC/global Steam settings alone.
  static SteamNetworkingConfigValue_t[] RelayOptions(){return new[]{new SteamNetworkingConfigValue_t{m_eValue=ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,m_eDataType=ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,m_val=new SteamNetworkingConfigValue_t.OptionValue{m_int32=0}}};}
  public void Host(IPAddress unused,int port){hosting=true;created=CallResult<LobbyCreated_t>.Create(delegate(LobbyCreated_t r,bool io){
   if(r.m_eResult==EResult.k_EResultOK){lobby=new CSteamID(r.m_ulSteamIDLobby);if(disposed){SteamMatchmaking.LeaveLobby(lobby);created.Dispose();return;}}
   Safe(delegate{if(io||r.m_eResult!=EResult.k_EResultOK)throw new IOException("Steam room creation failed: "+r.m_eResult);owner=SteamUser.GetSteamID();RoomToken=hostToken;
    listener=SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort,1,RelayOptions());if(listener.m_HSteamListenSocket==0)throw new IOException("Steam P2P listener unavailable");
    Set("ghpc_coop","0.9.9.1");Set("wire",Wire.Version.ToString());Set("build",build);Set("token",hostToken);Set("owner",owner.ToString());Set("capacity",capacity.ToString());
    GameBridge.Log("STEAM room ready capacity="+capacity);
   });});created.Set(SteamMatchmaking.CreateLobby(privateRoom?ELobbyType.k_ELobbyTypePrivate:ELobbyType.k_ELobbyTypeFriendsOnly,capacity));}
  public void Join(string room,int unused){ulong id;if(!ulong.TryParse(room,out id)||!new CSteamID(id).IsLobby())throw new IOException("Enter the host's numeric Steam Room ID");
   joined=CallResult<LobbyEnter_t>.Create(delegate(LobbyEnter_t r,bool io){if(r.m_EChatRoomEnterResponse==1){lobby=new CSteamID(r.m_ulSteamIDLobby);if(disposed){SteamMatchmaking.LeaveLobby(lobby);joined.Dispose();return;}}
    Safe(delegate{if(io||r.m_EChatRoomEnterResponse!=1)throw new IOException("Steam room join failed: "+r.m_EChatRoomEnterResponse);
     if(!SteamRoomPolicy.Compatible(SteamMatchmaking.GetLobbyData(lobby,"ghpc_coop"),SteamMatchmaking.GetLobbyData(lobby,"wire"),SteamMatchmaking.GetLobbyData(lobby,"build"),build))throw new IOException("All players need the same compatible mod protocol and game build (update every player)");
     owner=SteamMatchmaking.GetLobbyOwner(lobby);if(owner==SteamUser.GetSteamID()||SteamMatchmaking.GetLobbyData(lobby,"owner")!=owner.ToString())throw new IOException("Original host has left");
     RoomToken=SteamMatchmaking.GetLobbyData(lobby,"token");if(RoomToken.Length!=32)throw new IOException("Invalid room token");
     var identity=new SteamNetworkingIdentity();identity.SetSteamID(owner);var conn=SteamNetworkingSockets.ConnectP2P(ref identity,VirtualPort,1,RelayOptions());if(conn.m_HSteamNetConnection==0)throw new IOException("Steam P2P unavailable");client=new SteamPeer(conn,owner);
    });});joined.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));}
  bool Member(CSteamID id){for(int i=0;i<SteamMatchmaking.GetNumLobbyMembers(lobby);i++)if(SteamMatchmaking.GetLobbyMemberByIndex(lobby,i)==id)return true;return false;}
  void OnChanged(SteamNetConnectionStatusChangedCallback_t r){if(disposed)return;Safe(delegate{
   if(hosting){SteamPeer p;
    if(!peers.TryGetValue(r.m_hConn.m_HSteamNetConnection,out p)){
     if(r.m_info.m_hListenSocket!=listener||r.m_info.m_eState!=ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)return;
     var candidate=r.m_info.m_identityRemote.GetSteamID();
     if(!SteamRoomPolicy.CanAcceptMulti(owner.m_SteamID,candidate.m_SteamID,Member(candidate),capacity,peers.Values.Where(x=>!x.Closed).Select(x=>x.Identity.m_SteamID))){SteamNetworkingSockets.CloseConnection(r.m_hConn,0,"Room full or membership required",false);return;}
     p=new SteamPeer(r.m_hConn,candidate);if(SteamNetworkingSockets.AcceptConnection(r.m_hConn)!=EResult.k_EResultOK){p.Dispose();return;}peers.Add(r.m_hConn.m_HSteamNetConnection,p);accepted.Enqueue(p);
    }p.Changed(r.m_info);
   }else if(client!=null&&client.Connection==r.m_hConn)client.Changed(r.m_info);
  });}
  public ILink Take(){while(accepted.Count>0){var p=accepted.Dequeue();if(!p.Closed)return p;}return null;}
  public void Pump(){if(disposed)return;Safe(delegate{CheckSteam();if(lobby.m_SteamID==0)return;if(!SteamRoomPolicy.IsOriginalHost(owner.m_SteamID,SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID))throw new IOException("Host left Steam room");
   if(hosting){foreach(var p in peers.Values.ToArray())if(p.Closed||!Member(p.Identity)){p.Dispose();peers.Remove(p.Connection.m_HSteamNetConnection);}}
   else if(client!=null){if(!Member(owner))throw new IOException("Host left Steam room");if(client.Error!="")throw new IOException(client.Error);}
  });}
  public void Send(Message m){if(client==null)throw new IOException("Guest Steam connection not ready");client.Send(m);}
  public bool TryRead(out Message m){m=null;return client!=null&&client.TryRead(out m);}
  public void InviteOverlay(){if(lobby.m_SteamID!=0)SteamFriends.ActivateGameOverlayInviteDialog(lobby);}
  public void Dispose(){if(disposed)return;disposed=true;foreach(var p in peers.Values)p.Dispose();peers.Clear();accepted.Clear();if(client!=null)client.Dispose();if(listener.m_HSteamListenSocket!=0)SteamNetworkingSockets.CloseListenSocket(listener);if(lobby.m_SteamID!=0)SteamMatchmaking.LeaveLobby(lobby);if(changed!=null)changed.Dispose();if(created!=null&&!created.IsActive())created.Dispose();if(joined!=null&&!joined.IsActive())joined.Dispose();}
 }
 public sealed class SteamPeer:ILink {
  public readonly HSteamNetConnection Connection;public readonly CSteamID Identity;public bool Closed{get;private set;}public bool Connected{get;private set;}public string Error{get;private set;}
  public SteamPeer(HSteamNetConnection connection,CSteamID identity){Connection=connection;Identity=identity;Error="";}
  public void Changed(SteamNetConnectionInfo_t info){if(Closed)return;if(info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected){Connected=true;GameBridge.Log("STEAM connected relayed="+((info.m_nFlags&16)!=0)+" flags="+info.m_nFlags+" route="+info.m_szConnectionDescription);}
   if(info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer||info.m_eState==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally){Error="Steam connection closed: "+info.m_szEndDebug;Dispose();}}
  public void Host(IPAddress a,int p){throw new NotSupportedException();}public void Join(string a,int p){throw new NotSupportedException();}public void Pump(){}
  public void Send(Message m){if(!Connected||Closed)throw new IOException("Steam peer not connected");var bytes=Wire.Encode(m);var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);try{long number;var result=SteamNetworkingSockets.SendMessageToConnection(Connection,pin.AddrOfPinnedObject(),(uint)bytes.Length,9,out number);if(result!=EResult.k_EResultOK)throw new IOException("Steam send failed: "+result);}finally{pin.Free();}}
  public bool TryRead(out Message m){m=null;if(!Connected||Closed)return false;var pointers=new IntPtr[1];int count=SteamNetworkingSockets.ReceiveMessagesOnConnection(Connection,pointers,1);if(count<0)throw new IOException("Steam receive failed");if(count==0)return false;
   try{var packet=(SteamNetworkingMessage_t)Marshal.PtrToStructure(pointers[0],typeof(SteamNetworkingMessage_t));if(packet.m_identityPeer.GetSteamID()!=Identity||packet.m_cbSize<1||packet.m_cbSize>Wire.MaxFrame)throw new IOException("Invalid Steam packet");var bytes=new byte[packet.m_cbSize];Marshal.Copy(packet.m_pData,bytes,0,bytes.Length);m=Wire.Decode(bytes);return true;}finally{SteamNetworkingMessage_t.Release(pointers[0]);}}
  public void Dispose(){if(Closed)return;Closed=true;Connected=false;if(Error=="")Error="Steam peer disconnected";SteamNetworkingSockets.CloseConnection(Connection,0,"Co-op connection closed",false);}
 }
}

