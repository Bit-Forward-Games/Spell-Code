using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.VisualScripting.Antlr3.Runtime;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using BestoNet.Types;

public enum VisualEffects
{
    DASH_DUST
}
public class VFX_Manager : MonoBehaviour
{
    //<value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static VFX_Manager Instance { get; private set; }

    //Object to hold the data for each visual effect
    [Serializable]
    private class VisualEffectObject
    {
        public VisualEffects visualEffectName; //name of the visual effect
        public ParticleSystem particleSystem; //particle system that plays the visual effect
    }

    [Header("Visual effects that VFX Manager can play")]
    [SerializeField] private List<VisualEffectObject> visualEffectObjects;

    void Awake()
    {
        //if there is an instance of VFX_Manager that is NOT this one,...
        if (Instance != null && Instance != this)
        {
            //delete myself
            Destroy(this);
        }
        //else there is only 1 instance of VFX_Manager,...
        else
        {
            //set instance to this instance of VFX_Manager
            Instance = this;
        }
    }

    //DEBUGGING:
    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.Space))
    //    {
    //        FixedVec2 player1pos = GameManager.Instance.players[0].position;
    //        bool isFacingRight = GameManager.Instance.players[0].facingRight;
    //        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DASH_DUST, player1pos, isFacingRight);
    //    }
    //}

    /// <summary>
    /// Play a visual effect with the name defined by "_nameOfSoundToPlay" at the position defined by "_spawnPos"
    /// </summary>
    /// <param name="_nameOfVisualEffectToPlay"> Name of the visual effect to be played</param>
    /// <param name="_spawnPos"> Position of the visual effect to be played</param>
    /// <param name="_spawnDirection"> Direction of the visual effect to be played</param>
    // = FixedVec2(Fixed32.FromInt(0), Fixed32.FromInt(0))
    public void PlayVisualEffect(VisualEffects _nameOfVisualEffectToPlay, FixedVec2 _spawnPos, bool _spawnFacingRight = true)
    {
        //sanity check to make sure that there is a visual effect with name equal to _nameOfVisualEffectToPlay that exists within visualEffectObjects
        if (visualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified visual effect of name = \"" + _nameOfVisualEffectToPlay + "\" does not exist within visualEffectObjects of the VFX_Manager script. Please specify a song that exists with visualEffectObjects");

            //return
            return;
        }

        //get visual effect object
        VisualEffectObject _visualEffectObject = visualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay);

        //convert _spawnPos to a Vector3
        Vector3 _spawnPosVector3 = new Vector3(_spawnPos.X.ToFloat(), _spawnPos.Y.ToFloat(), 0f);

        //set position of particle system to Vector2 version of _spawnPos
        _visualEffectObject.particleSystem.gameObject.transform.position = _spawnPosVector3;

        //change particle system direction based on _spawnFacingRight
        if(_spawnFacingRight == true)
        {
            _visualEffectObject.particleSystem.gameObject.transform.localScale = new Vector3(1f, _visualEffectObject.particleSystem.gameObject.transform.localScale.y, _visualEffectObject.particleSystem.gameObject.transform.localScale.z);
        }
        else
        {
            _visualEffectObject.particleSystem.gameObject.transform.localScale = new Vector3(-1f, _visualEffectObject.particleSystem.gameObject.transform.localScale.y, _visualEffectObject.particleSystem.gameObject.transform.localScale.z);
        }

        //play the visual effect
        Debug.Log(gameObject.name + ": Playing visual effect of name = \"" + _nameOfVisualEffectToPlay + "\"");
        _visualEffectObject.particleSystem.Play();
    }
}
