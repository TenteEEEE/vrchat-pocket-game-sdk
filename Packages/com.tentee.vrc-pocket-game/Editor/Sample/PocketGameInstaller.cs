using System;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
using VrcPocketGame.Sample;

namespace VrcPocketGame.Editor
{
    /// <summary>Example consumer of the game-neutral terminal and UI builders.</summary>
    public static class PocketGameInstaller
    {
        public const string RootName = "VRC Pocket Game SDK Sample";
        public const string ScenePath = "Assets/VrcPocketGameGenerated/CounterSample.unity";

        [MenuItem("Tools/VRC Pocket Game SDK/Install Counter Sample")]
        public static void InstallIntoCurrentScene()
        {
            PrepareProgramAssetsBatch();
            var theme = new PocketGameTheme { Font = TMP_Settings.defaultFontAsset };
            if (theme.Font == null) throw new InvalidOperationException("Import TMP Essential Resources before installing the sample.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open a scene first.");
            var old = GameObject.Find(RootName);
            var position = old != null ? old.transform.position : Selection.activeTransform != null ? Selection.activeTransform.position : new Vector3(0, 1.2f, 1.5f);
            var rotation = old != null ? old.transform.rotation : Quaternion.identity;
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(RootName);
            root.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(root, "Install Pocket Game");
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(events, "Create UI EventSystem");
            }
            var kiosk = new GameObject("Kiosk");
            kiosk.transform.SetParent(root.transform, false);
            var hit = kiosk.AddComponent<BoxCollider>();
            hit.size = new Vector3(.7f, .32f, .05f);
            var pool = kiosk.AddUdonSharpComponent<PocketGameTerminalPool>();
            pool.pool = kiosk.AddComponent<VRCObjectPool>();
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(pool);
            backing.interactText = "Call / recall your game";
            backing.proximity = 2;
            var kioskCanvas = PocketGameUiBuilder.Canvas(kiosk.transform, "Kiosk screen", new Vector2(660, 280), new Vector3(0, 0, -.035f), .001f, PocketGameUi.WorldUiSortingOrder);
            // The kiosk uses Interact, not uGUI input; its solid collider stays reachable.
            UnityEngine.Object.DestroyImmediate(kioskCanvas.GetComponent<VRCUiShape>());
            UnityEngine.Object.DestroyImmediate(kioskCanvas.GetComponent<BoxCollider>());
            PocketGameUiBuilder.Panel(kioskCanvas.transform, "Background", new Vector2(660, 280), Vector2.zero, theme.Background);
            PocketGameUiBuilder.Text(kioskCanvas.transform, "Title", "POCKET / GAME SDK", new Vector2(600, 60), new Vector2(0, 72), 30, theme);
            PocketGameUiBuilder.Text(kioskCanvas.transform, "Subtitle", "Your own game. A shared world.", new Vector2(600, 40), new Vector2(0, 14), 22, theme, true);
            pool.statusText = PocketGameUiBuilder.Text(kioskCanvas.transform, "Status", "Use to call your terminal", new Vector2(600, 70), new Vector2(0, -65), 21, theme);
            var storage = new GameObject("Terminal pool");
            storage.transform.SetParent(root.transform, false);
            const int count = 2;
            pool.terminalRoots = new GameObject[count];
            pool.terminalSessions = new PocketGameTerminalSession[count];
            pool.claimedPlayerIds = new int[count];
            pool.claimGenerations = new int[count];
            for (var i = 0; i < count; i++)
            {
                var parts = PocketGameTerminalBuilder.Create(storage.transform, "Counter terminal " + i, pool, i, theme, new Vector2(800, 520));
                parts.Root.transform.localPosition = new Vector3(i, -4, 0);
                BuildCounter(parts, theme);
                pool.terminalRoots[i] = parts.Root;
                pool.terminalSessions[i] = parts.Session;
                pool.claimedPlayerIds[i] = -1;
                parts.Root.SetActive(false);
            }
            pool.pool.Pool = pool.terminalRoots;
            PocketGameTerminalBuilder.CopyToUdon(root);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            ValidateCurrentScene();
            Debug.Log("[Pocket Game SDK] Counter sample installed.");
        }

        private static void BuildCounter(PocketGameTerminalParts parts, PocketGameTheme theme)
        {
            var game = parts.GameStateSlot.gameObject.AddUdonSharpComponent<PocketCounterGame>();
            game.terminalSession = parts.Session;
            game.ui = parts.Ui;
            game.saveNamespace = "tentee.pocket.counter.v1";
            parts.Session.gameEvents = UdonSharpEditorUtility.GetBackingUdonBehaviour(game);
            parts.Session.ui = parts.Ui;
            var gameEvents = parts.Session.gameEvents;
            var uiEvents = UdonSharpEditorUtility.GetBackingUdonBehaviour(parts.Ui);
            var sessionEvents = UdonSharpEditorUtility.GetBackingUdonBehaviour(parts.Session);
            PocketGameUiBuilder.Text(parts.HeaderSlot, "Title", "POCKET / COUNTER", new Vector2(470, 54), new Vector2(-135, 0), 27, theme);
            PocketGameUiBuilder.Button(parts.HeaderSlot, "Help", new Vector2(96, 40), new Vector2(190, 0), uiEvents, "ShowHelp", theme);
            PocketGameUiBuilder.Button(parts.HeaderSlot, "Settings", new Vector2(118, 40), new Vector2(309, 0), uiEvents, "ShowSettings", theme);
            PocketGameUiBuilder.Panel(parts.ContentSlot, "Game area", new Vector2(752, 286), Vector2.zero, theme.Panel);
            PocketGameUiBuilder.Text(parts.ContentSlot, "Caption", "A SMALL GAME / A COMPLETE TERMINAL", new Vector2(680, 34), new Vector2(0, 100), 16, theme, true, TextAlignmentOptions.Center);
            game.countText = PocketGameUiBuilder.Text(parts.ContentSlot, "Count", "Count: 0", new Vector2(680, 92), new Vector2(0, 23), 54, theme, false, TextAlignmentOptions.Center);
            game.ownerText = PocketGameUiBuilder.Text(parts.ContentSlot, "Owner", "Call this terminal from the kiosk", new Vector2(680, 38), new Vector2(0, -53), 19, theme, true, TextAlignmentOptions.Center);
            PocketGameUiBuilder.Text(parts.ContentSlot, "Hint", "UI button or Pickup Use to add one", new Vector2(680, 34), new Vector2(0, -103), 17, theme, true, TextAlignmentOptions.Center);
            PocketGameUiBuilder.Button(parts.ActionSlot, "On-screen drawer", new Vector2(225, 52), new Vector2(-258, 24), uiEvents, "ToggleOverlayDrawer", theme);
            PocketGameUiBuilder.Button(parts.ActionSlot, "+1", new Vector2(225, 52), new Vector2(0, 24), gameEvents, "OwnerAddOne", theme, true);
            PocketGameUiBuilder.Button(parts.ActionSlot, "Side drawer", new Vector2(225, 52), new Vector2(258, 24), uiEvents, "ToggleExternalDrawer", theme);
            PocketGameUiBuilder.Text(parts.ActionSlot, "Grip hint", "Grab below the screen  /  Your progress follows you", new Vector2(730, 30), new Vector2(0, -29), 16, theme, true, TextAlignmentOptions.Center);
            var gripCanvas = PocketGameUiBuilder.Canvas(parts.Root.transform, "Grip label", new Vector2(240, 26), new Vector3(0, -.30f, -.022f), .001f, PocketGameUi.TerminalUiSortingOrder);
            UnityEngine.Object.DestroyImmediate(gripCanvas.GetComponent<VRCUiShape>());
            UnityEngine.Object.DestroyImmediate(gripCanvas.GetComponent<BoxCollider>());
            PocketGameUiBuilder.Panel(gripCanvas.transform, "Grip", new Vector2(240, 18), Vector2.zero, theme.Accent);

            var overlay = PocketGameUiBuilder.Panel(parts.OverlaySlot, "On-screen drawer", new Vector2(320, 300), new Vector2(-216, 8), theme.Button, true);
            parts.Ui.overlayDrawer = overlay.gameObject;
            PocketGameUiBuilder.Text(overlay.transform, "Title", "On-screen drawer", new Vector2(284, 48), new Vector2(0, 105), 23, theme);
            PocketGameUiBuilder.Text(overlay.transform, "Body", "This panel covers the game area.\n\nOpening a drawer does not pause the game.", new Vector2(284, 170), new Vector2(0, -1), 20, theme);
            PocketGameUiBuilder.Button(overlay.transform, "Close drawer", new Vector2(272, 42), new Vector2(0, -117), uiEvents, "ToggleOverlayDrawer", theme);
            parts.Ui.externalDrawer = parts.ExternalCanvas.gameObject;
            PocketGameUiBuilder.Panel(parts.ExternalCanvas.transform, "Background", new Vector2(300, 520), Vector2.zero, theme.Panel, true);
            PocketGameUiBuilder.Text(parts.ExternalCanvas.transform, "Title", "Side drawer", new Vector2(252, 56), new Vector2(0, 208), 25, theme);
            PocketGameUiBuilder.Text(parts.ExternalCanvas.transform, "Body", "A separate canvas beside the terminal.\n\nUse this space for inventory, statistics or a catalogue.", new Vector2(252, 274), new Vector2(0, 31), 21, theme);
            PocketGameUiBuilder.Button(parts.ExternalCanvas.transform, "Close drawer", new Vector2(252, 46), new Vector2(0, -212), uiEvents, "ToggleExternalDrawer", theme);

            var help = Modal(parts, "Help", theme);
            parts.Ui.helpPanel = help;
            PocketGameUiBuilder.Text(help.transform, "Title", "HOW TO PLAY", new Vector2(640, 55), new Vector2(0, 175), 29, theme);
            PocketGameUiBuilder.Text(help.transform, "Body", "1. Call a terminal from the kiosk.\n2. Add one with +1 or Pickup Use.\n3. Open either drawer to inspect the layout.\n4. Stow from Settings to save progress.\n\nOther players can watch your count.", new Vector2(640, 280), new Vector2(0, 4), 24, theme);
            PocketGameUiBuilder.Button(help.transform, "Back", new Vector2(190, 46), new Vector2(0, -192), uiEvents, "ClosePanels", theme);
            var settings = Modal(parts, "Settings", theme);
            parts.Ui.settingsPanel = settings;
            PocketGameUiBuilder.Text(settings.transform, "Title", "TERMINAL SETTINGS", new Vector2(640, 60), new Vector2(0, 175), 29, theme);
            PocketGameUiBuilder.Text(settings.transform, "Scale label", "Display size", new Vector2(640, 40), new Vector2(0, 108), 21, theme, true);
            PocketGameUiBuilder.Button(settings.transform, "Small", new Vector2(180, 46), new Vector2(-208, 47), uiEvents, "SetSmallScale", theme);
            PocketGameUiBuilder.Button(settings.transform, "Normal", new Vector2(180, 46), new Vector2(0, 47), uiEvents, "SetNormalScale", theme);
            PocketGameUiBuilder.Button(settings.transform, "Large", new Vector2(180, 46), new Vector2(208, 47), uiEvents, "SetLargeScale", theme);
            PocketGameUiBuilder.Button(settings.transform, "Save & stow", new Vector2(300, 48), new Vector2(0, -35), sessionEvents, "PocketTerminal_RequestReturn", theme, true);
            PocketGameUiBuilder.Button(settings.transform, "Reset counter…", new Vector2(300, 46), new Vector2(0, -103), uiEvents, "ShowConfirm", theme);
            PocketGameUiBuilder.Button(settings.transform, "Back", new Vector2(190, 42), new Vector2(0, -192), uiEvents, "ClosePanels", theme);
            var confirm = Modal(parts, "Confirm reset", theme);
            parts.Ui.confirmPanel = confirm;
            parts.Ui.inputModal = confirm;
            PocketGameUiBuilder.Text(confirm.transform, "Title", "RESET YOUR COUNTER?", new Vector2(640, 60), new Vector2(0, 145), 29, theme);
            PocketGameUiBuilder.Text(confirm.transform, "Body", "Your saved count will become zero.\n\nGame input is blocked until you choose.", new Vector2(640, 200), new Vector2(0, 8), 25, theme);
            PocketGameUiBuilder.Button(confirm.transform, "Cancel", new Vector2(260, 50), new Vector2(-150, -165), uiEvents, "ClosePanels", theme);
            PocketGameUiBuilder.Button(confirm.transform, "Reset", new Vector2(260, 50), new Vector2(150, -165), gameEvents, "ConfirmReset", theme, true);
            parts.Ui.ResetForSession();
        }

        private static GameObject Modal(PocketGameTerminalParts parts, string name, PocketGameTheme theme)
        {
            return PocketGameUiBuilder.Panel(parts.OverlaySlot, name, new Vector2(800, 520), Vector2.zero, theme.Panel, true).gameObject;
        }

        [MenuItem("Tools/VRC Pocket Game SDK/Validate Current Scene")]
        public static void ValidateCurrentScene() { PocketGameSceneValidator.ValidateAll(); }
        public static void PrepareProgramAssetsBatch() { PocketGameTerminalBuilder.PreparePrograms(typeof(PocketCounterGame)); }
        public static void InstallAndValidateBatch()
        {
            InstallIntoCurrentScene();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath)) throw new InvalidOperationException("Could not save sample scene.");
            Debug.Log("[Pocket Game SDK] InstallAndValidateBatch PASS");
        }
    }
}
