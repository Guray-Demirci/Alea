using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Interfaces;
using Tavla.Core.Run;
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
        public void CheckerArt_BreaksEverySpecialChecker_WithFramesTwoToSixOfItsOwnFile()
        {
            CheckerArtSet art = Load<CheckerArtSet>(CheckerArtPath);
            HashSet<string> files = new HashSet<string>();

            foreach (CheckerKind kind in (CheckerKind[])Enum.GetValues(typeof(CheckerKind)))
            {
                Sprite[] frames = art.GetSkin(CheckerCatalog.GetTypeId(kind)).Break;
                Assert.AreEqual(5, frames.Length, $"{kind} break frames in {CheckerArtPath}");

                string file = null;
                for (int i = 0; i < frames.Length; i++)
                {
                    Assert.IsNotNull(frames[i], $"{kind} break frame {i}");

                    string path = AssetDatabase.GetAssetPath(frames[i]);
                    string suffix = $"_{i + 2}.png";
                    StringAssert.StartsWith("Assets/Sprites/Checkers/Breaks/", path);
                    StringAssert.EndsWith(suffix, path);

                    // All five from one artist's file, and no other checker's.
                    string own = path.Substring(0, path.Length - suffix.Length);
                    if (file == null)
                        Assert.IsTrue(files.Add(own), $"{kind} breaks like another checker: {own}");
                    else
                        Assert.AreEqual(file, own, $"{kind} break frame {i}");
                    file = own;

                    // Cut square around the 12x12 disc, pivot on its centre like the resting art, at the board's scale.
                    Assert.AreEqual(12f, frames[i].pixelsPerUnit, path);
                    Assert.AreEqual(new Vector2(16f, 16f), frames[i].rect.size, path);
                    Assert.AreEqual(new Vector2(8f, 8f), frames[i].pivot, path);
                }
            }
        }

        [Test]
        public void ShopArt_HasEverySprite()
        {
            Assert.IsTrue(Load<ShopArtSet>(ShopArtPath).IsComplete);
        }

        [Test]
        public void ShopArt_TheStallsTexture_IsReadable_ForItsHoverGlow()
        {
            Assert.IsTrue(Load<ShopArtSet>(ShopArtPath).SellButton.texture.isReadable,
                          "Tick Read/Write on the stall's sprite, or it does not glow on hover.");
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
        public void TileArt_HasTheMaelstrom_WithItsLightTransition()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string maelstrom = TileCatalog.GetEffectId(TileKind.Maelstrom);

            Assert.AreEqual("Girdap Hanesi", art.GetSprite(maelstrom).name);
            Assert.AreEqual(6, art.GetTransition(maelstrom, true).Count, "BeyazdanGirdapHanesineAnimasyon frames 2-7.");
        }

        [Test]
        public void TileArt_HasTheSwamp_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string swamp = TileCatalog.GetEffectId(TileKind.Swamp);

            Assert.AreEqual("Bataklık Hanesi", art.GetSprite(swamp).name);
            Assert.AreEqual(6, art.GetTransition(swamp, true).Count, "BeyazdanBataklıkHanesiAnimasyon frames 2-7.");
            Assert.AreEqual(6, art.GetTransition(swamp, false).Count, "SiyahtanBataklıkHanesiAnimasyon frames 2-7.");
        }

        [Test]
        public void TileArt_HasTheSpikePit_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string spikePit = TileCatalog.GetEffectId(TileKind.SpikePit);

            Assert.AreEqual("TahtaKazıkHanesi", art.GetSprite(spikePit).name);
            Assert.AreEqual(6, art.GetTransition(spikePit, true).Count, "BeyazdanTahtaKazıkAnimasyon frames 2-7.");
            Assert.AreEqual(6, art.GetTransition(spikePit, false).Count, "SiyahdanTahtaKazıkAnimasyon frames 2-7.");
        }

        [Test]
        public void TileArt_HasTheFog_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string fog = TileCatalog.GetEffectId(TileKind.Fog);

            Assert.AreEqual("SisHanesi", art.GetSprite(fog).name);
            Assert.AreEqual(6, art.GetTransition(fog, true).Count, "BeyazdanSisHanesiAnimasyon frames 2-7.");
            Assert.AreEqual(6, art.GetTransition(fog, false).Count, "SiyahdanSisHanesiAnimasyon frames 2-7.");
        }

        [Test]
        public void TileArt_HasTheDungeon_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string dungeon = TileCatalog.GetEffectId(TileKind.Dungeon);

            // Longer than the others: the stone spreads, then the bars go up.
            Assert.AreEqual("ZindanHanesi", art.GetSprite(dungeon).name);
            Assert.AreEqual(9, art.GetTransition(dungeon, true).Count, "BeyazdanZindanHanesineAnimasyon frames 2-10.");
            Assert.AreEqual(9, art.GetTransition(dungeon, false).Count, "SiyahdanZindanHanesineAnimasyon frames 2-10.");

            // Open: the artist's frame 6, the stone before the bars; the frames after it close the cage.
            Assert.AreEqual(5, art.GetOpenFrame(dungeon));
            Assert.AreEqual("BeyazdanZindanHanesineAnimasyon_6", art.GetTransition(dungeon, true)[4].name);
            Assert.AreEqual("SiyahdanZindanHanesineAnimasyon_6", art.GetTransition(dungeon, false)[4].name);
        }

        [Test]
        public void TileArt_HasThePlunderCove_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string cove = TileCatalog.GetEffectId(TileKind.PlunderCove);

            Assert.AreEqual("KorsanKoyu", art.GetSprite(cove).name);
            Assert.AreEqual(6, art.GetTransition(cove, true).Count, "BeyazdanKorsanKoyuAnimasyon frames 2-7.");
            Assert.AreEqual(6, art.GetTransition(cove, false).Count, "SiyahtanKorsanKoyuAnimasyon frames 2-7.");
        }

        [Test]
        public void TileArt_HasTheTollBooth_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string tollBooth = TileCatalog.GetEffectId(TileKind.TollBooth);

            // Shorter than the others: 7 frames, so only 5 in between.
            Assert.AreEqual("VergiHanesi", art.GetSprite(tollBooth).name);
            Assert.AreEqual(5, art.GetTransition(tollBooth, true).Count, "BeyazdanVergiHanesiAnimasyon frames 2-6.");
            Assert.AreEqual(5, art.GetTransition(tollBooth, false).Count, "SiyahtanVergiHanesiAnimasyon frames 2-6.");
        }

        [Test]
        public void TileArt_HasTheGraveyard_WithBothTransitions()
        {
            TileArtSet art = Load<TileArtSet>(TileArtPath);
            string graveyard = TileCatalog.GetEffectId(TileKind.Graveyard);

            Assert.AreEqual("Mezarlık Hanesi", art.GetSprite(graveyard).name);
            Assert.AreEqual(6, art.GetTransition(graveyard, true).Count, "BeyazdanMezarlıkHanesiAnimasyon frames 2-7.");
            Assert.AreEqual(6, art.GetTransition(graveyard, false).Count, "SiyahtanMezarlıkHanesiAnimasyon frames 2-7.");
        }

        [Test]
        public void StandardRun_DrawsOnlyTilesThatHaveArt()
        {
            // A tile without art would leave its point plain, so the player could not see it.
            RunDefinition run = Load<RunConfig>(StandardRunPath).ToDefinition();
            TileArtSet art = Load<TileArtSet>(TileArtPath);

            foreach (Func<ITileEffect> createTile in run.TilePool)
            {
                string effectId = createTile().EffectId;
                Assert.IsNotNull(art.GetSprite(effectId), $"{effectId} is in the tile pool but has no art in {TileArtPath}.");
            }
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
