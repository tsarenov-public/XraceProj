using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RaceCoreSmokeCheck
{
    private const string Key="Xrace.RaceSmoke";
    private static RaceSessionManager session;
    private static RaceTimer timer;
    private static DriftScorer scorer;
    private static DriftZone[] zones;
    private static FakeState fake;
    private static float nextTime;
    private static int savedScore;
    private static float savedTime;
    private static int frames;
    private class FakeState : IVehicleDriftState
    {
        public float SpeedKmh=>80;
        public float SlipAngle=>45;
        public bool IsGrounded=>true;
        public bool IsDrifting { get; set; }=true;
    }
    static RaceCoreSmokeCheck()=>EditorApplication.update+=Tick;
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        SessionState.SetInt(Key,1);
        SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddMinutes(3).ToString("O"));
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        int step=SessionState.GetInt(Key,0);if(step==0)return;
        try
        {
            Require(DateTime.UtcNow<DateTime.Parse(SessionState.GetString(Key+"Deadline","")),"Race check timed out.");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames<15)return;
            frames=0;
            if(step==1)
            {
                var roots=SceneManager.GetActiveScene().GetRootGameObjects();
                session=roots.Select(r=>r.GetComponent<RaceSessionManager>()).First(v=>v!=null);
                timer=session.GetComponent<RaceTimer>();scorer=session.GetComponent<DriftScorer>();
                zones=session.GetComponentsInChildren<DriftZone>().OrderBy(z=>z.ZoneId).ToArray();
                for(int i=0;i<zones.Length;i++)
                {
                    Require(zones[i].ZoneId==i+1,"Zone IDs must be contiguous.");
                    for(int j=i+1;j<zones.Length;j++)
                        Require(!Physics.ComputePenetration(zones[i].GetComponent<BoxCollider>(),zones[i].transform.position,zones[i].transform.rotation,zones[j].GetComponent<BoxCollider>(),zones[j].transform.position,zones[j].transform.rotation,out _,out _),"Zone colliders overlap.");
                }
                Require(session.CarBody!=null,"Fallback car not bound.");
                Require(session.GetComponent<CarSpawner>().Spawn()==session.CarBody,"Duplicate spawn.");
                Require(session.State==RaceSessionManager.RaceState.Ready && timer.TimeSeconds==0,"Timer started while standing/falling.");
                session.CarBody.GetComponent<WheelController>().enabled=false;
                session.CarBody.useGravity=false;
                session.CarBody.position+=Vector3.up*20;
                session.CarBody.linearVelocity=session.CarBody.transform.forward*10;
                nextTime=Time.time+0.2f;
            }
            else if(Time.time<nextTime)return;
            else if(step==2)
            {
                Require(session.State==RaceSessionManager.RaceState.Racing && timer.TimeSeconds>0,"Movement did not start timer.");
                fake=new FakeState();
                typeof(DriftScorer).GetField("vehicleState",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(scorer,fake);
                scorer.EnterZone(zones[0]);Require(scorer.currentMultiplier==1,"Zone 1 multiplier.");
                nextTime=Time.time+0.5f;
            }
            else if(step==3)
            {
                scorer.ExitZone(zones[0]);Require(scorer.Score>=40 && scorer.successfulChain==1,"Zone 1 scoring/chain.");
                scorer.EnterZone(zones[1]);Require(scorer.currentMultiplier==2,"Zone 2 multiplier.");
                nextTime=Time.time+0.5f;
            }
            else if(step==4)
            {
                scorer.ExitZone(zones[1]);Require(scorer.successfulChain==2,"Zone 2 chain.");
                savedScore=scorer.Score;fake.IsDrifting=false;scorer.EnterZone(zones[2]);nextTime=Time.time+0.4f;
            }
            else if(step==5)
            {
                scorer.ExitZone(zones[2]);Require(scorer.successfulChain==0 && scorer.currentMultiplier==1 && scorer.Score==savedScore,"Failed zone did not reset chain/preserve points.");
                savedTime=timer.TimeSeconds;session.SetPaused(true);nextTime=Time.time+0.25f;
            }
            else if(step==6)
            {
                Require(timer.TimeSeconds==savedTime,"Timer ran during pause.");
                session.SetPaused(false);
                var trigger=session.GetComponentInChildren<RaceFinishTrigger>();
                var collider=session.CarBody.GetComponentsInChildren<Collider>().First(c=>c.attachedRigidbody==session.CarBody);
                Debug.Log("RACE_FINISH_CHECK state="+session.State+" paused="+session.IsPaused+" timescale="+Time.timeScale+" collider="+collider.name);
                trigger.SendMessage("OnTriggerEnter",collider);
                Require(session.State==RaceSessionManager.RaceState.Finished && !session.TryFinish(),"Finish did not latch exactly once.");
                savedTime=timer.TimeSeconds;nextTime=Time.time+1;
            }
            else
            {
                Require(session.CarBody.isKinematic && timer.TimeSeconds==savedTime,"Finish did not freeze car/timer.");
                Debug.Log("XRACE_RACE_SMOKE_OK: fallback/no duplicate, timer on move, x1/x2, reset keeps score, pause, one finish, stopped car.");
                SessionState.SetInt(Key,0);EditorApplication.Exit(0);return;
            }
            SessionState.SetInt(Key,step+1);
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
