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
    //constant integer to store the number of particle systems per VisualEffectObject
    private const int NUM_PARTICLESYSTEMS_PER_VFXOBJECT = 5;

    /// <value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static VFX_Manager Instance { get; private set; }

    //Object to hold the data for each visual effect
    [Serializable]
    private class VisualEffectObject
    {
        public VisualEffects visualEffectName; //name of the visual effect
        public GameObject particleSystemPrefab; //prefab of the particle system for this visual effect
        [HideInInspector] public ParticleSystem[] particleSystems = new ParticleSystem[NUM_PARTICLESYSTEMS_PER_VFXOBJECT]; //List of 5 particle systems that each play the visual effect. Each particle system in the array is associated with a unique player as well as a non player specific particle system
    }

    [Header("Visual effects that VFX Manager can play")]
    [SerializeField] private List<VisualEffectObject> visualEffectObjects; //list of visual effects that the VFX Manager can play

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

        //instantiate all particle systems
        InstantiateParticleSystems();
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

    //Function to instantiate all particle systems
    private void InstantiateParticleSystems()
    {
        //instantiate NUM_PARTICLESYSTEMS_PER_VFXOBJECT (5) particle systems per VisualEffectObject in visualEffectObjects
        foreach (VisualEffectObject visualEffectObject in visualEffectObjects)
        {
            //sanity check to make sure that particleSystemPrefab has been assigned for this visualEffectObject
            if (visualEffectObject.particleSystemPrefab == null)
            {
                //log an error
                Debug.LogError(gameObject.name + ": The particle system prefab for \"" + visualEffectObject.visualEffectName + "\" has not been assigned within visualEffectObjects. Assign a particle system prefab to \"" + visualEffectObject.visualEffectName + "\"");

                //skip the rest of this foreach loop iteration
                continue;   
            }

            //define and instantiate an empty gameobject to help organize the particle systems
            GameObject _headingGameobject = Instantiate(new GameObject(), this.gameObject.transform);
            _headingGameobject.name = visualEffectObject.particleSystemPrefab.name + "s";

            //instantiate NUM_PARTICLESYSTEMS_PER_VFXOBJECT (5) particle systems
            for (int i = 0; i < NUM_PARTICLESYSTEMS_PER_VFXOBJECT; i++)
            {
                //create a particle system that is a child of the VFX_Manager 
                GameObject _createdParticleSystem = Instantiate(visualEffectObject.particleSystemPrefab, _headingGameobject.transform);

                //give the newly created particle system a unique name
                _createdParticleSystem.name = visualEffectObject.particleSystemPrefab.name + " #" + i.ToString();

                //add the newly created particle system to the particleSystems array for this VisualEffectObject
                visualEffectObject.particleSystems[i] = _createdParticleSystem.GetComponent<ParticleSystem>();
            }
        }
    }

    /// <summary>
    /// Play a visual effect with the name defined by "_nameOfSoundToPlay" at the position defined by "_spawnPos"
    /// </summary>
    /// <param name="_nameOfVisualEffectToPlay"> Name of the visual effect to be played</param>
    /// <param name="_spawnPos"> Position of the visual effect to be played</param>
    /// <param name="_playerNum"> Player number of the player who is spawning this visual effect. To spawn a visual effect without it being associated with a player, set _playerNum to 0. By default, set to 0 (not associated with a player)</param>
    /// <param name="_spawnFacingRight"> Whether or not the visual effect will spawn facing right. By default, set to true (facing right)</param>
    // = FixedVec2(Fixed32.FromInt(0), Fixed32.FromInt(0))
    public void PlayVisualEffect(VisualEffects _nameOfVisualEffectToPlay, FixedVec2 _spawnPos, int _playerNum = 0, bool _spawnFacingRight = true)
    {
        //sanity check to make sure that there is a visual effect with name equal to _nameOfVisualEffectToPlay that exists within visualEffectObjects
        if (visualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified visual effect of name = \"" + _nameOfVisualEffectToPlay + "\" does not exist within visualEffectObjects of the VFX_Manager script. Please specify a song that exists with visualEffectObjects");

            //return
            return;
        }

        //sanity check to make sure that _playerNum is valid
        if(_playerNum < 0 && _playerNum > 4)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": _playerNum of \"" + _playerNum + "\" is not valid. Please make sure thet _playerNum is either 0, 1, 2, 3, or 4");

            //return
            return;
        }

        //get visual effect object
        VisualEffectObject _visualEffectObject = visualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay);

        //convert _spawnPos to a Vector3
        Vector3 _spawnPosVector3 = new Vector3(_spawnPos.X.ToFloat(), _spawnPos.Y.ToFloat(), 0f);

        //set position of particle system to Vector2 version of _spawnPos
        _visualEffectObject.particleSystems[_playerNum].gameObject.transform.position = _spawnPosVector3;

        //change particle system direction based on _spawnFacingRight
        if(_spawnFacingRight == true)
        {
            _visualEffectObject.particleSystems[_playerNum].gameObject.transform.localScale = new Vector3(1f, _visualEffectObject.particleSystems[_playerNum].gameObject.transform.localScale.y, _visualEffectObject.particleSystems[_playerNum].gameObject.transform.localScale.z);
        }
        else
        {
            _visualEffectObject.particleSystems[_playerNum].gameObject.transform.localScale = new Vector3(-1f, _visualEffectObject.particleSystems[_playerNum].gameObject.transform.localScale.y, _visualEffectObject.particleSystems[_playerNum].gameObject.transform.localScale.z);
        }

        //play the visual effect
        //Debug.Log(gameObject.name + ": Playing visual effect of name = \"" + _nameOfVisualEffectToPlay + "\"");
        _visualEffectObject.particleSystems[_playerNum].Play();
    }
}
