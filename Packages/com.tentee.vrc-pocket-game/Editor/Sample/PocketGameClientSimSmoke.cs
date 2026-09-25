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
    /// <summary>Runs real Udon in ClientSim, including Tier 0 simulated-remote checks in one editor process.</summary>
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
        private static VRCPlayerApi remote;
        private static UdonBehaviour remoteSession;
        private static GameObject remoteRoot;
        private static int remoteSlot;
        private static int remoteGeneration;
        private static int remoteCount;
        private static GameObject raceRoot;
        private static double raceStart;
        private static Vector3 stalePosition;

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
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 140);
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
                    Check(KioskDistance(root) <= .25f, "first claim places terminal at kiosk front");
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
                    Advance(5, 0);
                }
                else if (phase == 5)
                {
                    Debug.Log("[Pocket Game SDK] Smoke phase 5: simulated remote claim and audience.");
                    SpawnRemotePlayer();
                    var roots = (GameObject[])pool.GetProgramVariable("terminalRoots");
                    var claimed = (int[])pool.GetProgramVariable("claimedPlayerIds");
                    var generations = (int[])pool.GetProgramVariable("claimGenerations");
                    remoteSlot = Array.FindIndex(claimed, id => id < 0);
                    Check(remoteSlot >= 0 && remoteSlot != (int)session.GetProgramVariable("slotIndex"), "remote receives a distinct free slot");
                    var objectPool = (VRCObjectPool)pool.GetProgramVariable("pool");
                    remoteRoot = objectPool.TryToSpawn();
                    Check(remoteRoot == roots[remoteSlot], "remote slot root borrowed from pool");
                    remoteGeneration = generations[remoteSlot] + 1;
                    claimed[remoteSlot] = remote.playerId;
                    generations[remoteSlot] = remoteGeneration;
                    remoteSession = FindIn(remoteRoot, "PocketGameTerminalSession");
                    var remoteGame = FindIn(remoteRoot, "PocketCounterGame");
                    Networking.SetOwner(remote, remoteRoot);
                    Networking.SetOwner(remote, remoteSession.gameObject);
                    var remoteEvents = (UdonBehaviour)remoteSession.GetProgramVariable("gameEvents");
                    if (remoteEvents != null) Networking.SetOwner(remote, remoteEvents.gameObject);
                    remoteSession.SetProgramVariable("assignedPlayerId", remote.playerId);
                    remoteSession.SetProgramVariable("sessionGeneration", remoteGeneration);
                    remoteSession.RunEvent("_onDeserialization");
                    Check(!Networking.IsOwner(remoteSession.gameObject) && !Networking.IsOwner(remoteRoot), "remote session is not local claimant");
                    Check(!((VRC_Pickup)remoteSession.GetProgramVariable("pickup")).pickupable, "remote pickup is disabled locally");
                    var claimantOnly = (GameObject[])remoteSession.GetProgramVariable("claimantOnlyObjects") ?? new GameObject[0];
                    var spectators = (GameObject[])remoteSession.GetProgramVariable("spectatorObjects") ?? new GameObject[0];
                    // The counter sample wires no audience objects; an empty list must not read as a pass.
                    if (claimantOnly.Length + spectators.Length == 0) Debug.Log("[Pocket Game SDK] Smoke skip: no audience objects wired in this scene.");
                    Check(!claimantOnly.Any(o => o.activeSelf), "claimant-only objects hidden");
                    Check(spectators.All(o => o.activeSelf), "spectator objects shown");
                    remoteCount = (int)remoteGame.GetProgramVariable("sharedCount");
                    remoteGame.SendCustomEvent("OwnerAddOne");
                    remoteSession.SendCustomEvent("PocketTerminal_OnUseDown");
                    Check((int)remoteGame.GetProgramVariable("sharedCount") == remoteCount, "local actions do not change remote game count");
                    Advance(6, 0);
                }
                else if (phase == 6)
                {
                    Debug.Log("[Pocket Game SDK] Smoke phase 6: ownership guard decisions.");
                    Check(OwnershipRequest(remoteSession, Networking.LocalPlayer, Networking.LocalPlayer) == false, "remote claim denies local ownership request");
                    Check(OwnershipRequest(remoteSession, remote, remote), "remote claim permits claimant ownership request");
                    Check(root.activeSelf && (int)session.GetProgramVariable("assignedPlayerId") == Networking.LocalPlayer.playerId && Networking.IsOwner(session.gameObject), "local claim remains on its own terminal");
                    Advance(7, 0);
                }
                else if (phase == 7)
                {
                    Debug.Log("[Pocket Game SDK] Smoke phase 7: remote departure cleanup.");
                    RemoveRemotePlayer(remote);
                    Networking.SetOwner(Networking.LocalPlayer, pool.gameObject);
                    pool.SendCustomEvent("CleanupMissingClaims");
                    var claims = (int[])pool.GetProgramVariable("claimedPlayerIds");
                    var generations = (int[])pool.GetProgramVariable("claimGenerations");
                    Check(claims[remoteSlot] == -1 && generations[remoteSlot] > remoteGeneration, "missing remote claim cleared and generation advanced");
                    Check(!remoteRoot.activeSelf, "remote terminal returned to pool");
                    // Both terminals were in use until now; the freed slot checks the unclaimed-terminal rule.
                    SpawnRemotePlayer();
                    Check(OwnershipRequest(remoteSession, Networking.LocalPlayer, Networking.LocalPlayer), "pool owner may request ownership of free slot");
                    Check(!OwnershipRequest(remoteSession, remote, Networking.LocalPlayer), "non-pool owner denied ownership of free slot");
                    RemoveRemotePlayer(remote);
                    Advance(8, 0);
                }
                else if (phase == 8)
                {
                    Debug.Log("[Pocket Game SDK] Smoke phase 8: stale previous-owner pose on a new claim.");
                    session.SendCustomEvent("PocketTerminal_RequestReturn");
                    Advance(9, 1);
                }
                else if (phase == 9)
                {
                    Check(!root.activeSelf, "terminal stowed before the stale-pose claim");
                    SpawnRemotePlayer();
                    pool.Interact();
                    raceRoot = null;
                    Advance(10, 0);
                }
                else if (phase == 10)
                {
                    // Stands in for PR #18's race: the previous owner keeps the root and its ObjectSync keeps
                    // sending the old pose until the claimant's VerifyClaim (0.7 s after the claim) takes it back.
                    if (raceRoot == null)
                    {
                        var roots = (GameObject[])pool.GetProgramVariable("terminalRoots");
                        int slot = Array.FindIndex((int[])pool.GetProgramVariable("claimedPlayerIds"), id => id == Networking.LocalPlayer.playerId);
                        if (slot < 0 || !roots[slot].activeSelf) return;
                        raceRoot = roots[slot];
                        raceStart = EditorApplication.timeSinceStartup;
                        stalePosition = KioskPosition() + Vector3.right * 5f;
                    }
                    if (EditorApplication.timeSinceStartup - raceStart < .4)
                    {
                        Networking.SetOwner(remote, raceRoot);
                        raceRoot.transform.position = stalePosition;
                        return;
                    }
                    Advance(11, 3);
                }
                else if (phase == 11)
                {
                    float kioskDistance = KioskDistance(raceRoot);
                    Check(kioskDistance <= .25f, "new claim ends at kiosk front despite a stale previous-owner pose (distance=" + kioskDistance.ToString("F2") + "m)");
                    Check(Vector3.Distance(raceRoot.transform.position, stalePosition) > 2f, "new claim does not keep the stale previous-owner pose");
                    Check(Networking.IsOwner(raceRoot), "claimant took the terminal root back");
                    RemoveRemotePlayer(remote);
                    root = raceRoot;
                    Advance(12, 1);
                }
                else if (phase == 12)
                {
                    Debug.Log("[Pocket Game SDK] Smoke phase 12: kiosk reuse placement.");
                    // At the default spawn the head-relative recall point lands on the kiosk front, so step aside first.
                    Networking.LocalPlayer.TeleportTo(KioskPosition() + Vector3.left * 3f, Quaternion.LookRotation(Vector3.left));
                    Advance(13, 1);
                }
                else if (phase == 13)
                {
                    Check(Vector3.Distance(HeadRecallPosition(), KioskPosition()) > 1f, "head-relative recall point is distinct from the kiosk front");
                    root.transform.position = KioskPosition() + Vector3.right * 5f;
                    var objectSync = root.GetComponent<VRCObjectSync>();
                    if (objectSync != null) objectSync.FlagDiscontinuity();
                    pool.Interact();
                    Advance(14, 1.5);
                }
                else if (phase == 14)
                {
                    float kioskDistance = KioskDistance(root);
                    float headRecallDistance = Vector3.Distance(root.transform.position, HeadRecallPosition());
                    Check(kioskDistance <= .25f, "kiosk reuse places terminal at kiosk front");
                    Check(headRecallDistance > .5f, "kiosk reuse does not use head-relative recall position");
                    Debug.Log("[Pocket Game SDK] ClientSim smoke PASS: local UI/persistence checks, simulated remote guards/cleanup, first-claim placement, stale-pose recovery, and kiosk-reuse placement.");
                    SessionState.SetInt(Result, 0);
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
        private static Vector3 KioskPosition() => pool.transform.position - pool.transform.forward * (float)pool.GetProgramVariable("spawnDistance");
        // Builder makes the root kinematic with gravity off; 0.25 m allows minor ClientSim/ObjectSync pose error while separating the 5 m test offset.
        private static float KioskDistance(GameObject target) => Vector3.Distance(target.transform.position, KioskPosition());
        private static Vector3 HeadRecallPosition()
        {
            var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            return head.position + forward * .7f - Vector3.up * .18f;
        }
        private static bool OwnershipRequest(UdonBehaviour target, VRCPlayerApi requester, VRCPlayerApi owner)
        {
            // UdonSharp names event parameters after the Udon node outputs, so read them from its compiler.
            var udonInterface = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UdonSharp.Compiler.Udon.CompilerUdonInterface")).FirstOrDefault(t => t != null);
            Check(udonInterface != null, "UdonSharp event argument table available");
            var args = ((System.Collections.IEnumerable)udonInterface.GetMethod("GetUdonEventArgs", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { "OnOwnershipRequest" })).Cast<ValueTuple<string, Type>>().ToArray();
            Check(args.Length == 2, "OnOwnershipRequest has two Udon parameters");
            bool invoked = target.RunEventAdvanced("_onOwnershipRequest", false, false,
                (args[0].Item1, (object)requester), (args[1].Item1, (object)owner));
            Check(invoked, "compiled OnOwnershipRequest event found");
            return (bool)target.GetProgramVariable("__returnValue");
        }
        private static void SpawnRemotePlayer()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDK3.ClientSim.ClientSimMain")).FirstOrDefault(t => t != null);
            Check(type != null, "ClientSimMain available");
            type.GetMethod("SpawnRemotePlayer", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { "Pocket Smoke Remote" });
            remote = null;
            for (int i = 0; i < 100 && remote == null; i++)
            {
                foreach (var player in VRCPlayerApi.AllPlayers)
                    if (player != null && !player.isLocal) { remote = player; break; }
            }
            Check(remote != null, "remote player spawned");
        }
        private static void RemoveRemotePlayer(VRCPlayerApi player)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDK3.ClientSim.ClientSimMain")).First(t => t != null);
            type.GetMethod("RemovePlayer", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { player });
        }
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
