# Contributing

Contributions, bug reports, and pathing or efficiency improvements are welcome.

## Guidelines

1. **Keep Scripts Self-Contained:** All code must use standard Java 25 features supported by *Code Market* (no external dependencies, no file/network I/O, no `java.time`, no reflection, no `Thread` APIs beyond `Thread.sleep`).
2. **Respect In-Game Editor Limits:** Any change to `robot1.java`–`robot5.java` must also update the matching `robot1-mini.java`–`robot5-mini.java` file, keeping every `-mini` script strictly under **498 lines** and **16,382 characters** (including CRLF).
3. **Zero Bloat:** Keep logic flat, readable, and free of unnecessary abstractions.
4. **Conventional Commits:** Use Conventional Commits for commit messages (e.g., `fix(robot4): ...`, `perf(pathing): ...`, `docs: ...`).
