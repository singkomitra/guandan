# Card Submission — Design Document
**Issue #35 | Status: Design Review**

---

## Problem

Players need a way to select one or more cards from their hand and submit them as a set. Guandan sets range from 1 to 8 cards and must conform to strict type and rank rules. The interaction must be fast, unambiguous, and prevent illegal plays before they happen.

---

## Goals

- Players can select multiple cards and submit them as a single action.
- Invalid selections are surfaced immediately with a specific reason — not after submission.
- The flow is fully reversible before commit. Irreversible after.
- Architecture supports future additions (turn enforcement, beat-previous, bombs, network sync) without rework.

---

## Interaction Requirements

These are evaluated against each option below.

| # | Requirement | Notes |
|---|---|---|
| R1 | Multi-card selection | Sets range from 1–8 cards |
| R2 | Validity feedback on submit | Shown on attempt, not during selection — player owns the thinking process |
| R3 | Type lock context | After first player plays, all others must match that set type |
| R4 | Beat-previous context | Your set must be same type and higher rank, or a bomb |
| R5 | Reversibility before commit | Deselect individual cards; clear entire selection |
| R6 | Irreversibility after commit | No take-backs once played |
| R7 | Turn awareness | Selection and submission blocked when not your turn |
| R8 | Pass action | Player can pass instead of playing |
| R9 | Bomb override | Bombs can always be played regardless of current set type |
| R10 | Error prevention | System prevents illegal plays — no error recovery after the fact |

---

## Selection Reset Triggers

Before evaluating options, these are all the scenarios that must clear or modify selection state:

| Trigger | Expected Behavior |
|---|---|
| Player clicks a selected card | Deselect that card only; return it to its hand position |
| Player clicks "Clear" | Deselect all; all cards return to hand |
| Player clicks "Pass" | Deselect all; turn passes |
| Player commits a play | Selection clears after cards are removed from hand |
| Round ends (trick won) | All selections across all players clear |
| Turn changes to another player | Local selection clears |
| Player attempts invalid commit | Selection stays; error reason displayed — no state change |

These are not edge cases. They are first-class events the architecture must handle explicitly.

---

## Validation Timing

When feedback is shown is as important as what feedback is shown. This is an independent design decision from which submission path is used.

### Approach 1 — Real-Time Validation (Proactive)
Play button is disabled when selection is invalid. Reason shown continuously as cards are selected.

| | |
|---|---|
| **Pros** | No post-submit surprises. Teaches rules passively over time. |
| **Cons** | Player sees "invalid" 3–4 times while still assembling their play. Interrupts the cognitive process of set-building — a core Guandan skill. Feels like the game is thinking for you. |

### Approach 2 — Submit-Time Validation (Reactive)
Play button is always enabled. Validation runs on click; specific error shown if invalid.

| | |
|---|---|
| **Pros** | Full player agency during selection. Errors are contextual — player just decided to play, so they're receptive to feedback. Teaches the rules at the moment of intent. |
| **Cons** | Player can build a large selection only to be rejected. Error recovery requires re-evaluating the full selection. |

### Approach 3 — Informative Selection + Submit Validation (Recommended)
During selection: neutral identification only — *"3 cards · Pair of 5s"*. No judgment. On submit: if invalid, staging bar flashes with specific reason. Play button is always enabled for content reasons; disabled only for system reasons (not your turn).

| | |
|---|---|
| **Pros** | Mirrors how physical card games feel — pick up cards, think, then attempt. Reduces noise during set-building. Error is surfaced at the exact moment of intent, which is when the player is most receptive. Both new and experienced players are served: new players learn from specific errors, experienced players aren't interrupted. |
| **Cons** | Player may occasionally attempt a play they could have been warned about earlier. Requires clear visual distinction between "identifying" (neutral) and "error" (failed submit) states. |

**Key distinction:** Turn awareness remains a hard gate — Play is disabled when it's not your turn. That is a system constraint, not a content decision, and should never be attempted. Content validity (is this a legal set?) is the player's domain until they commit.

---

## Error Scenarios

| Scenario | When surfaced | Expected Behavior |
|---|---|---|
| Selection is not a valid set type | On submit | Staging bar flashes red, reason shown ("not a valid set type") |
| Selection doesn't beat current table play | On submit | Staging bar flashes red ("must beat Pair of 5s") |
| Selection violates type lock | On submit | Staging bar flashes red ("must play a Pair") |
| Bomb weaker than current bomb | On submit | Staging bar flashes red ("must beat 5-bomb of Aces") |
| Player tries to play out of turn | Immediately (hard gate) | Play and Pass buttons disabled; hand non-interactive |
| Invalid commit reaches backend (future) | On response | Error toast; selection restored; player retries |

The guiding principle: **system constraints are hard gates; content decisions are the player's to make.** The player owns what they play. The system owns whose turn it is.

---

## Interaction Options

### Option A — Click-to-Select + Staging Bar + Confirm Button

**Flow:** Click cards to toggle selection (selected cards lift +50px, distinct ring). A persistent staging bar above the hand shows staged cards, live validity, "Play Set", "Pass", and "Clear" controls.

**Requirements assessment:**

| Req | Handled? | Notes |
|---|---|---|
| R1 Multi-card selection | ✅ | Click-to-toggle, no limit |
| R2 Validity on submit | ✅ | Staging bar shows neutral identification during selection; error on submit attempt |
| R3 Type lock context | ✅ | Staging bar shows "Must play: Pair" as persistent context |
| R4 Beat-previous context | ✅ | Staging bar shows "Must beat: Pair of 5s" |
| R5 Reversibility | ✅ | Click selected card to deselect; Clear button always present |
| R6 Irreversibility | ✅ | Button click is deliberate; no accidental commit |
| R7 Turn awareness | ✅ | Controls disabled; cards non-clickable when not your turn |
| R8 Pass | ✅ | "Pass" button lives naturally in staging bar |
| R9 Bomb override | ✅ | Validator detects bomb; staging bar overrides type lock message |
| R10 Error prevention | ✅ | Button disabled until valid; no post-commit recovery needed |

**Pros:** All requirements covered in one consistent UI surface. Staging bar is a natural home for context that future requirements (type lock, beat-previous) will need regardless of submission path. Accessible — works on mouse, touch, keyboard.

**Cons:** Confirmation step requires eye movement from hand to button. Slightly friction-y for expert players who know their set.

---

### Option B — Click-to-Select + Drag-to-Submit (No Staging Bar)

**Flow:** Click cards to select. Drag any selected card toward the table — all selected cards follow as a ghost bundle. Drop on table to submit. Drop back on hand to cancel.

**Requirements assessment:**

| Req | Handled? | Notes |
|---|---|---|
| R1 Multi-card selection | ✅ | Same click-to-toggle model |
| R2 Real-time validity | ⚠️ | Only visible during active drag via floating indicator — no persistent preview |
| R3 Type lock context | ❌ | No persistent UI surface to show "Must play: Pair" between turns |
| R4 Beat-previous context | ❌ | No persistent surface; only possible during drag gesture |
| R5 Reversibility | ⚠️ | Drag back to hand cancels, but deselecting individual cards is ambiguous — does dropping one card on the hand deselect just that card or all? |
| R6 Irreversibility | ⚠️ | Dropping on an invalid target is unclear — bounce back? Silent ignore? Needs explicit error state |
| R7 Turn awareness | ✅ | Block drag start when not your turn |
| R8 Pass | ❌ | No natural gesture for "Pass". Requires a separate button anyway, which partially reintroduces Option A |
| R9 Bomb override | ⚠️ | Can't communicate "you can play a bomb here" without persistent context UI |
| R10 Error prevention | ❌ | Drop is the commit gesture — if the set is invalid at drop time, the player only learns after the gesture, not before |

**Verdict:** Option B alone fails R3, R4, R8, and R10. It requires a floating context indicator during drag and still needs a "Pass" button — meaning a partial staging bar is needed regardless. Option B cannot stand alone as a complete solution.

---

### Option C — Both Paths (Recommended)

**Flow:** Same as Option A (click-to-select + staging bar). Additionally, dragging any selected card initiates a group drag — all selected cards follow as a ghost bundle. Drop on table commits (same path as "Play Set" button). The staging bar remains visible during drag, so validity and context are always readable.

**Requirements assessment:** Inherits all ✅ from Option A. Drag path adds no new failure modes because the staging bar and SelectionManager are shared — the drag gesture is simply an alternative trigger to `SelectionManager.Commit()`.

| Req | Handled? | Notes |
|---|---|---|
| R1–R10 | ✅ | All via Option A's staging bar |
| Drag path errors | ✅ | Invalid drop → no commit (same gate as button); staging bar explains why |
| Partial deselect via drag | ✅ | Dropping one card back to hand deselects that card only; staging bar updates live |

**Pros:** Satisfies all requirements. Expert players get a fluid gesture. New players follow the button. Two paths, one model.

**Cons:** GroupDragHandler is additional implementation surface. Visual design must make clear that dragging a selected card moves the whole group — not just that one card.

**Recommended:** Ship Option A as MVP. Option B's drag path layers on as an additive enhancement with no changes to SelectionManager, SetValidator, or StagingBar.

---

## Option Comparison

| Criterion | A | B (standalone) | C |
|---|---|---|---|
| All requirements met | ✅ | ❌ | ✅ |
| Error prevention (R10) | ✅ | ❌ | ✅ |
| Type lock / beat-previous context (R3, R4) | ✅ | ❌ | ✅ |
| Pass action (R8) | ✅ | ❌ | ✅ |
| Ease of learning | High | Medium | High |
| Speed for experts | Medium | High | High |
| Accessibility | High | Low | High |
| Accidental play risk | Low | Medium | Low |
| Implementation complexity | Low | High | Medium (staged) |
| Extensibility | High | Low | High |

---

## Card Visual States

Selected overrides hovered — the player's committed intent takes priority over passive focus.

| State | Treatment |
|---|---|
| Default | At rest |
| Hovered | +30px lift, yellow glow |
| Selected | +50px lift, blue/white ring — overrides hover |
| Selected + Hovered | +50px lift, blue/white ring (selected wins) |
| Played | Flat on table pile, `raycastTarget = false`, permanently non-interactive |

---

## Architecture

### Boundaries

Strict one-way dependency rule. No layer may reference a layer above it.

```
UI Layer       CardSelectable, StagingBar, GroupDragHandler (future)
    ↓ events only — no direct calls upward
Coordinator    SelectionManager
    ↓ pure function call
Logic Layer    SetValidator
```

---

### Components

**`SetValidator`** — Pure C# class, no Unity dependencies.
- `Validate(List<CardId>, SetType? requiredType, CardSet? mustBeat) → ValidationResult`
- `ValidationResult`: `IsValid`, `SetType` (enum), `Description`, `FailReason`
- `requiredType` and `mustBeat` are nullable — null means no constraint (first player in a round)
- All Guandan set and bomb rules live here exclusively; unit-testable with plain NUnit
- Custom rules (`Guandan.md` feature) are additive to this class only

**`SelectionManager`** — Plain C# class, not a MonoBehaviour.
- Owns `List<CardId> _staged` and the last `ValidationResult`
- Accepts optional `GameContext` (required type, must-beat set) — injected by future `TurnManager`
- Events: `SelectionChanged(staged, result)`, `SelectionCommitted(staged)`, `SelectionCleared`
- Methods: `Toggle(CardId)`, `Commit()` (no-op if invalid or not player's turn), `Clear()`, `Pass()`
- `Commit()` is the single path to `SelectionCommitted` — both UI paths (button and drag) call it
- Not a MonoBehaviour: no GameObject lifecycle dependency; straightforward to serialize for network

**`CardSelectable`** — MonoBehaviour, one per hand card.
- `OnPointerClick` → `SelectionManager.Toggle(id)`
- Subscribes to `SelectionManager.SelectionChanged` → drives visual state (Y offset, ring)
- Blocked (pointer events disabled) when `!isPlayerTurn` — no separate conditional in each handler
- Composes with `CardHover` and `CardDrag`; each component has one responsibility

**`StagingBar`** — MonoBehaviour, persistent UI panel above the hand.
- Subscribes to `SelectionManager.SelectionChanged`
- Renders: staged card thumbnails, count badge, neutral set identification ("Pair of 5s" / "3 cards"), context label ("Must play: Pair" / "Must beat: Pair of 5s")
- On failed submit: transitions to error state — flashes red, shows `FailReason`; clears back to neutral after a short duration or on next selection change
- "Play Set": calls `SelectionManager.Commit()` — enabled only when `IsValid`
- "Pass": calls `SelectionManager.Pass()` — always enabled on your turn
- "Clear": calls `SelectionManager.Clear()` — always enabled when selection is non-empty
- Only component that reads `ValidationResult` — validity display is not scattered

**`GroupDragHandler`** _(future — Option C drag path)_
- `OnBeginDrag` on any selected card → renders ghost bundle of all selected cards; hides originals
- `OnDrop` on table zone → `SelectionManager.Commit()` (same gate as button)
- `OnDrop` on hand → `SelectionManager.Clear()` (explicit cancel gesture)
- `OnDrop` on individual hand card → `SelectionManager.Toggle(droppedCard.Id)` (partial deselect)
- No changes to `SelectionManager`, `SetValidator`, or `StagingBar` required

**`TurnManager`** _(future)_
- Owns `GameContext` (whose turn, required set type, must-beat set)
- On turn change: `SelectionManager.Clear()`, then injects new `GameContext`
- On round end: broadcasts `RoundEnded` event; all `SelectionManager` instances clear

---

### Data Flow

```
OnPointerClick (CardSelectable)
  → SelectionManager.Toggle(id)
  → SetValidator.Validate(staged, context.requiredType, context.mustBeat)
  → SelectionChanged fires
      → CardSelectable instances: update Y offset + ring
      → StagingBar: update thumbnails, validity label, button states

OnClick "Play Set" / OnDrop table (StagingBar / GroupDragHandler)
  → SelectionManager.Commit()              [no-op if !IsValid]
  → SelectionCommitted fires
      → HandManager: remove cards, add to table pile
      → TurnManager (future): advance turn, broadcast new GameContext
      → SelectionManager: reset; SelectionChanged fires (empty)

OnClick "Pass" (StagingBar)
  → SelectionManager.Pass()
  → SelectionCleared fires
      → TurnManager (future): advance turn

TurnManager.TurnChanged (future)
  → SelectionManager.Clear()
  → SelectionManager.SetContext(newGameContext)
  → SelectionChanged fires → StagingBar updates context label
```

---

### Invariants

- `SelectionManager.Commit()` is always gated by `IsValid`. No UI path bypasses this.
- `SetValidator` is the single source of truth for all rules. No validation logic exists outside it.
- Visual state on every card is derived from `SelectionManager` events — never set imperatively.
- The table pile is write-only. Played cards are permanently `raycastTarget = false`.
- `SelectionManager` never clears itself — only explicit calls (`Commit`, `Clear`, `Pass`) or external events (`TurnChanged`, `RoundEnded`) trigger a reset.

---

### MVP Scope

| Component | In scope |
|---|---|
| `SetValidator` | All sets from `Guandan.md` (single, pair, triple, full house, straight, consecutive triples, triple consecutive pairs) + 4-bomb |
| `SelectionManager` | Toggle, Commit, Clear, Pass; `SelectionChanged` + `SelectionCommitted` + `SelectionCleared` |
| `CardSelectable` | Click-to-toggle; visual state (Y offset + ring); disabled when not your turn |
| `StagingBar` | Thumbnails, validity label, fail reason, Play/Pass/Clear buttons |
| `GroupDragHandler` | Out of scope — follow-on |
| `TurnManager` | Out of scope — `GameContext` injected manually for now |
| Beat-previous validation | Out of scope — `mustBeat` param wired up when `TurnManager` ships |
