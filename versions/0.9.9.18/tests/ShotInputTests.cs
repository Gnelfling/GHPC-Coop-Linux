using System;

namespace GhpcCoop
{
    class ShotInputTests
    {
        static int n;
        static void Check(bool b, string name)
        {
            if (!b)
                throw new Exception(name);
            n++;
            Console.WriteLine("PASS " + name);
        }

        static Message Sample(int sequence, bool fire, float aim, int role = 0)
        {
            return new Message
            {
                Sequence = sequence,
                Fire = fire,
                AimX = aim,
                Range = aim * 100,
                Role = role,
                Poses = new[]
                {
                    new Pose
                    {
                        Qy = aim
                    }
                }
            };
        }

        static void Main()
        {
            Check(MenuFireGate.Suppress(false, true, true, true), "menu selection click blocked");
            Check(MenuFireGate.Suppress(true, false, true, false), "held menu click stays blocked after close");
            Check(MenuFireGate.Suppress(true, false, false, true), "same-frame menu closing click blocked");
            Check(!MenuFireGate.Suppress(true, false, false, false), "release rearms fire");
            Check(!MenuFireGate.Suppress(false, false, true, true), "new gameplay click allowed");
            var latch = new ShotInputLatch();
            var click = Sample(1, true, 1);
            var release = Sample(2, false, 2);
            latch.Receive(click, 0);
            latch.Receive(release, .01f);
            Check(Object.ReferenceEquals(latch.Select(release, .02f), click), "batched release keeps click sample");
            Check(latch.Select(release, .02f).Range == 100 &&
                latch.Select(release, .02f).Poses[0].Qy == 1, "range and muzzle remain from click");
            latch.Fired();
            Check(Object.ReferenceEquals(latch.Select(release, .03f), release), "fired click is not replayed");
            latch.Receive(Sample(3, true, 3), 1);
            latch.Receive(Sample(4, true, 4), 1.01f);
            latch.Fired();
            Check(latch.Select(release, 1.02f) == release, "held samples do not queue duplicate shots");
            latch.Clear();
            latch.Receive(click, 2);
            Check(latch.Select(release, 2.36f) == release, "expired click cannot fire late");
            latch.Receive(release, 3);
            latch.Receive(click, 3.01f);
            latch.Clear();
            Check(latch.Select(release, 3.02f) == release, "vehicle transfer clears queued fire");
            var peers = new[]
            {
                new ShotInputLatch(),
                new ShotInputLatch(),
                new ShotInputLatch()
            };
            for (int i = 0; i < 3; i++)
            {
                peers[i].Receive(Sample(1, true, i + 1, i), 4);
                peers[i].Receive(release, 4.01f);
            }

            for (int i = 0; i < 3; i++)
                Check(peers[i].Select(release, 4.02f).AimX == i + 1 &&
                    peers[i].Select(release, 4.02f).Role == i, "independent guest click " + i);
            peers[0].Clear();
            Check(peers[1].Select(release, 4.03f).AimX == 2, "one disconnect preserves other guest fire");
            Console.WriteLine("ALL " + n + " SHOT INPUT CHECKS PASSED");
        }
    }
}
