using System;
using System.Collections.Generic;
using System.Linq;

namespace GhpcCoop
{
    public static class PeerIsolation
    {
        // Snapshot the collection: quarantining a failed peer must not invalidate iteration.
        public static void Run<T>(IEnumerable<T> peers, Action<T> processPeer, Action<T, Exception> onPeerFailure)
        {
            foreach (var peer in peers.ToArray())
                try
                {
                    processPeer(peer);
                }
                catch (Exception exception)
                {
                    onPeerFailure(peer, exception);
                }
        }
    }

    // A smoke request count belongs to a controller session, not permanently to a vehicle.
    public sealed class RemoteRequestCounter
    {
        int lastAcceptedCount = -1;
        public void Reset()
        {
            lastAcceptedCount = -1;
        }

        public bool Accept(int requestCount)
        {
            if (requestCount < 0)
                throw new InvalidOperationException("Negative equipment request");
            if (lastAcceptedCount < 0)
            {
                lastAcceptedCount = requestCount;
                return false;
            }

            if (requestCount < lastAcceptedCount || (long)requestCount - lastAcceptedCount > 8)
                throw new InvalidOperationException("Invalid equipment request sequence");
            bool hasNewRequests = requestCount > lastAcceptedCount;
            lastAcceptedCount = requestCount;
            return hasNewRequests;
        }
    }
}
