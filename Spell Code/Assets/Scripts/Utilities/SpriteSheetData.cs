using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetData", menuName = "ScriptableObjects/SpriteSheetData", order = 1)]
public class SpriteSheetData : ScriptableObject //could potentally rename to FighterData
{
    public Sprite[] subSprites; // Array to store all sub-sprites
    public Sprite[] projSubSprites;
    public Sprite Portrait;
}