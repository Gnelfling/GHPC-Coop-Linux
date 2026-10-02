using System;
using System.Collections.Generic;
using System.Linq;

namespace GhpcCoop
{
    public sealed class RoomSeats
    {
        readonly HashSet<string> allowed;
        readonly Dictionary<int, string> seats = new Dictionary<int, string>();
        public int Capacity { get; private set; }

        public int Count
        {
            get
            {
                return seats.Count;
            }
        }

        public RoomSeats(string host, IEnumerable<string> vehicles, IEnumerable<string> switchVehicles = null)
        {
            allowed = new HashSet<string>(vehicles, StringComparer.Ordinal);
            if (!allowed.Contains(host))
                throw new ArgumentException("Host must belong to platoon");
            Capacity = Math.Min(4, allowed.Count);
            seats.Add(0, host);
            if (switchVehicles != null)
                allowed.UnionWith(switchVehicles);
        }

        // Reinforcements can become selectable without replacing occupied seats.
        public void AddVehicleChoices(IEnumerable<string> vehicles)
        {
            allowed.UnionWith(vehicles);
        }

        public string Vehicle(int peer)
        {
            string v;
            return seats.TryGetValue(peer, out v) ? v : null;
        }

        public bool Occupied(string unit)
        {
            return seats.Values.Contains(unit);
        }

        public string Reserve(int peer, IEnumerable<string> available)
        {
            if (peer <= 0)
                throw new ArgumentException("Guest id required");
            if (seats.ContainsKey(peer))
                return seats[peer];
            if (Count >= Capacity)
                return null;
            var unit = available.FirstOrDefault(v => allowed.Contains(v) && !Occupied(v));
            if (unit != null)
                seats.Add(peer, unit);
            return unit;
        }

        public bool CanMove(int peer, string unit)
        {
            return seats.ContainsKey(peer) && allowed.Contains(unit) && (seats[peer] == unit || !Occupied(unit));
        }

        public bool Move(int peer, string unit)
        {
            if (!CanMove(peer, unit))
                return false;
            seats[peer] = unit;
            return true;
        }

        public void Release(int peer)
        {
            if (peer > 0)
                seats.Remove(peer);
        }

        public string[] OccupiedVehicles()
        {
            return seats.Values.ToArray();
        }
    }
}
