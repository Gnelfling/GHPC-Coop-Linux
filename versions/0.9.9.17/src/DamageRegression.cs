using System;
using System.Linq;
using GHPC;
using GHPC.Player;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        float aimDiagnosticNext;
        int aimDiagnosticSamples;
        Unit aimDiagnosticUnit;
        static readonly bool AimDiagnosticsEnabled =
            Array.IndexOf(Environment.GetCommandLineArgs(), "--coop-aim-diagnostics") >= 0;
        void RecordAimDiagnostic()
        {
            // Detailed aim samples allocate strings and write to disk. Keep them opt-in
            // rather than imposing that cost on every normal multiplayer session.
            if (!AimDiagnosticsEnabled) return;
            var player = PlayerInput.Instance;
            var selected = player != null ? player.CurrentPlayerUnit : null;
            if (selected != aimDiagnosticUnit)
            {
                aimDiagnosticUnit = selected;
                aimDiagnosticSamples = 0;
                aimDiagnosticNext = 0;
            }

            if (aimDiagnosticSamples >= 600 || Time.realtimeSinceStartup < aimDiagnosticNext)
                return;
            aimDiagnosticNext = Time.realtimeSinceStartup + .5f;
            try
            {
                var p = PlayerInput.Instance;
                var cam = GHPC.Camera.CameraManager.MainCam;
                if (p == null || p.CurrentPlayerUnit == null || p.CurrentPlayerWeapon == null || cam == null)
                    return;
                var u = p.CurrentPlayerUnit;
                var w = p.CurrentPlayerWeapon;
                var muzzle = w.Weapon != null ? w.Weapon.MuzzleIdentity : null;
                if (muzzle == null || w.FCS == null)
                    return;
                aimDiagnosticSamples++;
                GameBridge.Log("AIM diagnostic unit=" + u.UniqueName + " connected=" + (link != null) + " guest=" + GameBridge.ReplicaActive + " cameraMuzzleDeg=" + Vector3.Angle(cam.transform.forward, muzzle.forward).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " fcsMuzzleDeg=" + Vector3.Angle(w.FCS.AimWorldVector, muzzle.forward).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " cameraJiggle=" + (GHPC.Camera.CameraJiggler.Instance != null ? GHPC.Camera.CameraJiggler.Instance.FinalOffset.ToString() : "none") + " fire=" + Input.GetMouseButton(0) + " destroyed=" + u.Destroyed + " mounts=" + String.Join(",", (u.AimablePlatforms ?? new AimablePlatform[0]).Where(x => x != null).Select(x => x.StabilizerActive.ToString()).ToArray()));
            }
            catch (Exception e)
            {
                aimDiagnosticSamples = 600;
                GameBridge.Log("AIM diagnostics stopped: " + e.Message);
            }
        }

        bool damageProbeDone;
        float verifyStart, verifyNext;
        bool verifyKilled, verifyLocalGuard;
        int verifySamples, verifyErrors, verifyDeadSamples, verifyFireSamples;
        static int VerifyFlags(Unit u)
        {
            return (u.Destroyed ? 1 : 0) | (u.Abandoned ? 2 : 0) | (u.CannotMove ? 4 : 0) | (u.CannotShoot ? 8 : 0) | (u.UnitIncapacitated ? 16 : 0);
        }

        void RunNetworkVerification()
        {
            if (!claimed || !Environment.GetCommandLineArgs().Contains("--coop-netverify"))
                return;
            int expected;
            var expectedArg = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith("--coop-testplayers="));
            if (hosting &&
                verifyStart == 0 &&
                expectedArg != null &&
                Int32.TryParse(expectedArg.Substring(19), out expected) &&
                guests.Count(x => x.Claimed) < expected - 1)
                return;
            float now = Time.realtimeSinceStartup;
            if (verifyStart == 0)
                verifyStart = now;
            float elapsed = now - verifyStart;
            if (hosting && !verifyKilled && elapsed > 15)
            {
                verifyKilled = true;
                var target = game.Vehicles.Values.OrderByDescending(r => r.Unit.UniqueName.Contains("M60") ||
                    r.Unit.UniqueName.Contains("M1IP") ||
                    r.Unit.UniqueName.Contains("T55") ||
                    r.Unit.UniqueName.Contains("T72") ||
                    r.Unit.UniqueName.Contains("T80") ||
                    r.Unit.UniqueName.Contains("Leopard")).FirstOrDefault(r => r.Id != game.LocalId &&
                    r.Id != remote &&
                    !r.Unit.Neutralized &&
                    r.Unit.Allegiance != game.Vehicles[game.LocalId].Unit.Allegiance);
                if (target != null)
                {
                    foreach (var item in target.Unit.GetComponentsInChildren<GHPC.Effects.FlammableItem>(false))
                        if (item.CanIgnite)
                            item.Ignite(true);
                    var part = target.Unit.GetComponentsInChildren<GHPC.Equipment.DestructibleComponent>(false).FirstOrDefault(x => x.HealthPercent > .9f);
                    if (part != null)
                        part.SetHealthPercent(.5f);
                    target.Unit.NotifyDestroyed();
                    target.Unit.NotifyCannotMove();
                    target.Unit.NotifyCannotShoot();
                    target.Unit.NotifyIncapacitated();
                    GameBridge.Log("VERIFY host destroyed id=" + target.Id + " type=" + target.Unit.UniqueName + " flags=" + VerifyFlags(target.Unit));
                }
                else
                {
                    verifyErrors++;
                    GameBridge.Log("VERIFY FAIL no enemy target");
                }
            }

            if (!hosting && !verifyLocalGuard && elapsed > 5)
            {
                verifyLocalGuard = true;
                var target = game.Vehicles.Values.FirstOrDefault(r => r.Id != game.LocalId &&
                    r.Id != remote &&
                    !r.Unit.Neutralized &&
                    r.Unit.Allegiance == game.Vehicles[game.LocalId].Unit.Allegiance);
                if (target == null)
                    target = game.Vehicles[game.LocalId];
                var part = target.Unit.GetComponentsInChildren<GHPC.Equipment.DestructibleComponent>(false).FirstOrDefault(x => x.HealthPercent > .9f);
                int flags = VerifyFlags(target.Unit);
                float hp = part == null ? 0 : part.HealthPercent;
                if (part != null)
                    part.SetHealthPercent(0);
                target.Unit.NotifyDestroyed();
                bool ok = flags == VerifyFlags(target.Unit) && (part == null || part.HealthPercent == hp);
                if (!ok)
                    verifyErrors++;
                GameBridge.Log("VERIFY guest independent damage " + (ok ? "BLOCKED" : "FAIL") + " id=" + target.Id);
            }

            if (now < verifyNext)
                return;
            verifyNext = now + 1;
            verifySamples++;
            ShotAudit.Save(DataDir);
            int dead = 0, fires = 0, mismatch = 0;
            foreach (var r in game.Vehicles.Values)
            {
                if (hosting)
                {
                    if (r.Unit.Destroyed)
                        dead++;
                    fires += game.Combat.Capture(r.Unit).Count(f => f.Fire > 0 || f.Smoke > 0);
                    continue;
                }

                if (r.Frames.Count == 0)
                {
                    mismatch++;
                    continue;
                }

                var p = r.Frames[r.Frames.Count - 1];
                if (p.Dead)
                    dead++;
                var hp = r.Damage.Capture();
                if (VerifyFlags(r.Unit) != p.Flags ||
                    hp.Length != p.Health.Length ||
                    hp.Where((v, i) => Math.Abs(v - p.Health[i]) > .001f).Any())
                {
                    mismatch++;
                    GameBridge.Log("VERIFY mismatch id=" + r.Id + " expected=" + p.Flags + " actual=" + VerifyFlags(r.Unit));
                }

                fires += game.Combat.VerifiedActiveVisuals(r.Id);
            }

            verifyErrors += mismatch;
            if (dead > 0)
                verifyDeadSamples++;
            if (fires > 0)
                verifyFireSamples++;
            var report = "role=" + (hosting ? "host" : "guest") + " elapsed=" + elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " samples=" + verifySamples + " errors=" + verifyErrors + " units=" + game.Vehicles.Count + " dead=" + dead + " activeFireVisuals=" + fires + " deadSamples=" + verifyDeadSamples + " fireSamples=" + verifyFireSamples + " independentDamageBlocked=" + verifyLocalGuard;
            System.IO.File.WriteAllText(System.IO.Path.Combine(DataDir, "network-verification.txt"), report);
            if (verifySamples % 10 == 0)
                GameBridge.Log("VERIFY " + report);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new System.Collections.Generic.List<string>
            {
                "Id\tType\tLocal\tDestroyed\tFlags\tShots\tX\tY\tZ\tReceivedStamp"
            };
            foreach (var r in game.Vehicles.Values.Where(x => x.Unit != null))
            {
                var position = r.Unit.RootTransform.position;
                rows.Add(String.Join("\t",
                    new[] { r.Id,
                    r.Unit.UniqueName,
                    (r.Id == game.LocalId).ToString(),
                    r.Unit.Destroyed.ToString(),
                    VerifyFlags(r.Unit).ToString(),
                    r.Shots.ToString(),
                    position.x.ToString("R", inv),
                    position.y.ToString("R", inv),
                    position.z.ToString("R", inv),
                    r.Frames.Count > 0 ? r.Frames.Last().Stamp.ToString("R", inv) : "host" }));
            }

            System.IO.File.WriteAllLines(System.IO.Path.Combine(DataDir, "network-vehicles.tsv"), rows.ToArray());
        }

        bool debugSkipWeather;
        string lastRenderCommand = "";
        float nextRenderCommand;
        void RunRenderCheck()
        {
            if (!Environment.GetCommandLineArgs().Contains("--coop-rendercheck") || Time.realtimeSinceStartup < nextRenderCommand)
                return;
            nextRenderCommand = Time.realtimeSinceStartup + 1;
            var path = System.IO.Path.Combine(DataDir, "render-check.txt");
            if (!System.IO.File.Exists(path))
                return;
            string command = System.IO.File.ReadAllText(path).Trim();
            if (command == lastRenderCommand)
                return;
            lastRenderCommand = command;
            if (command == "weather-off")
            {
                debugSkipWeather = true;
                weather.Dispose();
            }

            if (command == "weather-on")
                debugSkipWeather = false;
            if (command == "post-off" || command == "post-on" || command == "post-reset")
            {
                var cam = GHPC.Camera.CameraManager.MainCam;
                var type = HarmonyLib.AccessTools.TypeByName("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
                var layer = cam != null && type != null ? cam.GetComponent(type) as Behaviour : null;
                if (layer != null)
                {
                    if (command == "post-reset")
                        HarmonyLib.AccessTools.Method(type, "ResetHistory").Invoke(layer, null);
                    else
                        layer.enabled = command == "post-on";
                }
            }

            GameBridge.Log("RENDER CHECK " + command + " cameraMode=" + (GHPC.Camera.CameraManager.Instance != null ? GHPC.Camera.CameraManager.Instance.CurrentLightMode.ToString() : "none"));
        }

        void RunDamageRegression()
        {
            if (damageProbeDone || !Environment.GetCommandLineArgs().Contains("--coop-damagecheck"))
                return;
            var player = PlayerInput.Instance;
            if (player == null ||
                !player.IsInitialized ||
                player.CurrentPlayerUnit == null ||
                sceneReadyAt <= 0 ||
                Time.realtimeSinceStartup < sceneReadyAt)
                return;
            damageProbeDone = true;
            try
            {
                var bridge = new GameBridge();
                bridge.Capture();
                DamageSync.Loading = true;
                var record = bridge.Vehicles.Values.First(x => x.Id != bridge.LocalId &&
                    !x.Unit.Neutralized &&
                    x.Damage.Capture().Any(h => h > .9f));
                var part = record.Unit.GetComponentsInChildren<GHPC.Equipment.DestructibleComponent>(true).First(x => x.HealthPercent > .9f);
                float before = part.HealthPercent;
                part.SetHealthPercent(0);
                record.Unit.NotifyDestroyed();
                if (part.HealthPercent != before || record.Unit.Destroyed)
                    throw new Exception("Guest local damage was not blocked");
                var snapshots = bridge.Snapshot();
                var pose = snapshots.First(x => x.Id == record.Id);
                int index = Array.FindIndex(pose.Health, h => h > .9f);
                pose.Health[index] = .5f;
                bridge.BeginReplica();
                record.Damage.Apply(record.Unit, pose);
                if (Math.Abs(record.Damage.Capture()[index] - .5f) > .001f)
                    throw new Exception("Host component damage was not applied");
                pose.Flags = 31;
                pose.Dead = true;
                record.Damage.Apply(record.Unit, pose);
                if (!record.Unit.Destroyed ||
                    !record.Unit.Abandoned ||
                    !record.Unit.CannotMove ||
                    !record.Unit.CannotShoot ||
                    !record.Unit.UnitIncapacitated)
                    throw new Exception("Host destruction flags were not applied");
                var objectives = ObjectiveSync.Capture();
                foreach (var obj in objectives)
                    obj.Text = "HOST LANGUAGE MUST NOT REPLACE LOCAL TEXT";
                ObjectiveSync.Apply(objectives);
                var bytes = Wire.Encode(new Message { Kind = Kind.Snapshot, Poses = snapshots });
                GameBridge.Log("DAMAGE REGRESSION PASS: guest damage blocked; host health and death applied; units=" + snapshots.Length + " bytes=" + bytes.Length);
            }
            catch (Exception e)
            {
                GameBridge.Log("DAMAGE REGRESSION FAIL " + e);
            }
        }
    }
}

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        float aimTestStart, aimTestNext;
        int aimTestStage = -1;
        UnityEngine.Vector3 aimTestTarget;
        bool aimTestFire;
        void RunAimTest()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("--coop-aimtest") || hosting || !claimed)
                return;
            var record = game.Vehicles[game.LocalId];
            if (record.Unit.UniqueName != "M60A3TTS")
            {
                var candidate = game.FriendlyChoices().FirstOrDefault(id => game.Available(id) &&
                    game.Vehicles[id].Unit.UniqueName == "M60A3TTS" &&
                    !occupiedVehicles.Contains(id));
                if (candidate != null && pendingVehicle == "")
                    RequestVehicle(candidate);
                return;
            }

            var info = GHPC.Player.PlayerInput.Instance.CurrentPlayerWeapon;
            if (info == null || info.FCS == null || info.Weapon == null)
                return;
            var weapon = info.Weapon;
            var feed = weapon.Feed;
            if (feed == null || feed.ReadyRack == null)
                return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (aimTestStart == 0)
            {
                aimTestStart = now;
                var origin = weapon.MuzzleIdentity.position;
                var dir = record.Unit.RootTransform.forward;
                dir.y = 0;
                dir.Normalize();
                aimTestTarget = origin + dir * 600;
                UnityEngine.RaycastHit hit;
                if (UnityEngine.Physics.Raycast(aimTestTarget + UnityEngine.Vector3.up * 500, UnityEngine.Vector3.down, out hit, 1000))
                    aimTestTarget = hit.point;
                GameBridge.Log("AIMTEST target=" + aimTestTarget.ToString("F4"));
            }

            int stage = (int)((now - aimTestStart) / 25);
            if (stage >= 4)
            {
                aimTestFire = false;
                if (aimTestStage != 99)
                {
                    aimTestStage = 99;
                    GameBridge.Log("AIMTEST DONE");
                }

                return;
            }

            int clip = stage % System.Math.Min(2, feed.ReadyRack.ClipTypes.Length);
            if (stage != aimTestStage)
            {
                aimTestStage = stage;
                feed.SetNextClipType(feed.ReadyRack.ClipTypes[clip]);
                GameBridge.Log("AIMTEST NEXT clip=" + clip);
            }

            info.FCS.TargetRange = UnityEngine.Vector3.Distance(weapon.MuzzleIdentity.position, aimTestTarget);
            info.FCS.SetAimWorldPosition(aimTestTarget);
            panel = false;
            aimTestFire = (now - aimTestStart) % 25 > 8;
            if (now >= aimTestNext)
            {
                aimTestNext = now + 1;
                GameBridge.Log("AIMTEST state clip=" + clip + " ammo=" + (weapon.CurrentAmmoType == null ? "none" : weapon.CurrentAmmoType.Name) + " range=" + info.FCS.CurrentRange + " barrelToSolution=" + UnityEngine.Vector3.Angle(weapon.MuzzleIdentity.forward, info.FCS.CompensatedWeaponAimPoint(false) - weapon.MuzzleIdentity.position));
            }
        }
    }
}
