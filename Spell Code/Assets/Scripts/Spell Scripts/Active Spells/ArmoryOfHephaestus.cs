using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class ArmoryOfHephaestus : SpellData
{
    public byte weaponIndex = 0;
    public ArmoryOfHephaestus()
    {
        spellName = "Armory Of Hephaestus";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0011_0011_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast, ProcCondition.OnCastBasic, ProcCondition.ActiveOnHit};
        projectilePrefabs = new GameObject[3];

        description = "Enhance basic attack to be 1 of 3 weapons, cycling between an Anvil, a Spear, and a Hammer.";

        
    }
    public override void LoadSpell()
    {
        base.LoadSpell();
        weaponIndex = 0;
    }
    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;



        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {
            owner.basicSpawnOverride = spellName;
            // Reset the activate flag
            activateFlag = false;
            

            
            cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
            //if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
            //vibeCasted = false;
        }


    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                switch (weaponIndex)
                {
                    case 0://anvil
                        owner.SpawnToast("ANVIL", GameManager.colors["yellow"]);
                        break;
                    case 1://spear
                        owner.SpawnToast("SPEAR", GameManager.colors["yellow"]);
                        break;
                    case 2://hammer
                        owner.SpawnToast("HAMMER", GameManager.colors["yellow"]);
                        break;
                }
                break;
            case ProcCondition.OnCastBasic:
            
            if (owner.basicSpawnOverride == spellName)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[weaponIndex].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    weaponIndex = (byte)((weaponIndex + 1) % 3);
                }
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(weaponIndex);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        weaponIndex = br.ReadByte();
    }
}
