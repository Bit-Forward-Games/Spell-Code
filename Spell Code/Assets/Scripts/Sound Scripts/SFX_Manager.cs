using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.Audio;

public enum Sounds
{ 
    JUMP, RUN, HIT, DEATH, ENTER_CODE_WEAVE, EXIT_CODE_WEAVE, CONTINUOUS_CODE_WEAVE, FAILED_EXIT_CODE_WEAVE, INPUT_CODE
}

[RequireComponent(typeof(AudioSource))]
public class SFX_Manager : MonoBehaviour
{
    //<value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static SFX_Manager Instance { get; private set; }

    //AudioSource that will play sounds
    private AudioSource sfxAudioSource;

    //Object to hold the data for each sound
    [Serializable] private class SoundObject
    {
        public Sounds soundName; //name of the sound
        public List<AudioClip> possibleSounds; //list of sounds that can play when this sound object is told to play
        [HideInInspector] public bool[] playersWhoAreRepeatedlyPlaying = new bool[4]; //boolean array to record whether or not this sound is repeatedly playing for each of the 4 players
    }

    [Header("Sounds that SFX Manager can play")]
    [SerializeField] private List<SoundObject> soundObjects;

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

    /// <summary>
    /// Play a sound with the name defined by "_nameOfSoundToPlay"
    /// </summary>
    /// <param name="_nameOfSoundToPlay"> Sound to be played by the SFX Handler</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void PlaySound(Sounds _nameOfSoundToPlay, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
        if (soundObjects.Find(x => x.soundName == _nameOfSoundToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + _nameOfSoundToPlay + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a song that exists with availableSounds");

            //return
            return;
        }

        //save the appropriate SoundObject since we know it exists
        SoundObject _soundObject = soundObjects.Find(x => x.soundName == _nameOfSoundToPlay);

        //sanity check to make sure that _nameOfSoundToPlay has an AudioClip associated with it
        if(_soundObject.possibleSounds.Count <= 0)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": The SoundObject for \"" + _nameOfSoundToPlay + "\" does not contain an AudioClip to play. Please add an AudioClip to the SoundObject for \"" + _nameOfSoundToPlay + "\" in availableSounds");

            //return
            return;
        }

        //another sanity check to make sure that _nameOfSoundToPlay has an AudioClip associated with it
        if (_soundObject.possibleSounds[0] == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": The SoundObject for \"" + _nameOfSoundToPlay + "\" does not contain an AudioClip to play. Please add an AudioClip to the SoundObject for \"" + _nameOfSoundToPlay + "\" in availableSounds");

            //return
            return;
        }

        //Randomize pitch between minPitchShift and maxPitchShift
        sfxAudioSource.pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

        //pick a random sound from _possibleSounds to play
        int _randomSoundIndex = UnityEngine.Random.Range(0, _soundObject.possibleSounds.Count - 1);

        //load and play the sound with name equal to nameOfSoundToPlay
        sfxAudioSource.PlayOneShot(_soundObject.possibleSounds[_randomSoundIndex], sfxAudioSource.volume);
    }

    /// <summary>
    /// Start to repeatedly play the sound specified by _soundName
    /// </summary>
    /// <param name="_soundName"> Sound to be start be played by the SFX Handler. This sound will play on repeat until StopRepeatingSound(_soundName) is called</param>
    /// <param name="_playRate"> rate at which this sound will repeat. Note that this is the time between the start of each sound</param>
    /// <param name="_playerIndex"> player index of the player who is repeatedly playing the sound. Note that player 1 is _playerIndex == 0 and so on</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void StartRepeatingSound(Sounds _soundName, float _playRate, int _playerIndex, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //sanity check to make sure that StartRepeatingSound was not already called for _soundName
        if (soundObjects.Find(x => x.soundName == _soundName).playersWhoAreRepeatedlyPlaying[_playerIndex] == true)
        {
            //return
            return;
        }

        //set isRepeatedlyPlaying of _soundName to true
        soundObjects.Find(y => y.soundName == _soundName).playersWhoAreRepeatedlyPlaying[_playerIndex] = true;

        //Repeatedly play _soundName so long as isRepeatedlyPlaying is true
        StartCoroutine(RepeatedlyPlay(_soundName, _playRate, _playerIndex, _minPitchShift, _maxPitchShift));
    }

    /// <summary>
    /// Stop playing the sound specified by _soundName
    /// </summary>
    /// <param name="_soundName"> Sound to be start be played by the SFX Handler. This sound will play on repeat until StopRepeatingSound(_soundName) is called</param>
    /// <param name="_playerIndex"> player index of player that is playing this sound. Note that player 1 is _playerIndex == 0 and so on</param>
    public void StopRepeatingSound(Sounds _soundName, int _playerIndex)
    {
        //set isRepeatedlyPlaying of _soundName to false
        soundObjects.Find(x => x.soundName == _soundName).playersWhoAreRepeatedlyPlaying[_playerIndex] = false;
    }

    //Coroutine to repeatedly play a sound then wait for a time
    private IEnumerator RepeatedlyPlay(Sounds _soundName, float _playRate, int _playerIndex, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //play _soundName
        SFX_Manager.Instance.PlaySound(_soundName, _minPitchShift, _maxPitchShift);

        //wait for _playRate seconds
        yield return new WaitForSeconds(_playRate);

        //is this song should still repeat,...
        if (soundObjects.Find(x => x.soundName == _soundName).playersWhoAreRepeatedlyPlaying[_playerIndex] == true)
        {
            //Repeatedly play _soundName 
            StartCoroutine(RepeatedlyPlay(_soundName, _playRate, _playerIndex, _minPitchShift, _maxPitchShift));
        }
    }
}