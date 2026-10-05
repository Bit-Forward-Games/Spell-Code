using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View half of the custom match rules panel, mirroring TrainingOptionsUI. It holds no rule state of
/// its own: whatever owns the rules pushes values in through SetRowValue. Lives on the "Match
/// Options" prefab root, which sits inactive under the Gamemodes Panel until it is opened.
///
/// Deliberately view-only. The rules themselves must be owned somewhere that can make them
/// host-authoritative for online play -- every one of these feeds a value that ends up in
/// SerializeSharedGameplayHashState, so a peer that sets them locally instead of receiving them
/// would diverge on frame one.
/// </summary>
public class MatchOptionsUI : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        public string rowName;
        public GameObject root;
        public TMP_Text label;
        public TMP_Text valueText;
        public Image leftArrow;
        public Image rightArrow;
    }

    // Row order is the contract with whatever owns the rules -- keep an enum on that side in this
    // order. These are the GameObject names authored in the prefab; the auto wiring below resolves
    // each row from them.
    //
    // No Toggle field here, unlike TrainingOptionsUI: every row on this panel is an arrow selector
    // with a Value Text, and the prefab contains no Toggle at all.
    public static readonly string[] RowNames =
    {
        "Match Type",
        "Ram to win",
        "Ram added per round",
        "Starting Lives",
        "Lives added per round"
    };

    [Tooltip("Object the option rows live under. Leave empty to search the whole panel.")]
    public Transform rowsParent;
    public Row[] rows = new Row[RowNames.Length];

    [Header("Highlight Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color editingColor = new Color(0.4f, 1f, 0.55f, 1f);

    [Tooltip("Rows that don't apply to the current match type are dimmed to this instead of hidden, so the panel doesn't reflow as the type changes.")]
    public Color unavailableColor = new Color(1f, 1f, 1f, 0.35f);

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
        ApplyRowTint(row, tint, selected ? selectedArrowAlpha : idleArrowAlpha);
    }

    /// <summary>
    /// Dims a row that the current match type doesn't use -- the RAM rows under Elimination, the
    /// lives rows under RAM Rush. Not in TrainingOptionsUI, because there every row always applies.
    /// Purely cosmetic: the owner still decides whether the cursor may land on the row.
    /// </summary>
    public void SetRowAvailable(int index, bool available)
    {
        Row row = GetRow(index);
        if (row == null)
        {
            return;
        }

        if (available)
        {
            ApplyRowTint(row, normalColor, idleArrowAlpha);
            return;
        }

        ApplyRowTint(row, unavailableColor, idleArrowAlpha * unavailableColor.a);
    }

    private void ApplyRowTint(Row row, Color tint, float arrowAlpha)
    {
        if (row.label != null)
        {
            row.label.color = tint;
        }

        if (row.valueText != null)
        {
            row.valueText.color = tint;
        }

        // Arrows only read as "you can press left/right here" on the selected row, so dim them
        // everywhere else instead of hiding them (the layout keeps the row width either way).
        ApplyArrowTint(row.leftArrow, tint, arrowAlpha);
        ApplyArrowTint(row.rightArrow, tint, arrowAlpha);
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
