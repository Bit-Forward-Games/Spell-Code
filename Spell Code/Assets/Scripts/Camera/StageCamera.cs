using UnityEngine;
using UnityEngine.Rendering.Universal;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using FixedVec3 = BestoNet.Types.Vector3<BestoNet.Types.Fixed32>;

public class StageCamera : MonoBehaviour
{
    // ===== | Variables | =====
    [SerializeField] private Vector2 offset;
    [SerializeField] private float damping;
    [SerializeField] private float minZoom = 180f;
    [SerializeField] private float maxZoom = 1280F;
    [SerializeField] private float HardSetZoom = 180f;
    [SerializeField] private float minDistance = 360f;
    [SerializeField] private float zoomLimiter = 960f;
    [SerializeField] private float zoomSpeed = 1f;
    //[SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeMagnitude = 1f;

    public Vector2 target;
    private Vector3 vel = Vector3.one;
    private Camera cam;

    //[SerializeField] private PixelPerfectCamera pixelPerfectCamera;

    private float shakeTimeRemaining;
    private Vector3 shakeOffset;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void FixedUpdate()
    {
        //if the current scene is main menu, don't do anything
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
        {
            cam.orthographicSize = HardSetZoom;
            return;
        }

        if (GameManager.Instance.playerCount > 0)
        {
            Vector2 averagePosition = Vector3.up;
            for (int i = 0; i < GameManager.Instance.playerCount; i++)
            {
                FixedVec2 fixedPos = GameManager.Instance.players[i].position;
                Vector2 floatPos = new Vector2(fixedPos.X.ToFloat(), fixedPos.Y.ToFloat());
                averagePosition += floatPos;
            }
            averagePosition /= GameManager.Instance.playerCount;
            target = averagePosition + offset;

            Bounds greatestDistance = GetGreatestDistance();
            float newZoom = minZoom;

            if (greatestDistance.size.x > minDistance || greatestDistance.size.y > (minDistance / 16 * 9))
            {
                //newZoom = Mathf.Lerp(minZoom, maxZoom, (greatestDistance.size.magnitude - minDistance) / zoomLimiter);
                if (greatestDistance.size.x >= greatestDistance.size.y)
                {
                    newZoom = Mathf.Lerp(minZoom, maxZoom, ((greatestDistance.size.x / 16 * 9) - (minDistance / 16 * 9)) / zoomLimiter);
                }
                else
                {
                    newZoom = Mathf.Lerp(minZoom, maxZoom, (greatestDistance.size.y - (minDistance / 16 * 9)) / zoomLimiter);
                }
            }

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, newZoom, zoomSpeed * Time.deltaTime);

            //if (cam.orthographicSize <= minZoom)
            //{
            //    cam.orthographicSize = minZoom;
            //    pixelPerfectCamera.enabled = true;
            //}
            //else
            //{
            //    pixelPerfectCamera.enabled = false;
            //}

            Vector3 targetPosition = new Vector3(target.x, target.y, -10);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref vel, damping);

            ApplyShake();

            //transform.position = new Vector3(
            //    Mathf.Clamp(transform.position.x, StageData.Instance.leftWallXval + cam.orthographicSize * 16 / 9,
            //    StageData.Instance.rightWallXval - cam.orthographicSize * 16 / 9),
            //    Mathf.Clamp(transform.position.y, 0, StageData.Instance.ceilingYval - cam.orthographicSize),
            //    -10);
        }
    }

    private Bounds GetGreatestDistance()
    {
        Vector3 initialCenter = new Vector3(
        GameManager.Instance.players[0].position.X.ToFloat(),
        GameManager.Instance.players[0].position.Y.ToFloat(),
        -10f // Use -10f for Z
    );
        Bounds bounds = new Bounds(initialCenter, Vector3.zero);
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            Vector3 playerPosV3 = new Vector3(
            GameManager.Instance.players[i].position.X.ToFloat(),
            GameManager.Instance.players[i].position.Y.ToFloat(),
            -10f 
        );
            bounds.Encapsulate(playerPosV3);
        }
        return bounds;
    }

    public void ScreenShake(float duration, float magnitude)
    {
        shakeTimeRemaining = duration;
        shakeMagnitude = magnitude;
    }

    private void ApplyShake()
    {
        if (RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame)
        {
            shakeOffset = Vector3.zero; // Ensure no residual shake during rollback
            return;
        }
        if (shakeTimeRemaining > 0)
        {
            shakeOffset = Random.insideUnitCircle * shakeMagnitude;
            shakeTimeRemaining -= Time.deltaTime;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        transform.position += shakeOffset;
    }
}
