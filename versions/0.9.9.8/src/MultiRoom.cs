using System;using System.Collections.Generic;using System.Linq;using System.Net;using System.Net.Sockets;using System.Threading;
namespace GhpcCoop {
 public sealed class MultiRoom:IRoom {
  readonly object gate=new object();readonly Queue<Link> accepted=new Queue<Link>();
  TcpListener listener;Thread worker;volatile bool stopped;string error="";
  public int Port{get{return listener==null?0:((IPEndPoint)listener.LocalEndpoint).Port;}}
  public bool Connected{get{return !stopped;}}public string Error{get{return error;}}
  public void Host(IPAddress bind,int port){listener=new TcpListener(bind,port);listener.Start(8);worker=new Thread(delegate(){try{while(!stopped){var socket=listener.AcceptTcpClient();lock(gate){if(stopped||accepted.Count>=3){socket.Close();continue;}accepted.Enqueue(Link.Accept(socket));}}}catch(Exception e){if(!stopped)error=e.Message;}});worker.IsBackground=true;worker.Start();}
  public Link Take(){lock(gate)return accepted.Count==0?null:accepted.Dequeue();}
  ILink IRoom.Take(){return Take();}
  public void Pump(){}public void Join(string address,int port){throw new NotSupportedException();}
  public void Send(Message m){throw new NotSupportedException();}public bool TryRead(out Message m){m=null;return false;}
  public void Dispose(){stopped=true;if(listener!=null)listener.Stop();if(worker!=null)worker.Join(300);lock(gate){while(accepted.Count>0)accepted.Dequeue().Dispose();}}
 }
}
