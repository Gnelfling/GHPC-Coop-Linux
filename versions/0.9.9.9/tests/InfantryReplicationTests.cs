using System;
using System.IO;
using System.Linq;
using GhpcCoop;
class InfantryReplicationTests
{
 static int count;
 static void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
 static Pose Soldier(string id){return new Pose{Id=id,Qw=1,Flags=1,X=123,Y=4,Z=456,Health=new[]{1f,.4f},Ammo=new[]{new AmmoState{Breech="seat"}},Tracks=new[]{1f,0f,2f,0f}};}
 static void Reject(Message m){bool failed=false;try{Wire.Encode(m);}catch(InvalidDataException){failed=true;}Check(failed,"malformed state accepted");}
 static HostSession Session(){var s=new HostSession("t","b","w","r","host",new[]{"guest"});s.Handle(new Message{Kind=Kind.Hello,Token="t",Build="b",World="w",Roster="r"});s.Handle(new Message{Kind=Kind.Claim,Unit="guest"});return s;}
 static void Main(){
  var m=new Message{Kind=Kind.Infantry,Sequence=7,Poses=new[]{Soldier("a"),Soldier("b")}};
  m.Poses[1].Dead=true;m.Poses[1].Ammo[0].Breech="";
  var copy=Wire.Decode(Wire.Encode(m));Check(copy.Sequence==7&&copy.Poses.Length==2,"batch");Check(copy.Poses[0].X==123&&copy.Poses[0].Tracks[2]==2,"motion");Check(copy.Poses[1].Dead&&copy.Poses[1].Ammo[0].Breech=="","dismounted death");Check(copy.Poses[0].Health[1]==.4f,"health");
  m.Poses=Enumerable.Range(0,33).Select(i=>Soldier(i.ToString())).ToArray();Reject(m);
  m.Poses=new[]{Soldier("a")};m.Poses[0].Ammo=new AmmoState[0];Reject(m);
  m.Poses[0]=Soldier("a");m.Poses[0].Flags=2;Reject(m);
  m.Poses[0]=Soldier("a");m.Poses[0].X=float.NaN;Reject(m);
  m.Poses[0]=Soldier("a");m.Poses[0].Health=new float[129];Reject(m);
  var s=Session();Check(s.Handle(new Message{Kind=Kind.InfantryOrder,Unit="guest",Sequence=1}).Kind==Kind.Ping,"owned deploy");Check(s.Handle(new Message{Kind=Kind.InfantryOrder,Unit="guest",Sequence=1}).Kind==Kind.Error,"replayed deploy");Check(s.Handle(new Message{Kind=Kind.InfantryOrder,Unit="host",Sequence=2}).Kind==Kind.Error,"unowned deploy");Check(s.Handle(new Message{Kind=Kind.InfantryOrder,Unit="guest",Sequence=2,Fire=true}).Kind==Kind.Ping,"owned recall");
  var pre=new HostSession("t","b","w","r","host",new[]{"guest"});Check(pre.Handle(new Message{Kind=Kind.InfantryOrder,Unit="guest",Sequence=1}).Kind==Kind.Error,"pre handshake");
  Console.WriteLine("PASS "+count+" infantry replication protocol checks; not Unity gameplay");
 }
}
