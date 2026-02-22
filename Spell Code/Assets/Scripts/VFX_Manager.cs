using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.VisualScripting.Antlr3.Runtime;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using BestoNet.Types;
using UnityEngine.VFX;

public enum VisualEffects
{
    DASH_DUST, BOUNTY_AURA
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
        [Header("Mandatory variables")]
        public VisualEffects visualEffectName; //name of the visual effect
        public GameObject particleSystemPrefab; //prefab of the particle system for this visual effect

        //[Header("Optional variable")]
        //public bool followsParent = false; //boolean to determine whether or not this particle system should continously follow a parent
        [HideInInspector] public Transform parentTransform = null; //parent to follow if the particle system should continously follow a parent object
        public ParticleSystem[] particleSystems; //List of 5 particle systems that each play the visual effect. Each particle system in the array is associated with a unique player as well as a non player specific particle system
    }

    [Header("Visual effects that VFX Manager can play")]
    [SerializeField] private List<VisualEffectObject> playerVisualEffectObjects; //list of visual effects for players that the VFX Manager can play
    [SerializeField] private List<VisualEffectObject> spellVisualEffectObjects; //list of visual effects for spells that the VFX Manager can play

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
        InstantiateAllParticleSystems();
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
    private void InstantiateAllParticleSystems()
    {
        //instantiate player/general particle systems
        InstantiatePlayerParticleSystems();

        //instantiate spell particle systems
        InstantiateSpellParticleSystems();
    }

    //Function to instantiate all particle systems relating to players
    private void InstantiatePlayerParticleSystems()
    {
        //instantiate NUM_PARTICLESYSTEMS_PER_VFXOBJECT (5) particle systems per VisualEffectObject in playerVisualEffectObjects
        foreach (VisualEffectObject visualEffectObject in playerVisualEffectObjects)
        {
            //sanity check to make sure that particleSystemPrefab has been assigned for this visualEffectObject
            if (visualEffectObject.particleSystemPrefab == null)
            {
                //log an error
                Debug.LogError(gameObject.name + ": The particle system prefab for \"" + visualEffectObject.visualEffectName + "\" has not been assigned within playerVisualEffectObjects. Assign a particle system prefab to \"" + visualEffectObject.visualEffectName + "\"");

                //skip the rest of this foreach loop iteration
                continue;
            }

            //define and instantiate an empty gameobject to help organize the particle systems
            GameObject _headingGameobject = Instantiate(new GameObject(), this.gameObject.transform);
            _headingGameobject.name = visualEffectObject.particleSystemPrefab.name + "s";

            //allocate memory for the particleSystems array
            visualEffectObject.particleSystems = new ParticleSystem[NUM_PARTICLESYSTEMS_PER_VFXOBJECT];

            //instantiate NUM_PARTICLESYSTEMS_PER_VFXOBJECT (5) particle systems
            for (int i = 0; i < NUM_PARTICLESYSTEMS_PER_VFXOBJECT; i++)
            {
                //instantiate a particle system that is a child of the _headingGameobject 
                GameObject _createdParticleSystem = Instantiate(visualEffectObject.particleSystemPrefab, _headingGameobject.transform);

                //give the newly created particle system a unique name
                _createdParticleSystem.name = visualEffectObject.particleSystemPrefab.name + " #" + i.ToString();

                //add the newly created particle system to the particleSystems array for this VisualEffectObject
                visualEffectObject.particleSystems[i] = _createdParticleSystem.GetComponent<ParticleSystem>();
            }
        }
    }

    //Function to instantiate all particle systems relating to spells
    private void InstantiateSpellParticleSystems()
    {
        //loop through each player
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            //define and instantiate an empty gameobject to help organize the particle systems
            GameObject _headingGameobject = Instantiate(new GameObject(), this.gameObject.transform);
            _headingGameobject.name = "Spell Particle Systems for player #" + (i + 1).ToString();

            //loop through all the spells in a player's inventory
            foreach (SpellData _spell in GameManager.Instance.players[i].spellList)
            {
                ////instantiate a particle system that is a child of the ... 
                //GameObject _createdParticleSystem = Instantiate(_spell.projectileInstances[], _headingGameobject.transform);

                ////give the newly created particle system a unique name
                //_createdParticleSystem.name = visualEffectObject.particleSystemPrefab.name + " #" + i.ToString();

                ////add the newly created particle system to the particleSystems array for this VisualEffectObject
                //visualEffectObject.particleSystems[i] = _createdParticleSystem.GetComponent<ParticleSystem>();
            }
        }
    }

    //Function to destroy all particle systems
    private void DestroyAllParticleSystems()
    {
        ////loop through each player
        //for (int i = 0; i < GameManager.Instance.playerCount; i++)
        //{
        //    //loop through all spells in a player's inventory
        //    for (int j = 0; j < GameManager.Instance.players[i].spellList.Count; j++)
        //    {
        //        //CONTINUE HERE
        //    }
        //}
    }

    /// <summary>
    /// Play a visual effect with the name defined by "_nameOfSoundToPlay" at the position defined by "_spawnPos"
    /// </summary>
    /// <param name="_nameOfVisualEffectToPlay"> Name of the visual effect to be played</param>
    /// <param name="_spawnPos"> Position of the visual effect to be played</param>
    /// <param name="_playerNum"> Player number of the player who is spawning this visual effect. To spawn a visual effect without it being associated with a player, set _playerNum to 0. By default, set to 0 (not associated with a player)</param>
    /// <param name="_spawnFacingRight"> Whether or not the visual effect will spawn facing right. By default, set to true (facing right)</param>
    public void PlayVisualEffect(VisualEffects _nameOfVisualEffectToPlay, FixedVec2 _spawnPos, int _playerNum = 0, bool _spawnFacingRight = true, Transform _parentTransform = null, float _emissionRate = -1f)
    {
        //sanity check to make sure that there is a visual effect with name equal to _nameOfVisualEffectToPlay that exists within playerVisualEffectObjects
        if (playerVisualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified visual effect of name = \"" + _nameOfVisualEffectToPlay + "\" does not exist within playerVisualEffectObjects of the VFX_Manager script. Please specify a song that exists with playerVisualEffectObjects");

            //return
            return;
        }

        //sanity check to make sure that _playerNum is valid (always 0, 1, 2, 3, or 4)
        if(_playerNum < 0 && _playerNum > 4)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": _playerNum of \"" + _playerNum + "\" is not valid. Please make sure thet _playerNum is either 0, 1, 2, 3, or 4");

            //return
            return;
        }

        //get visual effect object
        VisualEffectObject _visualEffectObject = playerVisualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay);

        //Debug.Log("PlayerNum = " + _playerNum + ". And particleSystems has size = " + _visualEffectObject.particleSystems.Length);

        //if _parentTransform has been specified,...
        if (_parentTransform != null)
        {
            //make particle system a child of the _parentTransform
            _visualEffectObject.particleSystems[_playerNum].gameObject.transform.parent = _parentTransform;

            //assign parentTransform
            _visualEffectObject.parentTransform = _parentTransform;
        }

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

        //if _emissionRate is not the garbage defualt value,...
        if (_emissionRate != -1f)
        {
            //set emission rate over time of the particle system to _emissionRate
            var em = _visualEffectObject.particleSystems[_playerNum].emission;
            em.rateOverTime = _emissionRate;
        }

        //Debug.Log(gameObject.name + ": Playing visual effect of name = \"" + _nameOfVisualEffectToPlay + "\"");

        //play the visual effect
        _visualEffectObject.particleSystems[_playerNum].Play();
    }

    public void StopVisualEffect(VisualEffects _nameOfVisualEffectToPlay, int _playerNum = 0)
    {
        //sanity check to make sure that there is a visual effect with name equal to _nameOfVisualEffectToPlay that exists within playerVisualEffectObjects
        if (playerVisualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified visual effect of name = \"" + _nameOfVisualEffectToPlay + "\" does not exist within playerVisualEffectObjects of the VFX_Manager script. Please specify a song that exists with playerVisualEffectObjects");

            //return
            return;
        }

        //sanity check to make sure that _playerNum is valid (always 0, 1, 2, 3, or 4)
        if (_playerNum < 0 && _playerNum > 4)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": _playerNum of \"" + _playerNum + "\" is not valid. Please make sure thet _playerNum is either 0, 1, 2, 3, or 4");

            //return
            return;
        }

        //get visual effect object
        VisualEffectObject _visualEffectObject = playerVisualEffectObjects.Find(x => x.visualEffectName == _nameOfVisualEffectToPlay);

        ////if the visual effect for this player is already stopped,...
        //if (_visualEffectObject.particleSystems[_playerNum].isStopped)
        //{
        //    //do nothing and return
        //    return;
        //}

        //stop the particle effect
        _visualEffectObject.particleSystems[_playerNum].Stop();
    }
}
