using UnityEngine;
using System.IO;


using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpellCode_Gate : MonoBehaviour
{
    public bool isOpen = false;
    public Animator gateAnimator;
    public int ownerPID;
    Bounds gateBounds;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gateBounds = GetComponent<Collider>().bounds;
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        {
            SetOpen(isOpen);
            return;
        }

        if (!isOpen)
        {
            CheckGateBroken();
        }
        SetOpen(isOpen);
    }

    public void SimulateOnline()
    {
        if (!isOpen)
        {
            CheckGateBroken();
        }

        SetOpen(isOpen);
    }

    public void CheckGateBroken()
    {
        if (HasGateBounds() && !isOpen)
        {
            GateCollision();
        }
    }


    public void GateCollision()
    {
        if (HasGateBounds())
        {
            HurtboxData gateHurtbox = GetGateHurtbox();
            FixedVec2 gateHurtboxPos = FixedVec2.FromFloat(gateBounds.min.x, gateBounds.min.y);

            foreach (BaseProjectile projectile in ProjectileManager.Instance.activeProjectiles)
            {
                if(projectile.ownerSpell == null) break;
                if (projectile.owner.pID != ownerPID) continue;

                if (HitboxManager.Instance.ProcessSingleProjectileCollisison(projectile, gateHurtbox, gateHurtboxPos, true))
                {
                    isOpen = true;

                    //Play the glass break visual effect at the gate position
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.GLASS_BREAK, FixedVec2.FromFloat(gameObject.transform.position.x, gameObject.transform.position.y), ownerPID, projectile.facingRight);

                    return;
                }
            }
        }
    }

    // Helper to set state and update visuals
    public void SetOpen(bool open)
    {
        if (gateAnimator != null)
        {
            gateAnimator.SetBool("live", !isOpen);
        }
    }

    private HurtboxData GetGateHurtbox()
    {
        return new HurtboxData
        {
            xOffset = 0,
            yOffset = Mathf.RoundToInt(gateBounds.size.y),
            width = Mathf.RoundToInt(gateBounds.size.x),
            height = Mathf.RoundToInt(gateBounds.size.y)
        };
    }

    private bool HasGateBounds()
    {
        return gateBounds.size.x > 0 && gateBounds.size.y > 0;
    }

    public void Serialize(BinaryWriter bw)
    {
        bw.Write(isOpen);
    }

    public void Deserialize(BinaryReader br)
    {
        isOpen = br.ReadBoolean();
    }
}
