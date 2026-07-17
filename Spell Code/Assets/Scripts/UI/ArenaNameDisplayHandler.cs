using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class ArenaNameDisplayHandler : MonoBehaviour
{
    [SerializeField] private GameObject _displayBackground;
    [SerializeField] private TextMeshProUGUI _arenaNameTextMesh;

    private void Awake()
    {
        //Hide name display
        HideArenaName();
    }

    /// <summary>
    /// Displays the current arena name at the bottom of the screen
    /// </summary>
    public Tween DisplayArenaName(float _duration)
    {
        //if arenaNameTextMesh does not exist, then return
        if (_arenaNameTextMesh == null) return null;

        //if stage name does not exist OR is empty, then return
        if (GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName == null || GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName == "") return null;

        //enable background
        _displayBackground.gameObject.SetActive(true);

        //change arenaNameTextMesh text show stage name
        _arenaNameTextMesh.text = GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName;
        Debug.Log("ArenaNameDisplay | Current arena name is: " + GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName);

        ////wait for _delay seconds,...
        //DOVirtual.DelayedCall(_duration, () =>
        //{
        //    //stop showing arena name
        //    HideArenaName();
        //}).SetUpdate(false);

        //return
        return _displayBackground.gameObject.transform.DOPath
        (
            new Vector3[] { new Vector3(-53f, _displayBackground.gameObject.transform.position.y), new Vector3(53f, _displayBackground.gameObject.transform.position.y) },
            _duration,
            PathType.Linear,
            PathMode.Sidescroller2D).SetEase(Ease.InOutElastic).OnComplete(() => HideArenaName()?.Invoke()
        );
    }

    //private TweenCallback DelayedHideArenaName(float _delay)
    //{
    //    return () =>
    //    {
    //        DOVirtual.DelayedCall(_delay,
    //        () =>
    //        {
    //            //Hide name display
    //            HideArenaName();
    //        }
    //        ).SetUpdate(true);
    //    };
    //}

    /// <summary>
    /// Stops showing the current arena name display
    /// </summary>
    public TweenCallback HideArenaName()
    {
        //if arenaNameTextMesh does not exist, then return
        if (_arenaNameTextMesh == null) return null;

        //disable background
        _displayBackground.gameObject.SetActive(false);

        return null;
    }
}
