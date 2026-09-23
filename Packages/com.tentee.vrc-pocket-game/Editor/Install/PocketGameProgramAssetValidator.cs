using System;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.ProgramSources;
using VRC.Udon;

namespace VrcPocketGame.Editor
{
    internal static class PocketGameProgramAssetValidator
    {
        private static readonly Type[] CoreTypes = {
            typeof(PocketGameTerminalPool), typeof(PocketGameTerminalSession),
            typeof(PocketGameInputRelay), typeof(PocketGameUi)
        };

        private sealed class Findings
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            public void Error(string message) { Errors.Add(message); }
            public void Warning(string message) { Warnings.Add(message); }

            public int Finish()
            {
                foreach (var warning in Warnings) Debug.LogWarning("[Pocket Game SDK] " + warning);
                if (Errors.Count == 0) return Warnings.Count;
                throw new InvalidOperationException("Scene validation found " + Errors.Count + " problem(s):\n- " + string.Join("\n- ", Errors));
            }
        }

        public static int Validate(PocketGameTerminalPool[] pools)
        {
            var findings = new Findings();
            var behaviours = new HashSet<UdonBehaviour>();
            var gameEvents = new HashSet<UdonBehaviour>();
            var buttons = new HashSet<Button>();
            var validPools = pools ?? new PocketGameTerminalPool[0];
            foreach (var pool in validPools)
            {
                if (pool == null)
                {
                    findings.Error("A terminal pool reference is missing.");
                    continue;
                }
                AddSubtree(pool.gameObject, behaviours, buttons);
                if (pool.terminalRoots != null)
                    foreach (var root in pool.terminalRoots)
                        if (root != null) AddSubtree(root, behaviours, buttons);
                if (pool.terminalSessions != null)
                {
                    foreach (var session in pool.terminalSessions)
                        if (session != null && session.gameEvents != null)
                        {
                            gameEvents.Add(session.gameEvents);
                            behaviours.Add(session.gameEvents);
                        }
                }
            }

            foreach (var button in buttons)
            {
                var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null) continue;
                for (var i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    var target = call.FindPropertyRelative("m_Target").objectReferenceValue as UdonBehaviour;
                    if (target != null) behaviours.Add(target);
                }
            }

            var behaviourTypes = new Dictionary<UdonBehaviour, Type>();
            foreach (var behaviour in behaviours)
            {
                var serialized = new SerializedObject(behaviour);
                var sourceProperty = serialized.FindProperty("programSource");
                var source = sourceProperty == null ? null : sourceProperty.objectReferenceValue;
                var path = source == null ? "<missing program asset>" : AssetDatabase.GetAssetPath(source);
                if (source == null)
                {
                    var deleted = sourceProperty != null && sourceProperty.objectReferenceInstanceIDValue != 0;
                    findings.Error(ObjectContext(behaviour, null, path) + (deleted ? " references a deleted program asset." : " has no program asset."));
                    continue;
                }

                var program = source as UdonSharpProgramAsset;
                if (program == null)
                {
                    findings.Warning(ObjectContext(behaviour, null, path) + " references a graph program; its C# type cannot be checked.");
                    continue;
                }

                var script = program.sourceCsScript;
                if (script == null)
                {
                    findings.Error(ObjectContext(behaviour, null, path) + " references a program asset whose C# script was deleted.");
                    continue;
                }
                var type = script.GetClass();
                if (type == null)
                {
                    findings.Error(ObjectContext(behaviour, null, path) + " references a program asset whose C# class is not compiled or does not match its file name.");
                    continue;
                }
                behaviourTypes[behaviour] = type;
                // Read the serialized link directly: GetRealProgram() is a non-serialized cache and the
                // SerializedProgramAsset getter creates assets, which a validator must not do.
                var compiled = new SerializedObject(program).FindProperty("serializedUdonProgramAsset");
                var serializedProgram = compiled == null ? null : compiled.objectReferenceValue as AbstractSerializedUdonProgramAsset;
                if (serializedProgram == null || serializedProgram.RetrieveProgram() == null)
                    findings.Error(ObjectContext(behaviour, type, path) + " has an uncompiled program; run PrepareProgramAssetsBatch and let UdonSharp compile.");
            }

            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                findings.Error("UdonSharp reports a compile or assembly error in the project; compile the scripts before validating the scene.");

            var allAssets = AssetDatabase.FindAssets("t:UdonSharpProgramAsset")
                .Select(id => AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(asset => asset != null).ToArray();
            var relevantTypes = new HashSet<Type>(CoreTypes);
            foreach (var type in behaviourTypes.Values) relevantTypes.Add(type);

            foreach (var asset in allAssets)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (asset.sourceCsScript == null && path.StartsWith(PocketGameTerminalBuilder.GeneratedRoot + "/", StringComparison.Ordinal))
                    findings.Warning("Orphan program asset under GeneratedRoot has no source script: '" + path + "'.");
            }

            foreach (var type in relevantTypes)
            {
                var matching = allAssets.Where(asset => asset.sourceCsScript != null && asset.sourceCsScript.GetClass() == type).ToArray();
                if (matching.Length > 1)
                    findings.Error("Duplicate program assets for " + type.FullName + ": " + string.Join(", ", matching.Select(asset => "'" + AssetDatabase.GetAssetPath(asset) + "'")) + ".");
                if (CoreTypes.Contains(type))
                {
                    var expected = PocketGameTerminalBuilder.GeneratedRoot + "/" + type.Name + ".asset";
                    if (matching.Length == 0)
                        findings.Error("Missing core program asset for " + type.FullName + " at '" + expected + "'; run PrepareProgramAssetsBatch.");
                    else if (matching.Length == 1 && AssetDatabase.GetAssetPath(matching[0]) != expected)
                        findings.Error("Core program asset for " + type.FullName + " must be at '" + expected + "', found '" + AssetDatabase.GetAssetPath(matching[0]) + "'.");
                }
                else
                {
                    foreach (var asset in matching)
                    {
                        var path = AssetDatabase.GetAssetPath(asset);
                        if (!path.StartsWith(PocketGameTerminalBuilder.GeneratedRoot + "/", StringComparison.Ordinal))
                            findings.Warning("Game program asset for " + type.FullName + " is outside GeneratedRoot: '" + path + "'.");
                    }
                }
            }

            foreach (var behaviour in behaviours)
            {
                Type type;
                if (!behaviourTypes.TryGetValue(behaviour, out type)) continue;
                var program = behaviour.programSource as UdonSharpProgramAsset;
                var path = program == null ? "<missing program asset>" : AssetDatabase.GetAssetPath(program);
                if (CoreTypes.Contains(type))
                {
                    var expected = PocketGameTerminalBuilder.GeneratedRoot + "/" + type.Name + ".asset";
                    if (path != expected)
                        findings.Error(ObjectContext(behaviour, type, path) + " references a stale core program asset; expected '" + expected + "'.");
                }

                CheckSyncMode(behaviour, type, path, gameEvents.Contains(behaviour), findings);
            }

            foreach (var pool in validPools)
                if (pool != null) CheckOwnershipAndUi(pool, behaviours, behaviourTypes, buttons, findings);
            return findings.Finish();
        }

        private static void AddSubtree(GameObject root, HashSet<UdonBehaviour> behaviours, HashSet<Button> buttons)
        {
            foreach (var behaviour in root.GetComponentsInChildren<UdonBehaviour>(true)) behaviours.Add(behaviour);
            foreach (var button in root.GetComponentsInChildren<Button>(true)) buttons.Add(button);
        }

        private static void CheckSyncMode(UdonBehaviour behaviour, Type type, string assetPath, bool isGameEvents, Findings findings)
        {
            var attribute = Attribute.GetCustomAttribute(type, typeof(UdonBehaviourSyncModeAttribute), true) as UdonBehaviourSyncModeAttribute;
            if (attribute == null)
            {
                if (isGameEvents)
                    findings.Warning(ObjectContext(behaviour, type, assetPath) + " gameEvents class has no sync-mode attribute; consider [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)].");
                return;
            }
            // UdonSharp adapts NoVariableSync behaviours to their GameObject's sync mode, so only fixed modes are compared.
            if (attribute.behaviourSyncMode == BehaviourSyncMode.Any || attribute.behaviourSyncMode == BehaviourSyncMode.NoVariableSync) return;
            var expected = attribute.behaviourSyncMode == BehaviourSyncMode.None
                ? Networking.SyncType.None
                : attribute.behaviourSyncMode == BehaviourSyncMode.Continuous ? Networking.SyncType.Continuous : Networking.SyncType.Manual;
            if (behaviour.SyncMethod != expected)
                findings.Error(ObjectContext(behaviour, type, assetPath) + " has sync mode " + behaviour.SyncMethod + " but its class requires " + expected + "; re-run the installer/CopyToUdon.");
        }

        private static void CheckOwnershipAndUi(PocketGameTerminalPool pool, HashSet<UdonBehaviour> behaviours,
            Dictionary<UdonBehaviour, Type> behaviourTypes, HashSet<Button> buttons, Findings findings)
        {
            var roots = pool.terminalRoots ?? new GameObject[0];
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                var session = pool.terminalSessions != null && i < pool.terminalSessions.Length ? pool.terminalSessions[i] : null;
                if (root == null)
                {
                    findings.Error("Slot " + i + " has no terminal root (pool '" + Path(pool.transform) + "').");
                    continue;
                }
                if (pool.transform == root.transform || pool.transform.IsChildOf(root.transform))
                    findings.Error("Slot " + i + " pool object " + DescribeObject(pool.gameObject, behaviours, behaviourTypes, typeof(PocketGameTerminalPool)) + " is inside terminal root " + DescribeObject(root, behaviours, behaviourTypes, typeof(PocketGameInputRelay)) + ".");
                if (session == null)
                {
                    findings.Error("Slot " + i + " terminal root '" + Path(root.transform) + "' has no session object.");
                    continue;
                }
                var game = session.gameEvents;
                var sessionObject = session.gameObject;
                var gameObject = game == null ? null : game.gameObject;
                if (sessionObject == root || sessionObject.transform.IsChildOf(root.transform) == false)
                    findings.Error("Slot " + i + " session object " + DescribeObject(sessionObject, behaviours, behaviourTypes, typeof(PocketGameTerminalSession)) + " must be inside terminal root " + DescribeObject(root, behaviours, behaviourTypes, typeof(PocketGameInputRelay)) + ".");
                if (gameObject == null)
                    findings.Error("Slot " + i + " terminal root " + DescribeObject(root, behaviours, behaviourTypes, typeof(PocketGameInputRelay)) + " has no gameEvents object.");
                else if (!gameObject.transform.IsChildOf(root.transform))
                    findings.Error("Slot " + i + " game object " + DescribeObject(gameObject, behaviours, behaviourTypes) + " must be inside terminal root " + DescribeObject(root, behaviours, behaviourTypes, typeof(PocketGameInputRelay)) + ".");
                if (sessionObject == root || (gameObject != null && (gameObject == root || gameObject == sessionObject)))
                    findings.Error("Slot " + i + " requires three distinct ownership objects: root " + DescribeObject(root, behaviours, behaviourTypes, typeof(PocketGameInputRelay)) + ", session " + DescribeObject(sessionObject, behaviours, behaviourTypes, typeof(PocketGameTerminalSession)) + ", game " + (gameObject == null ? "<missing>" : DescribeObject(gameObject, behaviours, behaviourTypes)) + ".");

                CheckUi(pool, i, root, session, behaviours, behaviourTypes, buttons, findings);
                if (game != null)
                {
                    foreach (var other in gameObject.GetComponents<UdonBehaviour>())
                        if (other != game && other.SyncMethod != Networking.SyncType.None && game.SyncMethod != Networking.SyncType.None && other.SyncMethod != game.SyncMethod)
                            findings.Error(ObjectContext(game, behaviourTypes.ContainsKey(game) ? behaviourTypes[game] : null, AssetPath(game)) + " shares its GameObject with a behaviour using a different non-None sync mode.");
                }
            }
        }

        private static void CheckUi(PocketGameTerminalPool pool, int slot, GameObject root, PocketGameTerminalSession session,
            HashSet<UdonBehaviour> behaviours, Dictionary<UdonBehaviour, Type> behaviourTypes, HashSet<Button> buttons, Findings findings)
        {
            var ui = session.ui;
            if (ui == null) return;
            var uiBehaviour = FindBacking(behaviours, behaviourTypes, ui);
            var uiType = typeof(PocketGameUi);
            var uiPath = uiBehaviour == null ? "<unknown>" : AssetPath(uiBehaviour);
            if (ui.terminalSession == null)
                findings.Warning("Slot " + slot + " UI at '" + Path(ui.transform) + "' has no terminalSession reference (asset '" + uiPath + "').");
            else if (ui.terminalSession != session)
                findings.Error("Slot " + slot + " UI at '" + Path(ui.transform) + "' (type " + uiType.FullName + ", asset '" + uiPath + "') references a different terminal session at '" + Path(ui.terminalSession.transform) + "' (type " + typeof(PocketGameTerminalSession).FullName + ", asset '" + PocketGameTerminalBuilder.GeneratedRoot + "/PocketGameTerminalSession.asset').");

            CheckModal(ui.confirmPanel, "confirmPanel", ui, root, session, uiBehaviour, uiType, uiPath, slot, findings);
            CheckModal(ui.inputModal, "inputModal", ui, root, session, uiBehaviour, uiType, uiPath, slot, findings);
            if (ui.confirmPanel != null && ui.confirmPanel.activeSelf)
                findings.Warning("Slot " + slot + " confirmPanel '" + Path(ui.confirmPanel.transform) + "' is active in the saved scene (asset '" + uiPath + "').");
            if (ui.inputModal != null && ui.inputModal.activeSelf)
                findings.Warning("Slot " + slot + " inputModal '" + Path(ui.inputModal.transform) + "' is active in the saved scene (asset '" + uiPath + "').");

            foreach (var button in buttons)
            {
                if (!button.transform.IsChildOf(root.transform) && button.gameObject != root) continue;
                var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null) continue;
                for (var i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    if (call.FindPropertyRelative("m_Target").objectReferenceValue != uiBehaviour) continue;
                    var eventName = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                    var context = "Button '" + Path(button.transform) + "' calls " + eventName + " on UI '" + Path(ui.transform) + "' (asset '" + uiPath + "').";
                    if (eventName == "ShowConfirm" && ui.confirmPanel == null) findings.Error("Slot " + slot + " " + context + " confirmPanel is null.");
                    if (eventName == "OpenInputModal" && ui.inputModal == null) findings.Error("Slot " + slot + " " + context + " inputModal is null.");
                }
            }
        }

        private static void CheckModal(GameObject modal, string field, PocketGameUi ui, GameObject root, PocketGameTerminalSession session,
            UdonBehaviour uiBehaviour, Type uiType, string uiPath, int slot, Findings findings)
        {
            if (modal == null) return;
            var owner = uiBehaviour == null ? "<unknown>" : Path(uiBehaviour.transform);
            if (modal == root || !modal.transform.IsChildOf(root.transform))
                findings.Error("Slot " + slot + " " + field + " '" + Path(modal.transform) + "' on UI '" + Path(ui.transform) + "' must be inside terminal root '" + Path(root.transform) + "' (type " + uiType.FullName + ", asset '" + uiPath + "', owner '" + owner + "').");
            var protectedObjects = new[] { ui.mainScreen, ui.scalableRoot == null ? null : ui.scalableRoot.gameObject, ui.overlayDrawer, ui.externalDrawer, ui.helpPanel, ui.settingsPanel };
            foreach (var target in protectedObjects)
                if (target != null && (target == modal || target.transform.IsChildOf(modal.transform)))
                    findings.Error("Slot " + slot + " " + field + " '" + Path(modal.transform) + "' is the same as or an ancestor of protected UI object '" + Path(target.transform) + "' (UI type " + uiType.FullName + ", asset '" + uiPath + "', owner '" + owner + "').");
        }

        private static UdonBehaviour FindBacking(HashSet<UdonBehaviour> behaviours, Dictionary<UdonBehaviour, Type> types, PocketGameUi ui)
        {
            foreach (var behaviour in behaviours)
            {
                Type type;
                if (types.TryGetValue(behaviour, out type) && type == typeof(PocketGameUi) && behaviour.gameObject == ui.gameObject) return behaviour;
            }
            return null;
        }

        private static string AssetPath(UdonBehaviour behaviour)
        {
            var source = behaviour == null ? null : behaviour.programSource;
            return source == null ? "<missing program asset>" : AssetDatabase.GetAssetPath(source);
        }

        private static string DescribeObject(GameObject target, HashSet<UdonBehaviour> behaviours,
            Dictionary<UdonBehaviour, Type> types, Type fallbackType = null)
        {
            if (target == null) return "<missing>";
            foreach (var behaviour in behaviours)
            {
                if (behaviour.gameObject != target) continue;
                Type type;
                if (types.TryGetValue(behaviour, out type))
                    return "'" + Path(target.transform) + "' (type " + type.FullName + ", asset '" + AssetPath(behaviour) + "')";
            }
            var typeName = fallbackType == null ? "unknown" : fallbackType.FullName;
            var assetPath = fallbackType != null && CoreTypes.Contains(fallbackType)
                ? PocketGameTerminalBuilder.GeneratedRoot + "/" + fallbackType.Name + ".asset"
                : "<unknown>";
            return "'" + Path(target.transform) + "' (type " + typeName + ", asset '" + assetPath + "')";
        }

        private static string ObjectContext(UdonBehaviour behaviour, Type type, string assetPath)
        {
            return "Behaviour at '" + Path(behaviour.transform) + "'" + (type == null ? "" : " of type " + type.FullName) + " (asset '" + assetPath + "')";
        }

        private static string Path(Transform transform)
        {
            if (transform == null) return "<missing>";
            var parts = new Stack<string>();
            while (transform != null) { parts.Push(transform.name); transform = transform.parent; }
            return string.Join("/", parts.ToArray());
        }
    }
}
