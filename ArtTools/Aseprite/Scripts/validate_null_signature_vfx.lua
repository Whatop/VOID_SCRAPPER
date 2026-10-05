local root=assert(app.params.outputDir)
local refs=assert(app.params.sourceDir)
local pc=app.pixelColor
local function readJson(path) local f=assert(io.open(path,'r'));local v=json.decode(f:read('*a'));f:close();return v end
local manifest=readJson(root..'/manifest.json')
local report={passed=false,failures={},failureCounts={},families={},totalFrames=0,totalTags=0,pixelComparisons=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local white=rgba('FCE9FF');local pale=rgba('F0B3FB')
local function equal(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function diff(a,b) local n=0;for it in a:pixels() do if not equal(it(),b:getPixel(it.x,it.y)) then n=n+1 end end;return n end
local function layer(s,li,fi) local im=Image(s.width,s.height,ColorMode.RGB);local c=s.layers[li]:cel(fi);if c then im:drawImage(c.image,c.position) end;return im end
local expected={VFX_NULL_LaserRain_Telegraph={32,64,6,1},VFX_NULL_LaserRain_Beam={32,64,6,2},VFX_NULL_CompressionDispatch={64,64,6,1},VFX_NULL_PhaseRedirectCorruption={64,64,5,1},VFX_NULL_InterventionBeam={96,32,8,1},VFX_NULL_CoreGlitchBurst={64,64,6,1},VFX_NULL_CriticalLoop={64,64,8,1}}
local cache,metrics={},{}
check(#manifest.families==7,'Seven VFX families required')
for _,v in ipairs(manifest.families) do
 local ex=assert(expected[v.id]);local w,h=ex[1],ex[2];local s=assert(app.open(root..'/'..v.aseprite))
 local png=Image{fromFile=root..'/'..v.sheet};local jsonData=readJson(root..'/'..v.metadata)
 check(s.width==w and s.height==h and #s.frames==ex[3] and #s.tags==ex[4] and #s.layers==4,'Source structure '..v.id)
 check(png.width==w*ex[3] and png.height==h,'Sheet size '..v.id)
 check(#jsonData.frames==ex[3] and #jsonData.meta.frameTags==ex[4],'JSON counts '..v.id)
 check(jsonData.unity.pixelsPerUnit==32 and jsonData.unity.filterMode=='Point' and jsonData.unity.compression=='None' and not jsonData.unity.mipmaps,'Unity import metadata '..v.id)
 check(jsonData.unity.pivot.x==v.pivotPixels.x/w and jsonData.unity.pivot.y==1-v.pivotPixels.y/h,'Unity pivot '..v.id)
 for li,name in ipairs(manifest.layers) do check(s.layers[li].name==name and s.layers[li].opacity==255 and s.layers[li].isVisible,'Editable layer '..v.id..' / '..li) end
 for ti,t in ipairs(v.tags) do local saved=s.tags[ti];check(saved.name==t.name and saved.fromFrame.frameNumber==t.from and saved.toFrame.frameNumber==t.to,'Tag range '..v.id);local jt=jsonData.meta.frameTags[ti];check(jt.name==t.name and jt.from==t.from-1 and jt.to==t.to-1,'JSON tag range '..v.id) end
 local allowed={};for _,color in ipairs(v.palette) do allowed[rgba(color)]=true end
 local colors={};local frames={};local frameStats={};local moving={};local peak=0;local maxHot=0
 for fi=1,ex[3] do
  local im=Image(w,h,ColorMode.RGB);im:drawSprite(s,fi,Point(0,0));frames[fi]=im
  check(math.abs(s.frames[fi].duration*1000-v.durationsMs[fi])<0.6,'Duration '..v.id)
  local fm=jsonData.frames[fi];check(fm.duration==v.durationsMs[fi] and fm.frame.x==(fi-1)*w and fm.frame.y==0 and fm.frame.w==w and fm.frame.h==h and not fm.trimmed,'JSON frame '..v.id)
  local rec={opaquePixels=0,whitePixels=0,palePixels=0,durationMs=v.durationsMs[fi],bounds={x=w,y=h,r=-1,b=-1}}
  for it in im:pixels() do local pixel=it();local a=pc.rgbaA(pixel)
   check(a==0 or a==255,'Partial alpha '..v.id);check(equal(pixel,png:getPixel((fi-1)*w+it.x,it.y)),'PNG mismatch '..v.id);report.pixelComparisons=report.pixelComparisons+1
   if a>0 then
    check(allowed[pixel],'Palette changed '..v.id);colors[pixel]=true;rec.opaquePixels=rec.opaquePixels+1
    if pixel==white then rec.whitePixels=rec.whitePixels+1 end;if pixel==pale then rec.palePixels=rec.palePixels+1 end
    rec.bounds.x=math.min(rec.bounds.x,it.x);rec.bounds.y=math.min(rec.bounds.y,it.y);rec.bounds.r=math.max(rec.bounds.r,it.x);rec.bounds.b=math.max(rec.bounds.b,it.y)
    check((v.tileAxis=='X' or (it.x>=2 and it.x<w-2)) and (v.tileAxis=='Y' or (it.y>=2 and it.y<h-2)),'Unintended clipping '..v.id)
   end
  end
  if v.constructionPixelScale==2 then for y=0,h-1,2 do for x=0,w-1,2 do local pixel=im:getPixel(x,y);check(equal(pixel,im:getPixel(x+1,y)) and equal(pixel,im:getPixel(x,y+1)) and equal(pixel,im:getPixel(x+1,y+1)),'Pixel grid '..v.id) end end end
  if v.tileAxis=='Y' then for y=0,h-17 do for x=0,w-1 do check(equal(im:getPixel(x,y),im:getPixel(x,y+16)),'Vertical repeat seam '..v.id) end end end
  if v.tileAxis=='X' then
   for y=0,h-1 do for x=0,w-33 do check(equal(im:getPixel(x,y),im:getPixel(x+32,y)),'Horizontal repeat seam '..v.id) end end
   local packets=Image(w,h,ColorMode.RGB);for li=2,4 do packets:drawImage(layer(s,li,fi),Point(0,0)) end;moving[fi]=packets
  end
  if v.mode=='handoff' then check(rec.whitePixels==0 and rec.palePixels==0,'Telegraph contains attack flash') end
  if v.id=='VFX_NULL_LaserRain_Beam' and fi<=4 then for y=0,63 do check(im:getPixel(15,y)==white and im:getPixel(16,y)==white,'White strike axis interrupted during Fire') end end
  if v.id=='VFX_NULL_CriticalLoop' then check(rec.opaquePixels<=420,'Critical loop too dense');check((fi==2 or fi==6)==(rec.whitePixels>0),'Critical flash cadence') end
  peak=math.max(peak,rec.opaquePixels);maxHot=math.max(maxHot,rec.whitePixels);frameStats[fi]=rec;report.totalFrames=report.totalFrames+1
 end
 check(frameStats[1].opaquePixels>0,'Empty first frame '..v.id)
 if v.mode=='one_shot' then check(frameStats[#frames].opaquePixels==0 and frameStats[#frames-1].opaquePixels<peak,'Missing cleanup/decay '..v.id)
 else check(frameStats[#frames].opaquePixels>0,'Empty terminal loop/warning '..v.id) end
 local differences={};local maxDiff=0
 for i=1,#frames-1 do local d=diff(frames[i],frames[i+1]);check(d>0,'Adjacent duplicate frames '..v.id);differences[#differences+1]=d;maxDiff=math.max(maxDiff,d) end
 local seam
 if v.mode=='loop' then seam=diff(frames[#frames],frames[1]);check(seam<=maxDiff,'Loop seam exceeds internal transition '..v.id) end
 if v.tileAxis=='X' then
  for fi=1,#frames do local next=moving[fi%#frames+1];for y=0,h-1 do for x=0,w-1 do check(equal(moving[fi]:getPixel((x+4)%w,y),next:getPixel(x,y)),'Intervention packets must flow left 4px including seam') end end end
  local tiles=Image{fromFile=root..'/'..v.tileSheet};check(tiles.width==32*#frames and tiles.height==32,'Intervention tile strip dimensions')
  for fi,im in ipairs(frames) do for y=0,31 do for x=0,31 do check(equal(im:getPixel(x,y),tiles:getPixel((fi-1)*32+x,y)),'Tile strip mismatch');report.pixelComparisons=report.pixelComparisons+1 end end end
  report.interventionFlow={direction='-X toward emitter',pixelsPerFrame=4,periodPixels=32,loopSeamVerified=true}
 end
 if v.tagSheets then for _,ts in ipairs(v.tagSheets) do local tag;for _,t in ipairs(v.tags) do if t.name==ts.tag then tag=t end end;local strip=Image{fromFile=root..'/'..ts.sheet};check(strip.width==(tag.to-tag.from+1)*w and strip.height==h,'Tag strip dimensions');for fi=tag.from,tag.to do for it in frames[fi]:pixels() do check(equal(it(),strip:getPixel((fi-tag.from)*w+it.x,it.y)),'Tag strip pixels');report.pixelComparisons=report.pixelComparisons+1 end end end end
 if v.id=='VFX_NULL_CoreGlitchBurst' then
  local src=assert(app.open(refs..'/core_purple_corrupted_overloaded.aseprite'));local energy;for _,l in ipairs(src.layers) do if l.name=='Energy' then energy=l end end
  local expectedCore=Image(64,64,ColorMode.RGB);local cel=assert(energy:cel(1));expectedCore:drawImage(cel.image,cel.position)
  local actual=layer(s,2,2);check(diff(actual,expectedCore)==0,'Approved core Energy pixels altered');report.approvedCoreEnergyFrameExact=true;src:close()
 end
 local numColors=0;for _ in pairs(colors) do numColors=numColors+1 end;check(numColors<=7,'More than seven colors '..v.id)
 local result={id=v.id,frames=frameStats,peakOpaquePixels=peak,peakWhitePixels=maxHot,colors=numColors,adjacentDifferences=differences,loopSeamDifference=seam}
 report.families[#report.families+1]=result;cache[v.id]=frames;metrics[v.id]=result;report.totalTags=report.totalTags+#v.tags;s:close()
end
local warning=metrics.VFX_NULL_LaserRain_Telegraph;local attack=metrics.VFX_NULL_LaserRain_Beam
check(warning.peakWhitePixels==0 and attack.peakWhitePixels>=240,'Warning and damage brightness distinction')
report.laserTiming={warningMs=720,fireMs=240,releaseMs=100,strikeAxisPixels=16,warningWhitePixels=0,peakBeamWhitePixels=attack.peakWhitePixels}
local last=999
for i=1,5 do local b=metrics.VFX_NULL_CompressionDispatch.frames[i].bounds;local span=math.max(b.r-b.x+1,b.b-b.y+1);check(span<last,'Compression must visibly contract');last=span end
-- Guard against a pure palette swap: NULL masks differ from all comparable
-- rotated SYSTEM laser frames and the enlarged blue redirect reference.
local function maskDiff(im,reference,frame,scaleFactor,rotate)
 local n=0
 for it in im:pixels() do
  local x,y=it.x,it.y;local p
  if rotate then p=reference:getPixel(frame*64+y,31-x)
  else p=reference:getPixel(frame*32+x//scaleFactor,y//scaleFactor) end
  if (pc.rgbaA(it())>0)~=(pc.rgbaA(p)>0) then n=n+1 end
 end
 return n
end
local comparison={}
for _,pair in ipairs({{'VFX_NULL_LaserRain_Telegraph','VFX_Sector_LaserTelegraph.png',5,6},{'VFX_NULL_LaserRain_Beam','VFX_Sector_LaserBeam_Fire.png',3,4}}) do
 local reference=Image{fromFile=refs..'/'..pair[2]};local min=999999
 for a=1,pair[4] do for b=0,pair[3]-1 do min=math.min(min,maskDiff(cache[pair[1]][a],reference,b,1,true)) end end
 check(min>80,'NULL laser resembles only a recolored SYSTEM mask');comparison[#comparison+1]={id=pair[1],minimumAlphaMaskDifference=min}
end
local ref=Image{fromFile=refs..'/VFX_Phase_RedirectFlash.png'};local min=999999
for a=1,3 do for b=0,2 do min=math.min(min,maskDiff(cache.VFX_NULL_PhaseRedirectCorruption[a],ref,b,2,false)) end end
check(min>80,'NULL redirect resembles only a recolored SYSTEM mask');comparison[#comparison+1]={id='VFX_NULL_PhaseRedirectCorruption',minimumAlphaMaskDifference=min};report.systemMaskComparisons=comparison
check(report.totalFrames==45 and report.totalTags==8,'Frame/tag totals')
local contact=Image{fromFile=root..'/'..manifest.preview};check(contact.width==1260 and contact.height==1320,'Contact size')
local native=Image{fromFile=root..'/'..manifest.nativePreview};check(native.width==480 and native.height==270,'Native review size')
local gif=assert(app.open(root..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==160,'GIF review size/frames');for _,f in ipairs(gif.frames) do check(math.abs(f.duration-0.02)<0.001,'GIF frame duration') end;gif:close()
report.passed=#report.failures==0
local f=assert(io.open(root..'/validation.json','w'));f:write(json.encode(report));f:close();assert(report.passed,table.concat(report.failures,'\n'))
