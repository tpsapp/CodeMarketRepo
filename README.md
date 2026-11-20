# CodeMarketRepo — 5-Robot Autonomous Fleet for [*Code Market*](https://store.steampowered.com/app/5451260/Code_Market/)

Fully automated **Java 25** and **C#** scripts for a 5-robot, 3-register supermarket fleet in the Steam game **Code Market** (all tech-tree unlocks enabled).

## Repository Layout

- [`java/`](java/) — Java 25 fleet scripts (`robot1.java`–`robot5.java`)
- [`csharp/`](csharp/) — C# fleet scripts (`robot1.cs`–`robot5.cs`)

## In-Game Editor Limits

*Code Market*'s in-game editor enforces both a **16,382-character** and a **498-line** limit per robot script. The files in this repository (`java/robot1.java`–`robot5.java` and `csharp/robot1.cs`–`robot5.cs`) are kept cleanly formatted and readable for sharing and editing. Before pasting into the game, compact/minify the whitespace locally so each script stays under 498 lines and 16,382 characters.

## Fleet Architecture

| Robot | Java | C# | Role |
| :---: | :--- | :--- | :--- |
| **1** | `java/robot1.java` | `csharp/robot1.cs` | Register 1 cashier + midnight wheel-spin alert (`SPIN THE WHEEL!`) |
| **2** | `java/robot2.java` | `csharp/robot2.cs` | Register 2 cashier |
| **3** | `java/robot3.java` | `csharp/robot3.cs` | Register 3 cashier |
| **4** | `java/robot4.java` | `csharp/robot4.cs` | Inventory manager, delivery ordering, and urgency-first shelf restocker |
| **5** | `java/robot5.java` | `csharp/robot5.cs` | Vending machine & reverse-order truck restocker |

## Key Features

- **Zero-Blackout Inventory & Ordering (`robot4`):** Dynamically learns rack capacities (interleaved with live ordering — no blocking startup tour), tracks `robot5`'s final in-flight truck armload to prevent double-ordering without a cooldown stall, and automatically scales for multiple racks per product as well as 2-tile `BREAD_SHELF` / `BreadShelf` racks.
- **Collision-Resistant Pathfinding (all 5 robots):** Precomputed $O(1)$ landmark tables (`toCashier[]`, `toBag[]`, `toTruck[]`) paired with array-backed BFS, per-trip multi-tile dynamic obstacle memory (`blkGen[]`), 4-way adjacent `canMove()` / `CanMove()` sensing, staggered yield priorities (`5 / 4 / 3 / 2 / 1`), and alternate pickup-tile rerouting to eliminate L-shaped oscillation around customer queues.
- **Compliance & Order Reliability (`robot1`–`robot3`):** Automatically filters minors from 18+ items, enforces 22:00–06:00 alcohol restrictions (safely returning picked alcohol without leaking carry slots if the fridge is full), uses bag-aware BFS tie-breaking (`distToBag[]`), and waits at the bag for empty in-season shelves to be restocked while removing empty out-of-season items.
- **Split Truck Unloading (`robot4` & `robot5`):** Both stockers dynamically anchor to the two garage door tiles via `store.getGarageX()/Y()` (`store.GetGarageX()/Y()`). `robot4` unloads empty/highest-urgency shelves first while `robot5` prioritizes vending machines and reverse-order items to avoid shelf collisions.

## Usage

1. Unlock the full Java 25 or C# code tree and all 5 robots / 3 registers in *Code Market*.
2. Compact/minify `robot1`–`robot5` from either `java/` or `csharp/` (to stay under the 498-line / 16,382-character editor limits) and paste them into Robots 1–5 respectively.
3. Start all 5 robots. No hardcoded rack coordinates are required — the scripts discover registers, garage tiles, and all placed racks dynamically at startup.

## License

MIT License — see [LICENSE](LICENSE).
