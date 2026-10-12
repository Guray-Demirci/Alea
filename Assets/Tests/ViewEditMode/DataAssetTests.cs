using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>The design and art assets the game plays with are valid and complete.</summary>
    public class DataAssetTests
    {
        public const string StandardRunPath = "Assets/Data/StandardRun.asset";
        public const string CheckerArtPath = "Assets/Data/CheckerArt.asset";
        public const string ShopArtPath = "Assets/Data/ShopArt.asset";
        public const string DiceCardArtPath = "Assets/Data/DiceCardArt.asset";
        public const string TileArtPath = "Assets/Data/TileArt.asset";

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

        [Test]
        public void DiceCardArt_HasTheBackAndAFlipForEveryFace()
        {
            Assert.IsTrue(Load<DiceCardArtSet>(DiceCardArtPath).IsComplete);
        }

        [Test]
        public void DiceCardArt_EveryFlip_RunsFromTheBackToItsFace()
        {
            DiceCardArtSet art = Load<DiceCardArtSet>(DiceCardArtPath);

            for (int face = 1; face <= DiceCardFaces.CardCount; face++)
            {
                IReadOnlyList<Sprite> flip = art.GetFlip(face);

                // Turning a card down plays its flip backwards, so it must end on the very sprite of a card lying face down.
                Assert.AreSame(art.GetSprite(DiceCardFaces.FaceDown), flip[0], $"Face {face}.");
                Assert.AreEqual($"kartön{face}", flip[flip.Count - 1].name, $"Face {face}.");
            }
        }

        [Test]
        public void DiceCardArt_EveryFrameIsTheUprightCard_PivotedOnItsCentre()
        {
            DiceCardArtSet art = Load<DiceCardArtSet>(DiceCardArtPath);

            for (int face = 1; face <= DiceCardFaces.CardCount; face++)
            {
                foreach (Sprite frame in art.GetFlip(face))
                {
                    // BoardLayout.GetDiceCardPosition gives the card's centre.
                    Assert.AreEqual(new Vector2(17f, 26f), frame.rect.size, frame.name);
                    Assert.AreEqual(8.5f, frame.pivot.x, 1e-3f, frame.name);
                    Assert.AreEqual(13f, frame.pivot.y, 1e-3f, frame.name);
                }
            }
        }

        [Test]
        public void TileArt_HasThePowderKeg_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string keg = TileCatalog.GetEffectId(TileKind.PowderKeg);

            Assert.IsTrue(art.IsComplete);
            Assert.AreEqual("Mayın_Hanesi", art.GetSprite(keg).name);
            Assert.AreEqual(6, art.GetTransition(keg, true).Count, "BeyazdanBarutHanesineAnimasyon frames 2-7.");
            Assert.AreEqual(6, art.GetTransition(keg, false).Count, "SiyahtanBarutHanesineAnimasyon frames 2-7.");
        }

        [Test]
        public void TileArt_EveryFrameIsTheWholePoint_PivotedOnItsCentre()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);

            foreach (TileKind kind in (TileKind[])Enum.GetValues(typeof(TileKind)))
            {
                string effectId = TileCatalog.GetEffectId(kind);
                Sprite point = art.GetSprite(effectId);
                if (point == null)
                    continue;

                List<Sprite> frames = new List<Sprite> { point };
                frames.AddRange(art.GetTransition(effectId, true));
                frames.AddRange(art.GetTransition(effectId, false));

                foreach (Sprite frame in frames)
                {
                    // BoardLayout.GetNodeCenter gives the centre of the 10x56 triangle.
                    Assert.AreEqual(new Vector2(10f, 56f), frame.rect.size, frame.name);
                    Assert.AreEqual(5f, frame.pivot.x, 1e-3f, frame.name);
                    Assert.AreEqual(28f, frame.pivot.y, 1e-3f, frame.name);
                    Assert.AreEqual(12f, frame.pixelsPerUnit, frame.name);
                }
            }
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} is missing or is not a {typeof(T).Name}.");
            return asset;
        }
    }
}
