using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayAcceptanceTests
{
    GameObject player, prompt;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    InteractionPromptUI View()
    {
        player = new GameObject("Return player", typeof(EmergencyReturnController), typeof(EmergencyReturnExitSequence));
        prompt = new GameObject("Local prompt", typeof(RectTransform), typeof(CanvasGroup), typeof(InteractionPromptUI));
        var view = prompt.GetComponent<InteractionPromptUI>();
        Set(view, "emergencyReturn", player.GetComponent<EmergencyReturnController>());
        Set(view, "emergencyExit", player.GetComponent<EmergencyReturnExitSequence>());
        return view;
    }
    [TearDown] public void Cleanup()
    {
        if (prompt != null) Object.DestroyImmediate(prompt);
        if (player != null) Object.DestroyImmediate(player);
    }
    [Test] public void PreparingReturnHidesNearbyCardAndCancellationRestoresIt()
    {
        var view = View(); var returning = player.GetComponent<EmergencyReturnController>();
        view.SetVisible(true); Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        Set(returning, "isPreparing", true); view.SetVisible(true);
        Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.Zero);
        returning.CancelEmergencyReturnByButton(); view.SetVisible(true);
        Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
    }
    [Test] public void ReadyReturnInstructionStillHasPriorityOverNearbyCard()
    {
        var view = View(); var returning = player.GetComponent<EmergencyReturnController>();
        Set(returning, "isPreparing", true); Set(returning, "gaugeFilled", true);
        view.SetVisible(true); Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.Zero);
    }
    [Test] public void ReturnExitKeepsCardHiddenWithoutReleasingBossSuppression()
    {
        var view = View(); var exit = player.GetComponent<EmergencyReturnExitSequence>();
        Set(exit, "isPlaying", true); view.SetVisible(true);
        Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.Zero);
        view.SetRegionBossSuppressed(true); Set(exit, "isPlaying", false); view.SetVisible(true);
        Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.Zero);
        view.SetRegionBossSuppressed(false); view.SetVisible(true);
        Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
    }
}
