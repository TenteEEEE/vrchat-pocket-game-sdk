using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

namespace VrcPocketGame
{
    /// <summary>SDK-owned, small session mirror. Games are separate Udon behaviours referenced through one event target.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PocketGameTerminalSession : UdonSharpBehaviour
    {
        public PocketGameTerminalPool terminalPool;
        public int slotIndex;
        public UdonBehaviour gameEvents;
        public PocketGameUi ui;
        public VRC_Pickup pickup;
        [UdonSynced] public int assignedPlayerId = -1;
        [UdonSynced] public int sessionGeneration;
        public GameObject[] claimantOnlyObjects;
        public GameObject[] spectatorObjects;
        [HideInInspector] public bool returnApproved;
        private int _acceptedGeneration = -1;
        private int _acceptedPlayerId = -1;
        private bool _returnPending;
        private bool _returnResultPending;
        private bool _reconcileScheduled;

        public bool HasAcceptedSession()
        {
            return assignedPlayerId >= 0 && _acceptedPlayerId == assignedPlayerId && _acceptedGeneration == sessionGeneration;
        }

        public bool CanUseGameInput()
        {
            return IsLocalClaimant() && HasAcceptedSession() && !_returnPending && (ui == null || !ui.BlocksGameInput());
        }

        public bool IsLocalClaimant()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            return player != null && player.playerId == assignedPlayerId &&
                Networking.IsOwner(gameObject) && terminalPool != null &&
                IsCurrentSession() &&
                terminalPool.IsTerminalRootOwnedByLocal(slotIndex) &&
                (gameEvents == null || Networking.IsOwner(gameEvents.gameObject));
        }
        public bool IsCurrentSession()
        {
            return assignedPlayerId >= 0 && terminalPool != null &&
                terminalPool.GetClaimedPlayerId(slotIndex) == assignedPlayerId &&
                terminalPool.GetClaimGeneration(slotIndex) == sessionGeneration;
        }
        public bool CanPlayerOwnTerminal(VRCPlayerApi requester, VRCPlayerApi player)
        {
            if (player == null || terminalPool == null) return false;
            int claim = terminalPool.GetClaimedPlayerId(slotIndex);
            if (claim == player.playerId) return true;
            return (claim < 0 || VRCPlayerApi.GetPlayerById(claim) == null) && requester != null &&
                Networking.IsOwner(requester, terminalPool.gameObject);
        }
        public void PocketTerminal_OnClaimed()
        {
            if (!IsCurrentSession() || !IsLocalClaimant() || !terminalPool.IsLocalPlayerDataRestored()) return;
            if (_acceptedGeneration == sessionGeneration && _acceptedPlayerId == assignedPlayerId) return;
            _acceptedGeneration = sessionGeneration;
            _acceptedPlayerId = assignedPlayerId;
            returnApproved = false;
            _returnResultPending = false;
            _returnPending = false;
            if (ui != null) ui.ResetForSession();
            SetAudience();
            RequestSerialization();
            Send("PocketTerminal_OnClaimed");
        }
        public void PocketTerminal_OnReleased()
        {
            if (terminalPool == null || terminalPool.GetClaimedPlayerId(slotIndex) >= 0) return;
            if (Networking.IsOwner(gameObject)) assignedPlayerId = -1;
            _returnPending = false;
            _acceptedPlayerId = -1;
            returnApproved = false;
            if (ui != null) ui.ResetForSession();
            SetAudience();
            if (Networking.IsOwner(gameObject)) RequestSerialization();
            Send("PocketTerminal_OnReleased");
        }
        public override void OnDeserialization() { SetAudience(); Send("PocketTerminal_OnAudienceChanged"); }
        private void OnEnable()
        {
            _returnResultPending = false;
            _returnPending = false;
            returnApproved = false;
            if (ui != null) ui.ResetForSession();
            SetAudience();
            ScheduleReconcile();
        }
        public override void OnOwnershipTransferred(VRCPlayerApi player) { SetAudience(); ScheduleReconcile(); }
        public void PocketTerminal_OnRecalled() { if(IsLocalClaimant()) Send("PocketTerminal_OnRecalled"); }
        public void PocketTerminal_OnUseDown() { if(CanUseGameInput()) Send("PocketTerminal_OnUseDown"); }
        public void PocketTerminal_RequestReturn()
        {
            if(!CanUseGameInput()) return;
            returnApproved=false; Send("PocketTerminal_RequestReturn");
            if(returnApproved && terminalPool!=null)
            {
                _returnPending = true;
                _returnResultPending = true;
                Send("PocketTerminal_OnReturnStarted");
                terminalPool.RequestReturn(slotIndex);
            }
        }
        public void PocketTerminal_ApproveReturn() { if(IsLocalClaimant()) returnApproved=true; }
        public void CancelReturn() { _returnPending = false; returnApproved = false; }
        public void ReportReturnSucceeded() { if(!_returnResultPending) return; _returnResultPending=false; Send("PocketTerminal_OnReturnSucceeded"); }
        public void ReportReturnFailed() { if(!_returnResultPending) return; _returnResultPending=false; CancelReturn(); Send("PocketTerminal_OnReturnFailed"); }
        public void ReportReturnCancelled() { if(!_returnResultPending) return; _returnResultPending=false; CancelReturn(); Send("PocketTerminal_OnReturnCancelled"); }
        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return CanPlayerOwnTerminal(requestingPlayer, requestedOwner);
        }
        private void ScheduleReconcile()
        {
            if (_reconcileScheduled) return;
            _reconcileScheduled = true;
            SendCustomEventDelayedSeconds(nameof(Reconcile), 2f);
        }
        public void Reconcile()
        {
            _reconcileScheduled = false;
            if (!gameObject.activeInHierarchy || terminalPool == null) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local != null && assignedPlayerId == local.playerId && IsCurrentSession() && !_returnPending)
            {
                terminalPool.RestoreTerminalOwnership(slotIndex);
                PocketTerminal_OnClaimed();
            }
            SetAudience();
            ScheduleReconcile();
        }
        private void Send(string name) { if(gameEvents!=null) gameEvents.SendCustomEvent(name); }
        private void SetAudience()
        {
            bool claimant = IsLocalClaimant();
            if (pickup != null) pickup.pickupable = claimant;
            SetActive(claimantOnlyObjects,claimant);
            SetActive(spectatorObjects,!claimant);
        }
        private void SetActive(GameObject[] objects,bool active) { if(objects==null)return;for(int i=0;i<objects.Length;i++)if(objects[i]!=null)objects[i].SetActive(active); }
    }
}
