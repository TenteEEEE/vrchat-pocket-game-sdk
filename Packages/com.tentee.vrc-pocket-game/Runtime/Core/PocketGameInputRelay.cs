using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace VrcPocketGame
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PocketGameInputRelay : UdonSharpBehaviour
    {
        public PocketGameTerminalSession terminalSession;
        public override void OnPickupUseDown() { if (terminalSession != null) terminalSession.PocketTerminal_OnUseDown(); }
        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return terminalSession != null && terminalSession.CanPlayerOwnTerminal(requestingPlayer, requestedOwner);
        }
    }
}
