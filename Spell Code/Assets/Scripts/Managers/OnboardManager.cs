using System;
using System.Collections.Generic;
using BestoNet.Types;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class OnboardManager : MonoBehaviour
{
    [Serializable]
    private sealed class PlayerOnboarding
    {
        [Header("Configuration")]
        public bool startsJoined;

        [Header("Progress")]
        public bool joined;
        public bool moveComplete;
        public bool jumpComplete;
        public bool attackComplete;
        public bool glassBroken;

        [Header("UI")]
        public TextMeshProUGUI moveText;
        public TextMeshProUGUI jumpText;
        public TextMeshProUGUI attackText;
        public TextMeshProUGUI castText;
        public SpriteRenderer breakWithSpellcode;

        [NonSerialized] public GambaMachine gamba;
        [NonSerialized] public bool gambaActive;
    }

    public static OnboardManager Instance { get; private set; }

    public Sprite inputGraphic;
    public Sprite atkGraphic;
    [SerializeField] private InputActionReference attackActionReference;
    [SerializeField] private InputActionReference startActionReference;

    [SerializeField]
    private List<PlayerOnboarding> players = new List<PlayerOnboarding>();

    private readonly List<InputSnapshot> inputSnapshots = new List<InputSnapshot>();
    private GameManager gameManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        ResetOnboarding();
    }

    public void ResetOnboarding()
    {
        Debug.Log("OnboardingReset");

        for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            PlayerOnboarding player = players[playerIndex];
            if (player == null)
            {
                continue;
            }

            ResetProgress(player, player.startsJoined);
            ReloadAllGlyphs(playerIndex);
        }

        ApplyInitialUiState();
    }

    public void ResetPlayerOnboarding(int playerIndex)
    {
        if (!TryGetPlayerOnboarding(playerIndex, out PlayerOnboarding player))
        {
            Debug.LogWarning($"Cannot reset onboarding for player index {playerIndex}.");
            return;
        }

        ResetProgress(player, true);
        ResetPromptColors(player);

        if (!player.startsJoined)
        {
            player.attackText.text = "Attack:\n[CODE]";
            player.attackText.GetComponent<TextSetter>().referenceString = "Attack:\n[CODE]";
            player.attackText.GetComponent<TextSetter>().stringToReplace = "[CODE]";
            player.attackText.GetComponent<TextSetter>().defaultAction = attackActionReference;
            

        }

        player.moveText.enabled = true;
        player.jumpText.enabled = true;
        player.attackText.enabled = false;
        player.castText.enabled = false;
        player.breakWithSpellcode.enabled = false;
        SetGambaActive(player, false, true);
        ReloadAllGlyphs(playerIndex);

        StopGraffitiDrip(playerIndex);
    }

    // Online the local player is paired with keyboard AND pad simultaneously,
    // so the prompts have to re-render whenever they actually switch. Runs
    // outside the sim (plain Update) because glyphs are pure visuals, doing this from
    // OnboardUpdate would re-render on every rollback resim. Reloads only on an actual change.
    private InputDevice[] lastGlyphDevices;

    private void Update()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null || gameManager.players == null || players == null)
        {
            return;
        }

        if (lastGlyphDevices == null || lastGlyphDevices.Length != players.Count)
        {
            lastGlyphDevices = new InputDevice[players.Count];
        }

        for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            if (playerIndex >= gameManager.players.Length)
            {
                break;
            }

            if (players[playerIndex] == null)
            {
                continue;
            }

            PlayerController player = gameManager.players[playerIndex];
            InputDevice device = player != null && player.inputs != null
                ? player.inputs.ActiveInputDevice
                : null;

            if (device == null || lastGlyphDevices[playerIndex] == device)
            {
                continue;
            }

            lastGlyphDevices[playerIndex] = device;
            ReloadAllGlyphs(playerIndex);
        }
    }

    public void ReloadAllGlyphs(int playerIndex)
    {
        if (!TryGetPlayerOnboarding(playerIndex, out PlayerOnboarding player))
        {
            Debug.LogWarning($"Cannot reset onboarding for player index {playerIndex}.");
            return;
        }

        player.attackText.GetComponent<TextSetter>().UpdateGlyph();
        foreach(TextSetter ts in player.moveText.GetComponents<TextSetter>())
        {
            ts.UpdateGlyph();
        }
        player.jumpText.GetComponent<TextSetter>().UpdateGlyph();
        player.castText.GetComponent<TextSetter>().UpdateGlyph();

    }

    private static void ResetProgress(PlayerOnboarding player, bool joined)
    {
        player.joined = joined;
        player.moveComplete = false;
        player.jumpComplete = false;
        player.attackComplete = false;
        player.glassBroken = false;
        player.gambaActive = false;
    }

    private static void ResetPromptColors(PlayerOnboarding player)
    {
        Color defaultColor = GameManager.colors["white"];
        player.moveText.color = defaultColor;
        player.jumpText.color = defaultColor;
        player.attackText.color = defaultColor;
        player.castText.color = defaultColor;
    }

    private void ApplyInitialUiState()
    {
        Debug.Log("Applying Initial Onboarding UI State");

        for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            if (players[playerIndex] != null)
            {
                players[playerIndex].gamba = null;
            }
        }

        GambaMachine[] gambas =
            FindObjectsByType<GambaMachine>(FindObjectsSortMode.InstanceID);

        foreach (GambaMachine gamba in gambas)
        {
            int playerIndex = gamba.ownerPID - 1;
            if (TryGetPlayerOnboarding(playerIndex, out PlayerOnboarding player))
            {
                player.gamba = gamba;
            }
        }

        for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            PlayerOnboarding player = players[playerIndex];
            if (player == null)
            {
                continue;
            }

            player.joined = player.startsJoined;
            player.moveText.enabled = player.startsJoined;
            player.jumpText.enabled = player.startsJoined;
            player.attackText.enabled = !player.startsJoined;
            player.castText.enabled = false;
            player.breakWithSpellcode.enabled = false;

            if (!player.startsJoined)
            {
                player.attackText.text = "Join:\n[START]";
                player.attackText.GetComponent<TextSetter>().referenceString = "Join:\n[START]";
                player.attackText.GetComponent<TextSetter>().stringToReplace = "[START]";
                player.attackText.GetComponent<TextSetter>().defaultAction = startActionReference;
            }
            ReloadAllGlyphs(playerIndex);
            SetGambaActive(player, false, false);
        }
    }

    public void OnboardUpdate(ulong[] playerInputs)
    {
        if (gameManager == null || playerInputs == null)
        {
            return;
        }

        EnsureInputSnapshotCount(players.Count);

        int playerCount = Mathf.Min(
            players.Count,
            playerInputs.Length,
            gameManager.players.Length,
            gameManager.gates.Length);

        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            inputSnapshots[playerIndex] =
                InputConverter.ConvertFromLong(playerInputs[playerIndex]);

            if (!TryGetPlayerOnboarding(playerIndex, out PlayerOnboarding player))
            {
                continue;
            }

            if (gameManager.players[playerIndex] == null
                || !gameManager.IsPlayerSlotConnected(playerIndex))
            {
                // Sparse party gaps (for example empty P2 in a P1+P3 match) never receive input, so
                // their default "Join: [START]" prompt would otherwise remain visible forever.
                player.moveText.enabled = false;
                player.jumpText.enabled = false;
                player.attackText.enabled = false;
                player.castText.enabled = false;
                player.breakWithSpellcode.enabled = false;
                SetGambaActive(player, false, false);
                continue;
            }

            UpdatePlayerOnboarding(playerIndex, player);
        }
    }

    private void UpdatePlayerOnboarding(
        int playerIndex,
        PlayerOnboarding onboarding)
    {
        PlayerController player = gameManager.players[playerIndex];
        InputSnapshot input = inputSnapshots[playerIndex];

        if (!onboarding.joined)
        {
            onboarding.attackText.text = "Attack:\n[CODE]";
            onboarding.attackText.GetComponent<TextSetter>().referenceString = "Attack:\n[CODE]";
            onboarding.attackText.GetComponent<TextSetter>().stringToReplace = "[CODE]";
            onboarding.attackText.GetComponent<TextSetter>().defaultAction = attackActionReference;
            onboarding.attackText.enabled = false;
            onboarding.moveText.enabled = true;
            onboarding.jumpText.enabled = true;
            onboarding.joined = true;
        }
        ReloadAllGlyphs(playerIndex);

        if (!onboarding.moveComplete &&
            (input.Direction == 4 || input.Direction == 6))
        {
            onboarding.moveComplete = true;
            onboarding.moveText.color = GameManager.colors["green"];
            Debug.Log("Move Onboard Complete");
        }

        if (!onboarding.jumpComplete &&
            input.ButtonStates[1] == ButtonState.Pressed)
        {
            onboarding.jumpComplete = true;
            onboarding.jumpText.color = GameManager.colors["green"];
            Debug.Log("Jump Onboard Complete");
        }

        if (onboarding.moveComplete &&
            onboarding.jumpComplete &&
            !onboarding.attackComplete)
        {
            onboarding.moveText.enabled = false;
            onboarding.jumpText.enabled = false;
            onboarding.attackText.enabled = true;

            if (!onboarding.gambaActive)
            {
                SetGambaActive(onboarding, true, false);
            }

            if (player.basicsFired > 0)
            {
                onboarding.attackComplete = true;
                Debug.Log("Atk Onboard Complete");
            }
        }

        if (onboarding.attackComplete && player.spellList.Count == 0)
        {
            onboarding.attackText.enabled = false;
            onboarding.moveText.enabled = false;
            onboarding.jumpText.enabled = false;
        }

        if (player.spellList.Count > 0 && !onboarding.glassBroken)
        {
            ShowCastPrompt(playerIndex, onboarding, input);

            if (gameManager.gates[playerIndex].isOpen)
            {
                onboarding.glassBroken = true;
            }
        }

        if (onboarding.glassBroken)
        {
            CompleteOnboarding(playerIndex, onboarding);
        }
    }

    private static void SetGambaActive(
        PlayerOnboarding player,
        bool isActive,
        bool applyVisualState)
    {
        player.gambaActive = isActive;

        if (player.gamba == null)
        {
            return;
        }

        player.gamba.isActive = isActive;
        if (applyVisualState)
        {
            player.gamba.ApplyVisualState();
        }
    }

    private void EnsureInputSnapshotCount(int count)
    {
        while (inputSnapshots.Count < count)
        {
            inputSnapshots.Add(default);
        }
    }

    private void ShowCastPrompt(
        int playerIndex,
        PlayerOnboarding player,
        InputSnapshot input)
    {
        player.attackText.enabled = false;
        player.breakWithSpellcode.enabled = true;

        if (!player.castText.enabled)
        {
            int playerId = playerIndex + 1;
            Vector3 position = player.breakWithSpellcode.transform.position;
            FixedVec2 fixedPosition = new FixedVec2(
                Fixed.FromFloat(position.x),
                Fixed.FromFloat(position.y));

            VFX_Manager.Instance.PlayVisualEffect(
                VisualEffects.GRAFFITI_SPAWN,
                fixedPosition,
                playerId);
            VFX_Manager.Instance.PlayVisualEffect(
                VisualEffects.GRAFFITI_DRIP,
                fixedPosition,
                playerId);
        }

        player.castText.enabled = true;
        player.castText.text =
            input.ButtonStates[0] == ButtonState.Held ? "Input Code" : "Hold";
    }

    private void CompleteOnboarding(
        int playerIndex,
        PlayerOnboarding player)
    {
        player.castText.color = GameManager.colors["green"];
        player.castText.enabled = false;
        player.breakWithSpellcode.enabled = false;

        StopGraffitiDrip(playerIndex);
    }

    private static void StopGraffitiDrip(int playerIndex)
    {
        VFX_Manager.Instance.StopVisualEffect(
            VisualEffects.GRAFFITI_DRIP,
            playerIndex + 1);
    }

    private bool TryGetPlayerOnboarding(
        int playerIndex,
        out PlayerOnboarding player)
    {
        if (playerIndex >= 0 &&
            playerIndex < players.Count &&
            players[playerIndex] != null)
        {
            player = players[playerIndex];
            return true;
        }

        player = null;
        return false;
    }
}
