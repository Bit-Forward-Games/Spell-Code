using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View half of the training room options panel. It holds no option state of its own,
/// TrainingOptionsMachine owns the values and pushes them in. Lives on the "Training Options"
/// prefab root, which sits inactive under a canvas until a machine opens it.
/// </summary>
public class TrainingOptionsUI : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        public string rowName;
        public GameObject root;
        public TMP_Text label;
        public TMP_Text valueText;
        public Toggle toggle;
        public Image leftArrow;
        public Image rightArrow;
    }

    // Row order must match TrainingOptionsMachine.Option. These are the GameObject names inside the
    // prefab, the auto wiring below resolves each row from them.
    public static readonly string[] RowNames =
    {
        "Cooldowns",
        "Flow State",
        "Demon Aura",
        "Reps",
        "Stock Stability",
        "AI Behavior"
    };

    [Tooltip("Object the option rows live under. Leave empty to search the whole panel.")]
    public Transform rowsParent;
    public Row[] rows = new Row[RowNames.Length];

    [Header("Highlight Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color editingColor = new Color(0.4f, 1f, 0.55f, 1f);

    [Header("Arrows")]
    [Range(0f, 1f)] public float idleArrowAlpha = 0.25f;
    [Range(0f, 1f)] public float selectedArrowAlpha = 1f;

    public int RowCount => rows != null ? rows.Length : 0;

    void Awake()
    {
        EnsureRows();
    }

    void Reset()
    {
        EnsureRows();
    }

    /// <summary>
    /// Fills in any row reference that hasn't been wired by hand, by walking the prefab and matching
    /// the names the panel was authored with. Anything already assigned in the inspector wins.
    /// </summary>
    [ContextMenu("Auto Wire Rows")]
    public void EnsureRows()
    {
        if (rows == null || rows.Length != RowNames.Length)
        {
            Array.Resize(ref rows, RowNames.Length);
        }

        Transform searchRoot = rowsParent != null ? rowsParent : transform;

        for (int i = 0; i < RowNames.Length; i++)
        {
            if (rows[i] == null)
            {
                rows[i] = new Row();
            }

            Row row = rows[i];
            row.rowName = RowNames[i];

            if (row.root == null)
            {
                Transform found = FindChildByName(searchRoot, RowNames[i]);
                if (found != null)
                {
                    row.root = found.gameObject;
                }
            }

            if (row.root == null)
            {
                continue;
            }

            Transform rowTransform = row.root.transform;

            if (row.label == null)
            {
                Transform labelTransform = rowTransform.Find(RowNames[i] + " Text");
                if (labelTransform != null)
                {
                    row.label = labelTransform.GetComponent<TMP_Text>();
                }
            }

            if (row.valueText == null)
            {
                Transform valueTransform = rowTransform.Find("Value Text");
                if (valueTransform != null)
                {
                    row.valueText = valueTransform.GetComponent<TMP_Text>();
                }
            }

            if (row.toggle == null)
            {
                row.toggle = rowTransform.GetComponentInChildren<Toggle>(true);
            }

            if (row.leftArrow == null)
            {
                Transform leftTransform = rowTransform.Find("Left Arrow");
                if (leftTransform != null)
                {
                    row.leftArrow = leftTransform.GetComponent<Image>();
                }
            }

            if (row.rightArrow == null)
            {
                Transform rightTransform = rowTransform.Find("Right Arrow");
                if (rightTransform != null)
                {
                    row.rightArrow = rightTransform.GetComponent<Image>();
                }
            }
        }
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }
    }

    public void SetRowValue(int index, string value)
    {
        Row row = GetRow(index);
        if (row == null || row.valueText == null)
        {
            return;
        }

        row.valueText.text = value;
    }

    public void SetRowToggle(int index, bool isOn)
    {
        Row row = GetRow(index);
        if (row == null || row.toggle == null)
        {
            return;
        }

        // No listeners are attached, but go through SetIsOnWithoutNotify anyway so wiring an
        // onValueChanged later can't feed our own refresh back into us.
        row.toggle.SetIsOnWithoutNotify(isOn);
    }

    /// <summary>
    /// Recolors a row for the cursor. <paramref name="editing"/> means left/right are live on it.
    /// </summary>
    public void SetRowHighlight(int index, bool selected, bool editing)
    {
        Row row = GetRow(index);
        if (row == null)
        {
            return;
        }

        Color tint = !selected ? normalColor : (editing ? editingColor : selectedColor);

        if (row.label != null)
        {
            row.label.color = tint;
        }

        if (row.valueText != null)
        {
            row.valueText.color = tint;
        }

        // Arrows only read as "you can press left/right here" on the selected row, so dim them
        // everywhere else instead of hiding them (the grid layout keeps the row width either way).
        float alpha = selected ? selectedArrowAlpha : idleArrowAlpha;
        ApplyArrowTint(row.leftArrow, tint, alpha);
        ApplyArrowTint(row.rightArrow, tint, alpha);
    }

    private static void ApplyArrowTint(Image arrow, Color tint, float alpha)
    {
        if (arrow == null)
        {
            return;
        }

        arrow.color = new Color(tint.r, tint.g, tint.b, alpha);
    }

    private Row GetRow(int index)
    {
        if (rows == null || index < 0 || index >= rows.Length)
        {
            return null;
        }

        return rows[index];
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
