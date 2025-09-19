using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterDataDictionary
{
    private static readonly Dictionary<string, CharacterData> characterDataDictionary = new();

    public static CharacterDataList characterDatas;

    public static void SetupCharacterDataDictionary(CharacterDataList characterDataList)
    {
        characterDataDictionary.Clear();

        foreach (CharacterData characterData in characterDataList.CharacterData)
        {
            //Debug.Log(characterData.ToString());
            characterDataDictionary.Add(characterData.character, characterData);
        }

        characterDatas = characterDataList;

        //Debug.Log(characterDataDictionary.Count);
    }

    public static CharacterData GetCharacterData(string character)
    {
        return characterDataDictionary[character];
    }


    public static int GetTotalAnimationFrames(string character, PlayerState state)
    {
        return GetAnimFrames(character, state).frameLengths.Sum();
    }

    public static AnimFrames GetAnimFrames(string character, PlayerState state)
        => AnimFramesMap.TryGetValue(state, out var getter) ? getter(characterDataDictionary[character]) : null;

   /* public static (ImpulseData[], int[]) GetImpulseInfo(string character, PlayerState state)
    {
        if (characterDataDictionary.TryGetValue(character, out var data) &&
            ImpulseDataMap.TryGetValue(state, out var impulseDataFunc) &&
            ImpulseFramesMap.TryGetValue(state, out var impulseFramesFunc))
        {
            return (impulseDataFunc(data), impulseFramesFunc(data));
        }

        return (Array.Empty<ImpulseData>(), Array.Empty<int>());
    }

    public static ImpulseData[] GetImpulseData(string character, PlayerState state)
    {
        if (characterDataDictionary.TryGetValue(character, out var data) &&
            ImpulseDataMap.TryGetValue(state, out var impulseDataFunc))
        {
            return impulseDataFunc(data);
        }

        return Array.Empty<ImpulseData>();
    }


    public static (HitboxGroup, FrameData) GetHitboxInfo(string character, PlayerState state)
    {
        if (characterDataDictionary.TryGetValue(character, out var data) &&
            HitboxMap.TryGetValue(state, out var hitboxFunc))
        {
            return hitboxFunc(data);
        }
        return (null, null);
    }


    public static HitboxGroup GetHitboxData(string character, PlayerState state)
    {
        if (characterDataDictionary.TryGetValue(character, out var characterData) &&
            HitboxDataMap.TryGetValue(state, out var getHitboxData))
        {
            return getHitboxData(characterData);
        }
        return null;
    }

    public static (List<int> startFrames, List<int> endFrames) GetHitboxFrames(string character, PlayerState state)
    {

        //we need to grab special moves from the special data for each character to get hitbox start frames:
 
    


        if (characterDataDictionary.TryGetValue(character, out var characterData) &&
            HitboxMap.TryGetValue(state, out var hitboxFunc))
        {
            var (_, frameData) = hitboxFunc(characterData);
            return (frameData.startFrames, frameData.endFrames);
        }

        return (new List<int>(), new List<int>());

        
    }*/




    public static (HurtboxGroup, List<int>) GetHurtboxInfo(string character, PlayerState state)
    {
        if (characterDataDictionary.TryGetValue(character, out var characterData) &&
            HurtboxMap.TryGetValue(state, out var getHurtboxData))
        {
            return getHurtboxData(characterData);
        }
        return (null, null);
    }

    public static HurtboxGroup GetHurtboxData(string character, PlayerState state)
        => HurtboxDataMap.TryGetValue(state, out var getter) ? getter(characterDataDictionary[character]) : null;


    //helper to get the projectileIds based on character only
    //public static ushort[] GetProjectileIds(string character)
    //{
    //    return characterDataDictionary[character].projectileIds;
    //}

    //the reason to have these be a dictionary is to avoid having to write a switch statement for every single state which the compiler will spend time on making a jump table anyway increasing compile tim

    #region DataMaps

    //private static readonly Dictionary<PlayerState, Func<CharacterData, ImpulseData[]>> ImpulseDataMap =
    //    new()
    //    {
    //        { PlayerState.Light, cd => cd.impulseData._5LImpulseData.ToArray() },
    //        { PlayerState.Medium, cd => cd.impulseData._5MImpulseData.ToArray() },
    //        { PlayerState.Heavy, cd => cd.impulseData._5HImpulseData.ToArray() },
    //        { PlayerState.CrouchingLight, cd => cd.impulseData._2LImpulseData.ToArray() },
    //        { PlayerState.CrouchingMedium, cd => cd.impulseData._2MImpulseData.ToArray() },
    //        { PlayerState.CrouchingHeavy, cd => cd.impulseData._2HImpulseData.ToArray() },
    //        { PlayerState.JumpingLight, cd => cd.impulseData.JLImpulseData.ToArray() },
    //        { PlayerState.JumpingMedium, cd => cd.impulseData.JMImpulseData.ToArray() },
    //        { PlayerState.JumpingHeavy, cd => cd.impulseData.JHImpulseData.ToArray() },
    //        //parry exists in data json but not setup to be serialized
    //        //{ PlayerState.Parry, cd => cd.impulseData.parryImpulseData.ToArray() },
    //    };


    //private static readonly Dictionary<PlayerState, Func<CharacterData, int[]>> ImpulseFramesMap =
    //    new()
    //    {
    //        { PlayerState.Light, cd => cd.impulseFrames._5LImpulseFrames.ToArray() },
    //        { PlayerState.Medium, cd => cd.impulseFrames._5MImpulseFrames.ToArray() },
    //        { PlayerState.Heavy, cd => cd.impulseFrames._5HImpulseFrames.ToArray() },
    //        { PlayerState.CrouchingLight, cd => cd.impulseFrames._2LImpulseFrames.ToArray() },
    //        { PlayerState.CrouchingMedium, cd => cd.impulseFrames._2MImpulseFrames.ToArray() },
    //        { PlayerState.CrouchingHeavy, cd => cd.impulseFrames._2HImpulseFrames.ToArray() },
    //        { PlayerState.JumpingLight, cd => cd.impulseFrames.JLImpulseFrames.ToArray() },
    //        { PlayerState.JumpingMedium, cd => cd.impulseFrames.JMImpulseFrames.ToArray() },
    //        { PlayerState.JumpingHeavy, cd => cd.impulseFrames.JHImpulseFrames.ToArray() },
    //    };


    //private static readonly Dictionary<PlayerState, Func<CharacterData, (HitboxGroup, FrameData)>> HitboxMap =
    //    new()
    //    {
    //    { PlayerState.Light, cd => (cd.hitboxData._5LHitboxes, cd.hitboxFrames._5LHitboxFrames) },
    //    { PlayerState.Medium, cd => (cd.hitboxData._5MHitboxes, cd.hitboxFrames._5MHitboxFrames) },
    //    { PlayerState.Heavy, cd => (cd.hitboxData._5HHitboxes, cd.hitboxFrames._5HHitboxFrames) },
    //    { PlayerState.CrouchingLight, cd => (cd.hitboxData._2LHitboxes, cd.hitboxFrames._2LHitboxFrames) },
    //    { PlayerState.CrouchingMedium, cd => (cd.hitboxData._2MHitboxes, cd.hitboxFrames._2MHitboxFrames) },
    //    { PlayerState.CrouchingHeavy, cd => (cd.hitboxData._2HHitboxes, cd.hitboxFrames._2HHitboxFrames) },
    //    { PlayerState.JumpingLight, cd => (cd.hitboxData.JLHitboxes, cd.hitboxFrames.JLHitboxFrames) },
    //    { PlayerState.JumpingMedium, cd => (cd.hitboxData.JMHitboxes, cd.hitboxFrames.JMHitboxFrames) },
    //    { PlayerState.JumpingHeavy, cd => (cd.hitboxData.JHHitboxes, cd.hitboxFrames.JHHitboxFrames) },
    //    };


    //private static readonly Dictionary<PlayerState, Func<CharacterData, HitboxGroup>> HitboxDataMap =
    //    new()
    //    {
    //        { PlayerState.Light, cd => cd.hitboxData._5LHitboxes },
    //        { PlayerState.Medium, cd => cd.hitboxData._5MHitboxes },
    //        { PlayerState.Heavy, cd => cd.hitboxData._5HHitboxes },
    //        { PlayerState.CrouchingLight, cd => cd.hitboxData._2LHitboxes },
    //        { PlayerState.CrouchingMedium, cd => cd.hitboxData._2MHitboxes },
    //        { PlayerState.CrouchingHeavy, cd => cd.hitboxData._2HHitboxes },
    //        { PlayerState.JumpingLight, cd => cd.hitboxData.JLHitboxes },
    //        { PlayerState.JumpingMedium, cd => cd.hitboxData.JMHitboxes },
    //        { PlayerState.JumpingHeavy, cd => cd.hitboxData.JHHitboxes }
    //    };

    private static readonly Dictionary<PlayerState, Func<CharacterData, (HurtboxGroup, List<int>)>> HurtboxMap =
      new()
      {
        { PlayerState.CodeWeave, cd => (cd.hurtboxData.codeWeaveHurtbox, cd.hurtboxFrames.codeWeaveHurtboxFrames) },
        { PlayerState.CodeRelease, cd => (cd.hurtboxData.codeReleaseHurtbox, cd.hurtboxFrames.codeReleaseHurtboxFrames) },
        { PlayerState.Idle, cd => (cd.hurtboxData.idleHurtbox, cd.hurtboxFrames.idleHurtboxFrames) },
        { PlayerState.Run, cd => (cd.hurtboxData.runHurtbox, cd.hurtboxFrames.runHurtboxFrames) },
        { PlayerState.Jump, cd => (cd.hurtboxData.jumpHurtbox, cd.hurtboxFrames.jumpHurtboxFrames) },
        { PlayerState.Hitstun, cd => (cd.hurtboxData.hitstunHurtbox, cd.hurtboxFrames.hitstunHurtboxFrames) },
        { PlayerState.Tech, cd => (cd.hurtboxData.techHurtbox, cd.hurtboxFrames.techHurtboxFrames) },
        { PlayerState.Slide, cd => (cd.hurtboxData.slideHurtbox, cd.hurtboxFrames.slideHurtboxFrames) }
      };


    private static readonly Dictionary<PlayerState, Func<CharacterData, HurtboxGroup>> HurtboxDataMap = new()
    {
        { PlayerState.CodeWeave, data => data.hurtboxData.codeWeaveHurtbox },
        { PlayerState.CodeRelease, data => data.hurtboxData.codeReleaseHurtbox },
        { PlayerState.Idle, data => data.hurtboxData.idleHurtbox },
        { PlayerState.Run, data => data.hurtboxData.runHurtbox },
        { PlayerState.Jump, data => data.hurtboxData.jumpHurtbox },
        { PlayerState.Hitstun, data => data.hurtboxData.hitstunHurtbox },
        { PlayerState.Tech, data => data.hurtboxData.techHurtbox },
        { PlayerState.Slide, data => data.hurtboxData.slideHurtbox }

    };

    private static readonly Dictionary<PlayerState, Func<CharacterData, AnimFrames>> AnimFramesMap = new()
    {
        { PlayerState.CodeWeave, data => data.animFrames.codeWeaveAnimFrames },
        { PlayerState.CodeRelease, data => data.animFrames.codeReleaseAnimFrames },
        { PlayerState.Idle, data => data.animFrames.idleAnimFrames },
        { PlayerState.Run, data => data.animFrames.runAnimFrames },
        { PlayerState.Jump, data => data.animFrames.jumpAnimFrames },
        { PlayerState.Hitstun, data => data.animFrames.hitstunAnimFrames },
        { PlayerState.Tech, data => data.animFrames.techAnimFrames },
        { PlayerState.Slide, data => data.animFrames.slideAnimFrames }
    };
    #endregion
}
