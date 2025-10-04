using System;
using System.Collections.Generic;

[Serializable]
public class PlayerData
{
    //Data class to represent our players & their performance
    public int codesFired;
    public int basicsFired;
    public int codesHit;

    public string synthesizer;

    public List<string> times;
    public string[] spellList;
}