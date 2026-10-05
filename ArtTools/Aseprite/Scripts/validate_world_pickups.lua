local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local pc=app.pixelColor
local function readJson(path) local f=assert(io.open(path,'r'));local v=json.decode(f:read('*a'));f:close();return v end
local manifest=readJson(root..'/manifest.json')
local report={passed=true,failures={},failureCounts={},pickups={},rarityOverlays={},pixelComparisons=0,totalSources=0,totalFrames=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local function equal(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function flat(s,frame) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,frame,Point(0,0));return im end
local function diff(a,b) local count=0;for it in a:pixels() do if not equal(it(),b:getPixel(it.x,it.y)) then count=count+1 end end;return count end
local expected={Pickup_Credit={16,1},Pickup_Scrap={16,1},Pickup_CoreShard={32,4},Pickup_TuningChip={32,1},Pickup_RegionResource_A={32,1},Pickup_RegionResource_B={32,1},Pickup_RegionResource_C={32,1},Pickup_Heal={32,1},Pickup_TraitContainer={48,1},Pickup_ReinforcementContainer={48,1}}
check(#manifest.pickups==10 and #manifest.rarityOverlays==3,'Ten pickups and three reusable rarity sources')
local approved=assert(Image{fromFile=sourceDir..'/core_green_shard.png'})
local bases={};local overlays={}
for _,item in ipairs(manifest.pickups) do
 local e=assert(expected[item.id]);local size=e[1];local s=assert(app.open(root..'/'..item.aseprite));local base=assert(Image{fromFile=root..'/'..item.base});local sheet=assert(Image{fromFile=root..'/'..item.sheet});local meta=readJson(root..'/'..item.metadata)
 check(s.width==size and s.height==size and #s.frames==e[2],'Requested pickup dimensions/frames: '..item.id)
 check(base.width==size and base.height==size and sheet.width==size*e[2] and sheet.height==size,'Pickup PNG dimensions: '..item.id)
 check(#s.layers==#item.layers and #s.tags==1 and s.tags[1].name==item.tag and s.tags[1].fromFrame.frameNumber==1 and s.tags[1].toFrame.frameNumber==e[2],'Editable pickup tags/layers: '..item.id)
 check(#meta.frames==e[2] and meta.meta.image==item.sheet and meta.unity.pixelsPerUnit==32 and meta.unity.pivot.x==0.5 and meta.unity.pivot.y==0.5,'Pickup metadata: '..item.id)
 check(meta.unity.filterMode=='Point' and meta.unity.compression=='None' and not meta.unity.mipmaps,'Pickup pixel settings: '..item.id)
 for li,name in ipairs(item.layers) do check(s.layers[li].name==name and s.layers[li].opacity==255 and s.layers[li].isVisible,'Source layer visibility/name: '..item.id) end
 local allowed={};for _,v in ipairs(item.palette) do allowed[rgba(v)]=true end
 local frames,frameData,colors={},{},{};local bounds=nil
 for fi=1,e[2] do
  local im=flat(s,fi);frames[fi]=im;local visible=0;local minX,minY,maxX,maxY=size,size,-1,-1
  check(math.abs(s.frames[fi].duration*1000-item.durationsMs[fi])<0.6,'Pickup timing: '..item.id)
  local fm=meta.frames[fi];check(fm.duration==item.durationsMs[fi] and fm.frame.x==(fi-1)*size and fm.frame.y==0 and fm.frame.w==size and fm.frame.h==size and not fm.trimmed,'Pickup frame rectangle: '..item.id)
  for it in im:pixels() do
   local p=it();local a=pc.rgbaA(p);check(a==0 or a==255,'Partial alpha: '..item.id)
   check(equal(p,sheet:getPixel((fi-1)*size+it.x,it.y)),'Pickup sheet mismatch: '..item.id);report.pixelComparisons=report.pixelComparisons+1
   if fi==1 then check(equal(p,base:getPixel(it.x,it.y)),'Pickup base mismatch: '..item.id);report.pixelComparisons=report.pixelComparisons+1 end
   if a>0 then visible=visible+1;colors[p]=true;check(allowed[p],'Pickup palette mismatch: '..item.id);check(it.x>=1 and it.y>=1 and it.x<size-1 and it.y<size-1,'Pickup touches canvas boundary: '..item.id);minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y) end
  end
  if item.constructionPixelScale==2 then for y=0,size-2,2 do for x=0,size-2,2 do local p=im:getPixel(x,y);check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Pickup 2px construction: '..item.id) end end end
  check(visible>40,'Empty or unreadable pickup: '..item.id);frameData[fi]={opaquePixels=visible,durationMs=item.durationsMs[fi],bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1}}
  if item.socket then
   local r=item.socket;check(r.w==16 and r.h==16 and #s.layers[5].cels==0,'Assigned icon socket/layer: '..item.id)
   for y=r.y,r.y+r.h-1 do for x=r.x,r.x+r.w-1 do check(im:getPixel(x,y)==rgba('18252B'),'Socket must contain only blank dark backplate: '..item.id) end end
  end
  if item.approvedCoreMask then
   local core=Image(size,size,ColorMode.RGB)
   for li=1,3 do local cel=s.layers[li]:cel(fi);core:drawImage(cel.image,cel.position) end
   for it in core:pixels() do check((pc.rgbaA(it())>0)==(pc.rgbaA(approved:getPixel(it.x,it.y))>0),'Approved core shard silhouette changed') end
  end
  report.totalFrames=report.totalFrames+1
 end
 local paletteSize=0;for _ in pairs(colors) do paletteSize=paletteSize+1 end;check(paletteSize<=9,'Pickup palette exceeds nine colors: '..item.id)
 local r={id=item.id,width=size,height=size,frames=frameData,colors=paletteSize,socket=item.socket}
 if e[2]>1 then
  check(item.id=='Pickup_CoreShard','Only high-value core shard may animate');local maxDelta=0;local deltas={}
  for i=1,e[2]-1 do local d=diff(frames[i],frames[i+1]);check(d>0,'Duplicate shard idle frames');check(d<=8,'Shard idle is too noisy');maxDelta=math.max(maxDelta,d);deltas[#deltas+1]=d end
  local seam=diff(frames[e[2]],frames[1]);check(seam<=maxDelta,'Shard idle loop seam');r.adjacentDifferences=deltas;r.loopSeamDifference=seam
 end
 bases[item.id]=base;report.pickups[#report.pickups+1]=r;report.totalSources=report.totalSources+1;s:close()
end
for _,asset in ipairs(manifest.rarityOverlays) do
 local size=asset.width;check(size==16 or size==32 or size==48,'Unsupported rarity canvas')
 local s=assert(app.open(root..'/'..asset.aseprite));local sheet=assert(Image{fromFile=root..'/'..asset.sheet});local meta=readJson(root..'/'..asset.metadata)
 check(s.width==size and s.height==size and #s.frames==4 and #s.layers==2 and #s.tags==4,'Rarity source structure')
 check(s.layers[1].name=='Frame' and s.layers[2].name=='Glow Marks','Rarity editable layer names')
 check(sheet.width==size*4 and sheet.height==size and #meta.frames==4,'Rarity atlas structure')
 local result={size=size,variants={}};overlays[size]={}
 for fi,variant in ipairs(asset.variants) do
  local tag=s.tags[fi];check(tag.name==variant.tag and tag.fromFrame.frameNumber==fi and tag.toFrame.frameNumber==fi,'Single-state rarity tags')
  local im=flat(s,fi);local png=assert(Image{fromFile=root..'/'..variant.png});overlays[size][variant.tag]=im
  check(png.width==size and png.height==size,'Rarity individual PNG size');local visible=0;local allowed={};for _,v in ipairs(variant.palette) do allowed[rgba(v)]=true end
  local protected=size==16 and 4 or size==32 and 7 or 11
  for it in im:pixels() do
   local p=it();local a=pc.rgbaA(p);check(a==0 or a==255,'Rarity partial alpha')
   check(equal(p,sheet:getPixel((fi-1)*size+it.x,it.y)) and equal(p,png:getPixel(it.x,it.y)),'Rarity PNG pixels');report.pixelComparisons=report.pixelComparisons+2
   if a>0 then visible=visible+1;check(allowed[p],'Rarity palette');check(it.x>=1 and it.y>=1 and it.x<size-1 and it.y<size-1,'Rarity boundary clipping');check(it.x<protected or it.x>=size-protected or it.y<protected or it.y>=size-protected,'Rarity must leave center empty') end
  end
  check(visible>0 and visible<=size*size*0.16,'Rarity overlay too dense');result.variants[#result.variants+1]={tag=variant.tag,opaquePixels=visible};report.totalFrames=report.totalFrames+1
 end
 report.rarityOverlays[#report.rarityOverlays+1]=result;report.totalSources=report.totalSources+1;s:close()
end
local combinations={}
for _,item in ipairs(manifest.pickups) do
 local result={id=item.id,overlayIntersections={}}
 for _,tag in ipairs({'Common','Rare','Legendary','Curse'}) do
  local frame=overlays[item.width][tag];local intersection=0
  for it in frame:pixels() do if pc.rgbaA(it())>0 and pc.rgbaA(bases[item.id]:getPixel(it.x,it.y))>0 then intersection=intersection+1 end end
  -- Edge decoration must not erase or cover meaningful material silhouettes.
  check(intersection==0,'Rarity overlay obscures pickup: '..item.id..' '..tag);result.overlayIntersections[tag]=intersection
  if item.socket then local r=item.socket;for y=r.y,r.y+r.h-1 do for x=r.x,r.x+r.w-1 do check(pc.rgbaA(frame:getPixel(x,y))==0,'Rarity covers assigned icon socket') end end end
 end
 combinations[#combinations+1]=result
end
report.overlayCompatibility=combinations;report.approvedShardMaskPreserved=true
check(report.totalSources==13 and report.totalFrames==25,'Pack totals')
local contact=assert(Image{fromFile=root..'/'..manifest.preview});check(contact.width==1040 and contact.height==1020,'Contact sheet dimensions')
local native=assert(Image{fromFile=root..'/'..manifest.nativePreview});check(native.width==480 and native.height==270,'Native review dimensions')
local gif=assert(app.open(root..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==75,'Animated review dimensions')
for _,f in ipairs(gif.frames) do check(math.abs(f.duration-0.02)<0.001,'Review GIF timing') end;gif:close()
report.passed=#report.failures==0
local f=assert(io.open(root..'/validation.json','w'));f:write(json.encode(report));f:close()
assert(report.passed,'World pickup validation failed; see validation.json');print('WORLD_PICKUPS_VALIDATED')
