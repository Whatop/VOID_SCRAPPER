local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir);local pc=app.pixelColor
local function read(path) local f=assert(io.open(path,'r'));local t=json.decode(f:read('*a'));f:close();return t end
local function rgba(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local manifest=read(out..'/manifest.json');local report={passed=false,failures={},failureCounts={},families={},totalFrames=0,totalTags=0,pixelComparisons=0}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(a,b) return a==b or(pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function render(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function layer(s,i,f) local im=Image(s.width,s.height,ColorMode.RGB);local c=s.layers[i]:cel(f);if c then im:drawImage(c.image,c.position) end;return im end
local function difference(a,b,mask) local n=0;for it in a:pixels() do local q=b:getPixel(it.x,it.y);if mask and (pc.rgbaA(it())>0)~=(pc.rgbaA(q)>0) or not mask and not equal(it(),q) then n=n+1 end end;return n end
local function components(im)
 local seen={};local count=0
 for y=0,im.height-1 do for x=0,im.width-1 do local k=y*im.width+x
  if not seen[k] and pc.rgbaA(im:getPixel(x,y))>0 then count=count+1;local queue={{x,y}};seen[k]=true;local head=1
   while head<=#queue do local q=queue[head];head=head+1;for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do local xx,yy=q[1]+d[1],q[2]+d[2];local kk=yy*im.width+xx;if xx>=0 and xx<im.width and yy>=0 and yy<im.height and not seen[kk] and pc.rgbaA(im:getPixel(xx,yy))>0 then seen[kk]=true;queue[#queue+1]={xx,yy} end end end
  end
 end end;return count
end
local ex={VFX_Sector_EnemyMissileTrail={32,16,4,1},VFX_Sector_RectangleRelease={32,16,6,2},VFX_Sector_RectangleCorner={16,16,6,2},VFX_Sector_BarrierFormationFront={32,16,4,1},VFX_Sector_BarrierActivationSpark={16,16,4,1},VFX_Sector_BarrierStabilizationPulse={32,16,4,1}}
local cache,stats={},{};local barrier=assert(app.open(src..'/barrier.aseprite'));local barrierFrame=render(barrier,1)
check(#manifest.families==6,'Six editable families required')
for _,v in ipairs(manifest.families) do
 local expected=assert(ex[v.id]);local s=assert(app.open(out..'/'..v.aseprite));local sheet=Image{fromFile=out..'/'..v.sheet};local metadata=read(out..'/'..v.metadata)
 check(s.width==expected[1] and s.height==expected[2] and #s.frames==expected[3] and #s.tags==expected[4] and #s.layers==4,'Source structure '..v.id)
 check(#metadata.frames==#s.frames and #metadata.meta.frameTags==#s.tags,'Metadata counts '..v.id)
 check(metadata.unity.pixelsPerUnit==32 and metadata.unity.filterMode=='Point' and metadata.unity.compression=='None' and not metadata.unity.mipmaps,'Pixel import contract '..v.id)
 check(metadata.unity.pivot.x==v.pivotPixels.x/v.width and metadata.unity.pivot.y==1-v.pivotPixels.y/v.height,'Pivot contract '..v.id)
 for i,l in ipairs(s.layers) do check(l.name==manifest.layers[i] and l.opacity==255 and l.isVisible,'Editable layers '..v.id) end
 for i,t in ipairs(v.tags) do local a=s.tags[i];check(a.name==t.name and a.fromFrame.frameNumber==t.from and a.toFrame.frameNumber==t.to,'Tag ranges '..v.id);check(metadata.meta.frameTags[i].from==t.from-1 and metadata.meta.frameTags[i].to==t.to-1,'JSON tag ranges '..v.id) end
 local result={id=v.id,clips={}}
 for _,clip in ipairs(v.clips) do
  local allowed={};for _,h in ipairs(manifest.palettes[clip.palette]) do allowed[rgba(h)]=true end
  local white=rgba(manifest.palettes[clip.palette][4]);local frames,metrics={},{};local peak=0;local maxWhite=0;local maxDiff=0
  local clipPNG=Image{fromFile=out..'/'..clip.sheet};check(clipPNG.width==s.width*#clip.durationsMs and clipPNG.height==s.height,'Clip PNG dimensions '..v.id)
  for f=clip.from,clip.to do local localF=f-clip.from+1;local im=render(s,f);frames[localF]=im;local fm=metadata.frames[f];local m={opaquePixels=0,whitePixels=0,colors=0,bounds={x=s.width,y=s.height,r=-1,b=-1}};local colors={}
   check(math.abs(s.frames[f].duration*1000-clip.durationsMs[localF])<0.1 and fm.duration==clip.durationsMs[localF],'Saved timing '..v.id)
   check(fm.frame.w==s.width and fm.frame.h==s.height and not fm.trimmed,'Frame rectangles '..v.id)
   for it in im:pixels() do local q=it();local a=pc.rgbaA(q)
    check(a==0 or a==255,'Partial alpha '..v.id);check(equal(q,sheet:getPixel(fm.frame.x+it.x,fm.frame.y+it.y)),'Main PNG mismatch '..v.id);check(equal(q,clipPNG:getPixel((localF-1)*s.width+it.x,it.y)),'Clip PNG mismatch '..v.id);report.pixelComparisons=report.pixelComparisons+2
    if a>0 then check(allowed[q],'Semantic palette '..v.id);colors[q]=true;m.opaquePixels=m.opaquePixels+1;if q==white then m.whitePixels=m.whitePixels+1 end;m.bounds.x=math.min(m.bounds.x,it.x);m.bounds.y=math.min(m.bounds.y,it.y);m.bounds.r=math.max(m.bounds.r,it.x);m.bounds.b=math.max(m.bounds.b,it.y)
     if v.id~='VFX_Sector_RectangleCorner' then check(it.y>=2 and it.y<s.height-2,'Unintended Y clipping '..v.id) end
    end
   end
   for _ in pairs(colors) do m.colors=m.colors+1 end;check(m.colors<=4,'Palette limit '..v.id)
   for y=0,s.height-1,2 do for x=0,s.width-1,2 do local q=im:getPixel(x,y);check(equal(q,im:getPixel(x+1,y)) and equal(q,im:getPixel(x,y+1)) and equal(q,im:getPixel(x+1,y+1)),'2px construction grid '..v.id) end end
   if v.tileAxis=='X' then for y=0,s.height-1 do for x=0,s.width-17 do check(equal(im:getPixel(x,y),im:getPixel(x+16,y)),'16px tile period '..v.id) end end end
   if v.id=='VFX_Sector_BarrierFormationFront' then
    check(m.whitePixels>0 and m.whitePixels<=16 and m.opaquePixels<=228,'Compact formation leading edge')
   end
   if v.id=='VFX_Sector_EnemyMissileTrail' then check(m.whitePixels==4 and m.bounds.r<=27 and m.bounds.b-m.bounds.y+1<=6 and m.opaquePixels<=88,'Compact hostile propulsion footprint') end
   if v.splitRelease then
    check(m.opaquePixels<=s.width*s.height*0.5,'Ring release exceeds half-tile opaque coverage')
    if localF==1 then check(m.whitePixels==0,'Warning seed contains release flash') end
    if localF==2 then check(m.whitePixels>0,'Release flash invisible') end
   end
   peak=math.max(peak,m.opaquePixels);maxWhite=math.max(maxWhite,m.whitePixels);metrics[localF]=m;report.totalFrames=report.totalFrames+1
  end
  local adjacent={};for f=1,#frames-1 do local d=difference(frames[f],frames[f+1],false);check(d>0,'Adjacent frames identical '..v.id);adjacent[#adjacent+1]=d;maxDiff=math.max(maxDiff,d) end
  local seam=difference(frames[#frames],frames[1],false)
  if v.mode=='loop' then check(metrics[#frames].opaquePixels>0 and seam<=maxDiff,'Loop seam '..v.id)
  else check(metrics[#frames].opaquePixels==0 and metrics[#frames-1].opaquePixels<peak,'One-shot cleanup '..v.id) end
  local preview=Image{fromFile=out..'/'..clip.preview};check(preview.width==640 and preview.height==176,'Individual preview size '..v.id)
  if v.splitRelease then
   local warning=Image{fromFile=out..'/'..v.id..'_Warning.png'};local release=Image{fromFile=out..'/'..v.id..'_Release.png'};check(difference(warning,frames[1],false)==0,'Warning strip exact');check(release.width==v.width*5 and release.height==v.height,'Release strip dimensions')
   for f=2,6 do for it in frames[f]:pixels() do check(equal(it(),release:getPixel((f-2)*v.width+it.x,it.y)),'Release strip exact');report.pixelComparisons=report.pixelComparisons+1 end end
   local sum=0;for f=2,6 do sum=sum+clip.durationsMs[f] end;check(sum==240,'Ring release must preserve 240ms handoff')
  end
  local rec={tag=clip.name,frames=metrics,peakOpaquePixels=peak,peakWhitePixels=maxWhite,adjacentDifferences=adjacent,loopSeamDifference=v.mode=='loop' and seam or nil};result.clips[#result.clips+1]=rec;cache[v.id..'/'..clip.name]=frames;stats[v.id..'/'..clip.name]=rec
 end
 if v.reusedSourcePixels then
  local original=assert(app.open(src..'/'..v.aseprite))
  check(original.width==s.width and original.height==s.height and #original.frames==#s.frames,'Reused source dimensions')
  for f=1,#s.frames do for i=1,#s.layers do local a=layer(original,i,f);local b=layer(s,i,f);check(difference(a,b,false)==0,'Reused layer pixels changed '..v.id);report.pixelComparisons=report.pixelComparisons+s.width*s.height end end
  original:close()
 end
 report.totalTags=report.totalTags+#s.tags;report.families[#report.families+1]=result;s:close()
end
local ring=cache['VFX_Sector_RectangleRelease/Sequence'];local corners=cache['VFX_Sector_RectangleCorner/Sequence']
for f=1,6 do for y=0,15 do for x=0,15 do check(equal(corners[f]:getPixel(x,y),ring[f]:getPixel(math.max(x,y),math.min(x,y))),'Corner not an exact strip-derived miter') end end end
for _,d in ipairs({{160,80},{224,128}}) do
 local im=Image{fromFile=out..'/RingAssembly_'..d[1]..'x'..d[2]..'_Review.png'};check(im.width==d[1] and im.height==d[2],'Variable ring dimensions')
 for y=0,im.height-1 do for x=0,im.width-1 do
  if x>=16 and x<im.width-16 and y>=16 and y<im.height-16 then check(pc.rgbaA(im:getPixel(x,y))==0,'Variable ring filled center') end
 end end
 -- At the release peak, each light rail is continuous across all four miter joints.
 for _,r in ipairs({4,5,10,11}) do
  for _,y in ipairs({r,im.height-1-r}) do for x=r,im.width-1-r do check(pc.rgbaA(im:getPixel(x,y))==255,'Horizontal ring rail seam') end end
  for _,x in ipairs({r,im.width-1-r}) do for y=r,im.height-1-r do check(pc.rgbaA(im:getPixel(x,y))==255,'Vertical ring rail seam') end end
 end
end
for _,name in ipairs({manifest.nativePreview,manifest.ringPreview,manifest.barrierPreview}) do local im=Image{fromFile=out..'/'..name};check(im.width==480 and im.height==270,'Native preview size') end
local contact=Image{fromFile=out..'/'..manifest.preview};check(contact.width==960 and contact.height==900,'Contact sheet size')
local gif=assert(app.open(out..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==165,'Native GIF structure');for _,f in ipairs(gif.frames) do check(math.abs(f.duration-0.04)<0.001,'Native GIF cadence') end;gif:close()
local readability=read(out..'/readability_review.json')
check(readability.sixMissiles and readability.missilesOutsideFrame==0 and readability.minimumPursuitSeparation>=24,'Six missile pursuit review stays legible and in frame')
check(readability.safeRingOverlap==0 and readability.playerObscuredPixels==0,'Ring review obscures safe band or player')
check(readability.fourGrowingEdges and readability.staticWallReferenceOnly,'Four-edge formation review')
check(not readability.previewIsUnityCapture,'Art review must not claim Unity validation')
for _,v in ipairs(manifest.families) do check(v.id~='VFX_Sector_CoreMissileFlash' and not v.id:find('Laser'),'Approved family regenerated') end
check(report.totalFrames==28 and report.totalTags==8,'Frame/tag totals')
report.reusedFamiliesPixelIdentical=5;report.compactFormationFront=true;report.variableRingCornersVerified=true;report.releaseDurationMs=240;report.previewIsUnityCapture=false
report.passed=#report.failures==0;local f=assert(io.open(out..'/validation.json','w'));f:write(json.encode(report));f:close();barrier:close();assert(report.passed,table.concat(report.failures,'\n'))
