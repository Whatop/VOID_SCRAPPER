local root=assert(app.params.outputDir)
local refs=assert(app.params.sourceDir)
local pc=app.pixelColor
local function readJson(path) local f=assert(io.open(path,'r'));local v=json.decode(f:read('*a'));f:close();return v end
local manifest=readJson(root..'/manifest.json')
local report={passed=true,failures={},failureCounts={},families={},totalClips=0,totalFrames=0,pixelComparisons=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local function equal(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function difference(a,b) local n=0;for it in a:pixels() do if not equal(it(),b:getPixel(it.x,it.y)) then n=n+1 end end;return n end
local white=color('F0F2E9');local blue=color('B8EFFF');local gray1=color('30383E');local gray2=color('596369')
local expected={VFX_Raider_HeavyMuzzle={64,64,{Fire=4}},VFX_Raider_BarrageTelegraph={64,64,{Warning=6}},VFX_Raider_DamageSparks={32,32,{Burst=4}},VFX_Raider_CriticalDamage={64,64,{Loop=8}},VFX_Raider_Assault_WeaponsHot={32,32,{Heat=6}},VFX_Raider_Salvage_Beam={96,32,{Pull=6}},VFX_Raider_Sniper_Rail={96,32,{Lock=3,Shot=3}}}
check(#manifest.families==7,'Expected seven Raider families')
local cache,metrics={},{}
for _,family in ipairs(manifest.families) do
 local e=assert(expected[family.id]);local w,height=e[1],e[2];local s=assert(app.open(root..'/'..family.aseprite));local sheet=assert(Image{fromFile=root..'/'..family.sheet});local meta=readJson(root..'/'..family.metadata)
 local n,t=0,0;for _,frames in pairs(e[3]) do n=n+frames;t=t+1 end
 check(s.width==w and s.height==height and #s.frames==n and #s.tags==t and #s.layers==4,'Source dimensions/frames/tags/layers: '..family.id)
 check(sheet.width==family.atlasColumns*w and sheet.height==t*height,'Sheet dimensions: '..family.id)
 check(#meta.frames==n and #meta.meta.frameTags==t,'Metadata counts: '..family.id)
 check(meta.unity.pixelsPerUnit==32 and meta.unity.filterMode=='Point' and meta.unity.compression=='None' and not meta.unity.mipmaps,'Pixel import settings: '..family.id)
 check(math.abs(meta.unity.pivot.x-family.pivotPixels.x/w)<0.000001 and math.abs(meta.unity.pivot.y-(1-family.pivotPixels.y/height))<0.000001,'Pivot metadata: '..family.id)
 local allowed={};for _,v in ipairs(family.palette) do allowed[color(v)]=true end
 for i,name in ipairs(manifest.layers) do check(s.layers[i].name==name and s.layers[i].opacity==255 and s.layers[i].isVisible,'Editable layers: '..family.id) end
 cache[family.id]={};metrics[family.id]={};local result={id=family.id,width=w,height=height,clips={}}
 for row,c in ipairs(family.clips) do
  local length=assert(e[3][c.tag]);local tag=s.tags[row];local mt=meta.meta.frameTags[row]
  check(c.frames==length and tag.name==c.tag and tag.fromFrame.frameNumber==c.fromFrame and tag.toFrame.frameNumber==c.toFrame,'Source tag range: '..family.id..' '..c.tag)
  check(mt.name==c.tag and mt.from==c.fromFrame-1 and mt.to==c.toFrame-1,'JSON tag range: '..family.id..' '..c.tag)
  local strip=assert(Image{fromFile=root..'/'..c.sheet});check(strip.width==length*w and strip.height==height,'Tag strip dimensions: '..family.id)
  local frames,frameData,colors,flowFrames={},{},{},{};local peak,hotPeak,duration=0,0,0
  for fi=1,length do
   local frame=c.fromFrame+fi-1;local im=Image(w,height,ColorMode.RGB);im:drawSprite(s,frame,Point(0,0));frames[fi]=im
   check(math.abs(s.frames[frame].duration*1000-c.durationsMs[fi])<0.6,'Saved frame duration: '..family.id)
   local fm=meta.frames[frame];check(fm.duration==c.durationsMs[fi] and fm.frame.x==(fi-1)*w and fm.frame.y==(row-1)*height and fm.frame.w==w and fm.frame.h==height and not fm.trimmed,'Frame JSON: '..family.id)
   local visible,hot,cold,smoke=0,0,0,0;local minX,minY,maxX,maxY=w,height,-1,-1
   for it in im:pixels() do
    local p=it();local a=pc.rgbaA(p);check(a==0 or a==255,'Partial alpha: '..family.id)
    check(equal(p,sheet:getPixel((fi-1)*w+it.x,(row-1)*height+it.y)),'Atlas mismatch: '..family.id)
    check(equal(p,strip:getPixel((fi-1)*w+it.x,it.y)),'Strip mismatch: '..family.id);report.pixelComparisons=report.pixelComparisons+2
    if a>0 then
     visible=visible+1;colors[p]=true;check(allowed[p],'Unexpected palette color: '..family.id)
     if p==white then hot=hot+1 end;if p==blue then cold=cold+1 end;if p==gray1 or p==gray2 then smoke=smoke+1 end
     minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
     check(it.y>=2 and it.y<height-2 and (family.tileAxis=='X' or (it.x>=2 and it.x<w-2)),'Unintended canvas clipping: '..family.id)
    end
   end
   for y=0,height-2,2 do for x=0,w-2,2 do local p=im:getPixel(x,y);check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Construction pixel grid: '..family.id) end end
   if c.mode=='handoff' then check(hot==0 and cold==0,'Warning contains white/blue attack flash: '..family.id) end
   if family.id=='VFX_Raider_Salvage_Beam' then
    for y=0,height-1 do for x=0,w-25 do check(equal(im:getPixel(x,y),im:getPixel(x+24,y)),'Salvage repeating tile period') end end
    local cel=s.layers[3]:cel(frame);local flow=Image(w,height,ColorMode.RGB);flow:drawImage(cel.image,cel.position);flowFrames[fi]=flow
   end
   if family.id=='VFX_Raider_CriticalDamage' then
    check(smoke>0 and visible<=420,'Critical smoke missing or too dense')
    check((fi==1 or fi==5) == (hot>0),'Intermittent critical flashes')
   end
   frameData[fi]={durationMs=c.durationsMs[fi],opaquePixels=visible,whitePixels=hot,blueWhitePixels=cold,smokePixels=smoke,bounds=visible>0 and {x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1} or {x=0,y=0,w=0,h=0}}
   peak=math.max(peak,visible);hotPeak=math.max(hotPeak,hot);duration=duration+c.durationsMs[fi];report.totalFrames=report.totalFrames+1
  end
  local paletteSize=0;for _ in pairs(colors) do paletteSize=paletteSize+1 end;check(paletteSize<=6,'Palette exceeds six colors: '..family.id)
  check(frameData[1].opaquePixels>0,'Blank first frame: '..family.id..' '..c.tag)
  if c.mode=='one_shot' then check(frameData[length].opaquePixels==0 and frameData[length-1].opaquePixels<peak,'One-shot cleanup/decay: '..family.id) else check(frameData[length].opaquePixels>0,'Empty loop or handoff frame: '..family.id) end
  local diffs,maxDiff={},0;for i=1,length-1 do local d=difference(frames[i],frames[i+1]);check(d>0,'Duplicate adjacent frames: '..family.id);diffs[#diffs+1]=d;maxDiff=math.max(maxDiff,d) end
  local seam
  if c.mode=='loop' then seam=difference(frames[length],frames[1]);check(seam<=maxDiff,'Loop seam larger than internal transitions: '..family.id) end
  if family.id=='VFX_Raider_Salvage_Beam' then
   for i=1,length do local nextFrame=flowFrames[i%length+1];for y=0,height-1 do for x=0,w-1 do check(equal(flowFrames[i]:getPixel((x+4)%w,y),nextFrame:getPixel(x,y)),'Salvage flow must move left exactly four pixels, including loop seam') end end end
   report.salvageFlow={direction='-X toward emitter',pixelsPerFrame=4,periodPixels=24,loopMatches=true}
  end
  local data={tag=c.tag,mode=c.mode,durationMs=duration,frames=frameData,peakOpaquePixels=peak,peakWhitePixels=hotPeak,colors=paletteSize,adjacentDifferences=diffs,loopSeamDifference=seam}
  result.clips[#result.clips+1]=data;cache[family.id][c.tag]=frames;metrics[family.id][c.tag]=data;report.totalClips=report.totalClips+1
 end
 report.families[#report.families+1]=result;s:close()
end
local normal=assert(Image{fromFile=refs..'/VFX_Raider_Muzzle.png'});local normalPeak=0
for fi=0,2 do local visible=0;for y=0,31 do for x=0,31 do if pc.rgbaA(normal:getPixel(fi*32+x,y))>0 then visible=visible+1 end end end;normalPeak=math.max(normalPeak,visible) end
local heavy=metrics.VFX_Raider_HeavyMuzzle.Fire
check(heavy.peakOpaquePixels>normalPeak*2,'Heavy muzzle must exceed normal Raider muzzle')
report.muzzleComparison={normalPeakOpaquePixels=normalPeak,heavyPeakOpaquePixels=heavy.peakOpaquePixels}
local lock=metrics.VFX_Raider_Sniper_Rail.Lock;local shot=metrics.VFX_Raider_Sniper_Rail.Shot
check(lock.durationMs==540 and shot.durationMs==130 and shot.frames[1].whitePixels>=150,'Rail telegraph/shot timing or brightness')
check(report.totalFrames==40 and report.totalClips==8,'Pack frame/clip totals')
local contact=assert(Image{fromFile=root..'/'..manifest.preview});check(contact.width==1320 and contact.height==748,'Contact dimensions')
local native=assert(Image{fromFile=root..'/'..manifest.nativePreview});check(native.width==480 and native.height==270,'Native preview dimensions')
local gif=assert(app.open(root..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==60,'Review GIF dimensions/count')
for _,frame in ipairs(gif.frames) do check(math.abs(frame.duration-0.02)<0.001,'Review GIF timing') end;gif:close()
report.passed=#report.failures==0
local f=assert(io.open(root..'/validation.json','w'));f:write(json.encode(report));f:close()
assert(report.passed,'Raider VFX validation failed; see validation.json');print('RAIDER_BOSS_VFX_VALIDATED')
