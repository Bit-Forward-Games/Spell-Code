using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameEndScreen : MonoBehaviour
{
    private const int MaxPlayerSlots = 4;
    private const byte RematchOption = 0;
    private const byte MainMenuOption = 1;
    private const float StateResendInterval = 0.5f;
    private const float ResultResendInterval = 0.2f;
    private const float PeerLivenessCheckInterval = 1f;
    private const float PeerLivenessTimeout = 10f;

    public static GameEndScreen ActiveInstance { get; private set; }

    [SerializeField] private SpriteRenderer winnerImage;
    [SerializeField] private TextMeshProUGUI winnerText;
    public Vector3 startingLocation = new Vector3(-15f, -1f, 0f);
    public Vector3 targetLocation = new Vector3(3f, -1f, 0f);

    private readonly byte[] selectedOptions = new byte[MaxPlayerSlots];
    private readonly bool[] confirmedOptions = new bool[MaxPlayerSlots];
    private readonly uint[] optionRevisions = new uint[MaxPlayerSlots];
    private readonly Image[] playerIndicators = new Image[MaxPlayerSlots];
    private readonly Color[] confirmedIndicatorColors = new Color[MaxPlayerSlots];
    private readonly Vector2[] indicatorAnchoredPositions = new Vector2[MaxPlayerSlots];
    private readonly Vector3[] indicatorLocalScales = new Vector3[MaxPlayerSlots];
    private readonly HashSet<int> resultAcknowledgedSlots = new HashSet<int>();
    private readonly HashSet<GameObject> hiddenMatchUiObjects = new HashSet<GameObject>();

    private GameObject endGameOptions;
    private Button rematchButton;
    private Button mainMenuButton;
    private bool useOnlineEndFlow;
    private bool optionsVisible;
    private bool resolutionTriggered;
    private bool exitTriggered;
    private bool referencesResolved;
    private int onlineVoteEpoch;
    private byte resolvedOption;
    private float nextStateResendTime;
    private float nextPeerLivenessCheckTime;

    private void Awake()
    {
        ActiveInstance = this;
        ResolveEndGameOptionsReferences();
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }
    }

    private void Start()
    {
        useOnlineEndFlow = GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive;
        optionsVisible = false;
        resolutionTriggered = false;
        exitTriggered = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.endInputEnabled = false;
        }

        if (useOnlineEndFlow)
        {
            Time.timeScale = 1f;
            onlineVoteEpoch = GameManager.Instance.OnlineEndOptionsEpoch;
        }

        // Player UI and other Selectables are DontDestroyOnLoad / carried in from the match and
        // otherwise linger on the end screen. Preserve only the authored end-game options subtree.
        DisableEndSceneUi();
        ApplyWinnerPresentation(useOnlineEndFlow);

        if (winnerImage != null)
        {
            winnerImage.transform.position = startingLocation;
            winnerImage.transform
                .DOMoveX(targetLocation.x, 2f)
                .SetUpdate(true)
                .OnComplete(ShowEndGameOptions);
        }

        // A missing/killed tween must not strand the players without an end-screen choice.
        StartCoroutine(ShowEndGameOptionsFallback());
    }

    private void Update()
    {
        if (!optionsVisible || GameManager.Instance == null)
        {
            return;
        }

        RefreshParticipantIndicators();

        if (useOnlineEndFlow)
        {
            UpdateOnlinePeerLiveness();
            if (resolutionTriggered || exitTriggered)
            {
                return;
            }

            ProcessPlayerInput(GameManager.Instance.localPlayerIndex);

            if (Time.unscaledTime >= nextStateResendTime)
            {
                BroadcastLocalOnlineState();
            }

            if (GameManager.Instance.IsOnlineHostAuthority())
            {
                TryResolveSelections();
            }
            return;
        }

        if (resolutionTriggered)
        {
            return;
        }

        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            if (!GameManager.Instance.IsPlayerSlotConnected(slot))
            {
                continue;
            }

            ProcessPlayerInput(slot);
            if (resolutionTriggered)
            {
                return;
            }
        }
    }

    private void ProcessPlayerInput(int slot)
    {
        if (slot < 0
            || slot >= MaxPlayerSlots
            || confirmedOptions[slot]
            || !GameManager.Instance.IsPlayerSlotConnected(slot))
        {
            return;
        }

        PlayerController player = GameManager.Instance.players[slot];
        if (player == null || player.inputs == null)
        {
            return;
        }

        bool upPressed = player.inputs.UpAction != null && player.inputs.UpAction.WasPressedThisFrame();
        bool downPressed = player.inputs.DownAction != null && player.inputs.DownAction.WasPressedThisFrame();

        if (upPressed && !downPressed)
        {
            SetPlayerOption(slot, RematchOption);
        }
        else if (downPressed && !upPressed)
        {
            SetPlayerOption(slot, MainMenuOption);
        }

        if (player.inputs.JumpAction != null && player.inputs.JumpAction.WasPressedThisFrame())
        {
            ConfirmPlayerOption(slot);
        }
    }

    private void SetPlayerOption(int slot, byte option)
    {
        if (confirmedOptions[slot] || selectedOptions[slot] == option)
        {
            return;
        }

        selectedOptions[slot] = option;
        optionRevisions[slot]++;
        UpdateIndicator(slot);

        if (useOnlineEndFlow && slot == GameManager.Instance.localPlayerIndex)
        {
            BroadcastLocalOnlineState();
        }
    }

    private void ConfirmPlayerOption(int slot)
    {
        if (confirmedOptions[slot])
        {
            return;
        }

        confirmedOptions[slot] = true;
        optionRevisions[slot]++;
        UpdateIndicator(slot);

        if (useOnlineEndFlow)
        {
            if (slot == GameManager.Instance.localPlayerIndex)
            {
                BroadcastLocalOnlineState();
            }

            if (GameManager.Instance.IsOnlineHostAuthority())
            {
                TryResolveSelections();
            }
            return;
        }

        TryResolveSelections();
    }

    private void TryResolveSelections()
    {
        if (resolutionTriggered || GameManager.Instance == null)
        {
            return;
        }

        bool foundParticipant = false;
        bool unanimousRematch = true;
        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            if (!GameManager.Instance.IsPlayerSlotConnected(slot))
            {
                continue;
            }

            foundParticipant = true;
            if (!confirmedOptions[slot])
            {
                return;
            }

            unanimousRematch &= selectedOptions[slot] == RematchOption;
        }

        if (!foundParticipant)
        {
            return;
        }

        // An online rematch needs at least two surviving peers. If a match ended by disconnect and
        // only the winner remains, resolve to Solo Lobby instead of starting a one-player session.
        if (useOnlineEndFlow && GameManager.Instance.ActivePlayerCount < 2)
        {
            unanimousRematch = false;
        }

        byte result = unanimousRematch ? RematchOption : MainMenuOption;
        if (useOnlineEndFlow)
        {
            if (!GameManager.Instance.IsOnlineHostAuthority())
            {
                return;
            }

            BeginHostOnlineResolution(result);
            return;
        }

        resolutionTriggered = true;
        resolvedOption = result;
        SetButtonsInteractable(false);
        if (result == RematchOption)
        {
            if (!GameManager.Instance.StartOfflineRematchLobbyFromEnd())
            {
                ReturnToSoloLobby();
            }
        }
        else
        {
            ReturnToSoloLobby();
        }
    }

    private void BeginHostOnlineResolution(byte result)
    {
        resolutionTriggered = true;
        resolvedOption = result;
        resultAcknowledgedSlots.Clear();
        resultAcknowledgedSlots.Add(GameManager.Instance.localPlayerIndex);
        SetButtonsInteractable(false);

        StartCoroutine(DeliverHostOnlineResult());
    }

    private IEnumerator DeliverHostOnlineResult()
    {
        float nextSendTime = float.NegativeInfinity;

        // Do not tear the Steam session down until every still-responsive peer has ACKed. The
        // liveness check removes genuinely lost peers, while healthy peers keep receiving this
        // reliable result until they confirm it. This is especially important for Main Menu,
        // which has no subsequent STAGE_SELECT packet to pull a missed client forward.
        while (!exitTriggered && !HaveAllResultAcknowledgements())
        {
            if (Time.unscaledTime >= nextSendTime)
            {
                MatchMessageManager.Instance?.SendEndOptionResult(onlineVoteEpoch, resolvedOption);
                nextSendTime = Time.unscaledTime + ResultResendInterval;
            }

            UpdateOnlinePeerLiveness();
            yield return null;
        }

        if (exitTriggered)
        {
            yield break;
        }

        if (resolvedOption == RematchOption)
        {
            if (GameManager.Instance == null
                || !GameManager.Instance.StartOnlineRematchFromEnd(onlineVoteEpoch))
            {
                ReturnToSoloLobby();
            }
        }
        else
        {
            ReturnToSoloLobby();
        }
    }

    private bool HaveAllResultAcknowledgements()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }

        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            if (GameManager.Instance.IsPlayerSlotConnected(slot)
                && !resultAcknowledgedSlots.Contains(slot))
            {
                return false;
            }
        }

        return true;
    }

    public void ReceiveOnlineOptionState(int senderSlot, int epoch, byte option, bool confirmed, uint revision)
    {
        if (!CanAcceptOnlinePacket(senderSlot, epoch)
            || option > MainMenuOption
            || revision == 0
            || revision <= optionRevisions[senderSlot])
        {
            return;
        }

        optionRevisions[senderSlot] = revision;
        selectedOptions[senderSlot] = option;
        confirmedOptions[senderSlot] = confirmed;
        UpdateIndicator(senderSlot);

        if (GameManager.Instance.IsOnlineHostAuthority())
        {
            TryResolveSelections();
        }
    }

    public void ReceiveOnlineOptionResult(int senderSlot, int epoch, byte result)
    {
        if (!CanAcceptOnlinePacket(senderSlot, epoch)
            || !GameManager.Instance.IsOnlineHostSlot(senderSlot)
            || result > MainMenuOption)
        {
            return;
        }

        // ACK every duplicate. The host deliberately resends until every connected sparse-roster
        // slot has acknowledged, which prevents teardown from closing P2P before a 4th peer hears.
        MatchMessageManager.Instance?.SendEndOptionResultAcknowledgement(epoch);

        if (resolutionTriggered)
        {
            return;
        }

        resolutionTriggered = true;
        resolvedOption = result;
        SetButtonsInteractable(false);

        if (result == RematchOption)
        {
            // The host sends a separate cached MainMenu transition after every connected player
            // ACKs this result. Keeping the result cosmetic prevents an early-loading client from
            // destroying this screen before its ACK can reach the host under simulated latency.
        }
        else
        {
            StartCoroutine(ReturnToSoloLobbyAfterDeliveryGrace());
        }
    }

    public void ReceiveOnlineOptionResultAcknowledgement(int senderSlot, int epoch)
    {
        if (!useOnlineEndFlow
            || GameManager.Instance == null
            || !GameManager.Instance.IsOnlineHostAuthority()
            || !resolutionTriggered
            || epoch != onlineVoteEpoch
            || !GameManager.Instance.IsPlayerSlotConnected(senderSlot))
        {
            return;
        }

        resultAcknowledgedSlots.Add(senderSlot);
    }

    private IEnumerator ReturnToSoloLobbyAfterDeliveryGrace()
    {
        yield return new WaitForSecondsRealtime(0.75f);
        ReturnToSoloLobby();
    }

    private bool CanAcceptOnlinePacket(int senderSlot, int epoch)
    {
        return useOnlineEndFlow
            && optionsVisible
            && GameManager.Instance != null
            && GameManager.Instance.isOnlineMatchActive
            && SceneManager.GetActiveScene().name == "End"
            && epoch == onlineVoteEpoch
            && senderSlot >= 0
            && senderSlot < MaxPlayerSlots
            && GameManager.Instance.IsPlayerSlotConnected(senderSlot);
    }

    private void BroadcastLocalOnlineState()
    {
        if (!useOnlineEndFlow || GameManager.Instance == null || MatchMessageManager.Instance == null)
        {
            return;
        }

        int slot = GameManager.Instance.localPlayerIndex;
        if (slot < 0 || slot >= MaxPlayerSlots || !GameManager.Instance.IsPlayerSlotConnected(slot))
        {
            return;
        }

        if (optionRevisions[slot] == 0)
        {
            optionRevisions[slot] = 1;
        }

        MatchMessageManager.Instance.SendEndOptionState(
            onlineVoteEpoch,
            selectedOptions[slot],
            confirmedOptions[slot],
            optionRevisions[slot]);
        nextStateResendTime = Time.unscaledTime + StateResendInterval;
    }

    private void ShowEndGameOptions()
    {
        if (optionsVisible)
        {
            return;
        }

        if (!ResolveEndGameOptionsReferences())
        {
            Debug.LogError("[GameEndScreen] End Game Options prefab or one of its required children is missing.");
            return;
        }

        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            selectedOptions[slot] = RematchOption;
            confirmedOptions[slot] = false;
            optionRevisions[slot] = 0;
        }

        rematchButton.gameObject.SetActive(true);
        mainMenuButton.gameObject.SetActive(true);
        endGameOptions.SetActive(true);
        SetButtonsInteractable(true);
        optionsVisible = true;
        RefreshParticipantIndicators();

        if (useOnlineEndFlow)
        {
            if (onlineVoteEpoch <= 0)
            {
                onlineVoteEpoch = GameManager.Instance.OnlineEndOptionsEpoch;
            }
            BroadcastLocalOnlineState();
            nextPeerLivenessCheckTime = Time.unscaledTime + PeerLivenessTimeout;
        }
    }

    private void UpdateOnlinePeerLiveness()
    {
        if (!useOnlineEndFlow
            || GameManager.Instance == null
            || MatchMessageManager.Instance == null
            || Time.unscaledTime < nextPeerLivenessCheckTime)
        {
            return;
        }

        nextPeerLivenessCheckTime = Time.unscaledTime + PeerLivenessCheckInterval;
        GameManager manager = GameManager.Instance;

        if (manager.IsOnlineHostAuthority())
        {
            for (int slot = 0; slot < MaxPlayerSlots; slot++)
            {
                if (slot == manager.localPlayerIndex
                    || !manager.IsPlayerSlotConnected(slot)
                    || MatchMessageManager.Instance.IsPeerResponsive(slot, PeerLivenessTimeout))
                {
                    continue;
                }

                manager.DropUnresponsiveEndScreenPeer(slot);
            }

            if (!resolutionTriggered)
            {
                TryResolveSelections();
            }
            return;
        }

        int hostSlot = -1;
        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            if (manager.IsOnlineHostSlot(slot))
            {
                hostSlot = slot;
                break;
            }
        }

        if (hostSlot < 0
            || !manager.IsPlayerSlotConnected(hostSlot)
            || !MatchMessageManager.Instance.IsPeerResponsive(hostSlot, PeerLivenessTimeout))
        {
            HandleOnlineHostLost();
        }
    }

    public void HandleOnlineHostLost()
    {
        if (useOnlineEndFlow && !exitTriggered)
        {
            ReturnToSoloLobby();
        }
    }

    private IEnumerator ShowEndGameOptionsFallback()
    {
        yield return new WaitForSecondsRealtime(2.1f);
        ShowEndGameOptions();
    }

    private bool ResolveEndGameOptionsReferences()
    {
        if (referencesResolved)
        {
            return true;
        }

        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        Transform optionsTransform = FindNamedTransform(descendants, "End Game Options");
        Transform rematchTransform = FindNamedTransform(descendants, "Rematch Button");
        Transform mainMenuTransform = FindNamedTransform(descendants, "Main Menu Button");

        endGameOptions = optionsTransform != null ? optionsTransform.gameObject : null;
        rematchButton = rematchTransform != null ? rematchTransform.GetComponent<Button>() : null;
        mainMenuButton = mainMenuTransform != null ? mainMenuTransform.GetComponent<Button>() : null;

        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            Transform indicatorTransform = FindNamedTransform(descendants, $"P{slot + 1} Image");
            playerIndicators[slot] = indicatorTransform != null ? indicatorTransform.GetComponent<Image>() : null;
        }

        referencesResolved = endGameOptions != null
            && rematchButton != null
            && mainMenuButton != null;
        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            referencesResolved &= playerIndicators[slot] != null;
        }

        if (!referencesResolved)
        {
            return false;
        }

        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            RectTransform indicatorRect = playerIndicators[slot].rectTransform;
            confirmedIndicatorColors[slot] = playerIndicators[slot].color;
            indicatorAnchoredPositions[slot] = indicatorRect.anchoredPosition;
            indicatorLocalScales[slot] = indicatorRect.localScale;
        }

        // The scene instance currently has direct Restart/SoloLobby callbacks. Replace them at
        // runtime so pointer submits obey the same per-player confirmation and online vote rules.
        // Replacing the events also removes Inspector-persistent callbacks; RemoveAllListeners()
        // only clears runtime listeners and would leave Restart/SoloLobby free to bypass voting.
        rematchButton.onClick = new Button.ButtonClickedEvent();
        mainMenuButton.onClick = new Button.ButtonClickedEvent();
        rematchButton.onClick.AddListener(() => SelectAndConfirmPointerOption(RematchOption));
        mainMenuButton.onClick.AddListener(() => SelectAndConfirmPointerOption(MainMenuOption));
        return true;
    }

    private static Transform FindNamedTransform(Transform[] transforms, string objectName)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private void SelectAndConfirmPointerOption(byte option)
    {
        if (!optionsVisible || resolutionTriggered || GameManager.Instance == null)
        {
            return;
        }

        int slot = useOnlineEndFlow ? GameManager.Instance.localPlayerIndex : 0;
        if (!GameManager.Instance.IsPlayerSlotConnected(slot) || confirmedOptions[slot])
        {
            return;
        }

        SetPlayerOption(slot, option);
        ConfirmPlayerOption(slot);
    }

    private void RefreshParticipantIndicators()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        for (int slot = 0; slot < MaxPlayerSlots; slot++)
        {
            if (playerIndicators[slot] == null)
            {
                continue;
            }

            bool isParticipant = GameManager.Instance.IsPlayerSlotConnected(slot);
            playerIndicators[slot].gameObject.SetActive(isParticipant);
            if (isParticipant)
            {
                UpdateIndicator(slot);
            }
        }
    }

    private void UpdateIndicator(int slot)
    {
        Image indicator = playerIndicators[slot];
        if (indicator == null || rematchButton == null || mainMenuButton == null)
        {
            return;
        }

        Transform selectedParent = selectedOptions[slot] == MainMenuOption
            ? mainMenuButton.transform
            : rematchButton.transform;
        RectTransform indicatorRect = indicator.rectTransform;
        if (indicatorRect.parent != selectedParent)
        {
            indicatorRect.SetParent(selectedParent, false);
        }

        indicatorRect.anchoredPosition = indicatorAnchoredPositions[slot];
        indicatorRect.localScale = indicatorLocalScales[slot];
        indicator.color = confirmedOptions[slot] ? confirmedIndicatorColors[slot] : Color.white;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (rematchButton != null)
        {
            rematchButton.interactable = interactable;
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.interactable = interactable;
        }
    }

    private void ReturnToSoloLobby()
    {
        if (exitTriggered)
        {
            return;
        }

        exitTriggered = true;
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.isOnlineMatchActive)
        {
            manager.StopMatch("End-screen Main Menu selected");
        }

        if (manager != null && manager.sceneManager != null)
        {
            manager.sceneManager.SoloLobby();
            return;
        }

        SceneManager.LoadScene("SoloLobby");
    }

    private void ApplyWinnerPresentation(bool onlineEndFlow)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        int winnerPid = onlineEndFlow
            ? GameManager.Instance.endWinnerPid
            : (GameManager.Instance.bigWinner != null ? GameManager.Instance.bigWinner.pID : -1);
        Texture2D paletteTexture = onlineEndFlow
            ? GameManager.Instance.endWinnerPalette
            : (GameManager.Instance.bigWinner != null
                && GameManager.Instance.bigWinner.matchPalette != null
                && GameManager.Instance.bigWinner.pID - 1 >= 0
                && GameManager.Instance.bigWinner.pID - 1 < GameManager.Instance.bigWinner.matchPalette.Length
                    ? GameManager.Instance.bigWinner.matchPalette[GameManager.Instance.bigWinner.pID - 1]
                    : null);

        if (winnerText != null && winnerPid > 0)
        {
            winnerText.text = $"Player {winnerPid} WINS!";
            winnerText.gameObject.SetActive(true);
        }

        if (winnerImage == null)
        {
            Debug.LogError("SpriteRenderer is not assigned.");
            return;
        }

        if (paletteTexture == null)
        {
            return;
        }

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        winnerImage.GetPropertyBlock(propertyBlock);
        if (winnerImage.sharedMaterial != null && winnerImage.sharedMaterial.HasProperty("_PaletteTex"))
        {
            propertyBlock.SetTexture("_PaletteTex", paletteTexture);
            winnerImage.SetPropertyBlock(propertyBlock);
        }
        else
        {
            Debug.LogWarning("Material does not have a '_PaletteTex' property.");
        }
    }

    private void DisableEndSceneUi()
    {
        Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform optionsTransform = endGameOptions != null ? endGameOptions.transform : null;
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null
                || !selectable.gameObject.activeInHierarchy
                || (optionsTransform != null && selectable.transform.IsChildOf(optionsTransform)))
            {
                continue;
            }

            hiddenMatchUiObjects.Add(selectable.gameObject);
            selectable.gameObject.SetActive(false);
        }
    }

    public void RestoreHiddenMatchUiForRematch()
    {
        foreach (GameObject hiddenUiObject in hiddenMatchUiObjects)
        {
            if (hiddenUiObject != null)
            {
                hiddenUiObject.SetActive(true);
            }
        }

        hiddenMatchUiObjects.Clear();
    }
}
