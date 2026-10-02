using System;

namespace GhpcCoop
{
    // Keep a 20 Hz schedule aligned across rendering frames. Scheduling from "now"
    // each time turns 20 Hz into 15 Hz on a steady 30 FPS host. Skip missed slots
    // after a stall: sending catch-up snapshots would only enqueue obsolete work.
    public static class NetworkSendClock
    {
        const double Interval = .05;
        public static float Next(float previousDeadline, float now)
        {
            if (previousDeadline <= 0 || now < previousDeadline)
                return now + (float)Interval;
            double missedSlots = Math.Floor((now - (double)previousDeadline) / Interval) + 1;
            float next = (float)(previousDeadline + missedSlots * Interval);
            return next > now ? next : now + (float)Interval;
        }
    }

    // Smooth presentation only: authoritative positions, hits and damage are never predicted.
    // A fixed 75 ms buffer starves when host frames or packet arrivals take longer than that.
    public sealed class ReplicaTimeline
    {
        float lastStamp, lastArrival, offset, interval, jitter;
        bool initialized, rendering;
        float localTime, remoteTime;
        public float LocalDelay { get; private set; }
        public float RemoteDelay { get; private set; }
        public float LatestStamp { get { return lastStamp; } }

        public ReplicaTimeline() { Reset(); }

        public void Reset()
        {
            initialized = rendering = false;
            lastStamp = lastArrival = offset = jitter = localTime = remoteTime = 0;
            interval = .05f;
            LocalDelay = .075f;
            RemoteDelay = .30f;
        }

        static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        public void Observe(float stamp, float arrival)
        {
            if (Single.IsNaN(stamp) || Single.IsInfinity(stamp) ||
                Single.IsNaN(arrival) || Single.IsInfinity(arrival)) return;
            if (initialized && stamp <= lastStamp) return;
            float transit = arrival - stamp;
            if (!initialized)
            {
                offset = transit;
                initialized = true;
            }
            else
            {
                float hostGap = stamp - lastStamp;
                float arrivalGap = Math.Max(0, arrival - lastArrival);
                // Pause/loading gaps should not permanently inflate the playing buffer.
                if (hostGap < 1 && arrivalGap < 1)
                {
                    interval += (Clamp(hostGap, .01f, .25f) - interval) * .1f;
                    float variation = Math.Max(0, arrivalGap - hostGap);
                    jitter = Math.Max(variation, jitter * (float)Math.Exp(-hostGap / 2));
                }
                offset = Math.Min(offset, transit);
            }
            lastStamp = stamp;
            lastArrival = arrival;
            LocalDelay = Clamp(interval * 1.5f + jitter * 1.25f, .075f, .25f);
            RemoteDelay = Clamp(.30f + jitter * .5f, .30f, .40f);
        }

        public void Advance(float now, float delta)
        {
            if (!initialized) return;
            float hostNow = now - offset;
            if (!rendering)
            {
                localTime = Math.Min(lastStamp, hostNow - LocalDelay);
                remoteTime = Math.Min(lastStamp, hostNow - RemoteDelay);
                rendering = true;
                return;
            }
            localTime = AdvanceClock(localTime, hostNow - LocalDelay, delta);
            remoteTime = AdvanceClock(remoteTime, hostNow - RemoteDelay, delta);
        }

        float AdvanceClock(float current, float target, float delta)
        {
            // Recover a long pause without replaying seconds of old motion. Ordinary jitter
            // changes playback speed slightly instead of snapping or moving time backwards.
            if (target - current > 1) return Math.Max(current, Math.Min(lastStamp, target));
            float speed = Clamp(1 + (target - current) * 2, .85f, 1.10f);
            return Math.Max(current, Math.Min(lastStamp, current + Clamp(delta, 0, .1f) * speed));
        }

        public float RenderTime(bool local) { return local ? localTime : remoteTime; }
    }
}
