using System;
using System.Collections.Generic;
using System.Reflection;
using GHPC.Audio;
using HarmonyLib;
using UnityEngine;
using FMOD.Studio;

namespace GhpcCoop {
 // Replicate sound parameters, not the disabled guest physics simulation.
 public sealed class VehicleAudioSync {
  static readonly Dictionary<VehicleAudioController,VehicleAudioSync> Registry=new Dictionary<VehicleAudioController,VehicleAudioSync>();
  static readonly Dictionary<string,FieldInfo> Fields=new Dictionary<string,FieldInfo>();
  readonly VehicleAudioController audio; float[] state; float nextLog;
  static object Get(VehicleAudioController a,string n){FieldInfo f;if(!Fields.TryGetValue(n,out f)){f=AccessTools.Field(typeof(VehicleAudioController),n);Fields[n]=f;}return f.GetValue(a);}
  public VehicleAudioSync(GHPC.Unit unit){audio=unit.GetComponentInChildren<VehicleAudioController>(true);if(audio!=null)Registry[audio]=this;}
  static float WheelSpeed(object value){var w=value as NWH.VehiclePhysics.Wheel;return w!=null&&w.wheelController!=null?w.wheelController.wheel.velocity.magnitude*3.6f:0;}
  public float[] Capture(){
   var result=new float[8];if(audio==null||!audio.IsInitialized)return result;
   result[0]=(float)Get(audio,"_rpm");result[1]=(float)Get(audio,"_load");
   result[2]=WheelSpeed(Get(audio,"TrackLeftWheel"));result[3]=WheelSpeed(Get(audio,"TrackRightWheel"));
   var body=(Rigidbody)Get(audio,"_unitRigidbody");var chassis=(GHPC.IChassis)Get(audio,"_chassis");var vc=(NWH.VehiclePhysics.VehicleController)Get(audio,"_vehicleController");
   float turn=vc.tracks.trackedVehicle?vc.tracks.turnSpeedLimit:(float)Get(audio,"_maxWheelTurnVelocity");
   result[4]=Mathf.InverseLerp(0,turn,Mathf.Abs(body.angularVelocity.y));
   result[5]=Mathf.Clamp01(body.velocity.magnitude/Mathf.Max(.01f,chassis.MaxForwardSpeed));
   result[6]=Mathf.Max((float)Get(audio,"_turbulence"),(float)Get(audio,"_impactTurbulence"));
   result[7]=vc.engine.IsRunning?1:0;
   for(int i=0;i<result.Length;i++)result[i]=Normalize(result[i]);return result;
  }
  static float Normalize(float v){return float.IsNaN(v)||float.IsInfinity(v)?0:Mathf.Clamp(v,0,100000);}
  public void Apply(float[] value){state=value;}
  static void Parameter(EventInstance e,FMOD.Studio.PARAMETER_ID id,float value){if(e.hasHandle())e.setParameterByID(id,value,false);}
  public static void Update(VehicleAudioController a){
   VehicleAudioSync sync;if(!GameBridge.ReplicaActive||!Registry.TryGetValue(a,out sync)||sync.state==null||!a.IsInitialized)return;
   var s=sync.state;var engine=(EventInstance)Get(a,"_engineInstance");
   Parameter(engine,VehicleAudioManager.RPMParameterID,s[0]);Parameter(engine,VehicleAudioManager.LoadParameterID,s[1]);
   var left=(EventInstance)Get(a,"_trackLeftInstance");var right=(EventInstance)Get(a,"_trackRightInstance");
   Parameter(left,VehicleAudioManager.SpeedKphParameterID,s[2]);Parameter(right,VehicleAudioManager.SpeedKphParameterID,s[3]);
   Parameter(left,VehicleAudioManager.RotationVelocityParameterID,s[4]);Parameter(right,VehicleAudioManager.RotationVelocityParameterID,s[4]);
   if((bool)Get(a,"_isPlayerUnit")){
    var ambience=(EventInstance)Get(a,"_vehicleAmbienceInstance");Parameter(ambience,VehicleAudioManager.SpeedNormalizedParameterID,s[5]);Parameter(ambience,VehicleAudioManager.TurbulenceParameterID,s[6]);
    if(Time.realtimeSinceStartup>sync.nextLog){sync.nextLog=Time.realtimeSinceStartup+5;GameBridge.Log("AUDIO replica rpm="+s[0]+" load="+s[1]+" trackKph="+s[2]+" engineHandle="+engine.hasHandle());}
   }
  }
  public void Dispose(){if(audio!=null)Registry.Remove(audio);}
 }
 [HarmonyPatch(typeof(VehicleAudioController),"Update")] static class ReplicaVehicleAudio {static void Postfix(VehicleAudioController __instance){VehicleAudioSync.Update(__instance);}}
}
