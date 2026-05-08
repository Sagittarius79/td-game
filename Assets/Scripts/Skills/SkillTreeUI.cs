using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Skill fa scene vezérlője.
///
/// Felépíti a fát a SkillTreeDefinition ScriptableObject alapján:
///   1. SkillNodeUI prefabokat példányosít sor/oszlop pozícióba
///   2. Összekötő vonalakat rajzol az előfeltétel kapcsolatok mentén
///   3. Skill pontok kijelzése, frissítése upgrade után
///
/// Unity Editor beállítás:
///   1. Hozz létre egy új scene-t: ArcherSkillTree
///   2. Canvas → ScrollView → Viewport → Content = nodeParent
///   3. Hozd létre a SkillNodePrefab-ot (SkillNodeUI script + UI elemek)
///   4. Hozd létre az ArcherSkillTree ScriptableObject-et és töltsd fel node-okkal
///   5. Kösd be a mezőket
/// </summary>
public class SkillTreeUI : MonoBehaviour
{
    [Header("Fa definíció")]
    public SkillTreeDefinition treeDefinition;

    [Header("UI elemek")]
    public Transform          nodeParent;      // ScrollView Content
    public GameObject         nodePrefab;      // SkillNodeUI prefab
    public TextMeshProUGUI    pointsText;      // "Elérhető pontok: 5"
    public TextMeshProUGUI    treeTitleText;   // "ARCHER"
    public Button             backButton;
    public string             backSceneName = "MainMenu";
    public string             backPanel     = "DetailedCharacter"; // "DetailedCharacter" vagy "SpecialSkillsPanel"

    [Header("Elrendezés")]
    public float nodeWidth     = 110f;
    public float nodeHeight    = 110f;
    public float columnSpacing = 150f;
    public float rowSpacing    = 170f;

    [Header("Összekötő vonalak")]
    public Color lineColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);
    public float lineWidth = 5f;

    private List<SkillNodeUI> _nodeInstances = new List<SkillNodeUI>();

    // ── Életciklus ────────────────────────────────────────────────────

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);


        if (treeTitleText != null && treeDefinition != null)
            treeTitleText.text = treeDefinition.treeName.ToUpper();

        InitializeManualNodes();
        RefreshAll();
    }

    // ── Manuálisan elhelyezett node-ok inicializálása ─────────────────

    void InitializeManualNodes()
    {
        if (treeDefinition == null || nodeParent == null)
        {
            Debug.LogError("SkillTreeUI: hiányzó treeDefinition vagy nodeParent!");
            return;
        }

        _nodeInstances.Clear();

        // Megkeresi az összes SkillNodeUI-t a nodeParent gyerekei között
        var nodes = nodeParent.GetComponentsInChildren<SkillNodeUI>(includeInactive: true);

        if (nodes.Length == 0)
            Debug.LogWarning("SkillTreeUI: nem találhatók SkillNodeUI komponensek a nodeParent alatt!");

        foreach (var node in nodes)
        {
            node.Initialize(treeDefinition, RefreshAll);
            _nodeInstances.Add(node);
        }

        Debug.Log($"SkillTreeUI: {_nodeInstances.Count} node inicializálva.");
    }

    // ── Vonal rajzolás ────────────────────────────────────────────────

    void DrawLine(Vector2 from, Vector2 to)
    {
        var go   = new GameObject("SkillLine");
        go.transform.SetParent(nodeParent, false);
        go.transform.SetAsFirstSibling();   // vonalak a node-ok mögé kerülnek

        var img  = go.AddComponent<Image>();
        img.color = lineColor;

        var rect   = go.GetComponent<RectTransform>();
        Vector2 dir    = to - from;
        float   length = dir.magnitude;
        float   angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rect.sizeDelta        = new Vector2(length, lineWidth);
        rect.anchoredPosition = from + dir * 0.5f;
        rect.localRotation    = Quaternion.Euler(0, 0, angle);
        rect.pivot            = new Vector2(0.5f, 0.5f);
    }

    // ── Frissítés ─────────────────────────────────────────────────────

    void RefreshAll()
    {
        foreach (var node in _nodeInstances)
            node.Refresh();

        int points = UserProgressManager.Instance?.AvailableSkillPoints ?? 0;
        if (pointsText != null)
            pointsText.text = $"Elérhető pontok: {points}";
    }

    // ── Vissza ────────────────────────────────────────────────────────

    void OnBackPressed()
    {
        if (backPanel == "SpecialSkillsPanel")
            PlayerPrefs.SetInt("OpenSpecialSkillsPanel", 1);
        else if (backPanel == "SkillsPanel")
            PlayerPrefs.SetInt("OpenSkillsPanel", 1);
        else
            PlayerPrefs.SetInt("OpenDetailedCharacter", 1);
        SceneManager.LoadScene(backSceneName);
    }

}
