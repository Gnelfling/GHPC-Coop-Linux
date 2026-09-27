using GHPC;
using UnityEngine;
namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        InfantrySync infantry=new InfantrySync();float nextInfantry;
        bool hostPaused;
        float nextPauseNotice;
        bool NotifyHostPause(float now)
        {
            bool paused = AarController.InAar || GHPC.State.TimeController.Paused;
            if (paused != hostPaused || now >= nextPauseNotice)
            {
                hostPaused = paused; nextPauseNotice = now + 1;
                Broadcast(new Message { Kind = Kind.Ping, Text = paused
                    ? (AarController.InAar ? "host-aar" : "host-paused") : "host-running" });
            }
            if (paused) status = "Host paused / AAR. Guest controls resume when the host returns.";
            return paused;
        }
        void ReceiveHostPause(string state)
        {
            if (state != "host-aar" && state != "host-paused" && state != "host-running") return;
            bool paused = state != "host-running";
            if (paused != hostPaused)
            {
                hostPaused = paused; reload = firePressed = false;
                status = paused ? "Host paused / viewing AAR. Wait for the host to resume. AAR replay is host-only."
                    : "Host resumed. Vehicle controls are active.";
                if (paused) panel = true;
            }
        }
    }
}

