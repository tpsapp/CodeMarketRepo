#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd -P)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." &>/dev/null && pwd -P)"

DO_MINIFY=0
DRY_RUN=0
VERBOSE=0

if [[ -z "${NO_COLOR:-}" && -t 1 ]]; then
  C_GREEN=$'\033[0;32m'
  C_YELLOW=$'\033[0;33m'
  C_RED=$'\033[0;31m'
  C_RESET=$'\033[0m'
else
  C_GREEN=""
  C_YELLOW=""
  C_RED=""
  C_RESET=""
fi

usage() {
  cat <<'EOF'
Usage: ./scripts/check-fleet.sh [--minify] [--dry-run|-n] [--verbose|-v] [--help|-h]

Validate all 10 Code Market fleet scripts (Java 25 and C#) for balanced delimiters,
API symbol compliance, strict C# compilation, and in-game editor limits
(<= 498 lines and <= 16,382 characters including CRLF).

Flags:
  --minify       Write local gitignored *-mini.java / *-mini.cs copies
  -n, --dry-run  Validate and report minified sizes without writing files
  -v, --verbose  Print per-file metrics and build output
  -h, --help     Show this help message and exit
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --minify) DO_MINIFY=1 ;;
    -n|--dry-run) DRY_RUN=1 ;;
    -v|--verbose) VERBOSE=1 ;;
    -h|--help) usage; exit 0 ;;
    *)
      printf '%sError: unknown argument: %s%s\n' "${C_RED}" "$1" "${C_RESET}" >&2
      usage >&2
      exit 1
      ;;
  esac
  shift
done

if ! command -v python3 >/dev/null 2>&1; then
  printf '%sError: python3 is required.%s\n' "${C_RED}" "${C_RESET}" >&2
  exit 1
fi

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

python3 - "${REPO_ROOT}" "${DO_MINIFY}" "${DRY_RUN}" "${VERBOSE}" "${TMP_DIR}" <<'PYEOF'
import json
import os
import re
import sys

repo_root = sys.argv[1]
do_minify = sys.argv[2] == "1"
dry_run = sys.argv[3] == "1"
verbose = sys.argv[4] == "1"
tmp_dir = sys.argv[5]

MAX_LINES = 498
MAX_CHARS = 16382


def strip_comments_and_compact(src: str) -> str:
    """Strip // comments (preserving strings/chars) and minify to a single line safely."""
    # Tokenize into string/char literals, multi-char operators, words/numbers, and single-char punctuation.
    tokens = []
    i = 0
    n = len(src)
    two_char_ops = {
        "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=",
        "->", "=>", "<<", ">>", "??", "?.", "::",
    }
    while i < n:
        ch = src[i]
        # Skip whitespace
        if ch.isspace():
            i += 1
            continue
        # Strip // line comments
        if ch == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                i += 1
            continue
        # Strip /* block comments */
        if ch == "/" and i + 1 < n and src[i + 1] == "*":
            i += 2
            while i + 1 < n and not (src[i] == "*" and src[i + 1] == "/"):
                i += 1
            i += 2
            continue
        # Preserve string and char literals verbatim
        if ch in ('"', "'"):
            quote = ch
            j = i + 1
            while j < n:
                if src[j] == "\\":
                    j += 2
                elif src[j] == quote:
                    j += 1
                    break
                else:
                    j += 1
            tokens.append(("lit", src[i:j]))
            i = j
            continue
        # Word / identifier / number
        if ch.isalnum() or ch == "_":
            j = i + 1
            while j < n and (src[j].isalnum() or src[j] == "_"):
                j += 1
            tokens.append(("word", src[i:j]))
            i = j
            continue
        # Two-character operators
        if i + 1 < n and src[i : i + 2] in two_char_ops:
            tokens.append(("op", src[i : i + 2]))
            i += 2
            continue
        # Single-character punctuation / operator
        tokens.append(("op", ch))
        i += 1

    out = []
    prev_kind = ""
    prev_tok = ""
    for kind, tok in tokens:
        need_space = False
        if prev_tok:
            if prev_kind == "word" and kind == "word":
                need_space = True
            elif prev_tok[-1] == "+" and tok[0] == "+":
                need_space = True
            elif prev_tok[-1] == "-" and tok[0] == "-":
                need_space = True
            elif prev_tok[-1] == "/" and tok[0] in ("/", "*"):
                need_space = True
            elif prev_tok[-1] == ">" and tok[0] == ">":
                need_space = True
            elif prev_tok[-1] == "<" and tok[0] == "<":
                need_space = True
        if need_space:
            out.append(" ")
        out.append(tok)
        prev_kind = kind
        prev_tok = tok
    return "".join(out) + "\n"


api_path = os.path.join(repo_root, "code-market-api.json")
java_methods = None
cs_methods = None
if os.path.isfile(api_path):
    with open(api_path, encoding="utf-8") as f:
        api = json.load(f)
    java_methods = {m["name"] for t in api["types"].values() for m in t["methods"]}
    java_methods |= {m["name"] for m in api["productMethods"]}
    cs_methods = {m[0].upper() + m[1:] for m in java_methods}
else:
    print("[SKIP] code-market-api.json not found; skipping API symbol cross-check.")

failures = 0
for lang, ext, methods in [
    ("java", "java", java_methods),
    ("csharp", "cs", cs_methods),
]:
    for idx in range(1, 6):
        rel = f"{lang}/robot{idx}.{ext}"
        path = os.path.join(repo_root, rel)
        if not os.path.isfile(path):
            print(f"[FAIL] Missing script: {rel}", file=sys.stderr)
            failures += 1
            continue
        with open(path, encoding="utf-8") as f:
            txt = f.read()

        if txt.count("{") != txt.count("}") or txt.count("(") != txt.count(")"):
            print(f"[FAIL] Unbalanced delimiters in {rel}", file=sys.stderr)
            failures += 1
            continue

        if methods is not None:
            calls = set(re.findall(r"\b(?:robot|store|order|customer)\.([A-Za-z_]\w*)\s*\(", txt))
            unknown = calls - methods
            if unknown:
                print(f"[FAIL] Unknown API calls in {rel}: {sorted(unknown)}", file=sys.stderr)
                failures += 1
                continue

        mini = strip_comments_and_compact(txt)
        mini_lines = len(mini.splitlines())
        # Count CRLF character length to match Windows/Unity editor counting conservatively
        mini_chars_crlf = len(mini.replace("\n", "\r\n"))

        if mini_lines > MAX_LINES or mini_chars_crlf > MAX_CHARS:
            print(
                f"[FAIL] {rel} exceeds editor limit when minified: "
                f"{mini_lines}/{MAX_LINES} lines, {mini_chars_crlf}/{MAX_CHARS} chars (CRLF)",
                file=sys.stderr,
            )
            failures += 1
            continue

        if do_minify and not dry_run:
            mini_path = os.path.join(repo_root, f"{lang}/robot{idx}-mini.{ext}")
            with open(mini_path, "w", encoding="utf-8") as mf:
                mf.write(mini)

        if verbose:
            action = "wrote -mini" if (do_minify and not dry_run) else "ok"
            print(
                f"[PASS] {rel}: raw={len(txt)} chars/{len(txt.splitlines())} lines | "
                f"mini={mini_chars_crlf} chars(CRLF)/{mini_lines} lines ({action})"
            )

if failures > 0:
    sys.exit(1)
PYEOF

if command -v dotnet >/dev/null 2>&1; then
  cat > "${TMP_DIR}/Stubs.cs" <<'CSEOF'
using System;
using System.Collections.Generic;
using System.Linq;

public enum Direction { Up, Down, Left, Right }
public enum Season { Summer, Autumn, Winter, Spring }
public enum Entity {
  BreadShelf, Register, Fridge, PumpkinTable, Plant, Boxes, Flower, TomatoTable,
  BeerFridge, CigaretteStand, WineStand, MilkStand, TulipCounter, HoneyStall,
  WatermelonTable, TangerineTable, StrawberryStand, EasterTable, FireworksStand,
  TreeStand, SignBoard, PalmTree, FanPalm, Monstera, Dracaena, MeatFridge,
  TruffleFridge, CaviarFridge, LobsterFridge, VendingMachine, PopcornMachine,
  CoffeeMachine, ChristmasTree, IceCreamCounter, JamTable, FertilizerTable,
  MaskStand, WhiskyCabinet, CognacCabinet, Aquarium, WaterCooler, Balloons,
  FortuneWheel, Daisies, Bag
}
public enum Product {
  Tomato, Pumpkin, Bread, Soda, Beer, Cigarettes, Wine, Milk, Tulips, Honey,
  Watermelon, Tangerine, Strawberry, Eastercake, Fireworks, Tree, Steak,
  Truffle, Caviar, Lobster, Chocolate, Popcorn, Coffee, Icecream, Jam,
  Fertilizer, Mask, Whisky, Cognac
}
public static class ProductExtensions {
  public static bool IsAlcohol(this Product p) => false;
  public static bool IsAdultsOnly(this Product p) => false;
  public static bool IsVending(this Product p) => false;
  public static bool IsInfinite(this Product p) => false;
  public static bool IsInSeason(this Product p) => true;
  public static int GetPrice(this Product p) => 10;
  public static Entity GetRack(this Product p) => Entity.Boxes;
}
public class Cell { public int GetX() => 0; public int GetY() => 0; }
public class Shelf {
  public Product GetProduct() => Product.Tomato;
  public int GetCount() => 0;
  public int GetCapacity() => 10;
  public bool IsFull() => false;
  public bool IsEmpty() => true;
}
public class Customer { public bool IsAdult() => true; public int GetPatience() => 30; }
public class Order {
  public List<Product> GetProducts() => new();
  public int GetRemaining(Product p) => 0;
  public bool IsDone() => true;
  public bool Remove(Product p) => false;
  public bool RemoveRemaining() => false;
}
public class Store {
  public int GetWidth() => 1;
  public int GetHeight() => 1;
  public bool IsFree(int x, int y) => true;
  public List<Cell> FindAll(Entity e) => new();
  public int GetRegisterNumber(int x, int y) => 0;
  public Direction? GetRegisterSide(int x, int y) => null;
  public Cell? GetCashierCell(int x, int y) => null;
  public Cell? GetPackerCell(int x, int y) => null;
  public int GetStock(int x, int y) => 0;
  public List<Direction>? GetPickupDirections(int x, int y, Product p) => new();
  public int GetHour() => 12;
  public int GetMinute() => 0;
  public int GetRemaining(Product p) => 0;
  public bool IsTruckAway() => true;
  public bool IsTruckHere() => false;
  public int GetGarageX() => 0;
  public int GetGarageY() => 0;
  public Direction GetGarageSide() => Direction.Down;
  public int GetTruckCapacity() => 20;
  public bool CallDelivery(List<Product> batch) => false;
}
public class Robot {
  public int GetX() => 0;
  public int GetY() => 0;
  public Direction GetFacing() => Direction.Up;
  public int GetCapacity() => 6;
  public int GetLoad() => 0;
  public bool Has(Product p) => false;
  public List<Product> GetCarried() => new();
  public List<Product> GetList() => new();
  public void Turn(Direction d) {}
  public bool CanMove(Direction d) => false;
  public bool Move(Direction d) => false;
  public Shelf? GetShelf() => null;
  public bool IsAtPickup() => false;
  public bool Pick(Product p) => false;
  public bool PutOnShelf(Product p) => false;
  public bool IsAtTruck() => false;
  public Order? TakeOrder() => null;
  public Customer? GetCustomer() => null;
  public bool IsAtBag() => false;
  public bool PutInBag(Product p) => false;
}
CSEOF

  for i in 1 2 3 4 5; do
    PROJ_DIR="${TMP_DIR}/r${i}"
    mkdir -p "${PROJ_DIR}"
    cat > "${PROJ_DIR}/R${i}.csproj" <<'CSPROJ'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
CSPROJ
    cp "${TMP_DIR}/Stubs.cs" "${PROJ_DIR}/Stubs.cs"
    TARGET_CS="${REPO_ROOT}/csharp/robot${i}.cs"
    if [[ "${DO_MINIFY}" -eq 1 && "${DRY_RUN}" -eq 0 ]]; then
      TARGET_CS="${REPO_ROOT}/csharp/robot${i}-mini.cs"
    fi
    {
      printf 'var store = new Store();\nvar robot = new Robot();\n'
      cat "${TARGET_CS}"
    } > "${PROJ_DIR}/Program.cs"
    if [[ "${VERBOSE}" -eq 1 ]]; then
      dotnet build -c Release "${PROJ_DIR}/R${i}.csproj" -v q --nologo
    else
      dotnet build -c Release "${PROJ_DIR}/R${i}.csproj" -v q --nologo >/dev/null
    fi
    if [[ "${VERBOSE}" -eq 1 ]]; then
      printf '%s[BUILD PASS]%s %s\n' "${C_GREEN}" "${C_RESET}" "${TARGET_CS#"${REPO_ROOT}/"}"
    fi
  done
else
  printf '%s[SKIP]%s dotnet not found; skipping C# strict compilation.\n' "${C_YELLOW}" "${C_RESET}"
fi

printf '%sAll fleet checks passed.%s\n' "${C_GREEN}" "${C_RESET}"
