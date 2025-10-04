//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.U2D;

//public class cameraFollow : MonoBehaviour
//{
//    [SerializeField] private Vector3 offset;
//    [SerializeField] private float damping;
//    [SerializeField] private float minZoom = 180f;
//    [SerializeField] private float maxZoom = 1280F;
//    [SerializeField] private float minDistance = 360f;
//    [SerializeField] private float zoomLimiter = 960f;
//    [SerializeField] private float zoomSpeed = 1f;

//    public Vector3 target;
//    private Vector3 vel = Vector3.zero;
//    private Camera cam;
//    //private PixelPerfectCamera pixelPerfectCamera;

//    private void Start()
//    {
//        cam = GetComponent<Camera>();
//        //pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
//    }

//    private void FixedUpdate()
//    {
//        if (GameManager.Instance.playerCount > 0)
//        {
//            // Get the average position of all players and set the camera to that position
//            Vector3 averagePos = Vector3.zero;
//            for (int i = 0; i < GameManager.Instance.playerCount; i++)
//            {
//                averagePos += GameManager.Instance.players[i].transform.position;
//            }
//            averagePos /= GameManager.Instance.playerCount;
//            target = averagePos + offset;

//            // Calculate the new zoom level based on the distance between players
//            Bounds greatestDistance = GetGreatestDistance();
//            float newZoom = minZoom;

//            if (greatestDistance.size.x > minDistance || greatestDistance.size.y > (minDistance / 16 * 9))
//            {
//                //newZoom = Mathf.Lerp(minZoom, maxZoom, (greatestDistance.size.magnitude - minDistance) / zoomLimiter);
//                if (greatestDistance.size.x >= greatestDistance.size.y)
//                {
//                    newZoom = Mathf.Lerp(minZoom, maxZoom, ((greatestDistance.size.x / 16 * 9) - (minDistance / 16 * 9)) / zoomLimiter);
//                }
//                else
//                {
//                    newZoom = Mathf.Lerp(minZoom, maxZoom, (greatestDistance.size.y - (minDistance / 16 * 9)) / zoomLimiter);
//                }
//            }


//            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, newZoom, zoomSpeed * Time.deltaTime);
//            //cam.orthographicSize = newZoom;

//            // Disable Pixel Perfect Camera if zoomed out
//            if (cam.orthographicSize <= minZoom)
//            {
//                cam.orthographicSize = minZoom;
//                //pixelPerfectCamera.enabled = true;
//            }
//            else
//            {
//                //pixelPerfectCamera.enabled = false;
//            }

//        }

//        target.z = transform.position.z;
//        transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, damping);
//    }

//    private Bounds GetGreatestDistance()
//    {
//        Bounds bounds = new Bounds(GameManager.Instance.players[0].transform.position, Vector3.zero);
//        for (int i = 0; i < GameManager.Instance.playerCount; i++)
//        {
//            bounds.Encapsulate(GameManager.Instance.players[i].transform.position);
//        }
//        return bounds;
//    }
//}
