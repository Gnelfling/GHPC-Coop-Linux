using System;

namespace GhpcCoop
{
    // A baseline is a transaction, not permission to change seats. Only the exact
    // revision, sequence and already-owned unit can unlock a suspended peer.
    public sealed class ResyncGate
    {
        public bool Pending { get; private set; }
        public long Revision { get; private set; }
        public long Sequence { get; private set; }
        public string Unit { get; private set; }
        public float Started { get; private set; }

        public void Begin(Message baseline, float now)
        {
            Pending = true;
            Revision = baseline.SyncRevision;
            Sequence = baseline.Sequence;
            Unit = baseline.Unit;
            Started = now;
        }

        public bool Accept(Message ack)
        {
            if (!Pending || ack.Kind != Kind.ResyncAck || ack.Sequence != Sequence ||
                ack.SyncRevision != Revision || ack.Unit != Unit) return false;
            Pending = false;
            return true;
        }

        public bool AllowsInput(Message input)
        {
            return !Pending && Sequence > 0 && input.SyncRevision == Revision && input.SyncBaseline == Sequence && input.Unit == Unit;
        }

        public bool Expired(float now) { return Pending && now - Started > 15; }
    }
}
