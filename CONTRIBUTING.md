# Contributing

Contributions, bug reports, and pathing or efficiency improvements are welcome.

## Guidelines

1. **Keep Scripts Self-Contained:**
   - **Java (`java/robot1.java`–`robot5.java`):** Use only standard Java 25 features supported by *Code Market* (no external dependencies, no file/network I/O, no `java.time`, no reflection, no `Thread` APIs beyond `Thread.sleep`).
   - **C# (`csharp/robot1.cs`–`robot5.cs`):** Use only standard C# features supported by *Code Market* (no external dependencies, no `Thread.Sleep` or `async`/`await`, no `DateTime`, no file/network I/O, no reflection; keep top-level statements before `record`/`class`/`enum` declarations).
2. **Respect In-Game Editor Limits:** Keep `java/robot1.java`–`robot5.java` and `csharp/robot1.cs`–`robot5.cs` lean enough that a whitespace-compacted copy stays strictly under **498 lines** and **16,382 characters** (including CRLF). Local `*-mini.java` and `*-mini.cs` files are ignored via `.gitignore`.
3. **Zero Bloat & Cross-Language Parity:** Keep logic flat, readable, and free of unnecessary abstractions. When fixing bugs or improving pathing/inventory behavior, keep `java/` and `csharp/` fleets behaviorally aligned.
4. **Conventional Commits:** Use Conventional Commits for commit messages (e.g., `fix(robot4): ...`, `perf(pathing): ...`, `docs: ...`).
