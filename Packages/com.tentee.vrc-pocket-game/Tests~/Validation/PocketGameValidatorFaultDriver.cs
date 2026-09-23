#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;
using VrcPocketGame;
using VrcPocketGame.Editor;
using VrcPocketGame.Sample;

public static class PocketGameValidatorFaultDriver
{
    public static void RunBatch()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PocketGameInstaller.InstallIntoCurrentScene();
            PocketGameSceneValidator.ValidateAll();

            var counterPath = PocketGameTerminalBuilder.GeneratedRoot + "/PocketCounterGame.asset";
            var duplicatePath = PocketGameTerminalBuilder.GeneratedRoot + "/PocketCounterGame.ValidatorDuplicate.asset";
            AssetDatabase.DeleteAsset(duplicatePath);
            Check(AssetDatabase.CopyAsset(counterPath, duplicatePath), "counter duplicate asset created");
            var duplicateMessage = CaptureValidationFailure();
            Check(duplicateMessage.Contains(counterPath) && duplicateMessage.Contains(duplicatePath) && duplicateMessage.Contains(typeof(PocketCounterGame).FullName), "duplicate reports both paths and type");
            AssetDatabase.DeleteAsset(duplicatePath);

            var pool = UnityEngine.Object.FindObjectOfType<PocketGameTerminalPool>(true);
            var session = pool.terminalSessions[0];
            var uiProxy = session.ui;
            var uiBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(uiProxy);
            var sourcePath = PocketGameTerminalBuilder.GeneratedRoot + "/PocketGameUi.asset";
            var deletedPath = "Assets/PocketGameUi.ValidatorDeleted.asset";
            AssetDatabase.DeleteAsset(deletedPath);
            Check(AssetDatabase.CopyAsset(sourcePath, deletedPath), "temporary UI program asset created");
            var temporaryProgram = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(deletedPath);
            var serialized = new SerializedObject(uiBehaviour);
            var programSource = serialized.FindProperty("programSource");
            programSource.objectReferenceValue = temporaryProgram;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.DeleteAsset(deletedPath);
            var deletedMessage = CaptureValidationFailure();
            Check(deletedMessage.Contains("Counter terminal 0/Terminal session") && deletedMessage.Contains("deleted"), "deleted asset reports hierarchy and deleted state");
            PocketGameInstaller.InstallIntoCurrentScene();

            pool = UnityEngine.Object.FindObjectOfType<PocketGameTerminalPool>(true);
            uiProxy = pool.terminalSessions[0].ui;
            uiProxy.confirmPanel = null;
            UdonSharpEditorUtility.CopyProxyToUdon(uiProxy);
            var modalMessage = CaptureValidationFailure();
            Check(modalMessage.Contains("ShowConfirm") && modalMessage.Contains("confirmPanel is null"), "missing confirm panel reports ShowConfirm call");
            PocketGameInstaller.InstallIntoCurrentScene();

            Check(PocketGameProgramAssetValidator.DeclaresOwnershipGuard(typeof(PocketCounterGame)) &&
                !PocketGameProgramAssetValidator.DeclaresOwnershipGuard(typeof(PocketGameUi)), "ownership guard type check");

            pool = UnityEngine.Object.FindObjectOfType<PocketGameTerminalPool>(true);
            var gameProxy = UdonSharpEditorUtility.GetProxyBehaviour(pool.terminalSessions[0].gameEvents) as PocketGameBehaviour;
            gameProxy.terminalSession = null;
            UdonSharpEditorUtility.CopyProxyToUdon(gameProxy);
            var wiringMessage = CaptureValidationFailure();
            Check(wiringMessage.Contains("terminalSession") && wiringMessage.Contains("Slot 0"), "missing base terminalSession reports slot wiring");
            PocketGameInstaller.InstallIntoCurrentScene();

            Debug.Log("PocketGameValidatorFaultDriver PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static string CaptureValidationFailure()
    {
        try { PocketGameSceneValidator.ValidateAll(); }
        catch (InvalidOperationException exception)
        {
            Debug.Log("PocketGameValidatorFaultDriver expected failure:\n" + exception.Message);
            return exception.Message;
        }
        throw new InvalidOperationException("Expected scene validation to fail.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Fault driver assertion failed: " + message);
    }
}
#endif
