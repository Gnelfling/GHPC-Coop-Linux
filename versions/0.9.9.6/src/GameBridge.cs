using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using GHPC;using GHPC.AI;using GHPC.Crew;using GHPC.Player;using GHPC.Weapons;using HarmonyLib;
namespace GhpcCoop {
 public sealed class VehicleRecord {
  public DamageSync Damage; public VehicleAudioSync Audio; public EquipmentSync Equipment; public WeaponSystem[] Weapons; public int[] WeaponShots; public float[] SoundUntil; public NWH.VehiclePhysics.Tracks Tracks; public readonly List<Pose> Frames=new List<Pose>(); public Pose Previous; public float Arrival,RenderStamp; public string Id;public Unit Unit;public AimablePlatform[] Mounts;public int Shots;public Action<AmmoType,LiveRound> ShotHandler;
 }
 public sealed class GameBridge:IDisposable {
  static readonly System.Reflection.FieldInfo[] TrackFields=new[]{"leftTrackVel","rightTrackVel","maxLeftRpm","maxRightRpm"}.Select(n=>AccessTools.Field(typeof(NWH.VehiclePhysics.Tracks),n)).ToArray();
  static readonly System.Reflection.FieldInfo ProjectedGoal=AccessTools.Field(typeof(AimablePlatform),"_projectedGoalLook");
  public CombatVisuals Combat; public static bool ReplicaActive;public static Unit RemoteControlledUnit;public static bool ApplyingRemoteDrive; static readonly System.Reflection.MethodInfo FireMethod=typeof(CrewBrainWeaponsModule).GetMethod("Fire");
  public readonly Dictionary<string,VehicleRecord> Vehicles=new Dictionary<string,VehicleRecord>(StringComparer.Ordinal);
  readonly List<Action> restore=new List<Action>(),guestRestore=new List<Action>();readonly Dictionary<AimablePlatform,bool> replicaMountEnabled=new Dictionary<AimablePlatform,bool>();
  public string World,Roster,LocalId;public string GuestId;public bool Dirty;
  static readonly HashSet<Unit> remoteUnits=new HashSet<Unit>();
  readonly Dictionary<int,GameBridge> peers=new Dictionary<int,GameBridge>();
  public static bool IsRemote(Unit unit){return unit!=null&&remoteUnits.Contains(unit);}
  public static bool IsRemoteChassis(NwhChassis chassis){return remoteUnits.Any(u=>u!=null&&object.ReferenceEquals(u.Chassis,chassis)&&!u.CannotMove);}
  public void ReservePeer(int peer,string id){ReleasePeer(peer);var driver=new GameBridge{LocalId=LocalId,Combat=Combat};foreach(var v in Vehicles)driver.Vehicles.Add(v.Key,v.Value);driver.ReserveGuest(id);peers.Add(peer,driver);}
  public void ReleasePeer(int peer){GameBridge driver;if(peers.TryGetValue(peer,out driver)){peers.Remove(peer);driver.ReleaseGuest();}}
  public void ReceivePeer(int peer,Message m){GameBridge driver;if(!peers.TryGetValue(peer,out driver))throw new InvalidOperationException("Peer has no vehicle");driver.ReceiveInput(m);}
  public void DrivePeer(int id){GameBridge p;if(peers.TryGetValue(id,out p))p.DriveRemote();}
  public void FirePeer(int id){GameBridge p;if(peers.TryGetValue(id,out p))p.FireRemote();}
  public void RenderPeer(int id){GameBridge p;if(peers.TryGetValue(id,out p))p.RenderRemoteMounts();}
  float nextShotLog;float nextInputLog;float clockOffset=float.PositiveInfinity,lastRenderTime,statsStart,previousArrival,maxArrivalGap,maxFrame;int receivedFrames,renderedFrames,starvedFrames; Quaternion[] remoteStart; Message command;float received,nextDriveLog,nextAmmoLog;bool pendingReload;readonly ShotInputLatch shotInputs=new ShotInputLatch();readonly Dictionary<string,Pose> targets=new Dictionary<string,Pose>();
  public static Transform MountTransform(AimablePlatform m){return m.Transform!=null?m.Transform:m.transform;}
  public static string PathOf(Transform t){var parts=new List<string>();while(t!=null){parts.Add(t.name+"#"+t.GetSiblingIndex());t=t.parent;}parts.Reverse();return String.Join("/",parts.ToArray());}
  // Static mission spawn anchors disambiguate legacy root-level ported spawn points.
  // Never derive this key from a spawned vehicle, whose pose changes during play.
  static string SpawnAnchor(Transform t){
   var p=t.position;var r=t.rotation;
   return String.Join(",",new[]{p.x,p.y,p.z,r.x,r.y,r.z,r.w}.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).ToArray());
  }
  public void Capture(){
   var p=PlayerInput.Instance;
   if(p==null)throw new InvalidOperationException("Player controls are not ready. Wait for the mission to finish loading.");
   if(p.CurrentPlayerUnit==null)throw new InvalidOperationException("No player vehicle selected. Enter a mission and select a friendly vehicle.");
   var selected=p.CurrentPlayerUnit;
   if(selected.Chassis==null||!selected.gameObject.scene.IsValid()||!selected.gameObject.scene.isLoaded)throw new InvalidOperationException("Select a ground vehicle in a loaded mission.");
   if(selected.Destroyed)throw new InvalidOperationException("Select a surviving friendly vehicle.");
   // IsMainMenuActive also covers the in-mission pause menu; it is not a mission-readiness test.
   Log("CAPTURE ready initialized="+p.IsInitialized+" menu="+p.IsMainMenuActive+" scene="+selected.gameObject.scene.name+" vehicle="+selected.FriendlyName);
   Dispose();Combat=new CombatVisuals();Vehicles.Clear();LocalId=null;
   World=SceneController.TargetMissionScene+"|"+p.CurrentPlayerUnit.gameObject.scene.name+"|"+SceneController.IsDaytime;
   var all=UnityEngine.Object.FindObjectsOfType<Unit>().Where(u=>u.Chassis!=null).ToArray();
   if(all.Length<2)throw new InvalidOperationException("This mission has only "+all.Length+" active ground vehicle(s). Co-op needs at least two eligible vehicles.");
   if(all.Length>Wire.MaxUnits)throw new InvalidOperationException("Mission contains "+all.Length+" ground vehicles; supported limit is "+Wire.MaxUnits);
   var spawnIds=new Dictionary<Unit,string>();
   foreach(var point in Resources.FindObjectsOfTypeAll<GHPC.Mission.DynamicSpawnPoint>().Where(x=>x.gameObject.scene.IsValid()&&x.gameObject.scene.isLoaded&&x.SpawnedUnits!=null)){
    for(int i=0;i<point.SpawnedUnits.Count;i++){var unit=point.SpawnedUnits[i];if(unit==null||unit.Chassis==null)continue;
     // Authored spawn transforms can share names. Include hierarchy sibling indices;
     // never use a moving vehicle position, instance ID, or runtime spawn counter.
     var key="spawn/"+point.gameObject.scene.name+"/"+PathOf(point.transform)+"/anchor/"+SpawnAnchor(point.transform)+"/slot/"+i;
     string previous;if(spawnIds.TryGetValue(unit,out previous)&&previous!=key)throw new InvalidOperationException("Vehicle belongs to multiple spawn points");spawnIds[unit]=key;
    }
   }
   // Preflight the complete roster before registering any per-vehicle effects or handlers.
   var identities=new Dictionary<Unit,string>();
   foreach(var unit in all){
    string key;
    if(!spawnIds.TryGetValue(unit,out key)){
     var slot=unit.Platoon!=null&&unit.Platoon.Units!=null?unit.Platoon.Units.IndexOf(unit):-1;
     key=slot>=0?"platoon/"+PathOf(unit.Platoon.transform)+"/slot/"+slot:"unit/"+PathOf(unit.transform);
    }
    identities.Add(unit,unit.gameObject.scene.name+"/"+key);
   }
   var duplicates=identities.GroupBy(x=>x.Value,StringComparer.Ordinal).Where(x=>x.Count()>1).ToArray();
   if(duplicates.Length>0){
    var rows=duplicates.SelectMany(g=>g.Select(x=>g.Key+"\t"+x.Key.UniqueName+"\t"+PathOf(x.Key.transform))).ToArray();
    System.IO.Directory.CreateDirectory("UserData/GhpcCoop");
    System.IO.File.WriteAllLines("UserData/GhpcCoop/identity-conflicts.txt",rows);
    throw new InvalidOperationException("Ambiguous vehicle identity: "+duplicates[0].Key+". See identity-conflicts.txt");
   }
   var diagnostics=new List<string>();
   foreach(var u in all){
    var identity=identities[u];
    var id=Wire.Hash(identity);
    if(Vehicles.ContainsKey(id))throw new InvalidOperationException("Ambiguous vehicle identity: "+identity);
    var rec=new VehicleRecord{Id=id,Unit=u,Mounts=(u.AimablePlatforms??new AimablePlatform[0]).Where(x=>x!=null).OrderBy(x=>PathOf(MountTransform(x)),StringComparer.Ordinal).ToArray()};
    rec.Damage=new DamageSync(u);rec.Equipment=new EquipmentSync(u);rec.Audio=new VehicleAudioSync(u);
    rec.Weapons=u.GetComponentsInChildren<WeaponSystem>(true).OrderBy(w=>PathOf(w.transform),StringComparer.Ordinal).ToArray();
    rec.WeaponShots=new int[rec.Weapons.Length];rec.SoundUntil=new float[rec.Weapons.Length];
    var controller=u.GetComponentInChildren<NWH.VehiclePhysics.VehicleController>();rec.Tracks=controller!=null&&controller.tracks!=null&&controller.tracks.trackedVehicle?controller.tracks:null;
    for(int wi=0;wi<rec.Weapons.Length;wi++){int index=wi;var weapon=rec.Weapons[wi];Action<AmmoType,LiveRound> handler=delegate(AmmoType a,LiveRound round){rec.WeaponShots[index]++;ShotAudit.Record(rec.Id,round);};weapon.Fired+=handler;restore.Add(delegate{if(weapon!=null)weapon.Fired-=handler;});}
    if(rec.Weapons.Length>64||rec.Mounts.Length>64)throw new InvalidOperationException("Too many mounts");
    rec.ShotHandler=delegate(AmmoType a,LiveRound r){rec.Shots++;};u.InfoBroker.WeaponFired+=rec.ShotHandler;Vehicles.Add(id,rec);
    diagnostics.Add(id+"\t"+u.UniqueName+"\t"+u.Allegiance+"\t"+identity+"\t"+rec.Mounts.Length+"/"+rec.Weapons.Length+"/"+rec.Damage.Layout);
    if(u==p.CurrentPlayerUnit)LocalId=id;
   }
   if(LocalId==null)throw new InvalidOperationException("Select a ground vehicle");
   System.IO.Directory.CreateDirectory("UserData/GhpcCoop");System.IO.File.WriteAllLines("UserData/GhpcCoop/roster-diagnostic.txt",diagnostics.OrderBy(x=>x,StringComparer.Ordinal).ToArray());
   // Display names may be translated. Match the game's technical vehicle key instead.
   Roster=Wire.Hash(String.Join("\n",Vehicles.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+":"+x.Value.Unit.Allegiance+":"+x.Value.Unit.UniqueName+":"+x.Value.Mounts.Length+":"+x.Value.Weapons.Length+":"+x.Value.Damage.Layout).ToArray()));
  }
  public IEnumerable<string> FriendlyChoices(){var faction=Vehicles[LocalId].Unit.Allegiance;return Vehicles.Where(x=>x.Value.Unit.Allegiance==faction&&x.Value.Unit.CrewManager!=null&&x.Value.Unit.CrewManager.GetCrewBrain(CrewPosition.Driver)!=null).Select(x=>x.Key);}
  public IEnumerable<string> PlatoonChoices(){var host=Vehicles[LocalId].Unit;return FriendlyChoices().Where(id=>id!=LocalId&&(host.Platoon!=null?Vehicles[id].Unit.Platoon==host.Platoon:Vehicles[id].Unit.Platoon==null)&&!Vehicles[id].Unit.Destroyed&&!Vehicles[id].Unit.Abandoned&&!Vehicles[id].Unit.UnitIncapacitated).OrderBy(id=>Vehicles[id].Unit.FriendlyName==host.FriendlyName?0:1).ThenBy(id=>id,StringComparer.Ordinal);}
  public bool Available(string id){VehicleRecord r;return Vehicles.TryGetValue(id,out r)&&r.Unit!=null&&!r.Unit.Destroyed&&!r.Unit.Abandoned&&!r.Unit.UnitIncapacitated&&FriendlyChoices().Contains(id);}
  public static bool IsRemoteCrew(GHPC.Crew.CrewManager crew){return remoteUnits.Any(u=>u!=null&&u.CrewManager==crew);}
  void ManualCrew(Unit u,List<Action> undo=null){
   if(undo==null)undo=restore;
   if(u.CrewManager==null)return;
   foreach(var pos in new[]{CrewPosition.Driver,CrewPosition.Gunner,CrewPosition.Commander}){
    var brain=u.CrewManager.GetCrewBrain(pos);if(brain==null)continue;bool old=brain.IsAutonomous,wasSuspended=brain.Suspended;brain.IsAutonomous=false;brain.Suspended=false;
    undo.Add(delegate{try{brain.IsAutonomous=old;brain.Suspended=wasSuspended;}catch{}});
   }
  }
  public void ReserveGuest(string id){
   if(GuestId==id)return;if(id==LocalId||!Available(id))throw new InvalidOperationException("Invalid remote vehicle");
   ReleaseGuest();GuestId=id;RemoteControlledUnit=Vehicles[id].Unit;remoteUnits.Add(RemoteControlledUnit);CombatVisuals.Host=Combat;Vehicles[id].Equipment.BeginRemoteControl();ManualCrew(Vehicles[id].Unit,guestRestore);
   foreach(var mount in Vehicles[id].Mounts){var captured=mount;bool was=mount.enabled;mount.enabled=false;guestRestore.Add(delegate{if(captured!=null)captured.enabled=was;});}Log("RESERVED "+Vehicles[id].Unit.FriendlyName);
  }
  void ReleaseGuest(){
   if(GuestId!=null&&Vehicles.ContainsKey(GuestId))remoteUnits.Remove(Vehicles[GuestId].Unit);
   RemoteControlledUnit=null;
   if(GuestId!=null&&Vehicles.ContainsKey(GuestId)){var r=Vehicles[GuestId];if(r.Unit!=null)r.Unit.ClearAllInputs();foreach(var w in r.Weapons)if(w!=null)w.StopFiring();}
   for(int i=guestRestore.Count-1;i>=0;i--)try{guestRestore[i]();}catch(Exception e){Log("PEER restore: "+e.Message);}guestRestore.Clear();RemoteControlledUnit=null;GuestId=null;command=null;remoteStart=null;pendingReload=false;shotInputs.Clear();
  }
  public void SelectReplicaVehicle(string id){
   if(!Available(id))throw new InvalidOperationException("Vehicle unavailable");
   LocalId=id;foreach(var r in Vehicles.Values)foreach(var mount in r.Mounts){bool original;if(replicaMountEnabled.TryGetValue(mount,out original))mount.enabled=r.Id==id&&original;}
   PlayerInput.Instance.SetPlayerUnit(Vehicles[id].Unit);
  }
  public void BeginReplica(){
   if(ReplicaActive)return;Dirty=true;ReplicaActive=true;
   foreach(var r in Vehicles.Values){
    var u=r.Unit;ManualCrew(u);Log("BODY "+u.FriendlyName+" root="+u.RootTransform.position+" body="+(u.Chassis.Rigidbody!=null?u.Chassis.Rigidbody.position.ToString():"none")+" same="+(u.Chassis.Rigidbody!=null&&u.Chassis.Rigidbody.transform==u.RootTransform));
    foreach(var platform in r.Mounts){var captured=platform;bool was=platform.enabled;replicaMountEnabled[platform]=was;platform.enabled=r.Id==LocalId&&was;restore.Add(delegate{if(captured!=null)captured.enabled=was;});}
    // NWH must not write physical state over authoritative snapshots.
    foreach(var b in u.GetComponentsInChildren<MonoBehaviour>(true)){
     if(b is NwhChassis||b.GetType().FullName.StartsWith("NWH.VehiclePhysics.",StringComparison.Ordinal)){
      var captured=b;bool was=b.enabled;b.enabled=false;restore.Add(delegate{if(captured!=null)captured.enabled=was;});
     }
    }
    foreach(var body in u.GetComponentsInChildren<Rigidbody>(true)){
     var captured=body;var oldInterpolation=body.interpolation;body.interpolation=RigidbodyInterpolation.None;bool was=body.isKinematic;if(!was){body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;
     restore.Add(delegate{if(captured!=null){captured.isKinematic=was;captured.interpolation=oldInterpolation;}});
    }
   }
   Log("REPLICA started; local weapon fire and AI suppressed");
  }
  public Message ReadInput(long seq,bool reload,bool firePressed){
   var p=PlayerInput.Instance;var w=p.CurrentPlayerUnit==Vehicles[LocalId].Unit?p.CurrentPlayerWeapon:null;var aim=w!=null&&w.FCS!=null?w.FCS.AimWorldVector:p.CurrentPlayerUnit.transform.forward;
   if(aim.sqrMagnitude<0.1f)aim=p.CurrentPlayerUnit.transform.forward;aim.Normalize();
   bool live=p.CurrentPlayerUnit==Vehicles[LocalId].Unit&&p.AllowPlayerStrictLiveInput&&!p.IsMenuOverridingAction;
   if(Time.realtimeSinceStartup>=nextInputLog&&(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.S)||Input.GetMouseButton(0))){nextInputLog=Time.realtimeSinceStartup+2;Log("INPUT live="+live+" strict="+p.AllowPlayerStrictLiveInput+" menu="+p.IsMenuOverridingAction+" focused="+Application.isFocused+" fire="+Input.GetMouseButton(0)+" pause="+GHPC.State.TimeController.Paused+" scale="+Time.timeScale);}
   var rec=Vehicles[LocalId];var muzzle=w!=null&&w.Weapon!=null?w.Weapon.MuzzleIdentity:null;var mq=muzzle!=null?muzzle.rotation:rec.Unit.RootTransform.rotation;var mp=muzzle!=null?muzzle.position:rec.Unit.RootTransform.position;
   return new Message{Kind=Kind.Input,Text="reload:"+GHPC.Utility.SaveLoadUtility.PlayerConfigData.ReloadMode,Sequence=seq,Unit=LocalId,Poses=new[]{new Pose{Id=LocalId,X=mp.x,Y=mp.y,Z=mp.z,Qx=mq.x,Qy=mq.y,Qz=mq.z,Qw=mq.w,Equipment=rec.Equipment.Input(),Mounts=rec.Mounts.Select(m=>{var q=MountTransform(m).localRotation;return new Rotation{X=q.x,Y=q.y,Z=q.z,W=q.w};}).ToArray()}},Throttle=live?((Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)):0,Steer=live?((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0)):0,AimX=aim.x,AimY=aim.y,AimZ=aim.z,Range=w!=null&&w.FCS!=null?Mathf.Clamp(w.FCS.CurrentRange,0,20000):0,Role=w!=null?(int)w.Role:0,Fire=live&&(firePressed||Input.GetMouseButton(0)),Reload=live&&reload};
  }
  public void ReceiveInput(Message m){if(m.Unit!=GuestId||m.Poses.Length!=1||m.Poses[0].Id!=GuestId||m.Poses[0].Mounts.Length!=Vehicles[GuestId].Mounts.Length)throw new InvalidOperationException("Wrong vehicle");remoteStart=Vehicles[GuestId].Mounts.Select(x=>MountTransform(x).localRotation).ToArray();int preference; if(m.Text!=null&&m.Text.StartsWith("reload:",StringComparison.Ordinal)&&int.TryParse(m.Text.Substring(7),out preference)&&preference>=0&&preference<=2)RemoteReloadSettings.Preferences[Vehicles[GuestId].Unit.CrewManager]=preference;shotInputs.Receive(m,Time.realtimeSinceStartup);command=m;received=Time.realtimeSinceStartup;pendingReload|=m.Reload;Vehicles[GuestId].Equipment.ApplyInput(m.Poses[0].Equipment);}
  public void RenderRemoteMounts(bool exact=false){
   if(ReplicaActive||GuestId==null||command==null||remoteStart==null)return;
   var mounts=Vehicles[GuestId].Mounts;float t=exact?1f:Mathf.Clamp01((Time.realtimeSinceStartup-received)/0.05f);
   for(int i=0;i<mounts.Length;i++){var q=command.Poses[0].Mounts[i];var rot=Quaternion.Slerp(remoteStart[i],new Quaternion(q.X,q.Y,q.Z,q.W),t);mounts[i].LocalRotation=rot;var mt=MountTransform(mounts[i]);mt.localRotation=rot;var target=new Quaternion(q.X,q.Y,q.Z,q.W);ProjectedGoal.SetValue(mounts[i],(mt.parent!=null?mt.parent.rotation*target:target)*Vector3.forward);}
  }
  public void DriveRemote(){
   if(GuestId==null)return;var u=Vehicles[GuestId].Unit;if(u==null)return;
   var driver=u.CrewManager.GetCrewBrain(CrewPosition.Driver) as IDriverBrain;
   bool fresh=command!=null&&Time.realtimeSinceStartup-received<0.5f&&!u.Destroyed&&!u.UnitIncapacitated;
   float gas=fresh&&!u.CannotMove?command.Throttle:0,steer=fresh&&!u.CannotMove?command.Steer:0;
   if(driver!=null){ApplyingRemoteDrive=true;try{driver.ForceSpeed((gas<0?u.Chassis.MinReverseSpeed:u.Chassis.MaxForwardSpeed)*gas*gas,gas<0);driver.ForceSteerAmount(steer);}finally{ApplyingRemoteDrive=false;}}
   var nc=u.Chassis as NwhChassis;if(nc!=null&&(gas!=0||steer!=0)&&nc.Rigidbody!=null&&nc.Rigidbody.constraints!=RigidbodyConstraints.None)nc.Unfreeze(3,false);if(fresh&&command.Throttle!=0&&nc!=null&&Time.realtimeSinceStartup>nextDriveLog){var vc=nc.VehicleController;Log("PHYSICS active="+vc.Active+" enabled="+vc.enabled+" gas="+vc.input.Vertical+" handbrake="+vc.input.Handbrake+" cruise="+vc.drivingAssists.cruiseControl.enabled+" target="+vc.drivingAssists.cruiseControl.targetSpeed+" constraints="+nc.Rigidbody.constraints+" steering="+vc.input.Horizontal+" fixed="+Time.fixedDeltaTime);}
   if(fresh&&command.Throttle!=0&&Time.realtimeSinceStartup>nextDriveLog){nextDriveLog=Time.realtimeSinceStartup+2;Log("DRIVE gas="+gas+" intended="+u.Chassis.IntendedSpeed+" actual="+u.Chassis.ForwardVelocity+" rpm="+u.Chassis.CurrentRpm+" gear="+u.Chassis.CurrentGear+" cannotMove="+u.CannotMove+" suspended="+u.CrewManager.GetCrewBrain(CrewPosition.Driver).Suspended+" scale="+Time.timeScale+" paused="+GHPC.State.TimeController.Paused+" kinematic="+u.Chassis.Rigidbody.isKinematic);}
  }
  // Weapon trigger release is counted in rendered frames, so firing must run in Update.
  public void FireRemote(){ var latest=command;command=shotInputs.Select(latest,Time.realtimeSinceStartup);try{FireRemoteCore();}finally{command=latest;} }
  void FireRemoteCore(){
   if(GuestId==null)return;var u=Vehicles[GuestId].Unit;if(u==null)return;
   bool fresh=command!=null&&Time.realtimeSinceStartup-received<0.5f&&!u.Destroyed&&!u.UnitIncapacitated;
   var gunner=u.CrewManager.GetCrewBrain(CrewPosition.Gunner);if(gunner==null||gunner.WeaponsModule==null)return;
   bool requestedFire=fresh&&command.Fire;
   var wm=gunner.WeaponsModule;var role=fresh?(WeaponSystemRole)command.Role:WeaponSystemRole.MainGun;
   foreach(var other in wm.Weapons)if(other.Weapon!=null&&(!fresh||other.Role!=role))other.Weapon.StopFiring();
   var chosen=wm.Weapons.FirstOrDefault(x=>x.Role==role&&x.Weapon!=null);if(chosen==null)return;
   if(fresh&&!u.CannotShoot){wm.ActiveWeapon=chosen;wm.SetCurrentWeaponRange(command.Range);if(chosen.Weapon.FCS!=null)chosen.Weapon.FCS.SetAimVector(new Vector3(command.AimX,command.AimY,command.AimZ),true);RenderRemoteMounts(true);if(pendingReload){if(chosen.Weapon.Feed!=null&&!chosen.Weapon.Feed.Reloading&&!chosen.Weapon.Feed.Cycling){chosen.Weapon.Feed.Reload();Log("RELOAD requested role="+role+" reloading="+chosen.Weapon.Feed.Reloading+" reserve="+chosen.Weapon.Feed.ReserveCount);}pendingReload=false;}
    var feed=chosen.Weapon.Feed as AmmoFeed;if(feed!=null&&Time.realtimeSinceStartup>nextAmmoLog){nextAmmoLog=Time.realtimeSinceStartup+2;Log("AMMO role="+role+" able="+chosen.Weapon.AbleToFire+" gate="+(chosen.Weapon.FCS!=null&&chosen.Weapon.FCS.FiringBlocked)+" fire="+command.Fire+" suspended="+gunner.Suspended+" aimLocked="+wm.AimLockedOutManually+" loaded="+(feed.AmmoTypeInBreech!=null)+" reload="+feed.Reloading+" cycle="+feed.Cycling+" reserve="+feed.ReserveCount+" pause="+feed.ForcePauseReload+" restock="+feed.WaitingOnRestock+" enabled="+feed.enabled+" elapsed="+AccessTools.Field(typeof(AmmoFeed),"_clipFeedTime").GetValue(feed)+" total="+feed.TotalReloadTime+" stage="+AccessTools.Field(typeof(AmmoFeed),"_clipFeedStage").GetValue(feed));} if(requestedFire){int before=Vehicles[GuestId].Shots;var muzzle=chosen.Weapon.MuzzleIdentity;
     var pose=command.Poses[0];var rotation=new Quaternion(pose.Qx,pose.Qy,pose.Qz,pose.Qw);
     // Use the guest's actual barrel direction (including elevation), not the sight
     // aim vector or an interpolated host mount. Keep authoritative muzzle position.
     var saved=muzzle!=null?muzzle.rotation:Quaternion.identity;
     try{if(muzzle!=null){if(Time.realtimeSinceStartup>nextShotLog){nextShotLog=Time.realtimeSinceStartup+2;Log("SHOT alignment role="+role+" correctionDeg="+Quaternion.Angle(muzzle.rotation,rotation.normalized));}muzzle.rotation=rotation.normalized;}ShotAudit.Begin(GuestId,command.Sequence,rotation.normalized*Vector3.forward);FireMethod.Invoke(wm,new object[]{role,u});}
     finally{ShotAudit.End();if(muzzle!=null)muzzle.rotation=saved;}if(Vehicles[GuestId].Shots>before)shotInputs.Fired();}else {chosen.Weapon.StopFiring();wm.StopFiring(role);}}
   else foreach(var w in wm.Weapons)if(w.Weapon!=null)w.Weapon.StopFiring();
  }
  public Pose[] Snapshot(){return Vehicles.Values.Where(r=>r.Unit!=null).Select(r=>{
   var u=r.Unit;var p=u.RootTransform.position;var q=u.RootTransform.rotation;
   return new Pose{Scorch=r.Damage.CaptureScorch(),Health=r.Damage.Capture(),Audio=r.Audio.Capture(),Equipment=r.Equipment.Capture(),Id=r.Id,X=p.x,Y=p.y,Z=p.z,Qx=q.x,Qy=q.y,Qz=q.z,Qw=q.w,Dead=u.Destroyed,Flags=(u.Destroyed?1:0)|(u.Abandoned?2:0)|(u.CannotMove?4:0)|(u.CannotShoot?8:0)|(u.UnitIncapacitated?16:0),Shots=r.Shots,Fires=Combat.Capture(u),Ammo=r.Weapons.Select(AmmoSync.Capture).ToArray(),Stamp=Time.realtimeSinceStartup,Tracks=r.Tracks==null?new float[4]:TrackFields.Select(f=>(float)f.GetValue(r.Tracks)).ToArray(),WeaponShots=(int[])r.WeaponShots.Clone(),Mounts=r.Mounts.Select(m=>{var a=MountTransform(m).localRotation;return new Rotation{X=a.x,Y=a.y,Z=a.z,W=a.w};}).ToArray()};}).ToArray();}
  public void ReceiveSnapshot(Pose[] poses){
   float arrival=Time.realtimeSinceStartup;if(previousArrival>0)maxArrivalGap=Mathf.Max(maxArrivalGap,arrival-previousArrival);previousArrival=arrival;receivedFrames++;
   if(poses.Length>0)clockOffset=Mathf.Min(clockOffset,Time.realtimeSinceStartup-poses[0].Stamp);
   if(poses.Length!=Vehicles.Count)throw new InvalidOperationException("Vehicle spawned/despawned; restart this experimental session");
   foreach(var p in poses){VehicleRecord r;if(!Vehicles.TryGetValue(p.Id,out r)||p.Mounts.Length!=r.Mounts.Length||p.WeaponShots.Length!=r.Weapons.Length||p.Ammo.Length!=r.Weapons.Length)throw new InvalidOperationException("Vehicle layout changed");Pose old;if(targets.TryGetValue(p.Id,out old)){if(p.Stamp<=old.Stamp)continue;r.Previous=old;}else {r.Previous=p;Array.Copy(p.WeaponShots,r.WeaponShots,p.WeaponShots.Length);}r.Arrival=Time.realtimeSinceStartup;r.Frames.Add(p);if(r.Frames.Count>32)r.Frames.RemoveAt(0);targets[p.Id]=p; if(r.Unit!=null)r.Damage.Apply(r.Unit,p);}
  }
  public void RenderReplica(){
   renderedFrames++;maxFrame=Mathf.Max(maxFrame,Time.unscaledDeltaTime);
   float hostNow=Time.realtimeSinceStartup-clockOffset;
   foreach(var pair in targets){var latest=pair.Value;var r=Vehicles[latest.Id];var u=r.Unit;if(u==null||r.Frames.Count==0)continue;
    float renderTime=Mathf.Max(r.RenderStamp,hostNow-(r.Id==LocalId?0.075f:0.30f));r.RenderStamp=renderTime;
    while(r.Frames.Count>2&&r.Frames[1].Stamp<=renderTime)r.Frames.RemoveAt(0);
    if(r.Id==LocalId&&renderTime>latest.Stamp)starvedFrames++;
    var previous=r.Frames[0];var p=r.Frames.Count>1?r.Frames[1]:previous;
    var position=new Vector3(p.X,p.Y,p.Z);var rotation=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var t=u.RootTransform;
    float interval=Mathf.Max(0.001f,p.Stamp-previous.Stamp);
    float factor=Mathf.Clamp01((renderTime-previous.Stamp)/interval);
    var start=new Vector3(previous.X,previous.Y,previous.Z);if(Vector3.Distance(start,position)>20)factor=1;
    var pos=Vector3.Lerp(start,position,factor);var rot=Quaternion.Slerp(new Quaternion(previous.Qx,previous.Qy,previous.Qz,previous.Qw),rotation,factor);
    t.SetPositionAndRotation(pos,rot);if(u.Chassis.Rigidbody!=null&&u.Chassis.Rigidbody.transform==t){u.Chassis.Rigidbody.position=pos;u.Chassis.Rigidbody.rotation=rot;}
    if(p.Id!=LocalId)for(int i=0;i<r.Mounts.Length;i++){var q=p.Mounts[i];var a=previous.Mounts[i];var rotationLocal=Quaternion.Slerp(new Quaternion(a.X,a.Y,a.Z,a.W),new Quaternion(q.X,q.Y,q.Z,q.W),factor);r.Mounts[i].LocalRotation=rotationLocal;MountTransform(r.Mounts[i]).localRotation=rotationLocal;}
    // Authoritative damage is applied on receive, independently of visual interpolation.
    r.Equipment.Apply(latest.Equipment);r.Audio.Apply(latest.Audio);ReplicaCrewAudio.Ensure(u,r.Id==LocalId);
    if(r.Tracks!=null){for(int i=0;i<4;i++)TrackFields[i].SetValue(r.Tracks,Mathf.Lerp(previous.Tracks[i],p.Tracks[i],factor));r.Tracks.UpdateVisual();}
    for(int i=0;i<r.Weapons.Length;i++){
     var weapon=r.Weapons[i];if(weapon==null)continue;AmmoSync.Apply(weapon,latest.Ammo[i]);
     if(latest.WeaponShots[i]>r.WeaponShots[i]){ReplicaCrewAudio.Shot(u,weapon);ReplayShot(weapon);r.SoundUntil[i]=Time.realtimeSinceStartup+0.15f;Log("VISUAL SHOT "+u.FriendlyName+" weapon="+i+" count="+latest.WeaponShots[i]);}
     if(r.SoundUntil[i]>0&&Time.realtimeSinceStartup>r.SoundUntil[i]){if(weapon.SoundController!=null)weapon.SoundController.StopLoop();if(weapon.WeaponSound!=null)weapon.WeaponSound.StopLoop();if(weapon.UsesLoopingEffects)foreach(var fx in weapon.MuzzleEffects)if(fx!=null)fx.Stop();r.SoundUntil[i]=0;}
     r.WeaponShots[i]=latest.WeaponShots[i];
    }
    Combat.Apply(latest.Id,u,latest.Fires);r.Shots=latest.Shots;
   }
   float now=Time.realtimeSinceStartup;if(statsStart==0)statsStart=now;
   if(now-statsStart>=5){float elapsed=now-statsStart;Log("TIMING focused="+Application.isFocused+" cap="+Application.targetFrameRate+" size="+Screen.width+"x"+Screen.height+" fps="+(renderedFrames/elapsed).ToString("F1")+" snapshotsHz="+(receivedFrames/elapsed).ToString("F1")+" worstFrameMs="+(maxFrame*1000).ToString("F1")+" worstReceiveGapMs="+(maxArrivalGap*1000).ToString("F1")+" starved="+starvedFrames+"/"+renderedFrames);statsStart=now;receivedFrames=renderedFrames=starvedFrames=0;maxFrame=maxArrivalGap=0;}
  }
  static void ReplayShot(WeaponSystem weapon){
   if(weapon.SoundController!=null)weapon.SoundController.StartLoop();if(weapon.WeaponSound!=null)weapon.WeaponSound.StartLoop();
   foreach(var fx in weapon.MuzzleEffects)if(fx!=null)fx.Play();
   var muzzle=weapon.MuzzleIdentity;if(muzzle!=null)foreach(var prefab in weapon.MuzzleEffectPrefabs)if(prefab!=null){var fx=UnityEngine.Object.Instantiate(prefab,muzzle.position,muzzle.rotation);UnityEngine.Object.Destroy(fx,15);}
  }
  public void Dispose(){
   foreach(var peer in peers.Keys.ToArray())ReleasePeer(peer);
   ReleaseGuest();
   foreach(var rec in Vehicles.Values)if(ReplicaActive||rec.Id==GuestId)foreach(var weapon in rec.Weapons)if(weapon!=null)weapon.StopFiring();
   if(Combat!=null){Combat.Dispose();Combat=null;}ReplicaActive=false;AmmoSync.Clear();ReplicaCrewAudio.Clear();foreach(var rec in Vehicles.Values)if(rec.Audio!=null)rec.Audio.Dispose();clockOffset=float.PositiveInfinity;lastRenderTime=0;command=null;remoteStart=null;pendingReload=false;shotInputs.Clear();
   if(GuestId!=null&&Vehicles.ContainsKey(GuestId)){var u=Vehicles[GuestId].Unit;if(u!=null)u.ClearAllInputs();}
   RemoteControlledUnit=null;GuestId=null;targets.Clear();
   for(int i=restore.Count-1;i>=0;i--)try{restore[i]();}catch{}restore.Clear();
   foreach(var r in Vehicles.Values)if(r.Equipment!=null)r.Equipment.Dispose();
   foreach(var r in Vehicles.Values)if(r.Unit!=null&&r.Unit.InfoBroker!=null&&r.ShotHandler!=null){r.Unit.InfoBroker.WeaponFired-=r.ShotHandler;r.ShotHandler=null;}
  }
  public float ReplicaError(){float max=0;foreach(var pair in targets){var p=pair.Value;var u=Vehicles[p.Id].Unit;if(u!=null)max=Mathf.Max(max,Vector3.Distance(u.RootTransform.position,new Vector3(p.X,p.Y,p.Z)));}return max;}
  public static void Log(string s){MelonLoader.MelonLogger.Msg("COOP "+s);}
 }
 [HarmonyPatch(typeof(NwhChassis),"SetSpeed")]
 static class RemoteSpeedOwner {static float next;static bool Prefix(NwhChassis __instance,float __0){if(!GameBridge.IsRemoteChassis(__instance)||GameBridge.ApplyingRemoteDrive)return true;if(Time.realtimeSinceStartup>next){next=Time.realtimeSinceStartup+3;GameBridge.Log("BLOCKED local speed overwrite="+__0);}return false;}}
 [HarmonyPatch(typeof(NwhChassis),"SetHeading")]
 static class RemoteHeadingOwner {static bool Prefix(NwhChassis __instance){return !GameBridge.IsRemoteChassis(__instance)||GameBridge.ApplyingRemoteDrive;}}
 [HarmonyPatch(typeof(NwhChassis),"SetTurnAmount",new Type[]{typeof(bool),typeof(float),typeof(bool)})]
 static class RemoteSteerOwner {static bool Prefix(NwhChassis __instance){return !GameBridge.IsRemoteChassis(__instance)||GameBridge.ApplyingRemoteDrive;}}
 [HarmonyPatch(typeof(WeaponSystem),"Fire")]
 static class ClientFireBlock {static bool Prefix(ref bool __result){if(!DamageSync.IsGuest)return true;__result=false;return false;}}
 [HarmonyPatch(typeof(UnitAI),"UpdateAI")]
 static class ClientAiBlock {static bool Prefix(UnitAI __instance){return !DamageSync.IsGuest&&!GameBridge.IsRemote(__instance.Unit);}}
}




