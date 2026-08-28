using System;
using System.Collections.Generic;
using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

/// <summary>
/// Training room machine that opens the training options panel when the owner hits it with their
/// basic projectile, then drives that panel off the owner's simulation input: up/down moves the
/// cursor, jump submits, code backs out. Toggle rows flip on submit, value rows enter an edit state
/// where left/right change the value.
///
/// The machine also owns the option state and re-applies it every simulated frame, so a resource the
/// player dialled in holds instead of being decayed or recomputed back out from under them by the
/// universal passives (demon aura falls off, stock stability is rebuilt OnStart, reps clear on
/// respawn). Every value option starts on a "Normal" step that means "don't touch it", so the
/// training room plays exactly like a real match until something is deliberately dialled in.
///
/// Training is an offline scene, so none of this runs during an online match.
/// </summary>
public class TrainingOptionsMachine : MonoBehaviour
{
    // Must stay in the same order as TrainingOptionsUI.RowNames.
    public enum Option
    {
        Cooldowns,
        FlowState,
        DemonAura,
        Reps,
        StockStability,
        AiBehavior,
        Hitboxes
    }

    private const int OptionCount = 7;
    private const int NeutralDirection = 5;
    private const int UpDirection = 8;
    private const int DownDirection = 2;
    private const int LeftDirection = 4;
    private const int RightDirection = 6;

    // Frames a direction must be held before it starts auto repeating, and the gap between repeats.
    private const int DirectionRepeatDelay = 20;
    private const int DirectionRepeatInterval = 5;

    // Mirrors TempUIScript.demonAuraGradeVals, index n is demon aura n * 20.
    private static readonly string[] DemonAuraGrades = { "D", "C", "B", "A", "S", "X" };
    private const int StockStabilityStep = 10;
    private const int StockStabilityStepCount = 11; // 0% through 100%

    // Step 0 of every value option: leave the resource alone and let the match play out normally.
    private const string NormalLabel = "Normal";

    // Mirrors the code weave loop PlayerController starts on entering CodeWeave. It stops that loop
    // on exiting CodeRelease, which a player frozen by this panel never reaches.
    private const float CodeWeaveLoopRate = 0.42f;
    private const float CodeWeaveLoopMinPitch = 0.8f;
    private const float CodeWeaveLoopMaxPitch = 1.2f;

    private static readonly List<TrainingOptionsMachine> instances = new List<TrainingOptionsMachine>();

    [Header("Machine")]
    public Animator machineAnimator;
    public bool isActive = true;
    public bool facingRight = true;
    public int ownerPID = 1;
    public PlayerController ownerPlayer = null;
    public HurtboxData hurtbox = new HurtboxData();
    public float colliderRadius = 16f;
    [Tooltip("Frames before the machine can be hit again after the panel closes.")]
    public int reactivateDelay = 60;

    [Header("Panel")]
    [Tooltip("Panel instance to drive. Leave empty to spawn one from uiPrefab.")]
    public TrainingOptionsUI ui;
    [Tooltip("Spawned under uiParent the first time the panel is needed, if ui is empty.")]
    public TrainingOptionsUI uiPrefab;
    [Tooltip("Where a spawned panel is parented. Defaults to the canvas TempUI lives under.")]
    public Transform uiParent;

    [Header("AI Behavior")]
    [Tooltip("Optional. Pulls targetNPC and npcBehaviors from an existing AI machine so they only need wiring once.")]
    public AIMachine aiMachine;
    public PlayerController targetNPC = null;
    public List<NpcAI> npcBehaviors = new List<NpcAI>();

    [Header("Option Limits")]
    public int maxReps = 20;

    // Option state. Index 0 of every value option is "Off", meaning the machine leaves that
    // resource alone.
    private bool cooldownsEnabled = true;
    private bool flowStateForced = false;
    private int demonAuraIndex = 0;
    private int repsIndex = 0;
    private int stockStabilityIndex = 0;
    private int aiBehaviorIndex = 0;

    private bool menuOpen = false;
    private int selectedIndex = 0;
    private bool editing = false;
    private int heldDirection = NeutralDirection;
    private int directionHoldFrames = 0;
    private int resetTimer = 0;
    private GameManager gameManager;
    private bool spawnedOwnUI = false;
    private bool warnedPanelUnavailable = false;

    public bool IsMenuOpen => menuOpen;

    void OnEnable()
    {
        if (!instances.Contains(this))
        {
            instances.Add(this);
        }
    }

    void OnDisable()
    {
        instances.Remove(this);
        if (menuOpen)
        {
            CloseMenu();
        }
    }

    void Start()
    {
        gameManager = GameManager.Instance;
        hurtbox = new HurtboxData() { height = 48, width = 20, xOffset = -10, yOffset = 48 };

        if (aiMachine != null)
        {
            if (targetNPC == null)
            {
                targetNPC = aiMachine.targetNPC;
            }

            if ((npcBehaviors == null || npcBehaviors.Count == 0) && aiMachine.npcBehaviors != null)
            {
                npcBehaviors = aiMachine.npcBehaviors;
            }
        }

        ResolvePanel();

        InitializeAiBehavior();
        ApplyVisualState();
    }

    /// <summary>
    /// Picks the dummy's starting behaviour and makes it live. This machine took the job over from
    /// AIMachine, which assigned npcAI in its own Start but never set owner, so anything other than
    /// Idle sat inert until the machine had been shot once.
    /// </summary>
    private void InitializeAiBehavior()
    {
        if (targetNPC == null || npcBehaviors == null || npcBehaviors.Count == 0)
        {
            return;
        }

        // Keep whatever the dummy was already pointed at if it is one of ours, otherwise start at
        // the top of the list.
        int existingIndex = targetNPC.npcAI != null ? npcBehaviors.IndexOf(targetNPC.npcAI) : -1;
        aiBehaviorIndex = existingIndex >= 0 ? existingIndex : 0;

        if (npcBehaviors[aiBehaviorIndex] == null)
        {
            return;
        }

        targetNPC.npcAI = npcBehaviors[aiBehaviorIndex];
        targetNPC.npcAI.owner = targetNPC;
    }

    void OnDestroy()
    {
        // The panel is parented to the persistent UI canvas, so a machine that spawned its own has
        // to take it back down when the arena it belongs to goes away.
        if (spawnedOwnUI && ui != null)
        {
            Destroy(ui.gameObject);
            ui = null;
        }
    }

    /// <summary>
    /// Returns the panel this machine drives, spawning one from uiPrefab the first time if no
    /// instance was wired by hand. Spawning is what lets the machine be dropped into an arena
    /// without a cross-prefab reference to the canvas the panel has to live under.
    /// </summary>
    private TrainingOptionsUI ResolvePanel()
    {
        if (ui != null)
        {
            ui.EnsureRows();
            ui.SetVisible(false);
            return ui;
        }

        if (uiPrefab == null)
        {
            WarnPanelUnavailable("no uiPrefab is assigned, and no panel instance is wired into ui");
            return null;
        }

        Transform parent = ResolvePanelParent();
        if (parent == null)
        {
            WarnPanelUnavailable("no canvas could be found to parent it under, assign uiParent");
            return null;
        }

        ui = Instantiate(uiPrefab, parent, false);
        ui.gameObject.name = uiPrefab.gameObject.name;
        spawnedOwnUI = true;
        ui.EnsureRows();
        ui.SetVisible(false);
        return ui;
    }

    /// <summary>
    /// Finds the canvas a spawned panel hangs off. The Canvas is a CHILD of TempUI, not an ancestor
    /// of it, so this searches downward first.
    /// </summary>
    private Transform ResolvePanelParent()
    {
        if (uiParent != null)
        {
            return uiParent;
        }

        TempUIScript tempUI = GameManager.Instance != null ? GameManager.Instance.tempUI : null;
        if (tempUI == null)
        {
            return null;
        }

        Canvas canvas = tempUI.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            canvas = tempUI.GetComponentInParent<Canvas>(true);
        }

        if (canvas == null)
        {
            return null;
        }

        // TempUI nests canvases inside itself, always hang the panel off the outermost one.
        Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        return rootCanvas.transform;
    }

    /// <summary>
    /// A machine that can't find its panel just swallows every hit, which is indistinguishable from
    /// the hit detection not firing. Say so once instead of failing silently.
    /// </summary>
    private void WarnPanelUnavailable(string reason)
    {
        if (warnedPanelUnavailable)
        {
            return;
        }

        warnedPanelUnavailable = true;
        Debug.LogWarning($"{name}: training options panel unavailable, {reason}.");
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (ownerPlayer == null && gameManager.players != null &&
            ownerPID >= 1 && ownerPID <= gameManager.players.Length)
        {
            ownerPlayer = gameManager.players[ownerPID - 1];
        }

        ApplyVisualState();

        // While the panel is up it is driven from PlayerUpdate (so it reads the same input snapshot
        // the sim does, on the same frame), there is nothing to poll here.
        if (menuOpen)
        {
            return;
        }

        if (isActive && CheckHitboxCollision())
        {
            OpenMenu();
            return;
        }

        if (!isActive)
        {
            resetTimer++;

            if (resetTimer > reactivateDelay)
            {
                isActive = true;
                resetTimer = 0;
            }
        }
    }

    /// <summary>
    /// Called from PlayerController.PlayerUpdate. Returns true when this player's panel is up, which
    /// tells PlayerUpdate to freeze them for the frame. Stays true on the frame the panel closes so
    /// the back press can't also be read as a real code input.
    /// </summary>
    public static bool HandleMenuInput(PlayerController player)
    {
        if (player == null || (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive))
        {
            return false;
        }

        for (int i = 0; i < instances.Count; i++)
        {
            TrainingOptionsMachine machine = instances[i];
            if (machine == null || !machine.menuOpen || machine.ownerPlayer != player)
            {
                continue;
            }

            machine.UpdateMenu();
            return true;
        }

        return false;
    }

    public static bool IsMenuOpenFor(PlayerController player)
    {
        if (player == null)
        {
            return false;
        }

        for (int i = 0; i < instances.Count; i++)
        {
            TrainingOptionsMachine machine = instances[i];
            if (machine != null && machine.menuOpen && machine.ownerPlayer == player)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Re-applies every pinned resource. Called at the end of GameManager.UpdateGameState, after
    /// ProcEffectUpdate, so the per-frame decay inside the universal passives can't undo it.
    /// </summary>
    public static void ApplyAllOverrides()
    {
        if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null)
            {
                instances[i].ApplyOverrides();
            }
        }
    }

    private void ApplyOverrides()
    {
        if (ownerPlayer == null)
        {
            return;
        }

        if (!cooldownsEnabled)
        {
            ClearCooldowns(ownerPlayer.spellList);
            ClearCooldowns(ownerPlayer.universalSpells);
        }

        if (flowStateForced)
        {
            ownerPlayer.flowState = FlowState.maxFlowState;
        }

        if (demonAuraIndex > 0)
        {
            ownerPlayer.demonAura = (ushort)((demonAuraIndex - 1) * 20);
            // Refresh the falloff timer too, otherwise DemonAura's OnUpdate proc starts counting the
            // aura back down the moment the player stops dealing damage.
            ownerPlayer.demonAuraLifeSpanTimer = DemonAura.DemonAuraResetTime;
        }

        if (repsIndex > 0)
        {
            ownerPlayer.reps = (ushort)(repsIndex - 1);
        }

        if (stockStabilityIndex > 0)
        {
            ushort stockStability = (ushort)((stockStabilityIndex - 1) * StockStabilityStep);
            ownerPlayer.stockStability = stockStability;
            ownerPlayer.stockStabilityModified = stockStability;
        }
    }

    /// <summary>
    /// Stock stability is only ever recalculated by StockStability's OnStart proc, which fires on
    /// spawn and on every spell pickup. Dropping the override would otherwise leave the last pinned
    /// value stuck until one of those happened, so re-fire the proc to put the real value back now.
    /// StockStability is the only spell that procs OnStart, so nothing else is disturbed.
    /// </summary>
    private void RestoreNaturalStockStability()
    {
        if (ownerPlayer == null)
        {
            return;
        }

        ownerPlayer.CheckAllSpellConditionsOfProcCon(ownerPlayer, ProcCondition.OnStart);
    }

    private static void ClearCooldowns(List<SpellData> spells)
    {
        if (spells == null)
        {
            return;
        }

        for (int i = 0; i < spells.Count; i++)
        {
            if (spells[i] != null)
            {
                spells[i].cooldownCounter = 0;
            }
        }
    }

    private void OpenMenu()
    {
        // Resolved lazily as well as in Start, the canvas the panel is parented to may not have
        // existed yet the first time this machine woke up.
        if (ownerPlayer == null || ResolvePanel() == null)
        {
            return;
        }

        menuOpen = true;
        isActive = false;
        resetTimer = 0;
        selectedIndex = 0;
        editing = false;
        // Seed from whatever the player is already holding (they were moving when they shot the
        // machine) so a held stick doesn't count as a fresh press on the first frame.
        heldDirection = ownerPlayer.input.Direction;
        directionHoldFrames = 0;

        SyncAiBehaviorIndexFromTarget();
        SetCodeWeaveLoopActive(false);

        ui.SetVisible(true);
        RefreshUI();

        if (SFX_Manager.Instance != null)
        {
            SFX_Manager.Instance.PlaySound(Sounds.CLEAR_MACHINE_HIT, 1.0f, 1.0f);
        }
    }

    private void CloseMenu()
    {
        menuOpen = false;
        editing = false;
        SetCodeWeaveLoopActive(true);
        // Re-arm on the usual timer so the shot that opened the panel can't immediately reopen it.
        isActive = false;
        resetTimer = 0;

        if (ui != null)
        {
            ui.SetVisible(false);
        }
    }

    /// <summary>
    /// Freezing PlayerUpdate strands a player who was mid code weave: they never reach the
    /// CodeRelease exit that stops the looping weave sound, so it repeats for as long as the panel
    /// is up. Silence it on open, and put it back on close only if they are still weaving, which
    /// leaves the normal stop path to fire as usual once they unfreeze.
    /// </summary>
    private void SetCodeWeaveLoopActive(bool active)
    {
        if (ownerPlayer == null || SFX_Manager.Instance == null || GameManager.Instance == null)
        {
            return;
        }

        int playerIndex = Array.IndexOf(GameManager.Instance.players, ownerPlayer);
        if (playerIndex < 0)
        {
            return;
        }

        if (!active)
        {
            SFX_Manager.Instance.StopRepeatingSound(Sounds.CONTINUOUS_CODE_WEAVE, playerIndex);
            return;
        }

        if (ownerPlayer.state == PlayerState.CodeWeave)
        {
            SFX_Manager.Instance.StartRepeatingSound(
                Sounds.CONTINUOUS_CODE_WEAVE,
                CodeWeaveLoopRate,
                playerIndex,
                CodeWeaveLoopMinPitch,
                CodeWeaveLoopMaxPitch);
        }
    }

    private void UpdateMenu()
    {
        if (ownerPlayer == null)
        {
            CloseMenu();
            return;
        }

        // The ` key toggles hitboxes too and keeps working while the panel is up, so re-read that
        // one row every frame instead of only on menu events. Just the check box, refreshing the
        // whole panel per frame would rebuild the value strings for nothing.
        if (ui != null)
        {
            ui.SetRowToggle((int)Option.Hitboxes, BoxRenderer.RenderBoxes);
        }

        InputSnapshot input = ownerPlayer.input;
        ButtonState[] buttons = input.ButtonStates;

        bool backPressed = buttons != null && buttons.Length > 0 && buttons[0] == ButtonState.Pressed;
        bool submitPressed = buttons != null && buttons.Length > 1 && buttons[1] == ButtonState.Pressed;

        int direction = input.Direction;
        bool directionTriggered = TrackDirection(direction);

        if (backPressed)
        {
            PlayMenuSound("Negative Select");

            if (editing)
            {
                editing = false;
                RefreshUI();
            }
            else
            {
                CloseMenu();
            }

            return;
        }

        if (submitPressed)
        {
            Submit();
            return;
        }

        if (!directionTriggered)
        {
            return;
        }

        if (!editing)
        {
            if (direction == UpDirection)
            {
                MoveSelection(-1);
            }
            else if (direction == DownDirection)
            {
                MoveSelection(1);
            }

            return;
        }

        if (direction == LeftDirection)
        {
            AdjustSelected(-1);
        }
        else if (direction == RightDirection)
        {
            AdjustSelected(1);
        }
    }

    /// <summary>
    /// True on the frame a direction is first held, and again on each auto repeat tick.
    /// </summary>
    private bool TrackDirection(int direction)
    {
        if (direction != heldDirection)
        {
            heldDirection = direction;
            directionHoldFrames = 0;
            return direction != NeutralDirection;
        }

        if (direction == NeutralDirection)
        {
            return false;
        }

        directionHoldFrames++;

        if (directionHoldFrames < DirectionRepeatDelay)
        {
            return false;
        }

        return (directionHoldFrames - DirectionRepeatDelay) % DirectionRepeatInterval == 0;
    }

    private void MoveSelection(int delta)
    {
        selectedIndex = (selectedIndex + delta + OptionCount) % OptionCount;
        PlayMenuSound("Neutral Select");
        RefreshUI();
    }

    private void Submit()
    {
        switch ((Option)selectedIndex)
        {
            case Option.Cooldowns:
                cooldownsEnabled = !cooldownsEnabled;
                PlayMenuSound("Positive Select");
                break;

            case Option.FlowState:
                flowStateForced = !flowStateForced;
                if (!flowStateForced && ownerPlayer != null)
                {
                    ownerPlayer.flowState = 0;
                }
                PlayMenuSound("Positive Select");
                break;

            case Option.Hitboxes:
                // No stored copy of this one: the ` key flips the same flag, so the row reads and
                // writes it directly rather than keeping a second source of truth that can drift.
                BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
                PlayMenuSound("Positive Select");
                break;

            default:
                // Value rows arm left/right instead of doing anything on their own.
                editing = !editing;
                PlayMenuSound(editing ? "Positive Select" : "Neutral Select");
                break;
        }

        RefreshUI();
    }

    private void AdjustSelected(int delta)
    {
        bool changed = false;

        switch ((Option)selectedIndex)
        {
            case Option.DemonAura:
            {
                int next = Mathf.Clamp(demonAuraIndex + delta, 0, DemonAuraGrades.Length);
                changed = next != demonAuraIndex;
                demonAuraIndex = next;
                break;
            }

            case Option.Reps:
            {
                int next = Mathf.Clamp(repsIndex + delta, 0, Mathf.Max(0, maxReps) + 1);
                changed = next != repsIndex;
                repsIndex = next;
                break;
            }

            case Option.StockStability:
            {
                int next = Mathf.Clamp(stockStabilityIndex + delta, 0, StockStabilityStepCount);
                changed = next != stockStabilityIndex;
                stockStabilityIndex = next;
                if (changed && stockStabilityIndex == 0)
                {
                    RestoreNaturalStockStability();
                }
                break;
            }

            case Option.AiBehavior:
                changed = CycleAiBehavior(delta);
                break;
        }

        if (changed)
        {
            PlayMenuSound("Tab Select");
            RefreshUI();
        }
    }

    private bool CycleAiBehavior(int delta)
    {
        if (targetNPC == null || npcBehaviors == null || npcBehaviors.Count == 0)
        {
            return false;
        }

        int count = npcBehaviors.Count;
        int next = ((aiBehaviorIndex + delta) % count + count) % count;
        if (next == aiBehaviorIndex)
        {
            return false;
        }

        aiBehaviorIndex = next;
        ApplyAiBehavior();
        return true;
    }

    private void ApplyAiBehavior()
    {
        if (targetNPC == null || npcBehaviors == null ||
            aiBehaviorIndex < 0 || aiBehaviorIndex >= npcBehaviors.Count ||
            npcBehaviors[aiBehaviorIndex] == null)
        {
            return;
        }

        targetNPC.npcAI = npcBehaviors[aiBehaviorIndex];
        targetNPC.npcAI.owner = targetNPC;

        // Respawn so the dummy drops whatever state the previous behaviour left it in, same as
        // AIMachine does when it cycles.
        Vector2[] npcSpawns = GameManager.Instance != null ? GameManager.Instance.GetNPCSpawnPositions() : null;
        if (npcSpawns != null && npcSpawns.Length > 0)
        {
            targetNPC.SpawnPlayer(FixedVec2.FromFloat(npcSpawns[0].x, npcSpawns[0].y));
        }

        targetNPC.SpawnToast(npcBehaviors[aiBehaviorIndex].BehaviorName, GameManager.colors["white"]);
    }

    private void SyncAiBehaviorIndexFromTarget()
    {
        if (targetNPC == null || targetNPC.npcAI == null || npcBehaviors == null)
        {
            return;
        }

        int index = npcBehaviors.IndexOf(targetNPC.npcAI);
        if (index >= 0)
        {
            aiBehaviorIndex = index;
        }
    }

    private void RefreshUI()
    {
        if (ui == null)
        {
            return;
        }

        // The Cooldowns row is labelled "Cooldowns Off", so its check box is inverted against the
        // state it drives: ticked means cooldowns are disabled. Flow State is labelled plainly and
        // reads straight.
        ui.SetRowToggle((int)Option.Cooldowns, !cooldownsEnabled);
        ui.SetRowToggle((int)Option.FlowState, flowStateForced);
        ui.SetRowToggle((int)Option.Hitboxes, BoxRenderer.RenderBoxes);

        ui.SetRowValue((int)Option.DemonAura, demonAuraIndex == 0 ? NormalLabel : DemonAuraGrades[demonAuraIndex - 1]);
        ui.SetRowValue((int)Option.Reps, repsIndex == 0 ? NormalLabel : (repsIndex - 1).ToString());
        ui.SetRowValue((int)Option.StockStability,
            stockStabilityIndex == 0 ? NormalLabel : ((stockStabilityIndex - 1) * StockStabilityStep) + "%");
        ui.SetRowValue((int)Option.AiBehavior, GetAiBehaviorName());

        for (int i = 0; i < OptionCount; i++)
        {
            bool selected = i == selectedIndex;
            ui.SetRowHighlight(i, selected, selected && editing);
        }
    }

    private string GetAiBehaviorName()
    {
        if (npcBehaviors == null || aiBehaviorIndex < 0 || aiBehaviorIndex >= npcBehaviors.Count ||
            npcBehaviors[aiBehaviorIndex] == null)
        {
            return "None";
        }

        return npcBehaviors[aiBehaviorIndex].BehaviorName;
    }

    private static void PlayMenuSound(string soundName)
    {
        if (SFX_Manager.Instance != null)
        {
            SFX_Manager.Instance.PlayMenuSound(soundName);
        }
    }

    public void ApplyVisualState()
    {
        if (machineAnimator == null)
        {
            machineAnimator = GetComponent<Animator>();
        }

        if (machineAnimator == null)
        {
            return;
        }

        machineAnimator.SetBool("facingLeft", !facingRight);
        machineAnimator.SetBool("isActive", isActive);
    }

    public bool CheckHitboxCollision()
    {
        HitboxManager hitboxManager = HitboxManager.Instance;
        if (ownerPlayer == null || hitboxManager == null)
        {
            return false;
        }

        Vector3 machinePosition = transform.position;
        return hitboxManager.ProcessPlayerBasicAttackCollision(ownerPlayer, hurtbox,
            FixedVec2.FromFloat(machinePosition.x, machinePosition.y), true);
    }
}
