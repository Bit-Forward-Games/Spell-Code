using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(AudioSource))]
public class BGM_Manager : MonoBehaviour
{
    /// <value>Property <c>Instance</c> is the single instance of the BGM_Manager.</value>
    public static BGM_Manager Instance { get; private set; }

    //AudioSource that will play songs
    private AudioSource musicAudioSource;

    [Header("Default song (leave as null to play a random song)")]
    [SerializeField] private AudioClip defaultSong = null;

    [Header("List of songs that have a chance to play in this scene")]
    [SerializeField] private List<AudioClip> availableSongs;

    //TESTBENCH FUNCTIONS
    //private SFX_Handler sFX_Manager;
    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.Alpha1))
    //    {
    //        sFX_Manager.PlaySound("v5_ParrySuccess"); //plays the parry success noise (sounds like "shink!")
    //    }
    //    else if (Input.GetKeyDown(KeyCode.Alpha2))
    //    {
    //        sFX_Manager.PlaySound(null); //logs a warning that reads: "Please specify a sound to play"
    //    }
    //    else if (Input.GetKeyDown(KeyCode.Alpha3))
    //    {
    //        sFX_Manager.PlaySound("unknown sound"); //logs a warning that says: "Specified sound of name = "unknown sound" does not exist within availableSounds of the SFX_Manager script. Please specify a song that exists with availableSounds"
    //    }
    //    else if (Input.GetKeyDown(KeyCode.Alpha4))
    //    {
    //        StartAndPlaySong(); //plays a random song
    //    }
    //    else if (Input.GetKeyDown(KeyCode.Alpha5))
    //    {
    //        StartAndPlaySong("vFunnie_StagChi_Henshin"); //plays the funny sounds (sounds like meme sounds)
    //    }
    //    else if (Input.GetKeyDown(KeyCode.Alpha6))
    //    {
    //        PauseSong(); //pauses the current song
    //    }
    //    else if (Input.GetKeyDown(KeyCode.Alpha7))
    //    {
    //        PlaySong(); //resumes the current song
    //    }
    //}

    private void Awake()
    {
        //MORE TESTBENCH STUFF
        //sFX_Manager = GameObject.Find("pfb_SFX_Handler").GetComponent<SFX_Handler>();

        //assign musicAudioSource
        musicAudioSource = gameObject.GetComponent<AudioSource>();

        //if there is an instance of BGM_Manager that is NOT this one,...
        if (Instance != null && Instance != this)
        {
            //delete myself
            Destroy(this);
        }
        //else there is only 1 instance of BGM_Manager,...
        else
        {
            //set instance to this instance of BGM_Manager
            Instance = this;
        }
    }

    void Start()
    {
        //start the default song
        StartAndPlaySong(defaultSong);
    }

    /// <summary>
    /// Begin playing a random song from a list of available songs
    /// </summary>
    public void StartAndPlaySong()
    {
        //call the string version of StartAndPlaySong() with a null string
        StartAndPlaySong((string)null);
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
    /// Begin playing a random song from a list of available songs
    /// </summary>
    /// <param name="audioClipToPlay"> AudioClip of the song to be played. Set it to null to play a random song from the list of available songs</param>
    public void StartAndPlaySong(AudioClip audioClipToPlay = null)
    {
        //if audioClipToPlay is null,...
        if (audioClipToPlay == null)
        {
            //call the string version of StartAndPlaySong() with a null string
            StartAndPlaySong((string)null);
        }
        //else audioClipToPlay exists,...
        else
        {
            //call the string version of StartAndPlaySong() with the audioClipToPlay's name
            StartAndPlaySong(audioClipToPlay.name);
        }
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
