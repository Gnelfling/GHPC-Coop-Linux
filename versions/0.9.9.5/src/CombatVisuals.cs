using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using GHPC;using GHPC.Effects;using HarmonyLib;
namespace GhpcCoop {
 public sealed class CombatVisuals:IDisposable {
  public static CombatVisuals Host;public static bool Replaying;
  sealed class ExitRecord {public int Id,Count;public float Burst;}
  sealed class Visual {public GameObject FireObject,SmokeObject;public IFireParticleSystem Fire,Smoke;public int Bursts;public float FireRatio=-1,SmokeRatio=-1;}
  sealed class Pending {public ImpactState State;public GameObject Object;}
  readonly Dictionary<CompartmentExit,ExitRecord> exits=new Dictionary<CompartmentExit,ExitRecord>();
  readonly Dictionary<string,Visual> visuals=new Dictionary<string,Visual>();
  readonly Dictionary<string,AmmoType> ammo=new Dictionary<string,AmmoType>();
  readonly Queue<Pending> pending=new Queue<Pending>();
  static readonly System.Reflection.FieldInfo FireRatio=AccessTools.Field(typeof(CompartmentExit),"_fireRatio"),SmokeRatio=AccessTools.Field(typeof(CompartmentExit),"_smokeRatio");
  long sequence,lastImpact;int nextExit;
  // Display names are translated by language patches; they are not network IDs.
  static string AmmoKey(AmmoType a){return "ammo-v2:"+Wire.Hash(String.Join("|",new[]{
   ((int)a.Category).ToString(),((int)a.VisualType).ToString(),
   a.Caliber.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
   a.Mass.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
   a.MuzzleVelocity.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
   a.TntEquivalentKg.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
   a.DetonateEffect==null?"":a.DetonateEffect.name,a.TerrainImpactEffect==null?"":a.TerrainImpactEffect.name}));}
  public CombatVisuals(){foreach(var codex in Resources.FindObjectsOfTypeAll<GHPC.Weaponry.AmmoCodexScriptable>()){var a=codex.AmmoType;if(a!=null)ammo[AmmoKey(a)]=a;}}
  public void Burst(CompartmentExit exit,float size){ExitRecord r;if(!exits.TryGetValue(exit,out r))exits[exit]=r=new ExitRecord{Id=nextExit++};r.Count++;r.Burst=Mathf.Clamp(size,0,10000);}
  public FireState[] Capture(Unit unit){var result=new List<FireState>();var seen=new HashSet<CompartmentExit>();foreach(var manager in unit.GetComponentsInChildren<FlammablesManager>(true))foreach(var compartment in manager.Compartments)if(compartment!=null)foreach(var exit in compartment.Exits){
   if(exit==null||exit.PositionMarker==null||!seen.Add(exit))continue;ExitRecord r;if(!exits.TryGetValue(exit,out r))exits[exit]=r=new ExitRecord{Id=nextExit++};
   float fire=exit.Closed?0:(float)FireRatio.GetValue(exit),smoke=exit.Closed?0:(float)SmokeRatio.GetValue(exit);if(fire<=0&&smoke<=0&&r.Count==0)continue;
   var p=unit.RootTransform.InverseTransformPoint(exit.PositionMarker.position);var q=Quaternion.Inverse(unit.RootTransform.rotation)*exit.PositionMarker.rotation;
   result.Add(new FireState{Id=r.Id,Type=(int)exit.SourceType,Bursts=r.Count,Burst=r.Burst,Fire=Mathf.Clamp(fire,0,10000),Smoke=Mathf.Clamp(smoke,0,10000),X=p.x,Y=p.y,Z=p.z,Qx=q.x,Qy=q.y,Qz=q.z,Qw=q.w});
  }if(result.Count>128)throw new InvalidOperationException("Fire state limit exceeded");return result.ToArray();}
  static void Align(GameObject obj,IFireParticleSystem particles,Transform parent,FireState state){
   if(obj==null)return;obj.transform.localPosition=new Vector3(state.X,state.Y,state.Z);
   obj.transform.rotation=Quaternion.identity;
   var direction=parent.rotation*new Quaternion(state.Qx,state.Qy,state.Qz,state.Qw)*Vector3.forward;
   // Match native CompartmentExit.AttachNewParticles: some prefabs emit on up,
   // others on forward. Treating both as forward rotated flames by 90 degrees.
   int orientation=particles==null?0:(int)particles.Orientation;
   if(orientation==0)obj.transform.forward=direction;else if(orientation==1)obj.transform.up=direction;
  }
  IFireParticleSystem Create(GameObject prefab,Transform parent,FireState state,out GameObject obj){obj=null;if(prefab==null)return null;obj=UnityEngine.Object.Instantiate(prefab,parent);var particles=obj.GetComponentInChildren<IFireParticleSystem>();Align(obj,particles,parent,state);return particles;}
  public void Apply(string id,Unit unit,FireState[] states){var seen=new HashSet<string>();foreach(var f in states){string key=id+":"+f.Id;seen.Add(key);Visual v;if(!visuals.TryGetValue(key,out v))visuals[key]=v=new Visual();var manager=ParticleEffectsManager.Instance;var type=(FlammableSourceType)f.Type;
   if(f.Fire>0&&v.Fire==null)v.Fire=Create(manager.GetFirePrefab(type),unit.RootTransform,f,out v.FireObject);
   if(f.Smoke>0&&v.Smoke==null)v.Smoke=Create(manager.GetSmokePrefab(type),unit.RootTransform,f,out v.SmokeObject);
   Align(v.FireObject,v.Fire,unit.RootTransform,f);Align(v.SmokeObject,v.Smoke,unit.RootTransform,f);
   Set(v.Fire,f.Fire,ref v.FireRatio);Set(v.Smoke,f.Smoke,ref v.SmokeRatio);
   if(f.Bursts>v.Bursts){GameObject burst;var particles=Create(manager.GetFireBurstPrefab(type),unit.RootTransform,f,out burst);if(particles!=null){particles.SetAllRatios(f.Burst);particles.Replay();}if(burst!=null)UnityEngine.Object.Destroy(burst,20);GameBridge.Log("BURN BURST "+unit.FriendlyName+" exit="+f.Id+" count="+f.Bursts);v.Bursts=f.Bursts;}
  }foreach(var pair in visuals)if(pair.Key.StartsWith(id+":",StringComparison.Ordinal)&&!seen.Contains(pair.Key)){var v=pair.Value;Set(v.Fire,0,ref v.FireRatio);Set(v.Smoke,0,ref v.SmokeRatio);}}
  static void Set(IFireParticleSystem particles,float ratio,ref float old){if(particles==null||Mathf.Abs(ratio-old)<0.001f)return;if(ratio<=0)particles.Stop();else{particles.SetAllRatios(ratio);if(old<=0)particles.Play();}if(old<=0&&ratio>0)GameBridge.Log("BURN VISUAL started ratio="+ratio);old=ratio;}
  public int VerifiedActiveVisuals(string id){return visuals.Count(p=>p.Key.StartsWith(id+":",StringComparison.Ordinal)&&((p.Value.FireObject!=null&&p.Value.FireObject.activeInHierarchy&&p.Value.FireRatio>0)||(p.Value.SmokeObject!=null&&p.Value.SmokeObject.activeInHierarchy&&p.Value.SmokeRatio>0)));}
  public void Impact(AmmoType a,int fused,int surface,bool ricochet,Vector3 position,GameObject obj){if(a==null||obj==null)return;if(pending.Count>=256){GameBridge.Log("Impact queue full; effect skipped");return;}pending.Enqueue(new Pending{Object=obj,State=new ImpactState{Id=++sequence,Ammo=AmmoKey(a),Fused=fused,Surface=surface,Ricochet=ricochet,X=position.x,Y=position.y,Z=position.z}});}
  public ImpactState[] Drain(){var result=new List<ImpactState>();while(pending.Count>0&&result.Count<64){var p=pending.Dequeue();if(p.Object!=null){var q=p.Object.transform.rotation;p.State.Qx=q.x;p.State.Qy=q.y;p.State.Qz=q.z;p.State.Qw=q.w;}result.Add(p.State);}return result.ToArray();}
  public void Receive(ImpactState[] events){foreach(var e in events){if(e.Id<=lastImpact)continue;lastImpact=e.Id;AmmoType a;if(!ammo.TryGetValue(e.Ammo,out a)){GameBridge.Log("Missing impact ammo "+e.Ammo);continue;}Replaying=true;try{var obj=ParticleEffectsManager.Instance.CreateImpactEffectOfType(a,(ParticleEffectsManager.FusedStatus)e.Fused,(ParticleEffectsManager.SurfaceMaterial)e.Surface,e.Ricochet,new Vector3(e.X,e.Y,e.Z),null);if(obj!=null)obj.transform.rotation=new Quaternion(e.Qx,e.Qy,e.Qz,e.Qw);GameBridge.Log("IMPACT REPLAY id="+e.Id+" ammo="+a.Name+" at="+new Vector3(e.X,e.Y,e.Z));}finally{Replaying=false;}}}
  public void Dispose(){if(Host==this)Host=null;foreach(var v in visuals.Values){if(v.FireObject!=null)UnityEngine.Object.Destroy(v.FireObject);if(v.SmokeObject!=null)UnityEngine.Object.Destroy(v.SmokeObject);}visuals.Clear();pending.Clear();exits.Clear();}
 }
 [HarmonyPatch(typeof(CompartmentExit),"MakeFireBurst")]
 static class CaptureBurst {static void Postfix(CompartmentExit __instance,float __0){if(CombatVisuals.Host!=null&&!GameBridge.ReplicaActive&&!__instance.Closed&&__instance.FireBurstParticles!=null)CombatVisuals.Host.Burst(__instance,__0);}}
 [HarmonyPatch(typeof(ParticleEffectsManager),"CreateImpactEffectOfType")]
 static class CaptureImpact {static void Postfix(AmmoType __0,ParticleEffectsManager.FusedStatus __1,ParticleEffectsManager.SurfaceMaterial __2,bool __3,Vector3 __4,GameObject __result){if(CombatVisuals.Host!=null&&!GameBridge.ReplicaActive&&!CombatVisuals.Replaying)CombatVisuals.Host.Impact(__0,(int)__1,(int)__2,__3,__4,__result);}}
}
