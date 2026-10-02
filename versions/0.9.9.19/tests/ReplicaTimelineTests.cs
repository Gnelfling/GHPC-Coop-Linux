using System;
using GhpcCoop;

static class ReplicaTimelineTests
{
    static int checks;
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
        checks++;
    }

    static void Main()
    {
        // Compare the old relative deadline with the corrected schedule at host frame rates.
        foreach (int fps in new[] { 30, 45, 60, 144 })
        {
            float deadline = 0, oldDeadline = 0;
            int sent = 0, oldSent = 0;
            for (int frame = 0; frame < fps * 10; frame++)
            {
                float now = frame / (float)fps;
                if (now >= deadline)
                {
                    sent++;
                    deadline = NetworkSendClock.Next(deadline, now);
                    Check(deadline > now, "deadline always skips elapsed slots");
                }
                if (now >= oldDeadline) { oldSent++; oldDeadline = now + .05f; }
            }
            Check(sent >= 198 && sent <= 201, "20 Hz stays stable at " + fps + " FPS");
            Console.WriteLine("CADENCE fps=" + fps + " old=" + oldSent + " new=" + sent + " over 10 seconds");
        }
        float afterStall = NetworkSendClock.Next(.05f, 5.023f);
        Check(afterStall > 5.023f && afterStall <= 5.0731f,
            "long stalls skip missed updates without creating catch-up traffic");
        var timeline = new ReplicaTimeline();
        for (int i = 0; i < 100; i++) timeline.Observe(i * .05f, 2 + i * .05f);
        Check(Math.Abs(timeline.LocalDelay - .075f) < .001f, "stable 20 Hz retains low delay");
        timeline.Observe(5, 7.2f);
        Check(timeline.LocalDelay > .15f && timeline.LocalDelay <= .25f, "late arrival increases bounded buffer");
        float delay = timeline.LocalDelay;
        timeline.Observe(4, 8);
        Check(timeline.LocalDelay == delay && timeline.LatestStamp == 5, "stale timestamps cannot distort timing");
        timeline.Observe(float.NaN, 8);
        Check(timeline.LatestStamp == 5, "invalid timestamp ignored");
        timeline.Advance(7.2f, .016f);
        float previous = timeline.RenderTime(true);
        for (int i = 0; i < 300; i++)
        {
            timeline.Advance(7.2f + i / 60f, 1f / 60);
            Check(timeline.RenderTime(true) >= previous && timeline.RenderTime(true) <= timeline.LatestStamp,
                "render clock stays monotonic and never predicts beyond host state");
            previous = timeline.RenderTime(true);
        }
        timeline.Reset();
        Check(timeline.LocalDelay == .075f, "session reset clears jitter history");

        // Twenty host updates per second arrive in 150 ms bursts. This models jitter,
        // not a claimed improvement in real Steam latency or game FPS.
        int nextPacket = 0, fixedStarved = 0, adaptiveStarved = 0;
        for (int frame = 0; frame < 1200; frame++)
        {
            float hostNow = frame / 60f;
            while (Math.Ceiling(nextPacket * .05 / .15) * .15 <= hostNow)
            {
                timeline.Observe(nextPacket * .05f, hostNow + 2);
                nextPacket++;
            }
            timeline.Advance(hostNow + 2, 1f / 60);
            if (hostNow < 2) continue;
            if (hostNow - .075f >= timeline.LatestStamp) fixedStarved++;
            if (timeline.RenderTime(true) >= timeline.LatestStamp) adaptiveStarved++;
        }
        Check(adaptiveStarved < fixedStarved / 2, "adaptive buffer reduces starvation under burst delivery");
        Console.WriteLine("PASS " + checks + " replica timeline assertions; burst starvation fixed=" +
            fixedStarved + " adaptive=" + adaptiveStarved + ". Synthetic timing, not Steam gameplay.");
    }
}
