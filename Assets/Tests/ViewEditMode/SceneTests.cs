using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tavla.View.Tests
{
    /// <summary>
    /// The scenes are wired the way the game expects. Sandbox.unity started as a copy of Main.unity, so the two
    /// drift apart unless something checks them. A scene is opened additively and closed again, so whatever the
    /// editor has open is left alone; a scene that is already open is checked as it is.
    /// </summary>
    public class SceneTests
    {
        private const string MainScene = "Assets/Scenes/Main.unity";
        private const string SandboxScene = "Assets/Scenes/Sandbox.unity";

        // Unity's built-in resources (default materials, skybox) have no asset file in the project.
        private const string BuiltInGuidPrefix = "0000000000000000";

        [TestCase(MainScene)]
        [TestCase(SandboxScene)]
        public void Scene_ReferencesOnlyAssetsThatExist(string scenePath)
        {
            string yaml = File.ReadAllText(scenePath);

            foreach (Match match in Regex.Matches(yaml, @"guid: ([0-9a-f]{32})"))
            {
                string guid = match.Groups[1].Value;
                if (guid.StartsWith(BuiltInGuidPrefix, StringComparison.Ordinal))
                    continue;

                Assert.IsNotEmpty(AssetDatabase.GUIDToAssetPath(guid), $"{scenePath} references a deleted asset ({guid}).");
            }
        }

        [TestCase(MainScene)]
        [TestCase(SandboxScene)]
        public void Scene_HasNoMissingScripts(string scenePath)
        {
            WithScene(scenePath, scene =>
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                                        $"{scenePath}: {child.name}");
                    }
                }
            });
        }

        [Test]
        public void MainScene_PlaysTheStandardRun_WithItsArt()
        {
            WithScene(MainScene, scene =>
            {
                SerializedObject board = new SerializedObject(FindBoardManager(scene));

                AssertReferences(board, "_run", DataAssetTests.StandardRunPath);
                AssertReferences(board, "_shopArt", DataAssetTests.ShopArtPath);
                AssertReferences(board, "_checkerArt", DataAssetTests.CheckerArtPath);
                AssertReferences(board, "_diceCardArt", DataAssetTests.DiceCardArtPath);
                AssertReferences(board, "_tileArt", DataAssetTests.TileArtPath);
            });
        }

        [Test]
        public void SandboxScene_HasNoRun_SoTheGodModePanelDealsItsOwnMatches()
        {
            WithScene(SandboxScene, scene =>
            {
                SerializedObject board = new SerializedObject(FindBoardManager(scene));

                Assert.IsNull(board.FindProperty("_run").objectReferenceValue);
                AssertReferences(board, "_checkerArt", DataAssetTests.CheckerArtPath);
                AssertReferences(board, "_diceCardArt", DataAssetTests.DiceCardArtPath);
                AssertReferences(board, "_tileArt", DataAssetTests.TileArtPath);
            });
        }

        private static void WithScene(string path, Action<Scene> check)
        {
            Scene open = SceneManager.GetSceneByPath(path);
            bool wasOpen = open.IsValid() && open.isLoaded;
            Scene scene = wasOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                check(scene);
            }
            finally
            {
                if (!wasOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static BoardManager FindBoardManager(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                BoardManager board = root.GetComponentInChildren<BoardManager>(true);
                if (board != null)
                    return board;
            }

            Assert.Fail($"{scene.path} has no BoardManager.");
            return null;
        }

        private static void AssertReferences(SerializedObject owner, string field, string assetPath)
        {
            SerializedProperty property = owner.FindProperty(field);
            Assert.IsNotNull(property, $"BoardManager has no field {field}.");
            Assert.AreEqual(assetPath, AssetDatabase.GetAssetPath(property.objectReferenceValue), field);
        }
    }
}
