using UdonSharp;
using UnityEngine;
using VRC.Udon;

namespace VrcPocketGame
{
    /// <summary>Local presentation only. Opening a drawer never changes the game's simulation.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PocketGameUi : UdonSharpBehaviour
    {
        public const int WorldUiSortingOrder = 0;
        public const int TerminalUiSortingOrder = 10;
        public const int TerminalEffectSortingOrder = 11;

        public GameObject mainScreen;
        public GameObject overlayDrawer;
        public GameObject externalDrawer;
        public GameObject helpPanel;
        public GameObject settingsPanel;
        public GameObject confirmPanel;
        public GameObject inputModal;
        public GameObject[] tabPanels;
        public GameObject[] helpPages;
        public GameObject[] extraDrawers;
        public bool closeExtraDrawersOnModal = true;
        public UdonBehaviour helpPageEvents;
        public GameObject[] confirmPanels;
        public Transform scalableRoot;
        public PocketGameTerminalSession terminalSession;
        public float smallScale = .85f;
        public float largeScale = 1.15f;

        private bool _modal;
        private int _drawer;
        private int _helpPage;
        private int _openConfirm = -1;
        private int _displayedHelpPage = -1;
        private Vector3 _baseScale;

        private void Start() { CaptureBaseScale(); }
        public bool BlocksGameInput() { return _modal; }
        public bool IsConfirmOpen() { return _modal && _openConfirm >= 0 && GetConfirmPanel(_openConfirm) != null && GetConfirmPanel(_openConfirm).activeSelf; }
        public int GetOpenConfirm() { return _modal ? _openConfirm : -1; }
        public int GetHelpPage() { return _helpPage; }
        public bool IsHelpOpen() { return helpPanel != null && helpPanel.activeSelf; }
        public bool IsSettingsOpen() { return settingsPanel != null && settingsPanel.activeSelf; }
        public void ToggleHelp() { if (IsHelpOpen()) CloseHelp(); else ShowHelp(); }
        public void ToggleSettings() { if (IsSettingsOpen()) CloseSettings(); else ShowSettings(); }
        public void CloseHelp() { SetActive(helpPanel, false); }
        public void CloseSettings() { SetActive(settingsPanel, false); }
        public void ToggleOverlayDrawer() { if (!_modal) ToggleDrawer(1); }
        public void ToggleExternalDrawer() { if (!_modal) ToggleDrawer(2); }

        public void ShowHelp()
        {
            if (_modal) return;
            ClosePanels();
            SetActive(helpPanel, true);
            ApplyHelpPage();
        }

        public void ShowSettings()
        {
            if (_modal) return;
            ClosePanels();
            SetActive(settingsPanel, true);
        }

        public void ShowConfirm() { ShowConfirmAt(0); }

        public void ShowConfirmAt(int index)
        {
            if (_modal || (terminalSession != null && !terminalSession.CanUseGameInput())) return;
            GameObject panel = GetConfirmPanel(index);
            if (panel == null) return;
            ClosePanels();
            CloseDrawers();
            CloseExtraDrawers(false);
            _modal = true;
            _openConfirm = index;
            SetActive(panel, true);
        }

        public void CloseConfirm(int index)
        {
            if (!_modal || _openConfirm != index) return;
            SetActive(GetConfirmPanel(index), false);
            _openConfirm = -1;
            _modal = false;
        }

        public void ClosePanels()
        {
            SetActive(helpPanel, false);
            SetActive(settingsPanel, false);
            for (int i = 0; i < ConfirmPanelCount(); i++) SetActive(GetConfirmPanel(i), false);
            SetActive(confirmPanel, false);
            SetActive(inputModal, false);
            _modal = false;
            _openConfirm = -1;
        }

        public void OpenInputModal()
        {
            if (_modal) return;
            ClosePanels();
            CloseDrawers();
            CloseExtraDrawers(false);
            _modal = true;
            SetActive(inputModal, true);
        }
        public void CloseInputModal() { ClosePanels(); }
        public void ShowTab0() { ShowTab(0); }
        public void ShowTab1() { ShowTab(1); }
        public void ShowTab2() { ShowTab(2); }
        public void NextHelpPage() { if (!_modal) { _helpPage++; ApplyHelpPage(); } }
        public void PreviousHelpPage() { if (!_modal) { _helpPage--; ApplyHelpPage(); } }
        public void SetSmallScale() { SetScale(smallScale); }
        public void SetNormalScale() { SetScale(1f); }
        public void SetLargeScale() { SetScale(largeScale); }

        public void SetScale(float multiplier)
        {
            if (_modal || scalableRoot == null || (terminalSession != null && !terminalSession.IsLocalClaimant())) return;
            CaptureBaseScale();
            scalableRoot.localScale = _baseScale * Mathf.Clamp(multiplier, .5f, 2f);
        }

        public void ResetForSession()
        {
            CaptureBaseScale();
            ClosePanels();
            CloseDrawers();
            CloseExtraDrawers(true);
            _helpPage = 0;
            if (scalableRoot != null) scalableRoot.localScale = _baseScale;
            ShowTab(0);
            ApplyHelpPage();
        }

        public void ToggleExtraDrawer(int index)
        {
            if (_modal || extraDrawers == null || index < 0 || index >= extraDrawers.Length || extraDrawers[index] == null) return;
            extraDrawers[index].SetActive(!extraDrawers[index].activeSelf);
        }

        public void SetExtraDrawerActive(int index, bool active)
        {
            if (_modal || extraDrawers == null || index < 0 || index >= extraDrawers.Length) return;
            SetActive(extraDrawers[index], active);
        }

        private int ConfirmPanelCount() { return confirmPanels != null && confirmPanels.Length > 0 ? confirmPanels.Length : (confirmPanel != null ? 1 : 0); }
        private GameObject GetConfirmPanel(int index)
        {
            if (confirmPanels != null && confirmPanels.Length > 0) return index >= 0 && index < confirmPanels.Length ? confirmPanels[index] : null;
            return index == 0 ? confirmPanel : null;
        }

        private void CloseExtraDrawers(bool force)
        {
            if ((!force && !closeExtraDrawersOnModal) || extraDrawers == null) return;
            for (int i = 0; i < extraDrawers.Length; i++) SetActive(extraDrawers[i], false);
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

        public void ShowHelpPage(int page)
        {
            if (_modal || helpPages == null || helpPages.Length == 0) return;
            _helpPage = Mathf.Clamp(page, 0, helpPages.Length - 1);
            ApplyHelpPage();
        }

        private void ApplyHelpPage()
        {
            if (helpPages == null || helpPages.Length == 0) return;
            if (_helpPage < 0) _helpPage = helpPages.Length - 1;
            if (_helpPage >= helpPages.Length) _helpPage = 0;
            for (int i = 0; i < helpPages.Length; i++) SetActive(helpPages[i], i == _helpPage);
            if (_displayedHelpPage != _helpPage)
            {
                _displayedHelpPage = _helpPage;
                if (helpPageEvents != null) helpPageEvents.SendCustomEvent("PocketUi_OnHelpPageChanged");
            }
        }

        private void CaptureBaseScale()
        {
            if (_baseScale == Vector3.zero && scalableRoot != null) _baseScale = scalableRoot.localScale;
        }

        private void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
