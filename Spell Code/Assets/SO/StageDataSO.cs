using UnityEngine;



public enum BorderType
{
    Collision,
    Loop,
    DeathZone
}


[CreateAssetMenu(fileName = "StageDataSO", menuName = "Scriptable Objects/StageDataSO")]
public class StageDataSO : ScriptableObject
{
    public Vector2[] platformCenter;
    public Vector2[] platformExtent;
    public Vector2[] solidCenter;
    public Vector2[] solidExtent;
    public Vector3[] playerSpawnTransform;
    public Vector3[] activatableSolidCenter;
    public Vector3[] activatableSolidExtent;
    public Vector3 borderMin;
    public Vector3 borderMax;
    public BorderType borderType;
}

