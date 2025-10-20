using System;

//holds data per match (match is 1 round in a game)
[Serializable]
public class MatchData
{
    //hold all data we wont to consolidate
    public PlayerData[] playerData;
}
