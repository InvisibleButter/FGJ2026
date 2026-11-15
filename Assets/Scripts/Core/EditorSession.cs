using System.Collections.Generic;
using FGJ2026.Simulation;

namespace FGJ2026
{
    /// <summary>A level played from the level editor (it returns to the editor afterwards).</summary>
    public sealed class TestRun
    {
        public LevelDefinition Level { get; }
        public bool InfiniteDays { get; }

        public TestRun(LevelDefinition level, bool infiniteDays)
        {
            Level = level;
            InfiniteDays = infiniteDays;
        }
    }

    /// <summary>How a test run was won: becomes the level's solution and can suggest star goals.</summary>
    public sealed class TestResult
    {
        /// <summary><see cref="LevelDraft.ContentKey"/> of the level that was tested.</summary>
        public string ContentKey;
        public int Days;
        public int Ticks;
        public int Items;
        public List<PlannedPlacement> Placements = new List<PlannedPlacement>();
    }

    /// <summary>The level editor's state, kept while a test run is played.</summary>
    public sealed class EditorSession
    {
        public string DraftJson;
        /// <summary>Id of the saved level being edited; null for a level that was never saved.</summary>
        public string SourceId;
        /// <summary>1-based position in the game's level order.</summary>
        public int FlowPosition = -1;
        public bool InfiniteDays = true;
        public string ContentKeyAtTest;
        public TestResult LastResult;
    }
}
