//using System;
//using System.Collections.Generic;
//using UnityEngine;

////public class MusicManager : ScriptableObjectSingleton<MusicManager>
//[RequireComponent(typeof(AudioSource))]
//public class MusicManager : NonPersistantSingleton<MusicManager>
//{
//    public enum SongTypes 
//    {
//        NONE, MAIN_MENU, CHARACTER_SELECT, HOPPER_ALPHA, STAG_CHI, SIGMA_BEE, FIGHTING_THEME_BASED_ON_CHARACTERS
//    }

//    [Header("Song To Play At Start Of Scene")]
//    public SongTypes songToPlay = SongTypes.NONE;

//    [Header("Component Variable(s)")]
//    [SerializeField] private AudioSource musicAudioSource;

//    [Header("Audio Clips")]
//    [SerializeField] private AudioClip mainMenuSong;
//    [SerializeField] private AudioClip characterSelectSong;
//    [SerializeField] private AudioClip hopperAlphaSong;
//    [SerializeField] private AudioClip stagChiSong;
//    [SerializeField] private AudioClip sigmaBeeSong;
//    [SerializeField] private AudioClip trainingRoom;

//    void Start()
//    {
//        if(ConfigObject.IsReady) 
//              StartSong(songToPlay);
//        else
//        {
//            //assume main menu song:
//            musicAudioSource.clip = mainMenuSong; 
//            PlaySong();
//        }
//    }

//    //***** StartSong: Begin playing the desired song *****
//    public void StartSong(SongTypes song)
//    {
//        //stop playing the current song
//        StopSong();

//        //logic for handling the song that plays during gameplay and as long as both player variables have been assigned,...
//        // && (player1 != null && player2 != null)
//        if (songToPlay == SongTypes.FIGHTING_THEME_BASED_ON_CHARACTERS)
//        {
//            if (ConfigObject.Instance.playerOneCharacter == "hopperAlpha")
//            {
//                song = SongTypes.HOPPER_ALPHA;
//            }
//            else if (ConfigObject.Instance.playerOneCharacter == "stagChi")
//            {
//                song = SongTypes.STAG_CHI;
//            }
//            else if(ConfigObject.Instance.playerOneCharacter == "sigmaBee")
//            {
//                song = SongTypes.SIGMA_BEE;
//            }
//            else
//            {
//                if (ConfigObject.Instance.playerTwoCharacter == "hopperAlpha")
//                {
//                    song = SongTypes.HOPPER_ALPHA;
//                }
//                else if (ConfigObject.Instance.playerTwoCharacter == "stagChi")
//                {
//                    song = SongTypes.STAG_CHI;
//                }
//                else if (ConfigObject.Instance.playerTwoCharacter == "sigmaBee")
//                {
//                    song = SongTypes.SIGMA_BEE;
//                }
//                else
//                {
//                    //randomly choose between the 2 songs
//                    song = (UnityEngine.Random.Range(0, 2) == 0) ? SongTypes.HOPPER_ALPHA : SongTypes.STAG_CHI;
//                }
//            }

//            //if (ConfigObject.Instance.selectedSong == "Hopper Alpha Song")
//            //    songToPlay = SongTypes.HOPPER_ALPHA;
//            //else
//            //    songToPlay = SongTypes.STAG_CHI;
//        }

//        //set current song to songToStart
//        switch (song)
//        {
//            case SongTypes.NONE:
//                Debug.LogWarning("No Song Specified in MusicManager! Please add a song to MusicManager");
//                break;
//            case SongTypes.MAIN_MENU:
//                musicAudioSource.clip = mainMenuSong;
//                break;
//            case SongTypes.CHARACTER_SELECT:
//                musicAudioSource.clip = characterSelectSong;
//                break;
//            case SongTypes.HOPPER_ALPHA:
//                musicAudioSource.clip = hopperAlphaSong;
//                break;
//            case SongTypes.STAG_CHI:
//                musicAudioSource.clip = stagChiSong;
//                break;
//            case SongTypes.SIGMA_BEE:
//                musicAudioSource.clip = sigmaBeeSong;
//                break;
//        }

//        //start playing the new song
//        PlaySong();
//    }

//    //***** StopSong: Stop playing the current song (makes song stop playing and return to start of song) *****
//    public void StopSong()
//    {
//        //if a song is playing,...
//        if (musicAudioSource.isPlaying)
//        {
//            //stop playing the current song
//            musicAudioSource.Stop();
//        }
//    }

//    //***** PauseSong: Pause the current song *****
//    public void PauseSong()
//    {
//        //if a song is playing,...
//        if (musicAudioSource.isPlaying)
//        {
//            //pause the current song
//            musicAudioSource.Pause();
//        }
//    }

//    //***** PlaySong: Play the current song *****
//    public void PlaySong()
//    {
//        //start playing current song
//        musicAudioSource.Play();
//    }
//}
