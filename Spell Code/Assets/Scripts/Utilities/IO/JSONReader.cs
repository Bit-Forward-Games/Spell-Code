using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FrameData
{
    public List<int> startFrames;
    public List<int> endFrames;
}

[System.Serializable]
public class HitboxData
{
    public int xOffset;
    public int yOffset;
    public int width;
    public int height;
    public int xKnockback;
    public int yKnockback;
    public ushort damage;
    public float damageProration = 0.95f; // this is how much this hitbox scales the damage of the next hit by in the combo, default proration is .9
    public int attackLvl;
    //we don't have to worry about serilizing hitbox data as we are doing a hitbox lookup based on logic frame anyway, so we can properties as bloated as this.
    //That being side design within reason

    //the optionals:
    public List<int> cancelOptions; //this is a states that the player can transition into to keep attacking 


    public HitboxData Clone() => (HitboxData)this.MemberwiseClone();
}

[System.Serializable]
public class HitboxGroup
{
    public List<HitboxData> hitbox1;
    public List<HitboxData> hitbox2;
    public List<HitboxData> hitbox3;
    public List<HitboxData> hitbox4;
}

[System.Serializable]
public class HurtboxData
{
    public int xOffset;
    public int yOffset;
    public int width;
    public int height;
}

[System.Serializable]
public class HurtboxGroup
{
    public List<HurtboxData> hurtbox1;
    public List<HurtboxData> hurtbox2;
    public List<HurtboxData> hurtbox3;
    public List<HurtboxData> hurtbox4;
}

[System.Serializable]
public class ImpulseData
{
    public int xImpulse;
    public int yImpulse;
    public bool resetXVel;
    public bool resetYVel;
}

[System.Serializable]
public class AnimFrames
{
    public List<int> frameIndexes;
    public List<int> frameLengths;
    public bool loopAnim;
}

[System.Serializable]
public class AnimFrameContainer
{
    public AnimFrames idleAnimFrames;
    public AnimFrames runAnimFrames;
    public AnimFrames jumpForwardAnimFrames;
    public AnimFrames jumpBackwardAnimFrames;
    public AnimFrames hitRisingAnimFrames;
    public AnimFrames hitFallingAnimFrames;
    public AnimFrames techAnimFrames;
    public AnimFrames codeWeaveAnimFrames;
    public AnimFrames codeReleaseAnimFrames;
    public AnimFrames slideAnimFrames;
}

//[System.Serializable]
//public class ImpulseFrames
//{
//    public List<int> _5LImpulseFrames;
//    public List<int> _5MImpulseFrames;
//    public List<int> _5HImpulseFrames;
//    public List<int> _2LImpulseFrames;
//    public List<int> _2MImpulseFrames;
//    public List<int> _2HImpulseFrames;
//    public List<int> JLImpulseFrames;
//    public List<int> JMImpulseFrames;
//    public List<int> JHImpulseFrames;
//}

//[System.Serializable]
//public class ImpulseDataContainer
//{
//    public List<ImpulseData> _5LImpulseData;
//    public List<ImpulseData> _5MImpulseData;
//    public List<ImpulseData> _5HImpulseData;
//    public List<ImpulseData> _2LImpulseData;
//    public List<ImpulseData> _2MImpulseData;
//    public List<ImpulseData> _2HImpulseData;
//    public List<ImpulseData> JLImpulseData;
//    public List<ImpulseData> JMImpulseData;
//    public List<ImpulseData> JHImpulseData;
//}

//[System.Serializable]
//public class HitboxFrames
//{
//    public FrameData _5LHitboxFrames;
//    public FrameData _5MHitboxFrames;
//    public FrameData _5HHitboxFrames;
//    public FrameData _2LHitboxFrames;
//    public FrameData _2MHitboxFrames;
//    public FrameData _2HHitboxFrames;
//    public FrameData JLHitboxFrames;
//    public FrameData JMHitboxFrames;
//    public FrameData JHHitboxFrames;
//}

//[System.Serializable]
//public class HitboxDataContainer
//{
//    public HitboxGroup _5LHitboxes;
//    public HitboxGroup _5MHitboxes;
//    public HitboxGroup _5HHitboxes;
//    public HitboxGroup _2LHitboxes;
//    public HitboxGroup _2MHitboxes;
//    public HitboxGroup _2HHitboxes;
//    public HitboxGroup JLHitboxes;
//    public HitboxGroup JMHitboxes;
//    public HitboxGroup JHHitboxes;
//}

[System.Serializable]
public class HurtboxDataContainer
{
    public HurtboxGroup idleHurtbox;
    public HurtboxGroup runHurtbox;
    public HurtboxGroup jumpHurtbox;
    public HurtboxGroup hitstunHurtbox;
    public HurtboxGroup techHurtbox;
    public HurtboxGroup codeWeaveHurtbox;
    public HurtboxGroup codeReleaseHurtbox;
    public HurtboxGroup slideHurtbox;
}

[System.Serializable]
public class HurtboxFrames
{
    public List<int> idleHurtboxFrames;
    public List<int> runHurtboxFrames;
    public List<int> jumpHurtboxFrames;
    public List<int> hitstunHurtboxFrames;
    public List<int> techHurtboxFrames;
    public List<int> codeWeaveHurtboxFrames;
    public List<int> codeReleaseHurtboxFrames;
    public List<int> slideHurtboxFrames;
}

//[System.Serializable]
//public class ProjectileSpawnFrameData
//{
//    public List<int> s1LSpawnFrames;
//    public List<int> s1MSpawnFrames;
//    public List<int> s1HSpawnFrames;
//    public List<int> s2LSpawnFrames;
//    public List<int> s2MSpawnFrames;
//    public List<int> s2HSpawnFrames;
//    public List<int> s3LSpawnFrames;
//    public List<int> s3MSpawnFrames;
//    public List<int> s3HSpawnFrames;
//    public List<int> js1LSpawnFrames;
//    public List<int> js1MSpawnFrames;
//    public List<int> js1HSpawnFrames;
//    public List<int> js2LSpawnFrames;
//    public List<int> js2MSpawnFrames;
//    public List<int> js2HSpawnFrames;
//    public List<int> js3LSpawnFrames;
//    public List<int> js3MSpawnFrames;
//    public List<int> js3HSpawnFrames;
//}

//[System.Serializable]
//public class ProjectileSpawnData
//{
//    public ushort projectileType;
//    public int xOffset;
//    public int yOffset;
//}

//[System.Serializable]
//public class ProjectileSpawnDataContainer
//{
//    public List<ProjectileSpawnData> s1LSpawnData;
//    public List<ProjectileSpawnData> s1MSpawnData;
//    public List<ProjectileSpawnData> s1HSpawnData;
//    public List<ProjectileSpawnData> s2LSpawnData;
//    public List<ProjectileSpawnData> s2MSpawnData;
//    public List<ProjectileSpawnData> s2HSpawnData;
//    public List<ProjectileSpawnData> s3LSpawnData;
//    public List<ProjectileSpawnData> s3MSpawnData;
//    public List<ProjectileSpawnData> s3HSpawnData;
//    public List<ProjectileSpawnData> js1LSpawnData;
//    public List<ProjectileSpawnData> js1MSpawnData;
//    public List<ProjectileSpawnData> js1HSpawnData;
//    public List<ProjectileSpawnData> js2LSpawnData;
//    public List<ProjectileSpawnData> js2MSpawnData;
//    public List<ProjectileSpawnData> js2HSpawnData;
//    public List<ProjectileSpawnData> js3LSpawnData;
//    public List<ProjectileSpawnData> js3MSpawnData;
//    public List<ProjectileSpawnData> js3HSpawnData;
//}



[System.Serializable]
public class SpecialInputs
{
    public List<short> special1Input;
    public List<short> special2Input;
    public List<short> special3Input;
}

[System.Serializable]
public class CharacterData
{
    public string character;
    public int runSpeed;
    public int jumpForce;
    public int jumpCounter;
    //public ushort[] projectileIds; //projectile Manager needs to be able to access this
    public ushort playerHealth;
    public int playerWidth;
    public int playerHeight;
    public AnimFrameContainer animFrames;
    //public ImpulseFrames impulseFrames;
    //public ImpulseDataContainer impulseData;
    //public HitboxFrames hitboxFrames;
    //public HitboxDataContainer hitboxData;
    public HurtboxFrames hurtboxFrames;
    public HurtboxDataContainer hurtboxData;
    //public ProjectileSpawnFrameData projectileSpawnFrames;
    //public ProjectileSpawnDataContainer projectileSpawnData;
    //public SpecialInputs specialInputs;
}

[System.Serializable]
public class CharacterDataList
{
    public List<CharacterData> CharacterData;
}

public class JSONReader : MonoBehaviour
{
    public TextAsset jsonFile;

    public CharacterDataList characterDataList = new CharacterDataList();

    private void Awake()
    {
        GetCharacterStats();
    }

    public void GetCharacterStats()
    {
        if (jsonFile != null)
        {
            characterDataList = JsonUtility.FromJson<CharacterDataList>(jsonFile.text);

            CharacterDataDictionary.SetupCharacterDataDictionary(characterDataList);
        }
        else
        {
            Debug.LogError("JSON file not assigned.");
        }
    }
}


public static class HboxUtils
{
    public static Vector2 GetWorldCenter(this HurtboxData hb, Transform owner, bool facingRight)
    {
        // 1) bottom-left local corner, flipped if needed
        float x0 = facingRight
            ? hb.xOffset
            : -(hb.xOffset + hb.width);

        float y0 = hb.yOffset;

        // 2) top-right local corner
        float x1 = x0 + hb.width;
        float y1 = y0 + hb.height;

        // 3) center in local space
        Vector3 localCenter = new Vector3(
            (x0 + x1) * 0.5f,
            (y0 + y1) * 0.5f,
            0f
        );

        // 4) transform into world space
        Vector3 worldPos = owner.TransformPoint(localCenter);
        return new Vector2(worldPos.x, worldPos.y);
    }
}