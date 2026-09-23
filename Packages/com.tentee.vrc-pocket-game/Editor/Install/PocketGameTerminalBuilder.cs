using System;
using System.Collections.Generic;
using TMPro;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;

namespace VrcPocketGame.Editor
{
    /// <summary>Physical dimensions use metres; screen and drawer sizes use canvas pixels.</summary>
    public sealed class PocketGameTerminalProfile
    {
        /// <summary>Main screen size in canvas pixels.</summary>
        public Vector2 ScreenSize = new Vector2(800, 520);
        /// <summary>Uniform terminal root scale.</summary>
        public float RootScale = 1f;
        /// <summary>Metres per canvas pixel for the main and default external canvas.</summary>
        public float ScreenPixelScale = .001f;
        /// <summary>Main canvas local position in metres.</summary>
        public Vector3 ScreenOffset = new Vector3(0, 0, -.02f);
        /// <summary>Null leaves the Rigidbody component default untouched.</summary>
        public float? Mass;
        /// <summary>Null leaves the Rigidbody component default untouched.</summary>
        public float? Drag;
        /// <summary>Null leaves the Rigidbody component default untouched.</summary>
        public float? AngularDrag;
        /// <summary>Null leaves the VRCPickup component default untouched.</summary>
        public float? PickupProximity;
        /// <summary>Null leaves the VRCPickup component default untouched.</summary>
        public VRC_Pickup.PickupOrientation? PickupOrientation;
        /// <summary>Null leaves the VRCPickup component default untouched.</summary>
        public VRC_Pickup.AutoHoldMode? PickupAutoHold;
        /// <summary>Root grip collider size in metres.</summary>
        public Vector3 GripSize = new Vector3(.26f, .045f, .025f);
        /// <summary>Null derives the grip center from screen offset and size, in metres.</summary>
        public Vector3? GripCenter;
        /// <summary>Null uses a 300-pixel-wide external canvas with the screen height.</summary>
        public Vector2? ExternalCanvasSize;
        /// <summary>Null derives the external canvas position from screen and drawer sizes, in metres.</summary>
        public Vector3? ExternalCanvasOffset;
    }

    /// <summary>References returned to a game's installer. No sample type enters this API.</summary>
    public sealed class PocketGameTerminalParts
    {
        public PocketGameTerminalProfile Profile;
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
            return Create(parent, name, pool, slot, theme, new PocketGameTerminalProfile { ScreenSize = screenSize });
        }

        public static PocketGameTerminalParts Create(Transform parent, string name, PocketGameTerminalPool pool,
            int slot, PocketGameTheme theme, PocketGameTerminalProfile profile)
        {
            profile = profile ?? new PocketGameTerminalProfile();
            var screenSize = profile.ScreenSize;
            var externalSize = profile.ExternalCanvasSize ?? new Vector2(300, screenSize.y);
            if (!(screenSize.x > 0) || !(screenSize.y > 0)) throw new ArgumentException("ScreenSize components must be greater than zero.", nameof(profile));
            if (!(profile.RootScale > 0)) throw new ArgumentException("RootScale must be greater than zero.", nameof(profile));
            if (!(profile.ScreenPixelScale > 0)) throw new ArgumentException("ScreenPixelScale must be greater than zero.", nameof(profile));
            if (!(externalSize.x > 0) || !(externalSize.y > 0)) throw new ArgumentException("Resolved external canvas size components must be greater than zero.", nameof(profile));
            if (theme.Font == null) throw new InvalidOperationException("Import TMP Essential Resources, then assign a font.");
            var parts = new PocketGameTerminalParts();
            parts.Profile = profile;
            parts.Root = new GameObject(name);
            parts.Root.transform.SetParent(parent, false);
            parts.Root.transform.localScale = Vector3.one * profile.RootScale;
            var body = parts.Root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (profile.Mass.HasValue) body.mass = profile.Mass.Value;
            if (profile.Drag.HasValue) body.drag = profile.Drag.Value;
            if (profile.AngularDrag.HasValue) body.angularDrag = profile.AngularDrag.Value;
            var grip = parts.Root.AddComponent<BoxCollider>();
            grip.isTrigger = true;
            grip.size = profile.GripSize;
            grip.center = profile.GripCenter ?? new Vector3(profile.ScreenOffset.x,
                profile.ScreenOffset.y - screenSize.y * profile.ScreenPixelScale / 2 - .04f, 0);
            var sync = parts.Root.AddComponent<VRCObjectSync>();
            sync.AllowCollisionOwnershipTransfer = false;
            var pickup = parts.Root.AddComponent<VRCPickup>();
            pickup.DisallowTheft = true;
            pickup.pickupable = false;
            if (profile.PickupProximity.HasValue) pickup.proximity = profile.PickupProximity.Value;
            if (profile.PickupOrientation.HasValue) pickup.orientation = profile.PickupOrientation.Value;
            if (profile.PickupAutoHold.HasValue) pickup.AutoHold = profile.PickupAutoHold.Value;
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
            parts.MainCanvas = PocketGameUiBuilder.Canvas(parts.Root.transform, "Main screen", screenSize,
                profile.ScreenOffset, profile.ScreenPixelScale);
            PocketGameUiBuilder.Panel(parts.MainCanvas.transform, "Background", screenSize, Vector2.zero, theme.Background);
            parts.HeaderSlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Header", new Vector2(screenSize.x - 48, 72), new Vector2(0, screenSize.y / 2 - 48));
            parts.ContentSlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Content", new Vector2(screenSize.x - 48, screenSize.y - 220), new Vector2(0, 12));
            parts.ActionSlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Actions", new Vector2(screenSize.x - 48, 112), new Vector2(0, -screenSize.y / 2 + 68));
            parts.OverlaySlot = PocketGameUiBuilder.Slot(parts.MainCanvas.transform, "Overlays", screenSize, Vector2.zero);
            var externalOffset = profile.ExternalCanvasOffset ?? new Vector3(
                profile.ScreenOffset.x + screenSize.x * profile.ScreenPixelScale / 2 + externalSize.x * profile.ScreenPixelScale / 2 + .02f,
                profile.ScreenOffset.y, -.025f);
            parts.ExternalCanvas = CreateExternalCanvas(parts, "External drawer", externalSize, externalOffset);
            parts.Ui = sessionRoot.AddUdonSharpComponent<PocketGameUi>();
            parts.Ui.mainScreen = parts.MainCanvas.gameObject;
            parts.Ui.scalableRoot = parts.Root.transform;
            parts.Session.ui = parts.Ui;
            parts.Ui.terminalSession = parts.Session;
            return parts;
        }

        /// <summary>Creates a game-owned additional drawer; visibility is game-owned and only parts.ExternalCanvas is driven by PocketGameUi.ToggleExternalDrawer.</summary>
        public static Canvas CreateExternalCanvas(PocketGameTerminalParts parts, string name, Vector2 size, Vector3 localPosition)
        {
            if (!(size.x > 0) || !(size.y > 0)) throw new ArgumentException("Canvas size components must be greater than zero.", nameof(size));
            return PocketGameUiBuilder.Canvas(parts.Root.transform, name, size, localPosition, parts.Profile.ScreenPixelScale);
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
