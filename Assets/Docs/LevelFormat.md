# Level format

The easiest way to make levels is the **level editor** (start menu > Level editor, or open
`Assets/Scenes/LevelEditor.unity` and press Play). It saves here as well, but only inside the Unity Editor.

Levels are JSON files in `Assets/Resources/Levels/`. The play order is the list of level ids in
`Assets/Resources/LevelOrder.json`; levels missing from it come last, sorted by file name. The test
`SimulationTests.ShippedLevels_LoadAndTheirSolutionsWin` checks every file and plays its `solution`
(the editor stores the last won test run as the solution).

Coordinates: `x` grows to the east, `y` grows to the **north**, `(0,0)` is the bottom-left square.

```json
{
    "id": "first-lesson",              // unique and stable; stars are saved under it
    "name": "First Lesson",
    "width": 5, "height": 4,
    "days": 2,                          // days the player gets
    "ticksPerDay": 24,
    "animals": [
        { "color": "red",  "x": 0, "y": 0, "facing": "north", "behaviours": ["move"] },
        { "color": "blue", "x": 4, "y": 2, "facing": "west",  "behaviours": ["sing"] }
    ],
    "flags":   [ { "color": "red", "x": 3, "y": 2 } ],
    "blocked": [ { "x": 2, "y": 0 } ],  // optional obstacles
    "items":   [ { "type": "goodSweet", "count": 1 }, { "type": "badSweet", "count": 0 } ],
    "stars": [
        { "type": "solve" },                        // always present, added automatically if missing
        { "type": "maxTicks", "value": 11 },        // total ticks over all days
        { "type": "maxItems", "value": 1 },         // items placed over all days
        { "type": "maxDays",  "value": 1, "description": "optional custom text" }
    ],
    "solution": [ { "day": 1, "item": "goodSweet", "x": 0, "y": 2 } ]   // optional, used by the test
}
```

Colours: red, blue, green, yellow, purple, orange, pink, white. Facing: north, east, south, west.

## Behaviours

| Text                     | Meaning |
|--------------------------|---------|
| `move`                   | Walks one square forward per tick. |
| `sing`                   | Sings every tick; the song covers every square in front of it up to the edge (blocked squares stop it). |
| `hear <colour> good/bad` | When it hears that colour: good = turn towards the singer and walk to it, bad = turn away. |
| `see <target> good/bad`  | When the first thing straight ahead is the target: good = keep walking, bad = turn right (90°). Target: a colour, `goodSweet`, `badSweet` or `flag <colour>`. |

Learned behaviours are added the same way during play (eating a sweet while hearing/seeing something).

## Rules of a tick

* Each tick every animal does one thing: eat, turn 90°, move one square, or nothing.
* An animal standing on a sweet eats it. It learns about everything it hears and sees in that tick;
  good sweet = "... is good", bad sweet = "... is bad". New rules only work from the next day.
* Learned rules are checked newest first and beat the basic behaviours.
* An animal waits if the square ahead is taken or blocked. It leaves the day when it walks off the map.
* An animal that reaches a flag of its colour stops there. All flags reached = puzzle solved.
* A day ends when the goal is reached, after `ticksPerDay` ticks, or when every walking animal is off the map or on its flag.
* Next day: animals start at their start squares again; uneaten sweets stay where they are.
