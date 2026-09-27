using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using GHPC.UI.Hud.Objectives;using HarmonyLib;
namespace GhpcCoop {
 public static class ObjectiveSync {
  static ObjectiveManager lastReplica,lastHost;
  static readonly Dictionary<int,string> LocalText=new Dictionary<int,string>();
  static string Plain(string s){return (s??"").Replace("<s>","").Replace("</s>","");}
  static ObjectiveManager Manager(){var current=ObjectiveManager.Instance;return current!=null&&current.IsInitialized?current:UnityEngine.Object.FindObjectsOfType<ObjectiveManager>().FirstOrDefault(x=>x.IsInitialized);}
  static readonly System.Reflection.FieldInfo Lists=AccessTools.Field(typeof(ObjectiveManager),"_listReferences"),State=AccessTools.Field(typeof(ObjectiveListItem),"_state"),Group=AccessTools.Field(typeof(ObjectiveManager),"_objectiveCanvasGroup"),Prefab=AccessTools.Field(typeof(ObjectiveManager),"_objectiveListItemPrefab");
  static IDictionary<int,ObjectiveListItem> Items(ObjectiveManager m){return Lists.GetValue(m) as IDictionary<int,ObjectiveListItem>;}
  public static ObjectiveStatus[] Capture(){
   var m=Manager();if(m==null||!m.IsInitialized)return new ObjectiveStatus[0];var list=Items(m);if(list==null)return new ObjectiveStatus[0];
   if(lastHost!=m){lastHost=m;GameBridge.Log("OBJECTIVES host count="+list.Count);}
   return list.OrderBy(x=>x.Key).Where(x=>x.Value!=null).Select(x=>new ObjectiveStatus{Id=x.Key,State=(int)(ObjectiveState)State.GetValue(x.Value),Visible=x.Value.gameObject.activeSelf,Text=""}).ToArray();
  }
  public static void Apply(ObjectiveStatus[] values){
   if(!GameBridge.ReplicaActive)return;var m=Manager();if(m==null||!m.IsInitialized)return;var list=Items(m);var group=Group.GetValue(m) as ObjectiveCanvasGroup;if(list==null||group==null)return;
   bool changed=lastReplica!=m;if(changed){LocalText.Clear();foreach(var pair in list)if(pair.Value!=null)LocalText[pair.Key]=Plain(pair.Value.Label.text);var data=m.ActiveObjectiveData;if(data!=null){int index=0;foreach(var row in data)if(!String.IsNullOrWhiteSpace(row.ObjectiveText)){if(!LocalText.ContainsKey(index))LocalText[index]=row.ObjectiveText;index++;}}}lastReplica=m;var ids=new HashSet<int>();
   foreach(var value in values){
    ids.Add(value.Id);ObjectiveListItem item;if(!list.TryGetValue(value.Id,out item)||item==null){var prefab=Prefab.GetValue(m) as GameObject;if(prefab==null)continue;item=UnityEngine.Object.Instantiate(prefab,group.transform).GetComponent<ObjectiveListItem>();list[value.Id]=item;changed=true;}
    string text;if(!LocalText.TryGetValue(value.Id,out text))text="Objective "+(value.Id+1);
    var state=(ObjectiveState)value.State;
    if(Plain(item.Label.text)!=Plain(text)||(ObjectiveState)State.GetValue(item)!=state||item.gameObject.activeSelf!=value.Visible){
     // Apply display state only. Never invoke guest mission events or score awards.
     State.SetValue(item,(ObjectiveState)(-1));item.Label.text=text;item.UpdateViewState(state);item.Label.text=text;item.gameObject.SetActive(value.Visible);changed=true;
    }
   }
   foreach(var pair in list)if(!ids.Contains(pair.Key)&&pair.Value!=null&&pair.Value.gameObject.activeSelf){pair.Value.gameObject.SetActive(false);changed=true;}
   if(changed){group.TriggerAnimation();GameBridge.Log("OBJECTIVES replica count="+values.Length+" states="+String.Join(",",values.Select(x=>x.Id+":"+x.State+":"+x.Visible).ToArray()));
    try{System.IO.Directory.CreateDirectory("UserData/GhpcCoop");System.IO.File.WriteAllLines("UserData/GhpcCoop/objectives-diagnostic.txt",values.Select(x=>x.Id+"\t"+x.State+"\t"+x.Visible+"\t"+(list.ContainsKey(x.Id)&&list[x.Id]!=null?list[x.Id].Label.text:"")).ToArray());}catch{}
   }
  }
 }
}
