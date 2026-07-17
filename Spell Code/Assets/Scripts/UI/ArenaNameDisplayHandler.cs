using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class ArenaNameDisplayHandler : MonoBehaviour
{
    [SerializeField] private RectTransform _displayBackgroundRectTransform;
    [SerializeField] private CanvasGroup _displayCanvasGroup;
    [SerializeField] private TextMeshProUGUI _arenaNameTextMesh;
    private Vector3 _initBackgroundPos = Vector3.zero;

    private void Awake()
    {
        //if _displayBackground has been defined, then record the initial position of the background
        if (_displayBackgroundRectTransform != null) _initBackgroundPos = new Vector3(_displayBackgroundRectTransform.anchoredPosition.x, _displayBackgroundRectTransform.anchoredPosition.y, 0f);

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
        //_displayBackgroundRectTransform.gameObject.SetActive(true);

        //change arenaNameTextMesh text show stage name
        _arenaNameTextMesh.text = GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName;
        //Debug.Log("ArenaNameDisplay | Current arena name is: " + GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName);

        //Debug.Log("Start = " + new Vector3(-53f, _initBackgroundPos.y) + ". Middle = " + new Vector3(0f, _initBackgroundPos.y) + ". End = " + new Vector3(53f, _initBackgroundPos.y));

        //move _displayBackground to its initial position
        _displayBackgroundRectTransform.anchoredPosition = _initBackgroundPos;

        //define a sequence to return
        Sequence sequence = DOTween.Sequence();

        //join the background movement tween to the sequence
        sequence.Join(
            _displayBackgroundRectTransform.DOAnchorPosX
            (
                53f,
                _duration,
                false
                ).SetEase(Ease.Linear).OnComplete(() => HideArenaName()?.Invoke()
            )
        );

        //insert the start of the background opacity tween to the sequence
        sequence.Insert(
            0f,
            _displayCanvasGroup.DOFade(1f, _duration * 0.25f)
        );

        //insert the end of the background opacity tween to the sequence
        sequence.Insert(
            _duration * 0.75f,
            _displayCanvasGroup.DOFade(0f, _duration * 0.25f)
        );

        //attach the HideArenaName() function to the completion of the sequence
        sequence.OnComplete(() => HideArenaName()?.Invoke());

        //begin the sequence
        return sequence;
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
        //if (_arenaNameTextMesh == null) return null;

        //reset _displayBackground to its initial position
        if (_displayBackgroundRectTransform != null) _displayBackgroundRectTransform.anchoredPosition = _initBackgroundPos;

        //make canvas group fully transparent
        if (_displayCanvasGroup != null) _displayCanvasGroup.alpha = 0f;

        //disable background
        //_displayBackgroundRectTransform.gameObject.SetActive(false);

        return null;
    }
}
