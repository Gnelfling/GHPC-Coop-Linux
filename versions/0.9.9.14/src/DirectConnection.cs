using System;
using System.Linq;
using System.IO;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        bool directMode, directLocalOnly = true;
        string directAddress = "127.0.0.1", directPort = "22395", directCode = "", testPlayerName = "";
        int DirectPort()
        {
            int port;
            if (!Int32.TryParse(directPort, out port) || port < 1 || port > 65535)
                throw new InvalidOperationException("Port must be between 1 and 65535");
            return port;
        }

        void ConfigureDirect()
        {
            var args = Environment.GetCommandLineArgs();
            directMode = args.Contains("--coop-direct");
            foreach (var arg in args)
            {
                if (arg.StartsWith("--coop-port="))
                    directPort = arg.Substring(12);
                if (arg.StartsWith("--coop-address="))
                    directAddress = arg.Substring(15);
                if (arg.StartsWith("--coop-name="))
                    testPlayerName = arg.Substring(12);
            }
        }

        void AutoJoinTest()
        {
            var cfg = File.ReadAllLines(joinFile);
            if (directMode)
            {
                if (cfg.Length < 6)
                    throw new IOException("Incomplete test room file");
                directCode = cfg[0];
            }

            Start(false, !directMode);
        }

        static string CleanPlayerName(string name)
        {
            var clean = new string ((name ?? "").Where(c => !Char.IsControl(c) &&
                !Char.IsSurrogate(c) &&
                Char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).Take(32).ToArray());
            return String.IsNullOrWhiteSpace(clean) ? "Player" : clean;
        }

        string LocalDisplayName()
        {
            if (testPlayerName != "")
                return CleanPlayerName(testPlayerName);
            try
            {
                return CleanPlayerName(Steamworks.SteamFriends.GetPersonaName());
            }
            catch
            {
                return "Player";
            }
        }

        string RoomNameRows()
        {
            var rows = new System.Collections.Generic.List<string>();
            Action<string, string> add = (id, name) => rows.Add(id + ":" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(CleanPlayerName(name))));
            add(game.LocalId, LocalDisplayName());
            foreach (var p in guests.Where(x => x.Claimed))
            {
                var sp = p.Link as SteamPeer;
                string name = p.Name;
                if (sp != null)
                    try
                    {
                        name = Steamworks.SteamFriends.GetFriendPersonaName(sp.Identity);
                    }
                    catch
                    {
                    // Keep the fallback player name when the optional Steam persona lookup fails.
                    }

                add(seats.Vehicle(p.Id), name);
            }

            return String.Join("\n", rows.ToArray());
        }

        readonly System.Collections.Generic.Dictionary<string, string> roomPlayerNames = new System.Collections.Generic.Dictionary<string, string>();
        void ReceivePlayerNames(string data)
        {
            if (data == null || data.Length > 2048)
                throw new IOException("Invalid player name list");
            var rows = data.Split('\n');
            if (rows.Length > 4)
                throw new IOException("Too many player names");
            var names = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var row in rows)
            {
                var pair = row.Split(':');
                if (pair.Length != 2 || !game.Vehicles.ContainsKey(pair[0]) || names.ContainsKey(pair[0]))
                    throw new IOException("Invalid player name assignment");
                names.Add(pair[0], CleanPlayerName(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(pair[1]))));
            }

            roomPlayerNames.Clear();
            foreach (var pair in names)
                roomPlayerNames.Add(pair.Key, pair.Value);
        }
    }
}


