Direction[]DIRS={Direction.UP,Direction.LEFT,Direction.DOWN,Direction.RIGHT};int W=store.getWidth();
int H=store.getHeight();int SIZE=W * H;List<Cell> regs=store.findAll(Entity.REGISTER);
Cell myReg=regs.get(0);for(Cell r:regs){if(store.getRegisterNumber(r.getX(),r.getY())==3){myReg=r;break;}
}int regX=myReg.getX();int regY=myReg.getY();Direction regSide=store.getRegisterSide(regX,regY);
Cell cCell=store.getCashierCell(regX,regY);int cashierX=(cCell!=null)?cCell.getX():robot.getX();
int cashierY=(cCell!=null)?cCell.getY():robot.getY();Cell pCell=store.getPackerCell(regX,regY);
int packerX=(pCell!=null)?pCell.getX():cashierX + 1;int packerY=(pCell!=null)?pCell.getY():cashierY;
record Spot(int tid,Direction dir,int rx,int ry){}void pause(int ms){try{Thread.sleep(ms);
}catch(InterruptedException e){Thread.currentThread().interrupt();}}int getDx(Direction d){
return switch(d){case LEFT->-1;case RIGHT->1;default->0;};}int getDy(Direction d){return switch(d){
case UP->1;case DOWN->-1;default->0;};}Direction opposite(Direction d){if(d==null)return null;
return switch(d){case UP->Direction.DOWN;case DOWN->Direction.UP;case LEFT->Direction.RIGHT;
case RIGHT->Direction.LEFT;};}boolean[]freeMap=new boolean[SIZE];for(int y=0;y < H;y++){
for(int x=0;x < W;x++){freeMap[y * W + x]=store.isFree(x,y);}}int[]dist=new int[SIZE];
int[]prev=new int[SIZE];Direction[]prevDir=new Direction[SIZE];Direction[]pathBuf=new Direction[SIZE];
int[]q=new int[SIZE];int[]visGen=new int[SIZE];int[]blkGen=new int[SIZE];int gen=0;int bGen=0;
int[]tgtGen=new int[SIZE];Product[]tgtProd=new Product[SIZE];Direction[]tgtDir=new Direction[SIZE];
int tGen=0;HashMap<Product,List<Cell>> rackCells=new HashMap<>();
HashMap<Product,ArrayList<Spot>> pickupSpots=new HashMap<>();
HashMap<Product,Integer> fullAtStock=new HashMap<>();List<Cell> getRackCells(Product p){
return rackCells.computeIfAbsent(p,k->store.findAll(k.getRack()));}void ensurePickupCache(Product p){
if(pickupSpots.containsKey(p))return;ArrayList<Spot> spots=new ArrayList<>();
HashSet<Long> seen=new HashSet<>();for(Cell rc:getRackCells(p)){for(Direction d:DIRS){
int nx=rc.getX()+ getDx(d);int ny=rc.getY()+ getDy(d);if(nx>=0&&nx < W&&ny>=0&&ny < H){
int tid=ny * W + nx;if(freeMap[tid]){List<Direction> pd=store.getPickupDirections(nx,ny,p);if(pd!=null){
for(Direction face:pd){long key=((long)tid << 3)| face.ordinal();if(seen.add(key)){
spots.add(new Spot(tid,face,nx + getDx(face),ny + getDy(face)));}}}}}}}pickupSpots.put(p,spots);}
boolean isNightTime(){int h=store.getHour();return h>=22||h < 6;}int getRackStock(Product p){
List<Cell> cells=getRackCells(p);if(cells==null||cells.isEmpty())return -1;int total=0;for(Cell c:cells){
total+=store.getStock(c.getX(),c.getY());}return total;}void markAdjBlocked(){
int rx=robot.getX(),ry=robot.getY();for(Direction d:DIRS){int nx=rx + getDx(d),ny=ry + getDy(d);
if(nx>=0&&nx < W&&ny>=0&&ny < H){int nid=ny * W + nx;if(freeMap[nid]&&!robot.canMove(d))blkGen[nid]=bGen;
}}}int bfs(int sx,int sy,int targetId,boolean useCanMove,boolean useMarked,int[]tieDist){gen++;
int head=0;int tail=0;int startId=sy * W + sx;visGen[startId]=gen;dist[startId]=0;prevDir[startId]=null;
q[tail++]=startId;int bestTid=-1;int bestDist=Integer.MAX_VALUE;int bestTie=-1;while(head < tail){
int cid=q[head++];if(dist[cid]>=bestDist)break;if(cid==targetId)return cid;
if(useMarked&&tgtGen[cid]==tGen)return cid;int cx=cid % W;int cy=cid / W;int nd=dist[cid]+ 1;
Direction straight=prevDir[cid];for(int k=0;k < 5;k++){Direction d=(k==0)?straight:DIRS[k - 1];
if(d==null||(k > 0&&d==straight))continue;if(useCanMove&&cid==startId&&!robot.canMove(d))continue;
int nx=cx + getDx(d);int ny=cy + getDy(d);if(nx>=0&&nx < W&&ny>=0&&ny < H){int nid=ny * W + nx;
if((!useCanMove||blkGen[nid]!=bGen)&&visGen[nid]!=gen&&freeMap[nid]){visGen[nid]=gen;dist[nid]=nd;
prev[nid]=cid;prevDir[nid]=d;if(nid==targetId)return nid;if(useMarked&&tgtGen[nid]==tGen){
if(tieDist==null)return nid;int tie=tieDist[nid];if(nd < bestDist||tie > bestTie){bestDist=nd;
bestTie=tie;bestTid=nid;}}if(nd < bestDist){q[tail++]=nid;}}}}}return bestTid;}
Direction[]buildNavTable(int tx,int ty){Direction[]table=new Direction[SIZE];
bfs(tx,ty,-1,false,false,null);int startId=ty * W + tx;for(int i=0;i < SIZE;i++){
if(i!=startId&&visGen[i]==gen&&prevDir[i]!=null){table[i]=opposite(prevDir[i]);}}return table;}
Direction[]toCashier=buildNavTable(cashierX,cashierY);Direction[]toBag=buildNavTable(packerX,packerY);
int[]distToBag=dist.clone();boolean sidestep(Direction blockedDir){int start=(int)(Math.random()* 4);
for(int i=0;i < 4;i++){Direction d=DIRS[(start + i)% 4];if(d!=blockedDir&&robot.canMove(d)){
return robot.move(d);}}return false;}int walkComputedPath(int startId,int targetId){int len=0;
int curr=targetId;while(curr!=startId){pathBuf[len++]=prevDir[curr];curr=prev[curr];}
for(int i=len - 1;i>=0;i--){Direction step=pathBuf[i];if(!robot.canMove(step)||!robot.move(step)){
int bx=robot.getX()+ getDx(step);int by=robot.getY()+ getDy(step);return by * W + bx;}}return -1;}
boolean goTo(int tx,int ty,Direction faceDir,int initBlocked){bGen++;
if(initBlocked>=0)blkGen[initBlocked]=bGen;Direction lastBlockedDir=null;int retries=0;
int totalAttempts=0;int tid=ty * W + tx;while(totalAttempts < 25){totalAttempts++;int sx=robot.getX();
int sy=robot.getY();int startId=sy * W + sx;if(startId==tid){
if(faceDir!=null&&robot.getFacing()!=faceDir)robot.turn(faceDir);return true;}markAdjBlocked();
blkGen[startId]=0;bfs(sx,sy,tid,true,false,null);if(visGen[tid]!=gen){bGen++;
if(!sidestep(lastBlockedDir))pause(60);else pause(40);lastBlockedDir=null;continue;}
int hit=walkComputedPath(startId,tid);if(hit==-1){
if(faceDir!=null&&robot.getFacing()!=faceDir)robot.turn(faceDir);return true;}blkGen[hit]=bGen;
markAdjBlocked();int hx=hit % W;int hy=hit / W;int rx=robot.getX();int ry=robot.getY();
if(hx > rx)lastBlockedDir=Direction.RIGHT;else if(hx < rx)lastBlockedDir=Direction.LEFT;
else if(hy > ry)lastBlockedDir=Direction.UP;else if(hy < ry)lastBlockedDir=Direction.DOWN;retries++;
if(retries > 3){if(!sidestep(lastBlockedDir))pause(60);lastBlockedDir=null;retries=0;}}return false;}
boolean goToCached(int tx,int ty,Direction faceDir,Direction[]table){int tid=ty * W + tx;while(true){
int cid=robot.getY()* W + robot.getX();if(cid==tid){
if(faceDir!=null&&robot.getFacing()!=faceDir)robot.turn(faceDir);return true;}Direction step=table[cid];
if(step==null){return goTo(tx,ty,faceDir,-1);}if(!robot.canMove(step)||!robot.move(step)){
int bx=robot.getX()+ getDx(step);int by=robot.getY()+ getDy(step);return goTo(tx,ty,faceDir,by * W + bx);
}}}Product goToNearest(List<Product> targets,boolean requireStock){tGen++;int sx=robot.getX();
int sy=robot.getY();int startId=sy * W + sx;for(Product p:targets){ensurePickupCache(p);
for(Spot sp:pickupSpots.get(p)){if(requireStock&&store.getStock(sp.rx(),sp.ry())<=0)continue;
if(sp.tid()==startId){if(sp.dir()!=null&&robot.getFacing()!=sp.dir())robot.turn(sp.dir());return p;}
tgtGen[sp.tid()]=tGen;tgtProd[sp.tid()]=p;tgtDir[sp.tid()]=sp.dir();}}
int[]tie=requireStock?distToBag:null;bGen++;for(int tries=0;tries < 6;tries++){
int cx=robot.getX(),cy=robot.getY(),curId=cy * W + cx;if(tgtGen[curId]==tGen){Direction d=tgtDir[curId];
if(d!=null&&robot.getFacing()!=d)robot.turn(d);return tgtProd[curId];}markAdjBlocked();blkGen[curId]=0;
int bestTid=bfs(cx,cy,-1,true,true,tie);if(bestTid==-1)break;Product bestP=tgtProd[bestTid];
Direction bestDir=tgtDir[bestTid];int hit=walkComputedPath(curId,bestTid);if(hit==-1){
if(bestDir!=null&&robot.getFacing()!=bestDir)robot.turn(bestDir);return bestP;}blkGen[hit]=bGen;
markAdjBlocked();}int bestTid=bfs(robot.getX(),robot.getY(),-1,false,true,tie);
if(bestTid==-1)return null;Product bestP=tgtProd[bestTid];
goTo(bestTid % W,bestTid / W,tgtDir[bestTid],-1);return bestP;}int carriedCount(Product p){int count=0;
for(Product item:robot.getCarried()){if(item==p)count++;}return count;}
boolean orderHasRemaining(Order order){for(Product p:order.getProducts()){
if(order.getRemaining(p)> 0)return true;}return false;}boolean safePick(Product p){
if(!robot.isAtPickup()||robot.getLoad()>=robot.getCapacity())return false;Shelf s=robot.getShelf();
if(s==null||s.getProduct()!=p||s.isEmpty())return false;try{return robot.pick(p);}catch(Exception e){
return false;}}boolean safePutOnShelf(Product p){if(!robot.isAtPickup()||!robot.has(p))return false;
Shelf s=robot.getShelf();if(s==null||s.getProduct()!=p||s.isFull())return false;try{
return robot.putOnShelf(p);}catch(Exception e){return false;}}boolean isReturnable(Product p){
if(!fullAtStock.containsKey(p))return true;if(getRackStock(p)< fullAtStock.get(p)){fullAtStock.remove(p);
return true;}return false;}void returnProducts(List<Product> toReturn){
ArrayList<Product> pending=new ArrayList<>();for(Product p:toReturn){
if(!pending.contains(p)&&isReturnable(p))pending.add(p);}while(!pending.isEmpty()){
Product ret=goToNearest(pending,false);if(ret==null)break;while(robot.has(ret)&&safePutOnShelf(ret)){}
if(robot.has(ret)&&robot.isAtPickup()){fullAtStock.put(ret,getRackStock(ret));}else if(!robot.has(ret)){
fullAtStock.remove(ret);}pending.remove(ret);}}void returnStrayCarried(){if(robot.getLoad()==0)return;
ArrayList<Product> stray=new ArrayList<>();for(Product p:robot.getCarried()){
if(!stray.contains(p)&&isReturnable(p))stray.add(p);}if(!stray.isEmpty()){returnProducts(stray);}}
void packOrder(Order order){while(orderHasRemaining(order)&&!order.isDone()){
while(robot.getLoad()< robot.getCapacity()){ArrayList<Product> neededList=new ArrayList<>();
boolean isNight=isNightTime();for(Product p:order.getProducts()){
int need=order.getRemaining(p)- carriedCount(p);if(need<=0)continue;int stock=getRackStock(p);
if(stock==-1||(stock<=0&&!p.isInSeason())||(p.isAlcohol()&&isNight)){
if(carriedCount(p)==0)order.remove(p);continue;}if(stock<=0)continue;neededList.add(p);}
if(neededList.isEmpty())break;Product next=goToNearest(neededList,true);if(next==null)break;
int need=order.getRemaining(next)- carriedCount(next);boolean picked=false;
while(robot.getLoad()< robot.getCapacity()&&need > 0&&safePick(next)){picked=true;need--;}
if(robot.isAtPickup()&&!picked&&carriedCount(next)==0&&!next.isInSeason()){order.remove(next);}}
boolean hasOrderItem=false;for(Product p:robot.getCarried()){if(order.getRemaining(p)> 0){
hasOrderItem=true;break;}}if(!hasOrderItem){if(orderHasRemaining(order)&&!order.isDone()){
goToCached(packerX,packerY,regSide,toBag);pause(80);continue;}break;}
if(!goToCached(packerX,packerY,regSide,toBag)||!robot.isAtBag()){pause(50);continue;}
boolean isNight=isNightTime();ArrayList<Product> toReturn=new ArrayList<>();
for(Product p:robot.getCarried()){if(order.getRemaining(p)<=0)continue;if(p.isAlcohol()&&isNight){
order.remove(p);if(!toReturn.contains(p))toReturn.add(p);}else{robot.putInBag(p);}}
if(!toReturn.isEmpty()){returnProducts(toReturn);}}}void serveCustomer(Order order,Customer customer){
boolean isNight=isNightTime();for(Product p:order.getProducts()){int stock=getRackStock(p);
if((p.isAdultsOnly()&&!customer.isAdult())||(p.isAlcohol()&&isNight)||stock==-1||(stock<=0&&!p.isInSeason())){
order.remove(p);}}if(!order.isDone()){packOrder(order);order.removeRemaining();}}while(true){
returnStrayCarried();if(!goToCached(cashierX,cashierY,regSide,toCashier)){pause(50);continue;}
Order order=robot.takeOrder();Customer customer=robot.getCustomer();if(order!=null){
serveCustomer(order,customer);}else{pause(50);}}
