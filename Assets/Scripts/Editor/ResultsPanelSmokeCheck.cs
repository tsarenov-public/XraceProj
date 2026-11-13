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

// Real scene/button regression; protects and restores the player's original history.
[InitializeOnLoad]
public static class ResultsPanelSmokeCheck
{
    private const string Key = "Xrace.ResultsSmoke";
    private const int SeedCount = 9;
    private static int frames;
    private static Scene previousScene;
    private static RaceSessionManager session;
    private static FinishPanelController finish;
    private static ResultsPanelController results;

    static ResultsPanelSmokeCheck() => EditorApplication.update += Tick;
    public static void Run()
    {
        Require(Application.isBatchMode, "Results smoke check is batch-only.");
        string path = ResultsRepository.FilePath;
        Require(!File.Exists(path + ".tmp"), "Pending result temporary file exists; preserve it before this test.");
        Directory.CreateDirectory(Application.persistentDataPath);
        Require(Directory.GetFiles(Application.persistentDataPath, "race_results.json.*test-backup-*").Length == 0,
            "Previous history test backup exists; recover it first.");
        string backup = path + ".results-test-backup-" + Guid.NewGuid().ToString("N");
        SessionState.SetString(Key + "Path", path); SessionState.SetString(Key + "Backup", backup);
        SessionState.SetBool(Key + "HadFile", File.Exists(path));
        if (File.Exists(path)) File.Move(path, backup);
        try
        {
            ResultsRepository.SaveNewEntry(Entry("a", 100, 30));
            ResultsRepository.SaveNewEntry(Entry("b", 100, 20));
            ResultsRepository.SaveNewEntry(Entry("c", 200, 50));
            // Overflow the viewport: normalized scrolling is only meaningful
            // when the history is taller than the visible area.
            for (int i = 1; i <= 6; i++) ResultsRepository.SaveNewEntry(Entry("low-" + i, i * 10, 60 + i));
            SessionState.SetInt(Key, 1);
            SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddMinutes(5).ToString("O"));
            EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity"); EditorApplication.EnterPlaymode();
        }
        catch { Cleanup(); SessionState.SetInt(Key, 0); throw; }
    }

    private static void Tick()
    {
        int step = SessionState.GetInt(Key, 0); if (step == 0) return;
        try
        {
            Require(DateTime.UtcNow < DateTime.Parse(SessionState.GetString(Key + "Deadline", "")), "Results check timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 15) return;
            frames = 0;
            if (step == 1 || step == 4 || step == 7)
            {
                var scene = SceneManager.GetActiveScene();
                if (step != 1 && scene == previousScene) return;
                var roots = scene.GetRootGameObjects();
                session = roots.SelectMany(r => r.GetComponentsInChildren<RaceSessionManager>(true)).Single();
                finish = roots.SelectMany(r => r.GetComponentsInChildren<FinishPanelController>(true)).Single();
                results = roots.SelectMany(r => r.GetComponentsInChildren<ResultsPanelController>(true)).Single();
                Require(session.State == RaceSessionManager.RaceState.Ready, "Race did not reset.");
                Require(!Panel("Results_Panel").activeSelf, "Results visible before finish.");
                Require(Button("Btn_Results").onClick.GetPersistentEventCount() == 1 &&
                    Button("Btn_Results").onClick.GetPersistentTarget(0) == finish &&
                    Button("Btn_Results").onClick.GetPersistentMethodName(0) == "OpenResults", "Results button binding invalid.");
                Require(Button("Btn_ResultsBack").onClick.GetPersistentEventCount() == 1 &&
                    Button("Btn_ResultsBack").onClick.GetPersistentTarget(0) == results &&
                    Button("Btn_ResultsBack").onClick.GetPersistentMethodName(0) == "Back", "Back button binding invalid.");
                var body = session.CarBody; Require(body != null, "No spawned car.");
                body.GetComponent<WheelController>().enabled = false;
                body.useGravity = false; body.position += Vector3.up * 20; body.linearVelocity = body.transform.forward * 10;
            }
            else if (step == 2 || step == 5 || step == 8)
            {
                if (session.State != RaceSessionManager.RaceState.Racing) return;
                int cycle = step == 2 ? 0 : step == 5 ? 1 : 2;
                int score = cycle == 0 ? 100 : 201;
                float seconds = cycle == 0 ? 25 : cycle == 1 ? 40 : 30;
                typeof(DriftScorer).GetField("points", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(session.GetComponent<DriftScorer>(), (double)score);
                typeof(RaceTimer).GetField("<TimeSeconds>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(session.GetComponent<RaceTimer>(), seconds);
                Require(session.TryFinish() && finish.SaveSucceeded, "Finish/save failed.");
                Require(finish.SavedResult.isNewRecord == (cycle == 1), "Strict score record comparison failed.");
                Require(Text("Record_Text").gameObject.activeSelf == (cycle == 1), "Finish record label incorrect.");
                string history = File.ReadAllText(ResultsRepository.FilePath);
                OpenAndCheck(cycle);
                Button("Btn_ResultsBack").onClick.Invoke();
                Require(!Panel("Results_Panel").activeSelf && Panel("Finish_Panel").activeSelf, "Back did not restore finish.");
                Button("Btn_Results").onClick.Invoke();
                CheckRows(cycle);
                Require(File.ReadAllText(ResultsRepository.FilePath) == history, "Opening/back/results wrote or duplicated history.");
                Require(!session.TryFinish() && ResultsRepository.Load().results.Count == SeedCount + 1 + cycle, "Result saved twice.");
                if (cycle == 2)
                {
                    // A future schema must show a readable UI error, while the current run stays visible.
                    try
                    {
                        File.WriteAllText(ResultsRepository.FilePath, "{\"schemaVersion\":2,\"results\":[]}");
                        Button("Btn_ResultsBack").onClick.Invoke(); Button("Btn_Results").onClick.Invoke();
                        Require(Text("ResultsStatus_Text").text.Contains("Не удалось загрузить"), "History error not reported in UI.");
                        Require(Panel("CurrentResult_Panel").activeSelf && Text("CurrentResult_Text").text.Contains("Дрифт: 201"),
                            "Current result disappeared on storage error.");
                    }
                    finally { File.WriteAllText(ResultsRepository.FilePath, history); }
                }
                Button("Btn_ResultsBack").onClick.Invoke();
            }
            else if (step == 3 || step == 6 || step == 9)
            {
                previousScene = SceneManager.GetActiveScene();
                Time.timeScale = 0; AudioListener.pause = true;
                Button(step == 9 ? "Btn_Garage" : "Btn_Retry").onClick.Invoke();
                Require(Time.timeScale == 1 && !AudioListener.pause, "Scene button did not restore time/audio.");
            }
            else
            {
                if (SceneManager.GetActiveScene().name != "Garage") return;
                Cleanup(); SessionState.SetInt(Key, 0);
                Debug.Log("XRACE_RESULTS_SMOKE_OK: current result pinned once with actual rank, sorted score/time history, strict records and ties, repeat opening without writes, readable IO/schema error, back/retry/garage; original history restored.");
                EditorApplication.Exit(0); return;
            }
            SessionState.SetInt(Key, step + 1);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            try { Cleanup(); } catch (Exception cleanupError) { Debug.LogException(cleanupError); }
            SessionState.SetInt(Key, 0); EditorApplication.Exit(1);
        }
    }

    private static void OpenAndCheck(int cycle)
    {
        Button("Btn_Results").onClick.Invoke();
        Require(Panel("Results_Panel").activeSelf && !Panel("Finish_Panel").activeSelf, "Results did not replace finish panel.");
        Require(Panel("CurrentResult_Panel").activeSelf, "Current run not visible.");
        Require(!Panel("CurrentResult_Panel").transform.IsChildOf(Panel("History_Scroll").transform), "Current run must stay outside scroll.");
        Require(Text("CurrentRecord_Text").gameObject.activeSelf == (cycle == 1), "Pinned record label incorrect.");
        Require(Text("CurrentResult_Text").text.Contains("Место: " + (cycle == 0 ? 3 : 1)), "Current actual rank incorrect.");
        var scroll = Panel("History_Scroll").GetComponent<ScrollRect>();
        Require(scroll.content.rect.height > scroll.viewport.rect.height,
            "History fixture must overflow viewport: content=" + scroll.content.rect.height + ", viewport=" + scroll.viewport.rect.height);
        Require(scroll.verticalNormalizedPosition >= 0.999f,
            "History did not open at top: normalized=" + scroll.verticalNormalizedPosition + ", content=" + scroll.content.rect.height +
            ", viewport=" + scroll.viewport.rect.height + ", anchoredPosition=" + scroll.content.anchoredPosition);
        CheckRows(cycle);
    }

    private static void CheckRows(int cycle)
    {
        var ranked = ResultsRepository.Load().results;
        var content = Panel("History_Scroll").GetComponent<ScrollRect>().content;
        var rows = content.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToArray();
        var expected = ranked.Where(entry => entry.id != finish.LastResult.id).ToArray();
        Require(rows.Length == expected.Length && ranked.Count == cycle + SeedCount + 1, "History/current duplication or count mismatch.");
        for (int i = 0; i < expected.Length; i++)
        {
            Require(rows[i].name == "ResultRow_" + expected[i].id, "Displayed history sort differs from stored score/time order.");
            int rank = ranked.FindIndex(entry => entry.id == expected[i].id) + 1;
            Require(rows[i].GetComponentInChildren<TMP_Text>().text.StartsWith(rank + ". "), "History rank must include the pinned run.");
        }
    }

    private static GameObject Panel(string name) => finish.GetComponentsInChildren<Transform>(true).Single(t => t.name == name).gameObject;
    private static TMP_Text Text(string name) => finish.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == name);
    private static Button Button(string name) => finish.GetComponentsInChildren<Button>(true).Single(t => t.name == name);
    private static RaceResultEntry Entry(string id, int score, float seconds) => new RaceResultEntry
    {
        id = id, carId = "results-test", carName = "TEST-" + id, driftScore = score, timeSeconds = seconds,
        maxMultiplier = 2, createdAtUtc = DateTime.UtcNow.ToString("O")
    };
    private static void Cleanup()
    {
        string path = SessionState.GetString(Key + "Path", ""), backup = SessionState.GetString(Key + "Backup", "");
        if (SessionState.GetBool(Key + "HadFile", false) && !File.Exists(backup))
            throw new IOException("Original history backup missing; leave current file untouched.");
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(backup)) File.Move(backup, path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
