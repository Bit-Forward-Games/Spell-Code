//using Ardalis.SmartEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class AnimationManager : NonPersistantSingleton<AnimationManager>
{
    public SpriteSheetData[] spriteSheetData;
    public Texture2D[] paletteTextures;
    [NonSerialized] public Dictionary<PlayerController, Dictionary<PlayerState, Sprite[]>> PlayerAnimations;
    //[NonSerialized] public Dictionary<PlayerController, Dictionary<Type, Dictionary<SmartEnum<ProjectileState>, Sprite[]>>> characterSpecificProjectileAnimations;
    [SerializeField] public  PlayerController[] fighters;


#if UNITY_EDITOR
    private void OnValidate()
    {
        //if (fighters.Length > 2)
        //{
        //    Debug.LogWarning("The 'fighters' array cannot have more than 2 elements. Trimming excess elements.");
        //    System.Array.Resize(ref fighters, 2);
        //}
    }
#endif

    private struct implicitStateAnimations
    {
        public Sprite[] landingHitstunAnim;
        public Sprite[] RisingHitstunAnim;
    }

    [NonSerialized]
    private Dictionary<PlayerController, implicitStateAnimations> implicitAnimationSprites = new();

    /// <summary>
    /// Todo: make josh or someone take note of this design decision
    /// Tldr: because game scene needs to have characters in it we have to run this method on start
    /// howeover we need to ALWAYS REPREAT calling this method because scene transition is from menu to css to game meaning we have to override the test scene characters and palettes
    /// </summary>
    /// <param name="player"></param>
    /// <param name="index"></param>
    public void InitializePlayerVisuals(PlayerController player, int index)
    {
        string characterName = spriteSheetData[index].name;
        Sprite[] spriteSheet = spriteSheetData[index].subSprites;
        //Sprite[] projectileSpriteSheet = spriteSheetData[index].projSubSprites;


        AnimFrames idleAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Idle);
        AnimFrames runAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Run);
        AnimFrames jumpAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Jump);
        AnimFrames techAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Tech);
        AnimFrames codeWeaveAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.CodeWeave);
        AnimFrames codeReleaseAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.CodeRelease);
        AnimFrames slideAnimFrames = CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Slide);



        // Map animations
        Sprite[] idleAnim = idleAnimFrames.frameIndexes.Select(i => spriteSheet[i]).ToArray();
        Sprite[] runAnim = runAnimFrames.frameIndexes.Select(i => spriteSheet[i]).ToArray();
        Sprite[] jumpAnim = jumpAnimFrames.frameIndexes.Select(i => spriteSheet[i]).ToArray();
        Sprite[] codeWeaveAnim = codeWeaveAnimFrames.frameIndexes.Select(i => spriteSheet[i]).ToArray();
        Sprite[] codeReleaseAnim = codeReleaseAnimFrames.frameIndexes.Select(i => spriteSheet[i]).ToArray();
        Sprite[] techAnim = techAnimFrames.frameIndexes.Select(i => spriteSheet[i]).ToArray();

        // implicit ANIMS **separately per player** 
        implicitAnimationSprites[player] = new implicitStateAnimations
        {
            landingHitstunAnim = spriteSheetData[index].subSprites[95..97],
            RisingHitstunAnim = spriteSheetData[index].subSprites[97..99]
        };

        // Initialize player's animation dictionary with default empty sprite arrays for each state
        if (!PlayerAnimations.ContainsKey(player))
        {
            PlayerAnimations[player] = new Dictionary<PlayerState, Sprite[]>();
        }

        // Assign movement ANIMS:
        PlayerAnimations[player][PlayerState.Idle] = idleAnim;
        PlayerAnimations[player][PlayerState.Run] = runAnim;
        PlayerAnimations[player][PlayerState.Jump] = jumpAnim;


        PlayerAnimations[player][PlayerState.Tech] = techAnim;


        //Attack ANIMS:
        PlayerAnimations[player][PlayerState.CodeWeave] = codeWeaveAnim;
        PlayerAnimations[player][PlayerState.CodeRelease] = codeReleaseAnim;

        // Initialize player palette
        //player.InitializePalette(paletteTextures[index]);
        
    }

    public List<int> GetFrameLengthsForCurrentState(PlayerController player)
    {
        int index = Array.IndexOf(fighters, player);
        List<int> frameLengthsData = CharacterDataDictionary.GetAnimFrames(spriteSheetData[index].name, player.state)
                .frameLengths;

        return frameLengthsData;
    }


    public void SetAirHitstun(PlayerController player)
    {
        if (!implicitAnimationSprites.TryGetValue(player, out implicitStateAnimations hitstunAnims))
        {
            Debug.LogError($"Hitstun animations not found for player {player.name}");
            return;
        }

        // Determine animation based on vertical speed (vSpd)
        bool rising = player.vSpd > 0;

        PlayerAnimations[player][PlayerState.Hitstun] = rising ? hitstunAnims.RisingHitstunAnim
            : hitstunAnims.landingHitstunAnim;
    }

    

    

    private void Start()
    {
        PlayerAnimations = new Dictionary<PlayerController, Dictionary<PlayerState, Sprite[]>>();
    }

    /// 🔹 **Used in Local Play** Either one only does what the old UpdateSprites did, if anything rename later.
    public void RenderGameState()
    {
        if(GameManager.Instance.playerCount == 0)
        {
            return;
        }
        fighters = GameManager.Instance.players[0..GameManager.Instance.playerCount];
        // First pass: count how many players are attacking
        int attackingCount = fighters.Count(player => HitboxManager.Instance.IsAttackingState(player.state));

        // Second pass: update sprites and z-indices
        for (int i = 0; i < fighters.Length; i++)
        {
            PlayerController player = fighters[i];

            //if (!PlayerAnimations.TryGetValue(player, out Dictionary<PlayerState, Sprite[]> playerAnimations)) continue;
            //if (!playerAnimations.TryGetValue(player.state, out Sprite[] currentAnimation)) continue;

            // Update the player's sprite and flip direction
            //player.spriteRenderer.sprite = currentAnimation[player.animationFrame];
            player.spriteRenderer.flipX = !player.facingRight;

            // Determine z-index
            float zIndex;
            bool isAttacking = HitboxManager.Instance.IsAttackingState(player.state);

            if (isAttacking)
            {
                if (attackingCount == 2)
                {
                    // When both players are attacking: first in fighters list gets priority
                    zIndex = (i == 0) ? 0 : -1;
                }
                else
                {
                    // Single attacker uses default attacking z-index
                    zIndex = -1;
                }
            }
            else
            {
                // Non-attacking players use original priority logic
                zIndex = Array.IndexOf(GameManager.Instance.players, player) + 2;
            }

            // Update position with new z-index
            player.transform.position = new Vector3(player.position.x, player.position.y, zIndex);
        }

    }


    /// 🔹 **Used in GGPO Rollback**
    //public void RenderGameStateNetwork()
    //{
    //    foreach (PlayerController player in fighters)
    //    {
    //        if (player == null) continue;
    //        if (!PlayerAnimations.TryGetValue(player, out Dictionary<PlayerState, Sprite[]> playerAnimations)) continue;
    //        if (!playerAnimations.TryGetValue(player.state, out Sprite[] currentAnimation)) continue;
    //        player.spriteRenderer.sprite = currentAnimation[player.animationFrame];
    //        player.spriteRenderer.flipX = !player.facingRight;
    //        player.transform.position = transform.position = new Vector3(player.position.x, player.position.y, (Array.IndexOf(GameSessionManager.Instance.playerControllers, player) + 2));
    //    }
    //}
}
