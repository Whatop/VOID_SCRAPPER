local root=assert(app.params.outputDir)
local refs=assert(app.params.sourceDir)
local pc=app.pixelColor
local function readJson(path) local f=assert(io.open(path,'r'));local v=json.decode(f:read('*a'));f:close();return v end
local manifest=readJson(root..'/manifest.json')
local report={passed=true,failures={},failureCounts={},families={},totalFrames=0,totalClips=0,pixelComparisons=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local function equal(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function diff(a,b) local n=0;for it in a:pixels() do if not equal(it(),b:getPixel(it.x,it.y)) then n=n+1 end end;return n end
local white=color('F0F2E9');local lightBlue=color('B8EFFF');local orange=color('E9913C');local paleOrange=color('FFE0A2')
local expected={VFX_Barrage_HeavyImpact={64,{Impact=6}},VFX_Phase_Portal={64,{Open=8,Close=8}},VFX_Phase_RedirectFlash={32,{Redirect=4}}}
local cache,metrics={},{}
check(#manifest.families==3,'Only three requested families')
for _,family in ipairs(manifest.families) do
 local e=assert(expected[family.id]);local size=e[1];local s=assert(app.open(root..'/'..family.aseprite));local sheet=assert(Image{fromFile=root..'/'..family.sheet});local meta=readJson(root..'/'..family.metadata)
 local n,t=0,0;for _,frames in pairs(e[2]) do n=n+frames;t=t+1 end
 check(s.width==size and s.height==size and #s.frames==n and #s.layers==4 and #s.tags==t,'Aseprite size/counts: '..family.id)
 check(sheet.width==family.atlasColumns*size and sheet.height==t*size,'Sheet size: '..family.id)
 check(#meta.frames==n and #meta.meta.frameTags==t,'Metadata counts: '..family.id)
 check(meta.unity.pixelsPerUnit==32 and meta.unity.filterMode=='Point' and meta.unity.compression=='None' and not meta.unity.mipmaps,'Import metadata: '..family.id)
 local allowed={};for _,v in ipairs(family.palette) do allowed[color(v)]=true end
 for li,name in ipairs(manifest.layers) do check(s.layers[li].name==name and s.layers[li].opacity==255 and s.layers[li].isVisible,'Editable visible source layers: '..family.id) end
 cache[family.id]={};metrics[family.id]={};local result={id=family.id,clips={}}
 for row,c in ipairs(family.clips) do
  local length=assert(e[2][c.tag]);local tag=s.tags[row];local mt=meta.meta.frameTags[row]
  check(c.frames==length and tag.name==c.tag and tag.fromFrame.frameNumber==c.fromFrame and tag.toFrame.frameNumber==c.toFrame,'Tag range: '..family.id..' '..c.tag)
  check(mt.name==c.tag and mt.from==c.fromFrame-1 and mt.to==c.toFrame-1,'Metadata tag: '..family.id)
  local strip=assert(Image{fromFile=root..'/'..c.sheet});check(strip.width==length*size and strip.height==size,'Strip size: '..family.id)
  local images,frameData,colors={},{},{};local maxVisible,maxWhite,totalMs=0,0,0
  for fi=1,length do
   local frame=c.fromFrame+fi-1;local im=Image(size,size,ColorMode.RGB);im:drawSprite(s,frame,Point(0,0));images[fi]=im
   check(math.abs(s.frames[frame].duration*1000-c.durationsMs[fi])<0.6,'Frame timing: '..family.id)
   local fm=meta.frames[frame];check(fm.duration==c.durationsMs[fi] and fm.frame.x==(fi-1)*size and fm.frame.y==(row-1)*size and fm.frame.w==size and fm.frame.h==size and not fm.trimmed,'Frame metadata: '..family.id)
   local visible,hot,inner=0,0,0;local minX,minY,maxX,maxY=size,size,-1,-1
   for it in im:pixels() do
    local p=it();local a=pc.rgbaA(p);check(a==0 or a==255,'Partial alpha: '..family.id)
    check(equal(p,sheet:getPixel((fi-1)*size+it.x,(row-1)*size+it.y)),'Atlas mismatch: '..family.id)
    check(equal(p,strip:getPixel((fi-1)*size+it.x,it.y)),'Strip mismatch: '..family.id);report.pixelComparisons=report.pixelComparisons+2
    if a>0 then
     visible=visible+1;colors[p]=true;check(allowed[p],'Palette mismatch: '..family.id)
     if p==white then hot=hot+1 end
     if p==white or p==lightBlue then if it.x>=20 and it.x<44 and it.y>=14 and it.y<50 then inner=inner+1 end end
     minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
     check(it.x>=2 and it.y>=2 and it.x<size-2 and it.y<size-2,'Clipped boundary: '..family.id)
    end
   end
   for y=0,size-2,2 do for x=0,size-2,2 do local p=im:getPixel(x,y);check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Integer pixel grid: '..family.id) end end
   if family.id=='VFX_Barrage_HeavyImpact' then
    check(visible>=128,'Barrage frame empty or too sparse: '..fi)
    local bright=0;for it in im:pixels() do local p=it();if p==white or p==orange or p==paleOrange then bright=bright+1 end end
    check(bright>=128,'Barrage frame lacks bright opaque pixels: '..fi)
    if fi<=3 then check(hot>=200,'Barrage initial center is too faint: '..fi) end
   end
   if family.id=='VFX_Phase_Portal' and c.tag=='Open' and fi>=4 then check(inner>=80,'Portal missing bright interior distortion');check(visible>=1000,'Portal missing spatial volume') end
   local data={index=fi,opaquePixels=visible,whitePixels=hot,innerBlueWhitePixels=inner,durationMs=c.durationsMs[fi],bounds=visible>0 and {x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1} or {x=0,y=0,w=0,h=0}}
   frameData[fi]=data;maxVisible=math.max(maxVisible,visible);maxWhite=math.max(maxWhite,hot);totalMs=totalMs+c.durationsMs[fi];report.totalFrames=report.totalFrames+1
  end
  local colorCount=0;for _ in pairs(colors) do colorCount=colorCount+1 end;check(colorCount<=4,'Palette exceeds four colors')
  check(frameData[1].opaquePixels>0,'Blank starting frame: '..family.id)
  if c.mode=='one_shot' then check(frameData[length].opaquePixels==0,'Cleanup missing: '..family.id..' '..c.tag) else check(frameData[length].opaquePixels>0,'Unexpected blank final frame') end
  local deltas={};for i=1,length-1 do local d=diff(images[i],images[i+1]);check(d>0,'Adjacent identical frames: '..family.id..' '..c.tag);deltas[#deltas+1]=d end
  local r={tag=c.tag,frames=frameData,durationMs=totalMs,maxOpaquePixels=maxVisible,maxWhitePixels=maxWhite,colors=colorCount,adjacentDifferences=deltas}
  result.clips[#result.clips+1]=r;cache[family.id][c.tag]=images;metrics[family.id][c.tag]=r;report.totalClips=report.totalClips+1
 end
 report.families[#report.families+1]=result;s:close()
end
local function peak(path,size)
 local im=assert(Image{fromFile=path});local visible,hot=0,0
 for fi=0,im.width//size-1 do local v,w=0,0;for y=0,size-1 do for x=0,size-1 do local p=im:getPixel(fi*size+x,y);if pc.rgbaA(p)>0 then v=v+1 end;if p==white then w=w+1 end end end;visible=math.max(visible,v);hot=math.max(hot,w) end
 return {opaquePixels=visible,whitePixels=hot}
end
local generic=peak(refs..'/VFX_Explosion_Medium.png',64)
local previous=peak(refs..'/VFX_Barrage_HeavyImpact.png',64)
local revised=metrics.VFX_Barrage_HeavyImpact.Impact
check(revised.maxOpaquePixels>generic.opaquePixels*1.5 and revised.maxWhitePixels>previous.whitePixels*1.5,'Impact must exceed generic size and previous flash strength')
report.impactComparison={generic=generic,previous=previous,revised={opaquePixels=revised.maxOpaquePixels,whitePixels=revised.maxWhitePixels}}
local prevPortal=peak(refs..'/VFX_Phase_Portal_Open.png',64);local gate=metrics.VFX_Phase_Portal.Open
check(gate.maxOpaquePixels>prevPortal.opaquePixels*3,'Portal visible volume increase')
check(diff(cache.VFX_Phase_Portal.Open[8],cache.VFX_Phase_Portal.Close[1])==0,'Open final must equal Close first')
report.portalComparison={previous=prevPortal,revisedOpaquePixels=gate.maxOpaquePixels,revisedWhitePixels=gate.maxWhitePixels,openCloseMatch=true}
local prevRedirect=peak(refs..'/VFX_Phase_RedirectFlash.png',32);local flash=metrics.VFX_Phase_RedirectFlash.Redirect
check(flash.frames[1].whitePixels>prevRedirect.whitePixels,'Redirect center must brighten')
check(flash.frames[2].whitePixels>0 and flash.frames[2].opaquePixels>100,'Redirect afterimage missing')
local dir=cache.VFX_Phase_RedirectFlash.Redirect[2];check(dir:getPixel(28,16)==lightBlue and pc.rgbaA(dir:getPixel(14,16))==255,'Rightward displaced arrow missing')
report.redirectComparison={previous=prevRedirect,revisedFirstWhitePixels=flash.frames[1].whitePixels,afterimageOpaquePixels=flash.frames[2].opaquePixels}
check(report.totalFrames==26 and report.totalClips==4,'Revision totals')
local contact=assert(Image{fromFile=root..'/'..manifest.preview});check(contact.width==1120 and contact.height==744,'Contact sheet dimensions')
local native=assert(Image{fromFile=root..'/'..manifest.nativePreview});check(native.width==480 and native.height==270,'Native preview dimensions')
local gif=assert(app.open(root..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==60,'Animated review dimensions')
for _,frame in ipairs(gif.frames) do check(math.abs(frame.duration-0.02)<0.001,'Animated review timing') end
-- Ensure the revised impact appears immediately in the GIF, not after a blank telegraph delay.
local first=Image(480,270,ColorMode.RGB);first:drawSprite(gif,1,Point(0,0));check(first:getPixel(80,193)==white,'Animated preview starts without visible impact');gif:close()
report.passed=#report.failures==0
local f=assert(io.open(root..'/validation.json','w'));f:write(json.encode(report));f:close()
assert(report.passed,'Revision validation failed; see validation.json');print('SYSTEM_VFX_REVISIONS_VALIDATED')
