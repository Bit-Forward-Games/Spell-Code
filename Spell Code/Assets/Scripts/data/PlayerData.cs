using System;
using System.Collections.Generic;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

[Serializable]
public class PlayerData
{
    //Data class to represent our players & their performance
    public int codesFired;
    public int basicsFired;
    public int codesHit;

    public string synthesizer;
    public float accuracy;
    public Fixed avgTimeToCast;
    public bool matchWon;

    public List<Fixed> times;
    public string[] spellList;
}