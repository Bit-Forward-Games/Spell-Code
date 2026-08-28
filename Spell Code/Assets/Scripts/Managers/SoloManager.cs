using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using UnityEngine.Video;

/// </summary>
public class SoloManager : MonoBehaviour
{
    [Header("Attract Mode")]
    [Tooltip("Seconds without any input before the video takes over. Set to 0 or less to turn attract mode off.")]
    [SerializeField] private float idleSecondsBeforeVideo = 30f;

    [Tooltip("The gameplay video to play. Leave empty to turn attract mode off.")]
    [SerializeField] private VideoClip attractVideo;

    [Tooltip("Loop the video until the player presses something. Off = play it once, then return to the lobby and start counting again.")]
    [SerializeField] private bool loopVideo = true;

    [Tooltip("Play the video's own audio. The lobby music is paused while the video runs and resumes when it stops.")]
    [SerializeField] private bool playVideoAudio = true;

    [Tooltip("Seconds the video fades in and out over.")]
    [SerializeField] private float fadeSeconds = 0.4f;

    [Tooltip("How far a stick has to move to count as activity. Raise this if a drifting controller keeps waking the lobby.")]
    [SerializeField] private float stickDeadzone = 0.35f;

    private float idleTimer;
    private bool isPlaying;
    private bool bgmPaused;

    private GameObject overlayRoot;
    private CanvasGroup overlayGroup;
    private RawImage videoImage;
    private AspectRatioFitter aspectFitter;
    private VideoPlayer videoPlayer;

    private IDisposable anyButtonSubscription;
    private bool buttonPressedThisFrame;
    private bool videoEventsRegistered;
    private bool videoReleased;
    private bool suppressPauseUntilInputReleased;

    private static bool attractBlockingPause;

    /// <summary>
    /// True while the attract video owns the Escape/Start press. Pause.CanOpenPauseMenu checks this
    /// so the press that dismisses the video can't also open Pause behind it
    /// </summary>
    public static bool IsBlockingPause => attractBlockingPause;

    /// <summary>
    /// Statics survive a play session when Enter Play Mode Options disables domain reload, and a
    /// stranded true here would mean the pause menu never opens again. Cheap insurance.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        attractBlockingPause = false;
    }

    private void OnEnable()
    {
        // onAnyButtonPress covers every button-like control on every device - keyboard keys, mouse
        // buttons, gamepad face/shoulder/trigger. Sticks and mouse movement are polled separately
        // below, since those aren't buttons.
        anyButtonSubscription = InputSystem.onAnyButtonPress.Call(_ => buttonPressedThisFrame = true);
    }

    private void OnDisable()
    {
        anyButtonSubscription?.Dispose();
        anyButtonSubscription = null;

        // Leaving SoloLobby must never strand the block on, or Pause would be dead for the rest of
        // the session.
        ClearPauseSuppression();
    }

    private void Update()
    {
        UpdatePauseSuppression();

        bool activity = ConsumeActivity();

        if (isPlaying)
        {
            if (activity)
            {
                StopAttract();
            }
            else
            {
                FadeOverlayToward(1f);
            }
            return;
        }

        FadeOverlayToward(0f);

        // Any activity, or any reason attract mode shouldn't run right now, restarts the count from
        // zero rather than just pausing it.
        if (activity || !CanStartAttract())
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.unscaledDeltaTime;
        if (idleTimer >= idleSecondsBeforeVideo)
        {
            StartAttract();
        }
    }

    private void UpdatePauseSuppression()
    {
        if (isPlaying)
        {
            attractBlockingPause = true;
            return;
        }

        if (!suppressPauseUntilInputReleased)
        {
            attractBlockingPause = false;
            return;
        }

        // Hold the block until the dismissing press is physically released. PlayerController reads
        // the pause action from FixedUpdate, which can see that same press on a later step than the
        // Update that dismissed the video here, so clearing on the edge alone isn't enough.
        if (IsPauseInputHeld())
        {
            attractBlockingPause = true;
            return;
        }

        suppressPauseUntilInputReleased = false;
        attractBlockingPause = false;
    }

    /// <summary>
    /// The physical pair behind the pause action: keyboard Escape and every gamepad's Start button.
    /// </summary>
    private static bool IsPauseInputHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.isPressed)
        {
            return true;
        }

        ReadOnlyArray<Gamepad> pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            Gamepad pad = pads[i];
            if (pad != null && pad.startButton.isPressed)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearPauseSuppression()
    {
        suppressPauseUntilInputReleased = false;
        attractBlockingPause = false;
    }

    private bool ConsumeActivity()
    {
        bool pressed = buttonPressedThisFrame;
        buttonPressedThisFrame = false;
        return pressed || PollAnalogActivity();
    }

    /// <summary>
    /// Stick and mouse movement, which onAnyButtonPress doesn't report. Sticks are checked against
    /// stickDeadzone so a drifting controller can't hold the lobby awake forever.
    /// </summary>
    private bool PollAnalogActivity()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 4f)
        {
            return true;
        }

        float deadzoneSq = stickDeadzone * stickDeadzone;
        ReadOnlyArray<Gamepad> pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            Gamepad pad = pads[i];
            if (pad == null) continue;

            if (pad.leftStick.ReadValue().sqrMagnitude > deadzoneSq) return true;
            if (pad.rightStick.ReadValue().sqrMagnitude > deadzoneSq) return true;
            if (pad.dpad.ReadValue().sqrMagnitude > 0.01f) return true;
        }

        return false;
    }

    private bool CanStartAttract()
    {
        if (attractVideo == null || idleSecondsBeforeVideo <= 0f)
        {
            return false;
        }

        // Never take the screen while an online lobby is up. Someone waiting on friends to join, or
        // sitting in matchmaking, is idle by definition and would get the video thrown at them.
        SteamLobbyManager lobby = SteamLobbyManager.Instance;
        if (lobby != null && lobby.IsInLobby)
        {
            return false;
        }

        // Attract mode belongs to the press-to-start screen. Once P1 has joined, the player is
        // already inside the solo lobby; starting the video there would cover active gameplay.
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.playerCount > 0)
        {
            return false;
        }

        // A menu that froze time (pause, options, the tutorial prompt) means the player is present.
        if (Time.timeScale == 0f)
        {
            return false;
        }

        return true;
    }

    private bool ShouldPlayVideoAudio()
    {
        return playVideoAudio
            && attractVideo != null
            && attractVideo.audioTrackCount > 0;
    }

    private void StartAttract()
    {
        EnsureOverlay();
        if (videoPlayer == null)
        {
            return;
        }

        isPlaying = true;
        idleTimer = 0f;
        attractBlockingPause = true;
        // PlayerInputManager receives join actions independently of this MonoBehaviour's Update.
        // Disable it now, before the first input that dismisses attract mode can also join P1.
        GameManager.Instance?.BlockPlayerJoiningForAttractMode();
        overlayRoot.SetActive(true);

        if (ShouldPlayVideoAudio() && !bgmPaused && BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.PauseSong();
            bgmPaused = true;
        }

        videoPlayer.isLooping = loopVideo;

        if (videoPlayer.isPrepared)
        {
            videoPlayer.time = 0d;
            videoPlayer.Play();
        }
        else
        {
            // Prepare() is async; HandlePrepared starts playback. The fade-in covers the wait.
            videoPlayer.Prepare();
        }
    }

    private void StopAttract()
    {
        isPlaying = false;
        idleTimer = 0f;

        // Whatever just dismissed the video keeps Pause blocked until it's released.
        suppressPauseUntilInputReleased = true;
        attractBlockingPause = true;

        if (videoPlayer != null)
        {
            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }

            if (videoPlayer.isPrepared)
            {
                videoPlayer.time = 0d;
            }
        }

        ResumeBgmIfPaused();
        // The overlay GameObject is deactivated by FadeOverlayToward once it has faded out.
    }

    private void ResumeBgmIfPaused()
    {
        if (!bgmPaused)
        {
            return;
        }

        bgmPaused = false;
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.PlaySong();
        }
    }

    private void FadeOverlayToward(float target)
    {
        if (overlayGroup == null || overlayRoot == null)
        {
            return;
        }

        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        overlayGroup.alpha = Mathf.MoveTowards(overlayGroup.alpha, target, step);

        if (isPlaying)
        {
            // Keep the RawImage pointed at the decoder's output texture, which is only valid once
            // playback has actually started.
            if (videoImage != null && videoPlayer != null && videoPlayer.texture != null && videoImage.texture != videoPlayer.texture)
            {
                videoImage.texture = videoPlayer.texture;
            }
            return;
        }

        if (overlayGroup.alpha <= 0f && overlayRoot.activeSelf)
        {
            overlayRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Builds the fullscreen overlay the first time it's needed: a black backdrop so a video that
    /// doesn't match the screen aspect letterboxes cleanly, and a RawImage the VideoPlayer renders
    /// into. Left unparented so it dies with the scene.
    /// </summary>
    private void EnsureOverlay()
    {
        if (overlayRoot != null)
        {
            return;
        }

        overlayRoot = new GameObject("Attract Mode Overlay");

        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // MainMenuScreen is authored on the UI sorting layer at order 32766. The old attract
        // canvas used the Default layer at 32760, so the video played invisibly behind the title
        // screen and appeared for only the one frame where Escape hid that screen. Put this root
        // at the absolute front of the same UI layer.
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        overlayGroup = overlayRoot.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        // Purely decorative: it must never eat clicks meant for the lobby underneath.
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(overlayRoot.transform, false);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = Color.black;
        backdropImage.raycastTarget = false;
        StretchToParent(backdrop.GetComponent<RectTransform>());

        GameObject video = new GameObject("Video");
        video.transform.SetParent(overlayRoot.transform, false);
        videoImage = video.AddComponent<RawImage>();
        videoImage.raycastTarget = false;
        StretchToParent(video.GetComponent<RectTransform>());

        aspectFitter = video.AddComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectFitter.aspectRatio = 16f / 9f;

        videoPlayer = video.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        // Preserve every frame. This matches SpellVideoPlayer and avoids making a low/VFR source
        // look even choppier by dropping frames whenever the decoder briefly falls behind.
        videoPlayer.skipOnDrop = false;
        videoPlayer.isLooping = loopVideo;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        // Silent clips should leave the lobby BGM alone instead of making a held final frame feel
        // like the whole game froze.
        videoPlayer.audioOutputMode = ShouldPlayVideoAudio()
            ? VideoAudioOutputMode.Direct
            : VideoAudioOutputMode.None;
        videoPlayer.clip = attractVideo;

        if (!videoEventsRegistered)
        {
            videoPlayer.prepareCompleted += HandlePrepared;
            videoPlayer.errorReceived += HandleError;
            videoPlayer.loopPointReached += HandleLoopPoint;
            videoEventsRegistered = true;
        }

        overlayRoot.SetActive(false);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void HandlePrepared(VideoPlayer source)
    {
        if (source.texture != null && source.texture.height > 0)
        {
            aspectFitter.aspectRatio = source.texture.width / (float)source.texture.height;
        }

        if (videoImage != null)
        {
            videoImage.texture = source.texture;
        }

        // The player may have dismissed the video during the prepare, so only start if attract mode
        // is still the thing on screen.
        if (isPlaying)
        {
            source.time = 0d;
            source.Play();
        }
    }

    private void HandleLoopPoint(VideoPlayer source)
    {
        // Only reached when loopVideo is off: the clip ran to its end on its own, so drop back to
        // the lobby and start counting again.
        if (!loopVideo && isPlaying)
        {
            StopAttract();
        }
    }

    private void HandleError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[SoloManager] Could not play the attract video '{source.clip?.name}': {message}", this);
        StopAttract();
    }

    private void OnDestroy()
    {
        ReleaseVideo();
    }

    private void OnApplicationQuit()
    {
        // Mirrors SpellVideoPlayer: release the decoder before Unity starts tearing the process
        // down, rather than leaving an active MP4 worker to be killed mid-shutdown.
        ReleaseVideo();
    }

    private void ReleaseVideo()
    {
        if (videoReleased)
        {
            return;
        }
        videoReleased = true;

        isPlaying = false;
        ResumeBgmIfPaused();
        ClearPauseSuppression();

        anyButtonSubscription?.Dispose();
        anyButtonSubscription = null;

        if (videoPlayer == null)
        {
            return;
        }

        if (videoEventsRegistered)
        {
            videoPlayer.prepareCompleted -= HandlePrepared;
            videoPlayer.errorReceived -= HandleError;
            videoPlayer.loopPointReached -= HandleLoopPoint;
            videoEventsRegistered = false;
        }

        if (videoImage != null)
        {
            videoImage.texture = null;
        }

        videoPlayer.Stop();
        videoPlayer.clip = null;
        videoPlayer.enabled = false;
    }
}
