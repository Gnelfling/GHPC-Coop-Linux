using System;
using Steamworks;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        bool invitePicker;
        Vector2 friendScroll;
        string inviteNotice = "";
        void OpenFriendPicker(SteamLink steam)
        {
            invitePicker = true;
            inviteNotice = "Choose a friend to send a Steam invitation.";
            try
            {
                GameBridge.Log("INVITE clicked overlayEnabled=" + SteamUtils.IsOverlayEnabled());
                steam.InviteOverlay();
            }
            catch (Exception e)
            {
                inviteNotice = "Overlay unavailable. You can invite below.";
                GameBridge.Log("INVITE overlay: " + e.Message);
            }
        }

        static string FriendPresence(EPersonaState state)
        {
            switch (state)
            {
                case EPersonaState.k_EPersonaStateOnline:
                    return "ONLINE";
                case EPersonaState.k_EPersonaStateBusy:
                    return "BUSY";
                case EPersonaState.k_EPersonaStateAway:
                    return "AWAY";
                case EPersonaState.k_EPersonaStateSnooze:
                    return "SNOOZE";
                case EPersonaState.k_EPersonaStateLookingToTrade:
                    return "LOOKING TO TRADE";
                case EPersonaState.k_EPersonaStateLookingToPlay:
                    return "LOOKING TO PLAY";
                default:
                    return "OFFLINE";
            }
        }

        void DrawFriendPicker()
        {
            if (!invitePicker)
                return;
            var steam = link as SteamLink;
            if (steam == null || steam.RoomId == "")
            {
                invitePicker = false;
                return;
            }

            GUI.enabled = true;
            GUI.DrawTexture(new Rect(90, 230, 860, 525), menuField);
            GUI.DrawTexture(new Rect(90, 230, 860, 4), menuPrimary);
            GUI.Label(new Rect(114, 250, 680, 32), "INVITE YOUR CREW", menuHeading);
            if (GUI.Button(new Rect(840, 249, 86, 32), "CLOSE", menuButton))
            {
                invitePicker = false;
                return;
            }

            GUI.Label(new Rect(114, 291, 790, 48), inviteNotice, menuSmall);
            var nameStyle = new GUIStyle(menuText)
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
                fontSize = 17
            };
            var presenceStyle = new GUIStyle(menuSmall)
            {
                fontSize = 12,
                wordWrap = false
            };
            try
            {
                int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
                friendScroll = GUI.BeginScrollView(new Rect(114, 348, 812, 326), friendScroll, new Rect(0, 0, 787, Mathf.Max(326, count * 68)));
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        float y = i * 68;
                        if (y + 64 < friendScroll.y || y > friendScroll.y + 326)
                            continue;
                        var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                        var state = SteamFriends.GetFriendPersonaState(id);
                        GUI.DrawTexture(new Rect(0, y, 782, 62), menuIdle);
                        string name = SteamFriends.GetFriendPersonaName(id).Replace("\n", " ").Replace("\r", " ");
                        var info = new System.Globalization.StringInfo(name);
                        int n = info.LengthInTextElements;
                        if (n > 42)
                            name = info.SubstringByTextElements(0, 42) + "…";
                        GUI.Label(new Rect(16, y + 8, 531, 27), new GUIContent(name, SteamFriends.GetFriendPersonaName(id)), nameStyle);
                        presenceStyle.normal.textColor = state == EPersonaState.k_EPersonaStateOffline ? MenuMuted : MenuAccent;
                        GUI.Label(new Rect(16, y + 35, 531, 21), FriendPresence(state), presenceStyle);
                        if (GUI.Button(new Rect(591, y + 12, 179, 38), "SEND INVITE", menuPrimaryButton))
                        {
                            bool sent = SteamMatchmaking.InviteUserToLobby(new CSteamID(ulong.Parse(steam.RoomId)), id);
                            inviteNotice = sent ? "Invitation sent. Waiting for your friend." : "Invitation unavailable. Try again or share the Room ID.";
                            GameBridge.Log("INVITE request accepted=" + sent);
                        }
                    }
                }
                finally
                {
                    GUI.EndScrollView();
                }

                if (count == 0)
                    inviteNotice = "No Steam friends found. Share the Room ID instead.";
            }
            catch (Exception e)
            {
                inviteNotice = "Friend list unavailable. Share the Room ID.";
                GameBridge.Log("INVITE picker: " + e.Message);
            }

            if (GUI.Button(new Rect(114, 699, 812, 36), "COPY STEAM ROOM ID", menuButton))
            {
                GUIUtility.systemCopyBuffer = steam.RoomId;
                inviteNotice = "Room ID copied.";
            }
        }

        bool steamLaunchChecked;
        string steamRoom = "", steamNotice = "Steam: waiting for GHPC initialization";
        Callback<GameLobbyJoinRequested_t> steamInvite;
        float nextSteamCheck;
        ulong invitedRoom;
        void PumpSteamMenu()
        {
            try
            {
                if (SteamAPI.GetHSteamUser().m_HSteamUser == 0)
                    return;
                if (steamInvite == null)
                    steamInvite = Callback<GameLobbyJoinRequested_t>.Create(delegate (GameLobbyJoinRequested_t r)
                    {
                        invitedRoom = r.m_steamIDLobby.m_SteamID;
                        panel = true;
                    });
                SteamAPI.RunCallbacks();
                if (!steamLaunchChecked)
                {
                    steamLaunchChecked = true;
                    var args = Environment.GetCommandLineArgs();
                    for (int i = 0; i + 1 < args.Length; i++)
                        if (args[i] == "+connect_lobby")
                        {
                            ulong room;
                            if (ulong.TryParse(args[i + 1], out room))
                                invitedRoom = room;
                        }
                }

                if (Time.realtimeSinceStartup > nextSteamCheck)
                {
                    nextSteamCheck = Time.realtimeSinceStartup + 3;
                    SteamRelayNetworkStatus_t relay;
                    steamNotice = "Steam: " + (SteamUser.BLoggedOn() ? "online" : "offline") + " | Relay: " + SteamNetworkingUtils.GetRelayNetworkStatus(out relay);
                }

                if (invitedRoom != 0)
                {
                    if (link != null)
                    {
                        steamNotice = "Disconnect before joining another Steam room.";
                        invitedRoom = 0;
                    }
                    else
                    {
                        steamRoom = invitedRoom.ToString();
                        invitedRoom = 0;
                        Guard(delegate
                        {
                            Start(false, true);
                        });
                    }
                }
            }
            catch (Exception e)
            {
                steamNotice = "Steam unavailable: " + e.Message;
            }
        }

        void DrawSteamMenu()
        {
            GUI.enabled = link == null;
            GUILayout.Label(steamNotice);
            GUILayout.Label("Steam co-op: up to 4 players.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Steam Room ID", GUILayout.Width(110));
            steamRoom = GUILayout.TextField(steamRoom, 20);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Steam Room"))
                Guard(delegate
                {
                    Start(true, true);
                });
            if (GUILayout.Button("Join Steam Room"))
                Guard(delegate
                {
                    Start(false, true);
                });
            GUILayout.EndHorizontal();
            var steam = link as SteamLink;
            if (steam != null && steam.RoomId != "")
            {
                GUI.enabled = true;
                GUILayout.Label("Steam Room ID: " + steam.RoomId);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy Steam Room ID"))
                    GUIUtility.systemCopyBuffer = steam.RoomId;
                if (hosting && GUILayout.Button("Invite Steam Friend"))
                    OpenFriendPicker(steam);
                GUILayout.EndHorizontal();
            }
        }
    }
}
