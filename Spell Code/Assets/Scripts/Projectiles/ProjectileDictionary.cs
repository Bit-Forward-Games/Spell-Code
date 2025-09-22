using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml.Linq;
using UnityEngine;


public class ProjectileDictionary : NonPersistantSingleton<ProjectileDictionary>
{
    public List<BaseProjectile> projectileList = new List<BaseProjectile>();
    //public OrderedDictionary projectileDict = new OrderedDictionary();
    public Dictionary<string,BaseProjectile> projectileDict = new Dictionary<string, BaseProjectile>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        for (ushort i = 0; i < projectileList.Count; i++)
        {
            //make sure each projectile has a BaseProjectile type component
            //if (projectileList[i].GetComponent<BaseProjectile>() == null)
            //{
            //    Debug.LogError("ProjectileDictionary: Projectile " + projectileList[i].name + " does not have a BaseProjectile type component.");
            //    return;
            //}


            //add the projectile to the dictionary
            if (!projectileDict.ContainsKey(projectileList[i].projName))
            {
                projectileDict.Add(projectileList[i].projName, projectileList[i]);
            }
            else
            {
                Debug.LogWarning("ProjectileDictionary: Duplicate projectile name found: " + projectileList[i].projName);
            }
        }
    }

}
