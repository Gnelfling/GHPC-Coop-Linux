using System;

namespace GhpcCoop
{
    // Retarget from the last rendered point, never from an older packet's start.
    // No extrapolation: a stalled connection cannot keep walking a soldier away.
    public sealed class InfantryMotion
    {
        // Timing uses the caller's monotonic seconds; distances use world-space units.
        const float MaximumReceiveGap = .5f;
        const float MaximumSmoothDistanceSquared = 25;
        const float MinimumBlendDuration = .05f;
        const float MaximumBlendDuration = .15f;

        public float X, Y, Z, Blend;
        public bool Initialized { get; private set; }

        float fromX, fromY, fromZ, targetX, targetY, targetZ, lastReceivedAt, blendDuration;
        public bool Receive(float x, float y, float z, float now, bool discontinuity)
        {
            float receiveInterval = now - lastReceivedAt;
            float deltaX = x - X, deltaY = y - Y, deltaZ = z - Z;
            bool shouldSnap = !Initialized || discontinuity ||
                receiveInterval > MaximumReceiveGap ||
                deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ > MaximumSmoothDistanceSquared;
            fromX = shouldSnap ? x : X;
            fromY = shouldSnap ? y : Y;
            fromZ = shouldSnap ? z : Z;
            targetX = x;
            targetY = y;
            targetZ = z;
            blendDuration = shouldSnap ? 0 : Math.Max(MinimumBlendDuration, Math.Min(MaximumBlendDuration, receiveInterval));
            lastReceivedAt = now;
            Initialized = true;
            Render(now);
            return shouldSnap;
        }

        public void Render(float now)
        {
            Blend = blendDuration <= 0 ? 1 : Math.Max(0, Math.Min(1, (now - lastReceivedAt) / blendDuration));
            X = fromX + (targetX - fromX) * Blend;
            Y = fromY + (targetY - fromY) * Blend;
            Z = fromZ + (targetZ - fromZ) * Blend;
        }
    }
}
