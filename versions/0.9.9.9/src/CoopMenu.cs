using System;using System.IO;using System.Linq;using System.Reflection;using UnityEngine;
namespace GhpcCoop {
 public sealed partial class CoopLabMod {
  const float MenuWidth=1040,MenuHeight=850;
  Texture2D menuFrame,menuHero,menuIdle,menuHover,menuPressed,menuField,menuPrimary,menuPrimaryHover;
  GUIStyle menuText,menuSmall,menuTitle,menuBrand,menuHeading,menuButton,menuPrimaryButton,menuInput,menuStatus;
  Font menuCondensed,menuBody;Vector2 menuVehicleScroll;int roomCapacity;
  static readonly Color MenuInk=new Color(.89f,.9f,.86f),MenuMuted=new Color(.65f,.7f,.66f),MenuAccent=new Color(.66f,.70f,.41f);
  Texture2D MenuSurface(Color fill,Color edge){
   var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.hideFlags=HideFlags.HideAndDontSave;
   var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){bool border=x==0||y==0||x==31||y==31;float grain=((x*73+y*151)%17-8)*.0015f;pixels[y*32+x]=border?edge:new Color(fill.r+grain,fill.g+grain,fill.b+grain,fill.a);}
   texture.SetPixels(pixels);texture.Apply(false,true);return texture;
  }
  void EnsureCoopMenu(){
   if(menuFrame!=null)return;
   menuCondensed=Font.CreateDynamicFontFromOSFont(new[]{"Arial"},24);
   menuBody=Font.CreateDynamicFontFromOSFont(new[]{"Arial","Malgun Gothic"},20);
   menuFrame=new Texture2D(1040,850,TextureFormat.RGBA32,false);menuFrame.hideFlags=HideFlags.HideAndDontSave;
   var pixels=new Color32[1040*850];for(int y=0;y<850;y++)for(int x=0;x<1040;x++){
    int d=Math.Min(Math.Min(x,1039-x),Math.Min(y,849-y));int corner=Math.Min(x,1039-x)+Math.Min(y,849-y);
    bool outside=corner<22;int grain=(x*31+y*71+(x*y)%23)%7;
    Color c=new Color((15+grain)/255f,(23+grain)/255f,(21+grain)/255f,.97f);
    if(d<3||corner<25)c=new Color(.045f,.065f,.06f,1);else if(d<5||corner<28)c=new Color(.34f,.37f,.25f,1);else if(d<9)c=new Color(.12f,.16f,.14f,1);
    pixels[y*1040+x]=outside?new Color32(0,0,0,0):(Color32)c;
   }menuFrame.SetPixels32(pixels);menuFrame.Apply(false,true);
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GhpcCoop.MenuHero.png"))if(stream!=null){using(var bytes=new MemoryStream()){stream.CopyTo(bytes);menuHero=new Texture2D(2,2);menuHero.hideFlags=HideFlags.HideAndDontSave;ImageConversion.LoadImage(menuHero,bytes.ToArray(),true);}}
   menuIdle=MenuSurface(new Color(.14f,.17f,.16f,1),new Color(.36f,.41f,.36f));
   menuHover=MenuSurface(new Color(.24f,.29f,.24f,1),MenuAccent);menuPressed=MenuSurface(new Color(.09f,.12f,.09f,1),MenuAccent);
   menuField=MenuSurface(new Color(.025f,.042f,.038f,1),new Color(.42f,.47f,.40f));
   menuPrimary=MenuSurface(new Color(.28f,.34f,.18f,1),new Color(.52f,.59f,.32f));menuPrimaryHover=MenuSurface(new Color(.39f,.45f,.25f,1),MenuAccent);
   menuText=new GUIStyle(){font=menuBody,fontSize=18,wordWrap=true};menuText.normal.textColor=MenuInk;
   menuSmall=new GUIStyle(menuText){fontSize=15};menuSmall.normal.textColor=MenuMuted;
   menuTitle=new GUIStyle(menuText){font=menuCondensed,fontSize=44,fontStyle=FontStyle.Bold,wordWrap=false};
   menuBrand=new GUIStyle(menuTitle){fontSize=82};menuHeading=new GUIStyle(menuTitle){fontSize=22};
   menuStatus=new GUIStyle(menuText){fontSize=16,clipping=TextClipping.Clip};
   menuButton=new GUIStyle(){font=menuCondensed,fontSize=21,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,border=new RectOffset(2,2,2,2),padding=new RectOffset(12,12,6,6)};
   menuButton.normal.background=menuIdle;menuButton.hover.background=menuHover;menuButton.active.background=menuPressed;menuButton.focused.background=menuHover;
   menuButton.normal.textColor=menuButton.hover.textColor=menuButton.active.textColor=menuButton.focused.textColor=MenuInk;
   menuPrimaryButton=new GUIStyle(menuButton);menuPrimaryButton.normal.background=menuPrimary;menuPrimaryButton.hover.background=menuPrimaryButton.focused.background=menuPrimaryHover;
   menuInput=new GUIStyle(){font=menuBody,fontSize=19,alignment=TextAnchor.MiddleLeft,border=new RectOffset(2,2,2,2),padding=new RectOffset(12,12,6,6)};
   menuInput.normal.background=menuInput.active.background=menuField;menuInput.focused.background=menuHover;menuInput.normal.textColor=menuInput.focused.textColor=menuInput.active.textColor=MenuInk;
  }
  void MenuFill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
  void MenuSection(float y,string title){
   MenuFill(new Rect(20,y,1000,1),new Color(.35f,.4f,.28f,.8f));MenuFill(new Rect(20,y+1,250,31),new Color(.28f,.32f,.22f,.5f));
   GUI.Label(new Rect(34,y+3,270,30),title,menuHeading);
   for(int i=0;i<3;i++)MenuFill(new Rect(280+i*10,y+8,5,16),new Color(.43f,.48f,.29f,.7f));
  }
  bool MenuAction(Rect rect,string text,bool primary=false){return GUI.Button(rect,text,primary?menuPrimaryButton:menuButton);}
  string MenuConnectionTitle(){if(link==null)return needsReload?"DISCONNECTED":"READY TO CONNECT";if(multiRoom!=null)return claimed?"HOST / CONNECTED":"HOST / WAITING FOR PLAYERS";if(claimed)return hosting?"HOST / CONNECTED":"GUEST / CONNECTED";return loadingGuest?"LOADING HOST MISSION":"CONNECTING";}
  void DrawCoopMenu(){
   if(!panel)return;EnsureCoopMenu();
   var matrix=GUI.matrix;var enabled=GUI.enabled;var color=GUI.color;var background=GUI.backgroundColor;var content=GUI.contentColor;
   try{
    GUI.enabled=true;GUI.color=GUI.backgroundColor=GUI.contentColor=Color.white;
    float scale=Mathf.Min(1.15f,Mathf.Min((Screen.width-24)/MenuWidth,(Screen.height-24)/MenuHeight));scale=Mathf.Max(.25f,scale*.85f);
    float left=16f,top=16f;
    GUI.matrix=Matrix4x4.TRS(new Vector3(left,top,0),Quaternion.identity,new Vector3(scale,scale,1));
    if(invitePicker){DrawFriendPicker();return;}
    GUI.DrawTexture(new Rect(0,0,MenuWidth,MenuHeight),menuFrame);
    // All wording is rendered live. The hero contains no baked-in labels or version.
    if(menuHero!=null){GUI.DrawTexture(new Rect(12,12,1016,125),menuHero,ScaleMode.ScaleAndCrop);MenuFill(new Rect(12,12,1016,125),new Color(.015f,.025f,.02f,.64f));}
    GUI.Label(new Rect(36,17,360,92),"GHPC",menuBrand);GUI.Label(new Rect(43,108,360,24),"G U N N E R,  H E A T,  P C",menuSmall);
    GUI.Label(new Rect(673,34,250,58),"CO-OP",menuTitle);GUI.Label(new Rect(677,95,300,25),"UNOFFICIAL MULTIPLAYER",menuSmall);
    if(MenuAction(new Rect(975,24,38,35),"X"))panel=false;
    MenuFill(new Rect(18,144,1004,2),MenuAccent);
    if(menuHero!=null)GUI.DrawTexture(new Rect(18,148,1004,211),menuHero,ScaleMode.ScaleAndCrop);
    MenuFill(new Rect(18,148,465,211),new Color(.025f,.04f,.033f,.84f));
    GUI.Label(new Rect(36,162,390,30),"SAME PLATOON. TOGETHER.",menuHeading);
    GUI.Label(new Rect(38,206,402,65),"HOST  /  Enter a mission and create a room.\nGUEST  /  Join from the main menu.",menuText);
    GUI.Label(new Rect(38,286,390,57),"Up to 4 players through Steam or Direct IP.\nSeats use available friendly vehicles across platoons.",menuSmall);
    // Reserve a fixed status strip so a connection error never covers a control.
    MenuFill(new Rect(20,361,1000,65),new Color(.035f,.052f,.043f,.96f));
    var oldLabel=menuHeading.normal.textColor;menuHeading.normal.textColor=link!=null?MenuAccent:MenuInk;
    GUI.Label(new Rect(36,367,715,28),MenuConnectionTitle(),menuHeading);menuHeading.normal.textColor=oldLabel;
    string players=seats!=null?seats.Count+" / "+seats.Capacity:claimed&&occupiedVehicles.Count>0?occupiedVehicles.Count+" / "+(roomCapacity>0?roomCapacity:4):claimed?"2 / 2":"-- / --";
    GUI.Label(new Rect(840,367,170,28),"PLAYERS  "+players,menuHeading);
    GUI.Label(new Rect(38,397,962,27),new GUIContent(status,status),menuStatus);
    MenuSection(433,directMode?"DIRECT IP ROOM":"STEAM ROOM");
    bool idle=link==null;GUI.enabled=idle;
    if(MenuAction(new Rect(640,433,174,32),"STEAM",!directMode))directMode=false;
    if(MenuAction(new Rect(828,433,174,32),"DIRECT IP",directMode))directMode=true;
    if(directMode){
     GUI.Label(new Rect(37,476,180,30),"HOST ADDRESS",menuText);directAddress=GUI.TextField(new Rect(230,472,450,37),directAddress,128,menuInput);
     GUI.Label(new Rect(699,476,78,30),"PORT",menuText);directPort=GUI.TextField(new Rect(785,472,217,37),directPort,5,menuInput);
    }else{GUI.Label(new Rect(37,476,180,30),"STEAM ROOM ID",menuText);steamRoom=GUI.TextField(new Rect(230,472,772,37),steamRoom,20,menuInput);}
    if(MenuAction(new Rect(230,519,375,42),directMode?"CREATE DIRECT ROOM":"CREATE STEAM ROOM",true))Guard(delegate{Start(true,!directMode);});
    if(MenuAction(new Rect(624,519,378,42),directMode?"JOIN DIRECT ROOM":"JOIN STEAM ROOM"))Guard(delegate{Start(false,!directMode);});GUI.enabled=true;
    MenuSection(572,claimed?"PLATOON / VEHICLE CONTROL":directMode?"DIRECT CONNECTION":"STEAM CONNECTION");
    if(claimed){
     string current=game.Vehicles.ContainsKey(game.LocalId)?game.Vehicles[game.LocalId].Unit.FriendlyName:"Assigned vehicle";
     GUI.Label(new Rect(38,611,760,29),"YOUR VEHICLE   "+current,menuText);
     var available=game.FriendlyChoices().Where(id=>id!=game.LocalId&&id!=remote&&!occupiedVehicles.Contains(id)&&(seats==null||!seats.Occupied(id))&&game.Available(id)).ToArray();
     // Friendly unoccupied vehicles in other platoons are valid transfer targets.
     menuVehicleScroll=GUI.BeginScrollView(new Rect(36,649,966,88),menuVehicleScroll,new Rect(0,0,932,Mathf.Max(76,available.Length*42)));
     if(available.Length==0)GUI.Label(new Rect(5,5,900,50),"No available AI vehicles. Other players keep their assigned vehicles.",menuSmall);
     for(int i=0;i<available.Length;i++){var selected=available[i];GUI.enabled=pendingVehicle=="";if(MenuAction(new Rect(2,i*42,928,37),game.Vehicles[selected].Unit.FriendlyName+"  /  "+selected.Substring(0,6)))Guard(delegate{RequestVehicle(selected);});}
     GUI.EndScrollView();GUI.enabled=true;
    }else if(directMode){
     GUI.enabled=idle;GUI.Label(new Rect(37,615,178,28),"ROOM CODE",menuText);directCode=GUI.TextField(new Rect(230,610,772,36),directCode,64,menuInput);
     if(MenuAction(new Rect(37,656,440,34),directLocalOnly?"HOST: THIS PC ONLY":"HOST: LAN / DIRECT IP"))directLocalOnly=!directLocalOnly;
     GUI.enabled=true;GUI.Label(new Rect(37,703,945,38),"Same PC: 127.0.0.1. LAN: host IP + port + matching room code. Same gameplay as Steam.",menuSmall);
    }else{
     GUI.Label(new Rect(37,611,920,55),steamNotice,menuSmall);
     GUI.Label(new Rect(37,674,920,55),"Steam friends can join by invitation or room ID.\nCapacity: 2-4 players, depending on available friendly vehicles.",menuSmall);
    }
    var steam=link as SteamLink;
    if(steam!=null&&steam.RoomId!=""){
     if(MenuAction(new Rect(38,749,245,39),"COPY STEAM ROOM ID"))GUIUtility.systemCopyBuffer=steam.RoomId;
     GUI.enabled=hosting;if(MenuAction(new Rect(299,749,242,39),"INVITE FRIEND"))OpenFriendPicker(steam);GUI.enabled=true;
    }else GUI.Label(new Rect(38,751,485,42),"One player per vehicle. Friendly platoons share available seats.",menuSmall);
    GUI.enabled=link!=null;if(MenuAction(new Rect(562,749,440,39),"DISCONNECT")){Stop();status="Disconnected. Reload the mission before hosting again.";}GUI.enabled=true;
    MenuFill(new Rect(20,800,1000,1),new Color(.35f,.4f,.28f));
    GUI.Label(new Rect(36,812,235,29),"F8 / CLOSE MENU",menuSmall); if(MenuAction(new Rect(280,806,310,34),"F9 / NAMES: "+(platesEnabled?"ON":"OFF")))TogglePlates(); if(MenuAction(new Rect(605,806,397,34),"SHIFT+F9 / MY NAME: "+(selfPlateEnabled?"ON":"OFF")))ToggleSelfPlate();
    if(new Rect(36,394,968,32).Contains(Event.current.mousePosition)&&!String.IsNullOrEmpty(status)){
     // A bounded wrapped tooltip keeps long connection errors readable.
     float detailHeight=Mathf.Min(170,menuText.CalcHeight(new GUIContent(status),906)+24);
     MenuFill(new Rect(38,428,962,detailHeight),new Color(.02f,.032f,.028f,.99f));
     GUI.Label(new Rect(50,436,934,detailHeight-12),status,menuText);
    }
   }finally{GUI.matrix=matrix;GUI.enabled=enabled;GUI.color=color;GUI.backgroundColor=background;GUI.contentColor=content;}
  }
 }
}

