using System;
using System.Reflection;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace VrcPocketGame.Editor
{
    /// <summary>Checks contract and wiring, not game rules or network convergence.</summary>
    public static class PocketGameSceneValidator
    {
        private static readonly string[] GameEvents = {
            "PocketTerminal_OnClaimed", "PocketTerminal_OnReleased", "PocketTerminal_OnRecalled",
            "PocketTerminal_OnUseDown", "PocketTerminal_RequestReturn"
        };

        public static void ValidateAll()
        {
            var pools = UnityEngine.Object.FindObjectsOfType<PocketGameTerminalPool>(true);
            var warningCount = PocketGameProgramAssetValidator.Validate(pools);
            Require(pools.Length > 0, "No terminal pool in the scene.");
            foreach (var pool in pools) ValidateStructure(pool);
            Debug.Log("[Pocket Game SDK] Structural validation PASS: " + pools.Length + " pool(s), " + warningCount + " warning(s).");
        }

        public static void Validate(PocketGameTerminalPool pool)
        {
            PocketGameProgramAssetValidator.Validate(new[] { pool });
            ValidateStructure(pool);
        }

        private static void ValidateStructure(PocketGameTerminalPool pool)
        {
            Require(pool.pool != null && pool.pool.gameObject == pool.gameObject, "Pool behaviour and VRCObjectPool must share an owner object.");
            Require(pool.terminalRoots != null && pool.terminalRoots.Length > 0, "Pool must contain terminals.");
            var count = pool.terminalRoots.Length;
            Require(pool.terminalSessions != null && pool.terminalSessions.Length == count, "Session count differs from terminal count.");
            Require(pool.claimedPlayerIds != null && pool.claimedPlayerIds.Length == count && pool.claimGenerations != null && pool.claimGenerations.Length == count, "Claim tables have invalid lengths.");
            Require(pool.pool.Pool != null && pool.pool.Pool.Length == count, "VRCObjectPool slots differ.");
            for (var i = 0; i < count; i++)
            {
                var root = pool.terminalRoots[i];
                var session = pool.terminalSessions[i];
                Require(root != null && session != null && pool.pool.Pool[i] == root, "Missing or mismatched slot " + i);
                for (var j = 0; j < i; j++) Require(pool.terminalRoots[j] != root, "Duplicate terminal root.");
                Require(session.terminalPool == pool && session.slotIndex == i && session.transform.IsChildOf(root.transform), "Session wiring differs at slot " + i);
                Require(session.gameEvents != null && session.gameEvents.transform.IsChildOf(root.transform), "Missing game event target at slot " + i);
                Require(session.gameEvents.gameObject != session.gameObject, "Game and session need separate state objects.");
                Require(root.GetComponent<Rigidbody>() != null && root.GetComponent<VRCObjectSync>() != null, "Terminal root needs Rigidbody and VRCObjectSync.");
                Require(session.pickup != null && session.pickup.gameObject == root && session.pickup.DisallowTheft, "Pickup must be guarded on terminal root.");
                var relay = root.GetComponent<PocketGameInputRelay>();
                Require(relay != null && relay.terminalSession == session, "Root input/ownership relay is not wired.");
                var gameProgram = session.gameEvents.programSource as UdonSharpProgramAsset;
                Require(gameProgram != null && gameProgram.sourceCsScript != null, "Game Udon program is missing.");
                var gameType = gameProgram.sourceCsScript.GetClass();
                foreach (var name in GameEvents) Require(HasEvent(gameType, name), "Game lacks public void " + name + "().");
                Require(session.ui != null, "Session needs the shared UI input gate.");
                ValidateUi(root, session.ui);
            }
        }

        private static void ValidateUi(GameObject root, PocketGameUi ui)
        {
            Require(ui.mainScreen != null && ui.scalableRoot != null, "Screen/scale target is missing.");
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                var shape = canvas.GetComponent<VRCUiShape>();
                if (shape == null) continue; // Noninteractive decoration needs no UI collider.
                var rect = (RectTransform)canvas.transform;
                var collider = canvas.GetComponent<BoxCollider>();
                Require(canvas.renderMode == RenderMode.WorldSpace && canvas.GetComponent<GraphicRaycaster>() != null, "Interactive screen must be a raycastable WorldSpace canvas.");
                Require(collider != null && collider.isTrigger && Mathf.Abs(collider.size.x - rect.rect.width) < .1f && Mathf.Abs(collider.size.y - rect.rect.height) < .1f, "Canvas collider must use matching canvas-pixel dimensions.");
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) Require(!text.raycastTarget && text.font != null, "Text must have a font and must not steal pointer hits: " + text.name);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var canvas = button.GetComponentInParent<Canvas>(true);
                Require(canvas != null && canvas.GetComponent<VRCUiShape>() != null, "Button has no interactive canvas: " + button.name);
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                var canvasRect = (RectTransform)canvas.transform;
                foreach (var corner in corners)
                {
                    var local = canvasRect.InverseTransformPoint(corner);
                    Require(Mathf.Abs(local.x) <= canvasRect.rect.width / 2 + .1f && Mathf.Abs(local.y) <= canvasRect.rect.height / 2 + .1f, "Button lies outside screen collider: " + button.name);
                }
                var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                Require(calls != null && calls.arraySize > 0, "Unwired button: " + button.name);
                for (var i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    var target = call.FindPropertyRelative("m_Target").objectReferenceValue as UdonBehaviour;
                    var eventName = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                    var program = target == null ? null : target.programSource as UdonSharpProgramAsset;
                    Require(program != null && program.sourceCsScript != null && HasEvent(program.sourceCsScript.GetClass(), eventName), "Button has an invalid Udon event: " + button.name + " -> " + eventName);
                }
            }
        }

        private static bool HasEvent(Type type, string name)
        {
            var method = type == null ? null : type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            return method != null && method.ReturnType == typeof(void);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
