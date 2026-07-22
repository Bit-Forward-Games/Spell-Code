using UnityEngine;

public class ArenaBoundRenderer : MonoBehaviour
{
    [SerializeField] private StageDataSO _stageDataSO;
    [SerializeField] private bool _renderBorders = true;
    [SerializeField] private bool _renderCameraBorders = true;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        //if _stageDataSO is not set, return
        if (_stageDataSO == null) return;

        if (_renderBorders)
        {
            //***** Draw borders in red *****
            //if _stageDataSO.borderMin and _stageDataSO.borderMax are not valid
            if (_stageDataSO.borderMin == null || _stageDataSO.borderMax == null || _stageDataSO.borderMin == Vector3.zero || _stageDataSO.borderMax == Vector3.zero) return;

            //determine vertices of border
            Vector3 _borderBottomLeft = _stageDataSO.borderMin;
            Vector3 _borderBottomRight = new Vector3(_stageDataSO.borderMax.x, _stageDataSO.borderMin.y);
            Vector3 _borderTopRight = _stageDataSO.borderMax;
            Vector3 _borderTopLeft = new Vector3(_stageDataSO.borderMin.x, _stageDataSO.borderMax.y);

            //set color to red for borders
            Gizmos.color = Color.red;

            //Draw borders in red
            Gizmos.DrawLine(_borderBottomLeft, _borderBottomRight);
            Gizmos.DrawLine(_borderBottomRight, _borderTopRight);
            Gizmos.DrawLine(_borderTopRight, _borderTopLeft);
            Gizmos.DrawLine(_borderTopLeft, _borderBottomLeft);
        }

        if (_renderCameraBorders)
        {
            //***** Draw camera bounds in yellow  *****
            //if _stageDataSO.borderMin and _stageDataSO.borderMax are not valid
            if (_stageDataSO.camBorderMin == null || _stageDataSO.camBorderMax == null || _stageDataSO.camBorderMin == Vector3.zero || _stageDataSO.camBorderMax == Vector3.zero) return;

            //determine vertices of camera borders
            Vector3 _camBorderBottomLeft = _stageDataSO.camBorderMin;
            Vector3 _CamBorderBottomRight = new Vector3(_stageDataSO.camBorderMax.x, _stageDataSO.camBorderMin.y);
            Vector3 _camBorderTopRight = _stageDataSO.camBorderMax;
            Vector3 _camBorderTopLeft = new Vector3(_stageDataSO.camBorderMin.x, _stageDataSO.camBorderMax.y);

            //set color to yellow for camera borders
            Gizmos.color = Color.yellow;

            //Draw camera borders in yellow
            Gizmos.DrawLine(_camBorderBottomLeft, _CamBorderBottomRight);
            Gizmos.DrawLine(_CamBorderBottomRight, _camBorderTopRight);
            Gizmos.DrawLine(_camBorderTopRight, _camBorderTopLeft);
            Gizmos.DrawLine(_camBorderTopLeft, _camBorderBottomLeft);
        }
    }
#endif
}
