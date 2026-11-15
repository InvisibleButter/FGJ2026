using System.Linq;
using FGJ2026.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FGJ2026.Tests
{
    public class LevelDraftTests
    {
        private static LevelDraft BuildExample()
        {
            LevelDraft draft = LevelDraft.CreateNew();
            draft.SetName("Example");
            draft.Resize(5, 4);
            draft.AddAnimal(new Vector2Int(0, 0), AnimalColor.Red, Directions.North, "move");
            int blue = draft.AddAnimal(new Vector2Int(4, 2), AnimalColor.Blue, Directions.North);
            draft.RotateAnimal(blue, clockwise: false); // north -> west
            Assert.IsTrue(draft.AddBehaviour(blue, "sing", out string error), error);
            draft.AddFlag(new Vector2Int(3, 2), AnimalColor.Red);
            draft.SetItemCount(ItemType.GoodSweet, 1);
            return draft;
        }

        [Test]
        public void NewDraft_IsNotValidUntilItHasAnimalsAndFlags_ButCanBePreviewed()
        {
            LevelDraft draft = LevelDraft.CreateNew();
            Assert.IsFalse(draft.Validate(out _, out _));
            Assert.IsNotNull(draft.BuildPreview(), "An empty board must still be drawable in the editor.");
        }

        [Test]
        public void BuiltDraft_PlaysLikeTheDesignExample()
        {
            LevelDraft draft = BuildExample();
            Assert.IsTrue(draft.Validate(out LevelDefinition level, out string error), error);

            var sim = new PuzzleSimulation(level);
            sim.TryPlaceItem(new Vector2Int(0, 2), ItemType.GoodSweet);
            sim.StartDay();
            sim.RunToEndOfDay();
            sim.StartDay();
            sim.RunToEndOfDay();
            Assert.AreEqual(PuzzlePhase.Won, sim.Phase);
        }

        [Test]
        public void Json_RoundTrip_KeepsEverything()
        {
            LevelDraft draft = BuildExample();
            draft.SetStar(1, StarKind.MaxTicks, 11);
            draft.SetSolution(new[] { new PlannedPlacement(1, ItemType.GoodSweet, new Vector2Int(0, 2)) });

            LevelDraft copy = LevelDraft.FromJson(draft.ToJson());
            Assert.AreEqual(draft.ToJson(), copy.ToJson());
            Assert.IsTrue(copy.Validate(out LevelDefinition level, out string error), error);
            Assert.AreEqual(2, level.Stars.Count);
            Assert.AreEqual(1, level.Solution.Count);
        }

        [Test]
        public void MapEdits_ClearTheSolution_SettingsDoNot()
        {
            LevelDraft draft = BuildExample();
            draft.SetSolution(new[] { new PlannedPlacement(1, ItemType.GoodSweet, new Vector2Int(0, 2)) });

            draft.SetName("Renamed");
            draft.SetStar(1, StarKind.MaxItems, 1);
            Assert.IsTrue(draft.HasSolution, "Name and stars don't change how the level plays.");

            draft.SetBlocked(new Vector2Int(2, 0), true);
            Assert.IsFalse(draft.HasSolution, "A map change can break the old solution.");
        }

        [Test]
        public void Squares_HoldOneThing_AndResizeDropsWhatFallsOff()
        {
            LevelDraft draft = BuildExample();
            Assert.AreEqual(-1, draft.AddFlag(new Vector2Int(0, 0), AnimalColor.Red), "animal square is taken");
            Assert.IsFalse(draft.SetBlocked(new Vector2Int(3, 2), true), "no water under a flag");

            draft.Resize(4, 4);
            Assert.AreEqual(1, draft.Data.animals.Length, "Blue at x=4 is outside a 4-wide board.");
        }

        [Test]
        public void ContentKey_IgnoresNameAndStars()
        {
            LevelDraft draft = BuildExample();
            string key = draft.ContentKey();
            draft.SetName("Other");
            draft.SetStar(2, StarKind.MaxDays, 1);
            Assert.AreEqual(key, draft.ContentKey());
            draft.RotateAnimal(0, true);
            Assert.AreNotEqual(key, draft.ContentKey());
        }

        [Test]
        public void MakeId_TurnsNamesIntoFileFriendlyIds()
        {
            Assert.AreEqual("pond-watch", LevelDraft.MakeId("Pond Watch!"));
            Assert.AreEqual("level", LevelDraft.MakeId("  "));
        }

        [Test]
        public void InvalidBehaviour_IsRefused()
        {
            LevelDraft draft = BuildExample();
            Assert.IsFalse(draft.AddBehaviour(0, "dance wildly", out string error));
            Assert.IsNotEmpty(error);
            Assert.IsFalse(draft.AddBehaviour(0, "move", out _), "duplicate");
            Assert.AreEqual(1, draft.Data.animals[0].behaviours.Count(b => b == "move"));
        }
    }
}
