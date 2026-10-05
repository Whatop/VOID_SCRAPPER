local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local pc=app.pixelColor
local function readJson(path) local f=assert(io.open(path,'r'));local value=json.decode(f:read('*a'));f:close();return value end
local manifest=readJson(root..'/manifest.json')
local report={passed=true,families={},failures={},failureCounts={},pixelComparisons=0,totalClips=0,totalFrames=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local function difference(a,b) local d=0;for y=0,a.height-1 do for x=0,a.width-1 do if not equal(a:getPixel(x,y),b:getPixel(x,y)) then d=d+1 end end end;return d end
local expected={
 VFX_Sector_LaserTelegraph={64,32,{Warning=5}},VFX_Sector_LaserBeam={64,32,{Fire=3,Sustain=4,Release=3}},VFX_Sector_ContainmentBarrier={64,64,{Loop=6}},
 VFX_Barrage_TargetTelegraph={64,64,{Warning=6}},VFX_Barrage_HeavyImpact={64,64,{Impact=7}},VFX_Barrage_BatteryFlash={64,64,{Fire=4}},
 VFX_Phase_Portal={64,64,{Open=7,Hold=4,Close=6}},VFX_Phase_PrecisionLock={64,64,{Warning=6}},VFX_Phase_RedirectFlash={32,32,{Redirect=4}}}
check(#manifest.families==9,'Expected nine editable families')
local cache={};local measurements={};local pureWhite=color('F0F2E9')
for _,family in ipairs(manifest.families) do
 local e=assert(expected[family.id],'Unexpected family')
 local s=assert(app.open(root..'/'..family.aseprite));local sheet=assert(Image{fromFile=root..'/'..family.sheet});local meta=readJson(root..'/'..family.metadata)
 local count,clipCount=0,0;for _,n in pairs(e[3]) do count=count+n;clipCount=clipCount+1 end
 check(s.width==e[1] and s.height==e[2] and #s.frames==count,'Requested size/frame count: '..family.id)
 check(#s.layers==4 and #s.tags==clipCount,'Editable layers/tags: '..family.id)
 check(sheet.width==family.atlasColumns*e[1] and sheet.height==clipCount*e[2],'Atlas dimensions: '..family.id)
 check(#meta.frames==count and #meta.meta.frameTags==clipCount,'Metadata count: '..family.id)
 check(meta.unity.pixelsPerUnit==32 and meta.unity.filterMode=='Point' and meta.unity.compression=='None' and not meta.unity.mipmaps,'Pixel import metadata: '..family.id)
 check(math.abs(meta.unity.pivot.x-family.pivotPixels.x/e[1])<0.000001 and math.abs(meta.unity.pivot.y-(1-family.pivotPixels.y/e[2]))<0.000001,'Pivot conversion: '..family.id)
 for i,name in ipairs(manifest.layers) do check(s.layers[i].name==name,'Layer name: '..family.id) end
 local allowed={};for _,v in ipairs(family.palette) do allowed[color(v)]=true end
 local result={id=family.id,width=e[1],height=e[2],clips={}};cache[family.id]={};measurements[family.id]={}
 for row,c in ipairs(family.clips) do
  local n=assert(e[3][c.tag],'Unexpected tag');check(c.frames==n and c.toFrame-c.fromFrame+1==n,'Clip length: '..family.id..' '..c.tag)
  local tag=s.tags[row];check(tag.name==c.tag and tag.fromFrame.frameNumber==c.fromFrame and tag.toFrame.frameNumber==c.toFrame,'Aseprite tag range: '..family.id..' '..c.tag)
  local mt=meta.meta.frameTags[row];check(mt.name==c.tag and mt.from==c.fromFrame-1 and mt.to==c.toFrame-1,'JSON tag range: '..family.id..' '..c.tag)
  local strip=assert(Image{fromFile=root..'/'..c.sheet});check(strip.width==e[1]*n and strip.height==e[2],'Tag strip dimensions: '..family.id..' '..c.tag)
  local images,frameData={},{};local colors={};local maxVisible=0;local maxWhite=0;local totalMs=0;local radii={}
  for fi=1,n do
   local frame=c.fromFrame+fi-1;local im=Image(e[1],e[2],ColorMode.RGB);im:drawSprite(s,frame,Point(0,0));images[fi]=im
   local fm=meta.frames[frame];local duration=c.durationsMs[fi]
   check(math.abs(s.frames[frame].duration*1000-duration)<0.6,'Saved frame timing: '..family.id)
   check(fm.duration==duration and fm.frame.x==(fi-1)*e[1] and fm.frame.y==(row-1)*e[2] and fm.frame.w==e[1] and fm.frame.h==e[2] and not fm.trimmed,'Saved frame metadata: '..family.id)
   local visible,white,radius=0,0,0;local minX,minY,maxX,maxY=e[1],e[2],-1,-1
   for it in im:pixels() do
    local p=it();local alpha=pc.rgbaA(p)
    check(alpha==0 or alpha==255,'Non-binary alpha: '..family.id)
    check(equal(p,sheet:getPixel((fi-1)*e[1]+it.x,(row-1)*e[2]+it.y)),'Atlas pixel mismatch: '..family.id)
    check(equal(p,strip:getPixel((fi-1)*e[1]+it.x,it.y)),'Strip pixel mismatch: '..family.id);report.pixelComparisons=report.pixelComparisons+2
    if alpha>0 then
     visible=visible+1;colors[p]=true;check(allowed[p],'Palette mismatch: '..family.id)
     if p==pureWhite then white=white+1 end
     minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
     check(it.y>=2 and it.y<e[2]-2 and ((family.tileAxis=='X' or family.id=='VFX_Sector_LaserTelegraph') or (it.x>=2 and it.x<e[1]-2)),'Unintended canvas clipping: '..family.id)
     if it.x%2==0 and it.y%2==0 then radius=math.max(radius,math.abs(it.x/2-16),math.abs(it.y/2-16)) end
    end
   end
   for y=0,e[2]-2,2 do for x=0,e[1]-2,2 do local p=im:getPixel(x,y);check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Pixel grid mismatch: '..family.id) end end
   if family.tileAxis=='X' then for y=0,e[2]-1 do check(equal(im:getPixel(0,y),im:getPixel(e[1]-1,y)),'Horizontal tiling seam: '..family.id..' '..c.tag) end end
   if c.mode=='handoff' then check(white==0,'Warning uses damaging white flash: '..family.id) end
   if family.id=='VFX_Barrage_TargetTelegraph' then
    local active=0;for i=1,6 do if im:getPixel((6+(i-1)*4)*2,58)==color('FFE0A2') then active=active+1 end end
    check(active==fi,'Barrage countdown block count');check(im:getPixel(8,8)==(fi>=3 and color('FFE0A2') or color('E9913C')),'Artillery outer bracket must remain fixed')
   end
   if family.id=='VFX_Phase_Portal' and (c.tag=='Hold' or c.tag=='Open' and fi>=3 or c.tag=='Close' and fi<=3) then
    check(pc.rgbaA(im:getPixel(32,32))==0,'Portal central aperture must stay transparent')
   end
   radii[fi]=radius*2;totalMs=totalMs+duration;maxVisible=math.max(maxVisible,visible);maxWhite=math.max(maxWhite,white)
   frameData[fi]={durationMs=duration,opaquePixels=visible,whitePixels=white,bounds=visible>0 and {x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1} or {x=0,y=0,w=0,h=0}}
   report.totalFrames=report.totalFrames+1
  end
  -- The combined multi-tag atlas pads short rows, but those cells are never animation frames.
  for y=(row-1)*e[2],row*e[2]-1 do for x=n*e[1],sheet.width-1 do check(pc.rgbaA(sheet:getPixel(x,y))==0,'Nontransparent atlas padding: '..family.id) end end
  local paletteCount=0;for _ in pairs(colors) do paletteCount=paletteCount+1 end;check(paletteCount<=4,'More than four visible clip colors: '..family.id)
  check(frameData[1].opaquePixels>0,'Blank lead frame: '..family.id..' '..c.tag)
  if c.mode=='one_shot' then check(frameData[n].opaquePixels==0 and frameData[n-1].opaquePixels<maxVisible,'Transparent decaying cleanup: '..family.id..' '..c.tag)
  else check(frameData[n].opaquePixels>0,'Unexpected empty live frame: '..family.id..' '..c.tag) end
  if family.id=='VFX_Phase_PrecisionLock' then
   for i,r in ipairs(radii) do check(r==family.radiiPixels[i],'Precision radius');if i>1 then check(r<radii[i-1],'Precision marker must contract every frame') end end
  end
  local deltas={};local maxDelta=0
  for i=1,n-1 do local d=difference(images[i],images[i+1]);check(d>0,'Duplicate adjacent clip frames: '..family.id..' '..c.tag);deltas[#deltas+1]=d;maxDelta=math.max(maxDelta,d) end
  local seam=nil;if c.mode=='loop' then seam=difference(images[n],images[1]);check(seam<=maxDelta,'Loop seam exceeds internal transition: '..family.id..' '..c.tag) end
  local data={tag=c.tag,mode=c.mode,frames=frameData,totalMs=totalMs,colors=paletteCount,maxOpaquePixels=maxVisible,maxWhitePixels=maxWhite,adjacentDifferences=deltas,loopSeamDifference=seam}
  if family.id=='VFX_Phase_PrecisionLock' then data.measuredRadiiPixels=radii end
  result.clips[#result.clips+1]=data;report.totalClips=report.totalClips+1;cache[family.id][c.tag]=images;measurements[family.id][c.tag]=data
 end
 report.families[#report.families+1]=result;s:close()
end
local portal=cache.VFX_Phase_Portal
check(difference(portal.Open[#portal.Open],portal.Hold[1])==0,'Portal Open to Hold transition')
check(difference(portal.Hold[1],portal.Close[1])==0,'Portal Hold to Close canonical transition')
report.portalTransitionPixelsMatch=true
local warning=cache.VFX_Sector_LaserTelegraph.Warning[5]
local firing=cache.VFX_Sector_LaserBeam.Fire[2]
local warningMin,warningMax,beamMin,beamMax=32,-1,32,-1
for y=0,31 do
 if pc.rgbaA(warning:getPixel(0,y))>0 then warningMin=math.min(warningMin,y);warningMax=math.max(warningMax,y) end
 if pc.rgbaA(firing:getPixel(0,y))>0 then beamMin=math.min(beamMin,y);beamMax=math.max(beamMax,y) end
end
check(warningMin==beamMin and warningMax==beamMax,'Final warning lane must match damaging beam envelope')
report.laserWarningEnvelope={top=warningMin,bottom=warningMax,height=warningMax-warningMin+1}
local generic=assert(Image{fromFile=sourceDir..'/VFX_Explosion_Medium.png'});local genericPeak,genericWhite=0,0
for fi=0,5 do local opaque,white=0,0;for y=0,63 do for x=0,63 do local p=generic:getPixel(fi*64+x,y);if pc.rgbaA(p)>0 then opaque=opaque+1 end;if p==pureWhite then white=white+1 end end end;genericPeak=math.max(genericPeak,opaque);genericWhite=math.max(genericWhite,white) end
local heavy=measurements.VFX_Barrage_HeavyImpact.Impact
check(heavy.maxOpaquePixels>=genericPeak*1.25,'Heavy barrage must exceed medium explosion silhouette area')
check(heavy.maxWhitePixels>genericWhite,'Heavy barrage must have stronger white flash than medium explosion')
report.impactComparison={genericPeakOpaquePixels=genericPeak,heavyPeakOpaquePixels=heavy.maxOpaquePixels,genericPeakWhitePixels=genericWhite,heavyPeakWhitePixels=heavy.maxWhitePixels}
check(report.totalFrames==65 and report.totalClips==13,'Pack totals')
local contact=assert(Image{fromFile=root..'/'..manifest.preview});check(contact.width==1000 and contact.height==1134,'Contact sheet dimensions')
local native=assert(Image{fromFile=root..'/'..manifest.nativePreview});check(native.width==480 and native.height==270,'Native review dimensions')
local gif=assert(app.open(root..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==80,'GIF dimensions and frame count')
local ms=0;for _,f in ipairs(gif.frames) do check(math.abs(f.duration-0.02)<0.001,'GIF timing');ms=ms+math.floor(f.duration*1000+0.5) end;gif:close();check(ms==1600,'GIF duration')
report.animatedPreview={width=480,height=270,frames=80,durationMs=ms}
report.passed=#report.failures==0
local f=assert(io.open(root..'/validation.json','w'));f:write(json.encode(report));f:close()
assert(report.passed,'SYSTEM VFX validation failed; read validation.json');print('SYSTEM_BOSS_VFX_VALIDATED')
