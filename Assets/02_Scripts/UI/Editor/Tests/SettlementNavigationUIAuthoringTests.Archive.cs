using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class SettlementNavigationUIAuthoringTests
{
    [Test]
    public void Archive_AuthoredAboveSettings_ReadOnlyAndSelectedPersistsAfterHoverExit()
    {
        WithPresentationServices(() =>
        {
            ReadyNavigation();
            PermanentProgress.Instance.LoadFromSave(new SaveData { unlockFlags = new System.Collections.Generic.List<string> { "tutorial_completed" } });
            var panel = Read<SettlementDialogueArchivePanelUI>(owner, "dialogueArchivePanel");
            Assert.That(panel.ValidateAuthoredReferences(out string error), Is.True, error);
            Assert.That(Primary(4).transform.parent, Is.SameAs(Primary(5).transform.parent));
            Assert.That(((RectTransform)Primary(4).transform).anchoredPosition.y, Is.GreaterThan(((RectTransform)Primary(5).transform).anchoredPosition.y));
            Assert.That(AuthoredRuntimeFixture.Find<SettlementDialogueArchivePanelUI>(scene).Length, Is.EqualTo(1));
            Assert.That(AuthoredRuntimeFixture.Find<DebugItemGrantUI>(scene).Length, Is.EqualTo(1));
            int[] beforeObjects = ObjectIds();
            string beforeProgress = JsonUtility.ToJson(PermanentProgress.Instance.CreateSaveData());
            Primary(4).onClick.Invoke();
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.DialogueArchive));
            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(Read<GameObject>(owner, "repairPanel").activeSelf, Is.False);
            Call(panel, "OnEnable");
            panel.SelectRecord(4);
            Assert.That(Read<TMP_Text>(panel, "transcript").text, Does.Contain("중계기"));
            var pointer = Primary(4).GetComponent<SettlementPrimaryNavigationPointer>();
            pointer.OnPointerEnter(new PointerEventData(eventSystem));
            pointer.OnPointerExit(new PointerEventData(eventSystem));
            Assert.That(View<TextMeshProUGUI>(4, "Label").color, Is.EqualTo(SettlementSelectionColors.Selected));
            Primary(0).GetComponent<SettlementPrimaryNavigationPointer>().OnPointerEnter(new PointerEventData(eventSystem));
            Assert.That(View<Image>(0, "Background").color, Is.EqualTo(SettlementSelectionColors.HoverBackground));
            Assert.That(View<Outline>(0, "ActiveOutline").effectColor, Is.EqualTo(SettlementSelectionColors.Hover));
            Primary(0).GetComponent<SettlementPrimaryNavigationPointer>().OnPointerExit(new PointerEventData(eventSystem));
            Assert.That(JsonUtility.ToJson(PermanentProgress.Instance.CreateSaveData()), Is.EqualTo(beforeProgress));
            Read<Button>(panel, "backButton").onClick.Invoke();
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Main));
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(ObjectIds(), Is.EqualTo(beforeObjects));
            Call(panel, "OnDisable");
        });
    }

    [Test]
    public void CampaignQA_AuthoredF10OwnerRetainsCatalogsAndAcceptsSettlementState()
    {
        var debug = AuthoredRuntimeFixture.Single<DebugItemGrantUI>(scene);
        Assert.That(Read<TraitCatalog>(debug, "traitCatalog"), Is.Not.Null);
        Assert.That(Read<ReinforcementCatalog>(debug, "reinforcementCatalog"), Is.Not.Null);
        Assert.That(Read<TMP_FontAsset>(debug, "uiFont"), Is.Not.Null);
        string source = System.IO.File.ReadAllText("Assets/02_Scripts/UI/DebugItemGrantUI.cs");
        Assert.That(source, Does.Contain("state == GameState.Settlement"));
        Assert.That(source, Does.Contain("RegisterCancelHandler(this, Close)"));
        Assert.That(source, Does.Contain("RestoreCursorState()"));
        string campaign = System.IO.File.ReadAllText("Assets/02_Scripts/UI/DebugItemGrantUI.Campaign.cs");
        Assert.That(campaign, Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
        Assert.That(campaign, Does.Not.Contain("RunWallet").And.Not.Contain("StartConversation(").And.Not.Contain("BeginSettlementDefense(").And.Not.Contain("LaunchFinal"));
    }
}
