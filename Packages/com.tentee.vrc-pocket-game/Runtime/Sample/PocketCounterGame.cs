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
    public class PocketCounterGame : UdonSharpBehaviour
    {
        public PocketGameTerminalSession terminalSession;
        [UdonSynced] public int sharedCount;
        public TMP_Text countText;
        public TMP_Text ownerText;
        public PocketGameUi ui;
        [Tooltip("Unique reverse-domain-ish prefix owned by this game. Change it when copying the sample.")]
        public string saveNamespace = "tentee.pocket.counter.v1";
        private int _savedCount;

        public override void OnDeserialization() { Refresh(); }
        public override void OnPlayerRestored(VRCPlayerApi player) { if (player != null && player.isLocal) Load(); }
        public void OwnerAddOne()
        {
            if (terminalSession == null || !terminalSession.CanUseGameInput()) return;
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
        public void PocketTerminal_OnClaimed() { if (ui != null) ui.ResetForSession(); if (IsLocalClaimant()) { Load(); sharedCount = _savedCount; RequestSerialization(); } Refresh(); }
        public void PocketTerminal_OnUseDown() { OwnerAddOne(); }
        public void PocketTerminal_OnRecalled() { Refresh(); }
        public void PocketTerminal_OnReleased() { Refresh(); }
        public void PocketTerminal_OnAudienceChanged() { Refresh(); }
        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return terminalSession != null && terminalSession.CanPlayerOwnTerminal(requestingPlayer, requestedOwner);
        }
        public void PocketTerminal_RequestReturn() { if(!IsLocalClaimant())return; Save(); if(terminalSession!=null) terminalSession.PocketTerminal_ApproveReturn(); }
        private bool IsLocalClaimant() { return terminalSession != null && terminalSession.IsLocalClaimant() && terminalSession.IsCurrentSession(); }
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
