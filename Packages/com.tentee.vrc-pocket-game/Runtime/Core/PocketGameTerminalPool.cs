using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace VrcPocketGame
{
    /// <summary>Small authoritative claim table for a pool of independent games.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PocketGameTerminalPool : UdonSharpBehaviour
    {
        public VRCObjectPool pool;
        public GameObject[] terminalRoots;
        public PocketGameTerminalSession[] terminalSessions;
        [UdonSynced] public int[] claimedPlayerIds;
        [UdonSynced] public int[] claimGenerations;
        public TMPro.TMP_Text statusText;
        public float spawnDistance = 0.45f;

        private bool _playerDataRestored;
        private bool _pendingClaim;
        private int _claimAttempts;
        private int _pendingReturn = -1;
        private int _pendingReturnPlayer = -1;
        private int _pendingReturnGeneration;
        private bool _resend;
        private int _verifySlot = -1;
        private int _verifyAttempts;
        private int _verifyGeneration;
        private bool _verifyConfirmed;
        private const int MaxRetries = 8;

        private void Start()
        {
            EnsureTables();
            SetStatus("Loading player data…");
            SendCustomEventDelayedSeconds(nameof(OwnerMaintenance), 5f);
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (player != null && player.isLocal) { _playerDataRestored = true; SetStatus("Use: call or recall your game"); }
        }

        public bool IsLocalPlayerDataRestored() { return _playerDataRestored; }
        public int GetClaimedPlayerId(int index) { return HasClaimSlot(index) ? claimedPlayerIds[index] : -1; }
        public int GetClaimGeneration(int index) { return HasClaimSlot(index) ? claimGenerations[index] : 0; }
        public bool IsTerminalRootOwnedByLocal(int index)
        {
            return IsSlot(index) && terminalRoots[index] != null && Networking.IsOwner(terminalRoots[index]);
        }

        public override void Interact()
        {
            if (!_playerDataRestored) { SetStatus("Loading player data…"); return; }
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null) return;
            int existing = FindClaim(local.playerId);
            if (existing >= 0) { RestoreTerminalOwnership(existing); Recall(existing); BeginVerify(existing); return; }
            if (_pendingClaim || _pendingReturn >= 0) return;
            _pendingClaim = true; _claimAttempts = 0;
            Networking.SetOwner(local, gameObject);
            SendCustomEventDelayedFrames(nameof(AttemptClaim), 1);
        }

        public void AttemptClaim()
        {
            if (!_pendingClaim) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !_playerDataRestored) { _pendingClaim = false; return; }
            if (!Networking.IsOwner(gameObject))
            {
                if (_claimAttempts++ < MaxRetries) { Networking.SetOwner(local, gameObject); SendCustomEventDelayedSeconds(nameof(AttemptClaim), RetryDelay()); }
                else { _pendingClaim = false; SetStatus("Terminal is busy; try again."); }
                return;
            }
            EnsureTables();
            CleanupMissingClaims();
            int existing = FindClaim(local.playerId);
            if (existing >= 0) { _pendingClaim = false; Recall(existing); BeginVerify(existing); return; }
            GameObject root = pool == null ? null : pool.TryToSpawn();
            int index = FindRoot(root);
            if (root == null || !IsSlot(index) || terminalSessions[index] == null) { if (root != null) pool.Return(root); _pendingClaim = false; SetStatus("All terminals are in use."); return; }
            claimedPlayerIds[index] = local.playerId;
            claimGenerations[index] = NextGeneration(claimGenerations[index]);
            Sync();
            Networking.SetOwner(local, root);
            Networking.SetOwner(local, terminalSessions[index].gameObject);
            if (terminalSessions[index].gameEvents != null) Networking.SetOwner(local, terminalSessions[index].gameEvents.gameObject);
            root.transform.SetPositionAndRotation(transform.position - transform.forward * spawnDistance, transform.rotation);
            _pendingClaim = false;
            SetStatus("Game ready.");
            BeginVerify(index);
        }

        private void BeginVerify(int index)
        {
            _verifySlot = index;
            _verifyGeneration = GetClaimGeneration(index);
            _verifyAttempts = 0;
            _verifyConfirmed = false;
            SendCustomEventDelayedSeconds(nameof(VerifyClaim), 0.7f);
        }

        // Re-sends the current generation. A delayed old OnClaimed cannot win because states reject it.
        public void VerifyClaim()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null) return;
            int index = _verifySlot;
            if (index < 0) return;
            if (!HasClaimSlot(index) || claimedPlayerIds[index] != local.playerId || claimGenerations[index] != _verifyGeneration || terminalRoots[index] == null || !terminalRoots[index].activeInHierarchy)
            {
                _verifySlot = -1;
                return;
            }
            PocketGameTerminalSession state = terminalSessions[index];
            if (state == null) return;
            Networking.SetOwner(local, terminalRoots[index]); Networking.SetOwner(local, state.gameObject); if(state.gameEvents!=null) Networking.SetOwner(local,state.gameEvents.gameObject);
            if (!Networking.IsOwner(terminalRoots[index]) || !Networking.IsOwner(state.gameObject) || (state.gameEvents != null && !Networking.IsOwner(state.gameEvents.gameObject)))
            {
                if (_verifyAttempts++ < MaxRetries) SendCustomEventDelayedSeconds(nameof(VerifyClaim), RetryDelay());
                else
                {
                    if (Networking.IsOwner(gameObject))
                    {
                        claimedPlayerIds[index] = -1;
                        claimGenerations[index] = NextGeneration(claimGenerations[index]);
                        state.PocketTerminal_OnReleased();
                        Sync();
                        if (pool != null) pool.Return(terminalRoots[index]);
                    }
                    _verifySlot = -1;
                    SetStatus("Could not prepare terminal; use the kiosk to retry.");
                }
                return;
            }
            state.assignedPlayerId=local.playerId; state.sessionGeneration=claimGenerations[index]; state.PocketTerminal_OnClaimed();
            if (!state.HasAcceptedSession())
            {
                if (_verifyAttempts++ < MaxRetries) SendCustomEventDelayedSeconds(nameof(VerifyClaim), .5f);
                else _verifySlot = -1;
                return;
            }
            if (!_verifyConfirmed)
            {
                _verifyConfirmed = true;
                SendCustomEventDelayedSeconds(nameof(VerifyClaim), .8f);
                return;
            }
            _verifySlot = -1;
        }

        public void RequestReturn(int slotIndex)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !IsSlot(slotIndex) || claimedPlayerIds[slotIndex] != local.playerId) return;
            // Game has already received OnReturnRequested and may deny by leaving returnApproved false.
            if (terminalRoots[slotIndex] == null || terminalSessions[slotIndex] == null) return;
            Networking.SetOwner(local, terminalRoots[slotIndex]);
            Networking.SetOwner(local, terminalSessions[slotIndex].gameObject);
            if(terminalSessions[slotIndex].gameEvents!=null) Networking.SetOwner(local,terminalSessions[slotIndex].gameEvents.gameObject);
            _pendingReturn = slotIndex; _pendingReturnPlayer = local.playerId; _pendingReturnGeneration = claimGenerations[slotIndex];
            _claimAttempts = 0; Networking.SetOwner(local, gameObject);
            SendCustomEventDelayedFrames(nameof(AttemptReturn), 1);
        }

        public void AttemptReturn()
        {
            int index = _pendingReturn; if (!IsSlot(index)) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || local.playerId != _pendingReturnPlayer || claimedPlayerIds[index] != _pendingReturnPlayer || claimGenerations[index] != _pendingReturnGeneration) { _pendingReturn = -1; return; }
            if (!Networking.IsOwner(gameObject))
            {
                if (_claimAttempts++ < MaxRetries) { Networking.SetOwner(local, gameObject); SendCustomEventDelayedSeconds(nameof(AttemptReturn), RetryDelay()); }
                else { terminalSessions[index].CancelReturn(); _pendingReturn = -1; SetStatus("Could not stow terminal; try again."); }
                return;
            }
            PocketGameTerminalSession state = terminalSessions[index];
            if (state == null || terminalRoots[index] == null || !state.IsLocalClaimant() || !state.returnApproved)
            {
                if (state != null) state.CancelReturn();
                _pendingReturn = -1;
                SetStatus("Game is not ready to stow.");
                return;
            }
            claimedPlayerIds[index] = -1; claimGenerations[index] = NextGeneration(claimGenerations[index]); Sync();
            state.sessionGeneration=claimGenerations[index]; state.PocketTerminal_OnReleased();
            if (pool != null && terminalRoots[index] != null) pool.Return(terminalRoots[index]);
            _pendingReturn = -1; SetStatus("Game stowed.");
        }

        public void RestoreTerminalOwnership(int index)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !HasClaimSlot(index) || claimedPlayerIds[index] != local.playerId) return;
            PocketGameTerminalSession session = terminalSessions[index];
            GameObject root = terminalRoots[index];
            if (session == null || root == null || !root.activeInHierarchy) return;
            if (session.pickup != null && session.pickup.IsHeld && session.pickup.currentPlayer != null && !session.pickup.currentPlayer.isLocal) return;
            if (!Networking.IsOwner(root)) Networking.SetOwner(local, root);
            if (!Networking.IsOwner(session.gameObject)) Networking.SetOwner(local, session.gameObject);
            if (session.gameEvents != null && !Networking.IsOwner(session.gameEvents.gameObject)) Networking.SetOwner(local, session.gameEvents.gameObject);
        }
        public void Recall(int index)
        {
            if (!HasClaimSlot(index) || terminalSessions[index] == null || !terminalSessions[index].IsLocalClaimant()) return;
            PocketGameTerminalSession session = terminalSessions[index];
            if (session.ui != null && session.ui.BlocksGameInput()) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null) return;
            if (session.pickup != null && session.pickup.IsHeld) session.pickup.Drop();
            VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            terminalRoots[index].transform.SetPositionAndRotation(head.position + forward * .7f - Vector3.up * .18f, Quaternion.LookRotation(forward, Vector3.up));
            VRCObjectSync objectSync = terminalRoots[index].GetComponent<VRCObjectSync>();
            if (objectSync != null) objectSync.FlagDiscontinuity();
            terminalSessions[index].sessionGeneration=claimGenerations[index]; terminalSessions[index].PocketTerminal_OnRecalled(); SetStatus("Game recalled.");
        }
        public override void OnPostSerialization(SerializationResult result) { if (!result.success) { _resend = true; SendCustomEventDelayedSeconds(nameof(RetrySync), .35f); } else _resend = false; }
        public void RetrySync() { if (_resend && Networking.IsOwner(gameObject)) RequestSerialization(); }
        private void EnsureTables() { int n = terminalRoots == null ? 0 : terminalRoots.Length; if (claimedPlayerIds == null || claimedPlayerIds.Length != n) { claimedPlayerIds = new int[n]; for(int i=0;i<n;i++) claimedPlayerIds[i]=-1; } if (claimGenerations == null || claimGenerations.Length != n) claimGenerations = new int[n]; }
        private bool IsSlot(int i) { return terminalRoots != null && terminalSessions != null && i >= 0 && i < terminalRoots.Length && i < terminalSessions.Length; }
        private bool HasClaimSlot(int i) { return IsSlot(i) && claimedPlayerIds != null && claimGenerations != null && i < claimedPlayerIds.Length && i < claimGenerations.Length; }
        private int FindClaim(int playerId) { if (claimedPlayerIds == null) return -1; for(int i=0;i<claimedPlayerIds.Length;i++) if(claimedPlayerIds[i]==playerId) return i; return -1; }
        private int FindRoot(GameObject root) { if (terminalRoots == null || root == null) return -1; for(int i=0;i<terminalRoots.Length;i++) if(terminalRoots[i]==root) return i; return -1; }
        private int NextGeneration(int current) { return current == int.MaxValue ? 1 : current + 1; }
        private float RetryDelay() { return .15f + _claimAttempts * .1f; }
        private void Sync() { if (Networking.IsOwner(gameObject)) { _resend = true; RequestSerialization(); } }
        private void SetStatus(string value) { if (statusText != null) statusText.text = value; }
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (player == null) return;
            if (!Networking.IsOwner(gameObject)) return;
            SendCustomEventDelayedSeconds(nameof(CleanupMissingClaims), .5f);
        }
        public void CleanupMissingClaims()
        {
            if (!Networking.IsOwner(gameObject) || claimedPlayerIds == null) return;
            bool changed=false;
            for(int i=0;i<claimedPlayerIds.Length;i++) if(HasClaimSlot(i) && claimedPlayerIds[i]>=0 && (VRCPlayerApi.GetPlayerById(claimedPlayerIds[i])==null || terminalRoots[i]==null || !terminalRoots[i].activeSelf))
            {
                claimedPlayerIds[i]=-1; claimGenerations[i]=NextGeneration(claimGenerations[i]); changed=true;
                if(terminalSessions[i]!=null) { terminalSessions[i].sessionGeneration=claimGenerations[i]; terminalSessions[i].PocketTerminal_OnReleased(); }
                if(pool!=null && terminalRoots[i]!=null && terminalRoots[i].activeInHierarchy) pool.Return(terminalRoots[i]);
            }
            if(changed) Sync();
        }
        public void OwnerMaintenance()
        {
            if (Networking.IsOwner(gameObject)) CleanupMissingClaims();
            SendCustomEventDelayedSeconds(nameof(OwnerMaintenance), 5f);
        }
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player != null && player.isLocal) SendCustomEventDelayedSeconds(nameof(CleanupMissingClaims), .5f);
        }
    }
}
