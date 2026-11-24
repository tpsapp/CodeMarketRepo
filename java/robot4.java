Direction[] DIRS = { Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.DOWN };

int W = store.getWidth();
int H = store.getHeight();
int SIZE = W * H;

robot.getList().clear();

Direction gSide = store.getGarageSide();
int gX = store.getGarageX();
int gY = store.getGarageY();

record Spot(int tid, Direction dir, int rx, int ry) {}
record Urg(Product p, int score, int need) {}

void pause(int ms) {
  try {
    Thread.sleep(ms);
  } catch (InterruptedException e) {
    Thread.currentThread().interrupt();
  }
}

int dx(Direction d) {
  return switch (d) {
    case LEFT -> -1;
    case RIGHT -> 1;
    default -> 0;
  };
}

int dy(Direction d) {
  return switch (d) {
    case UP -> 1;
    case DOWN -> -1;
    default -> 0;
  };
}

Direction opp(Direction d) {
  if (d == null) return null;
  return switch (d) {
    case UP -> Direction.DOWN;
    case DOWN -> Direction.UP;
    case LEFT -> Direction.RIGHT;
    case RIGHT -> Direction.LEFT;
  };
}

Direction faceTo(int fx, int fy, int tx, int ty) {
  if (tx > fx) return Direction.RIGHT;
  if (tx < fx) return Direction.LEFT;
  if (ty > fy) return Direction.UP;
  if (ty < fy) return Direction.DOWN;
  return null;
}

boolean[] freeMap = new boolean[SIZE];
for (int y = 0; y < H; y++) {
  for (int x = 0; x < W; x++) {
    freeMap[y * W + x] = store.isFree(x, y);
  }
}

int[] prev = new int[SIZE];
int[] q = new int[SIZE];
int[] visGen = new int[SIZE];
int[] tgtGen = new int[SIZE];
int[] blkGen = new int[SIZE];
Direction[] prevDir = new Direction[SIZE];
Direction[] pathBuf = new Direction[SIZE];
Direction[] tgtDir = new Direction[SIZE];
Product[] tgtProd = new Product[SIZE];
int gen = 0;
int tGen = 0;
int bGen = 0;

HashMap<Product, Integer> rackCap = new HashMap<>();
HashMap<Product, Integer> batchQty = new HashMap<>();
HashMap<Product, Integer> myPicked = new HashMap<>();
HashMap<Product, Integer> r5Hold = new HashMap<>();
HashMap<Product, Integer> r5Base = new HashMap<>();
HashMap<Integer, Integer> fullRack = new HashMap<>();
HashMap<Product, List<Cell>> rackCells = new HashMap<>();
HashMap<Product, ArrayList<Spot>> pickupSpots = new HashMap<>();

boolean truckBusy = false;
boolean breadDivKnown = false;
int r5Min = -1;
int breadDiv = 2;

List<Cell> getRacks(Product p) {
  return rackCells.computeIfAbsent(p, k -> store.findAll(k.getRack()));
}

int rackCount(Product p) {
  var cells = getRacks(p);
  if (cells == null || cells.isEmpty()) return 0;
  return p.getRack() == Entity.BREAD_SHELF ? Math.max(1, cells.size() / 2) : cells.size();
}

int totalCap(Product p) {
  Integer sc = rackCap.get(p);
  return sc == null ? -1 : sc * rackCount(p);
}

int rackStock(Product p) {
  var cells = getRacks(p);
  if (cells == null || cells.isEmpty()) return -1;
  int total = 0;
  for (Cell c : cells) {
    total += store.getStock(c.getX(), c.getY());
  }
  if (p.getRack() == Entity.BREAD_SHELF && breadDiv == 2) {
    total = (total + 1) / 2;
  }
  return total;
}

void learnCap(Product p) {
  if (!robot.isAtPickup()) return;
  Shelf s = robot.getShelf();
  if (s == null || s.getProduct() != p) return;
  rackCap.put(p, s.getCapacity());
  if (p.getRack() == Entity.BREAD_SHELF && !breadDivKnown && s.getCount() > 0 && rackCount(p) == 1) {
    int raw = 0;
    for (Cell c : getRacks(p)) {
      raw += store.getStock(c.getX(), c.getY());
    }
    breadDiv = raw >= s.getCount() * 2 ? 2 : 1;
    breadDivKnown = true;
  }
}

void markAdj() {
  int rx = robot.getX();
  int ry = robot.getY();
  for (Direction d : DIRS) {
    int nx = rx + dx(d);
    int ny = ry + dy(d);
    if (nx >= 0 && nx < W && ny >= 0 && ny < H) {
      int nid = ny * W + nx;
      if (freeMap[nid] && !robot.canMove(d)) {
        blkGen[nid] = bGen;
      }
    }
  }
}

int bfs(int sx, int sy, int targetId, boolean useCanMove, boolean useMarked) {
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
    Direction straight = prevDir[cid];

    for (int k = 0; k < 5; k++) {
      Direction d = k == 0 ? straight : DIRS[k - 1];
      if (d == null || (k > 0 && d == straight)) continue;
      if (useCanMove && cid == startId && !robot.canMove(d)) continue;
      int nx = cx + dx(d);
      int ny = cy + dy(d);
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

Direction[] buildNav(int tx, int ty) {
  var table = new Direction[SIZE];
  bfs(tx, ty, -1, false, false);
  int startId = ty * W + tx;
  for (int i = 0; i < SIZE; i++) {
    if (i != startId && visGen[i] == gen && prevDir[i] != null) {
      table[i] = opp(prevDir[i]);
    }
  }
  return table;
}

Direction[] toTruck = buildNav(gX, gY);

boolean sidestep(Direction blocked) {
  int start = (int) (Math.random() * 4);
  for (int i = 0; i < 4; i++) {
    Direction d = DIRS[(start + i) % 4];
    if (d != blocked && robot.canMove(d)) {
      return robot.move(d);
    }
  }
  return false;
}

int walkPath(int startId, int targetId) {
  int len = 0;
  int curr = targetId;
  while (curr != startId) {
    pathBuf[len++] = prevDir[curr];
    curr = prev[curr];
  }
  for (int i = len - 1; i >= 0; i--) {
    Direction step = pathBuf[i];
    if (!robot.canMove(step) || !robot.move(step)) {
      int bx = robot.getX() + dx(step);
      int by = robot.getY() + dy(step);
      return (bx >= 0 && bx < W && by >= 0 && by < H) ? by * W + bx : -2;
    }
  }
  return -1;
}

boolean goTo(int tx, int ty, Direction faceDir, int initBlocked) {
  bGen++;
  if (initBlocked >= 0 && initBlocked < SIZE) blkGen[initBlocked] = bGen;
  int retries = 0;
  int attempts = 0;
  int tid = ty * W + tx;
  Direction blockedDir = null;

  while (attempts++ < 25) {
    int sx = robot.getX();
    int sy = robot.getY();
    int startId = sy * W + sx;
    if (startId == tid) {
      if (faceDir != null && robot.getFacing() != faceDir) robot.turn(faceDir);
      return true;
    }

    markAdj();
    blkGen[startId] = 0;
    bfs(sx, sy, tid, true, false);

    if (visGen[tid] != gen) {
      bGen++;
      pause(sidestep(blockedDir) ? 40 : 60);
      blockedDir = null;
      continue;
    }

    int hit = walkPath(startId, tid);
    if (hit == -1) {
      if (faceDir != null && robot.getFacing() != faceDir) robot.turn(faceDir);
      return true;
    }
    if (hit >= 0 && hit < SIZE) {
      blkGen[hit] = bGen;
      blockedDir = faceTo(robot.getX(), robot.getY(), hit % W, hit / W);
    }
    markAdj();
    if (++retries > 2) {
      if (!sidestep(blockedDir)) pause(60);
      blockedDir = null;
      retries = 0;
    }
  }
  return false;
}

boolean goToTruck() {
  int tid = gY * W + gX;
  while (true) {
    int cid = robot.getY() * W + robot.getX();
    if (cid == tid) {
      if (gSide != null && robot.getFacing() != gSide) robot.turn(gSide);
      return true;
    }
    Direction step = toTruck[cid];
    if (step == null) return goTo(gX, gY, gSide, -1);
    if (!robot.canMove(step) || !robot.move(step)) {
      int bx = robot.getX() + dx(step);
      int by = robot.getY() + dy(step);
      int bid = (bx >= 0 && bx < W && by >= 0 && by < H) ? by * W + bx : -1;
      return goTo(gX, gY, gSide, bid);
    }
  }
}

boolean cacheSpots(Product p) {
  if (pickupSpots.containsKey(p) && !pickupSpots.get(p).isEmpty()) return true;
  var spots = new ArrayList<Spot>();
  var seen = new HashSet<Long>();
  for (Cell rc : getRacks(p)) {
    for (Direction d : DIRS) {
      int nx = rc.getX() + dx(d);
      int ny = rc.getY() + dy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H && freeMap[ny * W + nx]) {
        int tid = ny * W + nx;
        var pd = store.getPickupDirections(nx, ny, p);
        if (pd != null) {
          for (Direction face : pd) {
            if (seen.add(((long) tid << 3) | face.ordinal())) {
              spots.add(new Spot(tid, face, nx + dx(face), ny + dy(face)));
            }
          }
        }
      }
    }
  }
  if (!spots.isEmpty()) {
    pickupSpots.put(p, spots);
    return true;
  }
  return false;
}

boolean probeSpot(Product p) {
  var spots = new ArrayList<Spot>();
  for (Cell rc : getRacks(p)) {
    for (Direction d : DIRS) {
      int nx = rc.getX() + dx(d);
      int ny = rc.getY() + dy(d);
      if (nx >= 0 && nx < W && ny >= 0 && ny < H && freeMap[ny * W + nx]) {
        if (goTo(nx, ny, faceTo(nx, ny, rc.getX(), rc.getY()), -1)
            && robot.isAtPickup()
            && robot.getShelf() != null
            && robot.getShelf().getProduct() == p) {
          spots.add(new Spot(ny * W + nx, robot.getFacing(), rc.getX(), rc.getY()));
          learnCap(p);
          break;
        }
      }
    }
  }
  if (!spots.isEmpty()) {
    pickupSpots.put(p, spots);
    return true;
  }
  return false;
}

boolean spotFree(Spot sp) {
  int rkey = sp.ry() * W + sp.rx();
  Integer fv = fullRack.get(rkey);
  if (fv == null) return true;
  if (store.getStock(sp.rx(), sp.ry()) < fv) {
    fullRack.remove(rkey);
    return true;
  }
  return false;
}

boolean canUnload(Product p) {
  var spots = pickupSpots.get(p);
  if (spots == null) return false;
  for (Spot sp : spots) {
    if (spotFree(sp)) return true;
  }
  return false;
}

Product goToNearest(List<Product> targets, boolean filterFull) {
  tGen++;
  int sx = robot.getX();
  int sy = robot.getY();
  int startId = sy * W + sx;
  for (Product p : targets) {
    var spots = pickupSpots.get(p);
    if (spots == null) continue;
    for (Spot sp : spots) {
      if (filterFull && !spotFree(sp)) continue;
      if (sp.tid() == startId) {
        if (sp.dir() != null && robot.getFacing() != sp.dir()) robot.turn(sp.dir());
        learnCap(p);
        return p;
      }
      tgtGen[sp.tid()] = tGen;
      tgtProd[sp.tid()] = p;
      tgtDir[sp.tid()] = sp.dir();
    }
  }

  bGen++;
  for (int tries = 0; tries < 6; tries++) {
    int cx = robot.getX();
    int cy = robot.getY();
    int curId = cy * W + cx;
    if (tgtGen[curId] == tGen) {
      Direction d = tgtDir[curId];
      if (d != null && robot.getFacing() != d) robot.turn(d);
      Product p = tgtProd[curId];
      learnCap(p);
      return p;
    }
    markAdj();
    blkGen[curId] = 0;
    int bestTid = bfs(cx, cy, -1, true, true);
    if (bestTid == -1) break;
    Product bestP = tgtProd[bestTid];
    Direction bestDir = tgtDir[bestTid];
    int hit = walkPath(curId, bestTid);
    if (hit == -1) {
      if (bestDir != null && robot.getFacing() != bestDir) robot.turn(bestDir);
      learnCap(bestP);
      return bestP;
    }
    if (hit >= 0 && hit < SIZE) blkGen[hit] = bGen;
    markAdj();
  }

  int bestTid = bfs(robot.getX(), robot.getY(), -1, false, true);
  if (bestTid == -1) return null;
  Product bestP = tgtProd[bestTid];
  if (!goTo(bestTid % W, bestTid / W, tgtDir[bestTid], -1)) return null;
  learnCap(bestP);
  return bestP;
}

var toInspect = new ArrayList<Product>();
var toProbe = new ArrayList<Product>();
var skipInspect = new HashSet<Product>();

void initLists() {
  for (Product p : Product.values()) {
    if (p.isInfinite()) continue;
    var cells = getRacks(p);
    if (cells == null || cells.isEmpty()) continue;
    if (cacheSpots(p)) toInspect.add(p);
    else toProbe.add(p);
  }
}

void inspectOne() {
  if (!toInspect.isEmpty()) {
    var avail = new ArrayList<Product>();
    for (Product p : toInspect) {
      if (!skipInspect.contains(p)) avail.add(p);
    }
    if (avail.isEmpty()) {
      skipInspect.clear();
      if (!toProbe.isEmpty()) {
        Product p = toProbe.remove(0);
        if (!probeSpot(p)) toProbe.add(p);
      } else {
        pause(60);
      }
      return;
    }
    Product next = goToNearest(avail, false);
    if (next != null && rackCap.containsKey(next)) {
      toInspect.remove(next);
      skipInspect.remove(next);
    } else if (next != null) {
      skipInspect.add(next);
      pause(60);
    } else {
      skipInspect.addAll(avail);
      pause(60);
    }
  } else if (!toProbe.isEmpty()) {
    Product p = toProbe.remove(0);
    if (!probeSpot(p)) toProbe.add(p);
  }
}

int urgency(Product p) {
  int cap = totalCap(p);
  if (cap <= 0) return p.getPrice();
  int stock = rackStock(p);
  if (stock <= 0) return 1000000 + p.getPrice();
  return (1000 - (stock * 1000) / cap) * 500 + p.getPrice();
}

int carriedCount(Product p) {
  int c = 0;
  for (Product item : robot.getCarried()) {
    if (item == p) c++;
  }
  return c;
}

void updateR5Hold(int nowMin) {
  if (!store.isTruckAway()) {
    truckBusy = true;
    return;
  }
  if (truckBusy) {
    truckBusy = false;
    r5Hold.clear();
    r5Base.clear();
    int remCap = robot.getCapacity();
    for (var e : batchQty.entrySet()) {
      if (remCap <= 0) break;
      Product p = e.getKey();
      int rem = e.getValue() - myPicked.getOrDefault(p, 0);
      int cur = rackStock(p);
      int tc = totalCap(p);
      if (rem > 0 && (tc <= 0 || cur < tc)) {
        int hold = Math.min(rem, remCap);
        r5Hold.put(p, hold);
        r5Base.put(p, cur);
        remCap -= hold;
      }
    }
    batchQty.clear();
    myPicked.clear();
    r5Min = r5Hold.isEmpty() ? -1 : nowMin;
  }
  if (r5Min == -1) return;
  if ((nowMin - r5Min + 1440) % 1440 >= 25) {
    r5Hold.clear();
    r5Base.clear();
    r5Min = -1;
    return;
  }
  r5Hold.entrySet().removeIf(e -> {
    Product p = e.getKey();
    int cur = rackStock(p);
    int tc = totalCap(p);
    if (tc > 0 && cur >= tc) {
      r5Base.remove(p);
      return true;
    }
    int base = r5Base.getOrDefault(p, cur);
    if (cur <= base) return false;
    int left = e.getValue() - (cur - base);
    if (left <= 0) {
      r5Base.remove(p);
      return true;
    }
    e.setValue(left);
    r5Base.put(p, cur);
    return false;
  });
  if (r5Hold.isEmpty()) r5Min = -1;
}

void syncAndOrder() {
  updateR5Hold(store.getHour() * 60 + store.getMinute());
  if (!store.isTruckAway()) return;

  var list = robot.getList();
  list.clear();
  var cands = new ArrayList<Urg>();
  for (Product p : Product.values()) {
    if (p.isInfinite() || !p.isInSeason()) continue;
    int cap = totalCap(p);
    int stock = rackStock(p);
    if (cap <= 0 || stock == -1) continue;
    int onWay = store.getRemaining(p) + carriedCount(p) + r5Hold.getOrDefault(p, 0);
    int need = cap - (stock + onWay);
    if (need > 0) {
      cands.add(new Urg(p, urgency(p), need));
    }
  }

  cands.sort((a, b) -> Integer.compare(b.score(), a.score()));

  for (Urg u : cands) {
    Product p = u.p();
    int need = u.need();
    while (need-- > 0) list.add(p);
  }

  if (!list.isEmpty()) {
    int count = Math.min(list.size(), store.getTruckCapacity());
    var batch = new ArrayList<>(list.subList(0, count));
    if (store.callDelivery(batch)) {
      batchQty.clear();
      myPicked.clear();
      for (Product p : batch) {
        batchQty.put(p, batchQty.getOrDefault(p, 0) + 1);
      }
      truckBusy = true;
      list.subList(0, count).clear();
    }
  }
}

boolean truckHasItems() {
  for (Product p : Product.values()) {
    if (store.getRemaining(p) > 0) return true;
  }
  return false;
}

boolean hasUnloadable() {
  for (Product p : robot.getCarried()) {
    if (canUnload(p)) return true;
  }
  return false;
}

boolean safePut(Product p) {
  if (!robot.isAtPickup() || !robot.has(p)) return false;
  Shelf s = robot.getShelf();
  if (s == null || s.getProduct() != p || s.isFull()) return false;
  try {
    return robot.putOnShelf(p);
  } catch (Exception e) {
    return false;
  }
}

boolean safePickTruck(Product p) {
  if (!robot.isAtTruck() || robot.getLoad() >= robot.getCapacity() || store.getRemaining(p) <= 0) return false;
  try {
    if (robot.pick(p)) {
      myPicked.put(p, myPicked.getOrDefault(p, 0) + 1);
      return true;
    }
    return false;
  } catch (Exception e) {
    return false;
  }
}

void unloadCarried() {
  while (true) {
    var carried = new ArrayList<Product>();
    for (Product p : robot.getCarried()) {
      if (!carried.contains(p) && canUnload(p)) carried.add(p);
    }
    if (carried.isEmpty()) break;

    Product next = goToNearest(carried, true);
    if (next == null) break;
    while (robot.has(next) && safePut(next)) {}
    Shelf s = robot.getShelf();
    if (robot.has(next) && robot.isAtPickup() && s != null && s.getProduct() == next) {
      int rx = robot.getX() + dx(robot.getFacing());
      int ry = robot.getY() + dy(robot.getFacing());
      fullRack.put(ry * W + rx, store.getStock(rx, ry));
    }
  }
}

void restockFromTruck() {
  while (truckHasItems() && robot.getLoad() < robot.getCapacity()) {
    if (!goToTruck()) {
      sidestep(null);
      pause(60);
      return;
    }
    while (!robot.isAtTruck()) {
      if (store.isTruckAway() || robot.getX() != gX || robot.getY() != gY) return;
      pause(50);
    }

    var items = new ArrayList<Urg>();
    for (Product p : Product.values()) {
      int rem = store.getRemaining(p);
      if (rem > 0) {
        items.add(new Urg(p, urgency(p), rem));
      }
    }
    items.sort((a, b) -> Integer.compare(b.score(), a.score()));

    int before = robot.getLoad();
    for (Urg u : items) {
      Product p = u.p();
      while (robot.getLoad() < robot.getCapacity() && store.getRemaining(p) > 0 && safePickTruck(p)) {}
      if (robot.getLoad() >= robot.getCapacity()) break;
    }

    if (robot.getLoad() == before) break;
    unloadCarried();
    if (robot.getLoad() >= robot.getCapacity()) break;
  }
}

initLists();

while (true) {
  syncAndOrder();
  if (hasUnloadable()) {
    unloadCarried();
  } else if (store.isTruckHere() && truckHasItems() && robot.getLoad() < robot.getCapacity()) {
    restockFromTruck();
  } else if (!toInspect.isEmpty() || !toProbe.isEmpty()) {
    inspectOne();
  } else {
    goToTruck();
    pause(60);
  }
}
