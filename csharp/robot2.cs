Direction[] DIRS = { Direction.Down, Direction.Up, Direction.Right, Direction.Left };

int W = store.GetWidth();
int H = store.GetHeight();
int SIZE = W * H;

List<Cell> regs = store.FindAll(Entity.Register);
Cell myReg = regs[0];
foreach (Cell r in regs) {
  if (store.GetRegisterNumber(r.GetX(), r.GetY()) == 2) {
    myReg = r;
    break;
  }
}

int regX = myReg.GetX();
int regY = myReg.GetY();
Direction? regSide = store.GetRegisterSide(regX, regY);

Cell? cCell = store.GetCashierCell(regX, regY);
int cashierX = (cCell != null) ? cCell.GetX() : robot.GetX();
int cashierY = (cCell != null) ? cCell.GetY() : robot.GetY();

Cell? pCell = store.GetPackerCell(regX, regY);
int packerX = (pCell != null) ? pCell.GetX() : cashierX + 1;
int packerY = (pCell != null) ? pCell.GetY() : cashierY;

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

bool[] freeMap = new bool[SIZE];
for (int y = 0; y < H; y++) {
  for (int x = 0; x < W; x++) {
    freeMap[y * W + x] = store.IsFree(x, y);
  }
}

int[] dist = new int[SIZE];
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

Dictionary<Product, List<Cell>> rackCells = new();
Dictionary<Product, List<Spot>> pickupSpots = new();
Dictionary<Product, int> fullAtStock = new();

List<Cell> GetRackCells(Product p) {
  if (!rackCells.TryGetValue(p, out var cells)) {
    cells = store.FindAll(p.GetRack());
    rackCells[p] = cells;
  }
  return cells;
}

void EnsurePickupCache(Product p) {
  if (pickupSpots.ContainsKey(p)) return;
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
  pickupSpots[p] = spots;
}

bool IsNightTime() {
  int h = store.GetHour();
  return h >= 22 || h < 6;
}

int GetRackStock(Product p) {
  List<Cell> cells = GetRackCells(p);
  if (cells == null || cells.Count == 0) return -1;
  int total = 0;
  foreach (Cell c in cells) {
    total += store.GetStock(c.GetX(), c.GetY());
  }
  return total;
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

int Bfs(int sx, int sy, int targetId, bool useCanMove, bool useMarked, int[]? tieDist) {
  gen++;
  int head = 0;
  int tail = 0;
  int startId = sy * W + sx;
  visGen[startId] = gen;
  dist[startId] = 0;
  prevDir[startId] = null;
  q[tail++] = startId;

  int bestTid = -1;
  int bestDist = int.MaxValue;
  int bestTie = -1;

  while (head < tail) {
    int cid = q[head++];
    if (dist[cid] >= bestDist) break;
    if (cid == targetId) return cid;
    if (useMarked && tgtGen[cid] == tGen) return cid;

    int cx = cid % W;
    int cy = cid / W;
    int nd = dist[cid] + 1;
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
          dist[nid] = nd;
          prev[nid] = cid;
          prevDir[nid] = d;
          if (nid == targetId) return nid;
          if (useMarked && tgtGen[nid] == tGen) {
            if (tieDist == null) return nid;
            int tie = tieDist[nid];
            if (nd < bestDist || tie > bestTie) {
              bestDist = nd;
              bestTie = tie;
              bestTid = nid;
            }
          }
          if (nd < bestDist) {
            q[tail++] = nid;
          }
        }
      }
    }
  }
  return bestTid;
}

Direction?[] BuildNavTable(int tx, int ty) {
  Direction?[] table = new Direction?[SIZE];
  Bfs(tx, ty, -1, false, false, null);
  int startId = ty * W + tx;
  for (int i = 0; i < SIZE; i++) {
    if (i != startId && visGen[i] == gen && prevDir[i] != null) {
      table[i] = Opposite(prevDir[i]);
    }
  }
  return table;
}

Direction?[] toCashier = BuildNavTable(cashierX, cashierY);
Direction?[] toBag = BuildNavTable(packerX, packerY);
int[] distToBag = (int[])dist.Clone();

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
      return by * W + bx;
    }
  }
  return -1;
}

bool GoTo(int tx, int ty, Direction? faceDir, int initBlocked) {
  bGen++;
  if (initBlocked >= 0) blkGen[initBlocked] = bGen;
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
    Bfs(sx, sy, tid, true, false, null);

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
    blkGen[hit] = bGen;
    MarkAdjBlocked();
    int hx = hit % W;
    int hy = hit / W;
    int rx = robot.GetX();
    int ry = robot.GetY();
    if (hx > rx) lastBlockedDir = Direction.Right;
    else if (hx < rx) lastBlockedDir = Direction.Left;
    else if (hy > ry) lastBlockedDir = Direction.Up;
    else if (hy < ry) lastBlockedDir = Direction.Down;

    retries++;
    if (retries > 4) {
      if (!Sidestep(lastBlockedDir)) YieldTick();
      lastBlockedDir = null;
      retries = 0;
    }
  }
  return false;
}

bool GoToCached(int tx, int ty, Direction? faceDir, Direction?[] table) {
  int tid = ty * W + tx;
  while (true) {
    int cid = robot.GetY() * W + robot.GetX();
    if (cid == tid) {
      if (faceDir != null && robot.GetFacing() != faceDir.Value) robot.Turn(faceDir.Value);
      return true;
    }
    Direction? stepOpt = table[cid];
    if (stepOpt == null) {
      return GoTo(tx, ty, faceDir, -1);
    }
    Direction step = stepOpt.Value;
    if (!robot.CanMove(step) || !robot.Move(step)) {
      int bx = robot.GetX() + GetDx(step);
      int by = robot.GetY() + GetDy(step);
      return GoTo(tx, ty, faceDir, by * W + bx);
    }
  }
}

Product? GoToNearest(List<Product> targets, bool requireStock) {
  tGen++;
  int sx = robot.GetX();
  int sy = robot.GetY();
  int startId = sy * W + sx;

  foreach (Product p in targets) {
    EnsurePickupCache(p);
    foreach (Spot sp in pickupSpots[p]) {
      if (requireStock && store.GetStock(sp.Rx, sp.Ry) <= 0) continue;
      if (sp.Tid == startId) {
        if (robot.GetFacing() != sp.Dir) robot.Turn(sp.Dir);
        return p;
      }
      tgtGen[sp.Tid] = tGen;
      tgtProd[sp.Tid] = p;
      tgtDir[sp.Tid] = sp.Dir;
    }
  }

  int[]? tie = requireStock ? distToBag : null;
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
    int bestTid = Bfs(cx, cy, -1, true, true, tie);
    if (bestTid == -1) break;
    Product? bestP = tgtProd[bestTid];
    Direction? bestDir = tgtDir[bestTid];
    int hit = WalkComputedPath(curId, bestTid);
    if (hit == -1) {
      if (bestDir != null && robot.GetFacing() != bestDir.Value) robot.Turn(bestDir.Value);
      return bestP;
    }
    blkGen[hit] = bGen;
    MarkAdjBlocked();
  }

  int fallbackTid = Bfs(robot.GetX(), robot.GetY(), -1, false, true, tie);
  if (fallbackTid == -1) return null;
  Product? fallbackP = tgtProd[fallbackTid];
  GoTo(fallbackTid % W, fallbackTid / W, tgtDir[fallbackTid], -1);
  return fallbackP;
}

int CarriedCount(Product p) {
  int count = 0;
  foreach (Product item in robot.GetCarried()) {
    if (item == p) count++;
  }
  return count;
}

bool OrderHasRemaining(Order order) {
  foreach (Product p in order.GetProducts()) {
    if (order.GetRemaining(p) > 0) return true;
  }
  return false;
}

bool SafePick(Product p) {
  if (!robot.IsAtPickup() || robot.GetLoad() >= robot.GetCapacity()) return false;
  Shelf? s = robot.GetShelf();
  if (s == null || s.GetProduct() != p || s.IsEmpty()) return false;
  try {
    return robot.Pick(p);
  } catch {
    return false;
  }
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

bool IsReturnable(Product p) {
  if (!fullAtStock.TryGetValue(p, out int fullVal)) return true;
  if (GetRackStock(p) < fullVal) {
    fullAtStock.Remove(p);
    return true;
  }
  return false;
}

void ReturnProducts(List<Product> toReturn) {
  List<Product> pending = new();
  foreach (Product p in toReturn) {
    if (!pending.Contains(p) && IsReturnable(p)) pending.Add(p);
  }
  while (pending.Count > 0) {
    Product? retOpt = GoToNearest(pending, false);
    if (retOpt == null) break;
    Product ret = retOpt.Value;
    while (robot.Has(ret) && SafePutOnShelf(ret)) {}
    if (robot.Has(ret) && robot.IsAtPickup()) {
      fullAtStock[ret] = GetRackStock(ret);
    } else if (!robot.Has(ret)) {
      fullAtStock.Remove(ret);
    }
    pending.Remove(ret);
  }
}

void ReturnStrayCarried() {
  if (robot.GetLoad() == 0) return;
  List<Product> stray = new();
  foreach (Product p in robot.GetCarried()) {
    if (!stray.Contains(p) && IsReturnable(p)) stray.Add(p);
  }
  if (stray.Count > 0) {
    ReturnProducts(stray);
  }
}

void PackOrder(Order order) {
  while (OrderHasRemaining(order) && !order.IsDone()) {
    while (robot.GetLoad() < robot.GetCapacity()) {
      List<Product> neededList = new();
      bool isNight = IsNightTime();
      foreach (Product p in order.GetProducts()) {
        int need = order.GetRemaining(p) - CarriedCount(p);
        if (need <= 0) continue;
        int stock = GetRackStock(p);
        if (stock == -1 || (stock <= 0 && !p.IsInSeason()) || (p.IsAlcohol() && isNight)) {
          if (CarriedCount(p) == 0) order.Remove(p);
          continue;
        }
        if (stock <= 0) continue;
        neededList.Add(p);
      }
      if (neededList.Count == 0) break;

      Product? nextOpt = GoToNearest(neededList, true);
      if (nextOpt == null) break;
      Product next = nextOpt.Value;

      int remNeed = order.GetRemaining(next) - CarriedCount(next);
      bool picked = false;
      while (robot.GetLoad() < robot.GetCapacity() && remNeed > 0 && SafePick(next)) {
        picked = true;
        remNeed--;
      }
      if (robot.IsAtPickup() && !picked && CarriedCount(next) == 0 && !next.IsInSeason()) {
        order.Remove(next);
      }
    }

    bool hasOrderItem = false;
    foreach (Product p in robot.GetCarried()) {
      if (order.GetRemaining(p) > 0) {
        hasOrderItem = true;
        break;
      }
    }

    if (!hasOrderItem) {
      if (OrderHasRemaining(order) && !order.IsDone()) {
        GoToCached(packerX, packerY, regSide, toBag);
        YieldTick();
        continue;
      }
      break;
    }

    if (!GoToCached(packerX, packerY, regSide, toBag) || !robot.IsAtBag()) {
      YieldTick();
      continue;
    }

    bool nightNow = IsNightTime();
    List<Product> toReturn = new();
    foreach (Product p in robot.GetCarried()) {
      if (order.GetRemaining(p) <= 0) continue;
      if (p.IsAlcohol() && nightNow) {
        order.Remove(p);
        if (!toReturn.Contains(p)) toReturn.Add(p);
      } else {
        robot.PutInBag(p);
      }
    }

    if (toReturn.Count > 0) {
      ReturnProducts(toReturn);
    }
  }
}

void ServeCustomer(Order order, Customer customer) {
  bool isNight = IsNightTime();
  foreach (Product p in order.GetProducts()) {
    int stock = GetRackStock(p);
    if ((p.IsAdultsOnly() && !customer.IsAdult()) || (p.IsAlcohol() && isNight) || stock == -1 || (stock <= 0 && !p.IsInSeason())) {
      order.Remove(p);
    }
  }
  if (!order.IsDone()) {
    PackOrder(order);
    order.RemoveRemaining();
  }
}

while (true) {
  ReturnStrayCarried();
  if (!GoToCached(cashierX, cashierY, regSide, toCashier)) {
    YieldTick();
    continue;
  }
  Order? order = robot.TakeOrder();
  Customer? customer = robot.GetCustomer();
  if (order != null && customer != null) {
    ServeCustomer(order, customer);
  } else {
    YieldTick();
  }
}

record Spot(int Tid, Direction Dir, int Rx, int Ry);
