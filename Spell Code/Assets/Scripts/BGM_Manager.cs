using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BGM_Manager : MonoBehaviour
{
    [Header("BGM Audio Source")]
    [SerializeField] private AudioSource musicAudioSource;

    [Header("List of songs that have a chance to play in this scene")]
    [SerializeField] private List<AudioClip> availableSongs;

    //TESTING FUNCTION
    //private void Awake()
    //{
    //    SFX_Manager sFX_Manager = GameObject.Find("pfb_SFX_Manager").GetComponent<SFX_Manager>();
    //    sFX_Manager.PlaySound("mysound");
    //}

    void Start()
    {
        //allocate space for availableSongs
        availableSongs = new List<AudioClip>();

        //start a random song
        StartAndPlaySong();
    }

    /// <summary>
    /// Begin playing a random song from a list of available songs
    /// </summary>
    /// <param name="songToPlay"> Name of the song to be played. Set it to null to play a random song from the list of available songs</param>
    public void StartAndPlaySong(string nameOfSongToPlay = null)
    {
        //stop playing the current song
        StopSong();

        //sanity check to make sure that there are songs available to play
        if (availableSongs.Count <= 0)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Please specify at least 1 song to play in availableSongs of the BGM_Manager script");

            //return
            return;
        }

        //sanity check to make sure that the songToPlay exists within availableSongs
        if (nameOfSongToPlay != null && availableSongs.Find(x => x.name == nameOfSongToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Specified song of name = \"" + nameOfSongToPlay + "\" does not exist within availableSongs of the BGM_Manager script. Playing a random song");

            //set nameOfSongToPlay to null so that a random song is player
            nameOfSongToPlay = null;
        }

        //if a specific song is specified,...
        if (nameOfSongToPlay != null)
        {
            //assign the clip of the audio source to the song with the name specified by nameOfSongToPlay
            musicAudioSource.clip = availableSongs.Find(x => x.name == nameOfSongToPlay);
        }
        //else no song name is specified,...
        else
        {
            //choose a random song from availableSongs
            int choosenSongIndex = UnityEngine.Random.Range(0, availableSongs.Count);

            //assign the clip of the audio source to the randomly choosen song
            musicAudioSource.clip = availableSongs[choosenSongIndex];
        }

        //start playing the new song
        PlaySong();
    }

    /// <summary>
    /// Stop playing the current song (makes song stop playing and return to start of song)
    /// </summary>
    public void StopSong()
    {
        //if a song is NOT playing, then return
        if (!musicAudioSource.isPlaying)
            return;

        //stop playing the current song
        musicAudioSource.Stop();
    }

    /// <summary>
    /// Pause the current song
    /// </summary>
    public void PauseSong()
    {
        //if a song is NOT playing, then return
        if (!musicAudioSource.isPlaying)
            return;

        //pause the current song
        musicAudioSource.Pause();
    }

    /// <summary>
    /// Play the current song
    /// </summary>
    public void PlaySong()
    {
        //if there is NOT a clip to play, then return
        if (musicAudioSource.clip == null)
            return;

        //start playing current song
        musicAudioSource.Play();
    }
}
