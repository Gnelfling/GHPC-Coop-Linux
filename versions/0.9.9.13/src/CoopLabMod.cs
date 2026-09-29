using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using MelonLoader;
using UnityEngine;
using GHPC;
using GHPC.Player;

[assembly: MelonInfo(typeof(GhpcCoop.CoopLabMod), "GHPC Coop Experimental", "0.9.9.13", "Local development")]
[assembly: MelonGame(null, null)]
namespace GhpcCoop
{
    public sealed partial class CoopLabMod : MelonMod
    {
        string missionChoice = "TR01_showcase", terrainName = "";
        SupportSync support;
        TracerSync tracers;
        HelicopterSync helicopters = new HelicopterSync();
        WeatherSync weather = new WeatherSync();
        ILink link;
        HostSession session;
        GameBridge game = new GameBridge();
        string build = "", token = "", status = "Load the same fixed mission; select different friendly tanks.", remote = "";
        bool panel = true, hosting, hello, ready, claimed, connected, reload, firePressed, probe, probeStarted, needsReload;
        float nextSend, lastReceive, started;
        long seq, lastSnapshot = -1;
        bool testWindow, windowApplied, fourWindow;
        bool reloadProbe;
        bool autoLoad, loadStarted, autoDrive, reportWritten, wasBackground;
        string joinFile = "";
        float sceneReadyAt, claimedAt;
        Vector3 testStart, testAim;
        float driveDistance = -1;
        string DataDir
        {
            get
            {
                return Path.Combine(Environment.CurrentDirectory, "UserData", "GhpcCoop");
            }
        }

        public override void OnInitializeMelon()
        {
            ConfigureDirect();
            using (var h = SHA256.Create())
            using (var f = File.OpenRead(typeof(Unit).Assembly.Location))
                build = BitConverter.ToString(h.ComputeHash(f)).Replace("-", "");
            token = Wire.NewToken();
            probe = Environment.GetCommandLineArgs().Contains("--coop-probe");
            var args = Environment.GetCommandLineArgs();
            testWindow = args.Contains("--coop-testwindow");
            fourWindow = args.Contains("--coop-fourwindow");
            menuJoin = args.Contains("--coop-menujoin");
            autoLoad = args.Contains("--coop-autoload") && !menuJoin;
            autoDrive = args.Contains("--coop-autodrive");
            reloadProbe = args.Contains("--coop-reloadprobe");
            joinFile = args.FirstOrDefault(x => x.StartsWith("--coop-roomfile=")) ?? "";
            if (joinFile != "")
                joinFile = joinFile.Substring("--coop-roomfile=".Length);
            if (autoLoad || probe || joinFile != "" || sweepEnabled)
                Application.runInBackground = true;
            wasBackground = Application.runInBackground;
            Directory.CreateDirectory(DataDir);
            // Two local test copies must not race over the user's single config.json.
            // The launcher seeds this per-copy file before either game starts.
            if (testWindow)
            {
                var config = Path.Combine(DataDir, "test-config.json");
                if (File.Exists(config))
                {
                    HarmonyLib.AccessTools.Field(typeof(GHPC.Utility.SaveLoadUtility), "CONFIG_FILE_NAME").SetValue(null, config);
                    GameBridge.Log("TEST CONFIG isolated per game copy");
                }
            }

            var missionArg = args.FirstOrDefault(x => x.StartsWith("--coop-mission="));
            if (missionArg != null)
                missionChoice = missionArg.Substring("--coop-mission=".Length);
            GameBridge.Log("0.9.9.13 loaded. Network controls enabled only after matching handshake and claim.");
        }

        void Capture()
        {
            if (needsReload)
                throw new InvalidOperationException("Reload the mission after disconnecting a client");
            Stop();
            game = new GameBridge();
            game.Capture();
            status = "Captured " + game.Vehicles.Count + " ground vehicles. Ready to connect.";
        }

        void Start(bool host, bool steam = true)
        {
            lastConnectionError = "";
            // Direct transport must enforce the same entitlement as Steam rooms.
            SteamLink.CheckSteam();
            if (!steam)
            {
                DirectPort();
                if (!host && String.IsNullOrWhiteSpace(directAddress))
                    throw new InvalidOperationException("Enter the host IP address");
            }

            if (host)
                Capture();
            if (!steam && host && !directLocalOnly && directCode.Trim().Length < 8)
                throw new InvalidOperationException("For a LAN room, enter a room code with at least 8 characters");
            directMode = !steam;
            token = steam ? Wire.NewToken() : directCode.Trim();
            hosting = host;
            support = new SupportSync(host);
            tracers = new TracerSync(host);
            started = lastReceive = Time.realtimeSinceStartup;
            wasBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (host)
            {
                BeginMultiHost(steam);
                return;
            }

            link = steam ? (ILink)new SteamLink(token, build) : new Link();
            awaitingRoom = true;
            lobbySent = hello = false;
            loadingGuest = false;
            lobbyNextPing = 0;
            link.Join(steam ? steamRoom : directAddress.Trim(), steam ? 0 : DirectPort());
            status = steam ? "Connecting to Steam host room..." : "Connecting to Direct IP host...";
        }

        void Stop()
        {
            foreach (var peer in guests.ToArray())
            {
                game.ReleasePeer(peer.Id);
                peer.Link.Dispose();
            }

            guests.Clear();
            seats = null;
            multiRoom = null;
            if (support != null)
            {
                support.Dispose();
                support = null;
            }

            if (tracers != null)
            {
                tracers.Dispose();
                tracers = null;
            }

            helicopters.Dispose();
            DamageSync.Loading = false;
            ScorchAuthority.Clear();
            MissionChoices.Reset();
            weather.Dispose();
            weather = new WeatherSync();
            if (game != null)
            {
                needsReload |= game.Dirty;
                game.Dispose();
            }

            if (link != null)
                link.Dispose();
            link = null;
            if (session != null)
                session.Disconnect();
            session = null;
            infantry.Dispose();
            infantry = new InfantrySync();
            nextInfantry = 0;
            hostPaused = false;
            ClearAarView();
            hostSessionState = "host-running";
            nextPauseNotice = 0;
            MissionConfiguration.Restore();
            awaitingRoom = loadingGuest = lobbySent = false;
            missionOffer = null;
            offeredGuest = "";
            ready = claimed = hello = connected = false;
            remote = "";
            pendingVehicle = "";
            occupiedVehicles.Clear();
            roomCapacity = 0;
            seq = 0;
            lastSnapshot = -1;
            reload = firePressed = false;
            Application.runInBackground = wasBackground;
        }

        bool returnToMenuPending, returnAfterMissionLoad;
        string lastConnectionError = "";
        void Guard(Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                bool guestMission = !hosting && link != null && (claimed || loadingGuest || missionOffer != null);
                bool wasLoading = loadingGuest;
                Stop();
                panel = true;
                lastConnectionError = e.GetType().Name + ": " + e.Message;
                status = lastConnectionError;
                GameBridge.Log(e.ToString());
                try
                {
                    File.WriteAllText(Path.Combine(DataDir, "last-error.txt"), DateTime.UtcNow.ToString("O") + "\n" + e);
                }
                catch
                {
                // The original error is already logged; failure to save its optional copy must not block return to menu.
                }

                if (guestMission)
                {
                    returnToMenuPending = true;
                    returnAfterMissionLoad = wasLoading;
                    status = lastConnectionError + " — Returning to main menu.";
                }
            }
        }

        void UpdateReturnToMenu()
        {
            if (!returnToMenuPending)
                return;
            // Finish an in-flight mission load before requesting a second scene transition.
            if (returnAfterMissionLoad)
            {
                var p = PlayerInput.Instance;
                if (p == null ||
                    !p.IsInitialized ||
                    p.CurrentPlayerUnit == null ||
                    p.CurrentPlayerUnit.gameObject.scene.name != terrainName ||
                    sceneReadyAt <= 0 ||
                    Time.realtimeSinceStartup < sceneReadyAt)
                    return;
            }

            var sc = UnityEngine.Object.FindObjectOfType<SceneController>();
            if (sc == null)
                return;
            returnToMenuPending = returnAfterMissionLoad = false;
            try
            {
                GameBridge.Log("ROOM EXIT returning guest to main menu");
                sc.LoadMainMenu();
            }
            catch (Exception e)
            {
                status = "Could not return to main menu: " + e.Message;
                GameBridge.Log(e.ToString());
            }
        }

        bool suppressFireUntilRelease;
        bool GuestMenuBlocksFire()
        {
            var player = PlayerInput.Instance;
            return panel ||
                player == null ||
                player.IsMenuOverridingAction ||
                !player.AllowPlayerStrictLiveInput ||
                Cursor.visible ||
                Input.GetKey(KeyCode.Tab) ||
                Input.GetKey(KeyCode.Q);
        }

        public override void OnUpdate()
        {
            if (claimed && !hosting)
            {
                suppressFireUntilRelease = MenuFireGate.Suppress(suppressFireUntilRelease, GuestMenuBlocksFire(), Input.GetMouseButton(0), Input.GetMouseButtonDown(0));
                if (suppressFireUntilRelease)
                    firePressed = false;
            }

            UpdateNameplates();
            if (claimed && Environment.GetCommandLineArgs().Contains("--coop-rendercheck"))
                WeatherSync.InspectLighting(!hosting);
            if (testWindow)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = fourWindow ? 15 : 60;
                if (!windowApplied)
                {
                    windowApplied = true;
                    Screen.SetResolution(fourWindow ? 1024 : 1280, fourWindow ? 576 : 720, FullScreenMode.Windowed);
                }
            }

            RunDamageRegression();
            RunRenderCheck();
            RunSupportRegression();
            PumpSteamMenu();
            if (sweepEnabled)
            {
                RunMissionSweep();
                return;
            }

            if (returnToMenuPending)
            {
                UpdateReturnToMenu();
                return;
            }

            // Sample the click in this frame's LateUpdate, not at the next periodic send.
            // Waiting for the 50 ms tick can combine a click with a later aim direction.
            if (claimed && !hosting && !suppressFireUntilRelease && !GuestMenuBlocksFire() && Input.GetMouseButtonDown(0))
            {
                firePressed = true;
                guestInputDue = true;
            }

            if (Input.GetKeyDown(KeyCode.F8))
                panel = !panel;
            if (autoLoad && !loadStarted && Time.realtimeSinceStartup > 35)
            {
                var sc = UnityEngine.Object.FindObjectOfType<SceneController>();
                if (sc != null)
                {
                    loadStarted = true;
                    Guard(delegate
                    {
                        var theaters = Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>().Where(t => t.Missions != null).OrderBy(t => t.Key, StringComparer.Ordinal).ToArray();
                        MissionPolicy.Audit(theaters, Path.Combine(DataDir, "mission-audit.tsv"));
                        Func<GHPC.Mission.Data.MissionMetaData, bool> valid = delegate (GHPC.Mission.Data.MissionMetaData m)
                        {
                            try
                            {
                                return m != null && !m.IsCategory && !String.IsNullOrEmpty(m.MissionSceneReference.Name);
                            }
                            catch
                            {
                                return false;
                            }
                        };
                        File.WriteAllLines(Path.Combine(DataDir, "missions.txt"),
                            theaters.SelectMany(t => t.Missions.Where(valid).Select(m => t.Key + "|" + m.MissionName + "|" + m.MissionSceneReference.Name + "|flex=" + m.IsFlexMission)).ToArray());
                        Func<GHPC.Mission.Data.MissionMetaData, bool> matches = delegate (GHPC.Mission.Data.MissionMetaData m)
                        {
                            if (!valid(m))
                                return false;
                            var n = m.MissionSceneReference.Name;
                            return missionChoice == "combat" ? !n.StartsWith("TR", StringComparison.OrdinalIgnoreCase) &&
                                !n.ToLowerInvariant().Contains("training") : n == missionChoice ||
                                m.MissionName == missionChoice;
                        };
                        var theater = theaters.First(t => t.Missions.Any(matches));
                        var meta = theater.Missions.First(matches);
                        var key = meta.MissionSceneReference.Name;
                        ConfigureCustomMissionTest(meta);
                        terrainName = theater.TerrainSceneReference.Name;
                        int terrain = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(theater.TerrainSceneReference.Path);
                        if (terrain < 0)
                            throw new InvalidOperationException("Mission terrain missing");
                        GHPC.State.PersistentDataManager.PersistMissionInfo(new GHPC.State.Data.MissionInfoData("Co-op test", meta.MissionName), meta, theater.Key);
                        GHPC.Mission.DynamicMissionComposer.CampaignMode = false;
                        GHPC.Mission.DynamicMissionComposer.DynamicMission = meta.IsFlexMission;
                        GHPC.Mission.DynamicMissionComposer.NeedsFlexMissionSetup = meta.IsFlexMission;
                        GHPC.Mission.DynamicMissionComposer.CurrentMissionNameKey = key;
                        var faction = MissionPolicy.DefaultFaction(meta);
                        var factionArg = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith("--coop-faction="));
                        if (factionArg != null)
                        {
                            faction = (Faction)Enum.Parse(typeof(Faction), factionArg.Substring(15));
                            MissionPolicy.ValidateFaction(meta, faction);
                        }

                        SceneController.IsDaytime = meta.IsDefaultDayMission;
                        SceneController.AllowTimeChoice = false;
                        SceneController.TargetSpawningFaction = faction;
                        GameBridge.Log("MISSION " + theater.Key + "/" + meta.MissionName + " scene=" + key + " terrain=" + terrainName + " faction=" + faction);
                        sc.LoadSceneByMission(terrain + "," + key);
                    });
                }
            }

            if (menuJoin &&
                !probeStarted &&
                joinFile != "" &&
                Time.realtimeSinceStartup > 14 &&
                File.Exists(joinFile) &&
                File.GetLastWriteTimeUtc(joinFile) > launchUtc)
            {
                probeStarted = true;
                Guard(AutoJoinTest);
            }

            if (!menuJoin && (probe || joinFile != "") && !probeStarted)
            {
                var player = PlayerInput.Instance;
                if (player != null &&
                    player.IsInitialized &&
                    player.CurrentPlayerUnit != null &&
                    player.CurrentPlayerUnit.gameObject.scene.name == terrainName &&
                    Time.realtimeSinceStartup > sceneReadyAt &&
                    sceneReadyAt > 0)
                {
                    probeStarted = true;
                    Guard(delegate
                    {
                        Capture();
                        if (joinFile != "")
                        {
                            var cfg = File.ReadAllLines(joinFile);
                            token = cfg[0];
                            if (!game.Vehicles.ContainsKey(cfg[5]))
                                throw new InvalidOperationException("Cross-instance vehicle identity mismatch");
                            player.SetPlayerUnit(game.Vehicles[cfg[5]].Unit);
                            game.LocalId = cfg[5];
                            Start(false);
                        }
                        else
                            Start(true, !directMode);
                    });
                }
            }

            if (multiRoom != null)
            {
                Guard(delegate
                {
                    UpdateMultiHost(Time.realtimeSinceStartup);
                });
                return;
            }

            if (link == null)
                return;
            if (Input.GetKeyDown(KeyCode.R))
                reload = true;
            Guard(delegate
            {
                float now = Time.realtimeSinceStartup;
                link.Pump();
                var steamLink = link as SteamLink;
                if (steamLink != null && !hosting && steamLink.RoomToken != null)
                    token = steamLink.RoomToken;
                if (!String.IsNullOrEmpty(link.Error))
                    throw new IOException(link.Error);
                if (!link.Connected)
                {
                    if (connected)
                        throw new IOException("Peer disconnected; remote inputs cleared");
                    if (now - started > 120)
                        throw new IOException("Connection timed out");
                    return;
                }

                if (!connected)
                    lastReceive = now;
                connected = true;
                if (!hosting && (awaitingRoom || loadingGuest))
                {
                    UpdateGuestLobby(now);
                    return;
                }

                var p = PlayerInput.Instance;
                if (p == null || game.Vehicles[game.LocalId].Unit == null)
                    throw new IOException("Assigned vehicle or player context was removed");
                var assigned = game.Vehicles[game.LocalId].Unit;
                if (claimed && p.CurrentPlayerUnit != assigned)
                {
                    var selected = game.Vehicles.FirstOrDefault(x => x.Value.Unit == p.CurrentPlayerUnit).Key;
                    p.SetPlayerUnit(assigned);
                    if (selected != null)
                        RequestVehicle(selected);
                }

                if (!hosting && !hello)
                {
                    link.Send(new Message { Kind = Kind.Hello, Token = token, Build = build, World = game.World, Roster = game.Roster });
                    hello = true;
                }

                Message m;
                int budget = 0;
                while (budget++ < 32 && link.TryRead(out m))
                {
                    lastReceive = now;
                    if (hosting)
                    {
                        if (m.Kind == Kind.Claim &&
                            (!game.RoomChoices().Contains(m.Unit) ||
                            (offeredGuest != "" &&
                            m.Unit != offeredGuest)))
                        {
                            link.Send(new Message { Kind = Kind.Error, Text = "Reserved friendly vehicle is no longer available" });
                            continue;
                        }

                        if (m.Kind == Kind.SwitchVehicle && !game.Available(m.Unit))
                        {
                            link.Send(new Message { Kind = Kind.SwitchRejected, Text = "Vehicle is destroyed or unavailable" });
                            continue;
                        }

                        var reply = session.Handle(m);
                        if (reply.Kind == Kind.VehicleAssignment)
                        {
                            game.ReserveGuest(reply.Text);
                            remote = reply.Text;
                            status = "Guest changed vehicle.";
                        }

                        if (reply.Kind == Kind.Mission)
                            reply = MakeMissionOffer(reply);
                        if (reply.Kind == Kind.Claimed)
                        {
                            game.ReserveGuest(reply.Unit);
                            claimed = true;
                            panel = false;
                            remote = reply.Unit;
                            status = "Connected: host calculates both tanks, AI and damage.";
                        }

                        if (m.Kind == Kind.Input && reply.Kind == Kind.Ping)
                            game.ReceiveInput(m);
                        if (m.Kind == Kind.InfantryOrder &&
                            reply.Kind == Kind.Ping &&
                            claimed &&
                            !GHPC.AarController.InAar &&
                            !GHPC.State.TimeController.Paused)
                            infantry.HandleRequest(m, game.Vehicles[remote].Unit);
                        if (m.Kind == Kind.SupportRequest && reply.Kind == Kind.Ping && claimed)
                            reply = support.HandleRequest(m, game.Vehicles[remote].Unit);
                        link.Send(reply);
                        ready = session.Ready;
                    }
                    else if (m.Kind == Kind.Welcome)
                    {
                        if (ready || m.Build != build || m.World != game.World || m.Roster != game.Roster || m.Unit == game.LocalId)
                            throw new IOException("Host assignment or mission verification failed");
                        remote = m.Unit;
                        ready = true;
                        link.Send(new Message { Kind = Kind.Claim, Unit = game.LocalId });
                    }
                    else if (m.Kind == Kind.Claimed)
                    {
                        if (!ready || m.Unit != game.LocalId)
                            throw new IOException("Wrong reservation");
                        game.BeginReplica();
                        claimed = true;
                        claimedAt = now;
                        testStart = game.Vehicles[game.LocalId].Unit.RootTransform.position;
                        testAim = game.Vehicles[game.LocalId].Unit.RootTransform.forward;
                        testAim = Quaternion.AngleAxis(90, Vector3.up) * testAim;
                        testAim.y = -0.15f;
                        testAim.Normalize();
                        panel = false;
                        status = "Connected. WASD drive / native aim / left mouse fire / R reload. F8 panel.";
                    }
                    else if (m.Kind == Kind.VehicleAssignment)
                    {
                        if (!claimed || !game.Vehicles.ContainsKey(m.Unit) || !game.Vehicles.ContainsKey(m.Text) || m.Unit == m.Text)
                            throw new IOException("Invalid vehicle assignment");
                        roomCapacity = m.Role;
                        occupiedVehicles.Clear();
                        if (m.Roster != "")
                            foreach (var unit in m.Roster.Split('\n'))
                                occupiedVehicles.Add(unit);
                        remote = m.Unit;
                        if (game.LocalId != m.Text)
                            game.SelectReplicaVehicle(m.Text);
                        if (pendingVehicle == m.Text)
                            pendingVehicle = "";
                        reload = firePressed = false;
                        status = "Vehicle assigned: " + game.Vehicles[game.LocalId].Unit.FriendlyName;
                    }
                    else if (m.Kind == Kind.AarView)
                    {
                        ReceiveAarView(m);
                    }
                    else if (m.Kind == Kind.Ping)
                    {
                        if (claimed)
                            ReceiveHostPause(m.Text);
                    }
                    else if (m.Kind == Kind.Helicopters)
                    {
                        if (claimed)
                            helicopters.Apply(m);
                    }
                    else if (m.Kind == Kind.Infantry)
                    {
                        if (claimed)
                            infantry.Apply(m);
                    }
                    else if (m.Kind == Kind.PlayerNames)
                    {
                        if (claimed)
                            ReceivePlayerNames(m.Text);
                    }
                    else if (m.Kind == Kind.SwitchRejected)
                    {
                        pendingVehicle = "";
                        status = m.Text;
                    }
                    else if (m.Kind == Kind.SupportResult)
                    {
                        if (claimed)
                        {
                            support.Result(m);
                            if (m.Text != "")
                                status = m.Text;
                        }
                    }
                    else if (m.Kind == Kind.SupportVisual)
                    {
                        if (claimed)
                            support.Visual(m);
                    }
                    else if (m.Kind == Kind.Tracers)
                    {
                        if (claimed)
                            tracers.Receive(m.Tracers);
                    }
                    else if (m.Kind == Kind.Effects)
                    {
                        if (claimed)
                            game.Combat.Receive(m.Impacts);
                    }
                    else if (m.Kind == Kind.Error)
                        throw new IOException(m.Text);
                    else if (m.Kind == Kind.Snapshot)
                    {
                        if (!claimed)
                            continue;
                        if (m.Sequence <= lastSnapshot)
                            throw new IOException("Stale snapshot");
                        lastSnapshot = m.Sequence;
                        game.ReceiveSnapshot(m.Poses);
                        support.Apply(m.Supports);
                        ObjectiveSync.Apply(m.Objectives);
                        if (!debugSkipWeather)
                        {
                            weather.Apply(m.Weather);
                            weather.ApplySky(m);
                        }
                    }
                }

                if (claimed && !hosting)
                {
                    foreach (var request in infantry.Drain())
                    {
                        request.Sequence = ++seq;
                        request.Unit = game.LocalId;
                        link.Send(request);
                    }
                }

                if (claimed)
                {
                    support.Ready = true;
                    tracers.Ready = true;
                    foreach (var request in support.Drain())
                    {
                        request.Sequence = ++seq;
                        if (!hosting)
                            request.Unit = game.LocalId;
                        link.Send(request);
                    }
                }

                if (now - lastReceive > (claimed ? 10 : 180))
                    throw new IOException("Peer timeout");
                if (claimed && now >= nextSend)
                {
                    nextSend = Mathf.Max(nextSend + 0.05f, now);
                    if (hosting)
                    {
                        var snapshot = new Message
                        {
                            Kind = Kind.Snapshot,
                            Sequence = ++seq,
                            Poses = game.Snapshot(),
                            Supports = support.Capture(),
                            Objectives = ObjectiveSync.Capture(),
                            Weather = WeatherSync.Capture()
                        };
                        WeatherSync.CaptureSky(snapshot);
                        link.Send(snapshot);
                        var paths = tracers.Drain();
                        if (paths.Length > 0)
                            link.Send(new Message { Kind = Kind.Tracers, Sequence = ++seq, Tracers = paths });
                    }
                    else if (pendingVehicle == "")
                    {
                        guestInputDue = true;
                    }
                }
                else if (!hosting && ready && !claimed && now >= nextSend)
                {
                    nextSend = now + 1;
                    link.Send(new Message { Kind = Kind.Ping, Sequence = ++seq });
                }

                if (hosting && claimed)
                {
                    game.FireRemote();
                    var effects = game.Combat.Drain();
                    if (effects.Length > 0)
                        link.Send(new Message { Kind = Kind.Effects, Sequence = ++seq, Impacts = effects });
                }

                if (!hosting && claimed && !GHPC.AarController.InAar)
                    game.RenderReplica();
            });
        }

        public override void OnSceneWasInitialized(int index, string name)
        {
            EquipmentSync.ResetDiagnostics();
            if (name == terrainName)
                sceneReadyAt = Time.realtimeSinceStartup + 12;
        }

        readonly System.Collections.Generic.HashSet<string> occupiedVehicles = new System.Collections.Generic.HashSet<string>();
        string pendingVehicle = "";
        void RequestVehicle(string id)
        {
            if ((!claimed && multiRoom == null) || pendingVehicle != "" || id == game.LocalId)
                return;
            if (multiRoom != null)
            {
                if (!game.Available(id) || !seats.Move(0, id))
                {
                    status = "Vehicle occupied or unavailable";
                    return;
                }

                game.LocalId = id;
                PlayerInput.Instance.SetPlayerUnit(game.Vehicles[id].Unit);
                foreach (var peer in guests)
                    peer.Session.MoveHost(id);
                BroadcastOccupancy();
                return;
            }

            if (id == remote || !game.Available(id))
            {
                status = "That vehicle is occupied or unavailable.";
                return;
            }

            if (hosting)
            {
                if (!session.MoveHost(id))
                {
                    status = "That vehicle is occupied.";
                    return;
                }

                game.LocalId = id;
                PlayerInput.Instance.SetPlayerUnit(game.Vehicles[id].Unit);
                link.Send(session.Assignment());
                status = "Vehicle assigned: " + game.Vehicles[id].Unit.FriendlyName;
            }
            else
            {
                pendingVehicle = id;
                link.Send(new Message { Kind = Kind.SwitchVehicle, Unit = id, Sequence = ++seq });
                status = "Requesting vehicle...";
            }
        }

        public override void OnGUI()
        {
            DrawNameplates();
            DrawCoopMenu();
        }

        public override void OnSceneWasUnloaded(int index, string name)
        {
            if (loadingGuest)
                return;
            Stop();
            game = new GameBridge();
            needsReload = false;
            status = lastConnectionError != "" ? lastConnectionError : "Host: enter a mission and create a room. Guest: join from the main menu.";
        }

        public override void OnDeinitializeMelon()
        {
            Stop();
            if (steamInvite != null)
                steamInvite.Dispose();
        }
    }
}


