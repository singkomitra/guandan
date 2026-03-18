# Card Submission — Implementation Specification
**Issue #35 | Approach 3 + Option B with staging bar**

---

## Chosen Design

| Dimension | Decision |
|---|---|
| Selection | Click-to-toggle. Cards lift +50px with a blue/white ring on selection. |
| Submission (primary) | Drag any selected card toward the table — all selected cards follow as a ghost bundle. Drop on the table zone commits. |
| Submission (secondary) | "Play Set" button in the staging bar. Always enabled for content reasons; disabled only when it is not the player's turn. |
| Validation timing | **On submit only.** During selection the staging bar shows neutral identification ("3 cards · Pair of 5s"). No red/green judgment. On submit attempt, if invalid, staging bar flashes with specific reason. |
| Error gate | System constraints (not your turn) are hard gates. Content validity is the player's decision until they commit. |

---

## Interaction Flow

### Happy path
1. Player clicks cards in hand to select. Each selected card lifts +50px and shows a blue/white ring.
2. Staging bar appears above the hand showing: card count + neutral set identification ("3 cards · Pair of 5s"). No valid/invalid label.
3. Player drags any selected card toward the table. All selected cards animate together as a ghost bundle following the cursor.
4. Player drops on the table zone. `SelectionManager.Commit()` fires.
5. `SetValidator` runs. If valid: cards are removed from hand, placed on the table pile, selection clears, staging bar clears.
6. Turn advances (future).

### Submit with invalid set
1–4. Same as above.
5. `SetValidator` runs. Invalid: cards stay in hand, selection stays, staging bar flashes red + shows specific reason ("must play a Pair" / "ranks are not consecutive").
6. Player adjusts selection and retries.

### Cancel / clear
- Click a selected card → deselects that card only; returns to its position in hand.
- Click "Clear" in staging bar → all selected cards return to hand; staging bar clears.
- Drag selected bundle back to hand → `SelectionManager.Clear()`.

### Pass
- Player clicks "Pass" in staging bar → selection clears, turn passes (future).

### Not your turn
- Hand cards are non-interactive (pointer events blocked).
- Staging bar shows "Play Set" and "Pass" as disabled.
- "Clear" remains enabled if cards are staged.

---

## Visual States

### Hand cards

| State | Y Offset | Border | Notes |
|---|---|---|---|
| Default | 0 | None | At rest |
| Hovered | +30px | Yellow glow | CardHover (existing) |
| Selected | +50px | Blue/white ring | Overrides hover |
| Selected + Hovered | +50px | Blue/white ring | Selected wins |
| Dragging (group) | — | — | Card hidden; represented in ghost bundle |

### Staging bar

| State | Appearance | Content |
|---|---|---|
| Empty (no selection) | Hidden or collapsed | — |
| Has selection | Visible, neutral | Card count · set identification |
| Submit error | Flash red, then neutral | Specific fail reason for ~2s, then returns to neutral |
| Not your turn | Visible, muted | Play + Pass buttons disabled |

### Ghost bundle (drag)

- All selected cards rendered together, slightly fanned, following cursor at 80% opacity.
- Original card positions in hand show a faint outline placeholder (so the player can see the gap their play creates).
- Bundle snaps back to hand with an ease-out animation if dropped outside a valid zone.

---

## Architecture

### Boundaries

```
UI Layer        CardSelectable, StagingBar, GroupDragHandler
    ↓ events only
Coordinator     SelectionManager
    ↓ pure call
Logic Layer     SetValidator
```

No upward dependencies. Logic layer has zero Unity references. Coordinator has no UI references.

---

### Components

**`SetValidator`** — Pure C# class, no Unity dependencies.
- `Validate(List<CardId>, SetType? requiredType, CardSet? mustBeat) → ValidationResult`
- `ValidationResult`: `IsValid`, `SetType` (enum), `Description` ("Pair of 5s"), `FailReason` ("ranks must be consecutive")
- `requiredType` and `mustBeat` are nullable — null means first player in a round, no constraint
- Called only on commit attempt, never during selection
- All Guandan set and bomb rules live here exclusively; unit-testable with plain NUnit

**`SelectionManager`** — Plain C# class, not a MonoBehaviour.
- Owns `List<CardId> _staged`
- Does NOT call `SetValidator` on `Toggle` — only on `Commit`
- Events: `SelectionChanged(staged)`, `SelectionCommitted(staged)`, `SelectionCleared`, `SelectionFailed(ValidationResult)`
- Methods: `Toggle(CardId)`, `Commit()`, `Clear()`, `Pass()`
- `Commit()`: calls `SetValidator.Validate`; if valid fires `SelectionCommitted`; if invalid fires `SelectionFailed` — selection state is unchanged on failure
- `SelectionChanged` carries only the staged list, no validity — neutral by design

**`CardSelectable`** — MonoBehaviour, one per hand card.
- `OnPointerClick` → `SelectionManager.Toggle(id)`
- Subscribes to `SelectionManager.SelectionChanged` → updates visual state (Y offset, ring)
- Blocked when `!isPlayerTurn` — a single flag, not scattered conditionals
- Composes with `CardHover` (existing) and `CardDrag` (existing); one responsibility each

**`StagingBar`** — MonoBehaviour, persistent UI panel above the hand.
- Subscribes to `SelectionManager.SelectionChanged` → updates count + set identification (calls `SetValidator.Describe(staged)` — a read-only identification call, not a full validation)
- Subscribes to `SelectionManager.SelectionFailed` → triggers error flash + shows `FailReason` for ~2s
- Subscribes to `SelectionManager.SelectionCleared` → hides or collapses
- "Play Set": calls `SelectionManager.Commit()` — disabled only when `!isPlayerTurn`
- "Pass": calls `SelectionManager.Pass()` — disabled only when `!isPlayerTurn`
- "Clear": calls `SelectionManager.Clear()` — always enabled when selection is non-empty
- Never shows valid/invalid state during selection — only error state on `SelectionFailed`

**`GroupDragHandler`** — MonoBehaviour, primary submission path.
- Subscribes to `CardDrag.OnDragBegin` on each hand card
- When drag begins on a selected card: suppresses the single-card drag; initiates group drag instead
- When drag begins on an unselected card: normal single-card drag (rearrange hand)
- Renders ghost bundle: all selected cards fanned, 80% opacity, follows cursor
- Shows faint placeholder outlines at original hand positions during drag
- `OnDrop` on table zone → `SelectionManager.Commit()`
- `OnDrop` on hand → `SelectionManager.Clear()`
- `OnDrop` on individual hand card → `SelectionManager.Toggle(that card's id)`
- `OnDrop` outside any zone → snap bundle back to hand; `SelectionManager` unchanged

**`TurnManager`** _(future)_
- On turn change: `SelectionManager.Clear()`, inject new `GameContext` (requiredType, mustBeat)
- On round end: broadcasts `RoundEnded`; all `SelectionManager` instances clear

---

### Data Flow

```
OnPointerClick (CardSelectable)
  → SelectionManager.Toggle(id)
  → SelectionChanged(staged) fires           [no validation — neutral]
      → CardSelectable instances: update Y offset + ring
      → StagingBar: update count + set identification

OnDrop table zone (GroupDragHandler) / OnClick "Play Set" (StagingBar)
  → SelectionManager.Commit()
  → SetValidator.Validate(staged, context?)  [only called here]
      → Valid:
          SelectionCommitted(staged) fires
          HandManager: remove cards, add to table pile
          SelectionManager: reset → SelectionChanged([]) fires
          StagingBar: clears
      → Invalid:
          SelectionFailed(result) fires
          StagingBar: flash red + show FailReason for ~2s
          Selection unchanged — player adjusts and retries

OnDrop hand / "Clear" / "Pass" (GroupDragHandler / StagingBar)
  → SelectionManager.Clear() or .Pass()
  → SelectionCleared fires
      → CardSelectable instances: reset visual state
      → StagingBar: collapses
```

---

### Invariants

- `SetValidator` is never called during selection — only on commit. No mid-selection judgment.
- `SelectionManager.Commit()` is the single code path to `SelectionCommitted`. Both GroupDragHandler and StagingBar call it — neither bypasses the validator.
- On `SelectionFailed`, selection state is unchanged. The player retains their staged cards.
- Visual state on every card is derived from `SelectionManager` events only — never set imperatively.
- The table pile is write-only. Played cards are permanently `raycastTarget = false`.
- `StagingBar` never reads `ValidationResult.IsValid` during selection — only during a `SelectionFailed` event.

---

### MVP Scope

| Component | In scope |
|---|---|
| `SetValidator` | `Validate` + `Describe` — all set types from `Guandan.md` + 4-bomb |
| `SelectionManager` | `Toggle`, `Commit`, `Clear`, `Pass`; all four events |
| `CardSelectable` | Click-to-toggle; Y offset + ring visual states; turn gate |
| `StagingBar` | Count + identification label; error flash on `SelectionFailed`; Play / Pass / Clear buttons |
| `GroupDragHandler` | Group drag initiation; ghost bundle rendering; all four drop outcomes |
| `TurnManager` | Out of scope — `GameContext` injected as null (no constraint) for now |
| Beat-previous validation | Out of scope — `mustBeat` wired up when `TurnManager` ships |

---

### Open Questions

| # | Question | Impact |
|---|---|---|
| 1 | How long does the error flash persist? (~2s, or until next selection change?) | StagingBar animation spec |
| 2 | Does dragging an unselected card while others are selected deselect them first, or start a normal single-card drag? | GroupDragHandler logic |
| 3 | Can a player select cards when it is not their turn (to pre-plan) but not submit? | Turn gate scope — CardSelectable vs SelectionManager |
