using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml.Linq;
using UnityEngine;



public class SpellDictionary : NonPersistantSingleton<SpellDictionary>
{
    public List<SpellData> spellList = new List<SpellData>();
    //public OrderedDictionary spellDict = new OrderedDictionary();
    public Dictionary<string, SpellData> spellDict = new Dictionary<string, SpellData>();

    // Stable spell name -> id map so player serialization can
    // write a 4-byte int per spell instead of a length-prefixed string (br.ReadString allocated a
    // fresh string on every save-state/rollback). 'id' is the index into spellList -- the same
    // authored asset on every client -- so ids are deterministic across clients.
    private readonly Dictionary<string, int> spellNameToId = new Dictionary<string, int>();

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

        spellNameToId.Clear();
        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i] != null && !spellNameToId.ContainsKey(spellList[i].spellName))
            {
                spellNameToId[spellList[i].spellName] = i;
            }
        }

        Debug.Log("SpellDictionary: Loaded " + spellDict.Count + " spells into the dictionary.");
        for(int i = 0; i < spellDict.Count; i++)
        {
            Debug.Log("SpellDictionary: Spell " + i + ": " + spellDict[spellList[i].spellName].spellName);
        }
    }

    // Stable serialization id for a spell name. Returns -1 for null/empty/unknown -- the "no spell"
    // sentinel (e.g. an empty basicSpawnOverride). Allocation-free.
    public int GetSpellId(string spellName)
    {
        if (string.IsNullOrEmpty(spellName)) return -1;
        return spellNameToId.TryGetValue(spellName, out int id) ? id : -1;
    }

    // Reverse of GetSpellId. Returns the spell's existing name string (no new allocation), or "" for
    // -1 / out-of-range.
    public string GetSpellName(int id)
    {
        if (id < 0 || id >= spellList.Count || spellList[id] == null) return "";
        return spellList[id].spellName;
    }

    // Template lookup by id, used by RebuildSpellListFromSaved. Null for out-of-range.
    public SpellData GetSpellTemplate(int id)
    {
        if (id < 0 || id >= spellList.Count) return null;
        return spellList[id];
    }

    // Update is called once per frame
    void Update()
    {

    }
}
