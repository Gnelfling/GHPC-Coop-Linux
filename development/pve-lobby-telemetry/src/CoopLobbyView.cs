using System;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        bool lobbyConnectionSettings;
        void ResetLobbyNavigation()
        {
            lobbyConnectionSettings = missionPicker = roomBrowserOpen = invitePicker = false;
        }
        Texture2D lobbyAbrams, lobbyT80, lobbyMap;
        bool lobbyAssetsLoaded;
        readonly System.Collections.Generic.Dictionary<string, Texture2D> terrainPreviews =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        Texture2D TerrainPreview(string theaterKey, out bool satellite)
        {
            satellite = false;
            string key = (theaterKey ?? "").ToUpperInvariant();
            foreach (string terrain in new[] { "GT01", "GT02", "GT03", "GT04" })
            {
                if (!key.Contains(terrain)) continue;
                satellite = true;
                Texture2D preview;
                // Decode only when first selected; OnGUI runs more than once a frame.
                if (!terrainPreviews.TryGetValue(terrain, out preview))
                    terrainPreviews[terrain] = preview = LobbyAsset("map-" + terrain + ".png");
                return preview;
            }
            return key.Contains("POINTALPHA") || key == "FG85_PA" ? lobbyMap : null;
        }
        Texture2D LobbyAsset(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GhpcCoop." + name))
            {
                if (stream == null) return null;
                using (var bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes); var image = new Texture2D(2, 2);
                    image.hideFlags = HideFlags.HideAndDontSave;
                    ImageConversion.LoadImage(image, bytes.ToArray(), true); return image;
                }
            }
        }
        void LobbyBox(Rect area)
        {
            MenuFill(area, new Color(.045f,.065f,.07f,.86f));
            var edge = new Color(.3f,.36f,.36f,.8f);
            MenuFill(new Rect(area.x,area.y,area.width,1),edge);
            MenuFill(new Rect(area.x,area.yMax-1,area.width,1),edge);
            MenuFill(new Rect(area.x,area.y,1,area.height),edge);
            MenuFill(new Rect(area.xMax-1,area.y,1,area.height),edge);
        }
        sealed class MissionChoice
        {
            public GHPC.Mission.Data.MissionTheaterScriptable Theater;
            public GHPC.Mission.Data.MissionMetaData Meta;
        }
        MissionChoice[] missionChoices;
        MissionChoice previewMission;
        string missionSearch = "";
        string previewForces = "";
        Vector2 forcesScroll;

        string MissionDetails(MissionChoice choice)
        {
            // Read the installed game's localized briefing for the playable faction.
            // Do not synthesize objectives from the mission name or expose enemy orders.
            var faction = MissionPolicy.DefaultFaction(choice.Meta);
            var briefing = choice.Meta.FactionInfo == null ? null :
                choice.Meta.FactionInfo.FirstOrDefault(x => x.Allegiance == faction);
            string description = briefing == null ? "" : briefing.Description;
            if (String.IsNullOrWhiteSpace(description))
                description = "Briefing unavailable before mission loading.";
            else
                description = System.Text.RegularExpressions.Regex.Replace(description, "<[^>]+>", "").Trim();
            return "MISSION BRIEFING\n\n" + description + "\n\nFRIENDLY FORCES\n\n" + FriendlyForces(choice);
        }

        string FriendlyForces(MissionChoice choice)
        {
            // SpawnOrder.Count counts platoons when PlatoonMode is set. Never
            // advertise that as an exact vehicle count or a guaranteed player slot.
            var meta = choice.Meta;
            if (!meta.IsFlexMission || meta.FlexMissionData == null || meta.FlexMissionData.MissionData == null)
                return "Vehicle information becomes available after mission loading.";
            var info = meta.FlexMissionData.MissionData.FriendlySpawnInfo;
            if (info == null || info.SpawnOrders == null) return "Vehicle information unavailable.";
            int vehicles = 0, platoons = 0;
            var rows = new System.Collections.Generic.List<string>();
            foreach (var order in info.SpawnOrders)
            {
                if (order == null || order.Count <= 0) continue;
                var unit = info.Faction == null || info.Faction.AllUnits == null ? null :
                    info.Faction.AllUnits.FirstOrDefault(x => x.Key == order.Key);
                string name = unit != null ? unit.FriendlyName : order.Key;
                if (String.IsNullOrEmpty(name)) name = order.Class.ToString();
                if (order.PlatoonMode) platoons += order.Count; else vehicles += order.Count;
                rows.Add(name + "  x " + order.Count + (order.PlatoonMode ? " platoon(s)" : " vehicle(s)"));
            }
            if (rows.Count == 0)
                return "Vehicle count unavailable before mission loading. This does not mean there are no vehicles.";
            string total = platoons == 0 ? "Listed vehicles: " + vehicles :
                "Listed forces: " + vehicles + " individual vehicle(s) + " + platoons + " platoon(s)";
            return total + "\n\n" + String.Join("\n", rows.ToArray()) +
                "\n\nDefault mission formation. Customization, reinforcements and controllable seats may differ. Final co-op capacity is checked after loading.";
        }
        void DrawMissionPicker()
        {
            GUI.Label(new Rect(45,35,1050,55),"SELECT MISSION",menuTitle);
            if (MenuAction(new Rect(1170,40,220,45),"BACK")) { missionPicker=false; missionChoices=null; }
            // Resolve the catalogue once per visit rather than scanning all Unity assets on every GUI event.
            if (missionChoices == null)
                missionChoices = Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>()
                    .Where(t=>t.Missions!=null).OrderBy(t=>t.Name)
                    .SelectMany(t=>t.Missions.Where(m=>m!=null && !m.IsCategory && !String.IsNullOrEmpty(m.MissionSceneReference.Name))
                    .Select(m=>new MissionChoice {Theater=t,Meta=m})).ToArray();
            GUI.Label(new Rect(45,95,100,30),"SEARCH",menuSmall);
            missionSearch = GUI.TextField(new Rect(150,92,660,35),missionSearch,80);
            var choices=missionChoices.Where(c=>String.IsNullOrEmpty(missionSearch) ||
                (c.Meta.MissionName+" "+c.Theater.Name).IndexOf(missionSearch,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            missionScroll=GUI.BeginScrollView(new Rect(40,145,785,665),missionScroll,new Rect(0,0,745,choices.Length*92));
            for(int i=0;i<choices.Length;i++)
            {
                var item=choices[i];
                if(MenuAction(new Rect(0,i*92,735,54),item.Meta.MissionName))
                {
                    previewMission=item; previewForces=MissionDetails(item); forcesScroll=Vector2.zero;
                }
                GUI.Label(new Rect(12,i*92+55,710,28),item.Theater.Name+"  |  "+(item.Meta.IsDefaultDayMission?"DAY":"NIGHT"),menuSmall);
            }
            GUI.EndScrollView();
            LobbyBox(new Rect(845,145,550,665));
            if(previewMission==null) { GUI.Label(new Rect(870,175,500,60),"Select a mission to inspect its forces.",menuText); return; }
            var detail = new GUIStyle(menuText) { wordWrap=true, richText=false };
            GUI.Label(new Rect(870,170,500,65),previewMission.Meta.MissionName,detail);
            GUI.Label(new Rect(870,235,500,55),"MAP: "+previewMission.Theater.Name,detail);
            bool satellite;
            var map=TerrainPreview(previewMission.Theater.Key,out satellite);
            if(map!=null) GUI.DrawTexture(new Rect(870,285,500,120),map,ScaleMode.ScaleToFit);
            // Give the briefing the space of an unavailable image instead of a blank panel.
            float detailsTop = map != null ? 420 : 295;
            float detailsHeight = 725 - detailsTop;
            float height = Mathf.Max(detailsHeight,detail.CalcHeight(new GUIContent(previewForces),470));
            forcesScroll=GUI.BeginScrollView(new Rect(870,detailsTop,505,detailsHeight),forcesScroll,new Rect(0,0,475,height));
            GUI.Label(new Rect(0,0,470,height),previewForces,detail);
            GUI.EndScrollView();
            if(MenuAction(new Rect(870,745,500,45),"SELECT THIS MISSION",true))
            {
                stagingTheater=previewMission.Theater.Key; stagingMission=previewMission.Meta.MissionSceneReference.Name;
                stagingTitle=previewMission.Meta.MissionName; InvalidateLobbyReady(); missionPicker=false; missionChoices=null;
            }
        }
        void DrawLobbyView()
        {
            if(!lobbyAssetsLoaded)
            {
                lobbyAssetsLoaded=true;
                lobbyAbrams=LobbyAsset("official-m1ip.jpg"); lobbyT80=LobbyAsset("official-t80b.jpg"); lobbyMap=LobbyAsset("official-point-alpha-map.png");
            }
            // Original GHPC screenshot covers the entire backdrop, not just a banner.
            // The backdrop is drawn once at screen size by DrawCoopMenu. Keep
            // outside margins visible instead of cropping a second tank image here.
            MenuFill(new Rect(0,0,1440,900),new Color(0,.015f,.02f,.22f));
            LobbyBox(new Rect(28,94,1384,756));
            if(missionPicker) { DrawMissionPicker(); return; }
            GUI.Label(new Rect(38,20,700,58),"GHPC / CO-OP",menuTitle);
            if(MenuAction(new Rect(520,35,220,38),platesEnabled?"Names: On (F9)":"Names: Off (F9)")) TogglePlates();
            if(MenuAction(new Rect(750,35,220,38),selfPlateEnabled?"My Name: On":"My Name: Off")) ToggleSelfPlate();
            GUI.Label(new Rect(1000,37,330,30),directMode?"DIRECT IP":listPublicRoom?"STEAM / PUBLIC ROOMS":"STEAM / FRIENDS",menuSmall);
            if(MenuAction(new Rect(1350,25,48,44),"X")) panel=false;
            GUI.Label(new Rect(55,110,850,55),"CO-OP LOBBY",menuTitle);
            bool waiting=stagingHost||stagingGuest;
            if(link==null) roomDisplayName=GUI.TextField(new Rect(55,170,650,40),roomDisplayName,48,menuInput);
            else GUI.Label(new Rect(55,170,690,38),(link as SteamLink)?.DisplayName ?? roomDisplayName,menuHeading);
            if(MenuAction(new Rect(1155,168,225,40),link==null?"Connection":"Leave Room"))
            { if(link==null) lobbyConnectionSettings=true; else Guard(()=>Stop()); }
            LobbyBox(new Rect(50,230,905,535)); LobbyBox(new Rect(980,230,400,535));
            GUI.Label(new Rect(68,245,440,35),"SQUAD",menuHeading);
            var steam=link as SteamLink;
            GUI.enabled=steam!=null && hosting;
            if(MenuAction(new Rect(716,242,220,38),"Invite Friend")) OpenFriendPicker(steam);
            GUI.enabled=true;
            string[] occupied=seats!=null?seats.OccupiedVehicles():occupiedVehicles.ToArray();
            if(occupied.Length==0 && game.LocalId!=null && link!=null) occupied=new[]{game.LocalId};
            int count=waiting?stagingPlayers.Length:occupied.Length;
            if(stagingHost && count==0) count=1;
            GUI.Label(new Rect(805,170,300,35),count+" / 4 PLAYERS",menuHeading);
            for(int i=0;i<4;i++)
            {
                float x=68+(i%2)*440,y=295+(i/2)*225;
                LobbyBox(new Rect(x,y,420,208));
                string name="OPEN SLOT", vehicle="", condition="";
                Texture2D thumb=null;
                if(waiting && i<stagingPlayers.Length)
                {
                    var parts=stagingPlayers[i].Split(new[]{'|'},2);
                    name=parts.Length==2?parts[1]:"Player"; condition=parts[0];
                    vehicle="Vehicle assigned after mission loading";
                }
                else if(i<occupied.Length)
                {
                    string id=occupied[i]; VehicleRecord record;
                    name=id==game.LocalId?LocalDisplayName():plateNames.ContainsKey(id)?plateNames[id]:"Player";
                    condition="CONNECTED";
                    if(game.Vehicles.TryGetValue(id,out record) && record.Unit!=null)
                    {
                        vehicle=record.Unit.FriendlyName;
                        string key=record.Unit.UniqueName.ToLowerInvariant();
                        if(key.Contains("m1ip")) thumb=lobbyAbrams;
                        else if(key.Contains("t80")||key.Contains("t-80")) thumb=lobbyT80;
                    }
                }
                else if(link==null && i==0) {name=LocalDisplayName(); vehicle="Create or join a room";}
                GUI.Label(new Rect(x+15,y+12,275,30),name,menuText);
                GUI.Label(new Rect(x+285,y+14,130,25),condition,menuSmall);
                if(thumb!=null) GUI.DrawTexture(new Rect(x+10,y+48,400,116),thumb,ScaleMode.ScaleAndCrop);
                else if(name!="OPEN SLOT") GUI.Label(new Rect(x+24,y+75,370,60),waiting?"SELECTED MISSION\n"+stagingTitle:"GHPC / CO-OP",menuHeading);
                GUI.Label(new Rect(x+12,y+172,397,33),vehicle,menuSmall);
            }
            GUI.Label(new Rect(998,245,360,36),"MISSION",menuHeading);
            var state=GHPC.State.PersistentDataManager.GetStateData();
            string title=waiting||link==null?stagingTitle:state!=null && state.MetaData!=null?state.MetaData.MissionName:"Mission";
            bool satellite;
            var preview=TerrainPreview(waiting||link==null?stagingTheater:state==null?"":state.TheaterKey,out satellite);
            if(preview!=null)
            {
                GUI.DrawTexture(new Rect(998,290,364,244),preview,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(998,536,364,22),satellite?"TERRAIN / SATELLITE PREVIEW":"POINT ALPHA / MAP",menuSmall);
            }
            else { LobbyBox(new Rect(998,290,364,264)); GUI.Label(new Rect(1020,385,320,65),"MISSION MAP\nPreview unavailable",menuHeading); }
            GUI.Label(new Rect(998,569,364,62),title,menuHeading);
            GUI.Label(new Rect(998,638,185,30),"TEAMMATE AI",menuText);
            GUI.enabled=link==null||hosting;
            if(MenuAction(new Rect(1190,631,82,37),"ON",teammateAiEnabled) && !teammateAiEnabled) {teammateAiEnabled=true; TeammateAi.Reset(); InvalidateLobbyReady(); nextPauseNotice=0;}
            if(MenuAction(new Rect(1280,631,82,37),"OFF",!teammateAiEnabled) && teammateAiEnabled) {teammateAiEnabled=false; InvalidateLobbyReady(); nextPauseNotice=0;}
            GUI.enabled=link==null || (stagingHost&&!stagingLoading);
            if(MenuAction(new Rect(998,693,364,47),"Change Mission")) missionPicker=true;
            GUI.enabled=true;
            if(link!=null) GUI.Label(new Rect(58,785,665,45),status,menuSmall);
            if(waiting)
            {
                GUI.enabled=!stagingLoading && !String.IsNullOrEmpty(stagingMission);
                if(MenuAction(new Rect(755,785,235,47),stagingReady?"READY ✓":"READY",stagingReady)) SetLobbyReady();
                GUI.enabled=stagingHost && !stagingLoading && EveryoneLobbyReady();
                if(MenuAction(new Rect(1010,785,367,47),stagingLoading?"Loading...":"Start Mission",true)) Guard(LaunchStagingMission);
            }
            else if(link==null)
            {
                // All entry points remain available regardless of launch flags.
                // Changing this selection affects only the next room connection.
                if(MenuAction(new Rect(55,785,210,47),"Servers",!directMode && listPublicRoom))
                {
                    directMode=false; listPublicRoom=true;
                    roomBrowserOpen=true; Guard(RefreshRooms);
                }
                if(MenuAction(new Rect(280,785,210,47),"Friends",!directMode && !listPublicRoom))
                {
                    directMode=false; listPublicRoom=false;
                    Guard(() => {
                        SteamLink.CheckSteam();
                        if (Steamworks.SteamUtils.IsOverlayEnabled())
                        {
                            Steamworks.SteamFriends.ActivateGameOverlay("Friends");
                            status="Join through Steam friends, or create a friends room and invite them.";
                        }
                        else status="Steam overlay unavailable. Create a friends room, then use Invite Friend.";
                    });
                }
                if(MenuAction(new Rect(505,785,210,47),"Direct IP",directMode))
                { directMode=true; lobbyConnectionSettings=true; }
                if(MenuAction(new Rect(1010,785,367,47),"Create Room",true)) Guard(CreateStagingRoom);
            }
            else
            {
                if(MenuAction(new Rect(755,785,235,47),"VEHICLE / SETTINGS")) lobbyConnectionSettings=true;
                if(MenuAction(new Rect(1010,785,367,47),"RETURN TO MISSION",true)) panel=false;
            }
            GUI.enabled=true;
            GUI.Label(new Rect(40,865,700,28),"F8 — TOGGLE LOBBY",menuSmall);
            if(link==null) GUI.Label(new Rect(285,860,1100,36),status,menuSmall);
        }
    }
}
