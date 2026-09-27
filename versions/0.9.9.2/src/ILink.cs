using System;using System.Net;
namespace GhpcCoop {
 public interface ILink:IDisposable {bool Connected{get;}string Error{get;}void Host(IPAddress bind,int port);void Join(string address,int port);void Pump();void Send(Message m);bool TryRead(out Message m);}
}
