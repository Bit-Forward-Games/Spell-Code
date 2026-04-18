using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.VisualScripting.Antlr3.Runtime;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using BestoNet.Types;
using UnityEngine.VFX;
using static UnityEngine.ParticleSystem;
using UnityEngine.UI.Extensions.Tweens;
using static SFX_Manager;
using UnityEditor;
using TMPro;

public enum VisualEffects
{
    DASH_DUST, BOUNTY_AURA, DEMON_AURA, STOCK_AURA, FLOW_STATE_AURA, REPS_AURA, 
    DAMAGE, JUMP_DUST, DEATH, BOUNTY_DEATH,
    VWAVE_FLOPPY_SPAWN, KILLEEZ_FLOPPY_SPAWN, DEMONX_FLOPPY_SPAWN, BIGSTOX_FLOPPY_SPAWN, 
    COMBO_BREAKER, CODE_FAIL,
    VWAVE_CAST, KILLEEZ_CAST, DEMONX_CAST, BIGSTOX_CAST,
    BLOCKED, BLOCKING
}
public class VFX_Manager : MonoBehaviour
{
    //constant integer to store the number of particle systems per VisualEffectObject
    private const uint DEFAULT_NUM_PARTICLESYSTEMS_PER_VFXOBJECT = 5;

    /// <value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static VFX_Manager Instance { get; private set; }

    //Object to hold the data for each visual effect
    [Serializable]
    private class VisualEffectObject
    {
        [Header("Mandatory variables")]
        public VisualEffects visualEffectName; //name of the visual effect
        public GameObject particleSystemPrefab; //prefab of the particle system for this visual effect

        [Range(1, 10)]
        public uint numParticleSystemsPerPlayer = 1; //number of particle systems to spawn on Awake. By default, this number is set to 1

        //[HideInInspector] public Transform parentTransform = null; //parent to follow if the particle system should continously follow a parent object
        [HideInInspector] public List<ParticleSystem>[] particleSystems; //Array of Lists of particle systems that each play the visual effect. Each list of particle systems in the array is associated with a unique player as well as a non player specific particle system list
    }

    [Header("Visual effects that VFX Manager can play")]
    [SerializeField] private List<VisualEffectObject> playerVisualEffectObjects; //list of visual effects for players that the VFX Manager can play
    [SerializeField] private List<VisualEffectObject> spellVisualEffectObjects; //list of visual effects for spells that the VFX Manager can play

    private bool TryGetVisualEffectObject(VisualEffects effectName, int playerNum, out VisualEffectObject visualEffectObject)
    {
        visualEffectObject = playerVisualEffectObjects.Find(x => x.visualEffectName == effectName);
        if (visualEffectObject == null)
        {
            Debug.LogWarning(gameObject.name + ": Specified visual effect of name = \"" + effectName + "\" does not exist within playerVisualEffectObjects of the VFX_Manager script.");
            return false;
        }

        if (playerNum < 0 || playerNum >= DEFAULT_NUM_PARTICLESYSTEMS_PER_VFXOBJECT)
        {
            Debug.LogWarning(gameObject.name + ": _playerNum of \"" + playerNum + "\" is not valid. Please make sure that _playerNum is either 0, 1, 2, 3, or 4");
            return false;
        }

        if (visualEffectObject.particleSystems == null ||
            playerNum >= visualEffectObject.particleSystems.Length ||
            visualEffectObject.particleSystems[playerNum] == null ||
            visualEffectObject.particleSystems[playerNum].Count == 0)
        {
            return false;
        }

        return true;
    }

    private ParticleSystem GetFirstValidParticleSystem(List<ParticleSystem> particleSystems)
    {
        if (particleSystems == null)
        {
            return null;
        }

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null)
            {
                return particleSystem;
            }
        }

        return null;
    }

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

    private void Start()
    {
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
        //InstantiateSpellParticleSystems();
    }

    //Function to instantiate all particle systems relating to players
    private void InstantiatePlayerParticleSystems()
    {
        //instantiate the appropriate number of particle systems per VisualEffectObject in playerVisualEffectObjects
        foreach (VisualEffectObject _visualEffectObject in playerVisualEffectObjects)
        {
            //sanity check to make sure that particleSystemPrefab has been assigned for this visualEffectObject
            if (_visualEffectObject.particleSystemPrefab == null)
            {
                //log an error
                Debug.LogError(gameObject.name + ": The particle system prefab for \"" + _visualEffectObject.visualEffectName + "\" has not been assigned within playerVisualEffectObjects. Assign a particle system prefab to \"" + _visualEffectObject.visualEffectName + "\"");

                //skip the rest of this foreach loop iteration
                continue;
            }

            //define and instantiate an empty gameobject to help organize the particle systems
            GameObject _headingGameobject = new GameObject();
            _headingGameobject.transform.SetParent(VFX_Manager.Instance.gameObject.transform);
            _headingGameobject.name = _visualEffectObject.particleSystemPrefab.name + "s";

            //calculate the number of particle systems for this visual effect object with where _numParticleSystemsToInstantiate is equal to 1 + (4 * numParticleSystemsPerPlayer)
            //uint _numParticleSystemsToInstantiate = 1 + (4 * _visualEffectObject.numParticleSystemsPerPlayer);

            //allocate memory for the particleSystems array and each list for each element within that array 
            _visualEffectObject.particleSystems = new List<ParticleSystem>[DEFAULT_NUM_PARTICLESYSTEMS_PER_VFXOBJECT];
            for (int i = 0; i < _visualEffectObject.particleSystems.Length; i++)
            {
                _visualEffectObject.particleSystems[i] = new List<ParticleSystem>();
            }

            //instantiate a particle system for each element of each list of each array
            for (int i = 0; i < _visualEffectObject.particleSystems.Length; i++)
            {
                for (int j = 0; j < _visualEffectObject.numParticleSystemsPerPlayer; j++)
                {
                    //instantiate a particle system that is a child of the _headingGameobject 
                    GameObject _createdParticleSystem = Instantiate(_visualEffectObject.particleSystemPrefab, _headingGameobject.transform);

                    //give the newly created particle system a unique name
                    _createdParticleSystem.name = _visualEffectObject.particleSystemPrefab.name + " #" + (i + j).ToString();

                    //add the newly created particle system to the particleSystems array for this player number (i)
                    _visualEffectObject.particleSystems[i].Add(_createdParticleSystem.GetComponent<ParticleSystem>());
                }
            }
        }
    }

    //Function to instantiate all particle systems relating to spells
    private void InstantiateSpellParticleSystems()
    {
        //if there are no players,...
        if (GameManager.Instance.playerCount == 0)
        {
            //return
            return;
        }

        //loop through each player
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            //define and instantiate an empty gameobject to help organize the particle systems
            GameObject _headingGameobject = new GameObject();
            _headingGameobject.transform.SetParent(VFX_Manager.Instance.gameObject.transform);
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
    /// <param name="_spawnPos"> Position of the visual effect to be played.</param>
    /// <param name="_playerNum"> Player number of the player who is spawning this visual effect. To spawn a visual effect without it being associated with a player, set _playerNum to 0. By default, set to 0 (not associated with a player).</param>
    /// <param name="_spawnFacingRight"> Whether or not the visual effect will spawn facing right. By default, set to true (facing right).</param>
    /// <param name="_parentTransform"> Parent transform that this particle effect should follow. By default, set to null.</param>
    /// <param name="_emissionRate"> Rate at which particles are emitted from the particle system. By default, set to -1 which indicates that the particle system should emit at its default rate.</param>
    /// <param name="_particleLifetime"> How long (in seconds) each particle emitted by the particle system will last for. By default, set to -1 which indicates that the particles will last for their default lifetime.</param>
    public void PlayVisualEffect(VisualEffects _nameOfVisualEffectToPlay, FixedVec2 _spawnPos, int _playerNum = 0, bool _spawnFacingRight = true, Transform _parentTransform = null, float _emissionRate = -1f, float _particleLifetime = -1f)
    {
        if (!TryGetVisualEffectObject(_nameOfVisualEffectToPlay, _playerNum, out VisualEffectObject _visualEffectObject))
        {
            return;
        }

        //Debug.Log("PlayerNum = " + _playerNum + ". And particleSystems has size = " + _visualEffectObject.particleSystems.Length);

        //find the appropriate particle system based on playerNum and what particle systems associated with _playerNum are already playing
        ParticleSystem _particleSystem = null;
        foreach (ParticleSystem _listedParticleSystem in _visualEffectObject.particleSystems[_playerNum])
        {
            if (_listedParticleSystem == null)
            {
                continue;
            }
            //if the particle system is NOT already playing,...
            if(!_listedParticleSystem.isPlaying)
            {
                //if (_visualEffectObject.visualEffectName == VisualEffects.DASH_DUST) { Debug.Log("VFX Debug | Dash dust particle found = " + _listedParticleSystem.gameObject.name); }
                //set _particleSystem to the particle system in question
                _particleSystem = _listedParticleSystem;

                //break out of the foreach loop
                break;
            }
        }

        //if no available particle system was found,...
        if(_particleSystem == null)
        {
            _particleSystem = GetFirstValidParticleSystem(_visualEffectObject.particleSystems[_playerNum]);
            if (_particleSystem == null)
            {
                return;
            }
        }

        //if _parentTransform has been specified,...
        if (_parentTransform != null)
        {
            //make particle system a child of the _parentTransform
            _particleSystem.gameObject.transform.parent = _parentTransform;
            
            //Debug.Log("VFX Debug | " + _particleSystem.gameObject.name + "'s Parent has been set to " + _parentTransform.gameObject.name);
        }

        //convert _spawnPos to a Vector3
        Vector3 _spawnPosVector3 = new Vector3(_spawnPos.X.ToFloat(), _spawnPos.Y.ToFloat(), 0f);

        //set position of particle system to Vector2 version of _spawnPos
        _particleSystem.gameObject.transform.position = _spawnPosVector3;

        //change particle system direction based on _spawnFacingRight
        if(_spawnFacingRight == true)
        {
            _particleSystem.gameObject.transform.localScale = new Vector3(1f, _particleSystem.gameObject.transform.localScale.y, _particleSystem.gameObject.transform.localScale.z);
            _particleSystem.gameObject.transform.localEulerAngles = new Vector3(_visualEffectObject.particleSystemPrefab.gameObject.transform.eulerAngles.x, _visualEffectObject.particleSystemPrefab.gameObject.transform.eulerAngles.y, _visualEffectObject.particleSystemPrefab.gameObject.transform.eulerAngles.z);
        }
        else
        {
            _particleSystem.gameObject.transform.localScale = new Vector3(-1f, _particleSystem.gameObject.transform.localScale.y, _particleSystem.gameObject.transform.localScale.z);
            _particleSystem.gameObject.transform.localEulerAngles = new Vector3(_visualEffectObject.particleSystemPrefab.gameObject.transform.eulerAngles.x, _visualEffectObject.particleSystemPrefab.gameObject.transform.eulerAngles.y, _visualEffectObject.particleSystemPrefab.gameObject.transform.eulerAngles.z * -1f);
        }

        //if _emissionRate is not the garbage default value,...
        if (_emissionRate != -1f && _visualEffectObject.numParticleSystemsPerPlayer == 1)
        {
            ParticleSystem emissionTarget = GetFirstValidParticleSystem(_visualEffectObject.particleSystems[_playerNum]);
            if (emissionTarget == null)
            {
                return;
            }

            var em = emissionTarget.emission;

            //turn off emmision
            emissionTarget.Stop();
            em.enabled = false;

            //set emission rate over time of the particle system to _emissionRate
            em.rateOverTime = _emissionRate;
            em.enabled = true;
        }

        //if _particleLifetime is not the garbage default value,...
        if (_particleLifetime != -1f)
        {
            //set lifetime of the particle system to _particleLifetime
            var main = _particleSystem.main;
            main.startLifetime = _particleLifetime;
        }

        //Debug.Log(gameObject.name + ": Playing visual effect of name = \"" + _nameOfVisualEffectToPlay + "\"");

        //play the visual effect
        _particleSystem.Play();
    }

    public void StopVisualEffect(VisualEffects _nameOfVisualEffectToPlay, int _playerNum = 0)
    {
        if (!TryGetVisualEffectObject(_nameOfVisualEffectToPlay, _playerNum, out VisualEffectObject _visualEffectObject))
        {
            return;
        }

        //for each particle system for this _nameOfVisualEffectToPlay and _playerNum,...
        foreach (ParticleSystem _particleSystem in _visualEffectObject.particleSystems[_playerNum])
        {
            if (_particleSystem == null)
            {
                continue;
            }
            //stop playing the particle effect
            _particleSystem.Stop();
        }
    }

    public bool IsVisualEffecyPlaying(VisualEffects _nameOfVisualEffectToPlay, int _playerNum = 0)
    {
        if (!TryGetVisualEffectObject(_nameOfVisualEffectToPlay, _playerNum, out VisualEffectObject _visualEffectObject))
        {
            return false;
        }

        //return true if any of the particle systems in question are playing
        foreach (ParticleSystem _particleSystem in _visualEffectObject.particleSystems[_playerNum])
        {
            if (_particleSystem == null)
            {
                continue;
            }
            if (_particleSystem.isPlaying)
            {
                return true;
            }
        }

        //return false if NONE of the particle systems in question are playing 
        return false;
    }
}
