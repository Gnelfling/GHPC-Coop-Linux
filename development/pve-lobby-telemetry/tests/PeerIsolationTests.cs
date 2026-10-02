using System;
using System.Collections.Generic;

namespace GhpcCoop
{
    class PeerIsolationTests
    {
        static int checks;
        static void Check(bool b, string name)
        {
            if (!b)
                throw new Exception(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }

        static void Main()
        {
            foreach (int count in new[]
            {
                1,
                2,
                3
            }

            )
                foreach (int fault in new[]
                {
                    1,
                    count
                }

                )
                {
                    var peers = new List<int>();
                    for (int i = 1; i <= count; i++)
                        peers.Add(i);
                    var visited = new List<int>();
                    int dropped = 0;
                    PeerIsolation.Run(peers, p =>
                    {
                        if (p == fault)
                            throw new InvalidOperationException("vehicle failure");
                        visited.Add(p);
                    }, (p, e) =>
                    {
                        peers.Remove(p);
                        dropped++;
                    });
                    Check(dropped == 1 &&
                        peers.Count == count - 1 &&
                        visited.Count == count - 1, "isolated failure for " + (count + 1) + " players / peer " + fault);
                    visited.Clear();
                    PeerIsolation.Run(peers, p => visited.Add(p), (p, e) =>
                    {
                        throw e;
                    });
                    Check(visited.Count == count - 1, "survivors processed next phase");
                }

            var counter = new RemoteRequestCounter();
            Check(!counter.Accept(7), "first owner history does not replay smoke");
            Check(counter.Accept(8), "new owner request fires");
            Check(!counter.Accept(8), "duplicate packet cannot refire");
            counter.Reset();
            Check(!counter.Accept(0), "new controller starts at zero after transfer");
            Check(counter.Accept(1), "new controller smoke works");
            counter.Reset();
            Check(!counter.Accept(100), "returning controller establishes nonzero baseline");
            Check(counter.Accept(101), "returning controller next request works");
            bool rejected = false;
            try
            {
                counter.Accept(99);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Check(rejected, "counter rollback rejected within same control session");
            rejected = false;
            try
            {
                counter.Accept(120);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Check(rejected, "excess request jump rejected");
            Check(counter.Accept(102), "invalid request cannot corrupt last accepted counter");
            Console.WriteLine("ALL " + checks + " PEER ISOLATION CHECKS PASSED");
        }
    }
}
