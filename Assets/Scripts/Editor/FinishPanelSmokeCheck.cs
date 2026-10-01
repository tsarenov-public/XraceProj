using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class FinishPanelSmokeCheck
{
    private const string Key="Xrace.FinishSmoke";
    private static int frames;
    private static Scene previousScene;
    private static RaceSessionManager session;
    private static FinishPanelController controller;
    static FinishPanelSmokeCheck()=>EditorApplication.update+=Tick;
    public static void Run()
    {
        string path=ResultsRepository.FilePath;
        if(File.Exists(path+".tmp"))throw new IOException("Pending result temporary file exists; leave it intact before running this test.");
        string backup=path+".finish-test-backup-"+Guid.NewGuid().ToString("N");
        SessionState.SetString(Key+"Path",path);SessionState.SetString(Key+"Backup",backup);
        SessionState.SetBool(Key+"HadFile",File.Exists(path));
        if(File.Exists(path))File.Move(path,backup);
        SessionState.SetInt(Key,1);SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddMinutes(4).ToString("O"));
        EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        int step=SessionState.GetInt(Key,0);if(step==0)return;
        try
        {
            Require(DateTime.UtcNow<DateTime.Parse(SessionState.GetString(Key+"Deadline","")),"Finish check timed out.");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames<15)return;
            frames=0;
            if(step==1 || step==4 || step==7)
            {
                var scene=SceneManager.GetActiveScene();
                if(step!=1 && scene==previousScene)return;
                var roots=scene.GetRootGameObjects();session=roots.Select(r=>r.GetComponent<RaceSessionManager>()).First(s=>s!=null);
                controller=roots.Select(r=>r.GetComponent<FinishPanelController>()).First(c=>c!=null);
                Require(!Panel().activeSelf,"Finish panel visible before finish.");
                Require(session.State==RaceSessionManager.RaceState.Ready,"Race did not reset.");
                var body=session.CarBody;Require(body!=null,"No spawned car.");body.GetComponent<WheelController>().enabled=false;body.useGravity=false;body.position+=Vector3.up*20;body.linearVelocity=body.transform.forward*10;
            }
            else if(step==2 || step==5 || step==8)
            {
                if(session.State!=RaceSessionManager.RaceState.Racing)return;
                int score=step==8?101:100;
                typeof(DriftScorer).GetField("points",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session.GetComponent<DriftScorer>(),(double)score);
                Require(session.TryFinish(),"Finish failed.");Require(controller.SaveSucceeded,"Save failed.");
                Require(controller.SavedResult.isNewRecord==(step!=5),"Strict record comparison failed.");
                var data=ResultsRepository.Load();Require(data.results.Count==(step==2?1:step==5?2:3),"Unexpected saved result count.");
                Require(data.results[0].driftScore==score,"Results sort failed.");
                Require(controller.LastResult.carId=="ae86" && !string.IsNullOrEmpty(controller.LastResult.createdAtUtc),"Car/date metadata missing.");
                var texts=controller.GetComponentsInChildren<TMP_Text>(true);
                Require(texts.First(t=>t.name=="FinishTime_Text").text==session.GetComponent<RaceTimer>().FormattedTime,"Finish time incorrect.");
                Require(texts.First(t=>t.name=="FinishScore_Text").text=="Дрифт: "+score,"Finish score incorrect.");
                Require(texts.First(t=>t.name=="Record_Text").gameObject.activeSelf==(step!=5),"Record label incorrect.");
                Require(Panel().activeSelf,"Finish panel hidden.");
                int count=data.results.Count;controller.enabled=false;controller.enabled=true;
                Require(!session.TryFinish() && ResultsRepository.Load().results.Count==count,"Duplicate result saved.");
            }
            else if(step==3 || step==6 || step==9)
            {
                previousScene=SceneManager.GetActiveScene();
                Time.timeScale=0;AudioListener.pause=true;
                string button=step==9?"Btn_Garage":"Btn_Retry";
                controller.GetComponentsInChildren<Button>(true).First(b=>b.name==button).onClick.Invoke();
                Require(Time.timeScale==1 && !AudioListener.pause,"Scene button did not restore time/audio.");
            }
            else
            {
                if(SceneManager.GetActiveScene().name!="Garage")return;
                Debug.Log("XRACE_FINISH_SMOKE_OK: save once, displayed time/score, record/equal/new record, retry twice, garage, time reset.");
                Cleanup();SessionState.SetInt(Key,0);EditorApplication.Exit(0);return;
            }
            SessionState.SetInt(Key,step+1);
        }
        catch(Exception error)
        {
            Debug.LogException(error);
            try{Cleanup();}catch(Exception cleanupError){Debug.LogException(cleanupError);}
            SessionState.SetInt(Key,0);EditorApplication.Exit(1);
        }
    }
    private static GameObject Panel()=>controller.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Finish_Panel").gameObject;
    private static void Cleanup()
    {
        string path=SessionState.GetString(Key+"Path","");string backup=SessionState.GetString(Key+"Backup","");
        if(SessionState.GetBool(Key+"HadFile",false) && !File.Exists(backup))throw new IOException("Original history backup missing; leave current file untouched.");
        if(File.Exists(path))File.Delete(path);
        if(File.Exists(backup))File.Move(backup,path);
        if(File.Exists(path+".tmp"))File.Delete(path+".tmp");
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
