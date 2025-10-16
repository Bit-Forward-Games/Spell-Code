using UnityEngine;

public class SpeedBoost_Spell : SpellData
{
    float baseSpeed;
    public float speedMultiplier = 2;
    int speedBoostDuration = 180; // Duration of the speed boost in frames
    int speedBoostCounter = 0; 
    Brands[] brands = new Brands[] { Brands.VWave, Brands.BigStox, Brands.RawrDX, Brands.SLUG, Brands.Killeez, Brands.Halk };
    int selectedBrandIndex = 2; // Index to select which brand to use
    public SpeedBoost_Spell()
    {
        spellName = "SpeedBoost";
        cooldown = 1.0f;
        //spellInput = 0b_0000_0000_0000_0000_1001_0011_0000_0100; // Example input sequence
        spellType = SpellType.Passive;
        //projectilePrefabs = new GameObject[1];
    }

    public override void SpellUpdate()
    {
        Brands brandToCheck = brands[selectedBrandIndex];
        if (ProjectileManager.Instance.CheckProjectileHit(owner, brandToCheck))
        {
            Debug.Log("Speed Boost Activated!");
            if (owner.runSpeed == baseSpeed)
            {
                owner.runSpeed *= speedMultiplier;
                speedBoostCounter = speedBoostDuration; // Reset counter
            }
        }
        /*else
        {
            owner.runSpeed = baseSpeed; // Reset to base speed
        }*/
        if (speedBoostCounter > 0)
        {
            speedBoostCounter--;
            if (speedBoostCounter == 0)
            {
                owner.runSpeed = baseSpeed; // Reset to base speed when counter ends
            }
        }
    }

    public override void LoadSpell()
    {
        baseSpeed = owner.runSpeed; // Initialize base speed
    }
}
