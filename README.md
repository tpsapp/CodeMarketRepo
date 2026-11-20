# CodeMarketRepo — 5-Robot Autonomous Fleet for *Code Market*

Fully automated Java 25 scripts for a 5-robot, 3-register supermarket fleet in the Steam game **Code Market** (all tech-tree unlocks enabled).

## Readable vs. Minified Files

*Code Market*'s in-game editor enforces both a **16,382-character** and a **498-line** limit per robot script. Every robot is provided in two forms:

- **`robot1.java` – `robot5.java`**: Cleanly formatted, readable source code for reading, learning, and modifying.
- **`robot1-mini.java` – `robot5-mini.java`**: Compacted, paste-ready versions (~126–151 lines, ~11–13 KB) with identical logic that fit comfortably inside the in-game editor limits.

## Fleet Architecture

| Robot | Readable Source | Paste-Ready Script | Role |
| :---: | :--- | :--- | :--- |
| **1** | `robot1.java` | `robot1-mini.java` | Register 1 cashier + midnight wheel-spin alert (`SPIN THE WHEEL!`) |
| **2** | `robot2.java` | `robot2-mini.java` | Register 2 cashier |
| **3** | `robot3.java` | `robot3-mini.java` | Register 3 cashier |
| **4** | `robot4.java` | `robot4-mini.java` | Inventory manager, delivery ordering, and urgency-first shelf restocker |
| **5** | `robot5.java` | `robot5-mini.java` | Vending machine & reverse-order truck restocker |

## Key Features

- **Zero-Blackout Inventory & Ordering (`robot4`):** Dynamically learns rack capacities (interleaved with live ordering — no blocking startup tour), tracks `robot5`'s final in-flight truck armload to prevent double-ordering without a cooldown stall, and automatically scales for multiple racks per product as well as 2-tile `BREAD_SHELF` racks.
- **Collision-Resistant Pathfinding (all 5 robots):** Precomputed $O(1)$ landmark tables (`toCashier[]`, `toBag[]`, `toTruck[]`) paired with array-backed BFS, per-trip multi-tile dynamic obstacle memory (`blkGen[]`), 4-way adjacent `canMove()` sensing, staggered yield priorities (`5 / 4 / 3 / 2 / 1`), and alternate pickup-tile rerouting to eliminate L-shaped oscillation around customer queues.
- **Compliance & Order Reliability (`robot1`–`robot3`):** Automatically filters minors from 18+ items, enforces 22:00–06:00 alcohol restrictions (safely returning picked alcohol without leaking carry slots if the fridge is full), uses bag-aware BFS tie-breaking (`distToBag[]`), and waits at the bag for empty in-season shelves to be restocked while removing empty out-of-season items.
- **Split Truck Unloading (`robot4` & `robot5`):** Both stockers dynamically anchor to the two garage door tiles via `store.getGarageX()/Y()`. `robot4` unloads empty/highest-urgency shelves first while `robot5` prioritizes vending machines and reverse-order items to avoid shelf collisions.

## Usage

1. Unlock the full Java 25 code tree and all 5 robots / 3 registers in *Code Market*.
2. Copy the contents of **`robot1-mini.java`** through **`robot5-mini.java`** into Robots 1–5 respectively.
3. Start all 5 robots. No hardcoded rack coordinates are required — the scripts discover registers, garage tiles, and all placed racks dynamically at startup.

## License

MIT License — see [LICENSE](LICENSE).
