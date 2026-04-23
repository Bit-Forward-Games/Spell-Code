using UnityEngine;



public enum BorderType
{
    Collision,
    Loop,
    DeathZone
}

public enum StageType
{
    General,
    Duel,
    Party
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
    public Vector3 camBorderMin;
    public Vector3 camBorderMax;
    public BorderType borderType;
    public StageType stageType;
    public bool dynamicCamera;
}

