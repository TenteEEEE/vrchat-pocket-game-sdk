using System;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace VrcPocketGame.Editor
{
    /// <summary>Runs real Udon in ClientSim. This is a local smoke test, not a multiplayer test.</summary>
    [InitializeOnLoad]
    public static class PocketGameClientSimSmoke
    {
        private const string Running = "PocketGameSdk.Smoke.Running";
        private const string Result = "PocketGameSdk.Smoke.Result";
        private const string Deadline = "PocketGameSdk.Smoke.Deadline";
        private static int phase;
        private static double next;
        private static int initialCount;
        private static UdonBehaviour pool;
        private static UdonBehaviour session;
        private static UdonBehaviour game;
        private static UdonBehaviour ui;
        private static GameObject root;

        static PocketGameClientSimSmoke()
        {
            if (!SessionState.GetBool(Running, false)) return;
            Debug.Log("[Pocket Game SDK] Smoke resumed after domain reload.");
            ConfigureClientSim();
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a disposable validation project in batch mode.");
            PocketGameInstaller.InstallIntoCurrentScene();
            if (UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>() == null)
            {
                new GameObject("Smoke test world").AddComponent<VRCSceneDescriptor>();
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Smoke test floor";
                floor.transform.position = new Vector3(0, -.1f, 0);
                floor.transform.localScale = new Vector3(20, .2f, 20);
            }
            var descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            if (descriptor.spawns == null || descriptor.spawns.Length == 0)
            {
                var spawn = new GameObject("Smoke test spawn");
                spawn.transform.position = new Vector3(0, .1f, 0);
                descriptor.spawns = new[] { spawn.transform };
            }
            // Every invocation owns a distinct save namespace; no existing player data is overwritten.
            string testNamespace = "sdk.smoke." + DateTime.UtcNow.Ticks;
            foreach (var counter in UnityEngine.Object.FindObjectsOfType<Sample.PocketCounterGame>(true))
            {
                counter.saveNamespace = testNamespace;
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(counter);
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), PocketGameInstaller.ScenePath);
            SessionState.SetBool(Running, true);
            SessionState.SetInt(Result, 1);
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 100);
            ConfigureClientSim();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.EnterPlaymode();
        }

        private static void ConfigureClientSim()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDK3.ClientSim.ClientSimSettings")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("ClientSim is required for this smoke test.");
            var settings = type.GetProperty("Instance").GetValue(null);
            type.GetField("enableClientSim").SetValue(settings, true);
            type.GetField("initializationDelay").SetValue(settings, 0f);
            type.GetField("spawnPlayer").SetValue(settings, true);
            type.GetField("localPlayerIsMaster").SetValue(settings, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0)) throw new TimeoutException("ClientSim did not finish; phase " + phase + ", playing=" + EditorApplication.isPlaying + ", compiling=" + EditorApplication.isCompiling + ", local=" + (Networking.LocalPlayer != null) + ", pool=" + (pool != null) + ", restored=" + (pool == null ? "unknown" : pool.GetProgramVariable("_playerDataRestored")?.ToString()));
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
                if (phase == 0)
                {
                    if (Networking.LocalPlayer == null) return;
                    pool = Find("PocketGameTerminalPool");
                    if (pool == null || !(bool)pool.GetProgramVariable("_playerDataRestored"))
                    {
                        if (pool != null) Debug.Log("[Pocket Game SDK] Waiting for PlayerData: " + pool.GetProgramVariable("_playerDataRestored"));
                        next = EditorApplication.timeSinceStartup + 10;
                        return;
                    }
                    pool.Interact();
                    Advance(1, 2);
                }
                else if (phase == 1)
                {
                    root = ((GameObject[])pool.GetProgramVariable("terminalRoots")).First(r => r.activeSelf);
                    session = FindIn(root, "PocketGameTerminalSession");
                    game = FindIn(root, "PocketCounterGame");
                    ui = FindIn(root, "PocketGameUi");
                    Check((int)session.GetProgramVariable("assignedPlayerId") == Networking.LocalPlayer.playerId, "claimant assigned");
                    initialCount = Count();
                    game.SendCustomEvent("OwnerAddOne");
                    Check(Count() == initialCount + 1, "owner action updates count");
                    pool.SendCustomEvent("VerifyClaim");
                    Advance(2, 1);
                }
                else if (phase == 2)
                {
                    Check(Count() == initialCount + 1, "claim retry does not reload game");
                    ui.SendCustomEvent("ToggleOverlayDrawer");
                    Check(((GameObject)ui.GetProgramVariable("overlayDrawer")).activeSelf, "overlay drawer opens");
                    ui.SendCustomEvent("ToggleExternalDrawer");
                    Check(((GameObject)ui.GetProgramVariable("externalDrawer")).activeSelf, "external drawer opens");
                    ui.SendCustomEvent("ShowConfirm");
                    game.SendCustomEvent("OwnerAddOne");
                    session.SendCustomEvent("PocketTerminal_OnUseDown");
                    session.SendCustomEvent("PocketTerminal_RequestReturn");
                    Check(Count() == initialCount + 1, "modal blocks button and Pickup Use");
                    Check(root.activeSelf, "modal blocks stow");
                    var scale = root.transform.localScale;
                    ui.SendCustomEvent("SetLargeScale");
                    Check(root.transform.localScale == scale, "modal blocks scale change");
                    ui.SendCustomEvent("ClosePanels");
                    ui.SendCustomEvent("SetSmallScale");
                    Check(Mathf.Abs(root.transform.localScale.x - .85f) < .001f, "scale preserves base units");
                    ui.SendCustomEvent("SetNormalScale");
                    session.SendCustomEvent("PocketTerminal_RequestReturn");
                    Advance(3, 1);
                }
                else if (phase == 3)
                {
                    Check(!root.activeSelf, "stow returns terminal to pool");
                    pool.Interact();
                    Advance(4, 2);
                }
                else if (phase == 4)
                {
                    // A pool may lend a different physical terminal on the next claim.
                    root = ((GameObject[])pool.GetProgramVariable("terminalRoots")).First(r => r.activeSelf);
                    session = FindIn(root, "PocketGameTerminalSession");
                    game = FindIn(root, "PocketCounterGame");
                    ui = FindIn(root, "PocketGameUi");
                    Check(root.activeSelf && Count() == initialCount + 1, "reborrow restores saved progress");
                    pool.SendCustomEvent("AttemptReturn");
                    Check(root.activeSelf, "old return callback does not stow new session");
                    ui.SendCustomEvent("ShowConfirm");
                    game.SendCustomEvent("ConfirmReset");
                    Check(Count() == 0, "confirmed reset updates game");
                    Debug.Log("[Pocket Game SDK] ClientSim smoke PASS: claim/action/retry/drawers/modal/scale/stow/restore/reset.");
                    SessionState.SetInt(Result, 0);
                    phase = 5;
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception error)
            {
                Debug.LogError("[Pocket Game SDK] ClientSim smoke FAIL at phase " + phase + ": " + error);
                SessionState.SetInt(Result, 1);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else Finish();
            }
        }

        private static void OnPlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.EnteredEditMode) Finish(); }
        private static void Finish()
        {
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(SessionState.GetInt(Result, 1));
        }
        private static int Count() { return (int)game.GetProgramVariable("sharedCount"); }
        private static void Advance(int value, double delay)
        {
            phase = value;
            next = EditorApplication.timeSinceStartup + delay;
            Debug.Log("[Pocket Game SDK] Smoke phase " + phase);
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static UdonBehaviour Find(string name) { return UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true).FirstOrDefault(b => Matches(b, name)); }
        private static UdonBehaviour FindIn(GameObject target, string name) { return target.GetComponentsInChildren<UdonBehaviour>(true).First(b => Matches(b, name)); }
        private static bool Matches(UdonBehaviour behaviour, string name)
        {
            var program = behaviour.programSource as UdonSharpProgramAsset;
            return program != null && program.sourceCsScript != null && program.sourceCsScript.GetClass().Name == name;
        }
    }
}
