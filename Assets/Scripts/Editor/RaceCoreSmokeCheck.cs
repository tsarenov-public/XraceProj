using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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
    private static TMP_Text hudTime;
    private static TMP_Text hudScore;
    private static TMP_Text hudMultiplier;
    private static GameObject hudPanel;
    private static GameObject collisionWall;
    private static BoxCollider collisionProbe;
    private static Action collisionObserver;
    private static int collisionResetCount;
    private static int scoreAtCollision;
    private class FakeState : IVehicleDriftState
    {
        public float SpeedKmh { get; set; }=80;
        public float SlipAngle { get; set; }=45;
        public bool IsGrounded { get; set; }=true;
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
                // This check fabricates drift scores. Keep the real player's history intact;
                // persistence is exercised separately by FinishPanelSmokeCheck.
                foreach (var finishUi in roots.SelectMany(r => r.GetComponentsInChildren<FinishPanelController>(true)))
                    finishUi.enabled = false;
                timer=session.GetComponent<RaceTimer>();scorer=session.GetComponent<DriftScorer>();
                var texts=roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
                hudTime=texts.First(t=>t.name=="Time_Text");hudScore=texts.First(t=>t.name=="Score_Text");hudMultiplier=texts.First(t=>t.name=="Multiplier_Text");hudPanel=hudTime.transform.parent.gameObject;
                Require(hudPanel.activeSelf && hudTime.text=="00:00.000" && hudScore.text=="Дрифт: 0" && hudMultiplier.text=="x1","Initial HUD incorrect.");
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
                Require(hudTime.text==timer.FormattedTime,"HUD time format/synchronization.");
                CheckAdapterMotion();
                fake=new FakeState();
                typeof(DriftScorer).GetField("vehicleState",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(scorer,fake);
                scorer.EnterZone(zones[0]);Require(scorer.currentMultiplier==1,"Zone 1 multiplier.");
                nextTime=Time.time+0.5f;
            }
            else if(step==3)
            {
                scorer.ExitZone(zones[0]);Require(scorer.Score>=40 && scorer.successfulChain==1,"Zone 1 scoring/chain.");
                savedScore=scorer.Score;fake.IsDrifting=false;
                scorer.EnterZone(zones[1]);Require(scorer.currentMultiplier==1,"Entering zone 2 without drift raised the multiplier.");
                Require(hudMultiplier.text=="x1","HUD awarded a multiplier for corner entry.");
                nextTime=Time.time+0.4f;
            }
            else if(step==4)
            {
                Require(scorer.Score==savedScore && scorer.currentMultiplier==1 && scorer.successfulChain==1,"Rolling through zone 2 without drift awarded points/multiplier.");
                fake.IsDrifting=true;nextTime=Time.time+0.5f;
            }
            else if(step==5)
            {
                Require(scorer.currentMultiplier==2 && scorer.Score>savedScore,"A real second drift did not use x2.");
                Require(hudMultiplier.text=="x2" && hudScore.text=="Дрифт: "+scorer.Score,"HUD did not react to drift events.");
                var controller=hudPanel.GetComponentInParent<RaceHUDController>();controller.enabled=false;controller.enabled=true;
                Require(hudMultiplier.text=="x2" && hudScore.text=="Дрифт: "+scorer.Score,"HUD resubscription did not restore current values.");
                scorer.ExitZone(zones[1]);Require(scorer.successfulChain==2,"Zone 2 chain.");
                Require(scorer.currentMultiplier==1,"Leaving a drift zone kept an active multiplier.");
                savedScore=scorer.Score;fake.IsDrifting=false;scorer.EnterZone(zones[2]);
                Require(scorer.currentMultiplier==1,"Entering zone 3 without drift raised the multiplier.");
                nextTime=Time.time+0.4f;
            }
            else if(step==6)
            {
                scorer.ExitZone(zones[2]);Require(scorer.successfulChain==0 && scorer.currentMultiplier==1 && scorer.Score==savedScore,"Failed zone did not reset chain/preserve points.");
                Require(hudMultiplier.text=="x1" && hudScore.text=="Дрифт: "+savedScore,"HUD chain reset display incorrect.");
                CheckScoringGuards();
                CheckStopAndCollisionGuards();
                PrepareWallCollision();nextTime=Time.time+2.5f;
            }
            else if(step==7)
            {
                Require(collisionResetCount==1,"A physical wall impact did not reset the chain exactly once.");
                Require(scorer.successfulChain==0 && scorer.currentMultiplier==1 && scorer.Score==scoreAtCollision,"Wall impact did not reset multiplier/preserve points.");
                CleanupWallCollision();
                // A tiny new drift must not qualify using the pre-impact time.
                fake.IsDrifting=true;scorer.SendMessage("FixedUpdate");scorer.ExitZone(zones[13]);
                Require(scorer.successfulChain==0,"A wall impact retained pre-impact drift time.");
                fake.IsDrifting=false;
                savedTime=timer.TimeSeconds;session.SetPaused(true);nextTime=Time.time+0.25f;
            }
            else if(step==8)
            {
                Require(timer.TimeSeconds==savedTime,"Timer ran during pause.");
                session.SetPaused(false);
                var trigger=session.GetComponentInChildren<RaceFinishTrigger>();
                var collider=session.CarBody.GetComponentsInChildren<Collider>().First(c=>c.attachedRigidbody==session.CarBody);
                Debug.Log("RACE_FINISH_CHECK state="+session.State+" paused="+session.IsPaused+" timescale="+Time.timeScale+" collider="+collider.name);
                trigger.SendMessage("OnTriggerEnter",collider);
                Require(session.State==RaceSessionManager.RaceState.Finished && !session.TryFinish(),"Finish did not latch exactly once.");
                Require(!hudPanel.activeSelf,"HUD overlaps finish window.");
                savedTime=timer.TimeSeconds;nextTime=Time.time+1;
            }
            else
            {
                Require(session.CarBody.isKinematic && timer.TimeSeconds==savedTime,"Finish did not freeze car/timer.");
                Debug.Log("XRACE_RACE_SMOKE_OK: fallback/no duplicate, timer on move, x1/x2 only during drift, no drift/no ground/low speed/invalid state cannot advance chain, short drift preserves points without success, cancelled zone never awards success, grip turns/reverse/airborne excluded, stop and physical wall impact reset chain and pre-impact drift time without losing points, road/light impacts/pause do not reset, one finish, stopped car; HUD ready/time/score/multiplier/resubscribe/hide verified.");
                SessionState.SetInt(Key,0);EditorApplication.Exit(0);return;
            }
            SessionState.SetInt(Key,step+1);
        }
        catch(Exception e){CleanupWallCollision();Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    private static void CheckAdapterMotion()
    {
        // A turn changes heading; it is not a drift when velocity follows that
        // heading. Exercise the actual Rigidbody fallback, not a fake flag.
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var vehicle=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            ground.name="RaceSmoke_DriftStateGround";
            ground.transform.position=new Vector3(0,1000,0);
            ground.transform.localScale=new Vector3(8,0.1f,8);
            vehicle.name="RaceSmoke_DriftStateBody";
            vehicle.transform.position=new Vector3(0,1001,0);
            var body=vehicle.AddComponent<Rigidbody>();body.useGravity=false;
            // A real collider supplies a valid mass/COM before any physics
            // step. Teleport Transform and body together for immediate queries.
            body.centerOfMass=Vector3.zero;
            var adapter=vehicle.AddComponent<VehicleDriftStateAdapter>();adapter.Bind(body);
            Physics.SyncTransforms();
            foreach(float heading in new[]{0f,45f,90f,180f})
            {
                vehicle.transform.rotation=Quaternion.Euler(0,heading,0);
                body.rotation=vehicle.transform.rotation;
                body.linearVelocity=body.rotation*Vector3.forward*(80f/3.6f);
                Physics.SyncTransforms();
                Require(Vector3.Distance(body.worldCenterOfMass,vehicle.transform.position)<0.01f,"Drift fixture COM was not synchronized. "+AdapterDiagnostic(adapter,body,heading));
                Require(adapter.IsGrounded && !adapter.IsDrifting,"A grip turn was detected as drift. "+AdapterDiagnostic(adapter,body,heading));
            }
            vehicle.transform.rotation=Quaternion.identity;body.rotation=Quaternion.identity;
            body.linearVelocity=Quaternion.Euler(0,30,0)*Vector3.forward*(80f/3.6f);
            Physics.SyncTransforms();
            Require(adapter.IsDrifting,"Grounded sideways motion was not detected as drift. "+AdapterDiagnostic(adapter,body,0));
            body.linearVelocity=Quaternion.Euler(0,150,0)*Vector3.forward*(80f/3.6f);
            Require(!adapter.IsDrifting,"Reverse slide was detected as drift. "+AdapterDiagnostic(adapter,body,0));
            vehicle.transform.position=new Vector3(0,1005,0);body.position=vehicle.transform.position;
            body.linearVelocity=Quaternion.Euler(0,30,0)*Vector3.forward*(80f/3.6f);
            Physics.SyncTransforms();
            Require(!adapter.IsGrounded && !adapter.IsDrifting,"Airborne movement was detected as drift. "+AdapterDiagnostic(adapter,body,0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vehicle);
            UnityEngine.Object.DestroyImmediate(ground);
        }
    }
    private static string AdapterDiagnostic(VehicleDriftStateAdapter adapter,Rigidbody body,float heading)
    {
        return "heading="+heading+" speed="+adapter.SpeedKmh+" slip="+adapter.SlipAngle
            +" grounded="+adapter.IsGrounded+" drifting="+adapter.IsDrifting+" COM="+body.worldCenterOfMass
            +" bodyPosition="+body.position+" transformPosition="+body.transform.position
            +" forward="+body.transform.forward+" velocity="+body.linearVelocity;
    }
    private static void CheckScoringGuards()
    {
        fake.IsDrifting=true;fake.IsGrounded=false;
        CheckRejectedDrift(zones[3],"Airborne state");
        fake.IsGrounded=true;fake.SpeedKmh=10;
        CheckRejectedDrift(zones[4],"Below-zone speed");
        fake.SpeedKmh=float.NaN;
        CheckRejectedDrift(zones[5],"Invalid speed");
        fake.SpeedKmh=80;fake.SlipAngle=0;
        CheckRejectedDrift(zones[6],"Zero slip");
        fake.SlipAngle=45;
        int before=scorer.Score;
        scorer.EnterZone(zones[7]);scorer.SendMessage("FixedUpdate");scorer.ExitZone(zones[7]);
        Require(scorer.Score>before && scorer.successfulChain==0 && scorer.currentMultiplier==1,"Short drift should retain points without advancing chain.");
        scorer.EnterZone(zones[8]);SimulateDrift(zones[8]);scorer.ExitZone(zones[8]);
        Require(scorer.successfulChain==1,"Valid drift after failed corner did not restart the chain.");
        scorer.EnterZone(zones[9]);SimulateDrift(zones[9]);
        Require(scorer.currentMultiplier==2,"Second valid drift before cancellation did not use x2.");
        int saved=scorer.Score;
        int zoneSuccesses=0;
        Action<int> observer=_=>zoneSuccesses++;
        scorer.OnZoneSuccess+=observer;
        try
        {
            zones[9].gameObject.SetActive(false);
            scorer.ExitZone(zones[9]);
            Require(zoneSuccesses==0 && scorer.successfulChain==0 && scorer.currentMultiplier==1 && scorer.Score==saved,"Disabling a zone awarded success or lost accumulated points.");
            Require(hudMultiplier.text=="x1","Cancelling a manually disabled zone did not synchronize the HUD.");
        }
        finally
        {
            scorer.OnZoneSuccess-=observer;
            zones[9].gameObject.SetActive(true);
        }
        fake.IsDrifting=false;
    }
    private static void SimulateDrift(DriftZone zone)
    {
        int steps=Mathf.CeilToInt(zone.MinDriftTime/Time.fixedDeltaTime)+2;
        for(int i=0;i<steps;i++)scorer.SendMessage("FixedUpdate");
    }
    private static void CheckStopAndCollisionGuards()
    {
        fake.IsDrifting=true;
        scorer.EnterZone(zones[10]);SimulateDrift(zones[10]);scorer.ExitZone(zones[10]);
        scorer.EnterZone(zones[11]);SimulateDrift(zones[11]);
        Require(scorer.successfulChain==1 && scorer.currentMultiplier==2,"Stop test did not establish x2.");
        int before=scorer.Score;
        fake.SpeedKmh=0;fake.IsDrifting=false;
        int steps=Mathf.CeilToInt(new SerializedObject(scorer).FindProperty("stopResetDelay").floatValue/Time.fixedDeltaTime)+2;
        session.SetPaused(true);
        for(int i=0;i<steps;i++)scorer.SendMessage("FixedUpdate");
        Require(!scorer.NotifyCollision(20,Vector3.right) && scorer.successfulChain==1 && scorer.Score==before,"Pause reset a stopped/impacted chain.");
        session.SetPaused(false);
        int stopResets=0;Action observer=()=>stopResets++;
        scorer.OnChainReset+=observer;
        try
        {
            for(int i=0;i<steps*2;i++)scorer.SendMessage("FixedUpdate");
            Require(stopResets==1 && scorer.successfulChain==0 && scorer.currentMultiplier==1 && scorer.Score==before,"Stop did not reset once without losing points.");
        }
        finally { scorer.OnChainReset-=observer; }
        fake.SpeedKmh=80;fake.IsDrifting=true;scorer.SendMessage("FixedUpdate");scorer.ExitZone(zones[11]);
        Require(scorer.successfulChain==0,"Stopping retained pre-stop drift time.");
        scorer.EnterZone(zones[12]);SimulateDrift(zones[12]);scorer.ExitZone(zones[12]);
        scorer.EnterZone(zones[13]);SimulateDrift(zones[13]);
        before=scorer.Score;
        Require(!scorer.NotifyCollision(20,Vector3.up),"A vertical road/landing contact broke the chain.");
        Require(!scorer.NotifyCollision(0.1f,Vector3.right),"A light touch broke the chain.");
        Require(scorer.successfulChain==1 && scorer.currentMultiplier==2 && scorer.Score==before,"Filtered contacts modified drift state.");
    }
    private static void PrepareWallCollision()
    {
        var body=session.CarBody;
        Require(body.GetComponent<RaceCarCollisionRelay>()!=null,"Spawner did not install collision relay.");
        body.position=new Vector3(0,1500,0);body.rotation=Quaternion.identity;
        body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        collisionProbe=body.gameObject.AddComponent<BoxCollider>();collisionProbe.size=new Vector3(2,1,2);
        collisionWall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        collisionWall.name="RaceSmoke_ImpactWall";collisionWall.transform.position=new Vector3(0,1500,20);
        collisionWall.transform.localScale=new Vector3(20,20,0.5f);
        collisionResetCount=0;
        collisionObserver=()=>
        {
            collisionResetCount++;
            scoreAtCollision=scorer.Score;
            fake.IsDrifting=false;
        };
        scorer.OnChainReset+=collisionObserver;
        Physics.SyncTransforms();body.linearVelocity=Vector3.forward*12;
    }
    private static void CleanupWallCollision()
    {
        if(scorer!=null && collisionObserver!=null)scorer.OnChainReset-=collisionObserver;
        collisionObserver=null;
        if(collisionProbe!=null)UnityEngine.Object.DestroyImmediate(collisionProbe);
        if(collisionWall!=null)UnityEngine.Object.DestroyImmediate(collisionWall);
        collisionProbe=null;collisionWall=null;
    }
    private static void CheckRejectedDrift(DriftZone zone,string label)
    {
        int before=scorer.Score;
        scorer.EnterZone(zone);
        int steps=Mathf.CeilToInt(zone.MinDriftTime/Time.fixedDeltaTime)+2;
        for(int i=0;i<steps;i++)scorer.SendMessage("FixedUpdate");
        scorer.ExitZone(zone);
        Require(scorer.Score==before && scorer.successfulChain==0 && scorer.currentMultiplier==1,label+" awarded drift points/chain.");
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
