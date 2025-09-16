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

    void Start()
    {
        //allocate space for availableSongs
        availableSongs = new List<AudioClip>();

        //start a random song
        StartSong(null);
    }

    //***** StartSong: Begin playing a random song from availableSongs *****
    /// <summary>
    /// Begin playing a random song from a list of available songs
    /// </summary>
    /// <param name="songToPlay"> Song to be played by the BGM Handler. Set it to null to .... CONTINUE HERE</param>
    public void StartSong(AudioClip songToPlay)
    {
        //stop playing the current song
        StopSong();

        //sanity check to see if there are songs available to play
        if (availableSongs.Count <= 0)
        {
            //log a warning
            Debug.LogWarning(gameObject.name + ": Please specify at least 1 song to play in availableSongs of the BGM_Manager script");

            //return
            return;
        }

        //if a specific song is specified
        if()
        {

        }

        //choose a random song from availableSongs
        int choosenSongIndex = UnityEngine.Random.Range(0, availableSongs.Count);

        //assign the resource of the audio source to the randomly choosen song
        musicAudioSource.resource = availableSongs[choosenSongIndex];

        //start playing the new song
        PlaySong();
    }

    //***** StopSong: Stop playing the current song (makes song stop playing and return to start of song) *****
    public void StopSong()
    {
        //if a song is NOT playing,...
        if (!musicAudioSource.isPlaying)
            return;

        //stop playing the current song
        musicAudioSource.Stop();
    }

    //***** PauseSong: Pause the current song *****
    public void PauseSong()
    {
        //if a song is NOT playing,...
        if (!musicAudioSource.isPlaying)
            return;

        //pause the current song
        musicAudioSource.Pause();
    }

    //***** PlaySong: Play the current song *****
    public void PlaySong()
    {
        if (musicAudioSource.resource == null)
            return;

        //start playing current song
        musicAudioSource.Play();
    }
}
