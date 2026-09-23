using UdonSharp;
using VRC.SDKBase;

namespace VrcPocketGame
{
    /// <summary>Optional terminal game base with event hooks. It implements no Udon or Unity events beyond ownership forwarding and the terminal contract, so derived games need no base calls.</summary>
    public abstract class PocketGameBehaviour : UdonSharpBehaviour
    {
        public PocketGameTerminalSession terminalSession;
        public PocketGameUi ui;

        protected bool CanUseGameInput() { return terminalSession != null && terminalSession.CanUseGameInput(); }
        // IsLocalClaimant already includes the session's IsCurrentSession check.
        protected bool IsLocalClaimant() { return terminalSession != null && terminalSession.IsLocalClaimant(); }

        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return terminalSession == null || terminalSession.CanPlayerOwnTerminal(requestingPlayer, requestedOwner);
        }

        public void PocketTerminal_OnClaimed() { OnTerminalClaimed(); }
        public void PocketTerminal_OnReleased() { OnTerminalReleased(); }
        public void PocketTerminal_OnRecalled() { OnTerminalRecalled(); }
        public void PocketTerminal_OnAudienceChanged() { OnTerminalAudienceChanged(); }
        public void PocketTerminal_OnUseDown() { if (CanUseGameInput()) OnTerminalUseDown(); }
        public void PocketTerminal_RequestReturn()
        {
            if (!IsLocalClaimant()) return;
            if (OnTerminalReturnRequested()) terminalSession.PocketTerminal_ApproveReturn();
        }
        public void PocketTerminal_OnReturnStarted() { OnTerminalReturnStarted(); }
        public void PocketTerminal_OnReturnSucceeded() { OnTerminalReturnSucceeded(); }
        public void PocketTerminal_OnReturnFailed() { OnTerminalReturnFailed(); }
        public void PocketTerminal_OnReturnCancelled() { OnTerminalReturnCancelled(); }

        protected virtual void OnTerminalClaimed() { }
        protected virtual void OnTerminalReleased() { }
        protected virtual void OnTerminalRecalled() { }
        protected virtual void OnTerminalAudienceChanged() { }
        protected virtual void OnTerminalUseDown() { }
        /// <summary>Save here; return false to refuse the stow; approval is synchronous.</summary>
        protected virtual bool OnTerminalReturnRequested() { return true; }
        protected virtual void OnTerminalReturnStarted() { }
        protected virtual void OnTerminalReturnSucceeded() { }
        protected virtual void OnTerminalReturnFailed() { }
        protected virtual void OnTerminalReturnCancelled() { }
    }
}
