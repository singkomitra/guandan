# Guandan

## What is Guandan?

Guandan is a Chinese game that amongst the founders is very addicting. We play this often using real cards, but want to have the ability to play on the go and across large distances. There isn't a formal application for this game and is a gap we intend to fill.

---

## Setup

Guandan can be played with 4 or 6 people using two standard 52 decks with 4 jokers (two black and two red). Players are split into teams according to who you sit across from. This game is most often played in a circle so the person on your team is the one sitting across from you. Player setup should mean that the teams alternate clockwise as you go around.

Cards will total 108 and are aggregated together, then distributed evenly across all individuals. A random person is selected to start first **only** in the first round. Subsequent rounds have the winner starting last (play starts to the left of the winner).

Players may not look at each other's cards but may discuss anything by word of mouth. All players may hear however.

---

## Card Hierarchy

In all play, this is the hierarchy for cards irrespective of the set (highest to lowest):

| Rank | Card Type | Notes |
|------|-----------|-------|
| 1 | Red Joker | |
| 2 | Black Joker | |
| 3 | Trump Card | This is one of the rank cards |
| 4 | Ranked Cards (A, K, Q, J, 10, 9...) | Does NOT include the trump |

---

## Set List

These are the types of sets you can play during a turn. This does **not** include bombs. Sets are only decided by the first player in the turn order. Comparisons are only done within sets — sets cannot be played on top of a different type of set (e.g. a triple cannot be played on a pair).

| Set Type | Notes | Examples |
|----------|-------|---------|
| Straight | 5+ cards in increasing rank. Does NOT wrap from A→2 or 2→A. Comparison uses the starting number. | `2,3,4,5,6` / `9,10,J,Q,K` / `3,4,5,6,7 > 2,3,4,5,6` |
| Full House | A triple and a pair. Comparison uses only the triple. | `3,3,3,10,10` is beaten by `4,4,4,3,3` |
| Pair of Consecutive Triples | Two triples in consecutive order. Comparison uses the starting number. | `4,4,4,5,5,5 > 2,2,2,3,3,3` |
| Triple Consecutive Pairs | Three pairs in consecutive order. Comparison uses the starting number. | `3,3,4,4,5,5 > 2,2,3,3,4,4` |
| Triple | Three of a kind | `Q,Q,Q` |
| Pair | Two of a kind | `K,K` |
| Single | One card | `A` |

---

## Bomb Hierarchy

Bombs can be played on top of any set. A bomb can be played on top of another bomb only if it is greater in value or number of cards. Listed highest to lowest strength:

| Bomb Type | Notes |
|-----------|-------|
| Joker Bomb | All 4 jokers (2 red + 2 black). Must use ALL jokers — no subset. |
| 8-bomb | 8 cards of the same rank |
| 7-bomb | 7 cards of the same rank |
| 6-bomb | 6 cards of the same rank |
| Straight Flush | 5 consecutive cards of the same suit |
| 5-bomb | 5 cards of the same rank |
| 4-bomb | 4 cards of the same rank |

**Examples:**
- A 4-bomb of 3s can beat a pair of Aces
- A 5-bomb of 2s can beat a 4-bomb of Aces
- A 5-bomb of 4s can beat a 5-bomb of 2s

### Caveats

Jokers cannot be played as part of a set other than singles, pairs, or joker bomb. Pairs require both jokers to be the same color.

---

## Trump Card

The trump card starts at **2** at the beginning of every game and reflects the leading team's score. The trump rank is greater than every other rank card, placing it above the Ace.

Additionally, the **trump of hearts becomes a wild card** — it can substitute for any card except jokers.

**Example:** A player wants to create a straight flush using 9♦, 10♦, Q♦, K♦. They are missing J♦ but have 2♥. They can play: `9♦, 10♦, 2♥, Q♦, K♦`.

---

## Play

The first player selects any set from their hand. Once decided, the set type cannot change until a winner is decided for that round (e.g. if Player 1 plays singles, everyone in that round must play singles).

Everyone after the first player must play a card/set of the same type that is greater than the previous, or pass their turn.

At any point, a bomb can be played on any normal set. Once a bomb is played, only greater bombs are valid plays.

A round is won when all other players pass and play returns to the last player who played a card. Cards are moved aside and a new round begins.

---

## Winning the Game

A Guandan game consists of smaller rounds. Each round ends when all players exhaust their hands. Placements are recorded and points are awarded to teams.

The **leading team's score** determines the current trump card rank (e.g. a team at 11 points has Jack as trump).

Teams accumulate points until one team reaches **13 points** (Ace as trump). To win the game at 13, the winning team must finish **1st + 2nd** or **1st + 3rd** — finishing 1st + 4th does not win the game.

### Scoring

| Position Combo | Points Awarded |
|---------------|----------------|
| 1st & 2nd | 3 |
| 1st & 3rd | 2 |
| 1st & 4th | 1 |

**Example:**

| Game | Team 1 Players | Position | Team 2 Players | Position | Points to Team 1 |
|------|---------------|----------|---------------|----------|-----------------|
| A | Player 1, Player 2 | 1st, 2nd | Player 3, Player 4 | 3rd, 4th | 3 |
| B | Player 1, Player 2 | 1st, 3rd | Player 3, Player 4 | 2nd, 4th | 2 |

---

## Tax

After each round, the top two (1st and 2nd place) and bottom two (3rd and 4th place) exchange cards:

- 1st place receives the **best card** from the bottom two players
- 2nd place receives the **second best card** from the bottom two players
- If there is a discrepancy (same rank, different suit), 1st place may choose
- If 3rd and 4th hold a trump wild and other trump cards, they may choose which to give
- If the tax cards include a joker and the trump of hearts, **1st place must take the joker**

Cards are swapped face-up and everyone is aware of what was received.

---

## Strategy

This game has many layers and requires a good amount of card counting. It's important to know when to play certain sets and what sets to play when going first. Going first in a round is extremely important as certain cards (like 2s or 3s) are hard to get rid of due to their low value.

---

## Design

We intend to make a game playable through Steam and executable on computer. This game will feature the outlined gameplay above using standardized turn-based rules and comprehensive UI.

---

## Customizable Rules

Users will be able to add and use new rules to make the game more interesting.

| Rule | Notes |
|------|-------|
| Timed Turns | Customizable timer on each person's turn |
| 4/6 Players | Games can be played in 4 or 6 players |
| Win Condition | Make the final stretch require 1st + 2nd place (disallow 1st + 3rd as a win) |
| Tax | Customizable tax rules |
