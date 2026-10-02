using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using GHPC.World;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed class WeatherSync : IDisposable
    {
        static readonly FieldInfo[] Fields = new[]
        {
            "_raininess",
            "_windiness",
            "_cloudiness",
            "_cloudSpeed",
            "_totalLightValue",
            "_cloudShadowDirectionX",
            "_cloudShadowDirectionY",
            "_cloudShadowSpeed"
        }.Select(n => AccessTools.Field(typeof(Weather), n)).ToArray();
        static readonly FieldInfo Clouds = AccessTools.Field(typeof(Weather), "_cloudValues"), Dynamic = AccessTools.Field(typeof(Weather), "_useDynamicWeather");
        static readonly MethodInfo RainRotation = AccessTools.Method(typeof(Weather), "UpdateRainRotation");
        static readonly MethodInfo UpdateSkyWeather = AccessTools.Method(typeof(Weather), "UpdateCelestialSky");
        CelestialSky skyReplica;
        double originalSkyTime;
        float originalSkyRate;
        bool originalDaytime;
        static float nextHostLightLog, nextGuestLightLog;
        public static void InspectLighting(bool guest)
        {
            float now = Time.realtimeSinceStartup;
            if (now < (guest ? nextGuestLightLog : nextHostLightLog))
                return;
            if (guest)
                nextGuestLightLog = now + 10;
            else
                nextHostLightLog = now + 10;
            var sky = CelestialSky.Instance;
            if (sky == null)
                return;
            var ambientProbe = RenderSettings.ambientProbe;
            GameBridge.Log("LIGHTING " + (guest ? "guest" : "host") + " time=" + sky.t + " rate=" + sky.timeScale + " sun=" + (sky.sunLight != null ? sky.sunLight.intensity : 0) + " moon=" + (sky.moonLight != null ? sky.moonLight.intensity : 0) + " ambient=" + ambientProbe[0, 0] + "," + ambientProbe[1, 0] + "," + ambientProbe[2, 0] + " night=" + sky.IsNight);
        }

        public static void CaptureSky(Message snapshot)
        {
            var sky = CelestialSky.Instance;
            if (sky == null)
                return;
            snapshot.HasSky = true;
            snapshot.SkyTime = sky.t;
            snapshot.SkyRate = sky.timeScale;
            snapshot.Daytime = GHPC.SceneController.IsDaytime;
        }

        public void ApplySky(Message snapshot)
        {
            if (!snapshot.HasSky)
                return;
            var sky = CelestialSky.Instance;
            if (sky == null)
                return;
            if (skyReplica == null)
            {
                skyReplica = sky;
                originalSkyTime = sky.t;
                originalSkyRate = sky.timeScale;
                originalDaytime = GHPC.SceneController.IsDaytime;
                GameBridge.Log("SKY synchronized host time=" + snapshot.SkyTime);
            }

            bool changed = Math.Abs(sky.t - snapshot.SkyTime) > 0.01;
            sky.t = snapshot.SkyTime;
            sky.timeScale = snapshot.SkyRate;
            GHPC.SceneController.IsDaytime = snapshot.Daytime;
            if (changed)
                sky.ForceDirty();
        }

        Weather replica;
        bool wasEnabled, wasDynamic;
        float[] originalWeatherValues;
        public static float[] Capture()
        {
            var weather = Weather.Instance;
            if (weather == null)
                return new float[0];
            var values = new float[16];
            for (int i = 0; i < 8; i++)
                values[i] = (float)Fields[i].GetValue(weather);
            var tuple = Clouds.GetValue(weather);
            var type = tuple.GetType();
            var firstCloudVector = (Vector4)type.GetField("Item1").GetValue(tuple);
            var secondCloudVector = (Vector4)type.GetField("Item2").GetValue(tuple);
            for (int i = 0; i < 4; i++)
            {
                values[8 + i] = firstCloudVector[i];
                values[12 + i] = secondCloudVector[i];
            }

            return values;
        }

        static void Write(Weather weather, float[] values)
        {
            bool rainChanged = Math.Abs((float)Fields[0].GetValue(weather) - values[0]) > .0001f;
            for (int i = 0; i < 8; i++)
                Fields[i].SetValue(weather, values[i]);
            var tuple = Activator.CreateInstance(Clouds.FieldType);
            Clouds.FieldType.GetField("Item1").SetValue(tuple, new Vector4(values[8], values[9], values[10], values[11]));
            Clouds.FieldType.GetField("Item2").SetValue(tuple, new Vector4(values[12], values[13], values[14], values[15]));
            Clouds.SetValue(weather, tuple);
            if (rainChanged)
                UpdateSkyWeather.Invoke(weather, null);
            weather.UpdateWeather();
            RainRotation.Invoke(weather, null);
            if (rainChanged && weather.OnRaininessChanged != null)
                weather.OnRaininessChanged(values[0]);
        }

        public void Apply(float[] values)
        {
            if (values.Length == 0)
                return;
            var weather = Weather.Instance;
            if (weather == null)
                return;
            if (replica == null)
            {
                replica = weather;
                originalWeatherValues = Capture();
                wasEnabled = weather.enabled;
                wasDynamic = (bool)Dynamic.GetValue(weather);
                weather.StopAllCoroutines();
                weather.enabled = false;
                Dynamic.SetValue(weather, false);
                GameBridge.Log("WEATHER replica rain=" + values[0] + " cloud=" + values[2]);
            }

            Write(weather, values);
        }

        public void Dispose()
        {
            if (skyReplica != null)
            {
                skyReplica.t = originalSkyTime;
                skyReplica.timeScale = originalSkyRate;
                GHPC.SceneController.IsDaytime = originalDaytime;
                skyReplica.ForceDirty();
                skyReplica = null;
            }

            if (replica == null)
                return;
            Write(replica, originalWeatherValues);
            Dynamic.SetValue(replica, wasDynamic);
            replica.enabled = wasEnabled;
            if (wasDynamic)
                replica.StartCoroutine("TransitionWeather");
            replica = null;
        }
    }
}
