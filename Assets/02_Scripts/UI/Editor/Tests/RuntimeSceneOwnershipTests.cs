using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RuntimeSceneOwnershipTests
{
    [TestCase(false, false, true, true, true)]
    [TestCase(true, true, true, true, true)]
    [TestCase(true, false, false, true, true)]
    [TestCase(true, false, true, false, true)]
    [TestCase(true, false, true, true, false)]
    public void GameBootstrap_TransientLifecycleClassificationIsExplicit(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded,
        bool expected)
    {
        Assert.That(
            GameBootstrap.IsTransientLifecycleStateForEditorAndTests(
                isPlaying,
                isPreviewScene,
                sceneIsValid,
                sceneIsLoaded),
            Is.EqualTo(expected));
    }

    [TestCase(false, false, true, true, true, false, false)]
    [TestCase(true, true, true, true, true, false, false)]
    [TestCase(true, false, false, true, true, false, false)]
    [TestCase(true, false, true, false, true, false, false)]
    [TestCase(true, false, true, true, false, false, false)]
    [TestCase(true, false, true, true, true, true, false)]
    [TestCase(true, false, true, true, true, false, true)]
    public void GameBootstrap_PersistenceDecisionRequiresOneValidRuntimeRoot(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded,
        bool isRoot,
        bool isAlreadyPersistent,
        bool expected)
    {
        Assert.That(
            GameBootstrap.ShouldRequestPersistenceForEditorAndTests(
                isPlaying,
                isPreviewScene,
                sceneIsValid,
                sceneIsLoaded,
                isRoot,
                isAlreadyPersistent),
            Is.EqualTo(expected));
    }

    [Test]
    public void RunResultPanelUI_CreatesRuntimeChildrenInParentsSceneBeforeParenting()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject parentObject = new GameObject("ResultPanelParent", typeof(RectTransform));
            parentObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(parentObject, previewScene);
            RectTransform parent = parentObject.GetComponent<RectTransform>();

            GameObject panel =
                RunResultPanelUI.CreateSceneOwnedRuntimeObjectForEditorAndTests(
                    "RuntimePanel",
                    parent,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            GameObject text =
                RunResultPanelUI.CreateSceneOwnedRuntimeObjectForEditorAndTests(
                    "RuntimeText",
                    parent,
                    typeof(RectTransform),
                    typeof(CanvasRenderer));

            Assert.That(panel.scene, Is.EqualTo(previewScene));
            Assert.That(text.scene, Is.EqualTo(previewScene));
            Assert.That(panel.transform.parent, Is.SameAs(parent));
            Assert.That(text.transform.parent, Is.SameAs(parent));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
        }
        finally
        {
            if (previewScene.IsValid() && previewScene.isLoaded)
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }
    }

    [Test]
    public void GameBootstrap_NonBootRuntimeSceneIsNotAnInitialAuthority()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene misplacedScene = SceneManager.CreateScene("MisplacedBootstrapTest");
        try
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }

            Assert.That(
                GameBootstrap.IsAuthorizedBootSceneForEditorAndTests(misplacedScene),
                Is.False);
        }
        finally
        {
            if (misplacedScene.IsValid() && misplacedScene.isLoaded)
            {
                EditorSceneManager.CloseScene(misplacedScene, true);
            }

            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }

    [Test]
    public void RunResultPanelUI_DefersOnlyOneInitializationForTransientOwnership()
    {
        Assert.That(
            RunResultPanelUI.ShouldScheduleDeferredInitializationForEditorAndTests(
                true,
                false,
                false,
                false,
                true),
            Is.True);
        Assert.That(
            RunResultPanelUI.ShouldScheduleDeferredInitializationForEditorAndTests(
                true,
                false,
                false,
                true,
                true),
            Is.False,
            "An already scheduled restoration retry must not be duplicated.");
        Assert.That(
            RunResultPanelUI.ShouldScheduleDeferredInitializationForEditorAndTests(
                true,
                true,
                true,
                false,
                true),
            Is.False,
            "An initialized result panel must not schedule another construction pass.");
    }
}
