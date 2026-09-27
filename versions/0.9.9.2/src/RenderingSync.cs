using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
namespace GhpcCoop {
 // GHPC 20260814: deferred registration mutates dictionaries concurrently
 // with gameplay registration and camera submission. Do not hold this gate
 // around director/worker waits or renderer shutdown.
 static class RenderingGate {internal static readonly object Sync=new object();
  internal static readonly Type Renderer=AccessTools.TypeByName("GHPC.Rendering.Instancing.InstancedMeshRenderer");
 }
 [HarmonyPatch] static class RendererRegistrationGuard {
  static IEnumerable<MethodBase> TargetMethods(){
   var names=new[]{"RegisterInstance","RegisterInstances","DeregisterInstance","ProcessRegistrations","ProcessDeregistrations"};
   return RenderingGate.Renderer.GetMethods(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>names.Contains(m.Name)).Cast<MethodBase>();
  }
  static void Prefix(out bool __state){__state=false;Monitor.Enter(RenderingGate.Sync);__state=true;}
  static Exception Finalizer(Exception __exception,bool __state){if(__state)Monitor.Exit(RenderingGate.Sync);return __exception;}
 }
 [HarmonyPatch] static class RendererSubmissionGuard {
  static MethodBase TargetMethod(){return AccessTools.Method(RenderingGate.Renderer,"SubmitCollectedInstances");}
  static readonly FieldInfo Completed=AccessTools.Field(RenderingGate.Renderer,"procThreadHandleDeferredEnd");
  static bool Prefix(object __instance,out bool __state){
   __state=false;
   // Wait BEFORE taking the gate: the director needs it to finish registration.
   var completed=Completed.GetValue(__instance) as WaitHandle;
   if(completed!=null&&!completed.WaitOne(100))return false;
   Monitor.Enter(RenderingGate.Sync);__state=true;return true;
  }
  static Exception Finalizer(Exception __exception,bool __state){if(__state)Monitor.Exit(RenderingGate.Sync);return __exception;}
 }
}
