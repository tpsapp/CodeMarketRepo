Direction[]DIRS={Direction.RIGHT,Direction.LEFT,Direction.DOWN,Direction.UP};int W=store.getWidth();
int H=store.getHeight();int SIZE=W * H;Direction garageSide=store.getGarageSide();
int gx0=store.getGarageX();int gy0=store.getGarageY();int garageX=gx0;int garageY=gy0;
record Spot(int tid,Direction dir,int rx,int ry){}void pause(int ms){try{Thread.sleep(ms);
}catch(InterruptedException e){Thread.currentThread().interrupt();}}int getDx(Direction d){
return switch(d){case LEFT->-1;case RIGHT->1;default->0;};}int getDy(Direction d){return switch(d){
case UP->1;case DOWN->-1;default->0;};}Direction opposite(Direction d){if(d==null)return null;
return switch(d){case UP->Direction.DOWN;case DOWN->Direction.UP;case LEFT->Direction.RIGHT;
case RIGHT->Direction.LEFT;};}Direction faceToward(int fx,int fy,int tx,int ty){
if(tx > fx)return Direction.RIGHT;if(tx < fx)return Direction.LEFT;if(ty > fy)return Direction.UP;
if(ty < fy)return Direction.DOWN;return null;}boolean[]freeMap=new boolean[SIZE];for(int y=0;y < H;y++){
for(int x=0;x < W;x++){freeMap[y * W + x]=store.isFree(x,y);}}
boolean horizontalDoor=(garageSide==Direction.UP||garageSide==Direction.DOWN);int sx0=robot.getX();
int sy0=robot.getY();if(horizontalDoor&&sy0==gy0&&Math.abs(sx0 - gx0)==1){garageX=sx0;garageY=sy0;
}else if(!horizontalDoor&&sx0==gx0&&Math.abs(sy0 - gy0)==1){garageX=sx0;garageY=sy0;
}else if(horizontalDoor){if(gx0 + 1 < W&&freeMap[gy0 * W +(gx0 + 1)])garageX=gx0 + 1;
else if(gx0 - 1>=0&&freeMap[gy0 * W +(gx0 - 1)])garageX=gx0 - 1;}else{
if(gy0 + 1 < H&&freeMap[(gy0 + 1)* W + gx0])garageY=gy0 + 1;
else if(gy0 - 1>=0&&freeMap[(gy0 - 1)* W + gx0])garageY=gy0 - 1;}int[]prev=new int[SIZE];
Direction[]prevDir=new Direction[SIZE];Direction[]pathBuf=new Direction[SIZE];int[]q=new int[SIZE];
int[]visGen=new int[SIZE];int[]blkGen=new int[SIZE];int gen=0;int bGen=0;int[]tgtGen=new int[SIZE];
Product[]tgtProd=new Product[SIZE];Direction[]tgtDir=new Direction[SIZE];int tGen=0;
HashMap<Integer,Integer> fullRackStock=new HashMap<>();
HashMap<Product,List<Cell>> rackCells=new HashMap<>();
HashMap<Product,ArrayList<Spot>> pickupSpots=new HashMap<>();List<Cell> getRackCells(Product p){
return rackCells.computeIfAbsent(p,k->store.findAll(k.getRack()));}void markAdjBlocked(){
int rx=robot.getX(),ry=robot.getY();for(Direction d:DIRS){int nx=rx + getDx(d),ny=ry + getDy(d);
if(nx>=0&&nx < W&&ny>=0&&ny < H){int nid=ny * W + nx;if(freeMap[nid]&&!robot.canMove(d))blkGen[nid]=bGen;
}}}int bfs(int sx,int sy,int targetId,boolean useCanMove,boolean useMarked){gen++;int head=0;int tail=0;
int startId=sy * W + sx;visGen[startId]=gen;prevDir[startId]=null;q[tail++]=startId;while(head < tail){
int cid=q[head++];if(cid==targetId||(useMarked&&tgtGen[cid]==tGen))return cid;int cx=cid % W;
int cy=cid / W;Direction straight=prevDir[cid];for(int k=0;k < 5;k++){
Direction d=(k==0)?straight:DIRS[k - 1];if(d==null||(k > 0&&d==straight))continue;
if(useCanMove&&cid==startId&&!robot.canMove(d))continue;int nx=cx + getDx(d);int ny=cy + getDy(d);
if(nx>=0&&nx < W&&ny>=0&&ny < H){int nid=ny * W + nx;
if((!useCanMove||blkGen[nid]!=bGen)&&visGen[nid]!=gen&&freeMap[nid]){visGen[nid]=gen;prev[nid]=cid;
prevDir[nid]=d;if(nid==targetId||(useMarked&&tgtGen[nid]==tGen))return nid;q[tail++]=nid;}}}}return -1;}
Direction[]buildNavTable(int tx,int ty){Direction[]table=new Direction[SIZE];bfs(tx,ty,-1,false,false);
int startId=ty * W + tx;for(int i=0;i < SIZE;i++){if(i!=startId&&visGen[i]==gen&&prevDir[i]!=null){
table[i]=opposite(prevDir[i]);}}return table;}Direction[]toTruck=buildNavTable(garageX,garageY);
boolean sidestep(Direction blockedDir){int start=(int)(Math.random()* 4);for(int i=0;i < 4;i++){
Direction d=DIRS[(start + i)% 4];if(d!=blockedDir&&robot.canMove(d)){return robot.move(d);}}return false;
}int walkComputedPath(int startId,int targetId){int len=0;int curr=targetId;while(curr!=startId){
pathBuf[len++]=prevDir[curr];curr=prev[curr];}for(int i=len - 1;i>=0;i--){Direction step=pathBuf[i];
if(!robot.canMove(step)||!robot.move(step)){int bx=robot.getX()+ getDx(step);
int by=robot.getY()+ getDy(step);return by * W + bx;}}return -1;}
boolean goTo(int tx,int ty,Direction faceDir,int initBlocked){bGen++;
if(initBlocked>=0)blkGen[initBlocked]=bGen;Direction lastBlockedDir=null;int retries=0;
int totalAttempts=0;int tid=ty * W + tx;while(totalAttempts < 25){totalAttempts++;int sx=robot.getX();
int sy=robot.getY();int startId=sy * W + sx;if(startId==tid){
if(faceDir!=null&&robot.getFacing()!=faceDir)robot.turn(faceDir);return true;}markAdjBlocked();
blkGen[startId]=0;bfs(sx,sy,tid,true,false);if(visGen[tid]!=gen){bGen++;
if(!sidestep(lastBlockedDir))pause(60);else pause(40);lastBlockedDir=null;continue;}
int hit=walkComputedPath(startId,tid);if(hit==-1){
if(faceDir!=null&&robot.getFacing()!=faceDir)robot.turn(faceDir);return true;}blkGen[hit]=bGen;
markAdjBlocked();int hx=hit % W;int hy=hit / W;int rx=robot.getX();int ry=robot.getY();
if(hx > rx)lastBlockedDir=Direction.RIGHT;else if(hx < rx)lastBlockedDir=Direction.LEFT;
else if(hy > ry)lastBlockedDir=Direction.UP;else if(hy < ry)lastBlockedDir=Direction.DOWN;retries++;
if(retries > 1){if(!sidestep(lastBlockedDir))pause(60);lastBlockedDir=null;retries=0;}}return false;}
boolean goToTruck(){int tid=garageY * W + garageX;while(true){int cid=robot.getY()* W + robot.getX();
if(cid==tid){if(garageSide!=null&&robot.getFacing()!=garageSide)robot.turn(garageSide);return true;}
Direction step=toTruck[cid];if(step==null){return goTo(garageX,garageY,garageSide,-1);}
if(!robot.canMove(step)||!robot.move(step)){int bx=robot.getX()+ getDx(step);
int by=robot.getY()+ getDy(step);return goTo(garageX,garageY,garageSide,by * W + bx);}}}
boolean cachePickupDirections(Product p){
if(pickupSpots.containsKey(p)&&!pickupSpots.get(p).isEmpty())return true;
ArrayList<Spot> spots=new ArrayList<>();HashSet<Long> seen=new HashSet<>();for(Cell rc:getRackCells(p)){
for(Direction d:DIRS){int nx=rc.getX()+ getDx(d);int ny=rc.getY()+ getDy(d);
if(nx>=0&&nx < W&&ny>=0&&ny < H){int tid=ny * W + nx;if(freeMap[tid]){
List<Direction> pd=store.getPickupDirections(nx,ny,p);if(pd!=null){for(Direction face:pd){
long key=((long)tid << 3)| face.ordinal();if(seen.add(key)){
spots.add(new Spot(tid,face,nx + getDx(face),ny + getDy(face)));}}}}}}}if(!spots.isEmpty()){
pickupSpots.put(p,spots);return true;}return false;}boolean probeSingleSpot(Product p){
ArrayList<Spot> spots=new ArrayList<>();for(Cell rc:getRackCells(p)){for(Direction d:DIRS){
int nx=rc.getX()+ getDx(d);int ny=rc.getY()+ getDy(d);
if(nx>=0&&nx < W&&ny>=0&&ny < H&&freeMap[ny * W + nx]){
Direction face=faceToward(nx,ny,rc.getX(),rc.getY());
if(goTo(nx,ny,face,-1)&&robot.isAtPickup()&&robot.getShelf()!=null&&robot.getShelf().getProduct()==p){
spots.add(new Spot(ny * W + nx,robot.getFacing(),rc.getX(),rc.getY()));break;}}}}if(!spots.isEmpty()){
pickupSpots.put(p,spots);return true;}return false;}void ensureSpots(Product p){
if(pickupSpots.containsKey(p)&&!pickupSpots.get(p).isEmpty())return;if(!cachePickupDirections(p)){
probeSingleSpot(p);}}boolean isSpotUnloadable(Spot sp){int rkey=sp.ry()* W + sp.rx();
Integer fullVal=fullRackStock.get(rkey);if(fullVal==null)return true;
if(store.getStock(sp.rx(),sp.ry())< fullVal){fullRackStock.remove(rkey);return true;}return false;}
boolean hasUnloadableSpot(Product p){ensureSpots(p);ArrayList<Spot> spots=pickupSpots.get(p);
if(spots==null||spots.isEmpty())return false;for(Spot sp:spots){if(isSpotUnloadable(sp))return true;}
return false;}Product goToNearest(List<Product> targets){tGen++;int sx=robot.getX();int sy=robot.getY();
int startId=sy * W + sx;for(Product p:targets){ensureSpots(p);ArrayList<Spot> spots=pickupSpots.get(p);
if(spots==null)continue;for(Spot sp:spots){if(!isSpotUnloadable(sp))continue;if(sp.tid()==startId){
if(sp.dir()!=null&&robot.getFacing()!=sp.dir())robot.turn(sp.dir());return p;}tgtGen[sp.tid()]=tGen;
tgtProd[sp.tid()]=p;tgtDir[sp.tid()]=sp.dir();}}bGen++;for(int tries=0;tries < 6;tries++){
int cx=robot.getX(),cy=robot.getY(),curId=cy * W + cx;if(tgtGen[curId]==tGen){Direction d=tgtDir[curId];
if(d!=null&&robot.getFacing()!=d)robot.turn(d);return tgtProd[curId];}markAdjBlocked();blkGen[curId]=0;
int bestTid=bfs(cx,cy,-1,true,true);if(bestTid==-1)break;Product bestP=tgtProd[bestTid];
Direction bestDir=tgtDir[bestTid];int hit=walkComputedPath(curId,bestTid);if(hit==-1){
if(bestDir!=null&&robot.getFacing()!=bestDir)robot.turn(bestDir);return bestP;}blkGen[hit]=bGen;
markAdjBlocked();}int bestTid=bfs(robot.getX(),robot.getY(),-1,false,true);if(bestTid==-1)return null;
Product bestP=tgtProd[bestTid];goTo(bestTid % W,bestTid / W,tgtDir[bestTid],-1);return bestP;}
void initPickupSpots(){for(Product p:Product.values()){if(p.isInfinite())continue;
List<Cell> cells=getRackCells(p);if(cells==null||cells.isEmpty())continue;if(!cachePickupDirections(p)){
probeSingleSpot(p);}}goToTruck();}boolean hasItemsInTruck(){for(Product p:Product.values()){
if(store.getRemaining(p)> 0)return true;}return false;}boolean hasUnloadableCarried(){
for(Product p:robot.getCarried()){if(hasUnloadableSpot(p))return true;}return false;}
boolean safePutOnShelf(Product p){if(!robot.isAtPickup()||!robot.has(p))return false;
Shelf s=robot.getShelf();if(s==null||s.getProduct()!=p||s.isFull())return false;try{
return robot.putOnShelf(p);}catch(Exception e){return false;}}boolean safePickTruck(Product p){
if(!robot.isAtTruck()||robot.getLoad()>=robot.getCapacity()||store.getRemaining(p)<=0)return false;try{
return robot.pick(p);}catch(Exception e){return false;}}void unloadCarried(){while(true){
ArrayList<Product> carried=new ArrayList<>();for(Product p:robot.getCarried()){
if(!carried.contains(p)&&hasUnloadableSpot(p))carried.add(p);}if(carried.isEmpty())break;
Product next=goToNearest(carried);if(next==null)break;while(robot.has(next)&&safePutOnShelf(next)){}
if(robot.has(next)&&robot.isAtPickup()){int rx=robot.getX()+ getDx(robot.getFacing());
int ry=robot.getY()+ getDy(robot.getFacing());fullRackStock.put(ry * W + rx,store.getStock(rx,ry));}}}
void restockFromTruck(){Product[]all=Product.values();
while(hasItemsInTruck()&&robot.getLoad()< robot.getCapacity()){if(!goToTruck()){sidestep(null);pause(60);
return;}while(!robot.isAtTruck()){
if(store.isTruckAway()||robot.getX()!=garageX||robot.getY()!=garageY)return;pause(50);}
int beforeLoad=robot.getLoad();for(Product p:all){if(!p.isVending())continue;
while(robot.getLoad()< robot.getCapacity()&&store.getRemaining(p)> 0&&safePickTruck(p)){}
if(robot.getLoad()>=robot.getCapacity())break;}if(robot.getLoad()< robot.getCapacity()){
for(int i=all.length - 1;i>=0;i--){Product p=all[i];if(p.isVending())continue;
while(robot.getLoad()< robot.getCapacity()&&store.getRemaining(p)> 0&&safePickTruck(p)){}
if(robot.getLoad()>=robot.getCapacity())break;}}if(robot.getLoad()==beforeLoad)break;unloadCarried();
if(robot.getLoad()>=robot.getCapacity())break;}}initPickupSpots();while(true){if(hasUnloadableCarried()){
unloadCarried();}else if(store.isTruckHere()&&hasItemsInTruck()&&robot.getLoad()< robot.getCapacity()){
restockFromTruck();}else{goToTruck();pause(60);}}
