using System;
using System.Collections.Generic;
using UnityEngine;

namespace FGJ2026
{
    public sealed class PuzzleMap
    {
        public const int MaximumSize = 128;
        private readonly HashSet<Vector2Int> blockedCells;

        public int Width { get; }
        public int Height { get; }
        public Vector2Int Start { get; }
        public Vector2Int StartDirection { get; }
        public Vector2Int Goal { get; }
        public IReadOnlyList<CueSource> Cues { get; }

        private PuzzleMap(MapData data, Vector2Int startDirection, HashSet<Vector2Int> obstacles, CueSource[] cues)
        {
            Width = data.width;
            Height = data.height;
            Start = data.start.Position;
            StartDirection = startDirection;
            Goal = data.goal.Position;
            blockedCells = obstacles;
            Cues = Array.AsReadOnly(cues);
        }

        public bool Contains(Vector2Int cell) =>
            cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public bool IsBlocked(Vector2Int cell) => blockedCells.Contains(cell);

        public bool IsCueAt(CueSource cue, Vector2Int cell) => Contains(cell) && cell.y == cue.Cell.y;

        public static bool TryParse(string json, out PuzzleMap map, out string error) =>
            TryParse(json, out map, out error, out _);

        public static bool TryParse(string json, out PuzzleMap map, out string error, out MapData mapData)
        {
            map = null;
            mapData = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Map JSON is empty.";
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<MapData>(json);
                if (data == null || data.width < 1 || data.width > MaximumSize ||
                    data.height < 1 || data.height > MaximumSize)
                    throw new FormatException($"width and height must be between 1 and {MaximumSize}.");

                Vector2Int start = ReadCell(data.start, "start", data);
                Vector2Int goal = ReadCell(data.goal, "goal", data);
                if (start == goal)
                    throw new FormatException("start and goal must be different cells.");

                Vector2Int direction = data.start.facing switch
                {
                    "north" => Vector2Int.up,
                    "east" => Vector2Int.right,
                    "south" => Vector2Int.down,
                    "west" => Vector2Int.left,
                    _ => throw new FormatException("start.facing must be north, east, south, or west.")
                };

                var obstacles = new HashSet<Vector2Int>();
                Cell[] blocked = data.blocked ?? Array.Empty<Cell>();
                for (int i = 0; i < blocked.Length; i++)
                {
                    Vector2Int obstacle = ReadCell(blocked[i], $"blocked[{i}]", data);
                    if (obstacle == start || obstacle == goal)
                        throw new FormatException($"blocked[{i}] overlaps the start or goal.");
                    obstacles.Add(obstacle);
                }

                CueData[] sources = data.cues ?? Array.Empty<CueData>();
                var cues = new CueSource[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    CueData source = sources[i];
                    Vector2Int cell = ReadCell(source, $"cues[{i}]", data);
                    if (string.IsNullOrWhiteSpace(source.type))
                        throw new FormatException($"cues[{i}].type must identify the cue, e.g. birdsong.");
                    cues[i] = new CueSource(source.type.Trim(), cell);
                }

                map = new PuzzleMap(data, direction, obstacles, cues);
                data.blocked = blocked;
                data.cues = sources;
                mapData = data;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static Vector2Int ReadCell(Cell cell, string field, MapData data)
        {
            if (cell == null || cell.x < 0 || cell.x >= data.width || cell.y < 0 || cell.y >= data.height)
                throw new FormatException($"{field} must be a cell inside the board.");
            return cell.Position;
        }

        public readonly struct CueSource
        {
            public string Type { get; }
            public Vector2Int Cell { get; }

            public CueSource(string type, Vector2Int cell)
            {
                Type = type;
                Cell = cell;
            }
        }

        [Serializable]
        public sealed class MapData
        {
            public int width = 0;
            public int height = 0;
            public StartData start = null;
            public Cell goal = null;
            public Cell[] blocked = null;
            public CueData[] cues = null;
        }

        [Serializable]
        public class Cell
        {
            public int x = -1;
            public int y = -1;
            public Vector2Int Position => new Vector2Int(x, y);
        }

        [Serializable]
        public sealed class StartData : Cell
        {
            public string facing = null;
        }

        [Serializable]
        public sealed class CueData : Cell
        {
            public string type = null;
        }
    }
}
