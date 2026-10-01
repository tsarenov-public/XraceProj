using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Batch verification of real scene wiring, preview safety and transition.
[InitializeOnLoad]
public static class GarageSmokeCheck
{
    private const string Key = "Xrace.GarageSmokeStep";
    private static int frames;
    static GarageSmokeCheck() => EditorApplication.update += Tick;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Garage.unity");
        SessionState.SetInt(Key, 1);
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddMinutes(3).ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        int step = SessionState.GetInt(Key, 0);
        if (step == 0) return;
        try
        {
            if (DateTime.UtcNow > DateTime.Parse(SessionState.GetString(Key + "Deadline", "")))
                throw new Exception("Garage smoke check timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (++frames < 15) return;
            frames = 0;
            Scene scene = SceneManager.GetActiveScene();
            if (step == 4)
            {
                if (scene.name != "Touge01") return;
                Require(SelectedCarService.Get() != null && SelectedCarService.Get().carId == "ae86", "Selected car did not survive scene transition.");
                Debug.Log("XRACE_GARAGE_SMOKE_OK: initial preview, next, previous, safe preview, selected car and Touge01 load.");
                SessionState.SetInt(Key, 0);
                EditorApplication.Exit(0);
                return;
            }
            Require(scene.name == "Garage", "Unexpected active scene.");
            GameObject[] roots = scene.GetRootGameObjects();
            GarageManager manager = roots.Select(root => root.GetComponent<GarageManager>()).First(value => value != null);
            TMP_Text name = roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>()).First(value => value.name == "CarName_Text");
            Button[] buttons = roots.SelectMany(root => root.GetComponentsInChildren<Button>()).ToArray();
            Require(MusicManager.Instance != null, "Music manager missing.");
            GameObject preview = manager.GetComponentsInChildren<Transform>().First(value => value.name == "CarPreview").gameObject;
            Require(preview.activeInHierarchy, "Preview inactive.");
            Require(preview.GetComponentsInChildren<Renderer>().Length > 0, "Preview has no renderers.");
            Require(preview.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Vehicle scripts still active on preview.");
            Require(preview.GetComponentsInChildren<Rigidbody>(true).All(body => body.isKinematic), "Preview physics active.");
            Require(preview.GetComponentsInChildren<Camera>(true).All(camera => !camera.enabled), "Preview camera active.");
            if (step == 1)
            {
                Require(name.text == "Toyota AE86", "Wrong initial car label.");
                CapturePreview(roots);
                buttons.First(button => button.name == "Btn_NextCar").onClick.Invoke();
            }
            else if (step == 2)
            {
                Require(name.text == "RMCar26", "Next button failed.");
                buttons.First(button => button.name == "Btn_PrevCar").onClick.Invoke();
            }
            else
            {
                Require(name.text == "Toyota AE86", "Previous button failed.");
                buttons.First(button => button.name == "Btn_Race").onClick.Invoke();
            }
            SessionState.SetInt(Key, step + 1);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void CapturePreview(GameObject[] roots)
    {
        Camera camera = roots.Select(root => root.GetComponent<Camera>()).First(value => value != null);
        Canvas canvas = roots.Select(root => root.GetComponent<Canvas>()).First(value => value != null);
        var renderTexture = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture });
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes("Logs/GaragePreview.png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }
}
