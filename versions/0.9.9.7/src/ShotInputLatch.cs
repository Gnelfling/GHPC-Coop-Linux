using System.Collections.Generic;
namespace GhpcCoop {
 // Preserve the complete click sample when several inputs arrive in one host frame.
 public sealed class ShotInputLatch {
  sealed class Entry {public Message Input;public float Expires;}
  readonly Queue<Entry> pending=new Queue<Entry>();bool held;int role;
  public void Receive(Message input,float now){
   Expire(now);
   if(input.Fire&&(!held||input.Role!=role)&&pending.Count<4)pending.Enqueue(new Entry{Input=input,Expires=now+.35f});
   held=input.Fire;role=input.Role;
  }
  void Expire(float now){while(pending.Count>0&&pending.Peek().Expires<=now)pending.Dequeue();}
  public Message Select(Message latest,float now){Expire(now);return pending.Count>0?pending.Peek().Input:latest;}
  public void Fired(){if(pending.Count>0)pending.Dequeue();}
  public void Clear(){pending.Clear();held=false;role=0;}
 }
}
