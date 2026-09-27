using System;
namespace GhpcCoop
{
    // Retarget from the last rendered point, never from an older packet's start.
    // No extrapolation: a stalled connection cannot keep walking a soldier away.
    public sealed class InfantryMotion
    {
        public float X, Y, Z, Blend;
        public bool Initialized { get; private set; }
        float fromX, fromY, fromZ, targetX, targetY, targetZ, arrived, duration;
        public bool Receive(float x, float y, float z, float now, bool discontinuity)
        {
            float gap = now - arrived;
            float dx = x - X, dy = y - Y, dz = z - Z;
            bool snap = !Initialized || discontinuity || gap > .5f || dx*dx + dy*dy + dz*dz > 25;
            fromX = snap ? x : X; fromY = snap ? y : Y; fromZ = snap ? z : Z;
            targetX = x; targetY = y; targetZ = z;
            duration = snap ? 0 : Math.Max(.05f, Math.Min(.15f, gap));
            arrived = now; Initialized = true;
            Render(now); return snap;
        }
        public void Render(float now)
        {
            Blend = duration <= 0 ? 1 : Math.Max(0, Math.Min(1, (now-arrived)/duration));
            X = fromX + (targetX-fromX)*Blend;
            Y = fromY + (targetY-fromY)*Blend;
            Z = fromZ + (targetZ-fromZ)*Blend;
        }
    }
}
