using System;
using System.Collections.Generic;
using TMPro;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

namespace VrcPocketGame.Editor
{
    /// <summary>References returned to a game's installer. No sample type enters this API.</summary>
    public sealed class PocketGameTerminalParts
    {
        public GameObject Root;
        public Transform GameStateSlot;
        public PocketGameTerminalSession Session;
        public PocketGameUi Ui;
        public Canvas MainCanvas;
        public RectTransform HeaderSlot;
        public RectTransform ContentSlot;
        public RectTransform ActionSlot;
        public RectTransform OverlaySlot;
        public Canvas ExternalCanvas;
    }

    public static class PocketGameTerminalBuilder
    {
        public const string GeneratedRoot = "Assets/VrcPocketGameGenerated";

        public static void PreparePrograms(params Type[] gameTypes)
        {
            // Asset imports reset UdonSharp's assembly cache. Let an automatic compile
            // finish before creating assets, then import the whole batch together.
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            if (!AssetDatabase.IsValidFolder(GeneratedRoot)) AssetDatabase.CreateFolder("Assets", "VrcPocketGameGenerated");
            var createdPaths = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                EnsureProgram(typeof(PocketGameTerminalPool), createdPaths);
                EnsureProgram(typeof(PocketGameTerminalSession), createdPaths);
                EnsureProgram(typeof(PocketGameInputRelay), createdPaths);
                EnsureProgram(typeof(PocketGameUi), createdPaths);
                foreach (var type in gameTypes) EnsureProgram(type, createdPaths);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var stampedPaths = new List<string>();
            // Fresh assets stay at Unknown until UdonSharp's deferred upgrade pass, which is
            // internal; stamping here lets CopyToUdon run in the same installation pass.
            foreach (var path in createdPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                if (asset != null && asset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion)
                {
                    asset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                    EditorUtility.SetDirty(asset);
                    stampedPaths.Add(AssetDatabase.GetAssetPath(asset));
                }
            }
            if (stampedPaths.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Pocket Game SDK] Marked newly generated program assets as current so installation can finish in one pass:\n" + string.Join("\n", stampedPaths));
            }
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new InvalidOperationException("UdonSharp compilation failed; see Console.");
        }

        private static void EnsureProgram(Type type, List<string> createdPaths)
        {
            MonoScript source = null;
            foreach (var id in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(id));
                if (script != null && script.GetClass() == type) { source = script; break; }
            }
            if (source == null) throw new InvalidOperationException("Cannot locate script " + type.FullName);
            var path = GeneratedRoot + "/" + type.Name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                asset.sourceCsScript = source;
                AssetDatabase.CreateAsset(asset, path);
                createdPaths.Add(path);
            }
            if (asset.sourceCsScript != source) { asset.sourceCsScript = source; EditorUtility.SetDirty(asset); }
        }

        public static PocketGameTerminalParts Create(Transform parent, string name, PocketGameTerminalPool pool,
            int slot, PocketGameTheme theme, Vector2 screenSize)
        {
            if (theme.Font == null) throw new InvalidOperationException("Import TMP Essential Resources, then assign a font.");
            var parts = new PocketGameTerminalParts();
            parts.Root = new GameObject(name);
            parts.Root.transform.SetParent(parent, false);
            var body = parts.Root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var grip = parts.Root.AddComponent<BoxCollider>();
            grip.isTrigger = true;
            grip.size = new Vector3(.26f, .045f, .025f);
            grip.center = new Vector3(0, -screenSize.y * .0005f - .04f, 0);
            var sync = parts.Root.AddComponent<VRCObjectSync>();
            sync.AllowCollisionOwnershipTransfer = false;
            var pickup = parts.Root.AddComponent<VRCPickup>();
            pickup.DisallowTheft = true;
            pickup.pickupable = false;
            var sessionRoot = new GameObject("Terminal session");
            sessionRoot.transform.SetParent(parts.Root.transform, false);
            parts.Session = sessionRoot.AddUdonSharpComponent<PocketGameTerminalSession>();
            parts.Session.terminalPool = pool;
            parts.Session.slotIndex = slot;
            parts.Session.pickup = pickup;
            var relay = parts.Root.AddUdonSharpComponent<PocketGameInputRelay>();
            relay.terminalSession = parts.Session;
            parts.GameStateSlot = new GameObject("Game state").transform;
            parts.GameStateSlot.SetParent(parts.Root.transform, false);
            parts.MainCanvas = PocketGameUiBuilder.Canvas(parts.Root.transform, "Main screen", screenSize, new Vector3(0, 0, -.02f));
            PocketGameUiBuilder.Panel(parts.MainCanvas.transform, "Background", screenSize, Vector2.zero, theme.Background);
            parts.HeaderSlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Header", new Vector2(screenSize.x - 48, 72), new Vector2(0, screenSize.y / 2 - 48));
            parts.ContentSlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Content", new Vector2(screenSize.x - 48, screenSize.y - 220), new Vector2(0, 12));
            parts.ActionSlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Actions", new Vector2(screenSize.x - 48, 112), new Vector2(0, -screenSize.y / 2 + 68));
            parts.OverlaySlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Overlays", screenSize, Vector2.zero);
            parts.ExternalCanvas = PocketGameUiBuilder.Canvas(parts.Root.transform, "External drawer", new Vector2(300, screenSize.y), new Vector3(screenSize.x * .0005f + .17f, 0, -.025f));
            parts.Ui = sessionRoot.AddUdonSharpComponent<PocketGameUi>();
            parts.Ui.mainScreen = parts.MainCanvas.gameObject;
            parts.Ui.scalableRoot = parts.Root.transform;
            parts.Session.ui = parts.Ui;
            parts.Ui.terminalSession = parts.Session;
            return parts;
        }

        public static void CopyToUdon(GameObject root)
        {
            var notReady = new List<string>();
            foreach (var behaviour in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
            {
                var asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviour);
                var scriptOutdated = asset == null || asset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion;
                var compiledOutdated = asset != null && asset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion;
                if (!scriptOutdated && !compiledOutdated) continue;
                var assetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : "<missing program asset>";
                var versions = scriptOutdated && compiledOutdated ? "script and compiled versions are outdated"
                    : scriptOutdated ? "script version is outdated" : "compiled version is outdated";
                var reason = asset == null ? "no program asset is assigned" : versions;
                notReady.Add(behaviour.GetType().Name + " on '" + behaviour.gameObject.name + "' at '" + assetPath + "': " + reason);
            }
            if (notReady.Count > 0)
                throw new InvalidOperationException("Pocket Game SDK: Udon program assets are not ready.\n" + string.Join("\n", notReady) +
                    "\nLet Unity finish compiling, then run the installer again.");
            foreach (var behaviour in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
                UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        }
    }
}
