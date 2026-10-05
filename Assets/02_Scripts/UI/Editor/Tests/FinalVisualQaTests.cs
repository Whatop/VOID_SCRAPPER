using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class FinalVisualQaTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Test]
    public void RainFootprintSurvivesEveryAnimationFrame()
    {
        foreach (string role in new[] { "RainWarning", "RainFire" })
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NullArtAuthoring.Vfx + role + ".prefab"));
            try
            {
                var effect = go.GetComponent<NullSignatureVfx>(); effect.Play(go, role == "RainWarning" ? .8f : 0);
                effect.SetLine(new Vector2(-9, 1), new Vector2(9, 1), true);
                for (int n = 0; n < 8; n++)
                {
                    effect.AdvancePresentation(.09f);
                    Assert.That(effect.Visual.size, Is.EqualTo(new Vector2(1, 18)), role + " frame " + effect.FrameIndex);
                    Assert.That(effect.Visual.bounds.size.x, Is.EqualTo(18).Within(.001f));
                    Assert.That(effect.Visual.bounds.size.y, Is.EqualTo(1).Within(.001f));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
    [Test]
    public void PurpleRelayAndEndingUseOneApprovedCell()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(NullArtAuthoring.BossPath);
        var so = new SerializedObject(root.GetComponent<NullDispatcherPresentation>());
        var sprite = (Sprite)so.FindProperty("endingCoreSprite").objectReferenceValue;
        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(64, 64)));
        Assert.That(root.transform.Find("PhaseRelay1").GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
        Assert.That(root.transform.Find("PhaseRelay2").GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
    }
    [TestCase(GameState.BossBattle, false)]
    [TestCase(GameState.FinalBossBattle, false)]
    [TestCase(GameState.Expedition, true)]
    [TestCase(GameState.SettlementDefense, true)]
    public void PromptPriorityPreservesDefenseAndRestoresAfterBoss(GameState state, bool visible)
    {
        var previous = GameStateManager.Instance;
        var owner = new GameObject("QA state");
        var go = new GameObject("QA prompt", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            var manager = owner.AddComponent<GameStateManager>();
            typeof(GameStateManager).GetProperty("Instance").SetValue(null, manager);
            var prompt = go.AddComponent<InteractionPromptUI>();
            typeof(InteractionPromptUI).GetField("canvasGroup", Private).SetValue(prompt, go.GetComponent<CanvasGroup>());
            manager.ChangeState(state); prompt.SetVisible(true);
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(visible ? 1 : 0));
            manager.ChangeState(GameState.Expedition); prompt.SetVisible(true);
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(owner); typeof(GameStateManager).GetProperty("Instance").SetValue(null, previous); }
    }
    [TestCase(-400, -300)]
    [TestCase(400, 300)]
    public void CompactWorldPromptStaysInsideNativeHudSafeBounds(float x, float y)
    {
        var root = new GameObject("QA canvas", typeof(RectTransform));
        var go = new GameObject("QA prompt", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            var canvas = (RectTransform)root.transform; canvas.sizeDelta = new Vector2(480, 270);
            var rect = (RectTransform)go.transform; rect.SetParent(canvas, false); rect.sizeDelta = new Vector2(200, 28); rect.anchoredPosition = new Vector2(x, y);
            var prompt = go.AddComponent<InteractionPromptUI>();
            typeof(InteractionPromptUI).GetField("rectTransform", Private).SetValue(prompt, rect);
            typeof(InteractionPromptUI).GetField("canvasRectTransform", Private).SetValue(prompt, canvas);
            typeof(InteractionPromptUI).GetMethod("ClampCompactPromptToCanvas", Private).Invoke(prompt, null);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = canvas.InverseTransformPoint(corners[0]); var max = canvas.InverseTransformPoint(corners[2]);
            Assert.That(min.x, Is.GreaterThanOrEqualTo(-232)); Assert.That(max.x, Is.LessThanOrEqualTo(232));
            Assert.That(min.y, Is.GreaterThanOrEqualTo(-99)); Assert.That(max.y, Is.LessThanOrEqualTo(71));
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test]
    public void CommunicationUsesAuthoredTopBandAndExistingTiming()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/Expedition.unity");
        try
        {
            WarningMessageUI warning = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.GetComponentInChildren<WarningMessageUI>(true) is WarningMessageUI found) warning = found;
            Assert.That(warning, Is.Not.Null);
            var rect = (RectTransform)warning.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, 1)));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(-32)); Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(220, 24)));
            var so = new SerializedObject(warning);
            Assert.That(so.FindProperty("messageText").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("defaultDuration").floatValue, Is.EqualTo(1.4f));
            Assert.That(so.FindProperty("repeatSuppressionSeconds").floatValue, Is.EqualTo(.75f));
            foreach (var root in scene.GetRootGameObjects())
            {
                var boss = root.GetComponentInChildren<BossHealthBarUI>(true);
                if (boss == null) continue;
                var bossRoot = (GameObject)new SerializedObject(boss).FindProperty("rootObject").objectReferenceValue;
                Assert.That(((RectTransform)bossRoot.transform).anchoredPosition, Is.EqualTo(new Vector2(0, 17)));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
