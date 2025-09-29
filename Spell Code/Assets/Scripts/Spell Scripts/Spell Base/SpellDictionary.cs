using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml.Linq;
using UnityEngine;



public class SpellDictionary : NonPersistantSingleton<SpellDictionary>
{
    public List<SpellData> spellList = new List<SpellData>();
    //public OrderedDictionary spellDict = new OrderedDictionary();
    public Dictionary<string, SpellData> spellDict = new Dictionary<string, SpellData>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (ushort i = 0; i < spellList.Count; i++)
        {
            if (!spellDict.ContainsKey(spellList[i].spellName))
            {
                spellDict.Add(spellList[i].spellName, spellList[i]);
            }
            else
            {
                Debug.LogWarning("SpellDictionary: Duplicate spell name found: " + spellList[i].spellName);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
