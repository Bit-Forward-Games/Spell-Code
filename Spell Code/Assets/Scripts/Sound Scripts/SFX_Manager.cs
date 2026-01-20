using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum Sounds
{ 
    JUMP, RUN, HIT, DEATH, ENTER_CODE_WEAVE, EXIT_CODE_WEAVE
}

//[Serializable] class SFXTuple { public string nameOfSoundToPlay; public AudioClip soundClip; }

[RequireComponent(typeof(AudioSource))]
public class SFX_Manager : MonoBehaviour
{
    //<value>Property <c>Instance</c> is the single instance of the SFX_Manager.</value>
    public static SFX_Manager Instance { get; private set; }

    //AudioSource that will play sounds
    private AudioSource sfxAudioSource;

    //Object to hold the data for each sound
    [Serializable]
    private class SoundObject
    {
        public Sounds soundName; //name of the sound
        public List<AudioClip> possibleSounds; //list of sounds that can play when this sound object is told to play
    }

    [Header("Sounds that SFX Manager can play")]
    [SerializeField] private List<SoundObject> soundObjects;

    void Awake()
    {
        //assign sfxAudioSource
        sfxAudioSource = gameObject.GetComponent<AudioSource>();
    }

    /// <summary>
    /// Play a sound with the name defined by "_nameOfSoundToPlay"
    /// </summary>
    /// <param name="_nameOfSoundToPlay"> Sound to be played by the SFX Handler</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void PlaySound(Sounds _nameOfSoundToPlay, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        ////sanity check to make sure that nameOfSoundToPlay is specified
        //if (_nameOfSoundToPlay == null)
        //{
        //    //log a warning
        //    Debug.LogWarning(gameObject.name + ": Please specify a sound to play");

        //    //return
        //    return;
        //}

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
        int randomSoundIndex = UnityEngine.Random.Range(0, soundObjects.Count - 1);

        //load and play the sound with name equal to nameOfSoundToPlay
        sfxAudioSource.PlayOneShot(soundObjects.Find(y => y.soundName == _nameOfSoundToPlay).possibleSounds[randomSoundIndex], sfxAudioSource.volume);
    }

    /// <summary>
    /// Play a sound from a List of sound names with the names defined by "_namesOfSoundsThatCanPlay"
    /// </summary>
    /// <param name="_namesOfSoundsThatCanPlay"> List of sounds to be randomly chosen from and played by the SFX Handler</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    //public void PlayRandomSound(List<Sounds> _namesOfSoundsThatCanPlay, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    //{
    //    //choose a random name of a sound from namesOfSoundsThatCanPlay
    //    string _randomSoundName = _namesOfSoundsThatCanPlay[UnityEngine.Random.Range(0, _namesOfSoundsThatCanPlay.Count)];

    //    //play the sound with randomSoundName 
    //    PlaySound(_randomSoundName, _minPitchShift, _maxPitchShift);
    //}
}
