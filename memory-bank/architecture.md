# Guandan — Game Logic Component Architecture

## Lifecycle hierarchy

Each layer owns the state relevant to its duration and is responsible for starting/stopping the layer below it.

```
GameManager          — full game session (scores, teams, trump rank)
└── DealManager      — one deal (shuffle, deal, tax, placements)
    └── TrickManager  — one trick (controlling set, required type)
        └── TurnManager  — one turn (active player, pass counting)
```

---

## Ownership table

| Component                       | Owns                                                                        | Does NOT own                                        | Started by                                                     |
| ------------------------------- | --------------------------------------------------------------------------- | --------------------------------------------------- | -------------------------------------------------------------- |
| `GameManager`                   | Team assignments, scores, trump rank, win condition                         | How cards are dealt, trick state, turn order        | Scene load                                                     |
| `DealManager`                   | Deck (shuffle + deal), first-trick leader, deal-end detection, tax exchange | Trick state, turn order, scores                     | `GameManager`                                                  |
| `TrickManager`                  | `ControllingSet`, `RequiredType`, trump rank (for now)                      | Whose turn it is, pass counts, player identities    | `DealManager` (first trick); `TurnManager` (subsequent tricks) |
| `TurnManager`                   | Active player, player order, pass count for current trick                   | What is on the table, validation rules, card layout | `TrickManager` (implicitly, as a sub-concern)                  |
| `SetValidator`                  | All set recognition and comparison rules                                    | Any state — pure static logic                       | Never instantiated                                             |
| `SelectionManager`              | Staged card list, current `GameContext`                                     | Game state, table state, whose turn it is           | Never instantiated (static singleton)                          |
| `TableDropZone`                 | Nothing — pure UI input handler                                             | Validation, game state, card layout                 | Scene                                                          |
| `HandManager`                   | Physical layout of cards in the hand                                        | Which cards are staged, validation, game rules      | Scene                                                          |
| `CardSelectable`                | Selected/unselected visual state of one card                                | Selection logic                                     | Scene (one per card)                                           |
| `CardDrag` / `GroupDragHandler` | Drag interaction for one card or a group                                    | Selection state, validation, hand layout            | Scene (one per card)                                           |

---

## Who calls what

| Caller             | Calls                             | When                                                |
| ------------------ | --------------------------------- | --------------------------------------------------- |
| `DealManager`      | `TrickManager.StartTrick(leader)` | First trick of a deal                               |
| `TurnManager`      | `TrickManager.StartTrick(leader)` | All other players passed — trick over               |
| `TrickManager`     | `SelectionManager.SetContext()`   | On every state change (play committed, trick reset) |
| `TableDropZone`    | `SelectionManager.Commit()`       | Card dropped onto the table                         |
| `CardSelectable`   | `SelectionManager.Toggle()`       | Card clicked                                        |
| `SelectionManager` | `SetValidator.Validate()`         | Inside `Commit()` only                              |

---

## Event flow

| Event                               | Fired by           | Heard by                            |
| ----------------------------------- | ------------------ | ----------------------------------- |
| `SelectionCommitted(cards, result)` | `SelectionManager` | `TrickManager`, `HandManager`       |
| `CommitFailed(result)`              | `SelectionManager` | `HandManager`, UI feedback          |
| `SelectionChanged(staged)`          | `SelectionManager` | `CardSelectable` (highlight update) |
| `SelectionCleared`                  | `SelectionManager` | `HandManager`                       |

---

## Dependency rules

- `SetValidator` has zero Unity dependencies and zero state. Only `SelectionManager` should call it.
- Nothing below `TrickManager` knows about `TrickManager` directly — they communicate through `SelectionManager` context and events.
- `TurnManager` gates **who** can play; `TrickManager` enforces **what** they can play. Separate concerns.
- UI components call into managers; managers never call back into UI — they fire events instead.

---

## Current stubs

| Stub                                                     | File                  | Remove when                                 |
| -------------------------------------------------------- | --------------------- | ------------------------------------------- |
| `TrickManager.Start()` auto-starts a trick on scene load | `TrickManager.cs`     | `DealManager` is implemented                |
| `_trumpRank` serialized field on `TrickManager`          | `TrickManager.cs`     | `GameManager` passes trump rank dynamically |
| `SelectionManager.IsPlayerTurn = true` always            | `SelectionManager.cs` | `TurnManager` sets it per turn              |
