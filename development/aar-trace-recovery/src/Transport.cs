using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace GhpcCoop
{
    // The worker threads own socket I/O. Unity APIs are called only by the adapter on the game thread.
    public sealed class Link : ILink
    {
        readonly Queue<Message> incoming = new Queue<Message>(), outgoing = new Queue<Message>();
        readonly object sync = new object ();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        TcpClient client;
        TcpListener listener;
        Thread reader, writer;
        volatile bool stopped;
        volatile bool connected;
        string error = "";
        public bool Connected
        {
            get
            {
                return connected;
            }

            private set
            {
                connected = value;
            }
        }

        public string Error
        {
            get
            {
                return error;
            }

            private set
            {
                error = value;
            }
        }

        public void Pump()
        {
        }

        public int Port { get; private set; }

        public void Host(IPAddress bind, int port)
        {
            listener = new TcpListener(bind, port);
            listener.Start(1);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            reader = Start(delegate
            {
                Attach(listener.AcceptTcpClient());
            });
        }

        public void Join(string address, int port)
        {
            reader = Start(delegate
            {
                var c = new TcpClient();
                client = c;
                var ar = c.BeginConnect(address, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(5000))
                    throw new TimeoutException("Connect timeout");
                c.EndConnect(ar);
                Attach(c);
            });
        }

        public static Link Accept(TcpClient client)
        {
            var link = new Link();
            link.reader = link.Start(delegate
            {
                link.Attach(client);
            });
            return link;
        }

        Thread Start(ThreadStart action)
        {
            var t = new Thread(delegate ()
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    if (!stopped)
                        Error = e.Message;
                }
                finally
                {
                    Stop();
                }
            });
            t.IsBackground = true;
            t.Start();
            return t;
        }

        void Attach(TcpClient c)
        {
            client = c;
            if (stopped)
            {
                c.Close();
                return;
            }

            c.NoDelay = true;
            c.ReceiveTimeout = 180000;
            c.SendTimeout = 3000;
            Connected = true;
            writer = Start(delegate
            {
                while (!stopped)
                {
                    Message m = null;
                    lock (sync)
                    {
                        if (outgoing.Count > 0)
                            m = outgoing.Dequeue();
                    }

                    if (m == null)
                    {
                        wake.WaitOne(100);
                        continue;
                    }

                    Wire.WriteFrame(c.GetStream(), Wire.Encode(m));
                }
            });
            while (!stopped)
            {
                var m = Wire.Decode(Wire.ReadFrame(c.GetStream()));
                lock (sync)
                {
                    if (incoming.Count >= 64)
                        throw new IOException("Receive queue overflow");
                    incoming.Enqueue(m);
                }
            }
        }

        public void Send(Message m)
        {
            if (stopped)
                throw new IOException("Connection closed");
            Wire.Validate(m);
            lock (sync)
            {
                if (outgoing.Count >= 16)
                    throw new IOException("Send queue overflow");
                outgoing.Enqueue(m);
            }

            wake.Set();
        }

        public bool TryRead(out Message m)
        {
            lock (sync)
            {
                if (incoming.Count == 0)
                {
                    m = null;
                    return false;
                }

                m = incoming.Dequeue();
                return true;
            }
        }

        void Stop()
        {
            stopped = true;
            Connected = false;
            try
            {
                if (listener != null)
                    listener.Stop();
            }
            catch
            {
            // Continue socket shutdown and wake the worker even if this handle is already closed.
            }

            try
            {
                if (client != null)
                    client.Close();
            }
            catch
            {
            // Continue socket shutdown and wake the worker even if this handle is already closed.
            }

            wake.Set();
        }

        public void Dispose()
        {
            Stop();
            if (reader != null && reader != Thread.CurrentThread)
                reader.Join(250);
            if (writer != null && writer != Thread.CurrentThread)
                writer.Join(250);
        }
    }
}

