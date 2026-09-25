#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharp;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

// Copy into Assets of a disposable VCC validation project. Reflection keeps the
// driver compilable when ParrelSync or MultiSim is not installed.
public sealed class PocketGameMultiSimSmoke : MonoBehaviour
{
    private const string Running = "PocketGameSdk.MultiSim.Running";
    private const string Result = "PocketGameSdk.MultiSim.Result";
    private const string Deadline = "PocketGameSdk.MultiSim.Deadline";
    private const string Phase = "PocketGameSdk.MultiSim.Phase";
    private const string PreviousMultiSim = "PocketGameSdk.MultiSim.PreviousEnabled";
    private const string MultiSimEnabled = "MultiSim.Enabled";
    private const string PreviousClientSim = "PocketGameSdk.MultiSim.PreviousClientSim";
    private const string ClientSimSettingsKey = "com.vrchat.clientsim.settings";
    private static Action tick;

    private static UdonBehaviour pool;
    private static UdonBehaviour hostSession;
    private static UdonBehaviour hostGame;
    private static GameObject hostRoot;
    private static UdonBehaviour clientSession;
    private static UdonBehaviour clientGame;
    private static GameObject clientRoot;
    private static int hostId;
    private static int clientId;
    private static int hostSlot;
    private static int clientSlot;
    private static int hostCount;
    private static int clientCount;
    private static bool clientCountStarted;
    private static int borrowedSlot = -1;
    private static Vector3 stalePosition;
    private static bool sawFarReuse;
    private static bool clientStowObserved;
    private static bool clientPlacementObserved;
    private static double borrowSeenAt = -1;
    private static double kioskSince = -1;
    private static int reverts;
    // Entering Play Mode reloads the domain, so the role is derived again instead of cached.
    private static bool isHost => !IsClone();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartDriver()
    {
        if (!SessionState.GetBool(Running, false)) return;
        var root = new GameObject("Pocket MultiSim smoke driver");
        DontDestroyOnLoad(root);
        root.AddComponent<PocketGameMultiSimSmoke>();
    }

    private void Awake()
    {
        tick = Tick;
        Debug.Log("[Pocket MultiSim] Runtime driver started.");
    }

    private void Update() { tick?.Invoke(); }

    [InitializeOnLoadMethod]
    private static void ResumeAfterReload()
    {
        if (!SessionState.GetBool(Running, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    public static void PrepareSceneBatch()
    {
        try
        {
            RequireBatch();
            if (EditorApplication.isPlaying) throw new InvalidOperationException("PrepareSceneBatch requires edit mode.");
            if (IsClone()) throw new InvalidOperationException("PrepareSceneBatch is host-only; run it in the original project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var installer = FindType("VrcPocketGame.Editor.PocketGameInstaller") ?? throw new InvalidOperationException("PocketGameInstaller was not found. Import the SDK package and Editor/Sample scripts.");
            installer.GetMethod("InstallIntoCurrentScene", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            if (UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>() == null)
            {
                new GameObject("MultiSim smoke world").AddComponent<VRCSceneDescriptor>();
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "MultiSim smoke floor";
                floor.transform.position = new Vector3(0, -.1f, 0);
                floor.transform.localScale = new Vector3(20, .2f, 20);
            }
            var descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            if (descriptor.spawns == null || descriptor.spawns.Length == 0)
            {
                var spawn = new GameObject("MultiSim smoke spawn");
                spawn.transform.position = new Vector3(0, .1f, 0);
                descriptor.spawns = new[] { spawn.transform };
            }
            string uniqueNamespace = "sdk.multisim." + DateTime.UtcNow.Ticks;
            foreach (var counter in UnityEngine.Object.FindObjectsOfType<VrcPocketGame.Sample.PocketCounterGame>(true))
            {
                counter.saveNamespace = uniqueNamespace;
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(counter);
            }
            var path = installer.GetField("ScenePath", BindingFlags.Public | BindingFlags.Static);
            string scenePath = (string)path.GetRawConstantValue();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath)) throw new InvalidOperationException("Could not save prepared scene to " + scenePath);
            Log("PASS scene prepared at " + scenePath + " with save namespace " + uniqueNamespace);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { FailAndExit("prepare scene", e); }
    }

    public static void RunBatch()
    {
        try
        {
            RequireBatch();
            if (EditorApplication.isPlaying) throw new InvalidOperationException("RunBatch requires edit mode.");
            if (FindType("MultiSim.MultiSimCore") == null) throw new InvalidOperationException("MultiSim is missing. Install VRChat MultiSim v0.2.6 in the validation project.");
            SessionState.SetBool(PreviousMultiSim, EditorPrefs.GetBool(MultiSimEnabled, false));
            // ClientSim settings are machine-wide EditorPrefs; keep the raw value to restore after the run.
            SessionState.SetString(PreviousClientSim, EditorPrefs.HasKey(ClientSimSettingsKey) ? EditorPrefs.GetString(ClientSimSettingsKey) : "");
            ConfigureClientSim();
            EditorPrefs.SetBool(MultiSimEnabled, true);
            var scenePath = "Assets/VrcPocketGameGenerated/CounterSample.unity";
            if (!File.Exists(scenePath)) throw new FileNotFoundException("Run PrepareSceneBatch on the original project first.", scenePath);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SessionState.SetBool(Running, true);
            SessionState.SetInt(Result, 1);
            SessionState.SetInt(Phase, 0);
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + (isHost ? 600 : 300));
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
            Log("starting " + (isHost ? "HOST" : "CLIENT") + " run; scene opened without saving");
            Log("known limitations: MultiSim does not invoke OnOwnershipRequest; simultaneous-claim races are not tested");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { FailAndExit("start run", e); }
    }

    private static void ConfigureClientSim()
    {
        var type = FindType("VRC.SDK3.ClientSim.ClientSimSettings") ?? throw new InvalidOperationException("ClientSim is required.");
        var settings = JsonUtility.FromJson(EditorPrefs.GetString(ClientSimSettingsKey, "{}"), type);
        SetField(type, settings, "enableClientSim", true);
        SetField(type, settings, "initializationDelay", 0f);
        SetField(type, settings, "spawnPlayer", true);
        SetField(type, settings, "localPlayerIsMaster", true);
        type.GetMethod("SaveSettings", BindingFlags.Public | BindingFlags.Static).Invoke(null, new[] { settings });
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            int phase = SessionState.GetInt(Phase, 0);
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0))
                throw new TimeoutException("phase " + phase + ", role=" + (isHost ? "host" : "client") + ", local=" + (Networking.LocalPlayer != null) + ", players=" + VRCPlayerApi.GetPlayerCount());
            if (Networking.LocalPlayer == null) return;
            pool = Find("PocketGameTerminalPool");
            if (pool == null || !(bool)pool.GetProgramVariable("_playerDataRestored")) return;
            if (isHost) TickHost(phase); else TickClient(phase);
        }
        catch (Exception e) { Log("FAIL phase " + SessionState.GetInt(Phase, 0) + ": " + e); SessionState.SetInt(Result, 1); EditorApplication.ExitPlaymode(); }
    }

    private static void TickHost(int phase)
    {
        var claims = (int[])pool.GetProgramVariable("claimedPlayerIds");
        var roots = (GameObject[])pool.GetProgramVariable("terminalRoots");
        if (phase == 0)
        {
            hostId = Networking.LocalPlayer.playerId;
            pool.Interact();
            SetPhase(1);
        }
        else if (phase == 1)
        {
            hostSlot = Array.FindIndex(claims, id => id == hostId);
            if (hostSlot < 0 || !roots[hostSlot].activeSelf) return;
            hostRoot = roots[hostSlot]; hostSession = FindIn(hostRoot, "PocketGameTerminalSession"); hostGame = FindIn(hostRoot, "PocketCounterGame");
            hostCount = IntVar(hostGame, "sharedCount");
            if (hostCount < 1) { hostGame.SendCustomEvent("OwnerAddOne"); return; }
            Pass("host claimed terminal and incremented counter");
            Log("HOST READY_FOR_CLIENT hostId=" + hostId + " slot=" + hostSlot + " count=" + hostCount);
            SetPhase(2);
        }
        else if (phase == 2)
        {
            if (VRCPlayerApi.GetPlayerCount() != 2) return;
            clientId = VRCPlayerApi.AllPlayers.First(p => p != null && !p.isLocal).playerId;
            if (!Elapsed(3)) return;
            hostRoot.transform.position = KioskPosition() + Vector3.right * 5f;
            var sync = hostRoot.GetComponent<VRCObjectSync>(); if (sync != null) sync.FlagDiscontinuity();
            Log("HOST moved old terminal far pos=" + hostRoot.transform.position + " kioskDistance=" + KioskDistance(hostRoot).ToString("F2") + "m");
            SetPhase(20);
        }
        else if (phase == 20)
        {
            if (!Elapsed(1.5f)) return;
            hostSession.SendCustomEvent("PocketTerminal_RequestReturn"); SetPhase(21);
        }
        else if (phase == 21)
        {
            if (claims[hostSlot] != -1 || roots[hostSlot].activeSelf) return;
            Log("HOST STOWED_FAR slot=" + hostSlot + " pos=" + hostRoot.transform.position);
            SetPhase(3);
        }
        else if (phase == 3)
        {
            clientSlot = Array.FindIndex(claims, id => id == clientId);
            if (clientSlot < 0 || !roots[clientSlot].activeSelf) return;
            clientRoot = roots[clientSlot]; Require(clientSlot == hostSlot, "host observes client borrowing same slot");
            borrowSeenAt = EditorApplication.timeSinceStartup;
            Pass("host observes client borrowing same slot");
            SetPhase(30);
        }
        else if (phase == 30)
        {
            // The borrower's first ObjectSync updates can still carry the previous owner's pose; PR #18 re-places
            // the terminal while it verifies the claim. Record those transients and require the settled result.
            double now = EditorApplication.timeSinceStartup;
            float d = KioskDistance(clientRoot);
            if (d <= .25f) { if (kioskSince < 0) kioskSince = now; }
            else
            {
                if (kioskSince >= 0) { reverts++; Log("host saw borrowed terminal leave the kiosk front at +" + (now - borrowSeenAt).ToString("F2") + "s (distance=" + d.ToString("F2") + "m)"); }
                kioskSince = -1;
            }
            Require(now - borrowSeenAt < 15, "borrowed terminal settles at kiosk front within 15 s (last distance=" + d.ToString("F2") + "m)");
            if (kioskSince < 0 || now - kioskSince < 3) return;
            Pass("#18 host sees borrowed terminal settle at kiosk front (settled at +" + (kioskSince - borrowSeenAt).ToString("F2") + "s, left the kiosk front " + reverts + " time(s) before settling)");
            SetPhase(4);
        }
        else if (phase == 4)
        {
            float d = KioskDistance(clientRoot);
            if (d > 2f) { sawFarReuse = true; return; }
            if (!sawFarReuse) return;
            SetPhase(40);
        }
        else if (phase == 40)
        {
            float d = KioskDistance(clientRoot); Require(d <= .25f, "client kiosk reuse is at kiosk front distance=" + d.ToString("F2") + "m");
            if (!Elapsed(2)) return;
            Pass("#18 host sees kiosk reuse placement (distance=" + d.ToString("F2") + "m)"); SetPhase(5);
        }
        else if (phase == 5) { pool.Interact(); SetPhase(51); }
        else if (phase == 51)
        {
            hostSlot = Array.FindIndex(claims, id => id == hostId); if (hostSlot < 0 || !roots[hostSlot].activeSelf) return;
            hostRoot = roots[hostSlot]; hostSession = FindIn(hostRoot, "PocketGameTerminalSession"); Log("HOST reclaimed slot=" + hostSlot); SetPhase(6);
        }
        else if (phase == 6)
        {
            if (claims[clientSlot] != -1 || roots[clientSlot].activeSelf) return;
            Pass("host observed client stow"); SetPhase(7);
        }
        else if (phase == 7)
        {
            int slot = Array.FindIndex(claims, id => id == clientId); if (slot < 0 || !roots[slot].activeSelf) return;
            clientSlot = slot; Pass("host observed client reclaim"); SetPhase(8);
        }
        else if (phase == 8)
        {
            if (VRCPlayerApi.GetPlayerById(clientId) != null) return;
            Pass("host observed client leave via player removal"); Log("pool owner after client leave: " + Networking.GetOwner(pool.gameObject)?.playerId); SetPhase(9);
        }
        else if (phase == 9)
        {
            if (claims[clientSlot] != -1 || roots[clientSlot].activeSelf) return;
            Check(Networking.IsOwner(pool.gameObject), "host became pool owner after client leave");
            Check(claims[hostSlot] == hostId && roots[hostSlot].activeSelf, "host claim remains intact"); Pass("client claim cleaned after leave; host claim remains"); Finish(0);
        }
    }

    private static void TickClient(int phase)
    {
        var claims = (int[])pool.GetProgramVariable("claimedPlayerIds");
        var roots = (GameObject[])pool.GetProgramVariable("terminalRoots");
        if (phase == 0)
        {
            if (VRCPlayerApi.GetPlayerCount() != 2) return;
            clientId = Networking.LocalPlayer.playerId;
            hostId = VRCPlayerApi.AllPlayers.First(p => p != null && !p.isLocal).playerId;
            clientSlot = Array.FindIndex(claims, id => id == hostId);
            if (clientSlot < 0 || !roots[clientSlot].activeSelf) return;
            borrowedSlot = clientSlot;
            var s = FindIn(roots[clientSlot], "PocketGameTerminalSession");
            var game = FindIn(roots[clientSlot], "PocketCounterGame");
            Check((int)s.GetProgramVariable("assignedPlayerId") == hostId && !Networking.IsOwner(s.gameObject) && !((VRC_Pickup)s.GetProgramVariable("pickup")).pickupable, "host terminal spectator and pickup disabled");
            CheckAudience(s);
            Check(IntVar(game, "sharedCount") == 1, "counter synced count matches host's logged value");
            Pass("late join sees host claim, active root, spectator state, synced spectator count");
            SetPhase(1);
        }
        else if (phase == 1)
        {
            if (Array.FindIndex(claims, id => id == hostId) >= 0 || roots[borrowedSlot].activeSelf) return;
            stalePosition = roots[borrowedSlot].transform.position;
            Log("CLIENT observed HOST stow slot=" + borrowedSlot + " stalePos=" + stalePosition + " kioskDistance=" + KioskDistance(roots[borrowedSlot]).ToString("F2") + "m");
            Check(KioskDistance(roots[borrowedSlot]) > 2f, "stowed root retains far previous-owner position"); pool.Interact(); SetPhase(2);
        }
        else if (phase == 2)
        {
            clientSlot = Array.FindIndex(claims, id => id == clientId); if (clientSlot < 0 || !roots[clientSlot].activeSelf) return;
            Check(clientSlot == borrowedSlot, "B borrows the slot A just used; expected=" + borrowedSlot + " actual=" + clientSlot);
            clientRoot = roots[clientSlot]; clientSession = FindIn(clientRoot, "PocketGameTerminalSession"); clientGame = FindIn(clientRoot, "PocketCounterGame");
            float d = KioskDistance(clientRoot); Check(d <= .25f, "new claim kiosk distance=" + d.ToString("F2") + "m");
            Check(Vector3.Distance(clientRoot.transform.position, stalePosition) > 2f, "new claim away from previous-owner position");
            if (!clientPlacementObserved) { clientPlacementObserved = true; SetPhase(22); return; }
        }
        else if (phase == 22)
        {
            float d = KioskDistance(clientRoot); Require(d <= .25f, "new claim remains at kiosk front distance=" + d.ToString("F2") + "m");
            Require(Vector3.Distance(clientRoot.transform.position, stalePosition) > 2f, "new claim remains away from previous-owner position");
            if (!Elapsed(3)) return;
            Pass("#18 new claim appears at kiosk front, not at previous owner's position (kiosk=" + d.ToString("F2") + "m, stale=" + Vector3.Distance(clientRoot.transform.position, stalePosition).ToString("F2") + "m)"); SetPhase(3);
        }
        else if (phase == 3)
        {
            // Give the host time to confirm the settled borrow before this deliberate move.
            if (!Elapsed(4)) return;
            // At the shared spawn the head-relative recall point lands on the kiosk front, so step aside first.
            Networking.LocalPlayer.TeleportTo(KioskPosition() + Vector3.left * 3f, Quaternion.LookRotation(Vector3.left));
            // A different spot from the host's far stow keeps the two moves distinguishable in the logs.
            clientRoot.transform.position = KioskPosition() + Vector3.left * 6f; var sync = clientRoot.GetComponent<VRCObjectSync>(); if (sync != null) sync.FlagDiscontinuity(); SetPhase(30);
        }
        else if (phase == 30)
        {
            if (!Elapsed(2)) return;
            Check(Vector3.Distance(HeadRecallPosition(), KioskPosition()) > 1f, "head-relative recall point is distinct from the kiosk front");
            pool.Interact(); SetPhase(31);
        }
        else if (phase == 31)
        {
            float d = KioskDistance(clientRoot); Require(d <= .25f, "reuse kiosk distance=" + d.ToString("F2") + "m"); Require(Vector3.Distance(clientRoot.transform.position, HeadRecallPosition()) > .5f, "reuse not at head-relative recall position");
            if (!Elapsed(2)) return; Pass("#18 kiosk reuse places terminal at kiosk front (distance=" + d.ToString("F2") + "m)"); SetPhase(4);
        }
        else if (phase == 4) { if (Array.FindIndex(claims, id => id == hostId) >= 0) SetPhase(5); }
        else if (phase == 5)
        {
            if (!clientCountStarted) { clientCount = IntVar(clientGame, "sharedCount"); clientCountStarted = true; }
            clientGame.SendCustomEvent("OwnerAddOne"); if (IntVar(clientGame, "sharedCount") <= clientCount) return;
            Check(IntVar(clientGame, "sharedCount") == clientCount + 1, "client owner increments counter"); clientSession.SendCustomEvent("PocketTerminal_RequestReturn"); SetPhase(6);
        }
        else if (phase == 6)
        {
            if (claims[clientSlot] != -1 || roots[clientSlot].activeSelf) return;
            if (!clientStowObserved) { clientStowObserved = true; SetPhase(60); return; }
        }
        else if (phase == 60)
        {
            if (claims[clientSlot] != -1 || roots[clientSlot].activeSelf || !Elapsed(3)) return;
            Pass("client stowed terminal and allowed 3 s replication"); pool.Interact(); SetPhase(7);
        }
        else if (phase == 7)
        {
            clientSlot = Array.FindIndex(claims, id => id == clientId); if (clientSlot < 0 || !roots[clientSlot].activeSelf) return;
            Pass("client reclaimed terminal"); Log("CLIENT DONE"); Finish(0);
        }
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
        EditorPrefs.SetBool(MultiSimEnabled, SessionState.GetBool(PreviousMultiSim, false));
        string previousClientSim = SessionState.GetString(PreviousClientSim, "");
        if (previousClientSim.Length > 0) EditorPrefs.SetString(ClientSimSettingsKey, previousClientSim);
        else EditorPrefs.DeleteKey(ClientSimSettingsKey);
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Log("run complete with exit code " + SessionState.GetInt(Result, 1));
        EditorApplication.Exit(SessionState.GetInt(Result, 1));
    }

    private static void Finish(int code) { SessionState.SetInt(Result, code); EditorApplication.ExitPlaymode(); }
    private static void SetPhase(int value) { SessionState.SetInt(Phase, value); SessionState.SetFloat("PocketGameSdk.MultiSim.PhaseStarted", Time.unscaledTime); SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 240); Log("phase " + value); }
    private static bool Elapsed(float seconds) => Time.unscaledTime - SessionState.GetFloat("PocketGameSdk.MultiSim.PhaseStarted", 0) >= seconds;
    private static Vector3 KioskPosition() => pool.transform.position - pool.transform.forward * (float)pool.GetProgramVariable("spawnDistance");
    // Root is kinematic with gravity disabled (builder), but 0.25 m allows ObjectSync transform quantization while separating the 5 m test offset.
    private static float KioskDistance(GameObject root) => Vector3.Distance(root.transform.position, KioskPosition());
    private static Vector3 HeadRecallPosition() { var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head); var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up).normalized; if (forward.sqrMagnitude < .1f) forward = Vector3.forward; return head.position + forward * .7f - Vector3.up * .18f; }
    private static void Pass(string name) { Log("PASS " + name); }
    // Sampled every tick while waiting for stability: fail loudly, but log only the final PASS.
    private static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException("check failed: " + name); }
    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("check failed: " + name); Pass(name); }
    // The counter sample wires no audience objects; an empty list must not read as a pass.
    private static void CheckAudience(UdonBehaviour s)
    {
        var spectators = (GameObject[])s.GetProgramVariable("spectatorObjects") ?? new GameObject[0];
        var claimantOnly = (GameObject[])s.GetProgramVariable("claimantOnlyObjects") ?? new GameObject[0];
        if (spectators.Length == 0 && claimantOnly.Length == 0) { Log("SKIP audience objects: none wired in this scene"); return; }
        Check(spectators.All(o => o.activeSelf), "spectator objects active");
        Check(!claimantOnly.Any(o => o.activeSelf), "claimant-only objects inactive");
    }
    private static int IntVar(UdonBehaviour b, string name) => (int)b.GetProgramVariable(name);
    private static void RequireBatch() { if (!Application.isBatchMode) throw new InvalidOperationException("Use a disposable validation project in batch mode."); }
    private static void Log(string message) { Debug.Log("[Pocket MultiSim] " + message); }
    private static void FailAndExit(string where, Exception e) { Log("FAIL " + where + ": " + e); EditorApplication.Exit(1); }
    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
    private static bool IsClone()
    {
        var method = FindType("ParrelSync.ClonesManager")?.GetMethod("IsClone", BindingFlags.Public | BindingFlags.Static);
        if (method != null) return (bool)method.Invoke(null, null);
        return File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".clone"));
    }
    private static void SetField(Type type, object target, string name, object value) => type.GetField(name, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);
    private static UdonBehaviour Find(string name) => UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true).FirstOrDefault(b => Matches(b, name));
    private static UdonBehaviour FindIn(GameObject root, string name) => root.GetComponentsInChildren<UdonBehaviour>(true).First(b => Matches(b, name));
    private static bool Matches(UdonBehaviour b, string name)
    {
        var asset = b.programSource as UdonSharp.UdonSharpProgramAsset;
        return asset != null && asset.sourceCsScript != null && asset.sourceCsScript.GetClass().Name == name;
    }
}
#endif
