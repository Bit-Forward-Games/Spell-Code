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
    /// Displays the current arena name after _waitTime seconds
    /// </summary>
    /// <param name="_waitTime"></param>
    /// <param name="_displayDuration"></param>
    public void WaitAndDisplay(float _waitTime, float _displayDuration)
    {
        //wait for _waitTime seconds,...
        DOVirtual.DelayedCall(_waitTime, () =>
        {
            //display the arena name
            DisplayArenaName(_displayDuration);
        }).SetUpdate(false);

        //return
        return;
    }

    /// <summary>
    /// Displays the current arena name at the bottom of the screen
    /// </summary>
    public Tween DisplayArenaName(float _displayDuration)
    {
        //if arenaNameTextMesh does not exist, then return
        if (_arenaNameTextMesh == null) return null;

        //if stage name does not exist OR is empty, then return
        if (GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName == null || GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName == "") return null;

        //change arenaNameTextMesh text show stage name
        //Debug.Log("ArenaNameDisplay | Current arena name is: " + GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName);
        _arenaNameTextMesh.text = GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName;

        //move _displayBackground to its initial position
        _displayBackgroundRectTransform.anchoredPosition = _initBackgroundPos;

        //define a sequence to return
        Sequence sequence = DOTween.Sequence();

        //join the start of the background movement tween to the sequence
        sequence.Insert(
            0f,
            _displayBackgroundRectTransform.DOAnchorPosX
            (
                0f,
                _displayDuration * 0.25f,
                false
            ).SetEase(Ease.OutSine)
        );

        //join the end of the background movement tween to the sequence
        sequence.Insert(
            _displayDuration * 0.75f,
            _displayBackgroundRectTransform.DOAnchorPosX
            (
                53f,
                _displayDuration * 0.25f,
                false
            ).SetEase(Ease.InSine)
        );

        //insert the start of the background opacity tween to the sequence
        sequence.Insert(
            0f,
            _displayCanvasGroup.DOFade(1f, _displayDuration * 0.25f)
        );

        //insert the end of the background opacity tween to the sequence
        sequence.Insert(
            _displayDuration * 0.75f,
            _displayCanvasGroup.DOFade(0f, _displayDuration * 0.25f)
        );

        //attach the HideArenaName() function to the completion of the sequence
        sequence.OnComplete(() => HideArenaName()?.Invoke());

        //begin the sequence
        return sequence;
    }

    /// <summary>
    /// Stops showing the current arena name display
    /// </summary>
    public TweenCallback HideArenaName()
    {
        //reset _displayBackground to its initial position
        if (_displayBackgroundRectTransform != null) _displayBackgroundRectTransform.anchoredPosition = _initBackgroundPos;

        //make canvas group fully transparent
        if (_displayCanvasGroup != null) _displayCanvasGroup.alpha = 0f;

        return null;
    }
}
