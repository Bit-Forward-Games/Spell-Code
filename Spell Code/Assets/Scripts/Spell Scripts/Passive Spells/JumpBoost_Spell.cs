using UnityEngine;

public class JumpBoost_Spell : SpellData
{
    float baseJumpForce;
    public float jumpMultiplier = 1.5f;
    int jumpBoostDuration = 180; // Duration of the speed boost in frames
    int jumpBoostCounter = 0;
    Brands[] brands = new Brands[] { Brands.VWave, Brands.BigStox, Brands.RawrDX, Brands.SLUG, Brands.Killeez, Brands.Halk };
    int selectedBrandIndex = 1; // Index to select which brand to use

    public JumpBoost_Spell()
    {
        spellName = "JumpBoost";
        cooldown = 1.0f;
        spellType = SpellType.Passive;
    }

    public override void SpellUpdate()
    {
        Brands brandToCheck = brands[selectedBrandIndex];
        if (ProjectileManager.Instance.CheckProjectileHit(owner, brandToCheck))
        {
            Debug.Log("Jump Boost Activated!");
            if (owner.jumpForce == baseJumpForce)
            {
                owner.jumpForce *= jumpMultiplier;
                jumpBoostCounter = jumpBoostDuration; // Reset counter
            }
        }
        if (jumpBoostCounter > 0)
        {
            jumpBoostCounter--;
            if (jumpBoostCounter == 0)
            {
                owner.jumpForce = baseJumpForce; // Reset to base jump foce when counter ends
            }
        }
    }

    public override void LoadSpell()
    {
        baseJumpForce = owner.jumpForce; // Initialize base jump force
    }
}
