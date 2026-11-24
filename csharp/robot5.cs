Direction[] DIRS = { Direction.Right, Direction.Left, Direction.Down, Direction.Up };

int W = store.GetWidth();
int H = store.GetHeight();
int SIZE = W * H;

Direction? garageSide = store.GetGarageSide();
int gx0 = store.GetGarageX();
int gy0 = store.GetGarageY();
int garageX = gx0;
int garageY = gy0;

void YieldTick() {
  robot.Turn(robot.GetFacing());
}

int GetDx(Direction d) => d switch {
  Direction.Left => -1,
  Direction.Right => 1,
  _ => 0
};

int GetDy(Direction d) => d switch {
  Direction.Up => 1,
  Direction.Down => -1,
  _ => 0
};

Direction? Opposite(Direction? d) => d switch {
  Direction.Up => Direction.Down,
  Direction.Down => Direction.Up,
  Direction.Left => Direction.Right,
  Direction.Right => Direction.Left,
  _ => null
};

Direction? FaceToward(int fx, int fy, int tx, int ty) {
  if (tx > fx) return Direction.Right;
  if (tx < fx) return Direction.Left;
  if (ty > fy) return Direction.Up;
  if (ty < fy) return Direction.Down;
  return null;
}

bool[] freeMap = new bool[SIZE];
for (int y = 0; y < H; y++) {
  for (int x = 0; x < W; x++) {
    freeMap[y * W + x] = store.IsFree(x, y);
  }
}

bool horizontalDoor = (garageSide == Direction.Up || garageSide == Direction.Down);
int sx0 = robot.GetX();
int sy0 = robot.GetY();
if (horizontalDoor && sy0 == gy0 && Math.Abs(sx0 - gx0) == 1) {
  garageX = sx0;
  garageY = sy0;
} else if (!horizontalDoor && sx0 == gx0 && Math.Abs(sy0 - gy0) == 1) {
  garageX = sx0;
  garageY = sy0;
} else if (horizontalDoor) {
  if (gx0 + 1 < W && freeMap[gy0 * W + (gx0 + 1)]) garageX = gx0 + 1;
  else if (gx0 - 1 >= 0 && freeMap[gy0 * W + (gx0 - 1)]) garageX = gx0 - 1;
} else {
  if (gy0 + 1 < H && freeMap[(gy0 + 1) * W + gx0]) garageY = gy0 + 1;
  else if (gy0 - 1 >= 0 && freeMap[(gy0 - 1) * W + gx0]) garageY = gy0 - 1;
}

int[] prev = new int[SIZE];
Direction?[] prevDir = new Direction?[SIZE];
Direction[] pathBuf = new Direction[SIZE];
int[] q = new int[SIZE];
int[] visGen = new int[SIZE];
int[] blkGen = new int[SIZE];
int gen = 0;
int bGen = 0;

int[] tgtGen = new int[SIZE];
Product?[] tgtProd = new Product?[SIZE];
Direction?[] tgtDir = new Direction?[SIZE];
int tGen = 0;

Dictionary<int, int> fullRackStock = new();
Dictionary<Product, List<Cell>> rackCells = new();
Dictionary<Product, List<Spot>> pickupSpots = new();

List<Cell> GetRackCells(Product p) {
  if (!rackCells.TryGetValue(p, out var cells)) {
    cells = store.FindAll(p.GetRack());
    rackCells[p] = cells;
  }
  return cells;
}

void MarkAdjBlocked() {
  int rx = robot.GetX();
  int ry = robot.GetY();
  foreach (Direction d in DIRS) {
    int nx = rx + GetDx(d);
    int ny = ry + GetDy(d);
    if (nx >= 0 && nx < W && ny >= 0 && ny < H) {
      int nid = ny * W + nx;
      if (freeMap[nid] && !robot.CanMove(d)) {
        blkGen[nid] = bGen;
      }
    }
  }
}

int Bfs(int sx, int sy, int targetId, bool useCanMove, bool useMarked) {
  gen++;
  int head = 0;
  int tail = 0;
  int startId = sy * W + sx;
  visGen[startId] = gen;
  prevDir[startId] = null;
  q[tail++] = startId;

  while (head < tail) {
    int cid = q[head++];
    if (cid == targetId || (useMarked && tgtGen[cid] == tGen)) return cid;
    int cx = cid % W;
    int cy = cid / W;
    Direction? straight = prevDir[cid];

    for (int k = 0; k < 5; k++) {
      Direction? dOpt = (k == 0) ? straight : DIRS[k - 1];
      if (dOpt == null || (k > 0 && dOpt == straight)) continue;
      Direction d = dOpt.Value;
      if (useCanMove && cid == startId && !robot.CanMove(d)) continue;
      int nx = cx + GetDx(d);
      int ny = cy + GetDy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H) {
        int nid = ny * W + nx;
        if ((!useCanMove || blkGen[nid] != bGen) && visGen[nid] != gen && freeMap[nid]) {
          visGen[nid] = gen;
          prev[nid] = cid;
          prevDir[nid] = d;
          if (nid == targetId || (useMarked && tgtGen[nid] == tGen)) return nid;
          q[tail++] = nid;
        }
      }
    }
  }
  return -1;
}

Direction?[] BuildNavTable(int tx, int ty) {
  Direction?[] table = new Direction?[SIZE];
  Bfs(tx, ty, -1, false, false);
  int startId = ty * W + tx;
  for (int i = 0; i < SIZE; i++) {
    if (i != startId && visGen[i] == gen && prevDir[i] != null) {
      table[i] = Opposite(prevDir[i]);
    }
  }
  return table;
}

Direction?[] toTruck = BuildNavTable(garageX, garageY);

bool Sidestep(Direction? blockedDir) {
  int start = Random.Shared.Next(4);
  for (int i = 0; i < 4; i++) {
    Direction d = DIRS[(start + i) % 4];
    if (d != blockedDir && robot.CanMove(d)) {
      return robot.Move(d);
    }
  }
  return false;
}

int WalkComputedPath(int startId, int targetId) {
  int len = 0;
  int curr = targetId;
  while (curr != startId) {
    pathBuf[len++] = prevDir[curr]!.Value;
    curr = prev[curr];
  }
  for (int i = len - 1; i >= 0; i--) {
    Direction step = pathBuf[i];
    if (!robot.CanMove(step) || !robot.Move(step)) {
      int bx = robot.GetX() + GetDx(step);
      int by = robot.GetY() + GetDy(step);
      return (bx >= 0 && bx < W && by >= 0 && by < H) ? by * W + bx : -2;
    }
  }
  return -1;
}

bool GoTo(int tx, int ty, Direction? faceDir, int initBlocked) {
  bGen++;
  if (initBlocked >= 0 && initBlocked < SIZE) blkGen[initBlocked] = bGen;
  Direction? lastBlockedDir = null;
  int retries = 0;
  int totalAttempts = 0;
  int tid = ty * W + tx;

  while (totalAttempts < 25) {
    totalAttempts++;
    int sx = robot.GetX();
    int sy = robot.GetY();
    int startId = sy * W + sx;
    if (startId == tid) {
      if (faceDir != null && robot.GetFacing() != faceDir.Value) robot.Turn(faceDir.Value);
      return true;
    }

    MarkAdjBlocked();
    blkGen[startId] = 0;
    Bfs(sx, sy, tid, true, false);

    if (visGen[tid] != gen) {
      bGen++;
      if (!Sidestep(lastBlockedDir)) YieldTick();
      lastBlockedDir = null;
      continue;
    }

    int hit = WalkComputedPath(startId, tid);
    if (hit == -1) {
      if (faceDir != null && robot.GetFacing() != faceDir.Value) robot.Turn(faceDir.Value);
      return true;
    }
    if (hit >= 0 && hit < SIZE) {
      blkGen[hit] = bGen;
      lastBlockedDir = FaceToward(robot.GetX(), robot.GetY(), hit % W, hit / W);
    }
    MarkAdjBlocked();

    retries++;
    if (retries > 1) {
      if (!Sidestep(lastBlockedDir)) YieldTick();
      lastBlockedDir = null;
      retries = 0;
    }
  }
  return false;
}

bool GoToTruck() {
  int tid = garageY * W + garageX;
  while (true) {
    int cid = robot.GetY() * W + robot.GetX();
    if (cid == tid) {
      if (garageSide != null && robot.GetFacing() != garageSide.Value) robot.Turn(garageSide.Value);
      return true;
    }
    Direction? stepOpt = toTruck[cid];
    if (stepOpt == null) {
      return GoTo(garageX, garageY, garageSide, -1);
    }
    Direction step = stepOpt.Value;
    if (!robot.CanMove(step) || !robot.Move(step)) {
      int bx = robot.GetX() + GetDx(step);
      int by = robot.GetY() + GetDy(step);
      int bid = (bx >= 0 && bx < W && by >= 0 && by < H) ? by * W + bx : -1;
      return GoTo(garageX, garageY, garageSide, bid);
    }
  }
}

bool CachePickupDirections(Product p) {
  if (pickupSpots.TryGetValue(p, out var existing) && existing.Count > 0) return true;
  List<Spot> spots = new();
  HashSet<long> seen = new();
  foreach (Cell rc in GetRackCells(p)) {
    foreach (Direction d in DIRS) {
      int nx = rc.GetX() + GetDx(d);
      int ny = rc.GetY() + GetDy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H) {
        int tid = ny * W + nx;
        if (freeMap[tid]) {
          List<Direction>? pd = store.GetPickupDirections(nx, ny, p);
          if (pd != null) {
            foreach (Direction face in pd) {
              long key = ((long)tid << 3) | (uint)(int)face;
              if (seen.Add(key)) {
                spots.Add(new Spot(tid, face, nx + GetDx(face), ny + GetDy(face)));
              }
            }
          }
        }
      }
    }
  }
  if (spots.Count > 0) {
    pickupSpots[p] = spots;
    return true;
  }
  return false;
}

bool ProbeSingleSpot(Product p) {
  List<Spot> spots = new();
  foreach (Cell rc in GetRackCells(p)) {
    foreach (Direction d in DIRS) {
      int nx = rc.GetX() + GetDx(d);
      int ny = rc.GetY() + GetDy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H && freeMap[ny * W + nx]) {
        Direction? face = FaceToward(nx, ny, rc.GetX(), rc.GetY());
        if (GoTo(nx, ny, face, -1)
            && robot.IsAtPickup()
            && robot.GetShelf() != null
            && robot.GetShelf()!.GetProduct() == p) {
          spots.Add(new Spot(ny * W + nx, robot.GetFacing(), rc.GetX(), rc.GetY()));
          break;
        }
      }
    }
  }
  if (spots.Count > 0) {
    pickupSpots[p] = spots;
    return true;
  }
  return false;
}

void EnsureSpots(Product p) {
  if (pickupSpots.TryGetValue(p, out var existing) && existing.Count > 0) return;
  if (!CachePickupDirections(p)) {
    ProbeSingleSpot(p);
  }
}

bool IsSpotUnloadable(Spot sp) {
  int rkey = sp.Ry * W + sp.Rx;
  if (!fullRackStock.TryGetValue(rkey, out int fullVal)) return true;
  if (store.GetStock(sp.Rx, sp.Ry) < fullVal) {
    fullRackStock.Remove(rkey);
    return true;
  }
  return false;
}

bool HasUnloadableSpot(Product p) {
  EnsureSpots(p);
  if (!pickupSpots.TryGetValue(p, out var spots) || spots.Count == 0) return false;
  foreach (Spot sp in spots) {
    if (IsSpotUnloadable(sp)) return true;
  }
  return false;
}

Product? GoToNearest(List<Product> targets) {
  tGen++;
  int sx = robot.GetX();
  int sy = robot.GetY();
  int startId = sy * W + sx;

  foreach (Product p in targets) {
    EnsureSpots(p);
    if (!pickupSpots.TryGetValue(p, out var spots)) continue;
    foreach (Spot sp in spots) {
      if (!IsSpotUnloadable(sp)) continue;
      if (sp.Tid == startId) {
        if (robot.GetFacing() != sp.Dir) robot.Turn(sp.Dir);
        return p;
      }
      tgtGen[sp.Tid] = tGen;
      tgtProd[sp.Tid] = p;
      tgtDir[sp.Tid] = sp.Dir;
    }
  }

  bGen++;
  for (int tries = 0; tries < 6; tries++) {
    int cx = robot.GetX();
    int cy = robot.GetY();
    int curId = cy * W + cx;
    if (tgtGen[curId] == tGen) {
      Direction? d = tgtDir[curId];
      if (d != null && robot.GetFacing() != d.Value) robot.Turn(d.Value);
      return tgtProd[curId];
    }
    MarkAdjBlocked();
    blkGen[curId] = 0;
    int bestTid = Bfs(cx, cy, -1, true, true);
    if (bestTid == -1) break;
    Product? bestP = tgtProd[bestTid];
    Direction? bestDir = tgtDir[bestTid];
    int hit = WalkComputedPath(curId, bestTid);
    if (hit == -1) {
      if (bestDir != null && robot.GetFacing() != bestDir.Value) robot.Turn(bestDir.Value);
      return bestP;
    }
    if (hit >= 0 && hit < SIZE) blkGen[hit] = bGen;
    MarkAdjBlocked();
  }

  int fallbackTid = Bfs(robot.GetX(), robot.GetY(), -1, false, true);
  if (fallbackTid == -1) return null;
  Product? fallbackP = tgtProd[fallbackTid];
  if (!GoTo(fallbackTid % W, fallbackTid / W, tgtDir[fallbackTid], -1)) return null;
  return fallbackP;
}

void InitPickupSpots() {
  foreach (Product p in Enum.GetValues<Product>()) {
    if (p.IsInfinite()) continue;
    List<Cell> cells = GetRackCells(p);
    if (cells == null || cells.Count == 0) continue;
    CachePickupDirections(p);
  }
  GoToTruck();
}

bool HasItemsInTruck() {
  foreach (Product p in Enum.GetValues<Product>()) {
    if (store.GetRemaining(p) > 0) return true;
  }
  return false;
}

bool HasUnloadableCarried() {
  foreach (Product p in robot.GetCarried()) {
    if (HasUnloadableSpot(p)) return true;
  }
  return false;
}

bool SafePutOnShelf(Product p) {
  if (!robot.IsAtPickup() || !robot.Has(p)) return false;
  Shelf? s = robot.GetShelf();
  if (s == null || s.GetProduct() != p || s.IsFull()) return false;
  try {
    return robot.PutOnShelf(p);
  } catch {
    return false;
  }
}

bool SafePickTruck(Product p) {
  if (!robot.IsAtTruck() || robot.GetLoad() >= robot.GetCapacity() || store.GetRemaining(p) <= 0) return false;
  try {
    return robot.Pick(p);
  } catch {
    return false;
  }
}

void UnloadCarried() {
  while (true) {
    List<Product> carried = new();
    foreach (Product p in robot.GetCarried()) {
      if (!carried.Contains(p) && HasUnloadableSpot(p)) carried.Add(p);
    }
    if (carried.Count == 0) break;

    Product? nextOpt = GoToNearest(carried);
    if (nextOpt == null) break;
    Product next = nextOpt.Value;
    while (robot.Has(next) && SafePutOnShelf(next)) {}
    Shelf? s = robot.GetShelf();
    if (robot.Has(next) && robot.IsAtPickup() && s != null && s.GetProduct() == next) {
      int rx = robot.GetX() + GetDx(robot.GetFacing());
      int ry = robot.GetY() + GetDy(robot.GetFacing());
      fullRackStock[ry * W + rx] = store.GetStock(rx, ry);
    }
  }
}

void RestockFromTruck() {
  Product[] all = Enum.GetValues<Product>();
  while (HasItemsInTruck() && robot.GetLoad() < robot.GetCapacity()) {
    if (!GoToTruck()) {
      Sidestep(null);
      YieldTick();
      return;
    }
    while (!robot.IsAtTruck()) {
      if (store.IsTruckAway() || robot.GetX() != garageX || robot.GetY() != garageY) return;
      YieldTick();
    }

    int beforeLoad = robot.GetLoad();
    foreach (Product p in all) {
      if (!p.IsVending()) continue;
      while (robot.GetLoad() < robot.GetCapacity() && store.GetRemaining(p) > 0 && SafePickTruck(p)) {}
      if (robot.GetLoad() >= robot.GetCapacity()) break;
    }
    if (robot.GetLoad() < robot.GetCapacity()) {
      for (int i = all.Length - 1; i >= 0; i--) {
        Product p = all[i];
        if (p.IsVending()) continue;
        while (robot.GetLoad() < robot.GetCapacity() && store.GetRemaining(p) > 0 && SafePickTruck(p)) {}
        if (robot.GetLoad() >= robot.GetCapacity()) break;
      }
    }

    if (robot.GetLoad() == beforeLoad) break;

    UnloadCarried();

    if (robot.GetLoad() >= robot.GetCapacity()) break;
  }
}

InitPickupSpots();

while (true) {
  if (HasUnloadableCarried()) {
    UnloadCarried();
  } else if (store.IsTruckHere() && HasItemsInTruck() && robot.GetLoad() < robot.GetCapacity()) {
    RestockFromTruck();
  } else {
    GoToTruck();
    YieldTick();
  }
}

record Spot(int Tid, Direction Dir, int Rx, int Ry);
