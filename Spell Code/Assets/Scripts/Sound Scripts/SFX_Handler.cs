using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

//[Serializable] class SFXTuple { public string nameOfSoundToPlay; public AudioClip soundClip; }

[RequireComponent(typeof(AudioSource))]
public class SFX_Handler : MonoBehaviour
{
    [Header("SFX Audio Source")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("Sounds that SFX Manager can play")]
    //[SerializeField] private List<SFXTuple> sounds;
    [SerializeField] private List<AudioClip> availableSounds;

    /// <summary>
    /// Play a sound defined by "soundTypeToPlay"
    /// </summary>
    /// <param name="soundTypeToPlay"> Sound to be played by the SFX Handler</param>
    /// <param name="minPitchShift"> minimum pitch shift for SFX. By default, set to 0.8f</param>
    /// <param name="maxPitchShift"> maximum pitch shift for SFX. By default, set to 1.2f</param>
    public void PlaySound(string nameOfSoundToPlay, float minPitchShift = 0.8f, float maxPitchShift = 1.2f)
    {
        //sanity check to make sure that nameOfSoundToPlay is specified
        if (nameOfSoundToPlay == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Please specify a sound to play");

            //return
            return;
        }

        //sanity check to make sure that there is a sound with name equal to nameOfSoundToPlay that exists within availableSounds
        if (availableSounds.Find(x => x.name == nameOfSoundToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified sound of name = \"" + nameOfSoundToPlay + "\" does not exist within availableSounds of the SFX_Manager script. Please specify a song that exists with availableSounds");

            //return
            return;
        }

        //Randomize pitch between minPitchShift and maxPitchShift
        sfxAudioSource.pitch = UnityEngine.Random.Range(minPitchShift, maxPitchShift);

        //load and play the sound with name equal to nameOfSoundToPlay
        sfxAudioSource.PlayOneShot(availableSounds.Find(y => y.name == nameOfSoundToPlay), sfxAudioSource.volume);
    }
}
