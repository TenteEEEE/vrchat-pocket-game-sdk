#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Import into the disposable project's Assets before running the batch smoke test.
// Some batch-mode Unity versions do not pump EditorApplication.update in Play Mode.
public sealed class PocketGameSmokeDriver : MonoBehaviour
{
    private Action tick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartSmokeDriver()
    {
        if (!SessionState.GetBool("PocketGameSdk.Smoke.Running", false)) return;
        var root = new GameObject("SDK smoke test driver");
        DontDestroyOnLoad(root);
        root.AddComponent<PocketGameSmokeDriver>();
    }

    private void Awake()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("VrcPocketGame.Editor.PocketGameClientSimSmoke"))
            .First(t => t != null);
        tick = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Static));
        Debug.Log("[Pocket Game SDK] Runtime smoke driver started.");
    }

    private void Update() { tick(); }
}
#endif
