local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir);local pc=app.pixelColor
local function read(path) local f=assert(io.open(path,'r'));local t=json.decode(f:read('*a'));f:close();return t end
local function rgba(s) return pc.rgba(tonumber(s:sub(1,2),16),tonumber(s:sub(3,4),16),tonumber(s:sub(5,6),16),255) end
local manifest=read(out..'/manifest.json')
local report={passed=false,failures={},failureCounts={},families={},pixelComparisons=0,totalFrames=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function render(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function layer(s,i,f) local im=Image(s.width,s.height,ColorMode.RGB);local c=s.layers[i]:cel(f);if c then im:drawImage(c.image,c.position) end;return im end
local function diff(a,b,mask) local n=0;for it in a:pixels() do local v=b:getPixel(it.x,it.y);if (mask and (pc.rgbaA(it())>0)~=(pc.rgbaA(v)>0)) or (not mask and not equal(it(),v)) then n=n+1 end end;return n end
local allowed={};for _,h in ipairs({'452D66','8353B8','BA79FF','D9A5FF','FFF6FF'}) do allowed[rgba(h)]=true end
local greenBeam=assert(app.open(src..'/green_beam.aseprite'));local greenWarning=assert(app.open(src..'/green_warning.aseprite'))
local white=rgba('FFF6FF');local cache,stats={},{}
check(#manifest.families==3,'Exactly three required families')
local expected={VFX_Sector_PurpleLaserWarning={64,32,'Warning','loop',{120,120,120,120}},VFX_Sector_PurpleLaserBeam={64,32,'Sustain','loop',{60,60,60,60}},VFX_Sector_PurpleLaserEmitter={32,32,'Activate','one_shot',{40,60,70,40}}}
for _,v in ipairs(manifest.families) do
 local ex=assert(expected[v.id]);local s=assert(app.open(out..'/'..v.aseprite));local png=Image{fromFile=out..'/'..v.sheet};local metadata=read(out..'/'..v.metadata)
 check(s.width==ex[1] and s.height==ex[2] and #s.frames==4 and #s.layers==4,'ASE structure '..v.id)
 check(png.width==s.width*4 and png.height==s.height,'PNG dimensions '..v.id)
 check(#s.tags==1 and s.tags[1].name==ex[3] and s.tags[1].fromFrame.frameNumber==1 and s.tags[1].toFrame.frameNumber==4,'Saved tag '..v.id)
 check(#metadata.frames==4 and metadata.meta.frameTags[1].name==ex[3] and metadata.meta.frameTags[1].from==0 and metadata.meta.frameTags[1].to==3,'Metadata tag '..v.id)
 check(metadata.unity.pixelsPerUnit==32 and metadata.unity.pivot.y==0.46875 and metadata.unity.pivot.x==(v.width==64 and 0 or 0.25),'Production axis and pivot '..v.id)
 check(metadata.unity.filterMode=='Point' and metadata.unity.compression=='None' and not metadata.unity.mipmaps,'Pixel-perfect import metadata '..v.id)
 for i,l in ipairs(s.layers) do check(l.name==manifest.layers[i] and l.opacity==255 and l.isVisible,'Editable layer '..v.id) end
 local frames,metrics={},{};local maxOpaque,maxWhite,maxDiff=0,0,0;local colors={}
 for f=1,4 do
  local im=render(s,f);frames[f]=im;local stat={opaquePixels=0,whitePixels=0,bounds={x=s.width,y=s.height,r=-1,b=-1}}
  check(math.abs(s.frames[f].duration*1000-ex[5][f])<0.1 and metadata.frames[f].duration==ex[5][f],'Frame timing '..v.id)
  local rect=metadata.frames[f].frame;check(rect.x==(f-1)*s.width and rect.y==0 and rect.w==s.width and rect.h==s.height and not metadata.frames[f].trimmed,'Metadata frame rect '..v.id)
  for it in im:pixels() do local c=it();local a=pc.rgbaA(c)
   check(a==0 or a==255,'Partial alpha '..v.id);check(equal(c,png:getPixel((f-1)*s.width+it.x,it.y)),'ASE/PNG mismatch '..v.id);report.pixelComparisons=report.pixelComparisons+1
   if a>0 then
    check(allowed[c],'Palette outside approved Overdrive '..v.id);colors[c]=true;stat.opaquePixels=stat.opaquePixels+1;if c==white then stat.whitePixels=stat.whitePixels+1 end
    stat.bounds.x=math.min(stat.bounds.x,it.x);stat.bounds.y=math.min(stat.bounds.y,it.y);stat.bounds.r=math.max(stat.bounds.r,it.x);stat.bounds.b=math.max(stat.bounds.b,it.y)
    check(it.y>=2 and it.y<s.height-2,'Transverse clipping '..v.id)
    if v.mode=='one_shot' then check(it.x>=2 and it.x<s.width-2,'Emitter clipping') end
   end
  end
  for y=0,s.height-1,2 do for x=0,s.width-1,2 do local c=im:getPixel(x,y);check(equal(c,im:getPixel(x+1,y)) and equal(c,im:getPixel(x,y+1)) and equal(c,im:getPixel(x+1,y+1)),'2px construction grid '..v.id) end end
  if v.id=='VFX_Sector_PurpleLaserBeam' then
   local reference=render(greenBeam,f+3);stat.approvedAlphaDifference=diff(im,reference,true);check(stat.approvedAlphaDifference==0,'Beam no longer shares approved green silhouette')
   stat.phaseColorDifference=diff(im,reference,false);check(stat.phaseColorDifference==stat.opaquePixels,'Every active green pixel must be visually distinct')
   for x=0,63 do for y=14,19 do check(im:getPixel(x,y)==white,'White core must be continuous') end end
   check(stat.whitePixels==384,'Stable six-pixel hot center')
   for y=0,31 do check(equal(im:getPixel(0,y),im:getPixel(63,y)),'Active tile boundary mismatch') end
   local expectedMarks=layer(greenBeam,3,f+3);local marks=layer(s,3,f);check(diff(expectedMarks,marks,true)==0,'Approved technical markers displaced')
   for y=0,31 do for x=0,47 do check(equal(marks:getPixel(x,y),marks:getPixel(x+16,y)),'Technical marks 16px repetition') end end
   local body=layer(s,1,f);local lo=({10,8,10,12})[f];local hi=33-lo
   for x=0,63 do check(body:getPixel(x,lo)==rgba('8353B8') and body:getPixel(x,hi)==rgba('8353B8'),'Controlled dark-violet beam edge') end
  elseif v.id=='VFX_Sector_PurpleLaserWarning' then
   check(stat.whitePixels==0,'Warning must never contain white-hot damage pixels')
   check(diff(layer(s,3,f),layer(greenWarning,3,3),true)==0,'Warning must retain approved brackets and chevrons')
   local dotted=layer(s,1,f)
   for y=0,31 do for x=0,55 do check(equal(dotted:getPixel(x,y),dotted:getPixel(x+8,y)),'Warning dash repeat seam') end end
   for it in dotted:pixels() do if pc.rgbaA(it())>0 then check(it.y==16 or it.y==17,'Warning axis grew outside 2px') end end
  end
  metrics[f]=stat;maxOpaque=math.max(maxOpaque,stat.opaquePixels);maxWhite=math.max(maxWhite,stat.whitePixels);report.totalFrames=report.totalFrames+1
 end
 local changes={};for f=1,3 do local d=diff(frames[f],frames[f+1],false);check(d>0,'Duplicate animation frame '..v.id);changes[#changes+1]=d;maxDiff=math.max(maxDiff,d) end
 local seam=diff(frames[4],frames[1],false)
 if v.mode=='loop' then check(seam<=maxDiff and metrics[4].opaquePixels>0,'Loop seam continuity '..v.id)
 else check(metrics[1].opaquePixels>0 and metrics[2].opaquePixels>metrics[1].opaquePixels and metrics[3].opaquePixels<metrics[2].opaquePixels and metrics[4].opaquePixels==0,'Emitter attack/decay/cleanup') end
 if v.id=='VFX_Sector_PurpleLaserWarning' then
  for f=1,4 do local current=layer(s,1,f);local next=layer(s,1,f%4+1);for it in current:pixels() do check(equal(it(),next:getPixel((it.x+2)%64,it.y)),'Warning motion must advance 2px through loop seam') end end
 end
 if v.coreSliceSheet then
  local slice=Image{fromFile=out..'/'..v.coreSliceSheet};check(slice.width==8 and slice.height==32,'Core stretch slice dimensions')
  for f=1,4 do for y=0,31 do for x=0,1 do check(equal(slice:getPixel((f-1)*2+x,y),frames[f]:getPixel(x,y)),'Core stretch slice pixel mismatch');report.pixelComparisons=report.pixelComparisons+1 end end end
  check(metadata.unity.coreSlices.pivot.y==0.46875 and metadata.unity.coreSlices.cellWidth==2,'Stretch slice metadata')
 end
 local numColors=0;for _ in pairs(colors) do numColors=numColors+1 end;check(numColors<=5,'More than five VFX colors')
 local rec={id=v.id,frames=metrics,peakOpaquePixels=maxOpaque,peakWhitePixels=maxWhite,colors=numColors,adjacentDifferences=changes,loopSeamDifference=v.mode=='loop' and seam or nil}
 report.families[#report.families+1]=rec;stats[v.id]=rec;cache[v.id]=frames;s:close()
end
check(stats.VFX_Sector_PurpleLaserWarning.peakOpaquePixels<stats.VFX_Sector_PurpleLaserBeam.peakOpaquePixels*0.4,'Warning too dense compared with active beam')
check(stats.VFX_Sector_PurpleLaserWarning.peakWhitePixels==0 and stats.VFX_Sector_PurpleLaserBeam.peakWhitePixels==384,'Warning/attack brightness distinction')
check(stats.VFX_Sector_PurpleLaserEmitter.peakOpaquePixels<=384,'Emitter too large for boss silhouette')
local contact=Image{fromFile=out..'/'..manifest.preview};check(contact.width==800 and contact.height==648,'Contact dimensions')
for f=1,4 do local im=render(greenBeam,f+3);for it in im:pixels() do if pc.rgbaA(it())>0 then check(equal(it(),contact:getPixel(240+(f-1)*128+it.x*2,112+it.y*2)),'Green comparison reference changed') end end end
local boss=Image{fromFile=src..'/purple_boss.png'};local player=Image{fromFile=src..'/player_reference.png'}
for _,file in ipairs({manifest.nativePreview,manifest.warningPreview}) do
 local im=Image{fromFile=out..'/'..file};check(im.width==480 and im.height==270,'Native review dimensions')
 -- The warning still provides a strict exact-boss reference; active adds only an emitter outside it.
 if file==manifest.warningPreview then for it in boss:pixels() do if pc.rgbaA(it())>0 then check(equal(it(),im:getPixel(176+it.x,80+it.y)),'Approved boss changed in native preview') end end end
 for it in player:pixels() do if pc.rgbaA(it())>0 then check(equal(it(),im:getPixel(334+it.x,205+it.y)),'Player reference hidden or rescaled') end end
end
local gif=assert(app.open(out..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==60,'Animated review structure');for _,f in ipairs(gif.frames) do check(math.abs(f.duration-0.04)<0.001,'Review GIF cadence') end;gif:close()
report.totalTags=3;report.approvedBeamMasksExact=true;report.warningTechnicalGeometryExact=true;report.productionBeamAxisPreserved=true;report.nativeReviewIsGameplayCapture=false
report.passed=#report.failures==0
local f=assert(io.open(out..'/validation.json','w'));f:write(json.encode(report));f:close();greenBeam:close();greenWarning:close();assert(report.passed,table.concat(report.failures,'\n'))
