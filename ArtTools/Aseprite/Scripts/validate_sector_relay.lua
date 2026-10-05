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
local expected={Sector_LaserRelay={48,48,2,2,3},VFX_Sector_RelayDeploy={48,48,6,1,4},VFX_Sector_RelayDisconnect={16,16,5,1,4},VFX_Sector_RelayReconnect={32,16,5,1,4},VFX_Sector_RelayStabilize={16,16,6,2,4}}
check(#manifest.families==5,'Five editable families required')
local cache={}
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
  if clip.palette=='cyan_purple' then add('cyan');add('purple') else add(clip.palette) end
  if v.mode=='states' then add('metal') end
  whites[rgba('EDFFDC')]=true;whites[rgba('FFF6FF')]=true;whites[rgba('E8FAFF')]=true
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
  if v.mode=='one_shot' then
   check(metrics[1].opaque>0 and metrics[#metrics].opaque==0,'One-shot first and cleanup frame '..v.id)
   for f=1,#frames-1 do check(difference(frames[f],frames[f+1],false)>0,'Identical neighboring VFX frames '..v.id) end
  end
  local preview=Image{fromFile=out..'/'..clip.preview};check(preview.width==640 and preview.height==224,'Individual preview size '..v.id)
  cache[v.id..'/'..clip.name]=frames;result.clips[#result.clips+1]={name=clip.name,frames=metrics}
 end
 if v.id=='Sector_LaserRelay' then
  local green,purple=render(s,1),render(s,2)
  report.relaySilhouetteChanges=difference(green,purple,true);check(report.relaySilhouetteChanges==0,'Relay silhouette changed between states')
  report.chassisPixelChanges=difference(layer(s,1,1),layer(s,1,2),false);check(report.chassisPixelChanges==0,'Relay armor changed between states')
  report.poweredPixelsChanged=difference(green,purple,false);check(report.poweredPixelsChanged>=35 and report.poweredPixelsChanged<300,'Relay powered-pixel scope')
  local sourceSprite=assert(app.open(src..'/relay_source.aseprite'));local source=render(sourceSprite,1);local energy={};for _,h in ipairs(manifest.palettes.green) do energy[rgba(h)]=true end
  local chassis=layer(s,1,1)
  for it in chassis:pixels() do local sx=it.x<24 and math.floor((it.x+.5)*64/48) or 63-math.floor((47-it.x+.5)*64/48);local c=source:getPixel(sx,math.floor((it.y+.5)*64/48));local expect=energy[c] and 0 or c;check(equal(it(),expect),'Approved sampled chassis was redesigned');report.pixelComparisons=report.pixelComparisons+1 end
  local em1,en1,em2,en2=layer(s,2,1),layer(s,3,1),layer(s,2,2),layer(s,3,2)
  for it in green:pixels() do
   local x,y=it.x,it.y;if not equal(it(),purple:getPixel(x,y)) then check(pc.rgbaA(em1:getPixel(x,y))>0 or pc.rgbaA(en1:getPixel(x,y))>0,'Non-powered pixel recolored') end
  end
  check(difference(em1,em2,true)==0 and difference(en1,en2,true)==0,'Powered layer alpha changed')
  sourceSprite:close()
 end
 report.families[#report.families+1]=result;report.totalTags=report.totalTags+#s.tags;s:close()
end
for f=1,3 do check(difference(cache['VFX_Sector_RelayStabilize/Green'][f],cache['VFX_Sector_RelayStabilize/Purple'][f],true)==0,'Green/Purple stabilization geometry differs') end
check(manifest.relay.sharedDesigns==1 and #manifest.formation.existingIds==4 and #manifest.formation.additionalIds==2 and #manifest.formation.sixFinalPositions==6,'One reusable relay for all six slots')
check(manifest.handoff.approvedBeamRegenerated==false and manifest.handoff.unityFilesModified==false,'Approved assets/runtime scope')
for _,name in ipairs({manifest.nativePreview,'SectorRelay_Disconnect_480x270.png','SectorRelay_Deploy_480x270.png','SectorRelay_Reconnect_480x270.png','SectorRelay_Rotation_480x270.png'}) do local im=Image{fromFile=out..'/'..name};check(im.width==480 and im.height==270,'Native preview dimensions') end
local comparison=Image{fromFile=out..'/'..manifest.comparison};check(comparison.width==640 and comparison.height==296,'Green/Purple comparison dimensions')
local contact=Image{fromFile=out..'/'..manifest.preview};check(contact.width==960 and contact.height==1190,'Contact sheet dimensions')
local formation=Image{fromFile=out..'/'..manifest.formationPreview};local native=Image{fromFile=out..'/'..manifest.nativePreview};check(formation.width==960 and formation.height==540,'Formation preview dimensions')
for it in native:pixels() do for dy=0,1 do for dx=0,1 do check(it()==formation:getPixel(it.x*2+dx,it.y*2+dy),'Formation review is not exact 2x');report.pixelComparisons=report.pixelComparisons+1 end end end
local gif=assert(app.open(out..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==100,'Native GIF structure');for _,f in ipairs(gif.frames) do check(math.abs(f.duration-.04)<.001,'Native GIF timing') end;gif:close()
check(report.totalFrames==24 and report.totalTags==7,'Pack totals')
report.previewIsUnityCapture=false;report.sharedRelayDesigns=1;report.existingRelays=4;report.additionalRelays=2;report.approvedBeamUsedAsReferenceOnly=true
report.passed=#report.failures==0
local f=assert(io.open(out..'/validation.json','w'));f:write(json.encode(report));f:close();assert(report.passed,table.concat(report.failures,'\n'))
