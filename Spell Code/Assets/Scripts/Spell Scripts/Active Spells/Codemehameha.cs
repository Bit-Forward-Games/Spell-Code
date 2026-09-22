using UnityEngine;
using BestoNet.Types;

using System.Collections.Generic;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class Codemehameha : SpellData
{
    public byte chargeLevel = 0;
    public Codemehameha()
    {
        spellName = "Codemehameha";
        brands = new Brand[]{ Brand.DarkWeb, Brand.DemonX, Brand.BigStox, Brand.Killeez, Brand.VWave };
        cooldown = 360;
        spellInput = 0b_0000_0000_1000_0111_1000_0111_0000_1000; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast, ProcCondition.OnCastBasic, ProcCondition.ActiveOnHit};
        projectilePrefabs = new GameObject[4];
        //codeReleaseFrameLengthsOverride = new List<int>(){1, 1, 1, 1, 1, 1};
        description = "The ultimate technique. Enhance your next basic attack into a beam attack, increasing in strength every additional time this spell is used before releasing.";

        
    }
    public override void LoadSpell()
    {
        base.LoadSpell();
        chargeLevel = 0;
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
            activateFlag = false;
            // owner.basicSpawnOverride = spellName;
            // basicEnhanceActive = true;
            SetBasicEnhancement(spellName + chargeLevel);
            cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
        }
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                chargeLevel = (byte)Mathf.Min(chargeLevel + 1, 3);
                switch (chargeLevel)
                {
                    case 0://anvil
                        owner.SpawnToast("Level 1", GameManager.colors["white"]);

                        //play the anvil display sound
                        //SFX_Manager.Instance.PlaySpellcodeSound("Armory Of Hephaestus Anvil Display");
                        
                        break;
                    case 1://spear
                        owner.SpawnToast("Level 2", GameManager.colors["white"]);

                        //play the anvil display sound
                        //SFX_Manager.Instance.PlaySpellcodeSound("Armory Of Hephaestus Spear Display");

                        break;
                    case 2://hammer
                        owner.SpawnToast("Level 3", GameManager.colors["white"]);

                        //play the anvil display sound
                        //SFX_Manager.Instance.PlaySpellcodeSound("Armory Of Hephaestus Hammer Display");

                        break;
                        
                }
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                break;
            case ProcCondition.OnCastBasic:
            
            if (owner.basicSpawnOverride == spellName && basicEnhanceActive)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[chargeLevel+1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    chargeLevel = 0;
                    basicEnhanceActive = false;
                }
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(chargeLevel);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        chargeLevel = br.ReadByte();
    }
}
