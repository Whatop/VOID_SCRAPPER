local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir);local pc=app.pixelColor
local function read(path) local f=assert(io.open(path,'r'));local t=json.decode(f:read('*a'));f:close();return t end
local function rgba(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local function render(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function layer(s,i,f) local im=Image(s.width,s.height,ColorMode.RGB);local c=s.layers[i]:cel(f);if c then im:drawImage(c.image,c.position) end;return im end
local function equal(a,b) return a==b or(pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function difference(a,b,alpha) local n=0;for it in a:pixels() do local q=b:getPixel(it.x,it.y);if alpha and (pc.rgbaA(it())>0)~=(pc.rgbaA(q)>0) or not alpha and not equal(it(),q) then n=n+1 end end;return n end
local manifest=read(out..'/manifest.json')
local report={passed=false,failures={},failureCounts={},families={},totalFrames=0,totalTags=0,pixelComparisons=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failureCounts[msg]=0;report.failures[#report.failures+1]=msg end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local expected={VFX_Sector_BossArrivalMarker={96,96,6,1,4},VFX_Sector_BossMaterialization={96,96,5,1,4},VFX_Sector_BossShield={128,128,8,2,4},VFX_Sector_BossShieldHit={32,32,8,2,4},VFX_Sector_BossShieldRelease={128,128,12,2,4},VFX_Sector_PhaseTransitionRing={112,112,5,1,4}}
check(#manifest.families==6,'Six editable families required')
local cache,stats={},{}
for _,v in ipairs(manifest.families) do
 local s=assert(app.open(out..'/'..v.aseprite));local ex=assert(expected[v.id]);local sheet=Image{fromFile=out..'/'..v.sheet};local metadata=read(out..'/'..v.metadata)
 check(s.width==ex[1] and s.height==ex[2] and #s.frames==ex[3] and #s.tags==ex[4] and #s.layers==ex[5],'Source structure '..v.id)
 check(#metadata.frames==#s.frames and #metadata.meta.frameTags==#s.tags,'Metadata count '..v.id)
 check(metadata.unity.pixelsPerUnit==32 and metadata.unity.filterMode=='Point' and metadata.unity.compression=='None' and not metadata.unity.mipmaps,'Pixel import settings '..v.id)
 check(metadata.unity.pivot.x==v.pivotPixels.x/v.width and metadata.unity.pivot.y==1-v.pivotPixels.y/v.height,'Pivot '..v.id)
 for i,l in ipairs(s.layers) do check(l.name==v.layers[i] and l.opacity==255 and l.isVisible,'Editable layer '..v.id) end
 for i,t in ipairs(v.tags) do local tag=s.tags[i];check(tag.name==t.name and tag.fromFrame.frameNumber==t.from and tag.toFrame.frameNumber==t.to,'Tags '..v.id);check(metadata.meta.frameTags[i].from==t.from-1 and metadata.meta.frameTags[i].to==t.to-1,'JSON tag offsets '..v.id) end
 local result={id=v.id,clips={}}
 for _,clip in ipairs(v.clips) do
  local allowed={};local whites={}
  local function add(name) for _,h in ipairs(manifest.palettes[name]) do allowed[rgba(h)]=true end end
  if clip.palette=='green_purple' then add('green');add('purple') else add(clip.palette) end
  if v.mode=='states' then add('metal') end
  whites[rgba('F0F2E9')]=true;whites[rgba('FFF6FF')]=true;whites[rgba('E8FAFF')]=true
  local strip=Image{fromFile=out..'/'..clip.sheet};local count=clip.to-clip.from+1
  check(strip.width==s.width*count and strip.height==s.height,'Clip strip dimensions '..v.id)
  local frames,metrics={},{}
  for f=clip.from,clip.to do
   local n=f-clip.from+1;local im=render(s,f);frames[n]=im;local meta=metadata.frames[f]
   local m={opaque=0,white=0,colorCount=0,bounds={x=s.width,y=s.height,r=-1,b=-1}};local colors={}
   check(math.abs(s.frames[f].duration*1000-clip.durationsMs[n])<.01 and meta.duration==clip.durationsMs[n],'Saved duration '..v.id)
   check(meta.frame.w==s.width and meta.frame.h==s.height and not meta.trimmed,'Untrimmed frame rectangles '..v.id)
   for it in im:pixels() do
    local c=it();local a=pc.rgbaA(c);check(a==0 or a==255,'Partial alpha '..v.id)
    check(equal(c,sheet:getPixel(meta.frame.x+it.x,meta.frame.y+it.y)) and equal(c,strip:getPixel((n-1)*s.width+it.x,it.y)),'PNG export mismatch '..v.id);report.pixelComparisons=report.pixelComparisons+2
    if a>0 then
     m.opaque=m.opaque+1;if whites[c] then m.white=m.white+1 end;colors[c]=true
     check(allowed[c],'Unexpected color '..v.id)
     check(not (pc.rgbaR(c)>pc.rgbaG(c)*1.7 and pc.rgbaR(c)>pc.rgbaB(c)*1.5),'Red faction color '..v.id)
     m.bounds.x=math.min(m.bounds.x,it.x);m.bounds.y=math.min(m.bounds.y,it.y);m.bounds.r=math.max(m.bounds.r,it.x);m.bounds.b=math.max(m.bounds.b,it.y)
     check(it.x>=2 and it.x<s.width-2 and it.y>=2 and it.y<s.height-2,'Effect clips canvas '..v.id)
    end
   end
   for _ in pairs(colors) do m.colorCount=m.colorCount+1 end;check(m.colorCount<=v.maxColors,'Limited palette '..v.id)
   if v.mode~='states' then
    for y=0,s.height-1,2 do for x=0,s.width-1,2 do local c=im:getPixel(x,y);check(equal(c,im:getPixel(x+1,y)) and equal(c,im:getPixel(x,y+1)) and equal(c,im:getPixel(x+1,y+1)),'2px VFX construction '..v.id) end end
   else
    for it in im:pixels() do check(equal(it(),im:getPixel(s.width-1-it.x,it.y)),'Relay bilateral symmetry') end
   end
   metrics[n]=m;report.totalFrames=report.totalFrames+1
  end
  if v.mode=='loop' then
   local maxDiff=0
   for f=1,#frames-1 do local d=difference(frames[f],frames[f+1],false);check(d>0,'Loop has duplicate adjacent frames');maxDiff=math.max(maxDiff,d) end
   check(difference(frames[#frames],frames[1],false)<=maxDiff,'Loop wrap jump')
   for f=2,#frames do check(difference(frames[1],frames[f],true)==0,'Stable shield footprint changes during loop') end
  end
  if v.mode=='one_shot' then
   check(metrics[1].opaque>0 and metrics[#metrics].opaque==0,'One-shot first and cleanup frame '..v.id)
   for f=1,#frames-1 do check(difference(frames[f],frames[f+1],false)>0,'Identical neighboring VFX frames '..v.id) end
  end
  local preview=Image{fromFile=out..'/'..clip.preview};check(preview.width==1024 and preview.height==224,'Individual preview size '..v.id)
  stats[v.id..'/'..clip.name]=metrics;cache[v.id..'/'..clip.name]=frames;result.clips[#result.clips+1]={name=clip.name,frames=metrics}
 end

 report.families[#report.families+1]=result;report.totalTags=report.totalTags+#s.tags;s:close()
end
-- FAMILY_READABILITY
local function peak(key,field)local n=0;for _,m in ipairs(stats[key])do n=math.max(n,m[field])end;return n end
for _,id in ipairs({'VFX_Sector_BossShield','VFX_Sector_BossShieldHit','VFX_Sector_BossShieldRelease'})do
 local a,b=cache[id..'/Green'],cache[id..'/Purple']
 for f=1,#a do check(difference(a[f],b[f],true)==0,'Green/Purple geometry differs '..id)
  for it in a[f]:pixels()do if pc.rgbaA(it())>0 then check(it()~=b[f]:getPixel(it.x,it.y),'Energy variant color unchanged '..id)end end
 end
end
local boss=Image{fromFile=src..'/boss_green.png'};local shieldOverlap=0;local minClear=1
for _,family in ipairs({'Green','Purple'})do
 for _,im in ipairs(cache['VFX_Sector_BossShield/'..family])do
  local opaque=0
  for it in im:pixels()do if pc.rgbaA(it())>0 then opaque=opaque+1
   if pc.rgbaA(boss:getPixel(it.x,it.y))>0 then shieldOverlap=shieldOverlap+1 end
   check(it.x<16 or it.x>=112 or it.y<16 or it.y>=112,'Shield has filled interior')
  end end
  local clear=1-opaque/(128*128);minClear=math.min(minClear,clear);check(clear>.92,'Shield is too opaque')
 end
end
check(shieldOverlap==0,'Shield obscures approved boss / emitter pixels')
local normal=Image{fromFile=out..'/SectorIntroShield_GreenShield_480x270.png'}
for it in boss:pixels()do if pc.rgbaA(it())>0 then check(it()==normal:getPixel(176+it.x,74+it.y),'Native shield preview changes visible boss pixel');report.pixelComparisons=report.pixelComparisons+1 end end
local marker=cache['VFX_Sector_BossArrivalMarker/Arrival']
for f,im in ipairs(marker)do local center=0;for y=32,63 do for x=32,63 do if pc.rgbaA(im:getPixel(x,y))>0 then center=center+1 end end end;check(center<=20,'Arrival center obscured')end
local burst=cache['VFX_Sector_BossMaterialization/Materialize']
for f=3,5 do for y=32,63 do for x=32,63 do check(pc.rgbaA(burst[f]:getPixel(x,y))==0,'Materialization covers core after reveal')end end end
local markerWhite=peak('VFX_Sector_BossArrivalMarker/Arrival','white');local burstWhite=peak('VFX_Sector_BossMaterialization/Materialize','white')
local shieldWhite=peak('VFX_Sector_BossShield/Green','white');local hitWhite=peak('VFX_Sector_BossShieldHit/Green','white')
check(markerWhite>0 and burstWhite>=markerWhite*3,'Anticipation/appearance brightness hierarchy')
check(shieldWhite<=20 and shieldWhite<burstWhite and hitWhite<=12,'Shield/hit brightness hierarchy')
check(peak('VFX_Sector_BossShieldHit/Green','opaque')<=96,'Shield hit exceeds local footprint')
check(peak('VFX_Sector_BossMaterialization/Materialize','opaque')<96*96*.12,'Large opaque materialization flash')
for _,family in ipairs({'Green','Purple'})do
 local m=stats['VFX_Sector_BossShieldRelease/'..family]
 for f=2,#m do check(m[f].opaque<m[f-1].opaque,'Release does not progressively retract')end
end
for _,name in ipairs({manifest.nativePreview,'SectorIntroShield_ArrivalMarker_480x270.png','SectorIntroShield_Materialize_480x270.png','SectorIntroShield_GreenShield_480x270.png','SectorIntroShield_LocalHit_480x270.png','SectorIntroShield_Transition_480x270.png','SectorIntroShield_Release_480x270.png'})do
 local im=Image{fromFile=out..'/'..name};check(im.width==480 and im.height==270,'Native preview dimensions')
end
local contact=Image{fromFile=out..'/'..manifest.preview};check(contact.width==1280 and contact.height==1664,'Contact sheet dimensions')
for _,entry in ipairs({{manifest.animatedPreview,100},{manifest.arrivalPreview,40},{manifest.overdrivePreview,35}})do
 local gif=assert(app.open(out..'/'..entry[1]));check(gif.width==480 and gif.height==270 and #gif.frames==entry[2],'Sequence GIF structure')
 for _,f in ipairs(gif.frames)do check(math.abs(f.duration-.04)<.001,'Sequence GIF cadence')end;gif:close()
end
check(report.totalFrames==44 and report.totalTags==9,'Pack totals')
report.minimumShieldTransparentFraction=minClear;report.shieldBossOverlapPixels=shieldOverlap
report.greenPurpleSameGeometry=true;report.markerPeakWhitePixels=markerWhite;report.materializationPeakWhitePixels=burstWhite;report.shieldPeakWhitePixels=shieldWhite;report.hitPeakWhitePixels=hitWhite
report.previewIsUnityCapture=false;report.approvedBossModified=false
report.passed=#report.failures==0
local f=assert(io.open(out..'/validation.json','w'));f:write(json.encode(report));f:close();assert(report.passed,table.concat(report.failures,'\n'))
