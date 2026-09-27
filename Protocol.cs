using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GhpcCoop {
 public enum Kind : byte { Hello=1, Welcome=2, Claim=3, Claimed=4, Snapshot=5, Input=6, Error=7, Ping=8, Effects=9, JoinRoom=10, Mission=11, SwitchVehicle=12, VehicleAssignment=13, SwitchRejected=14, SupportRequest=15, SupportResult=16, SupportVisual=17, Tracers=18 }
 public sealed class ObjectiveStatus {public int Id,State;public bool Visible;public string Text="";}
 public sealed class Rotation { public float X,Y,Z,W=1; }
 public sealed class FireState { public int Id,Type,Bursts;public float X,Y,Z,Qx,Qy,Qz,Qw=1,Fire,Smoke,Burst; }
 public sealed class ImpactState {public long Id;public string Ammo="";public int Fused,Surface;public bool Ricochet;public float X,Y,Z,Qx,Qy,Qz,Qw=1;}
 public sealed class AmmoState { public string Breech=""; public bool Reloading,Cycling; public int ClipType=-1; public int Clip,Reserve,Stage,CycleStage; public float Time,CycleTime; }
 public sealed class EquipmentState {public int SmokeRequest;public bool Screen;public int[] Salvos=new int[0],Materials=new int[0];public bool[] Lamps=new bool[0];}
 public sealed class Pose {
  public float[] Scorch=new float[0]; public float[] Health=new float[0]; public float[] Audio=new float[8]; public EquipmentState Equipment=new EquipmentState();
  public string Id=""; public float X,Y,Z,Qx,Qy,Qz,Qw; public bool Dead;
  public AmmoState[] Ammo=new AmmoState[0]; public FireState[] Fires=new FireState[0]; public float Stamp; public float[] Tracks=new float[4]; public int[] WeaponShots=new int[0]; public int Flags,Shots; public Rotation[] Mounts=new Rotation[0];
 }
 public sealed class SupportState { public string Id=""; public int Missions; public bool Firing; public float Cooldown,Delay,Impact; }
 public sealed class TracerState { public int Id,Visual; public float X,Y,Z,EndX,EndY,EndZ,Duration; }
 public sealed class Message {
  public SupportState[] Supports=new SupportState[0]; public TracerState[] Tracers=new TracerState[0]; public string SupportId="";public int Munition;public float SupportX,SupportY,SupportZ; public bool SupportAccepted;
  public ObjectiveStatus[] Objectives=new ObjectiveStatus[0];
  public bool HasSky,Daytime;public double SkyTime;public float SkyRate;
  public Kind Kind; public long Sequence;
  public string Token="",Build="",World="",Roster="",Unit="",Text="";
  public float Throttle,Steer,AimX,AimY,AimZ=1,Range; public bool Fire,Reload; public int Role;
  public float[] Weather=new float[0]; public ImpactState[] Impacts=new ImpactState[0]; public Pose[] Poses=new Pose[0];
 }
 public static class Wire {
  public const int Version=15, MaxFrame=262144, MaxUnits=256;
  public static string Hash(string text) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-",""); }
  // Day/night is mutable environment state, not a different mission identity.
  // Only relax the well-formed trailing boolean; mission and terrain stay exact.
  public static bool SameMissionWorld(string a,string b){
   if(String.IsNullOrEmpty(a)||String.IsNullOrEmpty(b))return false;
   if(String.Equals(a,b,StringComparison.Ordinal))return true;
   var x=a.Split('|');var y=b.Split('|');bool dx,dy;
   return x.Length==3&&y.Length==3&&x[0].Length>0&&x[1].Length>0&&
    x[0]==y[0]&&x[1]==y[1]&&Boolean.TryParse(x[2],out dx)&&Boolean.TryParse(y[2],out dy);
  }
  public static string NewToken() { var b=new byte[16];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return BitConverter.ToString(b).Replace("-",""); }
  static void Str(BinaryWriter w,string s){if(s==null||Encoding.UTF8.GetByteCount(s)>4096)throw new InvalidDataException("Text too long");w.Write(s);}
  static string Str(BinaryReader r){var s=r.ReadString();if(Encoding.UTF8.GetByteCount(s)>4096)throw new InvalidDataException("Text too long");return s;}
  static void Finite(float v){if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Non-finite coordinate");}
  public static void Validate(Message m){
   if(!Enum.IsDefined(typeof(Kind),m.Kind)||m.Sequence<0)throw new InvalidDataException("Invalid header");
   if(double.IsNaN(m.SkyTime)||double.IsInfinity(m.SkyTime)||Math.Abs(m.SkyTime)>1e12)throw new InvalidDataException("Invalid sky time");Finite(m.SkyRate);if(Math.Abs(m.SkyRate)>100000)throw new InvalidDataException("Invalid sky rate");
   if(m.Objectives==null||m.Objectives.Length>64)throw new InvalidDataException("Too many objectives");
   var objectiveIds=new HashSet<int>();foreach(var o in m.Objectives)if(o==null||o.Id<0||o.Id>1024||!objectiveIds.Add(o.Id)||o.State<0||o.State>5||o.Text==null||Encoding.UTF8.GetByteCount(o.Text)>2048)throw new InvalidDataException("Invalid objective");
   Finite(m.SupportX);Finite(m.SupportY);Finite(m.SupportZ);
   if(Math.Abs(m.SupportX)>100000||Math.Abs(m.SupportY)>100000||Math.Abs(m.SupportZ)>100000||m.SupportId==null||m.SupportId.Length>128||m.Munition<0||m.Munition>3)throw new InvalidDataException("Invalid support request");
   if(m.Supports==null||m.Supports.Length>64||m.Tracers==null||m.Tracers.Length>512)throw new InvalidDataException("Invalid support/tracer count");
   var supportIds=new HashSet<string>();foreach(var b in m.Supports){if(b==null||String.IsNullOrEmpty(b.Id)||b.Id.Length>128||!supportIds.Add(b.Id)||b.Missions< -1||b.Missions>100000)throw new InvalidDataException("Invalid battery");foreach(var v in new[]{b.Cooldown,b.Delay,b.Impact}){Finite(v);if(Math.Abs(v)>100000)throw new InvalidDataException("Invalid battery timer");}}
   var tracerIds=new HashSet<int>();foreach(var t in m.Tracers){if(t==null||t.Id<0||!tracerIds.Add(t.Id)||t.Visual<0||t.Visual>32)throw new InvalidDataException("Invalid tracer");foreach(var v in new[]{t.X,t.Y,t.Z,t.EndX,t.EndY,t.EndZ}){Finite(v);if(Math.Abs(v)>100000)throw new InvalidDataException("Invalid tracer position");}Finite(t.Duration);if(t.Duration<=0||t.Duration>1)throw new InvalidDataException("Invalid tracer duration");}
   Finite(m.Throttle);Finite(m.Steer);
   Finite(m.AimX);Finite(m.AimY);Finite(m.AimZ);Finite(m.Range);
   var aim=m.AimX*m.AimX+m.AimY*m.AimY+m.AimZ*m.AimZ;
   if(aim<0.8f||aim>1.2f||m.Range<0||m.Range>20000||m.Role<0||m.Role>6)throw new InvalidDataException("Invalid weapon input");
   if(Math.Abs(m.Throttle)>1||Math.Abs(m.Steer)>1||m.Poses==null||m.Poses.Length>MaxUnits)throw new InvalidDataException("Invalid input/count");
   if(m.Impacts==null||m.Impacts.Length>64)throw new InvalidDataException("Too many impacts");foreach(var e in m.Impacts){if(e==null||e.Id<1||String.IsNullOrEmpty(e.Ammo)||e.Fused<0||e.Fused>16||e.Surface<0||e.Surface>32)throw new InvalidDataException("Invalid impact");CheckPose(e.X,e.Y,e.Z,e.Qx,e.Qy,e.Qz,e.Qw);}
   if(m.Weather==null||(m.Weather.Length!=0&&m.Weather.Length!=16))throw new InvalidDataException("Invalid weather count");foreach(float v in m.Weather){Finite(v);if(Math.Abs(v)>100000)throw new InvalidDataException("Invalid weather value");}
   var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(var p in m.Poses){
    if(p==null||String.IsNullOrEmpty(p.Id)||!ids.Add(p.Id))throw new InvalidDataException("Duplicate/missing unit");
    ValidateEquipment(p.Equipment);if(p.Health==null||p.Health.Length>512)throw new InvalidDataException("Invalid component count");foreach(float h in p.Health){Finite(h);if(h<0||h>1)throw new InvalidDataException("Invalid component health");} if(p.Audio==null||p.Audio.Length!=8)throw new InvalidDataException("Invalid audio state");foreach(float v in p.Audio){Finite(v);if(v<0||v>100000)throw new InvalidDataException("Invalid audio value");}
    Finite(p.X);Finite(p.Y);Finite(p.Z);Finite(p.Qx);Finite(p.Qy);Finite(p.Qz);Finite(p.Qw);
    var n=p.Qx*p.Qx+p.Qy*p.Qy+p.Qz*p.Qz+p.Qw*p.Qw;if(n<0.8f||n>1.2f)throw new InvalidDataException("Invalid rotation");
    if(p.Fires==null||p.Fires.Length>128)throw new InvalidDataException("Too many fires");var fireIds=new HashSet<int>();foreach(var f in p.Fires){if(f==null||f.Id<0||!fireIds.Add(f.Id)||f.Type<0||f.Type>128||f.Bursts<0)throw new InvalidDataException("Invalid fire");CheckPose(f.X,f.Y,f.Z,f.Qx,f.Qy,f.Qz,f.Qw);foreach(float value in new[]{f.Fire,f.Smoke,f.Burst}){Finite(value);if(value<0||value>10000)throw new InvalidDataException("Invalid flame size");}}
    if(p.Scorch==null||p.Scorch.Length>64)throw new InvalidDataException("Invalid scorch count");foreach(var scorch in p.Scorch){Finite(scorch);if(scorch<0||scorch>1)throw new InvalidDataException("Invalid scorch ratio");}
    if(p.Ammo==null||p.Ammo.Length>64)throw new InvalidDataException("Invalid ammo count");foreach(var a in p.Ammo){if(a==null||a.Breech==null||a.Breech.Length>1024||a.ClipType < -1||a.ClipType>1024||a.Clip<0||a.Clip>100000||a.Reserve<0||a.Reserve>100000||a.Stage<0||a.Stage>256||a.CycleStage<0||a.CycleStage>256)throw new InvalidDataException("Invalid ammo state");Finite(a.Time);Finite(a.CycleTime);if(a.Time<0||a.CycleTime<0)throw new InvalidDataException("Invalid reload time");}Finite(p.Stamp);if(p.Stamp<0||p.Tracks==null||p.Tracks.Length!=4||p.WeaponShots==null||p.WeaponShots.Length>64)throw new InvalidDataException("Invalid visual state");foreach(var speed in p.Tracks){Finite(speed);if(Math.Abs(speed)>100000)throw new InvalidDataException("Invalid track speed");}foreach(var shots in p.WeaponShots)if(shots<0)throw new InvalidDataException("Invalid weapon counter");
    if(p.Mounts==null||p.Mounts.Length>64||p.Flags<0||p.Flags>31||p.Shots<0)throw new InvalidDataException("Invalid state");
    foreach(var q in p.Mounts){Finite(q.X);Finite(q.Y);Finite(q.Z);Finite(q.W);float qn=q.X*q.X+q.Y*q.Y+q.Z*q.Z+q.W*q.W;if(qn<0.8f||qn>1.2f)throw new InvalidDataException("Invalid mount rotation");}
   }
  }
  static void ValidateEquipment(EquipmentState e){
   if(e==null||e.SmokeRequest<0||e.Salvos==null||e.Salvos.Length>64||e.Materials==null||e.Materials.Length>128||e.Lamps==null||e.Lamps.Length>128)throw new InvalidDataException("Invalid equipment state");
   foreach(int n in e.Salvos)if(n<0||n>100000)throw new InvalidDataException("Invalid smoke counter");foreach(int n in e.Materials)if(n< -1||n>1024)throw new InvalidDataException("Invalid material variant");
  }
  static void WriteEquipment(BinaryWriter w,EquipmentState e){w.Write(e.SmokeRequest);w.Write(e.Screen);w.Write(e.Salvos.Length);foreach(int n in e.Salvos)w.Write(n);w.Write(e.Materials.Length);foreach(int n in e.Materials)w.Write(n);w.Write(e.Lamps.Length);foreach(bool b in e.Lamps)w.Write(b);}
  static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException("Invalid equipment count");return n;}
  static float[] ReadHealth(BinaryReader r){var values=new float[Count(r,512)];for(int i=0;i<values.Length;i++)values[i]=r.ReadSingle();return values;}
  static EquipmentState ReadEquipment(BinaryReader r){var e=new EquipmentState{SmokeRequest=r.ReadInt32(),Screen=r.ReadBoolean()};e.Salvos=new int[Count(r,64)];for(int i=0;i<e.Salvos.Length;i++)e.Salvos[i]=r.ReadInt32();e.Materials=new int[Count(r,128)];for(int i=0;i<e.Materials.Length;i++)e.Materials[i]=r.ReadInt32();e.Lamps=new bool[Count(r,128)];for(int i=0;i<e.Lamps.Length;i++)e.Lamps[i]=r.ReadBoolean();return e;}
  static void CheckPose(float x,float y,float z,float qx,float qy,float qz,float qw){foreach(float v in new[]{x,y,z,qx,qy,qz,qw})Finite(v);float n=qx*qx+qy*qy+qz*qz+qw*qw;if(n<0.8f||n>1.2f)throw new InvalidDataException("Invalid effect rotation");}
  public static byte[] Encode(Message m){
   Validate(m);
   using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
    w.Write(0x43504847);w.Write(Version);w.Write((byte)m.Kind);w.Write(m.Sequence);w.Write(m.HasSky);w.Write(m.Daytime);w.Write(m.SkyTime);w.Write(m.SkyRate);
    Str(w,m.Token);Str(w,m.Build);Str(w,m.World);Str(w,m.Roster);Str(w,m.Unit);Str(w,m.Text);
    w.Write(m.Throttle);w.Write(m.Steer);w.Write(m.AimX);w.Write(m.AimY);w.Write(m.AimZ);w.Write(m.Range);w.Write(m.Fire);w.Write(m.Reload);w.Write(m.Role);w.Write(m.Poses.Length);
    foreach(var p in m.Poses){WriteEquipment(w,p.Equipment);w.Write(p.Scorch.Length);foreach(float scorch in p.Scorch)w.Write(scorch);w.Write(p.Health.Length);foreach(float h in p.Health)w.Write(h);foreach(float v in p.Audio)w.Write(v);Str(w,p.Id);w.Write(p.X);w.Write(p.Y);w.Write(p.Z);w.Write(p.Qx);w.Write(p.Qy);w.Write(p.Qz);w.Write(p.Qw);w.Write(p.Dead);w.Write(p.Flags);w.Write(p.Shots);w.Write(p.Fires.Length);foreach(var f in p.Fires){w.Write(f.Id);w.Write(f.Type);w.Write(f.Bursts);w.Write(f.X);w.Write(f.Y);w.Write(f.Z);w.Write(f.Qx);w.Write(f.Qy);w.Write(f.Qz);w.Write(f.Qw);w.Write(f.Fire);w.Write(f.Smoke);w.Write(f.Burst);}w.Write(p.Ammo.Length);foreach(var a in p.Ammo){Str(w,a.Breech);w.Write(a.ClipType);w.Write(a.Reloading);w.Write(a.Cycling);w.Write(a.Clip);w.Write(a.Reserve);w.Write(a.Stage);w.Write(a.CycleStage);w.Write(a.Time);w.Write(a.CycleTime);}w.Write(p.Stamp);foreach(var speed in p.Tracks)w.Write(speed);w.Write(p.WeaponShots.Length);foreach(var shots in p.WeaponShots)w.Write(shots);w.Write(p.Mounts.Length);foreach(var q in p.Mounts){w.Write(q.X);w.Write(q.Y);w.Write(q.Z);w.Write(q.W);}}
    w.Write(m.Impacts.Length);foreach(var e in m.Impacts){w.Write(e.Id);Str(w,e.Ammo);w.Write(e.Fused);w.Write(e.Surface);w.Write(e.Ricochet);w.Write(e.X);w.Write(e.Y);w.Write(e.Z);w.Write(e.Qx);w.Write(e.Qy);w.Write(e.Qz);w.Write(e.Qw);}
    w.Write(m.Weather.Length);foreach(float v in m.Weather)w.Write(v);
    w.Write(m.Objectives.Length);foreach(var o in m.Objectives){w.Write(o.Id);w.Write(o.State);w.Write(o.Visible);Str(w,o.Text);}
    Str(w,m.SupportId);w.Write(m.Munition);w.Write(m.SupportX);w.Write(m.SupportY);w.Write(m.SupportZ);w.Write(m.SupportAccepted);
    w.Write(m.Supports.Length);foreach(var b in m.Supports){Str(w,b.Id);w.Write(b.Missions);w.Write(b.Firing);w.Write(b.Cooldown);w.Write(b.Delay);w.Write(b.Impact);}
    w.Write(m.Tracers.Length);foreach(var t in m.Tracers){w.Write(t.Id);w.Write(t.Visual);w.Write(t.X);w.Write(t.Y);w.Write(t.Z);w.Write(t.EndX);w.Write(t.EndY);w.Write(t.EndZ);w.Write(t.Duration);}
    w.Flush();if(s.Length>MaxFrame)throw new InvalidDataException("Frame too large");return s.ToArray();
   }
  }
  public static Message Decode(byte[] data){
   if(data==null||data.Length>MaxFrame)throw new InvalidDataException("Frame too large");
   using(var s=new MemoryStream(data))using(var r=new BinaryReader(s)){
    if(r.ReadInt32()!=0x43504847||r.ReadInt32()!=Version)throw new InvalidDataException("Protocol mismatch");
    var m=new Message{Kind=(Kind)r.ReadByte(),Sequence=r.ReadInt64(),HasSky=r.ReadBoolean(),Daytime=r.ReadBoolean(),SkyTime=r.ReadDouble(),SkyRate=r.ReadSingle(),Token=Str(r),Build=Str(r),World=Str(r),Roster=Str(r),Unit=Str(r),Text=Str(r),Throttle=r.ReadSingle(),Steer=r.ReadSingle(),AimX=r.ReadSingle(),AimY=r.ReadSingle(),AimZ=r.ReadSingle(),Range=r.ReadSingle(),Fire=r.ReadBoolean(),Reload=r.ReadBoolean(),Role=r.ReadInt32()};
    int count=r.ReadInt32();if(count<0||count>MaxUnits)throw new InvalidDataException("Invalid unit count");m.Poses=new Pose[count];
    for(int i=0;i<count;i++){var p=new Pose{Equipment=ReadEquipment(r),Scorch=ReadHealth(r),Health=ReadHealth(r),Audio=new[]{r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()},Id=Str(r),X=r.ReadSingle(),Y=r.ReadSingle(),Z=r.ReadSingle(),Qx=r.ReadSingle(),Qy=r.ReadSingle(),Qz=r.ReadSingle(),Qw=r.ReadSingle(),Dead=r.ReadBoolean(),Flags=r.ReadInt32(),Shots=r.ReadInt32()};int fires=r.ReadInt32();if(fires<0||fires>128)throw new InvalidDataException("Too many fires");p.Fires=new FireState[fires];for(int j=0;j<fires;j++)p.Fires[j]=new FireState{Id=r.ReadInt32(),Type=r.ReadInt32(),Bursts=r.ReadInt32(),X=r.ReadSingle(),Y=r.ReadSingle(),Z=r.ReadSingle(),Qx=r.ReadSingle(),Qy=r.ReadSingle(),Qz=r.ReadSingle(),Qw=r.ReadSingle(),Fire=r.ReadSingle(),Smoke=r.ReadSingle(),Burst=r.ReadSingle()};int feeds=r.ReadInt32();if(feeds<0||feeds>64)throw new InvalidDataException("Invalid ammo count");p.Ammo=new AmmoState[feeds];for(int j=0;j<feeds;j++)p.Ammo[j]=new AmmoState{Breech=Str(r),ClipType=r.ReadInt32(),Reloading=r.ReadBoolean(),Cycling=r.ReadBoolean(),Clip=r.ReadInt32(),Reserve=r.ReadInt32(),Stage=r.ReadInt32(),CycleStage=r.ReadInt32(),Time=r.ReadSingle(),CycleTime=r.ReadSingle()};p.Stamp=r.ReadSingle();for(int j=0;j<4;j++)p.Tracks[j]=r.ReadSingle();int weapons=r.ReadInt32();if(weapons<0||weapons>64)throw new InvalidDataException("Too many weapons");p.WeaponShots=new int[weapons];for(int j=0;j<weapons;j++)p.WeaponShots[j]=r.ReadInt32();int mounts=r.ReadInt32();if(mounts<0||mounts>64)throw new InvalidDataException("Too many mounts");p.Mounts=new Rotation[mounts];for(int j=0;j<mounts;j++)p.Mounts[j]=new Rotation{X=r.ReadSingle(),Y=r.ReadSingle(),Z=r.ReadSingle(),W=r.ReadSingle()};m.Poses[i]=p;}
    int impacts=r.ReadInt32();if(impacts<0||impacts>64)throw new InvalidDataException("Too many impacts");m.Impacts=new ImpactState[impacts];for(int j=0;j<impacts;j++)m.Impacts[j]=new ImpactState{Id=r.ReadInt64(),Ammo=Str(r),Fused=r.ReadInt32(),Surface=r.ReadInt32(),Ricochet=r.ReadBoolean(),X=r.ReadSingle(),Y=r.ReadSingle(),Z=r.ReadSingle(),Qx=r.ReadSingle(),Qy=r.ReadSingle(),Qz=r.ReadSingle(),Qw=r.ReadSingle()};
    int weather=r.ReadInt32();if(weather!=0&&weather!=16)throw new InvalidDataException("Invalid weather count");m.Weather=new float[weather];for(int j=0;j<weather;j++)m.Weather[j]=r.ReadSingle();
    int objectives=r.ReadInt32();if(objectives<0||objectives>64)throw new InvalidDataException("Too many objectives");m.Objectives=new ObjectiveStatus[objectives];for(int i=0;i<objectives;i++)m.Objectives[i]=new ObjectiveStatus{Id=r.ReadInt32(),State=r.ReadInt32(),Visible=r.ReadBoolean(),Text=Str(r)};
    m.SupportId=Str(r);m.Munition=r.ReadInt32();m.SupportX=r.ReadSingle();m.SupportY=r.ReadSingle();m.SupportZ=r.ReadSingle();m.SupportAccepted=r.ReadBoolean();
    m.Supports=new SupportState[Count(r,64)];for(int i=0;i<m.Supports.Length;i++)m.Supports[i]=new SupportState{Id=Str(r),Missions=r.ReadInt32(),Firing=r.ReadBoolean(),Cooldown=r.ReadSingle(),Delay=r.ReadSingle(),Impact=r.ReadSingle()};
    m.Tracers=new TracerState[Count(r,512)];for(int i=0;i<m.Tracers.Length;i++)m.Tracers[i]=new TracerState{Id=r.ReadInt32(),Visual=r.ReadInt32(),X=r.ReadSingle(),Y=r.ReadSingle(),Z=r.ReadSingle(),EndX=r.ReadSingle(),EndY=r.ReadSingle(),EndZ=r.ReadSingle(),Duration=r.ReadSingle()};
    if(s.Position!=s.Length)throw new InvalidDataException("Trailing data");Validate(m);return m;
   }
  }
  public static byte[] ReadFrame(Stream s){
   var head=ReadExact(s,4);int n=BitConverter.ToInt32(head,0);if(n<1||n>MaxFrame)throw new InvalidDataException("Invalid frame length");return ReadExact(s,n);
  }
  static byte[] ReadExact(Stream s,int n){var b=new byte[n];int at=0;while(at<n){int got=s.Read(b,at,n-at);if(got==0)throw new EndOfStreamException();at+=got;}return b;}
  public static void WriteFrame(Stream s,byte[] b){if(b.Length<1||b.Length>MaxFrame)throw new InvalidDataException();var h=BitConverter.GetBytes(b.Length);s.Write(h,0,h.Length);s.Write(b,0,b.Length);s.Flush();}
 }
 public sealed class HostSession {
  readonly string token,build,world,roster;string hostUnit;readonly HashSet<string> allowed;
  public bool LobbyAccepted{get;private set;} public bool Ready{get;private set;} public string GuestUnit{get;private set;} public long LastInput{get;private set;}
  public HostSession(string token,string build,string world,string roster,string hostUnit,IEnumerable<string> allowed){this.token=token;this.build=build;this.world=world;this.roster=roster;this.hostUnit=hostUnit;this.allowed=new HashSet<string>(allowed,StringComparer.Ordinal);LastInput=-1;}
  Message Error(string s){return new Message{Kind=Kind.Error,Text=s};}
  public bool MoveHost(string unit){if(unit==GuestUnit||!allowed.Contains(unit))return false;hostUnit=unit;return true;}
  public Message Assignment(){return new Message{Kind=Kind.VehicleAssignment,Unit=hostUnit,Text=GuestUnit??""};}
  public Message Handle(Message m){
   Wire.Validate(m);
   if(m.Kind==Kind.JoinRoom){
    if(Ready||LobbyAccepted)return Error("Room join already in progress");
    if(m.Token!=token||m.Build!=build)return Error("Room code or game build differs");
    LobbyAccepted=true;return new Message{Kind=Kind.Mission,Build=build,World=world,Roster=roster};
   }
   if(m.Kind==Kind.Ping&&LobbyAccepted&&!Ready)return new Message{Kind=Kind.Ping,Sequence=m.Sequence};
   if(m.Kind==Kind.Hello){
    if(Ready)return Error("Session already authenticated");
    if(m.Token!=token||m.Build!=build||m.World!=world||m.Roster!=roster)return Error("Room code, game build, mission, or vehicle roster differs");
    Ready=true;return new Message{Kind=Kind.Welcome,Build=build,World=world,Roster=roster,Unit=hostUnit};
   }
   if(!Ready)return Error("Handshake required");
   if(m.Kind==Kind.Claim){
    if(m.Unit==hostUnit||!allowed.Contains(m.Unit))return Error("Select a different friendly vehicle");
    if(GuestUnit!=null&&GuestUnit!=m.Unit)return Error("Vehicle already reserved; reconnect to change");
    GuestUnit=m.Unit;return new Message{Kind=Kind.Claimed,Unit=GuestUnit};
   }
   if(m.Kind==Kind.SwitchVehicle){
    if(GuestUnit==null||m.Unit==hostUnit||!allowed.Contains(m.Unit))return new Message{Kind=Kind.SwitchRejected,Text="Vehicle unavailable or occupied by host"};
    GuestUnit=m.Unit;return Assignment();
   }
   if(m.Kind==Kind.SupportRequest){if(GuestUnit==null||m.Unit!=GuestUnit||m.Sequence<=LastInput)return Error("Unowned support request");LastInput=m.Sequence;return new Message{Kind=Kind.Ping,Sequence=m.Sequence};}
   if(m.Kind==Kind.Input){
    if(GuestUnit==null||m.Unit!=GuestUnit||m.Sequence<=LastInput)return Error("Unowned vehicle or stale input");
    LastInput=m.Sequence;return new Message{Kind=Kind.Ping,Sequence=m.Sequence};
   }
   if(m.Kind==Kind.Ping)return new Message{Kind=Kind.Ping,Sequence=m.Sequence};
   return Error("Unsupported client message");
  }
  public void Disconnect(){LobbyAccepted=false;Ready=false;GuestUnit=null;LastInput=-1;}
 }
}
