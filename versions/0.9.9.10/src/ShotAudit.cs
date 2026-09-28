using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using GHPC.Weapons;

namespace GhpcCoop
{
    // Test-only observation. Never changes weapon, projectile, damage or network behavior.
    public static class ShotAudit
    {
        static readonly bool enabled = Environment.GetCommandLineArgs().Any(x => x == "--coop-netverify" || x == "--coop-shot-audit");
        sealed class Sample
        {
            public string Vehicle;
            public long Input;
            public int Round;
            public float Angle;
            public GHPC.Weapons.ShotInfo Info;
        }

        static readonly List<Sample> shots = new List<Sample>();
        static string vehicle;
        static long input;
        static Vector3 requested;
        public static void Begin(string id, long sequence, Vector3 direction)
        {
            if (!enabled)
                return;
            vehicle = id;
            input = sequence;
            requested = direction;
        }

        public static void End()
        {
            vehicle = null;
        }

        public static void Record(string id, LiveRound round)
        {
            if (!enabled || vehicle != id || round == null || round.ShotInfo == null || shots.Count >= 256)
                return;
            var info = round.ShotInfo;
            float angle = Vector3.Angle(requested, info.StartVector);
            shots.Add(new Sample { Vehicle = id, Input = input, Round = round.ID, Angle = angle, Info = info });
            GameBridge.Log("SHOT AUDIT vehicle=" + id + " input=" + input + " round=" + round.ID + " actualLaunchAngleDeg=" + angle.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
        }

        public static void Save(string directory)
        {
            if (!enabled)
                return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>
            {
                "Vehicle\tInputSequence\tRound\tLaunchAngleDeg\tHit\tDamage\tKill\tDistance\tStart\tStop"
            };
            foreach (var s in shots)
                rows.Add(String.Join("\t",
                    new[] { s.Vehicle,
                    s.Input.ToString(),
                    s.Round.ToString(),
                    s.Angle.ToString("F4", inv),
                    s.Info.IsHit.ToString(),
                    s.Info.IsDamagingShot.ToString(),
                    s.Info.IsKillShot.ToString(),
                    s.Info.Distance.ToString("F2", inv),
                    s.Info.StartPosition.ToString("F3"),
                    s.Info.StopPosition.ToString("F3") }));
            File.WriteAllLines(Path.Combine(directory, "shot-audit.tsv"), rows.ToArray());
        }
    }
}
