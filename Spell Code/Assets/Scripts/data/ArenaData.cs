using System;
using System.Collections.Generic;
using UnityEngine;

//holds data per match (match is 1 round in a game)
[Serializable]
public class ArenaData
{
    //hold all data we want to consolidate
    public Dictionary<String, List<Vector2>> deathDict = new Dictionary<string, List<Vector2>>();
    public Dictionary<String, List<Vector2>> hitDict = new Dictionary<string, List<Vector2>>();

}

