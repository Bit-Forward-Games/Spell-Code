using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

//[Serializable] class SFXTuple { public string nameOfSoundToPlay; public AudioClip soundClip; }

[RequireComponent(typeof(AudioSource))]
public class SFX_Handler : MonoBehaviour
{
    //AudioSource that will play sounds
    private AudioSource sfxAudioSource;

    [Header("Sounds that SFX Manager can play")]
    //[SerializeField] private List<SFXTuple> sounds;
    [SerializeField] private List<AudioClip> availableSounds;

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
    public void PlaySound(string _nameOfSoundToPlay, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //sanity check to make sure that nameOfSoundToPlay is specified
        if (_nameOfSoundToPlay == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Please specify a sound to play");

            //return
            return;
        }

        //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
        if (availableSounds.Find(x => x.name == _nameOfSoundToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + _nameOfSoundToPlay + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a song that exists with availableSounds");

            //return
            return;
        }

        //Randomize pitch between minPitchShift and maxPitchShift
        sfxAudioSource.pitch = UnityEngine.Random.Range(_minPitchShift, _maxPitchShift);

        //load and play the sound with name equal to nameOfSoundToPlay
        sfxAudioSource.PlayOneShot(availableSounds.Find(y => y.name == _nameOfSoundToPlay), sfxAudioSource.volume);
    }

    /// <summary>
    /// Play a sound from a List of sound names with the names defined by "_namesOfSoundsThatCanPlay"
    /// </summary>
    /// <param name="_namesOfSoundsThatCanPlay"> List of sounds to be randomly chosen from and played by the SFX Handler</param>
    /// <param name="_minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="_maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void PlayRandomSound(List<string> _namesOfSoundsThatCanPlay, float _minPitchShift = 0.8f, float _maxPitchShift = 1.2f)
    {
        //choose a random name of a sound from namesOfSoundsThatCanPlay
        string randomSoundName = _namesOfSoundsThatCanPlay[UnityEngine.Random.Range(0, _namesOfSoundsThatCanPlay.Count)];

        //play the sound with randomSoundName 
        PlaySound(randomSoundName, _minPitchShift, _maxPitchShift);
    }
}
