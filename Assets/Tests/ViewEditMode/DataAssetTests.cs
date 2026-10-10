using System;
using NUnit.Framework;
using UnityEditor;

namespace Tavla.View.Tests
{
    /// <summary>The design and art assets the game plays with are valid and complete.</summary>
    public class DataAssetTests
    {
        public const string StandardRunPath = "Assets/Data/StandardRun.asset";
        public const string CheckerArtPath = "Assets/Data/CheckerArt.asset";
        public const string ShopArtPath = "Assets/Data/ShopArt.asset";

        [Test]
        public void StandardRun_IsAValidRun()
        {
            RunConfig run = Load<RunConfig>(StandardRunPath);

            // Core validates the whole run while building it and throws on the first mistake.
            Assert.DoesNotThrow(() => run.ToDefinition());
        }

        [Test]
        public void CheckerArt_HasArtForEverySpecialChecker()
        {
            CheckerArtSet art = Load<CheckerArtSet>(CheckerArtPath);

            foreach (CheckerKind kind in (CheckerKind[])Enum.GetValues(typeof(CheckerKind)))
            {
                CheckerSkin skin = art.GetSkin(CheckerCatalog.GetTypeId(kind));
                Assert.IsTrue(skin != null && skin.Normal != null, $"{kind} has no art in {CheckerArtPath}.");
            }
        }

        [Test]
        public void ShopArt_HasEverySprite()
        {
            Assert.IsTrue(Load<ShopArtSet>(ShopArtPath).IsComplete);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} is missing or is not a {typeof(T).Name}.");
            return asset;
        }
    }
}
