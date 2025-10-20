using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class ProjectileManager : MonoBehaviour
{
    public static ProjectileManager Instance { get; private set; }
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    public List<BaseProjectile> projectilePrefabs = new List<BaseProjectile>();
    public List<BaseProjectile> activeProjectiles = new List<BaseProjectile>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    //public bool CheckBasicProjectileHit(PlayerController owner)
    //{
    //    for (int i = 0; i < activeProjectiles.Count; i++)
    //    {
    //        if ((activeProjectiles[i].owner == owner) && (activeProjectiles[i] is IBaseProjectile) && System.Array.Exists(activeProjectiles[i].playerIgnoreArr, ignore => ignore)) return true;
    //    }
    //    return false;
    //}

    //public bool CheckNinjaBuildBlastProjectileHit(PlayerController owner)
    //{
    //    for (int i = 0; i < activeProjectiles.Count; i++)
    //    {
    //        if ((activeProjectiles[i].owner == owner) && (activeProjectiles[i] is INinjaBuildBlastProjectile) && System.Array.Exists(activeProjectiles[i].playerIgnoreArr, ignore => ignore)) return true;
    //    }
    //    return false;
    //}

    public bool CheckProjectileHit(PlayerController owner, Brands brand)
    {
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            if (activeProjectiles[i].owner == owner &&
                activeProjectiles[i].brands != null &&
                System.Array.Exists(activeProjectiles[i].brands, b => b == brand) &&
                System.Array.Exists(activeProjectiles[i].playerIgnoreArr, ignore => ignore))
            {
                return true;
            }
        }
        return false;
    }

    public void UpdateProjectiles()
    {

        //activeProjectiles.Clear();
        for (int i = 0; i < projectilePrefabs.Count; i++)
        {
            if(projectilePrefabs[i] == null)
            {
                projectilePrefabs.RemoveAt(i);
                i--;
            }
            if (projectilePrefabs[i].gameObject.activeSelf)
            {
                if (!activeProjectiles.Contains(projectilePrefabs[i]))
                {
                    activeProjectiles.Add(projectilePrefabs[i]);
                }
                projectilePrefabs[i].ProjectileUpdate();
            }
            else
            {
                if (activeProjectiles.Contains(projectilePrefabs[i]))
                {
                    activeProjectiles.Remove(projectilePrefabs[i]);
                }
            }
            
        }
    }

    public void DeleteAllProjectiles()
    {
        activeProjectiles.Clear();
        //first destroy all projectiles in the list then clear the list
        for (int i = 0; i < projectilePrefabs.Count; i++)
        {
            if (projectilePrefabs[i] != null)
            {
                Destroy(projectilePrefabs[i].gameObject);
            }
        }
        projectilePrefabs.Clear();
    }

    public void InitializeAllProjectiles()
    {
        //first destroy all projectiles in the list then clear the list
        DeleteAllProjectiles();
        //loop through all players
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {


            GameObject spawnedBaseProjectile = Instantiate(ProjectileDictionary.Instance.projectileDict[GameManager.Instance.players[i].charData.basicAttackProjId].gameObject);
            spawnedBaseProjectile.GetComponent<BaseProjectile>().LoadProjectile();
            projectilePrefabs.Add(spawnedBaseProjectile.GetComponent<BaseProjectile>());
            spawnedBaseProjectile.GetComponent<BaseProjectile>().owner = GameManager.Instance.players[i];
            spawnedBaseProjectile.SetActive(false);

            //all spells in the player's inventory for their spells
            for (int j = 0; j < GameManager.Instance.players[i].spellList.Count; j++)
            {
                //if (GameManager.Instance.players[i].spellList[j] == null) break;

                //all projectiles in each spelldata
                for (int k = 0; k < GameManager.Instance.players[i].spellList[j].projectilePrefabs.Length; k++)
                {
                    GameObject spawnedProjectile = Instantiate(GameManager.Instance.players[i].spellList[j].projectilePrefabs[k]);
                    spawnedProjectile.GetComponent<BaseProjectile>().LoadProjectile();
                    projectilePrefabs.Add(spawnedProjectile.GetComponent<BaseProjectile>());
                    spawnedProjectile.GetComponent<BaseProjectile>().owner = GameManager.Instance.players[i];
                    spawnedProjectile.GetComponent<BaseProjectile>().ownerSpell = GameManager.Instance.players[i].spellList[j];
                    spawnedProjectile.SetActive(false);
                }
            }
        }
    }

    public void SpawnProjectile(string projectileName, PlayerController owner, bool facingRight, Vector2 spawnOffset)
    {

        List<BaseProjectile> matchingProjectiles = GetMatchingProjectiles(projectileName, owner);
        for (int i = 0; i < matchingProjectiles.Count; i++)
        {
            if (!matchingProjectiles[i].gameObject.activeSelf)
            {
                matchingProjectiles[i].gameObject.SetActive(true);
                matchingProjectiles[i].SpawnProjectile(facingRight, spawnOffset);
                return;
            }

        }
        // Respawn the longest living projectile if all are active
        if (matchingProjectiles.Count > 0)
        {
            BaseProjectile longestLiving = matchingProjectiles[0];
            int maxLogicFrame = matchingProjectiles[0].logicFrame;
            for (int i = 1; i < matchingProjectiles.Count; i++)
            {
                if (matchingProjectiles[i].logicFrame > maxLogicFrame)
                {
                    longestLiving = matchingProjectiles[i];
                    maxLogicFrame = matchingProjectiles[i].logicFrame;
                }
            }
            longestLiving.ResetValues();
            longestLiving.gameObject.SetActive(true);
            longestLiving.SpawnProjectile(facingRight, spawnOffset);
        }

    }

    public void DeleteProjectile(BaseProjectile targetProjectile)
    {
        targetProjectile.ResetValues();
        targetProjectile.gameObject.SetActive(false);
        //remove from activeProjectiles
        if (activeProjectiles.Contains(targetProjectile))
        {
            activeProjectiles.Remove(targetProjectile);
        }
    }

    public List<BaseProjectile> GetMatchingProjectiles(string projectileName, PlayerController owner)
    {
        List<BaseProjectile> matchingProjectiles = new List<BaseProjectile>();
        for (int i = 0; i < projectilePrefabs.Count; i++)
        {
            if (projectilePrefabs[i].projName == projectileName && projectilePrefabs[i].owner == owner)
            {
                matchingProjectiles.Add(projectilePrefabs[i]);
            }
        }
        return matchingProjectiles;
    }
}
