using System;using System.Collections.Generic;using System.Linq;
namespace GhpcCoop {
 public static class PeerIsolation {
  // Snapshot the collection: quarantining a failed peer must not invalidate iteration.
  public static void Run<T>(IEnumerable<T> peers,Action<T> work,Action<T,Exception> failed){
   foreach(var peer in peers.ToArray())try{work(peer);}catch(Exception e){failed(peer,e);}
  }
 }
  // A smoke request count belongs to a controller session, not permanently to a vehicle.
 public sealed class RemoteRequestCounter {
  int last=-1;
  public void Reset(){last=-1;}
  public bool Accept(int value){
   if(value<0)throw new InvalidOperationException("Negative equipment request");
   if(last<0){last=value;return false;}
   if(value<last||(long)value-last>8)throw new InvalidOperationException("Invalid equipment request sequence");
   bool changed=value>last;last=value;return changed;
  }
 }
}
