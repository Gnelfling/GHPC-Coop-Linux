using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        ResyncGate directResync = new ResyncGate();
        bool recoveringReplica;
        Message pendingBaseline;
        long appliedBaselineSequence, acceptedRevision;
        float lastVehicleStateAt, recoveryStarted, nextRecoveryAttempt, nextRecoveryPing, nextRosterScan;

        void ResetResync()
        {
            directResync = new ResyncGate();
            recoveringReplica = false;
            pendingBaseline = null;
            appliedBaselineSequence = acceptedRevision = 0;
            lastVehicleStateAt = recoveryStarted = nextRecoveryAttempt = nextRecoveryPing = nextRosterScan = 0;
            guestInputDue = false;
        }

        void DiscoverRosterChanges(float now)
        {
            if (now < nextRosterScan) return;
            nextRosterScan = now + 2;
            if (!game.RefreshRoster()) return;
            var choices = game.FriendlyChoices().ToArray();
            if (session != null) session.AddVehicleChoices(choices);
            if (seats != null) seats.AddVehicleChoices(choices);
            foreach (var peer in guests) peer.Session.AddVehicleChoices(choices);
            initialRoomVehicles = game.RoomChoices().Concat(new[] { game.LocalId }).ToArray();
            GameBridge.Log("RESYNC roster revision=" + game.RosterRevision);
        }

        Message NewBaseline(string unit)
        {
            var baseline = new Message
            {
                Kind = Kind.ResyncBaseline, Sequence = ++seq, SyncRevision = game.RosterRevision,
                Unit = unit, World = game.World, Poses = game.CaptureBaseline(),
                Supports = support.Capture(), Objectives = ObjectiveSync.Capture(), Weather = WeatherSync.Capture()
            };
            WeatherSync.CaptureSky(baseline);
            return baseline;
        }

        void BeginHostResync(ILink connection, ResyncGate gate, string unit, Action suspend, float now)
        {
            suspend(); // Clear held throttle, trigger and reload without releasing the reservation to AI.
            var baseline = NewBaseline(unit);
            Wire.Validate(baseline);
            gate.Begin(baseline, now);
            connection.Send(baseline);
            GameBridge.Log("RESYNC begin sequence=" + baseline.Sequence + " revision=" + baseline.SyncRevision + " unit=" + unit);
        }

        bool HandleHostResync(Message message, ILink connection, ResyncGate gate, string unit, Action suspend, float now)
        {
            if (message.Kind == Kind.ResyncRequest)
            {
                if (!gate.Pending) BeginHostResync(connection, gate, unit, suspend, now);
                return true;
            }
            if (message.Kind != Kind.ResyncAck) return false;
            if (gate.Accept(message))
            {
                connection.Send(new Message { Kind = Kind.ResyncReady, Sequence = gate.Sequence,
                    SyncRevision = gate.Revision, Unit = gate.Unit });
                GameBridge.Log("RESYNC acknowledged sequence=" + gate.Sequence + " unit=" + unit);
            }
            return true; // A delayed ACK cannot complete a newer transaction.
        }

        void RequireHostResync(ILink connection, ResyncGate gate, string unit, Action suspend, float now)
        {
            if (gate.Expired(now)) throw new IOException("Vehicle resynchronization timed out; peer did not acknowledge baseline");
            if (gate.Sequence == 0 || gate.Revision != game.RosterRevision || gate.Unit != unit)
                BeginHostResync(connection, gate, unit, suspend, now);
        }

        void RequestResync(float now, string reason)
        {
            if (recoveringReplica) return;
            recoveringReplica = true;
            recoveryStarted = now;
            pendingBaseline = null;
            support.Ready = false;
            guestInputDue = reload = firePressed = false;
            suppressFireUntilRelease = true;
            link.Send(new Message { Kind = Kind.ResyncRequest, Sequence = ++seq, Unit = game.LocalId });
            status = "Resynchronizing vehicles...";
            GameBridge.Log("RESYNC requested: " + reason);
        }

        bool HandleReplicaResync(Message message, float now)
        {
            if (message.Kind == Kind.ResyncBaseline)
            {
                if (!claimed || !Wire.SameMissionWorld(game.World, message.World) || message.Unit != game.LocalId)
                    throw new IOException("Unexpected resync mission or vehicle reservation");
                if (message.Sequence <= appliedBaselineSequence ||
                    (pendingBaseline != null && message.Sequence <= pendingBaseline.Sequence)) return true;
                if (!recoveringReplica) recoveryStarted = now;
                recoveringReplica = true;
                support.Ready = false;
                guestInputDue = reload = firePressed = false;
                suppressFireUntilRelease = true;
                pendingBaseline = message;
                nextRecoveryAttempt = 0;
                status = "Applying host vehicle baseline...";
                return true;
            }
            if (message.Kind != Kind.ResyncReady) return false;
            if (recoveringReplica && pendingBaseline == null && message.Sequence == appliedBaselineSequence &&
                message.SyncRevision == acceptedRevision && message.Unit == game.LocalId)
            {
                recoveringReplica = false;
                status = "Vehicle synchronization restored.";
                GameBridge.Log("RESYNC complete sequence=" + message.Sequence);
            }
            return true;
        }

        void UpdateReplicaResync(float now)
        {
            if (!recoveringReplica && acceptedRevision > 0 && !hostPaused && !GHPC.AarController.InAar && now - lastVehicleStateAt > 3)
                RequestResync(now, "vehicle snapshots stalled while connection remains alive");
            if (!recoveringReplica) return;
            // Commands queued during recovery must not execute after confirmation.
            infantry.Drain();
            support.Drain();
            if (now >= nextRecoveryPing)
            {
                nextRecoveryPing = now + 1;
                link.Send(new Message { Kind = Kind.Ping, Sequence = ++seq });
            }
            if (now - recoveryStarted > 15)
                throw new IOException("Vehicle resynchronization timed out" +
                    (pendingBaseline == null ? " waiting for host confirmation" : " waiting for matching mission vehicles"));
            if (pendingBaseline == null || now < nextRecoveryAttempt) return;
            nextRecoveryAttempt = now + .25f;
            string missing;
            if (!game.TryApplyBaseline(pendingBaseline, out missing))
            {
                status = "Waiting for mission vehicle: " + missing;
                return;
            }
            ApplySnapshotEnvironment(pendingBaseline);
            lastVehicleStateAt = now;
            acceptedRevision = pendingBaseline.SyncRevision;
            appliedBaselineSequence = lastSnapshot = pendingBaseline.Sequence;
            link.Send(new Message { Kind = Kind.ResyncAck, Sequence = appliedBaselineSequence,
                SyncRevision = acceptedRevision, Unit = game.LocalId });
            pendingBaseline = null;
        }

        void ApplySnapshotEnvironment(Message snapshot)
        {
            support.Apply(snapshot.Supports);
            ObjectiveSync.Apply(snapshot.Objectives);
            if (!debugSkipWeather)
            {
                weather.Apply(snapshot.Weather);
                weather.ApplySky(snapshot);
            }
        }

        void ReceiveVehicleSnapshot(Message snapshot, float now)
        {
            if (!claimed || recoveringReplica || snapshot.Sequence <= lastSnapshot) return;
            if (snapshot.SyncRevision != acceptedRevision)
            {
                RequestResync(now, "roster revision changed");
                return;
            }
            try { game.ValidateSnapshot(snapshot.Poses); }
            catch (InvalidOperationException error)
            {
                RequestResync(now, error.Message);
                return;
            }
            // Native application errors are not topology mismatches. Do not hide them
            // in an infinite retry loop or acknowledge a partially applied baseline.
            game.ReceiveSnapshot(snapshot.Poses);
            ApplySnapshotEnvironment(snapshot);
            lastSnapshot = snapshot.Sequence;
            lastVehicleStateAt = now;
        }
    }
}
