using UnityEngine;
using UnityEngine.Rendering.Universal;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using FixedVec3 = BestoNet.Types.Vector3<BestoNet.Types.Fixed32>;

public class StageCamera : MonoBehaviour
{
    // ===== | Variables | =====
    [SerializeField] private Vector2 offset = new Vector2(0, 36f);
    [SerializeField] private float damping;
    [SerializeField] private float minZoom = 180f;
    [SerializeField] private float maxZoom = 1280F;
    [SerializeField] private float HardSetZoom = 205f;
    [SerializeField] private float zoomSpeed = 1f;
    //[SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeMagnitude = 1f;
    // Buffer (world units) to keep between the players and the screen edge
    [SerializeField] private float screenEdgeBuffer = 128f;


    public Vector2 target;
    public bool lockCamera = true;
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

        // If camera is locked, set to hard zoom and return
        if (lockCamera)
        {
            cam.orthographicSize = HardSetZoom;
            ApplyShake();
            return;
        }

        if (GameManager.Instance.playerCount > 0)
        {

            // get players bounding box
            Bounds greatestDistance = GetGreatestDistance();
            target = (Vector2)greatestDistance.center + offset;
            StageDataSO stageDataSO = GameManager.Instance.currentStageIndex < 0 ? GameManager.Instance.lobbySO : GameManager.Instance.stages[GameManager.Instance.currentStageIndex];


            // --- get required orthographic size based on both width and height ---
            // orthographicSize is half the vertical size. Horizontal half-size = orthographicSize * aspect.
            float aspect = cam.aspect;
            // required size to fit height (half)
            float requiredFromHeight = greatestDistance.size.y * 0.5f + screenEdgeBuffer;
            // required size to fit width (convert half-width to half-vertical by dividing by aspect)
            float requiredFromWidth = (greatestDistance.size.x * 0.5f) / Mathf.Max(0.0001f, aspect) + screenEdgeBuffer;




            // choose the stricter requirement and clamp to min/max zoom
            float requiredSize = Mathf.Max(requiredFromHeight, requiredFromWidth, minZoom);

            float newZoom = Mathf.Clamp(requiredSize, minZoom, maxZoom);






            // smooth the zoom
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, newZoom, zoomSpeed * Time.deltaTime);


            Vector3 targetPosition = new Vector3(target.x, target.y, -10);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref vel, damping);

            ApplyShake();

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
        //if (RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame)
        //{
        //    shakeOffset = Vector3.zero; // Ensure no residual shake during rollback
        //    return;
        //}
        if (shakeTimeRemaining > 0)
        {
            shakeOffset = Random.insideUnitCircle * shakeMagnitude;
            shakeTimeRemaining -= Time.deltaTime;
        }
        else
        {
            shakeOffset = Vector3.zero;

            if (lockCamera)
            {
                transform.position = new Vector3(0, 0, -10);
            }
        }

        transform.position += shakeOffset;
    }
}
