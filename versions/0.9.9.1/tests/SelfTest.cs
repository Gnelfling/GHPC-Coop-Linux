using System;using System.IO;using System.Net;using System.Threading;using System.Collections.Generic;
namespace GhpcCoop {
 class SelfTest {
  static int passed;static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
  static void Reject(Action a,string name){bool threw=false;try{a();}catch(InvalidDataException){threw=true;}catch(EndOfStreamException){threw=true;}Check(threw,name);}
  static HostSession Host(){return new HostSession("secret","build","mission","roster","tank-a",new[]{"tank-a","tank-b"});}
  static Message Hello(){return new Message{Kind=Kind.Hello,Token="secret",Build="build",World="mission",Roster="roster"};}
  static Message Wait(Link l){var end=DateTime.UtcNow.AddSeconds(4);Message m;while(DateTime.UtcNow<end){if(l.TryRead(out m))return m;Thread.Sleep(10);}throw new Exception("Network receive timeout: "+l.Error);}
  static void Main(){
   var crossPlatoonSeats=new RoomSeats("a",new[]{"a","b"},new[]{"a","b","c","d"});
   Check(crossPlatoonSeats.CanMove(0,"c")&&crossPlatoonSeats.Vehicle(0)=="a","transfer validation leaves reservation unchanged");
   Check(crossPlatoonSeats.Capacity==2,"cross platoon switching preserves room capacity");
   Check(crossPlatoonSeats.Move(0,"c"),"waiting host transfers to another friendly platoon");
   Check(crossPlatoonSeats.Reserve(1,new[]{"a","b"})=="a","guest reservation after host transfer");
   Check(!crossPlatoonSeats.Move(1,"c"),"guest cannot steal transferred host vehicle");
   Check(crossPlatoonSeats.Move(1,"d"),"guest transfers to free friendly vehicle");
   Check(!crossPlatoonSeats.Move(0,"enemy"),"enemy transfer remains blocked");
   Check(crossPlatoonSeats.Reserve(2,new[]{"a","b"})==null,"switching cannot add extra players");

   Check(Wire.SameMissionWorld("PA_Forest_Incursion_LMC|PointAlphaGermany03|True","PA_Forest_Incursion_LMC|PointAlphaGermany03|False"),"join survives day night change during room lifetime");
   Check(Wire.SameMissionWorld("m|t|False","m|t|True"),"join survives night day change");
   Check(!Wire.SameMissionWorld("m|t|True","other|t|False"),"different mission still rejected");
   Check(!Wire.SameMissionWorld("m|t|True","m|other|False"),"different terrain still rejected");
   Check(!Wire.SameMissionWorld("m|t|True","m|t|invalid"),"invalid environment suffix rejected");
   Check(!Wire.SameMissionWorld("m|t|True","m|t|False|extra"),"extra world fields rejected");
   Check(!Wire.SameMissionWorld(null,"m|t|True"),"missing world rejected");
   var oldRoom=new HostSession("secret","build","m|t|True","roster","tank-a",new[]{"tank-a","tank-b"});
   var offer=oldRoom.Handle(new Message{Kind=Kind.JoinRoom,Token="secret",Build="build"});
   Check(Wire.SameMissionWorld(offer.World,"m|t|False")&&oldRoom.Handle(new Message{Kind=Kind.Hello,Token="secret",Build="build",World=offer.World,Roster="roster"}).Kind==Kind.Welcome,"fixed guest echoes validated token to already-running host");

   try{
    Check(SteamRoomPolicy.CanAcceptMulti(1,4,true,4,new ulong[]{2,3}),"Steam third guest admitted");
    Check(!SteamRoomPolicy.CanAcceptMulti(1,5,true,4,new ulong[]{2,3,4}),"Steam fifth player rejected");
    Check(!SteamRoomPolicy.CanAcceptMulti(1,2,true,4,new ulong[]{2}),"Steam duplicate identity rejected");
    Check(!SteamRoomPolicy.CanAcceptMulti(1,3,true,2,new ulong[]{2}),"Steam two-vehicle room limited");
    Check(!SteamRoomPolicy.CanAcceptMulti(1,4,false,4,new ulong[]{2}),"Steam nonmember rejected multi");
    Check(SteamRoomPolicy.CanAcceptMulti(1,3,true,3,new ulong[]{2}),"Steam freed slot reusable");
    Check(!SteamRoomPolicy.Compatible("0.9.0",Wire.Version.ToString(),"build","build"),"Legacy Steam room rejected");

    var scorched=new Message{Kind=Kind.Snapshot,Poses=new[]{new Pose{Id="tank",Qw=1,Scorch=new[]{0f,.4f,1f}}}};
    var scorchCopy=Wire.Decode(Wire.Encode(scorched));Check(scorchCopy.Poses[0].Scorch[0]==0&&scorchCopy.Poses[0].Scorch[1]==.4f&&scorchCopy.Poses[0].Scorch[2]==1,"intact partial and fully scorched appearance round trip");
    scorched.Poses[0].Scorch[0]=float.NaN;Reject(delegate{Wire.Encode(scorched);},"nonfinite scorch rejected");
    scorched.Poses[0].Scorch[0]=1.1f;Reject(delegate{Wire.Encode(scorched);},"excess scorch rejected");
    for(int count=2;count<=5;count++){
     var units=new List<string>();for(int i=0;i<count;i++)units.Add("vehicle-"+i);
     var roomSeats=new RoomSeats(units[0],units);Check(roomSeats.Capacity==Math.Min(count,4),"platoon capacity "+count);
     for(int i=1;i<roomSeats.Capacity;i++)Check(roomSeats.Reserve(i,units)==units[i],"distinct loading reservation "+count+"/"+i);
     Check(roomSeats.Reserve(99,units)==null,"full room refuses extra guest "+count);
     Check(!roomSeats.Move(1,units[0]),"guest cannot take host vehicle "+count);
     Check(!roomSeats.Move(0,units[1]),"host cannot take guest vehicle "+count);
     Check(!roomSeats.Move(1,"other-platoon"),"cross platoon vehicle refused "+count);
     roomSeats.Release(1);Check(roomSeats.Reserve(77,units)==units[1],"departed seat reused "+count);
    }
    using(var server=new MultiRoom())using(var c1=new Link())using(var c2=new Link())using(var c3=new Link()){
     server.Host(IPAddress.Loopback,0);var clients=new[]{c1,c2,c3};var acceptedPeers=new List<Link>();
     foreach(var client in clients){client.Join("127.0.0.1",server.Port);var until=DateTime.UtcNow.AddSeconds(4);Link connection=null;while(connection==null&&DateTime.UtcNow<until){connection=server.Take();Thread.Sleep(5);}Check(connection!=null,"accept concurrent guest");acceptedPeers.Add(connection);}
     try{for(int i=0;i<3;i++)clients[i].Send(new Message{Kind=Kind.Ping,Sequence=i+100});
      for(int i=0;i<3;i++)Check(Wait(acceptedPeers[i]).Sequence==i+100,"independent guest input stream "+i);
      for(int i=0;i<3;i++)acceptedPeers[i].Send(new Message{Kind=Kind.Ping,Sequence=i+200});
      for(int i=0;i<3;i++)Check(Wait(clients[i]).Sequence==i+200,"reply routed to correct guest "+i);
      c2.Dispose();c1.Send(new Message{Kind=Kind.Ping,Sequence=301});c3.Send(new Message{Kind=Kind.Ping,Sequence=303});
      Check(Wait(acceptedPeers[0]).Sequence==301&&Wait(acceptedPeers[2]).Sequence==303,"one guest departure preserves other connections");
     }finally{foreach(var peer in acceptedPeers)peer.Dispose();}
    }
    var support=new Message{Kind=Kind.SupportRequest,Sequence=1,Unit="tank-b",SupportId="B:0",Munition=2,SupportX=200,SupportY=30,SupportZ=-90};
    var supportCopy=Wire.Decode(Wire.Encode(support));Check(supportCopy.SupportId=="B:0"&&supportCopy.Munition==2&&supportCopy.SupportX==200&&supportCopy.SupportZ==-90,"map support request round trip");
    support.SupportX=float.NaN;Reject(delegate{Wire.Encode(support);},"nonfinite support target rejected");support.SupportX=0;
    support.Munition=4;Reject(delegate{Wire.Encode(support);},"unknown support munition rejected");support.Munition=2;
    support.Supports=new[]{new SupportState{Id="B:0",Missions=2,Firing=true,Cooldown=90,Delay=12,Impact=15}};
    var batteryCopy=Wire.Decode(Wire.Encode(support)).Supports[0];Check(batteryCopy.Missions==2&&batteryCopy.Firing&&batteryCopy.Cooldown==90&&batteryCopy.Impact==15,"battery availability and timing round trip");
    support.Supports=new[]{support.Supports[0],support.Supports[0]};Reject(delegate{Wire.Encode(support);},"duplicate battery rejected");support.Supports=new SupportState[65];Reject(delegate{Wire.Encode(support);},"oversized battery list rejected");support.Supports=new SupportState[0];
    support.Tracers=new[]{new TracerState{Id=5,Visual=8,X=1,Y=2,Z=3,EndX=30,EndY=4,EndZ=100,Duration=.05f}};
    var tracerCopy=Wire.Decode(Wire.Encode(support)).Tracers[0];Check(tracerCopy.Visual==8&&tracerCopy.EndZ==100&&tracerCopy.Duration==.05f,"host tracer path round trip");
    support.Tracers[0].EndX=float.PositiveInfinity;Reject(delegate{Wire.Encode(support);},"nonfinite tracer endpoint rejected");support.Tracers[0].EndX=30;support.Tracers[0].Duration=0;Reject(delegate{Wire.Encode(support);},"zero tracer duration rejected");support.Tracers[0].Duration=.05f;
    support.Tracers=new[]{support.Tracers[0],support.Tracers[0]};Reject(delegate{Wire.Encode(support);},"duplicate tracer ID rejected");support.Tracers=new TracerState[513];Reject(delegate{Wire.Encode(support);},"oversized tracer batch rejected");support.Tracers=new TracerState[0];
    var supportHost=Host();Check(supportHost.Handle(support).Kind==Kind.Error,"support blocked before handshake");supportHost.Handle(Hello());Check(supportHost.Handle(support).Kind==Kind.Error,"support blocked before vehicle claim");supportHost.Handle(new Message{Kind=Kind.Claim,Unit="tank-b"});support.Unit="tank-a";Check(supportHost.Handle(support).Kind==Kind.Error,"support from unowned vehicle blocked");support.Unit="tank-b";Check(supportHost.Handle(support).Kind==Kind.Ping,"owned support request accepted");Check(supportHost.Handle(support).Kind==Kind.Error,"duplicate support request blocked");
    Check(!SteamRoomPolicy.CanAccept(1,2,false,0,7),"Steam nonmember connection rejected");
    Check(!SteamRoomPolicy.CanAccept(1,1,true,0,7),"Steam self connection rejected");
    Check(!SteamRoomPolicy.CanAccept(1,0,true,0,7),"Steam missing identity rejected");
    Check(SteamRoomPolicy.CanAccept(1,2,true,0,7),"Steam room member accepted");
    Check(!SteamRoomPolicy.CanAccept(1,3,true,7,8),"Steam second connection rejected");
    Check(SteamRoomPolicy.CanAccept(1,2,true,7,7),"Steam repeated callback preserves connection");
    Check(!SteamRoomPolicy.IsOriginalHost(1,2)&&!SteamRoomPolicy.IsOriginalHost(0,0)&&SteamRoomPolicy.IsOriginalHost(1,1),"Steam host migration ends original session");
    Check(SteamRoomPolicy.Compatible("0.9.9.1",Wire.Version.ToString(),"build","build"),"Steam matching room metadata accepted");
    Check(!SteamRoomPolicy.Compatible("0.8.0",Wire.Version.ToString(),"build","build")&&!SteamRoomPolicy.Compatible("0.9.9.1","0","build","build")&&!SteamRoomPolicy.Compatible("0.9.9.1",Wire.Version.ToString(),"other","build"),"Steam incompatible metadata rejected");
    var pose=new Message{Kind=Kind.Snapshot,Sequence=22,Poses=new[]{new Pose{Id="tank-a",X=1.5f,Y=-3,Z=100,Qw=1}}};
    pose.Poses[0].Health=new[]{1f,.35f,0f};var health=Wire.Decode(Wire.Encode(pose)).Poses[0].Health;Check(health.Length==3&&health[0]==1&&health[1]==.35f&&health[2]==0,"intact damaged and destroyed components round trip");
    pose.Poses[0].Health[0]=float.NaN;Reject(delegate{Wire.Encode(pose);},"NaN component health rejected");pose.Poses[0].Health[0]=-1;Reject(delegate{Wire.Encode(pose);},"negative component health rejected");pose.Poses[0].Health[0]=1.1f;Reject(delegate{Wire.Encode(pose);},"excess component health rejected");pose.Poses[0].Health=new float[513];Reject(delegate{Wire.Encode(pose);},"oversized damage layout rejected");pose.Poses[0].Health=new float[0];
    pose.Poses[0].Audio=new[]{.7f,.8f,24f,23f,.2f,.5f,.1f,1f};
    var sound=Wire.Decode(Wire.Encode(pose)).Poses[0].Audio;Check(sound[0]==.7f&&sound[1]==.8f&&sound[2]==24&&sound[7]==1,"engine load and track audio round trip");
    pose.Poses[0].Audio[0]=float.NaN;Reject(delegate{Wire.Encode(pose);},"nonfinite engine audio rejected");pose.Poses[0].Audio[0]=-1;Reject(delegate{Wire.Encode(pose);},"negative engine audio rejected");
    pose.Poses[0].Audio=new float[7];Reject(delegate{Wire.Encode(pose);},"incomplete audio packet rejected");pose.Poses[0].Audio=new float[8];
    pose.Objectives=new[]{new ObjectiveStatus{Id=0,State=4,Visible=true,Text="RAIN 확보"},new ObjectiveStatus{Id=1,State=3,Visible=false,Text="Next objective"}};
    var objectiveCopy=Wire.Decode(Wire.Encode(pose)).Objectives;Check(objectiveCopy.Length==2&&objectiveCopy[0].Text=="RAIN 확보"&&objectiveCopy[0].State==4&&objectiveCopy[0].Visible&&!objectiveCopy[1].Visible,"objective text completion and visibility round trip");
    pose.Objectives[1].Id=0;Reject(delegate{Wire.Encode(pose);},"duplicate objective ID rejected");pose.Objectives[1].Id=1;
    pose.Objectives[0].State=6;Reject(delegate{Wire.Encode(pose);},"unknown objective state rejected");pose.Objectives[0].State=4;
    pose.Objectives[0].Text=new string('x',2049);Reject(delegate{Wire.Encode(pose);},"oversized objective text rejected");pose.Objectives=new ObjectiveStatus[65];Reject(delegate{Wire.Encode(pose);},"oversized objective list rejected");pose.Objectives=new ObjectiveStatus[0];
    pose.Poses[0].Ammo=new[]{new AmmoState{Breech="test",ClipType=2,Reloading=true,Clip=1,Reserve=12,Stage=2,Time=3.5f}};
    var ammoCopy=Wire.Decode(Wire.Encode(pose)).Poses[0].Ammo[0];Check(ammoCopy.ClipType==2&&ammoCopy.Breech=="test"&&ammoCopy.Reloading&&ammoCopy.Reserve==12&&ammoCopy.Stage==2&&ammoCopy.Time==3.5f,"ammo and reload progress round trip");
    pose.Poses[0].Ammo[0].Time=float.NaN;Reject(delegate{Wire.Encode(pose);},"invalid reload time rejected");pose.Poses[0].Ammo[0].Time=0;
    pose.Poses[0].Ammo[0].Reserve=-1;Reject(delegate{Wire.Encode(pose);},"negative ammo rejected");pose.Poses[0].Ammo[0].Reserve=12;
    pose.Poses[0].Equipment=new EquipmentState{SmokeRequest=2,Screen=true,Salvos=new[]{1,2},Materials=new[]{-1,3},Lamps=new[]{true,false}};
    var equipment=Wire.Decode(Wire.Encode(pose)).Poses[0].Equipment;
    Check(equipment.SmokeRequest==2&&equipment.Screen&&equipment.Salvos[1]==2&&equipment.Materials[0]==-1&&equipment.Materials[1]==3&&equipment.Lamps[0]&&!equipment.Lamps[1],"smoke lights and materials round trip");
    pose.Poses[0].Equipment.Salvos=new[]{-1};Reject(delegate{Wire.Encode(pose);},"negative smoke counter rejected");pose.Poses[0].Equipment.Salvos=new int[65];Reject(delegate{Wire.Encode(pose);},"oversized smoke array rejected");
    pose.Poses[0].Equipment=new EquipmentState{Materials=new[]{1025}};Reject(delegate{Wire.Encode(pose);},"invalid material index rejected");
    pose.Poses[0].Equipment=new EquipmentState{Lamps=new bool[129]};Reject(delegate{Wire.Encode(pose);},"oversized lamp array rejected");pose.Poses[0].Equipment=new EquipmentState();
    var transfer=new HostSession("secret","build","mission","roster","tank-a",new[]{"tank-a","tank-b","tank-c","tank-d"});
    Check(transfer.Handle(new Message{Kind=Kind.SwitchVehicle,Unit="tank-c"}).Kind==Kind.Error,"unauthenticated transfer blocked");transfer.Handle(Hello());transfer.Handle(new Message{Kind=Kind.Claim,Unit="tank-b"});
    Check(transfer.Handle(new Message{Kind=Kind.SwitchVehicle,Unit="tank-a"}).Kind==Kind.SwitchRejected&&transfer.GuestUnit=="tank-b","guest cannot take host vehicle");
    Check(transfer.Handle(new Message{Kind=Kind.SwitchVehicle,Unit="enemy"}).Kind==Kind.SwitchRejected&&transfer.GuestUnit=="tank-b","guest cannot take unlisted vehicle");
    Check(!transfer.MoveHost("tank-b")&&!transfer.MoveHost("enemy"),"host cannot take occupied or unlisted vehicle");
    var assignment=transfer.Handle(new Message{Kind=Kind.SwitchVehicle,Unit="tank-c"});Check(assignment.Kind==Kind.VehicleAssignment&&assignment.Unit=="tank-a"&&assignment.Text=="tank-c","guest transfer assigns free AI vehicle");
    Check(transfer.Handle(new Message{Kind=Kind.Input,Unit="tank-b",Sequence=1}).Kind==Kind.Error,"old vehicle input rejected after transfer");
    Check(transfer.Handle(new Message{Kind=Kind.Input,Unit="tank-c",Sequence=2}).Kind==Kind.Ping,"new vehicle input accepted after transfer");
    Check(transfer.MoveHost("tank-b")&&transfer.Assignment().Unit=="tank-b","released guest vehicle available to host");
    Check(transfer.Handle(new Message{Kind=Kind.SwitchVehicle,Unit="tank-a"}).Kind==Kind.VehicleAssignment,"released host vehicle available to guest");
    Check(!transfer.MoveHost("tank-a"),"simultaneous requests cannot share one vehicle");
    var assignmentCopy=Wire.Decode(Wire.Encode(transfer.Assignment()));Check(assignmentCopy.Unit=="tank-b"&&assignmentCopy.Text=="tank-a","vehicle ownership round trip");
    pose.HasSky=true;pose.SkyTime=123456789.125;pose.SkyRate=0.5f;pose.Daytime=true;var skyCopy=Wire.Decode(Wire.Encode(pose));Check(skyCopy.HasSky&&skyCopy.Daytime&&skyCopy.SkyTime==pose.SkyTime&&skyCopy.SkyRate==0.5f,"sky clock preserves double precision and daytime");
    pose.SkyTime=double.NaN;Reject(delegate{Wire.Encode(pose);},"nonfinite sky time rejected");pose.SkyTime=0;pose.SkyRate=float.PositiveInfinity;Reject(delegate{Wire.Encode(pose);},"nonfinite sky rate rejected");pose.SkyRate=0;
    var encoded=Wire.Encode(pose);var decoded=Wire.Decode(encoded);Check(decoded.Sequence==22&&decoded.Poses[0].X==1.5f,"snapshot round trip");
    Reject(delegate{Wire.Decode(new byte[3]);},"truncated packet");
    var bad=(byte[])encoded.Clone();bad[4]=99;Reject(delegate{Wire.Decode(bad);},"protocol mismatch");
    Reject(delegate{Wire.Encode(new Message{Kind=Kind.Input,Throttle=float.NaN});},"NaN input");
    Reject(delegate{Wire.Encode(new Message{Kind=Kind.Input,Steer=1.01f});},"out-of-range steering");
    Reject(delegate{Wire.Encode(new Message{Kind=Kind.Input,AimZ=0});},"zero aim vector rejected");
    Reject(delegate{Wire.Encode(new Message{Kind=Kind.Input,Role=7});},"unknown weapon role rejected");
    Reject(delegate{Wire.Encode(new Message{Kind=Kind.Input,Range=float.PositiveInfinity});},"infinite range rejected");
    var combat=new Message{Kind=Kind.Snapshot,Poses=new[]{new Pose{Id="combat",Qw=1,Flags=31,Shots=3,Mounts=new[]{new Rotation{Y=0.6f,W=0.8f}}}}};
    var combatCopy=Wire.Decode(Wire.Encode(combat));Check(combatCopy.Poses[0].Flags==31&&combatCopy.Poses[0].Shots==3&&combatCopy.Poses[0].Mounts[0].Y==0.6f,"combat state and turret rotation round trip");
    combat.Poses[0].Stamp=12.5f;combat.Poses[0].Tracks=new[]{-2f,3f,-40f,60f};combat.Poses[0].WeaponShots=new[]{4,200};
    var visual=Wire.Decode(Wire.Encode(combat)).Poses[0];Check(visual.Stamp==12.5f&&visual.Tracks[0]==-2&&visual.Tracks[3]==60&&visual.WeaponShots[1]==200,"timestamp tracks and separate weapon counters round trip");
    combat.Poses[0].Tracks[0]=float.NaN;Reject(delegate{Wire.Encode(combat);},"NaN track speed rejected");combat.Poses[0].Tracks[0]=0;
    combat.Poses[0].WeaponShots[0]=-1;Reject(delegate{Wire.Encode(combat);},"negative weapon counter rejected");combat.Poses[0].WeaponShots[0]=0;
    combat.Poses[0].Stamp=float.PositiveInfinity;Reject(delegate{Wire.Encode(combat);},"invalid snapshot timestamp rejected");combat.Poses[0].Stamp=0;
    combat.Poses[0].Mounts[0].Y=float.NaN;Reject(delegate{Wire.Encode(combat);},"NaN turret rotation rejected");
    Reject(delegate{Wire.Encode(new Message{Kind=Kind.Snapshot,Poses=new[]{new Pose{Id="a",Qw=1},new Pose{Id="a",Qw=1}}});},"duplicate snapshot IDs");
    Reject(delegate{Wire.ReadFrame(new MemoryStream(BitConverter.GetBytes(Wire.MaxFrame+1)));},"oversized frame rejected before allocation");
    var fx=new Message{Kind=Kind.Effects,Impacts=new[]{new ImpactState{Id=1,Ammo="test",X=42,Fused=1,Surface=2}},Poses=new[]{new Pose{Id="burn",Qw=1,Fires=new[]{new FireState{Id=5,Type=1,Fire=2,Smoke=3,Bursts=4,Burst=1}}}}};
    var fxCopy=Wire.Decode(Wire.Encode(fx));Check(fxCopy.Impacts[0].X==42&&fxCopy.Impacts[0].Ammo=="test"&&fxCopy.Poses[0].Fires[0].Bursts==4&&fxCopy.Poses[0].Fires[0].Smoke==3,"impact and persistent fire state round trip");
    fx.Poses[0].Fires[0].Fire=float.NaN;Reject(delegate{Wire.Encode(fx);},"invalid fire magnitude rejected");fx.Poses[0].Fires[0].Fire=1;
    fx.Impacts[0].Qw=0;Reject(delegate{Wire.Encode(fx);},"invalid impact rotation rejected");fx.Impacts[0].Qw=1;
    fx.Poses[0].Fires=new[]{fx.Poses[0].Fires[0],fx.Poses[0].Fires[0]};Reject(delegate{Wire.Encode(fx);},"duplicate fire identity rejected");
    var weather=new Message{Kind=Kind.Snapshot,Weather=new float[16]};weather.Weather[0]=0.8f;weather.Weather[15]=0.7f;var weatherCopy=Wire.Decode(Wire.Encode(weather));Check(weatherCopy.Weather[0]==0.8f&&weatherCopy.Weather[15]==0.7f,"weather rain and cloud configuration round trip");
    weather.Weather[0]=float.NaN;Reject(delegate{Wire.Encode(weather);},"NaN weather rejected");weather.Weather=new float[15];Reject(delegate{Wire.Encode(weather);},"incomplete weather state rejected");
    var lobby=Host();Check(lobby.Handle(new Message{Kind=Kind.JoinRoom,Token="wrong",Build="build"}).Kind==Kind.Error&&!lobby.LobbyAccepted,"room code required before mission disclosure");
    Check(lobby.Handle(new Message{Kind=Kind.JoinRoom,Token="secret",Build="wrong"}).Kind==Kind.Error,"room rejects incompatible game build");
    Check(lobby.Handle(new Message{Kind=Kind.JoinRoom,Token="secret",Build="build"}).Kind==Kind.Mission&&lobby.LobbyAccepted&&!lobby.Ready,"main menu join returns mission before vehicle handshake");
    Check(lobby.Handle(new Message{Kind=Kind.Ping,Sequence=1}).Kind==Kind.Ping,"authenticated loading heartbeat");
    Check(lobby.Handle(new Message{Kind=Kind.Claim,Unit="tank-b"}).Kind==Kind.Error,"loading guest cannot claim before roster verification");
    Check(lobby.Handle(Hello()).Kind==Kind.Welcome,"loaded guest completes normal roster verification");
    Check(lobby.Handle(new Message{Kind=Kind.Claim,Unit="tank-b"}).Kind==Kind.Claimed,"loaded guest claims offered platoon vehicle");
    lobby.Disconnect();Check(!lobby.LobbyAccepted&&!lobby.Ready,"disconnect clears room authentication");
    var host=Host();Check(host.Handle(new Message{Kind=Kind.Claim,Unit="tank-b"}).Kind==Kind.Error,"claim before handshake");
    var hello=Hello();hello.Token="wrong";Check(host.Handle(hello).Kind==Kind.Error&&!host.Ready,"wrong room code");
    hello=Hello();hello.World="other";Check(host.Handle(hello).Kind==Kind.Error,"different mission");
    hello=Hello();hello.Build="other";Check(host.Handle(hello).Kind==Kind.Error,"different game build");
    hello=Hello();hello.Roster="other";Check(host.Handle(hello).Kind==Kind.Error,"different vehicle roster");
    Check(host.Handle(Hello()).Kind==Kind.Welcome,"valid handshake");
    Check(host.Handle(new Message{Kind=Kind.Claim,Unit="tank-a"}).Kind==Kind.Error,"host vehicle protected");
    Check(host.Handle(new Message{Kind=Kind.Claim,Unit="enemy"}).Kind==Kind.Error,"unlisted vehicle protected");
    Check(host.Handle(new Message{Kind=Kind.Claim,Unit="tank-b"}).Kind==Kind.Claimed,"guest gets different tank");
    Check(host.Handle(new Message{Kind=Kind.Input,Unit="tank-a",Sequence=1}).Kind==Kind.Error,"input ownership enforced");
    Check(host.Handle(new Message{Kind=Kind.Input,Unit="tank-b",Sequence=1,Throttle=1}).Kind==Kind.Ping,"owned input accepted by protocol only");
    Check(host.Handle(new Message{Kind=Kind.Input,Unit="tank-b",Sequence=1}).Kind==Kind.Error,"stale input rejected");
    host.Disconnect();Check(!host.Ready&&host.GuestUnit==null&&host.LastInput==-1,"disconnect releases reservation");
    using(var a=new Link())using(var b=new Link()){
     a.Host(IPAddress.Loopback,0);b.Join("127.0.0.1",a.Port);host=new HostSession("secret","build","mission","roster","tank-a",new[]{"tank-a","tank-b","tank-c"});b.Send(new Message{Kind=Kind.JoinRoom,Token="secret",Build="build"});a.Send(host.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Mission,"real TCP main menu mission offer");b.Send(new Message{Kind=Kind.Ping,Sequence=1});a.Send(host.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Ping,"real TCP loading heartbeat");b.Send(Hello());a.Send(host.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Welcome,"real TCP loopback handshake");
     b.Send(new Message{Kind=Kind.Claim,Unit="tank-b"});a.Send(host.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Claimed,"real TCP separate vehicle claim");
     for(int i=1;i<=100;i++){pose.Sequence=i;a.Send(pose);var received=Wait(b);if(received.Sequence!=i||received.Poses[0].Z!=100)throw new Exception("Lost snapshot");}
     Check(true,"100 real TCP snapshots in order");
     b.Send(new Message{Kind=Kind.SwitchVehicle,Unit="tank-c"});a.Send(host.Handle(Wait(a)));var moved=Wait(b);Check(moved.Kind==Kind.VehicleAssignment&&moved.Text=="tank-c","real TCP vehicle transfer");
     b.Send(new Message{Kind=Kind.SwitchVehicle,Unit="tank-a"});a.Send(host.Handle(Wait(a)));Check(Wait(b).Kind==Kind.SwitchRejected&&a.Connected&&b.Connected,"rejected occupied vehicle keeps connection alive");
     b.Dispose();var until=DateTime.UtcNow.AddSeconds(3);while(a.Connected&&DateTime.UtcNow<until)Thread.Sleep(10);Check(!a.Connected,"disconnect detected");
    }
    using(var a=new Link())using(var b=new Link()){
     a.Host(IPAddress.Loopback,0);b.Join("127.0.0.1",a.Port);
     var direct=new HostSession("","build","mission","roster","tank-a",new[]{"tank-a","tank-b"});
     b.Send(new Message{Kind=Kind.JoinRoom,Token="",Build="build"});a.Send(direct.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Mission,"direct IP empty code mission join");
     var h=Hello();h.Token="";b.Send(h);a.Send(direct.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Welcome,"direct IP empty code handshake");
     b.Send(new Message{Kind=Kind.Claim,Unit="tank-b"});a.Send(direct.Handle(Wait(a)));Check(Wait(b).Kind==Kind.Claimed,"direct IP separate vehicle claim");
    }
    File.WriteAllText("test-report.json","{\"passed\":"+passed+",\"scope\":\"protocol_and_transport_only\",\"game_runtime_tested_by_this_suite\":false,\"two_pc_tested\":false}");
    Console.WriteLine("ALL "+passed+" PASSED. This verifies protocol/transport, not playable co-op.");
   }catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}
  }
 }
}
