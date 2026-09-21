using UdonSharp;
using UnityEngine;

namespace VrcPocketGame
{
    /// <summary>Local presentation only. Opening a drawer never changes the game's simulation.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PocketGameUi : UdonSharpBehaviour
    {
        public GameObject mainScreen;
        public GameObject overlayDrawer;
        public GameObject externalDrawer;
        public GameObject helpPanel;
        public GameObject settingsPanel;
        public GameObject confirmPanel;
        public GameObject inputModal;
        public GameObject[] tabPanels;
        public GameObject[] helpPages;
        public Transform scalableRoot;
        public PocketGameTerminalSession terminalSession;
        public float smallScale = .85f;
        public float largeScale = 1.15f;

        private bool _modal;
        private int _drawer;
        private int _helpPage;
        private Vector3 _baseScale;

        private void Start() { CaptureBaseScale(); }
        public bool BlocksGameInput() { return _modal; }
        public bool IsConfirmOpen() { return _modal && confirmPanel != null && confirmPanel.activeSelf; }
        public void ToggleOverlayDrawer() { if (!_modal) ToggleDrawer(1); }
        public void ToggleExternalDrawer() { if (!_modal) ToggleDrawer(2); }

        public void ShowHelp()
        {
            if (_modal) return;
            ClosePanels();
            SetActive(helpPanel, true);
            ShowHelpPage();
        }

        public void ShowSettings()
        {
            if (_modal) return;
            ClosePanels();
            SetActive(settingsPanel, true);
        }

        public void ShowConfirm()
        {
            if (_modal || (terminalSession != null && !terminalSession.CanUseGameInput())) return;
            ClosePanels();
            CloseDrawers();
            _modal = true;
            SetActive(confirmPanel, true);
        }

        public void ClosePanels()
        {
            SetActive(helpPanel, false);
            SetActive(settingsPanel, false);
            SetActive(confirmPanel, false);
            SetActive(inputModal, false);
            _modal = false;
        }

        public void OpenInputModal()
        {
            if (_modal) return;
            ClosePanels();
            CloseDrawers();
            _modal = true;
            SetActive(inputModal, true);
        }
        public void CloseInputModal() { ClosePanels(); }
        public void ShowTab0() { ShowTab(0); }
        public void ShowTab1() { ShowTab(1); }
        public void ShowTab2() { ShowTab(2); }
        public void NextHelpPage() { if (!_modal) { _helpPage++; ShowHelpPage(); } }
        public void PreviousHelpPage() { if (!_modal) { _helpPage--; ShowHelpPage(); } }
        public void SetSmallScale() { SetScale(smallScale); }
        public void SetNormalScale() { SetScale(1f); }
        public void SetLargeScale() { SetScale(largeScale); }

        public void ResetForSession()
        {
            CaptureBaseScale();
            ClosePanels();
            CloseDrawers();
            _helpPage = 0;
            if (scalableRoot != null) scalableRoot.localScale = _baseScale;
            ShowTab(0);
            ShowHelpPage();
        }

        private void CloseDrawers()
        {
            _drawer = 0;
            SetActive(overlayDrawer, false);
            SetActive(externalDrawer, false);
        }

        private void ToggleDrawer(int drawer)
        {
            _drawer = _drawer == drawer ? 0 : drawer;
            SetActive(overlayDrawer, _drawer == 1);
            SetActive(externalDrawer, _drawer == 2);
        }

        private void ShowTab(int index)
        {
            if (_modal || tabPanels == null || index < 0 || index >= tabPanels.Length) return;
            for (int i = 0; i < tabPanels.Length; i++) SetActive(tabPanels[i], i == index);
        }

        private void ShowHelpPage()
        {
            if (helpPages == null || helpPages.Length == 0) return;
            if (_helpPage < 0) _helpPage = helpPages.Length - 1;
            if (_helpPage >= helpPages.Length) _helpPage = 0;
            for (int i = 0; i < helpPages.Length; i++) SetActive(helpPages[i], i == _helpPage);
        }

        private void CaptureBaseScale()
        {
            if (_baseScale == Vector3.zero && scalableRoot != null) _baseScale = scalableRoot.localScale;
        }

        private void SetScale(float multiplier)
        {
            if (_modal || scalableRoot == null || (terminalSession != null && !terminalSession.IsLocalClaimant())) return;
            CaptureBaseScale();
            scalableRoot.localScale = _baseScale * Mathf.Clamp(multiplier, .5f, 2f);
        }

        private void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
