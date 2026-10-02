using System;
using Steamworks;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        bool startupLicenseChecked, startupLicenseDenied;
        float nextLicenseCheck;

        bool ValidateStartupLicense()
        {
            if (startupLicenseDenied) { Application.Quit(); return false; }
            if (startupLicenseChecked) return true;
            float now = Time.realtimeSinceStartup;
            if (now < nextLicenseCheck) return false;
            nextLicenseCheck = now + .5f;
            try
            {
                // Allow native Steam initialization to finish, but never run a mission
                // through this mod before an authenticated current-app entitlement check.
                if (SteamAPI.GetHSteamUser().m_HSteamUser == 0 && now < 15) return false;
                SteamLink.CheckSteam();
                startupLicenseChecked = true;
                return true;
            }
            catch (Exception error)
            {
                GameBridge.Log("Startup authorization failed: " + error.Message);
                startupLicenseDenied = true;
                Application.Quit();
                return false;
            }
        }
    }
}
