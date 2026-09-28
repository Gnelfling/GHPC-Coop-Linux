using System.Collections.Generic;

namespace GhpcCoop
{
    public static class MenuFireGate
    {
        public static bool Suppress(bool previouslyBlocked, bool menuOpen, bool held, bool pressed)
        {
            return menuOpen || (previouslyBlocked && (held || pressed));
        }
    }

    // Preserve the complete click sample when several inputs arrive in one host frame.
    public sealed class ShotInputLatch
    {
        sealed class Entry
        {
            public Message Input;
            public float ExpiresAt;
        }

        readonly Queue<Entry> pendingShots = new Queue<Entry>();
        bool fireWasHeld;
        int previousWeaponRole;
        public void Receive(Message input, float now)
        {
            DiscardExpiredShots(now);
            if (input.Fire && (!fireWasHeld || input.Role != previousWeaponRole) && pendingShots.Count < 4)
                pendingShots.Enqueue(new Entry { Input = input, ExpiresAt = now + .35f });
            fireWasHeld = input.Fire;
            previousWeaponRole = input.Role;
        }

        void DiscardExpiredShots(float now)
        {
            while (pendingShots.Count > 0 && pendingShots.Peek().ExpiresAt <= now)
                pendingShots.Dequeue();
        }

        public Message Select(Message latest, float now)
        {
            DiscardExpiredShots(now);
            return pendingShots.Count > 0 ? pendingShots.Peek().Input : latest;
        }

        public void Fired()
        {
            if (pendingShots.Count > 0)
                pendingShots.Dequeue();
        }

        public void Clear()
        {
            pendingShots.Clear();
            fireWasHeld = false;
            previousWeaponRole = 0;
        }
    }
}
