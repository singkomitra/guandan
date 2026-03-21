# Dragging System Design

## Architecture Overview

No `HorizontalLayoutGroup`. **HandManager owns all card positions** as data and animates them via per-card lerp coroutines. `CardHover` is visual-only (glow, render priority). Y offsets for hover/selection are part of HandManager's layout target calculation.

---

## Components and Responsibilities

| Component | Type | Role |
|---|---|---|
| `CardDrag` | MonoBehaviour (per-card) | Unity drag input (`IBeginDragHandler / IDragHandler / IEndDragHandler`). Reparents card to root canvas on drag. Fires static events (`AnyDragBegin`, `AnyDragEnd`, `AnyDragMoved`). No placeholder logic. |
| `GroupDragHandler` | MonoBehaviour (scene) | Moves staged follower cards in sync with the primary (fan/stack offsets). On drag end, hands followers back to `HandManager.ReturnCards`. No placeholder, no return coroutine. |
| `CardHover` | MonoBehaviour (per-card) | Yellow glow outline + `overrideSorting` canvas for render priority. Fires `static event AnyHoverChanged(CardId, bool)`. No Y-axis logic. |
| `HandManager` | MonoBehaviour (scene) | Owns `_order`, `_dragging`, `_hovered`, `_insertionHint`. Computes slot positions. Animates all cards via `LerpCard` coroutines. Owns Y targets. |
| `SelectionManager` | Plain C# singleton | Staged card list. Fires `SelectionChanged`, `SelectionCommitted`, `SelectionFailed`. |

---

## HandManager Layout

### Data

- `_order` — `List<CardId>`: logical order of cards in hand. Never mutated during drag.
- `_dragging` — `HashSet<CardId>`: cards currently on root canvas. Excluded from layout.
- `_hovered` — `HashSet<CardId>`: cards the pointer is over. Drives Y target.
- `_insertionHint` — `int`: visible-list slot index showing where a dragged card would land; creates a gap in the layout. `-1` = none.
- `_cardRects` — `Dictionary<CardId, RectTransform>`: live lookup.

### Position Computation

```
ComputeSlotX(slotIndex, totalSlots):
  spacing = clamp(_spacing, fitSpacing, minSpacing)  // never overflow container
  step     = cardWidth + spacing
  startX   = -(cardWidth + step * (totalSlots-1)) / 2 + cardWidth / 2
  return startX + slotIndex * step

GetYTarget(id):
  if selected → _selectedYOffset (50)
  if hovered  → _hoverYOffset    (30)
  else        → 0

RefreshLayout(speed):
  visible = _order excluding _dragging
  slots   = visible.Count + (insertionHint >= 0 ? 1 : 0)
  for each visible card:
    if slot == insertionHint: skip slot (gap)
    AnimateCard(id, (slotX, yTarget), speed)
```

### Animation

`AnimateCard` stops any running coroutine for that card and starts a new `LerpCard` coroutine. `LerpCard` lerps both `anchoredPosition` and `localRotation` toward target each frame; snaps when within 0.5 units.

**Critical**: `OnAnyDragBegin` stops all running coroutines for dragged cards before handing position control to `CardDrag`. Without this, `LerpCard` fights `CardDrag.OnDrag`'s `anchoredPosition +=` and causes Y drift across multiple drags.

### `ReparentToHand`

After `SetParent(_handView, worldPositionStays: true)`, sibling index is set by counting how many `_order`-earlier cards are already children of `_handView`. Without this, `SetParent` appends at the end (highest sibling = renders on top of all hand cards).

---

## `HandManager.HandViewRT`

Static `RectTransform` set in `Awake`. `CardDrag` reads it to detect `IsFromHand` (`_startParent == HandManager.HandViewRT`) without any HLG component dependency.

---

## Single Card Drag (unselected)

1. `CardDrag.OnBeginDrag`: reparents card to root canvas, fires `AnyDragBegin`.
2. `HandManager.OnAnyDragBegin`: adds primary to `_dragging`, stops its coroutine, calls `RefreshLayout`.
3. `GroupDragHandler.OnAnyDragBegin`: `staged.Count <= 1` → returns immediately.
4. `CardDrag.OnDrag`: moves card; fires `AnyDragMoved` → `HandManager.OnAnyDragMoved` → updates `_insertionHint` when pointer is over hand.
5. **Success**: drop zone calls `NotifyDropHandled()` + `Commit()`. `HandManager.OnSelectionCommitted` removes card from `_order` and `_cardRects`.
6. **Failure**: `HandManager.OnAnyDragEnd` sees `!WasDropHandled` → `ReturnCard(id)` → `ReparentToHand` + `RefreshLayout(_returnSpeed)`.
7. **Hand-drop (reorder)**: `HandDropZone` calls `NotifyDropHandled()` + fires `CardReturned(drag)`. `HandManager.OnCardReturnedToHand` reorders `_order` at `_insertionHint`, reparents, `RefreshLayout`.

---

## Single Selected Card Drag

Same as unselected, except:
- `HandManager.OnAnyDragBegin`: primary is in staged → adds primary to `_dragging` (same result).
- `GroupDragHandler.OnAnyDragBegin`: `staged.Count == 1` → returns immediately. No followers.

---

## Group Drag (staged.Count > 1)

Primary may be selected OR unselected.

### Drag Begin

**`CardDrag.OnBeginDrag`** (fires first):
- Reparents primary to root canvas.
- Fires `AnyDragBegin`.

**`GroupDragHandler.OnAnyDragBegin`** (subscribed in `OnEnable`, fires before HandManager):
- For each staged card that is not the primary: disable `blocksRaycasts`, reparent to root canvas, `SetAsLastSibling`. Adds to `_followers`.
- `_isGroupDragging = true` if any followers exist.

**`HandManager.OnAnyDragBegin`** (subscribed in `Start`, fires after GroupDragHandler):
- Adds primary to `_dragging`.
- Adds ALL staged cards to `_dragging` (HashSet — duplicates are no-ops).
  - This is critical: followers were moved to root canvas by GroupDragHandler before HandManager fires. If followers are not in `_dragging`, HandManager runs coroutines on them while they're on root canvas (wrong `anchoredPosition` space) and `ReturnCards` would skip them.
- Stops coroutines for all dragged cards.
- `RefreshLayout(_layoutSpeed)` — excludes all dragging cards.

### During Drag (GroupDragHandler.LateUpdate)

For each follower[i]: lerp `position` and `localRotation` toward fan offset (pointer in hand) or stack offset (pointer outside hand) from primary. Primary is driven by `CardDrag.OnDrag`.

### Drop Success

1. `SelectionManager.Commit()` → `SelectionCommitted` → `HandManager.OnSelectionCommitted`: removes all committed cards from `_order`, `_cardRects`, `_dragging`. Plays card visual.
2. `GroupDragHandler.OnAnyDragEnd`: sees `WasDropHandled && Staged.Count == 0` → clears followers.
3. `HandManager.OnAnyDragEnd`: sees `WasDropHandled` → no-op.

### Drop Failure

Event order (synchronous, same frame):

1. **`GroupDragHandler.OnAnyDragEnd`** (fires first):
   - Restores `blocksRaycasts = true`, `raycastTarget = true` on followers.
   - Calls `HandManager.ReturnCards(followerIds)`.
   - `ReturnCards`: removes followers from `_dragging`, calls `ReparentToHand` for each (SetParent + correct sibling index), calls `RefreshLayout(_returnSpeed)`.

2. **`HandManager.OnAnyDragEnd`** (fires second):
   - `!WasDropHandled` → `ReturnCard(primaryId)`.
   - `ReturnCard`: removes primary from `_dragging`, `ReparentToHand`, `RefreshLayout(_returnSpeed)`.

Cards animate from their drop-point world position (converted to handView local via `worldPositionStays: true`) down to their layout target via `LerpCard`. Sibling index is correct, so render order is correct throughout the animation.

---

## Event Order (CardDrag.OnBeginDrag / OnEndDrag)

```
OnBeginDrag:
  1. _startParent, _startSiblingIndex, _droppedOnZone = false
  2. CanvasGroup.blocksRaycasts = false
  3. _wasInHand = (_startParent == HandManager.HandViewRT)
  4. SetParent(rootCanvas), SetAsLastSibling
  5. OnDragBegin?.Invoke  (instance — unused currently)
  6. AnyDragBegin?.Invoke → GroupDragHandler, then HandManager
     (events fired AFTER reparenting so CardRect.position is correct)

OnEndDrag:
  1. CanvasGroup.blocksRaycasts = true
  2. OnDragEnd?.Invoke
  3. AnyDragEnd?.Invoke → GroupDragHandler (first), then HandManager (second)
  4. _droppedOnZone = false

Drop zone fires before OnEndDrag (Unity event order):
  - TableDropZone: Commit() → SelectionCommitted → HandManager.OnSelectionCommitted
                   then NotifyDropHandled() if commit succeeded
  - HandDropZone:  NotifyDropHandled() → CardReturned(drag) → HandManager.OnCardReturnedToHand
```

---

## Key Design Decisions

- **No HLG, manual positions**: HLG constantly reasserts `anchoredPosition` during its layout phase, conflicting with any manual Y writes (hover lift, return animation). Dropping HLG eliminates all `ignoreLayout`, `SuppressYOffset`, `SnapCurrentY`, and placeholder complexity.
- **HandManager owns Y**: Previously CardHover wrote Y via `Canvas.willRenderCanvases`, GroupDragHandler also wrote Y during return animation, requiring `SuppressYOffset` to coordinate. Now HandManager is the single writer — no conflict possible.
- **Stop coroutines on drag begin**: `LerpCard` must be stopped before the card is reparented to root canvas. If it keeps running, it writes `anchoredPosition` in root canvas local space while `CardDrag.OnDrag` also writes it, causing position drift that compounds across drag cycles.
- **`ReparentToHand` sets sibling index**: `SetParent` without `SetSiblingIndex` appends the card at the end of `_handView`'s children (highest sibling = renders on top of everything). Sibling index must be set from `_order` to get correct render order.
- **All staged cards go in `_dragging`**: GroupDragHandler moves followers to root canvas before HandManager fires (OnEnable < Start subscription order). HandManager must mark followers as dragging regardless of whether the primary is selected — otherwise layout runs coroutines on cards in root canvas space, and `ReturnCards` skips them (`.Remove` returns false).
- **`_insertionHint` is a visible-list index**: computed by iterating visible cards in order and counting, not `_order.IndexOf` (which includes dragging cards and gives the wrong slot number).
- **Per-card + scene coordinator pattern**: CardDrag must be per-card (Unity `IDragHandler` interface). GroupDragHandler is a scene-level coordinator on static events. Standard Unity card game architecture.
