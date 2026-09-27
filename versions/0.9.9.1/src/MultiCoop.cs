using System;using System.IO;using System.Linq;using System.Collections.Generic;using GHPC;using GHPC.Player;using UnityEngine;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  sealed class Peer {public int Id;public ILink Link;public HostSession Session;public bool Claimed;public float Seen,CloseAt;}
  readonly List<Peer> guests=new List<Peer>();RoomSeats seats;SteamLink multiRoom;int nextPeer;float nextRoomSnapshot;string[] initialRoomVehicles=new string[0];
  void BeginMultiHost(){
   var platoon=game.PlatoonChoices().Concat(new[]{game.LocalId}).ToArray();initialRoomVehicles=platoon;seats=new RoomSeats(game.LocalId,platoon,game.FriendlyChoices());
   GameBridge.Log("ROOM capacity="+seats.Capacity+" host="+game.LocalId+" platoon="+String.Join(",",platoon));
   if(seats.Capacity<2){
    var host=game.Vehicles[game.LocalId].Unit;
    var rows=new List<string>{"Vehicle\tFaction\tHost\tPlatoon\tPlatoonInitialized\tMembers\tAvailable\tSamePlatoon"};
    foreach(var item in game.Vehicles){var u=item.Value.Unit;rows.Add(u.UniqueName+"\t"+u.Allegiance+"\t"+(u==host)+"\t"+(u.Platoon==null?"null":GameBridge.PathOf(u.Platoon.transform))+"\t"+u.PlatoonInitialized+"\t"+(u.Platoon==null||u.Platoon.Units==null?0:u.Platoon.Units.Count)+"\t"+game.Available(item.Key)+"\t"+(host.Platoon!=null&&u.Platoon==host.Platoon));}
    File.WriteAllLines(Path.Combine(DataDir,"platoon-capacity-diagnostic.tsv"),rows.ToArray());
    throw new InvalidOperationException("Your platoon needs at least two available vehicles");
   }
   multiRoom=new SteamLink(token,build,sweepEnabled);multiRoom.SetCapacity(seats.Capacity);link=multiRoom;multiRoom.Host(System.Net.IPAddress.Any,0);
   status="Room open: 1/"+seats.Capacity+" players (including host). Creating Steam lobby...";
   File.WriteAllLines(Path.Combine(DataDir,"room.txt"),new[]{token,build,game.World,game.Roster,game.LocalId,platoon.First(x=>x!=game.LocalId)});
  }
  void DropPeer(Peer peer){game.ReleasePeer(peer.Id);seats.Release(peer.Id);peer.Session.Disconnect();peer.Link.Dispose();guests.Remove(peer);claimed=guests.Any(x=>x.Claimed);BroadcastOccupancy();GameBridge.Log("ROOM departed peer="+peer.Id+" players="+seats.Count);}
  void Broadcast(Message m){foreach(var peer in guests.Where(x=>x.Claimed).ToArray())try{peer.Link.Send(m);}catch(Exception){DropPeer(peer);}}
  void BroadcastOccupancy(){if(seats==null)return;foreach(var peer in guests.Where(x=>x.Claimed).ToArray())try{peer.Link.Send(new Message{Kind=Kind.VehicleAssignment,Unit=game.LocalId,Text=seats.Vehicle(peer.Id),Roster=String.Join("\n",seats.OccupiedVehicles()),Role=seats.Capacity});}catch{peer.Link.Dispose();}}
  void UpdateMultiHost(float now){
   multiRoom.Pump();
   if(!String.IsNullOrEmpty(multiRoom.Error))throw new IOException(multiRoom.Error);
   ILink accepted;while((accepted=multiRoom.Take())!=null){
    if(guests.Count>=seats.Capacity-1){accepted.Dispose();continue;}
    guests.Add(new Peer{Id=++nextPeer,Link=accepted,Seen=now,Session=new HostSession(token,build,game.World,game.Roster,game.LocalId,game.FriendlyChoices())});
   }
   foreach(var peer in guests.ToArray()){
    if(peer.CloseAt>0){if(now>=peer.CloseAt)DropPeer(peer);continue;}
    if(!String.IsNullOrEmpty(peer.Link.Error)||now-peer.Seen>180){DropPeer(peer);continue;}
    if(!peer.Link.Connected)continue;
    try{Message m;int budget=0;while(budget++<32&&peer.Link.TryRead(out m)){
     peer.Seen=now;
     if(m.Kind==Kind.Claim&&m.Unit!=seats.Vehicle(peer.Id))throw new IOException("Vehicle was not reserved for this player");
     if(m.Kind==Kind.SwitchVehicle){if(!peer.Claimed||!game.Available(m.Unit)||!seats.CanMove(peer.Id,m.Unit)){peer.Link.Send(new Message{Kind=Kind.SwitchRejected,Text="Vehicle occupied or outside your platoon"});continue;}}
     var reply=peer.Session.Handle(m);
     if(reply.Kind==Kind.Mission){
      var unit=seats.Reserve(peer.Id,initialRoomVehicles.Where(game.Available));
      if(unit==null)throw new IOException("Room full: no free vehicle in host platoon (capacity "+seats.Capacity+")");
      var state=GHPC.State.PersistentDataManager.GetStateData();
      reply.Unit=unit;reply.Text=state.TheaterKey+"\n"+state.MetaData.MissionSceneReference.Name+"\n"+MissionChoices.Export();
      reply.Role=(int)game.Vehicles[game.LocalId].Unit.Allegiance;reply.Fire=SceneController.IsDaytime;
     }
     if(reply.Kind==Kind.Claimed){if(!game.Available(reply.Unit))throw new IOException("Reserved vehicle no longer available");game.ReservePeer(peer.Id,reply.Unit);peer.Claimed=true;claimed=true;panel=false;GameBridge.Log("ROOM claimed peer="+peer.Id+" vehicle="+reply.Unit+" players="+seats.Count+"/"+seats.Capacity);}
     if(reply.Kind==Kind.VehicleAssignment){if(!seats.Move(peer.Id,reply.Text))throw new IOException("Vehicle reservation changed during transfer");game.ReservePeer(peer.Id,seats.Vehicle(peer.Id));}
     if(m.Kind==Kind.Input&&reply.Kind==Kind.Ping)game.ReceivePeer(peer.Id,m);
     if(m.Kind==Kind.SupportRequest&&reply.Kind==Kind.Ping){reply=support.HandleRequest(m,game.Vehicles[seats.Vehicle(peer.Id)].Unit);Broadcast(reply);}
     else peer.Link.Send(reply);
     if(reply.Kind==Kind.Claimed||reply.Kind==Kind.VehicleAssignment)BroadcastOccupancy();
     if(reply.Kind==Kind.Error){peer.CloseAt=now+.5f;break;}
    }}catch(Exception e){GameBridge.Log("ROOM peer="+peer.Id+" rejected: "+e.Message);try{peer.Link.Send(new Message{Kind=Kind.Error,Text=e.Message});}catch{}peer.CloseAt=now+.5f;}
   }
   status="Players: "+seats.Count+"/"+seats.Capacity+" (including host)";
   var input=PlayerInput.Instance;
   if(input!=null&&input.CurrentPlayerUnit!=null&&input.CurrentPlayerUnit!=game.Vehicles[game.LocalId].Unit){
    var id=game.Vehicles.FirstOrDefault(x=>x.Value.Unit==input.CurrentPlayerUnit).Key;
    // Validate and transfer first; do not re-enter a destroyed vehicle before handling TAB.
    if(id!=null)RequestVehicle(id);
    var assigned=game.Vehicles[game.LocalId].Unit;
    if(input.CurrentPlayerUnit!=assigned)input.SetPlayerUnit(assigned);
   }
   if(!claimed)return;support.Ready=tracers.Ready=true;game.FirePeers();
   foreach(var result in support.Drain()){result.Sequence=++seq;Broadcast(result);}
   if(now>=nextRoomSnapshot){nextRoomSnapshot=now+.05f;var snap=new Message{Kind=Kind.Snapshot,Sequence=++seq,Poses=game.Snapshot(),Supports=support.Capture(),Objectives=ObjectiveSync.Capture(),Weather=WeatherSync.Capture()};WeatherSync.CaptureSky(snap);Broadcast(snap);var paths=tracers.Drain();if(paths.Length>0)Broadcast(new Message{Kind=Kind.Tracers,Sequence=++seq,Tracers=paths});}
   var impacts=game.Combat.Drain();if(impacts.Length>0)Broadcast(new Message{Kind=Kind.Effects,Sequence=++seq,Impacts=impacts});
  }
 }
}
