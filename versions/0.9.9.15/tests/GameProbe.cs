using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Globalization;

namespace GhpcCoop
{
    class GameProbe
    {
        static Message Wait(Link l, Kind kind)
        {
            var until = DateTime.UtcNow.AddSeconds(12);
            Message m;
            while (DateTime.UtcNow < until)
            {
                while (l.TryRead(out m))
                {
                    if (m.Kind == Kind.Error)
                        throw new Exception(m.Text);
                    if (m.Kind == kind)
                        return m;
                }

                if (l.Error != "")
                    throw new Exception(l.Error);
                Thread.Sleep(10);
            }

            throw new Exception("Wait timed out: " + kind);
        }

        static void Main(string[] args)
        {
            try
            {
                var cfg = File.ReadAllLines(args[0]);
                string target = cfg[5];
                using (var link = new Link())
                {
                    link.Join("127.0.0.1", 29761);
                    link.Send(new Message { Kind = Kind.Hello, Token = cfg[0], Build = cfg[1], World = cfg[2], Roster = cfg[3] });
                    Wait(link, Kind.Welcome);
                    link.Send(new Message { Kind = Kind.Claim, Unit = target });
                    Wait(link, Kind.Claimed);
                    var start = Wait(link, Kind.Snapshot).Poses.First(p => p.Id == target);
                    var last = start;
                    long seq = 0;
                    int packets = 0;
                    // Drive a short distance, stop, aim into the sky, then request fire. No user save is involved.
                    float fx = 2 * (start.Qx * start.Qz + start.Qw * start.Qy), fz = 1 - 2 * (start.Qx * start.Qx + start.Qy * start.Qy);
                    double norm = Math.Sqrt(fx * fx + fz * fz + 0.09);
                    var began = DateTime.UtcNow;
                    Message m;
                    while ((DateTime.UtcNow - began).TotalSeconds < 16)
                    {
                        double t = (DateTime.UtcNow - began).TotalSeconds;
                        link.Send(new Message { Kind = Kind.Input,
                            Unit = target,
                            Sequence = ++seq,
                            Throttle = t < 4 ? 0.55f : 0,
                            Steer = 0,
                            AimX = (float)(fx / norm),
                            AimY = (float)(0.3 / norm),
                            AimZ = (float)(fz / norm),
                            Range = 0,
                            Role = 0,
                            Fire = t > 9 &&
                            t < 13,
                            Reload = t > 7 &&
                            t < 7.06 });
                        while (link.TryRead(out m))
                        {
                            if (m.Kind == Kind.Error)
                                throw new Exception(m.Text);
                            if (m.Kind == Kind.Snapshot)
                            {
                                last = m.Poses.First(p => p.Id == target);
                                packets++;
                            }
                        }

                        Thread.Sleep(50);
                    }

                    double dx = last.X - start.X, dy = last.Y - start.Y, dz = last.Z - start.Z;
                    double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    int shots = last.Shots - start.Shots;
                    var json = "{\"real_game_host\":true,\"guest_is_test_program\":true,\"two_game_instances\":false,\"snapshots\":" + packets + ",\"distance_metres\":" + distance.ToString("F3", CultureInfo.InvariantCulture) + ",\"shots\":" + shots + ",\"movement_passed\":" + (distance > 0.25 ? "true" : "false") + ",\"firing_passed\":" + (shots > 0 ? "true" : "false") + "}";
                    File.WriteAllText(args[1], json);
                    Console.WriteLine(json);
                    if (distance <= 0.25 || shots <= 0)
                        Environment.ExitCode = 2;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Environment.ExitCode = 1;
            }
        }
    }
}
