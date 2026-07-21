using UnityEngine;

public class ArenaBoundRenderer : MonoBehaviour
{
    [SerializeField] private StageDataSO stageDataSO;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        //if stageDataSO is not set, return
        if (stageDataSO == null) return;

        //***** Draw borders in red *****
        //if stageDataSO.borderMin and stageDataSO.borderMax are not valid
        if (stageDataSO.borderMin == null || stageDataSO.borderMax == null || stageDataSO.borderMin == Vector3.zero || stageDataSO.borderMax == Vector3.zero) return;

        //determine vertices of border
        Vector3 _borderBottomLeft = stageDataSO.borderMin;
        Vector3 _borderBottomRight = new Vector3(stageDataSO.borderMax.x, stageDataSO.borderMin.y);
        Vector3 _borderTopRight = stageDataSO.borderMax;
        Vector3 _borderTopLeft = new Vector3(stageDataSO.borderMin.x, stageDataSO.borderMax.y);

        //set color to red for borders
        Gizmos.color = Color.red;

        //Draw borders in red
        Gizmos.DrawLine(_borderBottomLeft, _borderBottomRight);
        Gizmos.DrawLine(_borderBottomRight, _borderTopRight);
        Gizmos.DrawLine(_borderTopRight, _borderTopLeft);
        Gizmos.DrawLine(_borderTopLeft, _borderBottomLeft);

        //***** Draw camera bounds in yellow  *****
        //if stageDataSO.borderMin and stageDataSO.borderMax are not valid
        if (stageDataSO.camBorderMin == null || stageDataSO.camBorderMax == null || stageDataSO.camBorderMin == Vector3.zero || stageDataSO.camBorderMax == Vector3.zero) return;

        //determine vertices of camera borders
        Vector3 _camBorderBottomLeft = stageDataSO.camBorderMin;
        Vector3 _CamBorderBottomRight = new Vector3(stageDataSO.camBorderMax.x, stageDataSO.camBorderMin.y);
        Vector3 _camBorderTopRight = stageDataSO.camBorderMax;
        Vector3 _camBorderTopLeft = new Vector3(stageDataSO.camBorderMin.x, stageDataSO.camBorderMax.y);
        
        //set color to yellow for camera borders
        Gizmos.color = Color.yellow;

        //Draw camera borders in yellow
        Gizmos.DrawLine(_camBorderBottomLeft, _CamBorderBottomRight);
        Gizmos.DrawLine(_CamBorderBottomRight, _camBorderTopRight);
        Gizmos.DrawLine(_camBorderTopRight, _camBorderTopLeft);
        Gizmos.DrawLine(_camBorderTopLeft, _camBorderBottomLeft);
    }
#endif
}
