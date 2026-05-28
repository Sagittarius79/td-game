using UnityEngine;

[CreateAssetMenu(fileName = "TutorialCardDatabase", menuName = "TD/Tutorial Card Database")]
public class TutorialCardDatabase : ScriptableObject
{
    [System.Serializable]
    public class TutorialCard
    {
        public string title;
        public Sprite image;
    }

    [Header("Fix bevezető kép")]
    public Sprite introSprite1;

    [Header("ToolTip kártyák")]
    public TutorialCard[] cards;
}
