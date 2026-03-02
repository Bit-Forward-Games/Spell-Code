using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
//using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.Audio;

public enum Sounds //enum to store the names of the sounds that can play
{ 
    JUMP, RUN, HIT, DEATH, ENTER_CODE_WEAVE, EXIT_CODE_WEAVE, CONTINUOUS_CODE_WEAVE, FAILED_EXIT_CODE_WEAVE, INPUT_CODE_UP, INPUT_CODE_RIGHT, INPUT_CODE_DOWN, INPUT_CODE_LEFT
}

[RequireComponent(typeof(AudioSource))]
public class SFX_Manager : MonoBehaviour
{
    /// <value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static SFX_Manager Instance { get; private set; }

    //AudioSource that will play sounds
    private AudioSource sfxAudioSource;

    //prefab for instantiated audio sources
    [SerializeField] private GameObject audioSourcePrefab;

    //Object to hold the data for each sound
    [Serializable] private class SoundObject
    {
        public Sounds soundName; //name of the sound
        public List<AudioClip> possibleSounds; //list of sounds that can play when this sound object is told to play
        public AudioClip secretVersionOfSound; //secret version of the sound to play
        public bool canRepeat = false; //whether or not this sound can repeat
        [HideInInspector] public AudioSource[] audioSources = new AudioSource[4]; //array to hold audio sources for if and when this song repeats 
    }

    [Header("Sounds that SFX Manager can play")]
    [SerializeField] private List<SoundObject> soundObjects; //list of sounds that the SFX Manager can play

    void Awake()
    {
        //assign sfxAudioSource
        sfxAudioSource = gameObject.GetComponent<AudioSource>();

        //if there is an instance of SFX_Manager that is NOT this one,...
        if (Instance != null && Instance != this)
        {
            //delete myself
            Destroy(this);
        }
        //else there is only 1 instance of SFX_Manager,...
        else
        {
            //set instance to this instance of SFX_Manager
            Instance = this;
        }
    }

    private void Start()
    {
        //instantiate audio sources
        InstantiateAudioSources();
    }

    /// <summary>
    /// Instantiate all the Audio Sources for each SoundObject
    /// </summary>
    public void InstantiateAudioSources()
    {
        //iterate through each SoundObject in soundObjects,...
        foreach(SoundObject _soundObject in soundObjects)
        {
            //iterate through each AudioSource in the audioSources array for each _soundObject,...
            for (int i = 0; i < _soundObject.audioSources.Length; i++)
            {
                //if this SoundObject can repeat,...
                if (_soundObject.canRepeat)
                {
                    //create the audio source object
                    GameObject audioSourceObject = Instantiate(audioSourcePrefab, this.gameObject.transform);

                    //assign the audio source of the audio source object
                    _soundObject.audioSources[i] = audioSourceObject.GetComponent<AudioSource>();

                    //give the audio source object a unique name
                    audioSourceObject.name = _soundObject.possibleSounds[0].name + " Audio Source #" + (i + 1).ToString();
                }
            }
        }
    }

    /// <summary>
    /// Play a sound with the name defined by "_soundName"
    /// </summary>
    /// <param name="_soundName"> Sound to be played by the SFX Handler</param>
    /// <param name="_minPitchShift"> minimum pitch shift for the sound. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for the sound. By default, set to 1.2f</param>
    /// <param name="_chanceToPlaySecretVersion"> change (out of 1f) to play the secret version of the sound</param>
    public void PlaySound(Sounds _soundName, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f, float _chanceToPlaySecretVersion = 0.0f)
    {
        //clamp _chanceToPlaySecretVersion between 0 and 1
        _chanceToPlaySecretVersion = Mathf.Clamp01(_chanceToPlaySecretVersion);

        //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
        if (soundObjects.Find(x => x.soundName == _soundName) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + _soundName + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a sound that exists with availableSounds");

            //return
            return;
        }

        //save the appropriate SoundObject since we know it exists
        SoundObject _soundObject = soundObjects.Find(x => x.soundName == _soundName);

        //sanity check to make sure that _soundName has an AudioClip associated with it
        if(_soundObject.possibleSounds.Count <= 0 || _soundObject.possibleSounds[0] == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": The SoundObject for \"" + _soundName + "\" does not contain an AudioClip to play. Please add an AudioClip to the SoundObject for \"" + _soundName + "\" in availableSounds");

            //return
            return;
        }

        //Randomize pitch between minPitchShift and maxPitchShift
        sfxAudioSource.pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

        //if this sound can play a secret version,...
        if (_soundObject.secretVersionOfSound != null && _chanceToPlaySecretVersion <= 0.0f)
        {
            //do a random roll to see if the secret version will play
            float _randomRoll = UnityEngine.Random.Range(0.0f, 1.0f);

            //if the secret version can play, play it and return
            if (_randomRoll <= _chanceToPlaySecretVersion)
            {
                //load and play the the secret version of sound with name equal to nameOfSoundToPlay
                sfxAudioSource.PlayOneShot(_soundObject.secretVersionOfSound, sfxAudioSource.volume);

                //return
                return;
            }
        }

        //pick a random sound from _possibleSounds to play
        int _randomSoundIndex = UnityEngine.Random.Range(0, _soundObject.possibleSounds.Count);

        //load and play the sound with name equal to nameOfSoundToPlay
        sfxAudioSource.PlayOneShot(_soundObject.possibleSounds[_randomSoundIndex], sfxAudioSource.volume);
    }

    /// <summary>
    /// Start to repeatedly play the sound specified by _soundName. Note: "StartRepeatingSound()" cannot play secret versions of sounds
    /// </summary>
    /// <param name="_soundName"> Sound to be start be played by the SFX Handler. This sound will play on repeat until StopRepeatingSound(_soundName) is called</param>
    /// <param name="_playRate"> [CURRENTLY NOT IN USE] Rate at which this sound will repeat. Note that this is the time between the start of each sound</param>
    /// <param name="_playerIndex"> player index of the player who is repeatedly playing the sound. Note that player 1 is _playerIndex == 0 and so on</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void StartRepeatingSound(Sounds _soundName, float _playRate, int _playerIndex, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
        if (soundObjects.Find(x => x.soundName == _soundName) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + _soundName + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a sound that exists with availableSounds");

            //return
            return;
        }

        //save the appropriate SoundObject since we know it exists
        SoundObject _soundObject = soundObjects.Find(x => x.soundName == _soundName);

        //sanity check to make sure that this sound can be repeatedly played
        if (_soundObject.canRepeat == false)
        {
            //return
            return;
        }

        //sanity check to make sure that _soundName has an AudioClip associated with it
        if (_soundObject.possibleSounds.Count <= 0 || _soundObject.possibleSounds[0] == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": The SoundObject for \"" + _soundName + "\" does not contain an AudioClip to play. Please add an AudioClip to the SoundObject for \"" + _soundName + "\" in availableSounds");

            //return
            return;
        }

        //sanity check to make sure that StartRepeatingSound was not already called for _soundName
        if (_soundObject.audioSources[_playerIndex].isPlaying == true)
        {
            //return
            return;
        }

        //Randomize pitch between minPitchShift and maxPitchShift
        _soundObject.audioSources[_playerIndex].pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

        //pick a random sound from _possibleSounds to play
        int _randomSoundIndex = UnityEngine.Random.Range(0, _soundObject.possibleSounds.Count);

        //set the resource of the audioSourceObject
        _soundObject.audioSources[_playerIndex].resource = _soundObject.possibleSounds[_randomSoundIndex];

        //start to repeatedly play the sound 
        _soundObject.audioSources[_playerIndex].Play();
    }

    /// <summary>
    /// Stop playing the sound specified by _soundName
    /// </summary>
    /// <param name="_soundName"> Sound to be start be played by the SFX Handler. This sound will play on repeat until StopRepeatingSound(_soundName) is called</param>
    /// <param name="_playerIndex"> player index of player that is playing this sound. Note that player 1 is _playerIndex == 0 and so on</param>
    public void StopRepeatingSound(Sounds _soundName, int _playerIndex)
    {
        //stop playing _soundname of _playerIndex
        soundObjects.Find(x => x.soundName == _soundName).audioSources[_playerIndex].Stop();
    }
}