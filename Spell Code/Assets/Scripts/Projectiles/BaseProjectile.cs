using UnityEngine;

public abstract class BaseProjectile : MonoBehaviour
{

    public HitboxData[] hitboxDatas;
    public Sprite[] sprites;
    public byte currentHitboxIndex = 0;
    public float hSpeed;
    public float vSpeed;
    public Vector2 position;
    public bool facingRight;
    public int logicFrame;
    public ushort animationFrame; //which frame of animation the projectile is on
    public ushort lifeSpan; //in logic frames
    public PlayerController owner;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public virtual void SpawnProjectile(PlayerController owner, bool facingRight, Vector2 spawnOffset, float hSpeed, float vSpeed, HitboxData[] hitboxDatas)
    {
        this.owner = owner;
        this.facingRight = facingRight;
        this.position = owner.position + (spawnOffset * (owner.facingRight?1:-1));
        this.hSpeed = hSpeed;
        this.vSpeed = vSpeed;
        //this.hitboxDatas = hitboxDatas;
        this.currentHitboxIndex = 0;
        this.logicFrame = 0;
    }

    public virtual void LoadProjectile()
    {

        this.currentHitboxIndex = 0;
        this.logicFrame = 0;
    }
    public virtual void ProjectileUpdate()
    {


    }
}
