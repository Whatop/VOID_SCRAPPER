using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicit, idempotent prefab authoring. No scene or gameplay-data writes.
public static class FeedbackPolishAuthoring
{
    public const string ReactorPath = "Assets/03_Prefabs/Event/Event_UnstableReactor.prefab";
    [MenuItem("Tools/VOID SCRAPPER/Author Reactor Feedback")]
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var root = PrefabUtility.LoadPrefabContents(ReactorPath);
        try
        {
            var existing = root.transform.Find("ReactorReadout");
            var readout = existing != null ? existing.gameObject : new GameObject("ReactorReadout", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var rect = (RectTransform)readout.transform;
            rect.SetParent(root.transform, false);
            rect.localPosition = new Vector3(0, -.7f, 0);
            rect.localScale = Vector3.one * .03f;
            rect.sizeDelta = new Vector2(72, 28);
            var canvas = readout.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            var feedback = readout.GetComponent<ReactorFeedbackUI>() ?? readout.AddComponent<ReactorFeedbackUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var time = Label(rect, "Countdown", new Vector2(0, 5), new Vector2(72, 14), font, 11, new Color(1, .8f, .35f));
            var hp = Label(rect, "Health", new Vector2(0, -7), new Vector2(72, 10), font, 8, new Color(.85f, .91f, .96f));
            var view = new SerializedObject(feedback);
            view.FindProperty("countdownText").objectReferenceValue = time;
            view.FindProperty("healthText").objectReferenceValue = hp;
            view.ApplyModifiedPropertiesWithoutUndo();
            var owner = new SerializedObject(root.GetComponent<ExpeditionEventObject>());
            owner.FindProperty("reactorFeedback").objectReferenceValue = feedback;
            owner.ApplyModifiedPropertiesWithoutUndo();
            readout.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, ReactorPath);
            Directory.CreateDirectory("Logs/FeedbackPolish1");
            File.WriteAllText("Logs/FeedbackPolish1/authoring.txt", "Saved ReactorReadout only. Existing HP, timer, rewards, colliders and visuals retained.\n");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static TMP_Text Label(RectTransform parent, string name, Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize, Color color)
    {
        var child = parent.Find(name);
        var label = child != null ? child.GetComponent<TextMeshProUGUI>() : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = size;
        label.font = font; label.fontSize = fontSize; label.color = color;
        label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false; label.text = string.Empty;
        return label;
    }
}
