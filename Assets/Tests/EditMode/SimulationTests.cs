using System.Collections.Generic;
using System.Linq;
using FGJ2026.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FGJ2026.Tests
{
    public class SimulationTests
    {
        // The example from the game design: Red walks north, Blue sings west, the red flag is on Blue's song line.
        private const string ExampleLevel = @"{
            ""id"": ""example"", ""width"": 5, ""height"": 4, ""days"": 2,
            ""animals"": [
                { ""color"": ""red"",  ""x"": 0, ""y"": 0, ""facing"": ""north"", ""behaviours"": [""move""] },
                { ""color"": ""blue"", ""x"": 4, ""y"": 2, ""facing"": ""west"",  ""behaviours"": [""sing""] }
            ],
            ""flags"": [ { ""color"": ""red"", ""x"": 3, ""y"": 2 } ],
            ""items"": [ { ""type"": ""goodSweet"", ""count"": 1 } ]
        }";

        private static PuzzleSimulation Load(string json)
        {
            Assert.IsTrue(LevelDefinition.TryParse(json, "test", out LevelDefinition level, out string error), error);
            return new PuzzleSimulation(level);
        }

        private static bool Knows(Animal animal, SenseKind sense, AnimalColor color, bool positive) =>
            animal.Rules.Any(r => r.IsAbout(sense, Stimulus.OfAnimal(color)) && r.Positive == positive && !r.Pending);

        [Test]
        public void Example_SweetOnSongLine_IsSolvedOnDayTwo()
        {
            PuzzleSimulation sim = Load(ExampleLevel);
            Assert.IsTrue(sim.TryPlaceItem(new Vector2Int(0, 2), ItemType.GoodSweet));
            Assert.IsTrue(sim.StartDay());
            sim.RunToEndOfDay();

            Assert.AreEqual(PuzzlePhase.Preparation, sim.Phase, "Day one should end without a win.");
            Assert.AreEqual(2, sim.DayNumber);
            Assert.AreEqual(5, sim.TotalTicks, "Day one: 2 moves, eat, 1 move, walk off = 5 ticks.");
            Assert.IsTrue(Knows(sim.Animals[0], SenseKind.Hear, AnimalColor.Blue, true), "Red should like hearing Blue now.");
            Assert.IsEmpty(sim.Items, "The sweet was eaten.");

            Assert.IsTrue(sim.StartDay());
            sim.RunToEndOfDay();

            Assert.AreEqual(PuzzlePhase.Won, sim.Phase);
            Assert.AreEqual(11, sim.TotalTicks, "Day two: 2 moves, turn east, 3 moves = 6 ticks.");
            Assert.AreEqual(new Vector2Int(3, 2), sim.Animals[0].Cell);
            Assert.IsTrue(sim.EvaluateStars().All(s => s));
        }

        [Test]
        public void LearnedRule_DoesNotWorkOnTheSameDay()
        {
            PuzzleSimulation sim = Load(ExampleLevel);
            sim.TryPlaceItem(new Vector2Int(0, 2), ItemType.GoodSweet);
            sim.StartDay();

            for (int i = 0; i < 3; i++)
                sim.Step(); // move, move, eat
            Animal red = sim.Animals[0];
            Assert.IsTrue(red.Rules.Any(r => r.Pending), "The lesson is pending during day one.");

            sim.Step();
            Assert.AreEqual(new Vector2Int(0, 3), red.Cell, "Red keeps walking north on day one.");
        }

        [Test]
        public void WithoutTraining_TheLevelIsLost()
        {
            PuzzleSimulation sim = Load(ExampleLevel);
            sim.StartDay();
            sim.RunToEndOfDay();
            sim.StartDay();
            sim.RunToEndOfDay();
            Assert.AreEqual(PuzzlePhase.Lost, sim.Phase);
        }

        [Test]
        public void Rewind_RestoresTheStartOfTheDay()
        {
            PuzzleSimulation sim = Load(ExampleLevel);
            sim.TryPlaceItem(new Vector2Int(0, 2), ItemType.GoodSweet);
            sim.StartDay();
            sim.Step();
            sim.Step();

            sim.Rewind();

            Assert.AreEqual(PuzzlePhase.Preparation, sim.Phase);
            Assert.AreEqual(0, sim.Tick);
            Assert.AreEqual(ItemType.GoodSweet, sim.Items[new Vector2Int(0, 2)], "The sweet is back where it was placed.");
            Assert.AreEqual(0, sim.GetRemaining(ItemType.GoodSweet));
            Assert.IsTrue(sim.IsPlacedToday(new Vector2Int(0, 2)), "It can still be moved or taken back.");
            Assert.AreEqual(new Vector2Int(0, 0), sim.Animals[0].Cell);

            Assert.IsTrue(sim.TryRemoveItem(new Vector2Int(0, 2)));
            Assert.AreEqual(1, sim.GetRemaining(ItemType.GoodSweet), "Taken back, it is in the bag again.");
        }

        [Test]
        public void Rewind_AfterTheDayEnded_BringsBackTheEatenSweets()
        {
            PuzzleSimulation sim = Load(ExampleLevel);
            sim.TryPlaceItem(new Vector2Int(0, 2), ItemType.GoodSweet);
            sim.StartDay();
            sim.RunToEndOfDay();
            Assert.AreEqual(2, sim.DayNumber, "Day 1 is over.");
            Assert.IsEmpty(sim.Items, "The sweet was eaten.");

            sim.Rewind(); // in day 2's preparation: back to the start of day 1

            Assert.AreEqual(1, sim.DayNumber);
            Assert.AreEqual(PuzzlePhase.Preparation, sim.Phase);
            Assert.AreEqual(ItemType.GoodSweet, sim.Items[new Vector2Int(0, 2)], "The sweet is back where it was placed.");
            Assert.IsTrue(sim.Animals[0].Rules.All(r => !r.Pending) && sim.Animals[0].Rules.Count == 1, "Nothing learned yet.");

            sim.Rewind(); // again: the empty start of day 1
            Assert.AreEqual(1, sim.DayNumber);
            Assert.IsEmpty(sim.Items);
            Assert.AreEqual(1, sim.GetRemaining(ItemType.GoodSweet));
            Assert.AreEqual(1, sim.Animals[0].Rules.Count, "The lesson from day 1 is forgotten again.");

            // Playing day 1 again works like the first time.
            Assert.IsTrue(sim.TryPlaceItem(new Vector2Int(0, 2), ItemType.GoodSweet));
            sim.StartDay();
            sim.RunToEndOfDay();
            Assert.AreEqual(2, sim.DayNumber);
            sim.Rewind();
            Assert.AreEqual(1, sim.DayNumber);
            Assert.AreEqual(ItemType.GoodSweet, sim.Items[new Vector2Int(0, 2)]);
        }

        [Test]
        public void Items_CannotGoOnAnimalsOrFlags()
        {
            PuzzleSimulation sim = Load(ExampleLevel);
            Assert.IsFalse(sim.TryPlaceItem(new Vector2Int(0, 0), ItemType.GoodSweet), "animal square");
            Assert.IsFalse(sim.TryPlaceItem(new Vector2Int(3, 2), ItemType.GoodSweet), "flag square");
            Assert.IsTrue(sim.TryPlaceItem(new Vector2Int(1, 1), ItemType.GoodSweet));
            Assert.IsFalse(sim.TryPlaceItem(new Vector2Int(2, 1), ItemType.GoodSweet), "bag is empty");
        }

        [Test]
        public void BadSweet_TeachesToTurnAwayFromTheSinger()
        {
            PuzzleSimulation sim = Load(@"{
                ""id"": ""t"", ""width"": 5, ""height"": 5, ""days"": 2,
                ""animals"": [
                    { ""color"": ""red"",  ""x"": 2, ""y"": 0, ""facing"": ""north"", ""behaviours"": [""move""] },
                    { ""color"": ""blue"", ""x"": 0, ""y"": 3, ""facing"": ""east"",  ""behaviours"": [""sing""] }
                ],
                ""flags"": [ { ""color"": ""red"", ""x"": 4, ""y"": 3 } ],
                ""items"": [ { ""type"": ""badSweet"", ""count"": 1 } ]
            }");

            sim.TryPlaceItem(new Vector2Int(2, 3), ItemType.BadSweet);
            sim.StartDay();
            sim.RunToEndOfDay();
            Assert.IsTrue(Knows(sim.Animals[0], SenseKind.Hear, AnimalColor.Blue, false));

            sim.StartDay();
            sim.RunToEndOfDay();
            Assert.AreEqual(PuzzlePhase.Won, sim.Phase);
        }

        [Test]
        public void SeeingSomethingBad_TurnsRightOnce()
        {
            PuzzleSimulation sim = Load(@"{
                ""id"": ""t"", ""width"": 6, ""height"": 3, ""days"": 1,
                ""animals"": [
                    { ""color"": ""green"",  ""x"": 0, ""y"": 2, ""facing"": ""east"", ""behaviours"": [""move"", ""see yellow bad""] },
                    { ""color"": ""yellow"", ""x"": 5, ""y"": 2, ""facing"": ""west"", ""behaviours"": [] }
                ],
                ""flags"": [ { ""color"": ""green"", ""x"": 0, ""y"": 0 } ]
            }");
            Animal green = sim.Animals[0];
            sim.StartDay();

            sim.Step();
            Assert.AreEqual(Directions.South, green.Facing, "one 90° turn to the right");
            sim.Step();
            Assert.AreEqual(new Vector2Int(0, 1), green.Cell, "then it walks on in the new direction");
            Assert.AreEqual(Directions.South, green.Facing);
        }

        [Test]
        public void NewLesson_ReplacesTheOppositeFeeling()
        {
            PuzzleSimulation sim = Load(@"{
                ""id"": ""t"", ""width"": 5, ""height"": 4, ""days"": 3,
                ""animals"": [
                    { ""color"": ""red"",  ""x"": 0, ""y"": 0, ""facing"": ""north"", ""behaviours"": [""move"", ""hear blue good""] },
                    { ""color"": ""blue"", ""x"": 4, ""y"": 2, ""facing"": ""west"",  ""behaviours"": [""sing""] }
                ],
                ""flags"": [ { ""color"": ""red"", ""x"": 3, ""y"": 3 } ],
                ""items"": [ { ""type"": ""badSweet"", ""count"": 1 } ]
            }");

            // Red turns east on the song line on its own; put the bad sweet where it will be after turning.
            sim.TryPlaceItem(new Vector2Int(1, 2), ItemType.BadSweet);
            sim.StartDay();
            sim.RunToEndOfDay();

            Animal red = sim.Animals[0];
            Assert.IsTrue(Knows(red, SenseKind.Hear, AnimalColor.Blue, false));
            Assert.IsFalse(Knows(red, SenseKind.Hear, AnimalColor.Blue, true), "The old feeling is replaced.");
        }

        [Test]
        public void InvalidLevel_ReportsAReadableError()
        {
            Assert.IsFalse(LevelDefinition.TryParse(@"{ ""id"": ""x"", ""width"": 3, ""height"": 3,
                ""animals"": [ { ""color"": ""mauve"", ""x"": 0, ""y"": 0, ""facing"": ""north"" } ],
                ""flags"": [ { ""color"": ""red"", ""x"": 1, ""y"": 1 } ] }", "x", out _, out string error));
            StringAssert.Contains("mauve", error);
        }

        [Test]
        public void ShippedLevels_LoadAndTheirSolutionsWin()
        {
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/Levels" });
            Assert.IsNotEmpty(guids, "No levels found in Assets/Resources/Levels.");

            var failures = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (!LevelDefinition.TryParse(asset.text, asset.name, out LevelDefinition level, out string error))
                {
                    failures.Add($"{path}: {error}");
                    continue;
                }
                if (level.Solution.Count == 0)
                    continue;

                var sim = new PuzzleSimulation(level);
                while (sim.Phase == PuzzlePhase.Preparation)
                {
                    foreach (PlannedPlacement step in level.Solution.Where(p => p.Day == sim.DayNumber))
                    {
                        if (!sim.TryPlaceItem(step.Cell, step.Item))
                            failures.Add($"{path}: day {step.Day} could not place {step.Item} at {step.Cell}.");
                    }
                    sim.StartDay();
                    sim.RunToEndOfDay();
                }

                if (sim.Phase != PuzzlePhase.Won)
                    failures.Add($"{path}: the stored solution does not win (ended {sim.Phase} on day {sim.DayNumber}).");
                else
                    Debug.Log($"{level.Name}: solution wins on day {sim.DayNumber} in {sim.TotalTicks} ticks with {sim.ItemsUsed} items; stars {string.Join(",", sim.EvaluateStars())}");
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
