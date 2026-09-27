using System;using System.Linq;using System.Reflection;using UnityEngine;using GHPC.World;using HarmonyLib;
namespace GhpcCoop {
 public sealed class WeatherSync:IDisposable {
  static readonly FieldInfo[] Fields=new[]{"_raininess","_windiness","_cloudiness","_cloudSpeed","_totalLightValue","_cloudShadowDirectionX","_cloudShadowDirectionY","_cloudShadowSpeed"}.Select(n=>AccessTools.Field(typeof(Weather),n)).ToArray();
  static readonly FieldInfo Clouds=AccessTools.Field(typeof(Weather),"_cloudValues"),Dynamic=AccessTools.Field(typeof(Weather),"_useDynamicWeather");
  static readonly MethodInfo RainRotation=AccessTools.Method(typeof(Weather),"UpdateRainRotation");
  static readonly MethodInfo UpdateSkyWeather=AccessTools.Method(typeof(Weather),"UpdateCelestialSky");
  CelestialSky skyReplica;double originalTime;float originalRate;bool originalDay;
  static float nextHostLightLog,nextGuestLightLog;
  public static void InspectLighting(bool guest){
   float now=Time.realtimeSinceStartup;if(now<(guest?nextGuestLightLog:nextHostLightLog))return;if(guest)nextGuestLightLog=now+10;else nextHostLightLog=now+10;
   var s=CelestialSky.Instance;if(s==null)return;var p=RenderSettings.ambientProbe;
   GameBridge.Log("LIGHTING "+(guest?"guest":"host")+" time="+s.t+" rate="+s.timeScale+" sun="+(s.sunLight!=null?s.sunLight.intensity:0)+" moon="+(s.moonLight!=null?s.moonLight.intensity:0)+" ambient="+p[0,0]+","+p[1,0]+","+p[2,0]+" night="+s.IsNight);
  }
  public static void CaptureSky(Message m){var sky=CelestialSky.Instance;if(sky==null)return;m.HasSky=true;m.SkyTime=sky.t;m.SkyRate=sky.timeScale;m.Daytime=GHPC.SceneController.IsDaytime;}
  public void ApplySky(Message m){if(!m.HasSky)return;var sky=CelestialSky.Instance;if(sky==null)return;if(skyReplica==null){skyReplica=sky;originalTime=sky.t;originalRate=sky.timeScale;originalDay=GHPC.SceneController.IsDaytime;GameBridge.Log("SKY synchronized host time="+m.SkyTime);}bool changed=Math.Abs(sky.t-m.SkyTime)>0.01;sky.t=m.SkyTime;sky.timeScale=m.SkyRate;GHPC.SceneController.IsDaytime=m.Daytime;if(changed)sky.ForceDirty();}
  Weather replica;bool wasEnabled,wasDynamic;float[] original;
  public static float[] Capture(){var w=Weather.Instance;if(w==null)return new float[0];var values=new float[16];for(int i=0;i<8;i++)values[i]=(float)Fields[i].GetValue(w);var tuple=Clouds.GetValue(w);var type=tuple.GetType();var a=(Vector4)type.GetField("Item1").GetValue(tuple);var b=(Vector4)type.GetField("Item2").GetValue(tuple);for(int i=0;i<4;i++){values[8+i]=a[i];values[12+i]=b[i];}return values;}
  static void Write(Weather w,float[] values){bool rainChanged=Math.Abs((float)Fields[0].GetValue(w)-values[0])>.0001f;for(int i=0;i<8;i++)Fields[i].SetValue(w,values[i]);var tuple=Activator.CreateInstance(Clouds.FieldType);Clouds.FieldType.GetField("Item1").SetValue(tuple,new Vector4(values[8],values[9],values[10],values[11]));Clouds.FieldType.GetField("Item2").SetValue(tuple,new Vector4(values[12],values[13],values[14],values[15]));Clouds.SetValue(w,tuple);if(rainChanged)UpdateSkyWeather.Invoke(w,null);w.UpdateWeather();RainRotation.Invoke(w,null);if(rainChanged&&w.OnRaininessChanged!=null)w.OnRaininessChanged(values[0]);}
  public void Apply(float[] values){if(values.Length==0)return;var w=Weather.Instance;if(w==null)return;if(replica==null){replica=w;original=Capture();wasEnabled=w.enabled;wasDynamic=(bool)Dynamic.GetValue(w);w.StopAllCoroutines();w.enabled=false;Dynamic.SetValue(w,false);GameBridge.Log("WEATHER replica rain="+values[0]+" cloud="+values[2]);}Write(w,values);}
  public void Dispose(){if(skyReplica!=null){skyReplica.t=originalTime;skyReplica.timeScale=originalRate;GHPC.SceneController.IsDaytime=originalDay;skyReplica.ForceDirty();skyReplica=null;}if(replica==null)return;Write(replica,original);Dynamic.SetValue(replica,wasDynamic);replica.enabled=wasEnabled;if(wasDynamic)replica.StartCoroutine("TransitionWeather");replica=null;}
 }
}
