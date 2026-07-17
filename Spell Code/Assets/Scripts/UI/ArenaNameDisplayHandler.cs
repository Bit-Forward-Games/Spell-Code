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
    public void DisplayArenaName(float _delay)
    {
        //if arenaNameTextMesh does not exist, then return
        if (_arenaNameTextMesh == null) return;

        //enable background
        _displayBackground.gameObject.SetActive(true);

        //change arenaNameTextMesh text show stage name
        _arenaNameTextMesh.text = GameManager.Instance.stages[GameManager.Instance.currentStageIndex].stageName;

        //wait for _delay seconds,...
        DOVirtual.DelayedCall(_delay, () =>
        {
            //stop showing arena name
            HideArenaName();
        }).SetUpdate(false);

        //return
        return;
    }

    /// <summary>
    /// Stops showing the current arena name display
    /// </summary>
    public void HideArenaName()
    {
        //if arenaNameTextMesh does not exist, then return
        if (_arenaNameTextMesh == null) return;

        //disable background
        _displayBackground.gameObject.SetActive(false);
    }
}
