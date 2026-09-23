// Only data and unrelated services touched by NpcAI. Tests drive the actual
// production Tick()/intent logic; these types do not implement a player sim.
using UnityEngine;

public readonly struct FixedValue
{
    private readonly float value;
    public FixedValue(float value) => this.value = value;
    public float ToFloat() => value;
    public static implicit operator FixedValue(float value) => new(value);
}
public struct FixedPosition
{
    public FixedValue X, Y;
    public FixedPosition(float x, float y) { X = x; Y = y; }
}
public enum ButtonState { None, Pressed, Held, Released }
public sealed class InputSnapshot
{
    public int Direction;
    public ButtonState[] ButtonStates;
    public InputSnapshot(int direction, ButtonState[] buttons) { Direction = direction; ButtonStates = buttons; }
}
public enum PlayerState { Idle, Run, Jump, Fall, Hitstun, Tech, Slide }
public enum BotDifficulty { Easy, Medium, Hard }
public sealed class PlayerController
{
    public FixedPosition position;
    public FixedValue vSpd, hSpd;
    public FixedValue playerWidth = 40, playerHeight = 48, jumpForce = 10, runSpeed = 4;
    public byte maxJumpCount = 2;
    public const float baseGravity = 0.45f;
    public bool isGrounded, onPlatform, facingRight, touchingLeftWall, touchingRightWall, collidingWithFloppy;
    public bool isAlive = true, isConnected = true;
    public PlayerState state = PlayerState.Idle;
    public int jumpCount = 2, pID;
    public List<SpellData> spellList = new();
    public BotDifficulty botDifficulty = BotDifficulty.Medium;
    public bool vibeCoding;
    public static int GetSpellInputLength(SpellData spell) => (int)(spell.spellInput & 15);
}
public sealed class GameManager
{
    public static GameManager Instance;
    public int playerCount;
    public PlayerController[] players = Array.Empty<PlayerController>();
    public SpellCode_Gate[] gates = Array.Empty<SpellCode_Gate>();
    public StageDataSO stage;
    public StageDataSO GetCurrentStageDataSO() => stage;
    public GambaMachine GetGambaForPID(int playerId) => null;
}
public sealed class SpellCode_Gate { public int ownerPID; }
public sealed class GambaMachine { }
public sealed class FloppyPickup : MonoBehaviour { public int ownerPID; public string diskName; }
public sealed class FloppyPickup_Character : MonoBehaviour { public int ownerPID; }
public enum SpellType { Active, Passive }
public enum SpellRole { Attack, Utility, Zone, Enhance }
public enum SpellRange { Short, Medium, Long }
public sealed class SpellData { public SpellType spellType; public int cooldownCounter; public uint spellInput; }
public sealed class SpellDictionary
{
    public static SpellDictionary Instance;
    public Dictionary<string, SpellData> spellDict = new();
}
public static class SpellTactics
{
    public struct Profile { public SpellRange Range; public SpellRole Role; public bool HitsAbove; }
    public static Profile For(SpellData spell) => default;
    public static float IdealDistance(SpellRange range) => 100f;
    public static float BandTolerance(SpellRange range) => 100f;
}

