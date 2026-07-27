using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class SpellVideoPlayer : MonoBehaviour
{
    [Tooltip("Drag an MP4 VideoClip here to preview it.")]
    public VideoClip Video;

    [SerializeField]
    [Tooltip("Keep decoding while the output is hidden so it can be revealed without playback startup delay.")]
    private bool keepWarmWhileHidden;

    private VideoPlayer videoPlayer;
    private RawImage videoImage;
    private Renderer videoRenderer;
    private bool eventsRegistered;
    private bool isPreparing;
    private bool playWhenPrepared;
    private bool hasBeenDisabled;
    private bool resumeAfterEnable;

    public bool IsPrepared =>
        videoPlayer != null
        && videoPlayer.clip == Video
        && videoPlayer.isPrepared;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!hasBeenDisabled && Video != null)
        {
            PlayFromBeginning();
        }
        else if (resumeAfterEnable)
        {
            PlayFromBeginning();
        }
        else if (Video != null)
        {
            Preload(Video);
        }
    }

    private void Update()
    {
        if (videoImage != null
            && videoPlayer != null
            && videoPlayer.texture != null
            && videoImage.texture != videoPlayer.texture)
        {
            videoImage.texture = videoPlayer.texture;
        }
    }

    private void OnDisable()
    {
        resumeAfterEnable = videoPlayer != null
            && (videoPlayer.isPlaying || playWhenPrepared);
        hasBeenDisabled = true;
        isPreparing = false;
        PauseAtBeginning();
    }

    private void OnDestroy()
    {
        if (videoPlayer == null || !eventsRegistered)
        {
            return;
        }

        videoPlayer.prepareCompleted -= HandlePrepared;
        videoPlayer.errorReceived -= HandleError;
    }

    public void SetVideo(VideoClip clip)
    {
        EnsureComponents();

        if (Video == clip && videoPlayer.clip == clip)
        {
            return;
        }

        AssignClip(clip);

        if (isActiveAndEnabled && Application.isPlaying)
        {
            PlayFromBeginning();
        }
    }

    public void Preload(VideoClip clip)
    {
        EnsureComponents();

        if (Video != clip || videoPlayer.clip != clip)
        {
            AssignClip(clip);
        }

        playWhenPrepared = false;
        resumeAfterEnable = false;

        if (clip == null)
        {
            return;
        }

        if (videoPlayer.isPrepared)
        {
            ApplyOutputTexture(videoPlayer.texture);

            if (keepWarmWhileHidden && isActiveAndEnabled && Application.isPlaying)
            {
                videoPlayer.Play();
            }

            return;
        }

        if (!isPreparing && isActiveAndEnabled && Application.isPlaying)
        {
            isPreparing = true;
            videoPlayer.Prepare();
        }
    }

    public void Reset()
    {
        if (isActiveAndEnabled && Application.isPlaying)
        {
            PlayFromBeginning();
        }
    }

    public void PlayPrepared()
    {
        EnsureComponents();

        if (Video == null)
        {
            return;
        }

        if (videoPlayer.clip != Video)
        {
            AssignClip(Video);
        }

        playWhenPrepared = true;
        resumeAfterEnable = true;

        if (videoPlayer.isPrepared)
        {
            isPreparing = false;
            ApplyOutputTexture(videoPlayer.texture);
            videoPlayer.Play();
        }
        else if (!isPreparing && isActiveAndEnabled && Application.isPlaying)
        {
            isPreparing = true;
            videoPlayer.Prepare();
        }
    }

    public void Stop()
    {
        resumeAfterEnable = false;
        PauseAtBeginning();
    }

    public void Hide()
    {
        resumeAfterEnable = false;
        playWhenPrepared = false;

        if (!keepWarmWhileHidden)
        {
            PauseAtBeginning();
            return;
        }

        if (videoPlayer != null
            && videoPlayer.isPrepared
            && !videoPlayer.isPlaying
            && isActiveAndEnabled
            && Application.isPlaying)
        {
            videoPlayer.Play();
        }
    }

    private void PlayFromBeginning()
    {
        EnsureComponents();

        if (Video == null)
        {
            return;
        }

        if (videoPlayer.clip != Video)
        {
            AssignClip(Video);
        }

        playWhenPrepared = true;
        resumeAfterEnable = true;

        if (videoPlayer.isPrepared)
        {
            isPreparing = false;
            ApplyOutputTexture(videoPlayer.texture);
            videoPlayer.time = 0d;
            videoPlayer.Play();
        }
        else if (!isPreparing)
        {
            isPreparing = true;
            videoPlayer.Prepare();
        }
    }

    private void AssignClip(VideoClip clip)
    {
        playWhenPrepared = false;
        isPreparing = false;
        videoPlayer.Stop();
        Video = clip;
        videoPlayer.clip = clip;
        ApplyOutputTexture(null);
    }

    private void PauseAtBeginning()
    {
        playWhenPrepared = false;

        if (videoPlayer == null)
        {
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }

        if (videoPlayer.isPrepared)
        {
            videoPlayer.time = 0d;

            if (videoImage != null)
            {
                ApplyOutputTexture(videoPlayer.texture);
            }
        }
    }

    private void EnsureComponents()
    {
        videoImage ??= GetComponent<RawImage>();
        videoRenderer ??= GetComponent<Renderer>();

        if (videoImage == null
            && videoRenderer == null
            && transform is RectTransform)
        {
            videoImage = gameObject.AddComponent<RawImage>();
        }

        videoPlayer ??= GetComponent<VideoPlayer>();

        if (videoPlayer == null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        if (videoImage != null)
        {
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
        }
        else if (videoRenderer != null)
        {
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = videoRenderer;
            videoPlayer.targetMaterialProperty = "_MainTex";
        }
        else
        {
            Debug.LogError(
                $"{nameof(SpellVideoPlayer)} requires a RawImage or Renderer.",
                this);
        }

        if (!eventsRegistered)
        {
            videoPlayer.prepareCompleted += HandlePrepared;
            videoPlayer.errorReceived += HandleError;
            eventsRegistered = true;
        }
    }

    private void HandlePrepared(VideoPlayer source)
    {
        isPreparing = false;

        if (source.clip != Video)
        {
            return;
        }

        ApplyOutputTexture(source.texture);

        if ((playWhenPrepared || keepWarmWhileHidden) && isActiveAndEnabled)
        {
            source.Play();
        }
    }

    private void HandleError(VideoPlayer source, string message)
    {
        isPreparing = false;
        playWhenPrepared = false;
        Debug.LogWarning($"Could not play spell video '{source.clip?.name}': {message}", this);
    }

    private void ApplyOutputTexture(Texture texture)
    {
        if (videoImage != null)
        {
            videoImage.texture = texture;
        }
    }
}
