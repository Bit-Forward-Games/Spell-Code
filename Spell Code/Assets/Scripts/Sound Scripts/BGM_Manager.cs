using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class BGM_Manager : MonoBehaviour
{
    /// <value>Property <c>Instance</c> is the single instance of the BGM_Manager.</value>
    public static BGM_Manager Instance { get; private set; }

    //AudioSource that will play songs
    private AudioSource musicAudioSource;

    //[Header("Default song (leave as null to play a random song)")]
    //[SerializeField] private AudioClip defaultSong = null;

    [Serializable]
    private class SceneAudioObject
    {
        public string sceneName; //name of the scene
        public List<AudioClip> availableSongs; //list of songs that can play in the scene with name = sceneName
        public AudioClip previousSong; //the last played song within the scene with name = sceneName
    }

    //[Header("List of songs that have a chance to play in this scene")]
    //[SerializeField] private List<AudioClip> availableSongs;

    //[Header("List of songs that can play in lobby phase")]
    //[SerializeField] private Scene lobbyScene;
    //[SerializeField] private List<AudioClip> lobbySongs;

    //[Header("List of songs that can play during a match")]
    //[SerializeField] private Scene gameplayScene;
    //[SerializeField] private List<AudioClip> gameplaySongs;

    //[Header("List of songs that can play in lobby phase")]
    //[SerializeField] private Scene shopScene;
    //[SerializeField] private List<AudioClip> shopSongs;

    [Header("List of songs that have a chance to play in each listed scene")]
    [SerializeField] private List<SceneAudioObject> sceneAudioObjects;

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

        ////
        //DontDestroyOnLoad(this.gameObject);

        //
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //sanity check for musicAudioSource
        if (musicAudioSource == null)
            return;

        ////
        //DontDestroyOnLoad(this.gameObject);

        //assign musicAudioSource
        musicAudioSource = gameObject.GetComponent<AudioSource>();

        //play a song appropriate for the current scene
        StartAndPlaySong();
    }

    //void Start()
    //{
    //    //start the default song
    //    StartAndPlaySong(defaultSong);
    //}

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

        //traverse through sceneAudioObjects until the correct scene is found,...
        int sceneAudioIndex;
        for(sceneAudioIndex = 0; sceneAudioIndex < sceneAudioObjects.Count; sceneAudioIndex++)
        {
            //if the active scene is found in sceneAudioObjects,...
            if (sceneAudioObjects[sceneAudioIndex].sceneName == SceneManager.GetActiveScene().name)
            {
                //break out of the enclosing for loop since we have found the correct
                break;
            }
        }
        
        ////sanity check
        //if()
        //{

        //}

        //
        //switch (SceneManager.GetActiveScene())
        //{
        //    case var _value when _value == lobbyScene:
        //        break;
        //    case var _value when _value == gameplayScene:


        //sanity check to make sure that there are songs available to play
        if (sceneAudioObjects[sceneAudioIndex].availableSongs.Count <= 0)
        {
            //log a warning
            Debug.LogWarning(BGM_Manager.Instance.name + ": Please specify at least 1 song to play in availableSongs of the BGM_Manager script");

            //return
            return;
        }

        //sanity check to make sure that the songToPlay exists within availableSongs
        if (nameOfSongToPlay != null && sceneAudioObjects[sceneAudioIndex].availableSongs.Find(x => x.name == nameOfSongToPlay) == null)
        {
            //log a warning
            Debug.LogWarning(BGM_Manager.Instance.name + ": Specified song of name = \"" + nameOfSongToPlay + "\" does not exist within availableSongs of the BGM_Manager script. Playing a random song");

            //set nameOfSongToPlay to null so that a random song is player
            nameOfSongToPlay = null;
        }

        //if a specific song is specified,...
        if (nameOfSongToPlay != null)
        {
            //assign the clip of the audio source to the song with the name specified by nameOfSongToPlay
            musicAudioSource.clip = sceneAudioObjects[sceneAudioIndex].availableSongs.Find(x => x.name == nameOfSongToPlay);
        }
        //else no song name is specified,...
        else
        {
            //create a list of songs to play that does not include the previously played song in the scene
            List<AudioClip> nonRepeatedSongs = new List<AudioClip>(sceneAudioObjects[sceneAudioIndex].availableSongs);
            if(nonRepeatedSongs.Contains(sceneAudioObjects[sceneAudioIndex].previousSong))
            {
                nonRepeatedSongs.Remove(sceneAudioObjects[sceneAudioIndex].previousSong);
            }

            //choose a random song from availableSongs
            int choosenSongIndex = UnityEngine.Random.Range(0, nonRepeatedSongs.Count);

            //assign the clip of the audio source to the randomly choosen song
            musicAudioSource.clip = nonRepeatedSongs[choosenSongIndex];
        }
        //        break;
        //    case var _value when _value == shopScene:
        //        break;
        //    default:
        //        break;
        //}

        //assign previousSong of current scene to the song that is currently playing
        sceneAudioObjects[sceneAudioIndex].previousSong = musicAudioSource.clip;

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
        //sanity check for musicAudioSource
        if (musicAudioSource == null)
            return;

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
