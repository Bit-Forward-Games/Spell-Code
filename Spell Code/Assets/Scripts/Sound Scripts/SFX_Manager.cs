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

//[Serializable] class SFXTuple { public string nameOfSoundToPlay; public AudioClip soundClip; }

[RequireComponent(typeof(AudioSource))]
public class SFX_Manager : MonoBehaviour
{
    //<value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static SFX_Manager Instance { get; private set; }

    //AudioSource that will play sounds
    private AudioSource sfxAudioSource;

    //list of player AudioSources that will play continous sounds for each player
    //[SerializeField] private List<AudioSource> playerAudioSources;

    //Object to hold the data for each sound
    [Serializable]
    private class SoundObject
    {
        public Sounds soundName; //name of the sound
        public List<AudioClip> possibleSounds; //list of sounds that can play when this sound object is told to play
        [HideInInspector] public bool[] playersWhoAreRepeatedlyPlaying = new bool[4]; //boolean to record whether or not this sound is repeatedly playing
        //[HideInInspector] public int playerWhoIsRepeatedlyPlayingSound = 0; //integer to record the number of the player who is repeatedly playing the sound
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

        //Randomize pitch between minPitchShift and maxPitchShift
        sfxAudioSource.pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

        //pick a random sound from _possibleSounds to play
        int randomSoundIndex = UnityEngine.Random.Range(0, soundObjects.Find(y => y.soundName == _nameOfSoundToPlay).possibleSounds.Count - 1);

        //load and play the sound with name equal to nameOfSoundToPlay
        sfxAudioSource.PlayOneShot(soundObjects.Find(z => z.soundName == _nameOfSoundToPlay).possibleSounds[randomSoundIndex], sfxAudioSource.volume);
    }

    /// <summary>
    /// Start to repeatedly play the sound specified by _nameOfSoundToStartPlaying
    /// </summary>
    /// <param name="_nameOfSoundToStartPlaying"> Sound to be start be played by the SFX Handler. This sound will play on repeat until StopRepeatingSound(_nameOfSoundToStartPlaying) is called</param>
    /// <param name="_timeBetweenPlays"> rate at which this sound will repeat</param>
    /// <param name="_playerWhoIsRepeatedlyPlayingSound"> player number of the player who is repeatedly playing the sound</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void StartRepeatingSound(Sounds _nameOfSoundToStartPlaying, float _timeBetweenPlays, int _playerWhoIsRepeatedlyPlayingSound, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //sanity check to make sure that StartRepeatingSound was not already called for _nameOfSoundToStartPlaying
        if (soundObjects.Find(x => x.soundName == _nameOfSoundToStartPlaying).playersWhoAreRepeatedlyPlaying[_playerWhoIsRepeatedlyPlayingSound] == true)
        {
            //return
            return;
        }

        //if(_nameOfSoundToStartPlaying == Sounds.RUN && soundObjects.Find(y => y.soundName == _nameOfSoundToStartPlaying).isRepeatedlyPlaying == false)
        //    sfxAudioSource.resource = soundObjects.Find(z => z.soundName == _nameOfSoundToStartPlaying).possibleSounds[0];

        //set isRepeatedlyPlaying of _nameOfSoundToStartPlaying to true
        soundObjects.Find(y => y.soundName == _nameOfSoundToStartPlaying).playersWhoAreRepeatedlyPlaying[_playerWhoIsRepeatedlyPlayingSound] = true;

        //Repeatedly play _nameOfSoundToStartPlaying so long as isRepeatedlyPlaying is true
        StartCoroutine(RepeatedlyPlay(_nameOfSoundToStartPlaying, _timeBetweenPlays, _playerWhoIsRepeatedlyPlayingSound, _minPitchShift, _maxPitchShift));
    }

    /// <summary>
    /// Stop playing the sound specified by _nameOfSoundToStartPlaying
    /// </summary>
    /// <param name="_nameOfSoundToStartPlaying"> Sound to be start be played by the SFX Handler. This sound will play on repeat until StopRepeatingSound(_nameOfSoundToStartPlaying) is called</param>
    /// <param name="_playerWhoIsRepeatedlyPlayingSound"> player number of player that is playing this sound</param>
    public void StopRepeatingSound(Sounds _nameOfSoundToStartPlaying, int _playerWhoIsRepeatedlyPlayingSound)
    {
        //set isRepeatedlyPlaying of _nameOfSoundToStartPlaying to false
        soundObjects.Find(x => x.soundName == _nameOfSoundToStartPlaying).playersWhoAreRepeatedlyPlaying[_playerWhoIsRepeatedlyPlayingSound] = false;
    }

    //Coroutine to repeatedly play a sound then wait for a time
    private IEnumerator RepeatedlyPlay(Sounds _nameOfSoundToStartPlaying, float _timeBetweenPlays, int _playerWhoIsRepeatedlyPlayingSound, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //play _nameOfSoundToStartPlaying
        SFX_Manager.Instance.PlaySound(_nameOfSoundToStartPlaying, _minPitchShift, _maxPitchShift);

        //wait for _timeBetweenPlays seconds
        yield return new WaitForSeconds(_timeBetweenPlays);

        //is this song should still repeat,...
        if (soundObjects.Find(x => x.soundName == _nameOfSoundToStartPlaying).playersWhoAreRepeatedlyPlaying[_playerWhoIsRepeatedlyPlayingSound] == true)
        {
            //Repeatedly play _nameOfSoundToStartPlaying 
            StartCoroutine(RepeatedlyPlay(_nameOfSoundToStartPlaying, _timeBetweenPlays, _playerWhoIsRepeatedlyPlayingSound, _minPitchShift, _maxPitchShift));
        }
    }

    ///// <summary>
    ///// Play a sound with the name defined by "_nameOfSoundToStartPlaying"
    ///// </summary>
    ///// <param name="_nameOfSoundToStartPlaying"> Sound to start playing</param>
    ///// <param name="_playerWhoIsRepeatedlyPlayingSound"> player number of player that is playing this sound</param>
    ///// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    ///// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    //private void PlayPlayerSound(Sounds _nameOfSoundToStartPlaying, int _playerWhoIsRepeatedlyPlayingSound, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    //{
    //    //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
    //    if (soundObjects.Find(x => x.soundName == _nameOfSoundToStartPlaying) == null)
    //    {
    //        //log a warning
    //        Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + _nameOfSoundToStartPlaying + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a song that exists with availableSounds");

    //        //return
    //        return;
    //    }

    //    //find appropriate audio source given _playerWhoIsRepeatedlyPlayingSound
    //    AudioSource audioSource = playerAudioSources[_playerWhoIsRepeatedlyPlayingSound];

    //    //Randomize pitch between minPitchShift and maxPitchShift
    //    audioSource.pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

    //    //pick a random sound from _possibleSounds to play
    //    int randomSoundIndex = UnityEngine.Random.Range(0, soundObjects.Find(y => y.soundName == _nameOfSoundToStartPlaying).possibleSounds.Count - 1);

    //    //load and play the sound with name equal to nameOfSoundToPlay
    //    audioSource.PlayOneShot(soundObjects.Find(z => z.soundName == _nameOfSoundToStartPlaying).possibleSounds[randomSoundIndex], sfxAudioSource.volume);
    //}

    //private void PlaySoundNotOneShot(Sounds _nameOfSoundToPlay, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    //{
    //    //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
    //    if (soundObjects.Find(x => x.soundName == _nameOfSoundToPlay) == null)
    //    {
    //        //log a warning
    //        Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + _nameOfSoundToPlay + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a song that exists with availableSounds");

    //        //return
    //        return;
    //    }

    //    //Randomize pitch between minPitchShift and maxPitchShift
    //    sfxAudioSource.pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

    //    //pick a random sound from _possibleSounds to play
    //    //int randomSoundIndex = UnityEngine.Random.Range(0, soundObjects.Find(y => y.soundName == _nameOfSoundToPlay).possibleSounds.Count - 1);

    //    //load and play the sound with name equal to nameOfSoundToPlay 
    //    sfxAudioSource.Play();
    //}
}
