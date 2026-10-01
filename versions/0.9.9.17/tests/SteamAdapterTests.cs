// Deterministic fake Steam API for testing the actual SteamLink adapter. No Steam network used.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Steamworks
{
    public enum EResult
    {
        k_EResultOK,
        k_EResultFail,
        k_EResultLimitExceeded
    }

    public enum ELobbyType
    {
        k_ELobbyTypePrivate,
        k_ELobbyTypeFriendsOnly,
        k_ELobbyTypePublic
    }

    public enum ESteamNetworkingConnectionState
    {
        k_ESteamNetworkingConnectionState_Connecting,
        k_ESteamNetworkingConnectionState_Connected,
        k_ESteamNetworkingConnectionState_ClosedByPeer,
        k_ESteamNetworkingConnectionState_ProblemDetectedLocally
    }

    public enum ESteamNetworkingConfigValue
    {
        k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable
    }

    public enum ESteamNetworkingConfigDataType
    {
        k_ESteamNetworkingConfig_Int32
    }

    public struct SteamNetworkingConfigValue_t
    {
        public ESteamNetworkingConfigValue m_eValue;
        public ESteamNetworkingConfigDataType m_eDataType;
        public OptionValue m_val;
        public struct OptionValue
        {
            public int m_int32;
        }
    }

    public struct CSteamID
    {
        public ulong m_SteamID;
        public CSteamID(ulong n)
        {
            m_SteamID = n;
        }

        public bool IsLobby()
        {
            return m_SteamID >= 100;
        }

        public override string ToString()
        {
            return m_SteamID.ToString();
        }

        public static bool operator ==(CSteamID a, CSteamID b)
        {
            return a.m_SteamID == b.m_SteamID;
        }

        public static bool operator !=(CSteamID a, CSteamID b)
        {
            return !(a == b);
        }

        public override bool Equals(object o)
        {
            return o is CSteamID && this == (CSteamID)o;
        }

        public override int GetHashCode()
        {
            return m_SteamID.GetHashCode();
        }
    }

    public struct HSteamListenSocket
    {
        public uint m_HSteamListenSocket;
        public static bool operator ==(HSteamListenSocket a, HSteamListenSocket b)
        {
            return a.m_HSteamListenSocket == b.m_HSteamListenSocket;
        }

        public static bool operator !=(HSteamListenSocket a, HSteamListenSocket b)
        {
            return !(a == b);
        }

        public override bool Equals(object o)
        {
            return o is HSteamListenSocket && this == (HSteamListenSocket)o;
        }

        public override int GetHashCode()
        {
            return (int)m_HSteamListenSocket;
        }
    }

    public struct HSteamNetConnection
    {
        public uint m_HSteamNetConnection;
        public static bool operator ==(HSteamNetConnection a, HSteamNetConnection b)
        {
            return a.m_HSteamNetConnection == b.m_HSteamNetConnection;
        }

        public static bool operator !=(HSteamNetConnection a, HSteamNetConnection b)
        {
            return !(a == b);
        }

        public override bool Equals(object o)
        {
            return o is HSteamNetConnection && this == (HSteamNetConnection)o;
        }

        public override int GetHashCode()
        {
            return (int)m_HSteamNetConnection;
        }
    }

    public struct SteamNetworkingIdentity
    {
        public CSteamID id;
        public void SetSteamID(CSteamID x)
        {
            id = x;
        }

        public CSteamID GetSteamID()
        {
            return id;
        }
    }

    public struct SteamNetConnectionInfo_t
    {
        public HSteamListenSocket m_hListenSocket;
        public ESteamNetworkingConnectionState m_eState;
        public SteamNetworkingIdentity m_identityRemote;
        public int m_nFlags;
        public string m_szConnectionDescription, m_szEndDebug;
    }

    public struct SteamNetConnectionStatusChangedCallback_t
    {
        public HSteamNetConnection m_hConn;
        public SteamNetConnectionInfo_t m_info;
    }

    public struct LobbyCreated_t
    {
        public EResult m_eResult;
        public ulong m_ulSteamIDLobby;
    }

    public struct LobbyEnter_t
    {
        public uint m_EChatRoomEnterResponse;
        public ulong m_ulSteamIDLobby;
    }

    public struct Handle
    {
        public int m_HSteamUser;
    }

    public struct AppID
    {
        public int m_AppId;
    }

    public static class SteamAPI
    {
        public static Handle GetHSteamUser()
        {
            return new Handle
            {
                m_HSteamUser = 1
            };
        }
    }

    public static class SteamApps
    {
        public static bool Subscribed = true;
        public static bool BIsSubscribed() { return Subscribed; }
    }

    public static class SteamUser
    {
        public static bool BLoggedOn()
        {
            return true;
        }

        public static CSteamID GetSteamID()
        {
            return new CSteamID(1);
        }
    }

    public static class SteamUtils
    {
        public static AppID GetAppID()
        {
            return new AppID
            {
                m_AppId = 1705180
            };
        }
    }

    public static class SteamNetworkingUtils
    {
        public static void InitRelayNetworkAccess()
        {
        }
    }

    public static class SteamFriends
    {
        public static void ActivateGameOverlayInviteDialog(CSteamID x)
        {
        }
    }

    public class Callback<T> : IDisposable
    {
        public static Action<T> Handler;
        public static Callback<T> Create(Action<T> a)
        {
            Handler = a;
            return new Callback<T>();
        }

        public void Dispose()
        {
            Handler = null;
        }
    }

    public class CallResult<T> : IDisposable
    {
        Action<T, bool> action;
        bool active;
        public static CallResult<T> Create(Action<T, bool> a)
        {
            return new CallResult<T>
            {
                action = a
            };
        }

        public void Set(object result)
        {
            active = false;
            action((T)result, false);
        }

        public bool IsActive()
        {
            return active;
        }

        public void Dispose()
        {
        }
    }

    public static class SteamMatchmaking
    {
        public static int Capacity;
        public static ELobbyType Visibility;
        public static List<ulong> Members = new List<ulong>
        {
            1,
            2,
            3,
            4
        };
        public static Dictionary<string, string> Data = new Dictionary<string, string>();
        public static object CreateLobby(ELobbyType t, int n)
        {
            Capacity = n;
            Visibility = t;
            return new LobbyCreated_t
            {
                m_eResult = EResult.k_EResultOK,
                m_ulSteamIDLobby = 100
            };
        }

        public static object JoinLobby(CSteamID id)
        {
            return new LobbyEnter_t
            {
                m_EChatRoomEnterResponse = 1,
                m_ulSteamIDLobby = id.m_SteamID
            };
        }

        public static void LeaveLobby(CSteamID l)
        {
        }

        public static bool SetLobbyData(CSteamID l, string k, string v)
        {
            Data[k] = v;
            return true;
        }

        public static string GetLobbyData(CSteamID l, string k)
        {
            return Data.ContainsKey(k) ? Data[k] : "";
        }

        public static CSteamID GetLobbyOwner(CSteamID l)
        {
            return new CSteamID(1);
        }

        public static int GetNumLobbyMembers(CSteamID l)
        {
            return Members.Count;
        }

        public static CSteamID GetLobbyMemberByIndex(CSteamID l, int i)
        {
            return new CSteamID(Members[i]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SteamNetworkingMessage_t
    {
        public SteamNetworkingIdentity m_identityPeer;
        public int m_cbSize;
        public IntPtr m_pData;
        public static int Released;
        public static void Release(IntPtr p)
        {
            var m = (SteamNetworkingMessage_t)Marshal.PtrToStructure(p, typeof(SteamNetworkingMessage_t));
            Marshal.FreeHGlobal(m.m_pData);
            Marshal.FreeHGlobal(p);
            Released++;
        }
    }

    public struct SteamNetworkingMicroseconds { public long m_SteamNetworkingMicroseconds; }
    public struct SteamNetConnectionRealTimeLaneStatus_t { }
    public struct SteamNetConnectionRealTimeStatus_t
    {
        public int m_nPing, m_cbPendingReliable, m_cbPendingUnreliable,
            m_cbSentUnackedReliable, m_nSendRateBytesPerSecond;
        public SteamNetworkingMicroseconds m_usecQueueTime;
    }

    public static class SteamNetworkingSockets
    {
        public static EResult StatusResult = EResult.k_EResultOK;
        public static bool ThrowStatus;
        public static EResult GetConnectionRealTimeStatus(HSteamNetConnection connection,
            ref SteamNetConnectionRealTimeStatus_t status, int lanes,
            ref SteamNetConnectionRealTimeLaneStatus_t lane)
        {
            if (lanes != 0) throw new Exception("diagnostics unexpectedly requested lanes");
            if (ThrowStatus) throw new EntryPointNotFoundException();
            lane = new SteamNetConnectionRealTimeLaneStatus_t();
            status = new SteamNetConnectionRealTimeStatus_t {
                m_nPing = 82, m_cbPendingReliable = 4096, m_cbPendingUnreliable = 128,
                m_cbSentUnackedReliable = 2048, m_nSendRateBytesPerSecond = 100000,
                m_usecQueueTime = new SteamNetworkingMicroseconds { m_SteamNetworkingMicroseconds = 125500 }
            };
            return StatusResult;
        }
        public static bool BufferFull;
        public static int SendAttempts;
        public static List<byte[]> SendHistory = new List<byte[]>();
        public static List<uint> Closed = new List<uint>();
        public static Dictionary<uint, byte[]> Sent = new Dictionary<uint, byte[]>();
        public static Dictionary<uint, Queue<IntPtr>> Incoming = new Dictionary<uint, Queue<IntPtr>>();
        public static bool RelayOnly;
        public static HSteamListenSocket CreateListenSocketP2P(int p, int n, SteamNetworkingConfigValue_t[] o)
        {
            RelayOnly = n == 1 && o[0].m_val.m_int32 == 0;
            return new HSteamListenSocket
            {
                m_HSteamListenSocket = 7
            };
        }

        public static HSteamNetConnection ConnectP2P(ref SteamNetworkingIdentity i, int p, int n, SteamNetworkingConfigValue_t[] o)
        {
            return new HSteamNetConnection
            {
                m_HSteamNetConnection = 99
            };
        }

        public static EResult AcceptConnection(HSteamNetConnection c)
        {
            return EResult.k_EResultOK;
        }

        public static void CloseConnection(HSteamNetConnection c, int n, string s, bool linger)
        {
            Closed.Add(c.m_HSteamNetConnection);
        }

        public static void CloseListenSocket(HSteamListenSocket s)
        {
        }

        public static EResult SendMessageToConnection(HSteamNetConnection c, IntPtr p, uint n, int flags, out long number)
        {
            number = 0;
            SendAttempts++;
            if (BufferFull)
                return EResult.k_EResultLimitExceeded;
            var b = new byte[n];
            Marshal.Copy(p, b, 0, (int)n);
            Sent[c.m_HSteamNetConnection] = b;
            SendHistory.Add(b);
            number = 1;
            return EResult.k_EResultOK;
        }

        public static int ReceiveMessagesOnConnection(HSteamNetConnection c, IntPtr[] p, int n)
        {
            Queue<IntPtr> q;
            if (!Incoming.TryGetValue(c.m_HSteamNetConnection, out q) || q.Count == 0)
                return 0;
            p[0] = q.Dequeue();
            return 1;
        }

        public static void Inject(uint conn, ulong identity, byte[] data)
        {
            var p = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, p, data.Length);
            var m = new SteamNetworkingMessage_t
            {
                m_identityPeer = new SteamNetworkingIdentity
                {
                    id = new CSteamID(identity)
                },
                m_cbSize = data.Length,
                m_pData = p
            };
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SteamNetworkingMessage_t)));
            Marshal.StructureToPtr(m, ptr, false);
            if (!Incoming.ContainsKey(conn))
                Incoming[conn] = new Queue<IntPtr>();
            Incoming[conn].Enqueue(ptr);
        }
    }
}

namespace GhpcCoop
{
    public static class GameBridge
    {
        public static string LastLog;
        public static void Log(string s)
        {
            LastLog = s;
            Console.WriteLine(s);
        }
    }

    public static class SteamAdapterTests
    {
        static int checks;
        static void Check(bool v, string n)
        {
            if (!v)
                throw new Exception(n);
            checks++;
            Console.WriteLine("PASS " + n);
        }

        static void Event(uint c, ulong id, Steamworks.ESteamNetworkingConnectionState state)
        {
            Steamworks.Callback<Steamworks.SteamNetConnectionStatusChangedCallback_t>.Handler(new Steamworks.SteamNetConnectionStatusChangedCallback_t { m_hConn = new Steamworks.HSteamNetConnection { m_HSteamNetConnection = c },
                m_info = new Steamworks.SteamNetConnectionInfo_t { m_hListenSocket = new Steamworks.HSteamListenSocket { m_HSteamListenSocket = 7 },
                m_eState = state,
                m_identityRemote = new Steamworks.SteamNetworkingIdentity { id = new Steamworks.CSteamID(id) },
                m_nFlags = 16,
                m_szConnectionDescription = "fixture SDR" } });
        }

        public static void Main()
        {
            Steamworks.SteamApps.Subscribed = false;
            bool licenseRejected = false;
            try
            {
                SteamLink.CheckSteam();
            }
            catch (System.IO.IOException)
            {
                licenseRejected = true;
            }
            Check(licenseRejected, "logged-in app context without entitlement is rejected");
            Steamworks.SteamApps.Subscribed = true;
            SteamLink.CheckSteam();
            Check(true, "valid current-app entitlement is accepted");
            using (var host = new SteamLink(new string ('a', 32), "build", false, "Test room", "Keen Kestrel", true))
            {
                host.SetCapacity(4);
                host.Host(null, 0);
                Check(Steamworks.SteamMatchmaking.Capacity == 4, "lobby has four seats");
                Check(Steamworks.SteamMatchmaking.Visibility == Steamworks.ELobbyType.k_ELobbyTypePublic,
                    "listed room uses public Steam visibility");
                Check(Steamworks.SteamMatchmaking.Data["name"] == "Test room" &&
                    Steamworks.SteamMatchmaking.Data["mission"] == "Keen Kestrel" &&
                    Steamworks.SteamMatchmaking.Data["mod_revision"] == SteamLink.ModRevision,
                    "room advertises name mission and exact mod identity");
                Check(SteamLink.DisplayText("  A\n<B>  ", 48) == "AB" &&
                    SteamLink.DisplayText("가나다", 2) == "가나", "lobby display text is bounded and strips markup");
                Check(Steamworks.SteamNetworkingSockets.RelayOnly, "direct ICE disabled on socket");
                var links = new List<ILink>();
                for (uint i = 0; i < 3; i++)
                {
                    Event(i + 10, i + 2, Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting);
                    var p = host.Take();
                    Check(p != null, "independent accepted peer " + i);
                    Event(i + 10, i + 2, Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
                    Check(p.Connected, "connected peer " + i);
                    links.Add(p);
                }

                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                for (int frame = 0; frame < 80; frame++)
                {
                    host.Pump();
                    foreach (var peer in links)
                        peer.Send(new Message { Kind = Kind.Ping, Sequence = frame });
                }
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Count == 240,
                    "host room replenishes all three peer budgets across eighty updates");

                Steamworks.SteamNetworkingSockets.BufferFull = true;
                links[0].Send(new Message { Kind = Kind.Ping, Sequence = 80 });
                links[0].Send(new Message { Kind = Kind.Ping, Sequence = 81 });
                Check(links[0].Connected, "temporary send saturation keeps connection alive");
                Steamworks.SteamNetworkingSockets.BufferFull = false;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode).Select(m => m.Sequence).SequenceEqual(new long[] { 80, 81 }), "buffered messages retry in order");
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Count == 2, "retried messages are not duplicated");

                // Shared broadcast bytes must survive independent delayed sends.
                var broadcast = new Message { Kind = Kind.Ping, Sequence = 82 };
                var encoded = Wire.Encode(broadcast);
                var original = (byte[])encoded.Clone();
                Steamworks.SteamNetworkingSockets.BufferFull = true;
                foreach (var peer in links)
                    ((SteamPeer)peer).SendEncoded(encoded);
                broadcast.Sequence = 999; // Later caller changes cannot alter queued bytes.
                Steamworks.SteamNetworkingSockets.BufferFull = false;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                foreach (var peer in links)
                {
                    peer.Send(new Message { Kind = Kind.Ping, Sequence = 83 });
                    peer.Pump();
                }
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode)
                    .Select(m => m.Sequence).SequenceEqual(new long[] { 82, 83, 82, 83, 82, 83 }),
                    "shared broadcast preserves ordering for every delayed peer");
                Check(encoded.SequenceEqual(original), "send queues do not mutate shared bytes");
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Where((bytes, index) => index % 2 == 0)
                    .All(bytes => bytes.SequenceEqual(original)), "all peers receive identical broadcast bytes");

                // A stalled reliable queue must drain across frames, preserving every event.
                Steamworks.SteamNetworkingSockets.BufferFull = true;
                for (int sequence = 100; sequence < 140; sequence++)
                    links[0].Send(new Message { Kind = Kind.Ping, Sequence = sequence });
                Steamworks.SteamNetworkingSockets.BufferFull = false;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Count == 16,
                    "recovery flush is bounded to sixteen packets per pump");
                Message noMessage;
                links[0].TryRead(out noMessage);
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Count == 16,
                    "receive path cannot bypass the frame send budget");
                links[0].Pump();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode)
                    .Select(message => message.Sequence).SequenceEqual(Enumerable.Range(100, 40).Select(value => (long)value)),
                    "bounded draining preserves all queued events in order");

                Steamworks.SteamNetworkingSockets.BufferFull = true;
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 200 });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 201 });
                links[0].Send(new Message { Kind = Kind.Effects, Sequence = 202 });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 203 });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 204 });
                Steamworks.SteamNetworkingSockets.BufferFull = false;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode)
                    .Select(message => message.Sequence).SequenceEqual(new long[] { 201, 202, 204 }),
                    "adjacent stale snapshots coalesce without crossing an impact event");

                // A busy frame should probe a saturated native queue only once.
                links[0].Pump();
                Steamworks.SteamNetworkingSockets.BufferFull = true;
                int attempts = Steamworks.SteamNetworkingSockets.SendAttempts;
                for (int sequence = 250; sequence < 270; sequence++)
                {
                    links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = sequence });
                    links[0].TryRead(out noMessage);
                }
                Check(Steamworks.SteamNetworkingSockets.SendAttempts == attempts + 1,
                    "saturation does not repeatedly call Steam during the same pump");
                Steamworks.SteamNetworkingSockets.BufferFull = false;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode)
                    .Select(message => message.Sequence).SequenceEqual(new long[] { 269 }),
                    "congestion recovery sends only the newest adjacent state");

                // Fill the per-frame budget, then let Send itself encounter an older pending pose.
                links[0].Pump();
                for (int sequence = 400; sequence < 416; sequence++)
                    links[0].Send(new Message { Kind = Kind.Ping, Sequence = sequence });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 416 });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 417 });
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode)
                    .Select(message => message.Sequence).SequenceEqual(new long[] { 417 }),
                    "budget exhaustion retains latest state for the next pump");

                // Recovery messages must remain barriers even when normal poses coalesce.
                Steamworks.SteamNetworkingSockets.BufferFull = true;
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 300 });
                links[0].Send(new Message { Kind = Kind.ResyncBaseline, Sequence = 301, SyncRevision = 2,
                    Unit = "guest", Poses = new[] { new Pose { Id = "guest", Layout = Wire.Hash("guest"), Qw = 1 } } });
                links[0].Send(new Message { Kind = Kind.ResyncReady, Sequence = 301, SyncRevision = 2, Unit = "guest" });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 302 });
                links[0].Send(new Message { Kind = Kind.Snapshot, Sequence = 303 });
                Steamworks.SteamNetworkingSockets.BufferFull = false;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                links[0].Pump();
                Check(Steamworks.SteamNetworkingSockets.SendHistory.Select(Wire.Decode).Select(message => message.Kind)
                    .SequenceEqual(new[] { Kind.Snapshot, Kind.ResyncBaseline, Kind.ResyncReady, Kind.Snapshot }),
                    "baseline and confirmation remain ordered coalescing barriers");

                Event(20, 5, Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting);
                Check(host.Take() == null && Steamworks.SteamNetworkingSockets.Closed.Contains(20), "fourth guest refused");
                Event(21, 2, Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting);
                Check(host.Take() == null, "duplicate identity refused");
                for (int i = 0; i < 3; i++)
                {
                    links[i].Send(new Message { Kind = Kind.Ping, Sequence = i + 1 });
                    Check(Wire.Decode(Steamworks.SteamNetworkingSockets.Sent[(uint)i + 10]).Sequence == i + 1, "outgoing route " + i);
                    Steamworks.SteamNetworkingSockets.Inject((uint)i + 10, (ulong)i + 2, Wire.Encode(new Message { Kind = Kind.Ping, Sequence = 10 + i }));
                    Message m;
                    Check(links[i].TryRead(out m) && m.Sequence == 10 + i, "incoming route " + i);
                }

                Steamworks.SteamNetworkingSockets.Inject(10, 999, Wire.Encode(new Message { Kind = Kind.Ping }));
                bool rejected = false;
                try
                {
                    Message m;
                    links[0].TryRead(out m);
                }
                catch (System.IO.IOException)
                {
                    rejected = true;
                }

                Check(rejected && Steamworks.SteamNetworkingMessage_t.Released == 4, "wrong identity rejected and native message released");
                Event(11, 3, Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
                host.Pump();
                Check(!links[1].Connected && links[0].Connected && links[2].Connected && host.Error == "", "one departure preserves room");
                Event(22, 3, Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting);
                Check(host.Take() != null, "departed identity reconnects");
                Steamworks.SteamMatchmaking.Members.Remove(4);
                host.Pump();
                Check(!links[2].Connected && links[0].Connected, "lobby departure clears only its connection");
            }

            Check(Steamworks.SteamNetworkingSockets.Closed.Contains(10) &&
                Steamworks.SteamNetworkingSockets.Closed.Contains(22),
                "dispose closes remaining peers");
            using (var guest = new SteamLink(new string('a', 32), "build"))
            {
                var peer = new SteamPeer(new Steamworks.HSteamNetConnection { m_HSteamNetConnection = 99 },
                    new Steamworks.CSteamID(1));
                peer.Changed(new Steamworks.SteamNetConnectionInfo_t {
                    m_eState = Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected });
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(SteamLink).GetField("client", flags).SetValue(guest, peer);
                typeof(SteamLink).GetField("lobby", flags).SetValue(guest, new Steamworks.CSteamID(100));
                typeof(SteamLink).GetField("owner", flags).SetValue(guest, new Steamworks.CSteamID(1));
                peer.LogNativeStatus();
                Check(guest.PingMs == 82, "guest UI ping uses measured Steam RTT");
                Check(GameBridge.LastLog.Contains("queueEstimateMs=125.50") &&
                    GameBridge.LastLog.Contains("pingMs=82") &&
                    GameBridge.LastLog.Contains("pendingReliableBytes=4096") &&
                    GameBridge.LastLog.Contains("unackedReliableBytes=2048"),
                    "native queue estimate converts microseconds separately from RTT and unacked bytes");
                Steamworks.SteamNetworkingSockets.StatusResult = Steamworks.EResult.k_EResultFail;
                peer.LogNativeStatus();
                Check(GameBridge.LastLog.Contains("unavailable=") && peer.Connected,
                    "unavailable diagnostics do not report a healthy zero or disconnect");
                Steamworks.SteamNetworkingSockets.ThrowStatus = true;
                peer.LogNativeStatus();
                Check(GameBridge.LastLog.Contains("EntryPointNotFoundException") && peer.Connected,
                    "missing native diagnostic entry point preserves the connection");
                Steamworks.SteamNetworkingSockets.ThrowStatus = false;
                Steamworks.SteamNetworkingSockets.StatusResult = Steamworks.EResult.k_EResultOK;
                Steamworks.SteamNetworkingSockets.SendHistory.Clear();
                for (int frame = 0; frame < 80; frame++)
                {
                    guest.Pump();
                    guest.Send(new Message { Kind = Kind.Ping, Sequence = frame });
                }
                Check(guest.Error == "" && Steamworks.SteamNetworkingSockets.SendHistory.Count == 80,
                    "guest room replenishes peer send budget beyond the first sixteen packets");
            }
            using (var privateHost = new SteamLink(new string('a', 32), "build", true, "Test", "Mission", true))
            {
                privateHost.Host(null, 0);
                Check(Steamworks.SteamMatchmaking.Visibility == Steamworks.ELobbyType.k_ELobbyTypePrivate,
                    "test isolation overrides public room selection");
            }
            Steamworks.SteamMatchmaking.Data["mod_revision"] = "different-build";
            using (var mismatched = new SteamLink(new string('a', 32), "build"))
            {
                mismatched.Join("100", 0);
                Check(mismatched.Error.Contains("same mod DLL"), "direct room-ID join also rejects a different mod revision");
            }
            Console.WriteLine("ALL " + checks + " STEAM ADAPTER FIXTURE CHECKS PASSED; not a live Steam test.");
        }
    }
}
