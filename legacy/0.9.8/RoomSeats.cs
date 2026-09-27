using System;using System.Collections.Generic;using System.Linq;
namespace GhpcCoop {
 public sealed class RoomSeats {
  readonly HashSet<string> allowed;readonly Dictionary<int,string> seats=new Dictionary<int,string>();
  public int Capacity{get;private set;}public int Count{get{return seats.Count;}}
  public RoomSeats(string host,IEnumerable<string> vehicles){allowed=new HashSet<string>(vehicles,StringComparer.Ordinal);if(!allowed.Contains(host))throw new ArgumentException("Host must belong to platoon");Capacity=Math.Min(4,allowed.Count);seats.Add(0,host);}
  public string Vehicle(int peer){string v;return seats.TryGetValue(peer,out v)?v:null;}
  public bool Occupied(string unit){return seats.Values.Contains(unit);}
  public string Reserve(int peer,IEnumerable<string> available){if(peer<=0)throw new ArgumentException("Guest id required");if(seats.ContainsKey(peer))return seats[peer];if(Count>=Capacity)return null;var unit=available.FirstOrDefault(v=>allowed.Contains(v)&&!Occupied(v));if(unit!=null)seats.Add(peer,unit);return unit;}
  public bool Move(int peer,string unit){if(!seats.ContainsKey(peer)||!allowed.Contains(unit))return false;if(seats[peer]==unit)return true;if(Occupied(unit))return false;seats[peer]=unit;return true;}
  public void Release(int peer){if(peer>0)seats.Remove(peer);}
  public string[] OccupiedVehicles(){return seats.Values.ToArray();}
 }
}
