using TMPro;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
//using UnityEngine.AddressableAssets;
//using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.InputSystem;

public class GameSessionManager : NonPersistantSingleton<GameSessionManager>
{
    [Header("Player Vars")]
    public PlayerController[] playerControllers;

    [Header("Match Timer Vars")]
    public float matchTimer;
    [SerializeField, Range(0.1f, 5f)] private float timeScaleMultiplier = 1f;
    [SerializeField] private int startingTimeUnits = 99;
    [SerializeField] private float framesPerTimeUnit = 60f;
    private bool isMatchActive;
    private float cumulativeTime;

    private readonly List<RoundResult> roundResults = new();
    private readonly int[] playerWins = new int[2];

    [Header("UI Vars")]
    public TextMeshProUGUI matchTimerText;
    public BarController[] playerHealthbars;
    public TextMeshProUGUI countdownText;
    public WinDrawPipBehavior[] playerPipBehaviors;
    public TextMeshProUGUI matchAndRoundResultText;
    [SerializeField] private Image oMImgUImage;
    [SerializeField] private Image oMImgUImage2;
    [SerializeField] private Image omegaMeterImage1Filled;
    [SerializeField] private Image omegaMeterImage2Filled;

    [SerializeField] private SpriteRenderer background;
    public bool IsMatchActive => isMatchActive;
    public bool pauseOnHit = false;

    private void Awake()
    {
        // each Image gets its own material instance for per‐image glow
        oMImgUImage.material = Instantiate(oMImgUImage.material);
        oMImgUImage2.material = Instantiate(oMImgUImage2.material);
        omegaMeterImage1Filled.material = Instantiate(omegaMeterImage1Filled.material);
        omegaMeterImage2Filled.material = Instantiate(omegaMeterImage2Filled.material);
    }

    private void Start()
    {
        InitPalettes();
        SetupInputs();
        SetupCharacterVisuals();
        InitPlayers();
        StartMatch();
    }

    private void Update()
    {
        if (isMatchActive)
        {
            cumulativeTime += Time.deltaTime * timeScaleMultiplier;
            if (cumulativeTime >= 1f)
            {
                matchTimer = Mathf.Max(matchTimer - 1f, 0f);
                cumulativeTime -= 1f;
                matchTimerText.text = matchTimer.ToString();
                if (matchTimer == 0f) CheckForWinner();
            }

            UpdateOmegaMeterVisual(0, oMImgUImage, omegaMeterImage1Filled);
            UpdateOmegaMeterVisual(1, oMImgUImage2, omegaMeterImage2Filled);
        }

        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }
        if (Input.GetKeyDown(KeyCode.Period))
        {
            Debug.Log("slideshow time");
            pauseOnHit = !pauseOnHit;
        }
    }

    private void InitPalettes()
    {
        var p = GetComponent<AnimationManager>().paletteTextures;
        playerHealthbars[0].InitializePalette(p[0]);
        playerHealthbars[1].InitializePalette(p[1]);
    }

    private void SetupInputs()
    {
        if (!ConfigObject.IsReady)
        {
            playerControllers[0].inputs.AssignInputDevice(null);
            playerControllers[1].inputs.AssignInputDevice(null);
            return;
        }

        // Player 1
        AnimationManager.Instance.paletteTextures[0] = ConfigObject.Instance.playerOneTexture[0];
        if (ConfigObject.Instance.playerOneActionMap != null)
        {
            var map1 = new InputActionMap("Player1");
            SCUtils.AddBindingsToMap(map1.AddAction("Up"), ConfigObject.Instance.playerOneActionMap.FindAction("Up").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Down"), ConfigObject.Instance.playerOneActionMap.FindAction("Down").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Left"), ConfigObject.Instance.playerOneActionMap.FindAction("Left").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Right"), ConfigObject.Instance.playerOneActionMap.FindAction("Right").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Light"), ConfigObject.Instance.playerOneActionMap.FindAction("Light").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Medium"), ConfigObject.Instance.playerOneActionMap.FindAction("Medium").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Heavy"), ConfigObject.Instance.playerOneActionMap.FindAction("Heavy").bindings);
            SCUtils.AddBindingsToMap(map1.AddAction("Special"), ConfigObject.Instance.playerOneActionMap.FindAction("Special").bindings);
            playerControllers[0].inputs.AssignInputDevice(map1, ConfigObject.Instance.playerOneDevice);
        }
        else
        {
            playerControllers[0].inputs.AssignInputDevice(ConfigObject.Instance.playerOneDevice);
        }

        // Player 2
        AnimationManager.Instance.paletteTextures[1] = ConfigObject.Instance.playerTwoTexture[0];
        if (ConfigObject.Instance.playerTwoActionMap != null)
        {
            var map2 = new InputActionMap("Player2");
            SCUtils.AddBindingsToMap(map2.AddAction("Up"), ConfigObject.Instance.playerTwoActionMap.FindAction("Up").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Down"), ConfigObject.Instance.playerTwoActionMap.FindAction("Down").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Left"), ConfigObject.Instance.playerTwoActionMap.FindAction("Left").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Right"), ConfigObject.Instance.playerTwoActionMap.FindAction("Right").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Light"), ConfigObject.Instance.playerTwoActionMap.FindAction("Light").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Medium"), ConfigObject.Instance.playerTwoActionMap.FindAction("Medium").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Heavy"), ConfigObject.Instance.playerTwoActionMap.FindAction("Heavy").bindings);
            SCUtils.AddBindingsToMap(map2.AddAction("Special"), ConfigObject.Instance.playerTwoActionMap.FindAction("Special").bindings);
            playerControllers[1].inputs.AssignInputDevice(map2, ConfigObject.Instance.playerTwoDevice);
        }
        else
        {
            playerControllers[1].inputs.AssignInputDevice(ConfigObject.Instance.playerTwoDevice);
        }

        // Background
        if (ConfigObject.Instance.stageBackground != null)
        {
            background.sprite = ConfigObject.Instance.stageBackground;
        }
    }

    private void SetupCharacterVisuals()
    {
        if (!ConfigObject.IsReady) return;

        // Player 1 visuals
        playerControllers[0].characterName = ConfigObject.Instance.playerOneCharacter;
        Addressables.LoadAssetAsync<SpriteSheetData>(ConfigObject.Instance.playerOneCharacter)
            .Completed += (AsyncOperationHandle<SpriteSheetData> handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var data = handle.Result;
                    AnimationManager.Instance.spriteSheetData[0] = data;
                    AnimationManager.Instance.InitializePlayerVisuals(playerControllers[0], 0);
                    playerHealthbars[0].SetCharacterPortrait(data.Portrait);
                    playerHealthbars[0].InitializePalette(ConfigObject.Instance.playerOneTexture[0]);
                }
                else Debug.LogError("Failed to load SpriteSheetData for Player 1.");
            };

        // Player 2 visuals
        playerControllers[1].characterName = ConfigObject.Instance.playerTwoCharacter;
        Addressables.LoadAssetAsync<SpriteSheetData>(ConfigObject.Instance.playerTwoCharacter)
            .Completed += (AsyncOperationHandle<SpriteSheetData> handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var data = handle.Result;
                    AnimationManager.Instance.spriteSheetData[1] = data;
                    AnimationManager.Instance.InitializePlayerVisuals(playerControllers[1], 1);
                    playerHealthbars[1].SetCharacterPortrait(data.Portrait);
                    playerHealthbars[1].InitializePalette(ConfigObject.Instance.playerTwoTexture[0]);
                }
                else Debug.LogError("Failed to load SpriteSheetData for Player 2.");
            };
    }

    private void InitPlayers()
    {
        foreach (var pc in playerControllers)
        {
            pc.InitCharacter();
            pc.specialMoves.SetupSpecialMoves(pc.characterName);
            ProjectileManager.Instance.SetupPlayerProjectilesInPool(pc);
            pc.omegaInstalls.SetupInstall(pc.characterName);
        }

        if (ConfigObject.IsReady)
        {
            playerControllers[0].matchPalette[0] = ConfigObject.Instance.playerOneTexture[0];
            playerControllers[0].matchPalette[1] = ConfigObject.Instance.playerOneTexture[1];
            playerControllers[1].matchPalette[0] = ConfigObject.Instance.playerTwoTexture[0];
            playerControllers[1].matchPalette[1] = ConfigObject.Instance.playerTwoTexture[1];
        }
    }

    // Replace your existing StartMatch() with this:
    private void StartMatch()
    {
        foreach (var pc in playerControllers)
            pc.CheckForInputs(false);
        StartCoroutine(StartMatchRoutine());
    }

    private IEnumerator StartMatchRoutine()
    {

        playerControllers[0].position = StageData.Instance.p1pos;
        playerControllers[1].position = StageData.Instance.p2pos;
        ResetGlowEffect();

        oMImgUImage.fillAmount = 0f;
        oMImgUImage2.fillAmount = 0f;
        oMImgUImage.gameObject.SetActive(true);
        oMImgUImage2.gameObject.SetActive(true);
        omegaMeterImage1Filled.gameObject.SetActive(false);
        omegaMeterImage2Filled.gameObject.SetActive(false);

        ResetMatchTimer();
        ResetPlayersHealth();
        UpdateAllHealthDisplays();

        isMatchActive = false;                // ensure timer doesn’t start yet
        matchTimerText.gameObject.SetActive(true);
        countdownText.gameObject.SetActive(false);
        matchAndRoundResultText.text = "";

        // 3) Pre‐fight text
        matchAndRoundResultText.text = "TRANSFORMATION THE NAME OF THE GAME";
        yield return new WaitForSeconds(1f);
        matchAndRoundResultText.text = "ACTIVATE THE OMEGA CENTURION";
        yield return new WaitForSeconds(1.5f);

        matchAndRoundResultText.text = "";

        // 4) Finally enable inputs & start match
        foreach (var pc in playerControllers)
            pc.CheckForInputs(true);

        isMatchActive = true;
        matchTimerText.text = matchTimer.ToString();
        matchTimerText.gameObject.SetActive(true);
    }


    private void UpdateOmegaMeterVisual(int playerIndex, Image rawImage, Image filledImage)
    {
        float meter = playerControllers[playerIndex].omegaMeter;
        float fraction = meter / 5000f;
        bool isInstall = playerControllers[playerIndex].omegaInstall;

        if (!isInstall && meter < 5000f)
        {
            rawImage.fillAmount = fraction;
            rawImage.gameObject.SetActive(true);
            filledImage.gameObject.SetActive(false);
        }
        else
        {
            filledImage.fillAmount = isInstall ? fraction : 1f;
            filledImage.gameObject.SetActive(true);
            rawImage.gameObject.SetActive(false);
            SetGlowEffectMax(filledImage);
        }
    }

    private void SetGlowEffectMax(Image image)
    {
        image.material.shader = Shader.Find("Custom/SharedMeter");
        image.material.SetFloat("_GlowIntensity", 5.0f);
    }

    private void ResetGlowEffect()
    {
        omegaMeterImage1Filled.material.SetFloat("_GlowIntensity", 0.0f);
        omegaMeterImage2Filled.material.SetFloat("_GlowIntensity", 0.0f);
    }

    public void UpdatePlayerHealthText(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex > 1) return;
        float fill = 1f - (
            (float)playerControllers[playerIndex].currrentPlayerHealth
            / CharacterDataDictionary
                .GetCharacterData(playerControllers[playerIndex].characterName)
                .playerHealth
        );
        if (playerHealthbars[playerIndex].fillDirection == -1f)
            fill = 1f - fill;
        playerHealthbars[playerIndex].SetFillAmountHealth(fill * 0.97f);
        CheckForWinner();
    }

    public void UpdateMeterBar(int index)
    {
        if (index < 0 || index > 1) return;
        float frac = playerControllers[index].playerMeter / 5000f;
        playerHealthbars[index].SetFillAmountMeter(frac);
    }

    private void UpdateAllHealthDisplays()
    {
        foreach (var hb in playerHealthbars)
            hb.SetFillAmountHealth(1f);
    }

    private void ResetPlayersHealth()
    {
        foreach (var pc in playerControllers)
            pc.ResetHealth();
    }

    private void CheckForWinner()
    {
        if (!isMatchActive) return;

        uint p1h = playerControllers[0].currrentPlayerHealth;
        uint p2h = playerControllers[1].currrentPlayerHealth;
        uint p1m = playerControllers[0].GetMaxHealth();
        uint p2m = playerControllers[1].GetMaxHealth();

        var winners = new List<PlayerController>();

        // Double KO
        if (p1h == 0 && p2h == 0)
        {
            winners.Add(playerControllers[0]);
            winners.Add(playerControllers[1]);
            UpdatePips(winners[0]);
            UpdatePips(winners[1]);
            roundResults.Add(new RoundResult(winners, false));
            DenoteMatchStatus(winners);
            return;
        }

        // Single KO
        if (p1h == 0 || p2h == 0)
        {
            var win = p1h > 0 ? playerControllers[0] : playerControllers[1];
            winners.Add(win);
            UpdatePips(win);
            roundResults.Add(new RoundResult(winners, false));
            DenoteMatchStatus(winners);
            return;
        }

        // Timeout
        if (matchTimer == 0f)
        {
            float p1p = (float)p1h / p1m;
            float p2p = (float)p2h / p2m;
            if (Mathf.Abs(p1p - p2p) < 0.001f)
            {
                winners.Add(playerControllers[0]);
                winners.Add(playerControllers[1]);
                UpdatePips(winners[0]);
                UpdatePips(winners[1]);
                roundResults.Add(new RoundResult(winners, true));
            }
            else
            {
                var win = p1p > p2p ? playerControllers[0] : playerControllers[1];
                winners.Add(win);
                UpdatePips(win);
                roundResults.Add(new RoundResult(winners, true));
            }
            DenoteMatchStatus(winners);
        }
    }

    private void UpdatePips(PlayerController winner)
    {
        if (winner == playerControllers[0])
            playerPipBehaviors[0].AddWinToPips();
        else if (winner == playerControllers[1])
            playerPipBehaviors[1].AddWinToPips();
        else
        {
            playerPipBehaviors[0].AddWinToPips();
            playerPipBehaviors[1].AddWinToPips();
        }
    }

    private void DenoteMatchStatus(List<PlayerController> winners)
    {
        if (!isMatchActive) return;
        isMatchActive = true;

        if (winners.Count == 1)
        {
            if (winners[0] == playerControllers[0])
            {
                playerWins[0]++;
                matchAndRoundResultText.text = $"Player 1 wins round {roundResults.Count}";
            }
            else
            {
                playerWins[1]++;
                matchAndRoundResultText.text = $"Player 2 wins round {roundResults.Count}";
            }
        }
        else
        {
            playerWins[0]++;
            playerWins[1]++;
            matchAndRoundResultText.text = $"Draw round {roundResults.Count}";
        }

        // Match end?
        if (playerWins[0] >= 2 || playerWins[1] >= 2)
        {
            foreach (var pc in playerControllers) pc.CheckForInputs(false);

            if (playerWins[0] >= 2 && playerWins[1] >= 2)
                matchAndRoundResultText.text = "Draw Match!";
            else if (playerWins[0] >= 2)
                matchAndRoundResultText.text = "Player 1 Wins the Match!";
            else
                matchAndRoundResultText.text = "Player 2 Wins the Match!";

            StopMatch();
            StartCoroutine(LoadMenuScene());
        }
        else
        {
            isMatchActive = false;
            NextRound();
        }
    }

    private void StopMatch() => isMatchActive = false;

    private void NextRound()
    {
        if (roundResults.Count < 3)
            StartCoroutine(CountdownBeforeNextRound());
    }

    private IEnumerator LoadMenuScene()
    {
        yield return new WaitForSeconds(3f);
        matchAndRoundResultText.text = "";
        SceneManager.LoadScene("Menu", LoadSceneMode.Single);
    }

    private IEnumerator CountdownBeforeNextRound()
    {
        // Disable inputs and reset palettes
        foreach (var pc in playerControllers)
        {
            pc.CheckForInputs(false);
            pc.InitializePalette(pc.matchPalette[0]);
            foreach (var install in pc.omegaInstalls.OmegaInstalls)
                install.CleanUpOmega(pc);
        }

        // Reset velocity
        playerControllers[0].hSpd = playerControllers[1].hSpd = 0f;
        playerControllers[0].vSpd = playerControllers[1].vSpd = 0f;

        yield return new WaitForSeconds(1.5f);
        // Reset positions & projectiles
        playerControllers[0].position = StageData.Instance.p1pos;
        playerControllers[1].position = StageData.Instance.p2pos;
        ProjectileManager.Instance.ClearProjectilePool();
        UpdateAllHealthDisplays();
        ResetMatchTimer();

        countdownText.gameObject.SetActive(true);
        matchAndRoundResultText.text = "3";
        yield return new WaitForSeconds(1f);
        playerControllers[0].omegaInstall = playerControllers[1].omegaInstall = false;
        matchAndRoundResultText.text = "2";
        yield return new WaitForSeconds(1f);
        matchAndRoundResultText.text = "1";
        yield return new WaitForSeconds(1f);
        matchAndRoundResultText.text = "FIGHT";
        isMatchActive = true;
        yield return new WaitForSecondsRealtime(0.5f);

        matchTimerText.text = matchTimer.ToString();
        ResetPlayersHealth();
        foreach (var pc in playerControllers) pc.CheckForInputs(true);
        matchAndRoundResultText.text = "";
        matchTimerText.gameObject.SetActive(true);
    }

    private void ResetMatchTimer()
    {
        matchTimerText.gameObject.SetActive(false);
        matchTimer = startingTimeUnits;
        matchTimerText.gameObject.SetActive(true);
    }

    private struct RoundResult
    {
        public List<PlayerController> Winners { get; }
        public bool WonByTimeout { get; }

        public RoundResult(List<PlayerController> winners, bool wonByTimeout)
        {
            Winners = winners;
            WonByTimeout = wonByTimeout;
        }

        public override string ToString() =>
            Winners.Count == 2
                ? "Round Draw"
                : $"{Winners[0].name} won {(WonByTimeout ? "by Timeout" : "by KO")}";
    }
}
