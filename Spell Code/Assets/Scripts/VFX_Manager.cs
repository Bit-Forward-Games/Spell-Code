using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.VisualScripting.Antlr3.Runtime;

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
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DASH_DUST);
        }
    }

    public void PlayVisualEffect(VisualEffects _nameOfVisualEffectToPlay)
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

        //play the visual effect
        Debug.Log(gameObject.name + ": Playing visual effect of name = \"" + _nameOfVisualEffectToPlay + "\"");
        _visualEffectObject.particleSystem.Play();
    }
}
