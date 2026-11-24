Direction[] DIRS = { Direction.Left, Direction.Right, Direction.Up, Direction.Down };

int W = store.GetWidth();
int H = store.GetHeight();
int SIZE = W * H;

robot.GetList().Clear();

Direction? gSide = store.GetGarageSide();
int gX = store.GetGarageX();
int gY = store.GetGarageY();

void YieldTick() {
  robot.Turn(robot.GetFacing());
}

int Dx(Direction d) => d switch {
  Direction.Left => -1,
  Direction.Right => 1,
  _ => 0
};

int Dy(Direction d) => d switch {
  Direction.Up => 1,
  Direction.Down => -1,
  _ => 0
};

Direction? Opp(Direction? d) => d switch {
  Direction.Up => Direction.Down,
  Direction.Down => Direction.Up,
  Direction.Left => Direction.Right,
  Direction.Right => Direction.Left,
  _ => null
};

Direction? FaceTo(int fx, int fy, int tx, int ty) {
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

int[] prev = new int[SIZE];
int[] q = new int[SIZE];
int[] visGen = new int[SIZE];
int[] tgtGen = new int[SIZE];
int[] blkGen = new int[SIZE];
Direction?[] prevDir = new Direction?[SIZE];
Direction[] pathBuf = new Direction[SIZE];
Direction?[] tgtDir = new Direction?[SIZE];
Product?[] tgtProd = new Product?[SIZE];
int gen = 0;
int tGen = 0;
int bGen = 0;

Dictionary<Product, int> rackCap = new();
Dictionary<Product, int> batchQty = new();
Dictionary<Product, int> myPicked = new();
Dictionary<Product, int> r5Hold = new();
Dictionary<Product, int> r5Base = new();
Dictionary<int, int> fullRack = new();
Dictionary<Product, List<Cell>> rackCells = new();
Dictionary<Product, List<Spot>> pickupSpots = new();

bool truckBusy = false;
bool breadDivKnown = false;
int r5Min = -1;
int breadDiv = 2;

List<Cell> GetRacks(Product p) {
  if (!rackCells.TryGetValue(p, out var cells)) {
    cells = store.FindAll(p.GetRack());
    rackCells[p] = cells;
  }
  return cells;
}

int RackCount(Product p) {
  var cells = GetRacks(p);
  if (cells == null || cells.Count == 0) return 0;
  return p.GetRack() == Entity.BreadShelf ? Math.Max(1, cells.Count / 2) : cells.Count;
}

int TotalCap(Product p) {
  return rackCap.TryGetValue(p, out int sc) ? sc * RackCount(p) : -1;
}

int RackStock(Product p) {
  var cells = GetRacks(p);
  if (cells == null || cells.Count == 0) return -1;
  int total = 0;
  foreach (Cell c in cells) {
    total += store.GetStock(c.GetX(), c.GetY());
  }
  if (p.GetRack() == Entity.BreadShelf && breadDiv == 2) {
    total = (total + 1) / 2;
  }
  return total;
}

void LearnCap(Product p) {
  if (!robot.IsAtPickup()) return;
  Shelf? s = robot.GetShelf();
  if (s == null || s.GetProduct() != p) return;
  rackCap[p] = s.GetCapacity();
  if (p.GetRack() == Entity.BreadShelf && !breadDivKnown && s.GetCount() > 0 && RackCount(p) == 1) {
    int raw = 0;
    foreach (Cell c in GetRacks(p)) {
      raw += store.GetStock(c.GetX(), c.GetY());
    }
    breadDiv = raw >= s.GetCount() * 2 ? 2 : 1;
    breadDivKnown = true;
  }
}

void MarkAdj() {
  int rx = robot.GetX();
  int ry = robot.GetY();
  foreach (Direction d in DIRS) {
    int nx = rx + Dx(d);
    int ny = ry + Dy(d);
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
      Direction? dOpt = k == 0 ? straight : DIRS[k - 1];
      if (dOpt == null || (k > 0 && dOpt == straight)) continue;
      Direction d = dOpt.Value;
      if (useCanMove && cid == startId && !robot.CanMove(d)) continue;
      int nx = cx + Dx(d);
      int ny = cy + Dy(d);
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

Direction?[] BuildNav(int tx, int ty) {
  var table = new Direction?[SIZE];
  Bfs(tx, ty, -1, false, false);
  int startId = ty * W + tx;
  for (int i = 0; i < SIZE; i++) {
    if (i != startId && visGen[i] == gen && prevDir[i] != null) {
      table[i] = Opp(prevDir[i]);
    }
  }
  return table;
}

Direction?[] toTruck = BuildNav(gX, gY);

bool Sidestep(Direction? blocked) {
  int start = Random.Shared.Next(4);
  for (int i = 0; i < 4; i++) {
    Direction d = DIRS[(start + i) % 4];
    if (d != blocked && robot.CanMove(d)) {
      return robot.Move(d);
    }
  }
  return false;
}

int WalkPath(int startId, int targetId) {
  int len = 0;
  int curr = targetId;
  while (curr != startId) {
    pathBuf[len++] = prevDir[curr]!.Value;
    curr = prev[curr];
  }
  for (int i = len - 1; i >= 0; i--) {
    Direction step = pathBuf[i];
    if (!robot.CanMove(step) || !robot.Move(step)) {
      int bx = robot.GetX() + Dx(step);
      int by = robot.GetY() + Dy(step);
      return (bx >= 0 && bx < W && by >= 0 && by < H) ? by * W + bx : -2;
    }
  }
  return -1;
}

bool GoTo(int tx, int ty, Direction? faceDir, int initBlocked) {
  bGen++;
  if (initBlocked >= 0 && initBlocked < SIZE) blkGen[initBlocked] = bGen;
  int retries = 0;
  int attempts = 0;
  int tid = ty * W + tx;
  Direction? blockedDir = null;

  while (attempts++ < 25) {
    int sx = robot.GetX();
    int sy = robot.GetY();
    int startId = sy * W + sx;
    if (startId == tid) {
      if (faceDir != null && robot.GetFacing() != faceDir.Value) robot.Turn(faceDir.Value);
      return true;
    }

    MarkAdj();
    blkGen[startId] = 0;
    Bfs(sx, sy, tid, true, false);

    if (visGen[tid] != gen) {
      bGen++;
      if (!Sidestep(blockedDir)) YieldTick();
      blockedDir = null;
      continue;
    }

    int hit = WalkPath(startId, tid);
    if (hit == -1) {
      if (faceDir != null && robot.GetFacing() != faceDir.Value) robot.Turn(faceDir.Value);
      return true;
    }
    if (hit >= 0 && hit < SIZE) {
      blkGen[hit] = bGen;
      blockedDir = FaceTo(robot.GetX(), robot.GetY(), hit % W, hit / W);
    }
    MarkAdj();
    if (++retries > 2) {
      if (!Sidestep(blockedDir)) YieldTick();
      blockedDir = null;
      retries = 0;
    }
  }
  return false;
}

bool GoToTruck() {
  int tid = gY * W + gX;
  while (true) {
    int cid = robot.GetY() * W + robot.GetX();
    if (cid == tid) {
      if (gSide != null && robot.GetFacing() != gSide.Value) robot.Turn(gSide.Value);
      return true;
    }
    Direction? stepOpt = toTruck[cid];
    if (stepOpt == null) return GoTo(gX, gY, gSide, -1);
    Direction step = stepOpt.Value;
    if (!robot.CanMove(step) || !robot.Move(step)) {
      int bx = robot.GetX() + Dx(step);
      int by = robot.GetY() + Dy(step);
      int bid = (bx >= 0 && bx < W && by >= 0 && by < H) ? by * W + bx : -1;
      return GoTo(gX, gY, gSide, bid);
    }
  }
}

bool CacheSpots(Product p) {
  if (pickupSpots.TryGetValue(p, out var existing) && existing.Count > 0) return true;
  var spots = new List<Spot>();
  var seen = new HashSet<long>();
  foreach (Cell rc in GetRacks(p)) {
    foreach (Direction d in DIRS) {
      int nx = rc.GetX() + Dx(d);
      int ny = rc.GetY() + Dy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H && freeMap[ny * W + nx]) {
        int tid = ny * W + nx;
        var pd = store.GetPickupDirections(nx, ny, p);
        if (pd != null) {
          foreach (Direction face in pd) {
            if (seen.Add(((long)tid << 3) | (uint)(int)face)) {
              spots.Add(new Spot(tid, face, nx + Dx(face), ny + Dy(face)));
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

bool ProbeSpot(Product p) {
  var spots = new List<Spot>();
  foreach (Cell rc in GetRacks(p)) {
    foreach (Direction d in DIRS) {
      int nx = rc.GetX() + Dx(d);
      int ny = rc.GetY() + Dy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H && freeMap[ny * W + nx]) {
        if (GoTo(nx, ny, FaceTo(nx, ny, rc.GetX(), rc.GetY()), -1)
            && robot.IsAtPickup()
            && robot.GetShelf() != null
            && robot.GetShelf()!.GetProduct() == p) {
          spots.Add(new Spot(ny * W + nx, robot.GetFacing(), rc.GetX(), rc.GetY()));
          LearnCap(p);
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

bool SpotFree(Spot sp) {
  int rkey = sp.Ry * W + sp.Rx;
  if (!fullRack.TryGetValue(rkey, out int fv)) return true;
  if (store.GetStock(sp.Rx, sp.Ry) < fv) {
    fullRack.Remove(rkey);
    return true;
  }
  return false;
}

bool CanUnload(Product p) {
  if (!pickupSpots.TryGetValue(p, out var spots)) return false;
  foreach (Spot sp in spots) {
    if (SpotFree(sp)) return true;
  }
  return false;
}

Product? GoToNearest(List<Product> targets, bool filterFull) {
  tGen++;
  int sx = robot.GetX();
  int sy = robot.GetY();
  int startId = sy * W + sx;
  foreach (Product p in targets) {
    if (!pickupSpots.TryGetValue(p, out var spots)) continue;
    foreach (Spot sp in spots) {
      if (filterFull && !SpotFree(sp)) continue;
      if (sp.Tid == startId) {
        if (robot.GetFacing() != sp.Dir) robot.Turn(sp.Dir);
        LearnCap(p);
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
      Product? p = tgtProd[curId];
      if (p != null) LearnCap(p.Value);
      return p;
    }
    MarkAdj();
    blkGen[curId] = 0;
    int bestTid = Bfs(cx, cy, -1, true, true);
    if (bestTid == -1) break;
    Product? bestP = tgtProd[bestTid];
    Direction? bestDir = tgtDir[bestTid];
    int hit = WalkPath(curId, bestTid);
    if (hit == -1) {
      if (bestDir != null && robot.GetFacing() != bestDir.Value) robot.Turn(bestDir.Value);
      if (bestP != null) LearnCap(bestP.Value);
      return bestP;
    }
    if (hit >= 0 && hit < SIZE) blkGen[hit] = bGen;
    MarkAdj();
  }

  int fallbackTid = Bfs(robot.GetX(), robot.GetY(), -1, false, true);
  if (fallbackTid == -1) return null;
  Product? fallbackP = tgtProd[fallbackTid];
  if (!GoTo(fallbackTid % W, fallbackTid / W, tgtDir[fallbackTid], -1)) return null;
  if (fallbackP != null) LearnCap(fallbackP.Value);
  return fallbackP;
}

var toInspect = new List<Product>();
var toProbe = new List<Product>();
var skipInspect = new HashSet<Product>();

void InitLists() {
  foreach (Product p in Enum.GetValues<Product>()) {
    if (p.IsInfinite()) continue;
    var cells = GetRacks(p);
    if (cells == null || cells.Count == 0) continue;
    if (CacheSpots(p)) toInspect.Add(p);
    else toProbe.Add(p);
  }
}

void InspectOne() {
  if (toInspect.Count > 0) {
    var avail = new List<Product>();
    foreach (Product p in toInspect) {
      if (!skipInspect.Contains(p)) avail.Add(p);
    }
    if (avail.Count == 0) {
      skipInspect.Clear();
      if (toProbe.Count > 0) {
        Product p = toProbe[0];
        toProbe.RemoveAt(0);
        if (!ProbeSpot(p)) toProbe.Add(p);
      } else {
        YieldTick();
      }
      return;
    }
    Product? next = GoToNearest(avail, false);
    if (next != null && rackCap.ContainsKey(next.Value)) {
      toInspect.Remove(next.Value);
      skipInspect.Remove(next.Value);
    } else if (next != null) {
      skipInspect.Add(next.Value);
      YieldTick();
    } else {
      foreach (Product p in avail) skipInspect.Add(p);
      YieldTick();
    }
  } else if (toProbe.Count > 0) {
    Product p = toProbe[0];
    toProbe.RemoveAt(0);
    if (!ProbeSpot(p)) toProbe.Add(p);
  }
}

int Urgency(Product p) {
  int cap = TotalCap(p);
  if (cap <= 0) return p.GetPrice();
  int stock = RackStock(p);
  if (stock <= 0) return 1000000 + p.GetPrice();
  return (1000 - (stock * 1000) / cap) * 500 + p.GetPrice();
}

int CarriedCount(Product p) {
  int c = 0;
  foreach (Product item in robot.GetCarried()) {
    if (item == p) c++;
  }
  return c;
}

void UpdateR5Hold(int nowMin) {
  if (!store.IsTruckAway()) {
    truckBusy = true;
    return;
  }
  if (truckBusy) {
    truckBusy = false;
    r5Hold.Clear();
    r5Base.Clear();
    int remCap = robot.GetCapacity();
    foreach (var e in batchQty) {
      if (remCap <= 0) break;
      Product p = e.Key;
      int rem = e.Value - myPicked.GetValueOrDefault(p, 0);
      int cur = RackStock(p);
      int tc = TotalCap(p);
      if (rem > 0 && (tc <= 0 || cur < tc)) {
        int hold = Math.Min(rem, remCap);
        r5Hold[p] = hold;
        r5Base[p] = cur;
        remCap -= hold;
      }
    }
    batchQty.Clear();
    myPicked.Clear();
    r5Min = r5Hold.Count == 0 ? -1 : nowMin;
  }
  if (r5Min == -1) return;
  if ((nowMin - r5Min + 1440) % 1440 >= 25) {
    r5Hold.Clear();
    r5Base.Clear();
    r5Min = -1;
    return;
  }
  foreach (Product p in r5Hold.Keys.ToList()) {
    int cur = RackStock(p);
    int tc = TotalCap(p);
    if (tc > 0 && cur >= tc) {
      r5Hold.Remove(p);
      r5Base.Remove(p);
      continue;
    }
    int b = r5Base.GetValueOrDefault(p, cur);
    if (cur <= b) continue;
    int left = r5Hold[p] - (cur - b);
    if (left <= 0) {
      r5Hold.Remove(p);
      r5Base.Remove(p);
    } else {
      r5Hold[p] = left;
      r5Base[p] = cur;
    }
  }
  if (r5Hold.Count == 0) r5Min = -1;
}

void SyncAndOrder() {
  UpdateR5Hold(store.GetHour() * 60 + store.GetMinute());
  if (!store.IsTruckAway()) return;

  var list = robot.GetList();
  list.Clear();
  var cands = new List<Urg>();
  foreach (Product p in Enum.GetValues<Product>()) {
    if (p.IsInfinite() || !p.IsInSeason()) continue;
    int cap = TotalCap(p);
    int stock = RackStock(p);
    if (cap <= 0 || stock == -1) continue;
    int onWay = store.GetRemaining(p) + CarriedCount(p) + r5Hold.GetValueOrDefault(p, 0);
    if (cap - (stock + onWay) > 0) {
      cands.Add(new Urg(p, Urgency(p)));
    }
  }

  cands.Sort((a, b) => b.Score.CompareTo(a.Score));

  foreach (Urg u in cands) {
    Product p = u.P;
    int need = TotalCap(p) - (RackStock(p) + store.GetRemaining(p) + CarriedCount(p) + r5Hold.GetValueOrDefault(p, 0));
    while (need-- > 0) list.Add(p);
  }

  if (store.IsTruckAway() && list.Count > 0) {
    int count = Math.Min(list.Count, store.GetTruckCapacity());
    var batch = list.GetRange(0, count);
    if (store.CallDelivery(batch)) {
      batchQty.Clear();
      myPicked.Clear();
      foreach (Product p in batch) {
        batchQty[p] = batchQty.GetValueOrDefault(p, 0) + 1;
      }
      truckBusy = true;
      list.RemoveRange(0, count);
    }
  }
}

bool TruckHasItems() {
  foreach (Product p in Enum.GetValues<Product>()) {
    if (store.GetRemaining(p) > 0) return true;
  }
  return false;
}

bool HasUnloadable() {
  foreach (Product p in robot.GetCarried()) {
    if (CanUnload(p)) return true;
  }
  return false;
}

bool SafePut(Product p) {
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
    if (robot.Pick(p)) {
      myPicked[p] = myPicked.GetValueOrDefault(p, 0) + 1;
      return true;
    }
    return false;
  } catch {
    return false;
  }
}

void UnloadCarried() {
  while (true) {
    var carried = new List<Product>();
    foreach (Product p in robot.GetCarried()) {
      if (!carried.Contains(p) && CanUnload(p)) carried.Add(p);
    }
    if (carried.Count == 0) break;

    Product? nextOpt = GoToNearest(carried, true);
    if (nextOpt == null) break;
    Product next = nextOpt.Value;
    while (robot.Has(next) && SafePut(next)) {}
    Shelf? s = robot.GetShelf();
    if (robot.Has(next) && robot.IsAtPickup() && s != null && s.GetProduct() == next) {
      int rx = robot.GetX() + Dx(robot.GetFacing());
      int ry = robot.GetY() + Dy(robot.GetFacing());
      fullRack[ry * W + rx] = store.GetStock(rx, ry);
    }
  }
}

void RestockFromTruck() {
  while (TruckHasItems() && robot.GetLoad() < robot.GetCapacity()) {
    if (!GoToTruck()) {
      Sidestep(null);
      YieldTick();
      return;
    }
    while (!robot.IsAtTruck()) {
      if (store.IsTruckAway() || robot.GetX() != gX || robot.GetY() != gY) return;
      YieldTick();
    }

    var items = new List<Urg>();
    foreach (Product p in Enum.GetValues<Product>()) {
      if (store.GetRemaining(p) > 0) {
        items.Add(new Urg(p, Urgency(p)));
      }
    }
    items.Sort((a, b) => b.Score.CompareTo(a.Score));

    int before = robot.GetLoad();
    foreach (Urg u in items) {
      Product p = u.P;
      while (robot.GetLoad() < robot.GetCapacity() && store.GetRemaining(p) > 0 && SafePickTruck(p)) {}
      if (robot.GetLoad() >= robot.GetCapacity()) break;
    }

    if (robot.GetLoad() == before) break;
    UnloadCarried();
    if (robot.GetLoad() >= robot.GetCapacity()) break;
  }
}

InitLists();

while (true) {
  SyncAndOrder();
  if (HasUnloadable()) {
    UnloadCarried();
  } else if (store.IsTruckHere() && TruckHasItems() && robot.GetLoad() < robot.GetCapacity()) {
    RestockFromTruck();
  } else if (toInspect.Count > 0 || toProbe.Count > 0) {
    InspectOne();
  } else {
    GoToTruck();
    YieldTick();
  }
}

record Spot(int Tid, Direction Dir, int Rx, int Ry);
record Urg(Product P, int Score);
