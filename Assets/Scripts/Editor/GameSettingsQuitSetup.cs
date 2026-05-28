using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Automatikusan hozzáadja a Feladás gombot, a megerősítő panelt
/// és a Music Mute togglet a meglévő GameSettings → Canvas → Panel hierarchiához.
///
/// Menük:
///   Tools → Setup GameSettings Quit Button
///   Tools → Setup GameSettings Music Mute Toggle
/// </summary>
public static class GameSettingsQuitSetup
{
    // ──────────────────────────────────────────────────────────────
    // Feladás gomb + confirm panel
    // ──────────────────────────────────────────────────────────────

    [MenuItem("Tools/Setup GameSettings Quit Button")]
    static void SetupQuit()
    {
        var gsUI = Object.FindFirstObjectByType<GameSettingsUI>();
        if (gsUI == null) { Debug.LogError("GameSettingsUI nem található a scene-ben!"); return; }
        if (gsUI.settingsPanel == null) { Debug.LogError("settingsPanel nincs bekötve!"); return; }

        if (gsUI.quitButton != null)
        {
            Debug.LogWarning("quitButton már be van kötve – setup kihagyva.");
            return;
        }

        Transform panelTr = gsUI.settingsPanel.transform;
        Canvas canvas = gsUI.settingsPanel.GetComponentInParent<Canvas>();

        // 1. Feladás gomb
        var quitGO = CreateButton("QuitButton", panelTr, "Feladás",
            anchoredPos: new Vector2(0, -230), size: new Vector2(340, 80),
            normalColor: new Color(0.75f, 0.2f, 0.2f));

        // 2. Megerősítő panel (Canvas szinten)
        var confirmGO = new GameObject("ConfirmPanel");
        Undo.RegisterCreatedObjectUndo(confirmGO, "Create ConfirmPanel");
        confirmGO.transform.SetParent(canvas.transform, false);
        confirmGO.SetActive(false);

        var confirmRect = confirmGO.AddComponent<RectTransform>();
        confirmRect.anchorMin = Vector2.zero;
        confirmRect.anchorMax = Vector2.one;
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;

        var bg = confirmGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        bg.raycastTarget = true;

        var boxGO = new GameObject("Box");
        Undo.RegisterCreatedObjectUndo(boxGO, "Create ConfirmBox");
        boxGO.transform.SetParent(confirmGO.transform, false);
        var boxRect = boxGO.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(500, 300);
        boxRect.anchoredPosition = Vector2.zero;
        var boxImg = boxGO.AddComponent<Image>();
        boxImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var labelGO = new GameObject("Label");
        Undo.RegisterCreatedObjectUndo(labelGO, "Create ConfirmLabel");
        labelGO.transform.SetParent(boxGO.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.pivot = new Vector2(0.5f, 1);
        labelRect.anchoredPosition = new Vector2(0, -20);
        labelRect.sizeDelta = new Vector2(-40, 120);
        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = "Biztosan feladod a játékot?";
        labelText.fontSize = 40;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        var yesGO = CreateButton("ConfirmYesButton", boxGO.transform, "Igen",
            anchoredPos: new Vector2(-110, -200), size: new Vector2(180, 70),
            normalColor: new Color(0.2f, 0.65f, 0.2f));

        var noGO = CreateButton("ConfirmNoButton", boxGO.transform, "Nem",
            anchoredPos: new Vector2(110, -200), size: new Vector2(180, 70),
            normalColor: new Color(0.65f, 0.2f, 0.2f));

        // Bekötés
        Undo.RecordObject(gsUI, "Setup GameSettings Quit");
        gsUI.quitButton       = quitGO.GetComponent<Button>();
        gsUI.confirmPanel     = confirmGO;
        gsUI.confirmYesButton = yesGO.GetComponent<Button>();
        gsUI.confirmNoButton  = noGO.GetComponent<Button>();

        Finalize(gsUI, "✓ Feladás gomb sikeresen létrehozva! Mentsd el a scene-t (Ctrl+S).");
    }

    // ──────────────────────────────────────────────────────────────
    // Music Mute toggle
    // ──────────────────────────────────────────────────────────────

    [MenuItem("Tools/Setup GameSettings Music Mute Toggle")]
    static void SetupMusicMute()
    {
        var gsUI = Object.FindFirstObjectByType<GameSettingsUI>();
        if (gsUI == null) { Debug.LogError("GameSettingsUI nem található a scene-ben!"); return; }
        if (gsUI.settingsPanel == null) { Debug.LogError("settingsPanel nincs bekötve!"); return; }

        if (gsUI.musicMuteToggle != null)
        {
            Debug.LogWarning("musicMuteToggle már be van kötve – setup kihagyva.");
            return;
        }

        // Referencia toggle: klónozzuk a towerHpAlwaysVisibleToggle-t (vagy a detailedNumbersToggle-t)
        Toggle sourceToggle = gsUI.towerHpAlwaysVisibleToggle != null
            ? gsUI.towerHpAlwaysVisibleToggle
            : gsUI.detailedNumbersToggle;

        if (sourceToggle == null)
        {
            Debug.LogError("Nincs referencia toggle a klónozáshoz (detailedNumbersToggle / towerHpAlwaysVisibleToggle).");
            return;
        }

        // Klónozás
        var clone = Object.Instantiate(sourceToggle.gameObject, gsUI.settingsPanel.transform, false);
        Undo.RegisterCreatedObjectUndo(clone, "Create MusicMuteToggle");
        clone.name = "Music Mute";

        // Pozíció: a forrásnál 175-tel lejjebb (a két meglévő toggle lépésköze)
        var cloneRect = clone.GetComponent<RectTransform>();
        var srcRect   = sourceToggle.GetComponent<RectTransform>();
        cloneRect.anchoredPosition = srcRect.anchoredPosition + new Vector2(0, -175.53f);

        // Label szöveg cseréje – megkeresi a TMP gyereket
        var tmp = clone.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = "Zene némítása";

        // onValueChanged törlése (ne örököljük az eredeti listener-eket)
        var toggle = clone.GetComponent<Toggle>();
        toggle.onValueChanged.RemoveAllListeners();
        toggle.isOn = false;

        // Bekötés
        Undo.RecordObject(gsUI, "Setup Music Mute Toggle");
        gsUI.musicMuteToggle = toggle;

        Finalize(gsUI, "✓ Music Mute toggle sikeresen létrehozva! Mentsd el a scene-t (Ctrl+S).");
    }

    // ──────────────────────────────────────────────────────────────
    // Közös segédmetódusok
    // ──────────────────────────────────────────────────────────────

    static void Finalize(GameSettingsUI gsUI, string logMsg)
    {
        EditorUtility.SetDirty(gsUI);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gsUI.gameObject.scene);
        Debug.Log(logMsg);
    }

    static GameObject CreateButton(string name, Transform parent, string label,
        Vector2 anchoredPos, Vector2 size, Color normalColor)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = normalColor;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = normalColor;
        colors.highlightedColor = normalColor * 1.15f;
        colors.pressedColor     = normalColor * 0.8f;
        btn.colors = colors;
        btn.targetGraphic = img;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }
}
