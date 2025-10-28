using UnityEngine;

[CreateAssetMenu(fileName = "StageDataSO", menuName = "Scriptable Objects/StageDataSO")]
public class StageDataSO : ScriptableObject
{
    public Vector2[] platformCenter;
    public Vector2[] platformExtent;
    public Vector2[] solidCenter;
    public Vector2[] solidExtent;
}
