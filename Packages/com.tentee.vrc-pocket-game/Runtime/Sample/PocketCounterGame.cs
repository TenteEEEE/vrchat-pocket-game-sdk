using UdonSharp;
using TMPro;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace VrcPocketGame.Sample
{
    /// <summary>Minimal sample: claimant increments a persistent counter; everyone sees its synced value.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PocketCounterGame : PocketGameBehaviour
    {
        [UdonSynced] public int sharedCount;
        public TMP_Text countText;
        public TMP_Text ownerText;
        [Tooltip("Unique reverse-domain-ish prefix owned by this game. Change it when copying the sample.")]
        public string saveNamespace = "tentee.pocket.counter.v1";
        private int _savedCount;

        public override void OnDeserialization() { Refresh(); }
        public override void OnPlayerRestored(VRCPlayerApi player) { if (player != null && player.isLocal) Load(); }
        public void OwnerAddOne()
        {
            if (!CanUseGameInput()) return;
            if (sharedCount < int.MaxValue) sharedCount++;
            Save();
            RequestSerialization();
            Refresh();
        }
        public void ConfirmReset()
        {
            if (!IsLocalClaimant() || ui == null || !ui.IsConfirmOpen()) return;
            sharedCount = 0;
            Save();
            RequestSerialization();
            ui.ClosePanels();
            Refresh();
        }
        protected override void OnTerminalClaimed() { if (ui != null) ui.ResetForSession(); if (IsLocalClaimant()) { Load(); sharedCount = _savedCount; RequestSerialization(); } Refresh(); }
        protected override void OnTerminalUseDown() { OwnerAddOne(); }
        protected override void OnTerminalRecalled() { Refresh(); }
        protected override void OnTerminalReleased() { Refresh(); }
        protected override void OnTerminalAudienceChanged() { Refresh(); }
        protected override bool OnTerminalReturnRequested() { Save(); return true; }
        protected override void OnTerminalReturnStarted() { if (ownerText != null) ownerText.text = "Saving and stowing…"; }
        protected override void OnTerminalReturnSucceeded() { Refresh(); }
        protected override void OnTerminalReturnFailed() { if (ownerText != null) ownerText.text = "Could not stow; try again"; }
        protected override void OnTerminalReturnCancelled() { Refresh(); }
        private void Load()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !PlayerData.HasKey(local, Key("schema")) || PlayerData.GetInt(local, Key("schema")) != 1) { _savedCount = 0; return; }
            _savedCount = PlayerData.GetInt(local, Key("count"));
        }
        private void Save()
        {
            if (!IsLocalClaimant() || terminalSession.terminalPool == null || !terminalSession.terminalPool.IsLocalPlayerDataRestored()) return;
            PlayerData.SetInt(Key("schema"), 1);
            PlayerData.SetInt(Key("count"), sharedCount);
        }
        private string Key(string field) { return saveNamespace + "." + field; }
        private void Refresh() { if (countText != null) countText.text = "Count: " + sharedCount; if (ownerText != null) ownerText.text = IsLocalClaimant() ? "You own this game" : "Spectating"; }
    }
}
