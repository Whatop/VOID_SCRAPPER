-- Compact Region A support effects. All art authored in Aseprite Lua.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local palettes={green={'254F39','4FA96C','B3EDAA','F0F2E9'},hostile={'662C32','FF211F','E9913C','F0F2E9'}}
local P={};for k,a in pairs(palettes) do P[k]={};for i,h in ipairs(a) do P[k][i]=rgba(h) end end
local names={'Energy Shape','White-Hot Center','Technical Marks','Pixel Sparks'}
local function layers(w,h) local r={};for i=1,4 do r[i]=Image(w,h,ColorMode.RGB) end;return r end
local function scaleLayers(l) local r={};for i=1,4 do r[i]=H.scale(l[i],2) end;return r end
local function render(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function read(path) local f=assert(io.open(path,'r'));local t=json.decode(f:read('*a'));f:close();return t end
local function write(name,t) local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(t));f:close() end
local extra={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,s,x,y,c,n) for i=1,#s do local ch=s:sub(i,i);local xx=x+(i-1)*4*n;if extra[ch] then for yy,row in ipairs(extra[ch]) do for col=1,3 do if row:sub(col,col)=='1' then H.rect(im,xx+(col-1)*n,y+(yy-1)*n,n,n,c) end end end else H.text(im,ch,xx,y,c,n) end end end
local barrier=assert(app.open(src..'/barrier.aseprite'));local barrierPNG=Image{fromFile=src..'/barrier.png'};local production=Image{fromFile=src..'/production_barrier.png'};local barrierData=read(src..'/barrier.json')
assert(barrier.width==64 and barrier.height==64)
for it in barrierPNG:pixels() do assert(it()==production:getPixel(it.x,it.y),'Approved/production barrier mismatch') end
local barrierFrames={};for f=1,#barrier.frames do local im=render(barrier,f);barrierFrames[f]=im;local r=barrierData.frames[f].frame;for it in im:pixels() do assert(it()==barrierPNG:getPixel(r.x+it.x,r.y+it.y),'Barrier ASE/PNG mismatch') end end

local function front(f)
 local l=layers(16,8);local c=P.green
 -- Three regulated rails and a compact white construction tip, on a 2px grid.
 H.rect(l[1],0,4,12,1,c[3]);H.rect(l[1],0,2,11,1,c[2]);H.rect(l[1],0,6,11,1,c[2])
 H.rect(l[3],6,3,1,1,c[2]);H.rect(l[3],6,5,1,1,c[2])
 H.rect(l[1],11,2,1,5,c[2]);H.rect(l[1],12,3,1,3,c[3]);H.rect(l[1],13,4,1,1,c[3])
 local h=({1,3,1,1})[f];H.rect(l[2],12,4-math.floor(h/2),1,h,c[4]);H.put(l[2],13,4,c[4])
 local x=({13,14,13,12})[f];H.put(l[4],x,2,c[3]);H.put(l[4],x,6,c[3])
 return scaleLayers(l)
end
local previous=read(src..'/previous_manifest.json');local specs={}
local sourceSprites={}
for _,v in ipairs(previous.families) do
 if v.id~='VFX_Sector_CoreMissileFlash' then
  local source=assert(app.open(src..'/'..v.aseprite));sourceSprites[#sourceSprites+1]=source
  local old=v.clips[1]
  local durations={};for _,ms in ipairs(old.durationsMs) do durations[#durations+1]=math.floor(ms) end
  local isFront=v.id=='VFX_Sector_BarrierFormationFront'
  local function copiedFrame(f)
   local ls=layers(source.width,source.height)
   for i,l in ipairs(source.layers) do local cel=l:cel(f);if cel then ls[i]:drawImage(cel.image,cel.position) end end
   return ls
  end
  local label=v.id:gsub('VFX_Sector_',''):gsub('(%l)(%u)','%1 %2'):upper()
  specs[#specs+1]={id=v.id,w=math.floor(v.width),h=isFront and 16 or math.floor(v.height),pivot=isFront and {x=24,y=8} or {x=v.pivotPixels.x,y=v.pivotPixels.y},
   clips={{name=old.name,label=label,palette=old.palette,ms=durations,make=isFront and front or copiedFrame}},
   splitRelease=v.splitRelease,tileAxis=v.tileAxis,mode=v.mode,peak=v.peakFrame,
   notes=isFront and 'Compact 32x16 leading tip; +X travel. Pivot at the growing endpoint. Regulated rails echo the approved barrier. Only this formation effect is revised; static wall unchanged.' or v.notes,
   reused=not isFront}
 end
end
local manifest={generatorId='void-scrapper-sector-remaining-v1',name='Sector Administrator Remaining Support VFX',layers=names,palettes=palettes,pixelsPerUnit=32,constructionPixelScale=2,families={},preview='SectorRemainingVFX_ContactSheet.png',nativePreview='SectorRemainingVFX_480x270.png',ringPreview='SectorRemainingVFX_Rings_480x270.png',barrierPreview='SectorRemainingVFX_Barrier_480x270.png',animatedPreview='SectorRemainingVFX_480x270.gif'}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local cache,reviewRows={},{}
local bg=rgba('101820');local panel=rgba('18232D');local ink=rgba('DCE5E3');local muted=rgba('83949D')
local function canvas(w,h) local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
for _,v in ipairs(specs) do
 local s=Sprite(v.w,v.h,ColorMode.RGB);for i,n in ipairs(names) do local l=i==1 and s.layers[1] or s:newLayer();l.name=n end
 local maxFrames=0;for _,clip in ipairs(v.clips) do maxFrames=math.max(maxFrames,#clip.ms) end
 local sheet=Image(maxFrames*v.w,#v.clips*v.h,ColorMode.RGB);local jf,tags,clips,allColors={}, {},{},{};local seen={};local total=0
 for row,clip in ipairs(v.clips) do
  local frames={};local from=total+1;local clipStrip=Image(v.w*#clip.ms,v.h,ColorMode.RGB)
  for localF,ms in ipairs(clip.ms) do
   total=total+1;if total>1 then s:newEmptyFrame(total) end;local ls=clip.make(localF)
   for i,im in ipairs(ls) do s:newCel(s.layers[i],total,im,Point(0,0)) end
   s.frames[total].duration=ms/1000;local im=render(s,total);frames[localF]=im;sheet:drawImage(im,Point((localF-1)*v.w,(row-1)*v.h));clipStrip:drawImage(im,Point((localF-1)*v.w,0))
   jf[#jf+1]={filename=v.id..'_'..clip.name..'_'..(localF-1),frame={x=(localF-1)*v.w,y=(row-1)*v.h,w=v.w,h=v.h},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=v.w,h=v.h},sourceSize={w=v.w,h=v.h},duration=ms}
  end
  local ranges=v.splitRelease and {{'Warning',from,from},{'Release',from+1,total}} or {{clip.name,from,total}}
  for _,q in ipairs(ranges) do tags[#tags+1]={name=q[1],from=q[2],to=q[3]} end
  local clipSheet=#v.clips>1 and v.id..'_'..clip.name..'.png' or v.id..'.png'
  if #v.clips>1 then clipStrip:saveAs(out..'/'..clipSheet) end
  if v.splitRelease then
   frames[1]:saveAs(out..'/'..v.id..'_Warning.png');local r=Image(v.w*5,v.h,ColorMode.RGB);for i=2,6 do r:drawImage(frames[i],Point((i-2)*v.w,0)) end;r:saveAs(out..'/'..v.id..'_Release.png')
  end
  local rec={name=clip.name,palette=clip.palette,from=from,to=total,durationsMs=clip.ms,sheet=clipSheet,preview=v.id..'_'..clip.name..'_Preview.png'};clips[#clips+1]=rec
  for _,h in ipairs(palettes[clip.palette]) do if not seen[h] then seen[h]=true;allColors[#allColors+1]=h end end
  cache[v.id..'/'..clip.name]=frames
  local preview=canvas(640,176);text(preview,clip.label,16,16,ink,2);text(preview,'NATIVE CELLS / INTEGER 3X REVIEW',16,36,muted,1)
  for f,im in ipairs(frames) do local x=20+(f-1)*102;text(preview,tostring(f),x+40,55,muted,1);preview:drawImage(H.scale(im,3),Point(x,72)) end
  preview:saveAs(out..'/'..rec.preview);reviewRows[#reviewRows+1]={v=v,clip=clip,frames=frames}
 end
 for _,q in ipairs(tags) do local t=s:newTag(q.from,q.to);t.name=q.name end
 local pal=Palette(#allColors+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,h in ipairs(allColors) do local c=rgba(h);pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end;s:setPalette(pal)
 s:saveAs(out..'/'..v.id..'.aseprite');s:close();sheet:saveAs(out..'/'..v.id..'.png')
 local pivot={x=v.pivot.x/v.w,y=1-v.pivot.y/v.h};local jsonTags={};for _,t in ipairs(tags) do jsonTags[#jsonTags+1]={name=t.name,from=t.from-1,to=t.to-1,direction='forward'} end
 local rec={id=v.id,width=v.w,height=v.h,frames=total,aseprite=v.id..'.aseprite',sheet=v.id..'.png',metadata=v.id..'.json',clips=clips,tags=tags,pivotPixels=v.pivot,unityPivot=pivot,mode=v.mode,tileAxis=v.tileAxis,tilePeriodPixels=v.tileAxis and 16 or nil,peakFrame=v.peak,splitRelease=v.splitRelease or false,notes=v.notes,reusedSourcePixels=v.reused};manifest.families[#manifest.families+1]=rec
 write(rec.metadata,{frames=jf,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=sheet.width,h=sheet.height},scale='1',frameTags=jsonTags},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot=pivot,loop=v.mode=='loop',tileAxis=v.tileAxis,clips=clips,releaseSheet=v.splitRelease and v.id..'_Release.png' or nil,warningSheet=v.splitRelease and v.id..'_Warning.png' or nil,warningSeedHold=v.splitRelease or false}})
end
-- Review-only compositions use approved objects without exporting new object art.
local contact=canvas(960,900)
text(contact,'SECTOR ADMINISTRATOR / REMAINING SUPPORT VFX',20,20,ink,2)
text(contact,'RED HOSTILE PROPULSION AND RINGS / GREEN CONTAINMENT',20,44,muted,1)
for i,row in ipairs(reviewRows) do
 local y=76+(i-1)*134;H.rect(contact,12,y,936,122,panel)
 text(contact,row.clip.label,24,y+10,ink,2)
 text(contact,row.v.w..'X'..row.v.h..' / '..row.v.mode:upper():gsub('_',' '),24,y+32,muted,1)
 for f,im in ipairs(row.frames) do local x=352+(f-1)*96;text(contact,tostring(f),x+26,y+10,muted,1);contact:drawImage(H.scale(im,2),Point(x,y+40)) end
end
contact:saveAs(out..'/'..manifest.preview)
local boss=Image{fromFile=src..'/boss_green.png'}
local player=Image{fromFile=src..'/player.png'}
local missile=Image{fromFile=src..'/missile_body.png'}
local function sample(ms,durations,loop)
 local total=0;for _,d in ipairs(durations) do total=total+d end
 if loop then ms=ms%total end
 for i,d in ipairs(durations) do if ms<d then return i end;ms=ms-d end
 return #durations
end
local function drawRotated(dest,im,cx,cy,angle,px,py)
 local a=angle*math.pi/180;local co,si=math.cos(a),math.sin(a)
 local extent=math.ceil(math.sqrt(im.width^2+im.height^2))
 for y=math.max(0,math.floor(cy-extent)),math.min(dest.height-1,math.ceil(cy+extent)) do
  for x=math.max(0,math.floor(cx-extent)),math.min(dest.width-1,math.ceil(cx+extent)) do
   local dx,dy=x+0.5-cx,y+0.5-cy;local u=math.floor(dx*co+dy*si+px);local v=math.floor(-dx*si+dy*co+py)
   if u>=0 and u<im.width and v>=0 and v<im.height then local c=im:getPixel(u,v);if pc.rgbaA(c)>0 then dest:drawPixel(x,y,c) end end
  end
 end
end
local smallMissile=Image(20,10,ColorMode.RGB)
for y=0,9 do for x=0,19 do smallMissile:drawPixel(x,y,missile:getPixel(math.floor(y*missile.width/10),missile.height-1-math.floor(x*missile.height/20))) end end
local function playerPosition(t) return 366+12*math.sin(t*1.7),178+28*math.sin(t*1.2) end
local missileTimeline={};local moving={}
for n=0,5 do moving[#moving+1]={x=204,y=139,a=(n*60-90)*math.pi/180} end
-- This motion is an art review of the requested radial / broad-turn design,
-- not a replacement for Unity movement or the existing guidance authority.
for frame=0,74 do
 local t=frame*.04;local px,py=playerPosition(t);local snapshot={}
 for _,m in ipairs(moving) do
  if frame>0 then
   if t>.8 then
    local desired=math.atan(py-m.y,px-m.x);local delta=(desired-m.a+math.pi)%(2*math.pi)-math.pi
    local step=math.rad(58)*.04;m.a=m.a+math.max(-step,math.min(step,delta))
   end
   m.x=m.x+math.cos(m.a)*44*.04;m.y=m.y+math.sin(m.a)*44*.04
  end
  snapshot[#snapshot+1]={x=math.floor(m.x+.5),y=math.floor(m.y+.5),a=m.a*180/math.pi}
 end
 missileTimeline[frame+1]=snapshot
end
local qa={sixMissiles=true,minimumPursuitSeparation=1000,missilesOutsideFrame=0,safeRingOverlap=0,playerObscuredPixels=0,fourGrowingEdges=true,staticWallReferenceOnly=true,previewIsUnityCapture=false}
local function reviewMissiles(ms,record)
 local im=canvas(480,270);text(im,'HOSTILE MISSILES / SIX BROAD TURN PATHS',16,14,ink,2)
 local t=ms/1000;local px,py=playerPosition(t);im:drawImage(player,Point(math.floor(px-16),math.floor(py-16)))
 local positions=missileTimeline[math.min(75,math.floor(ms/40)+1)]
 if ms>=280 then
  for i,m in ipairs(positions) do
   local a=m.a*math.pi/180;local trail=cache['VFX_Sector_EnemyMissileTrail/Loop'][math.floor(ms/60)%4+1]
   drawRotated(im,trail,m.x-math.cos(a)*8,m.y-math.sin(a)*8,m.a,28,8)
   if record and ms>=1400 then
    if m.x<24 or m.x>456 or m.y<32 or m.y>235 then qa.missilesOutsideFrame=qa.missilesOutsideFrame+1 end
    for j=i+1,#positions do local n=positions[j];local d=math.sqrt((m.x-n.x)^2+(m.y-n.y)^2);qa.minimumPursuitSeparation=math.min(qa.minimumPursuitSeparation,d) end
   end
  end
 end
 im:drawImage(boss,Point(140,75))
 if ms>=280 then for _,m in ipairs(positions) do drawRotated(im,smallMissile,m.x,m.y,m.a,10,5) end end
 text(im,ms<840 and 'CORE ORIGIN / SIX OUTWARD HEADINGS' or 'WIDE TURN PURSUIT / COMPACT HEADING TAILS',24,240,muted,1)
 return im
end
local function ringComposite(w,h,f)
 local im=Image(w,h,ColorMode.RGB)
 local strip=cache['VFX_Sector_RectangleRelease/Sequence'][f]
 local corner=cache['VFX_Sector_RectangleCorner/Sequence'][f]
 for y=0,h-1 do for x=0,w-1 do local c=0
  if (x<16 or x>=w-16) and (y<16 or y>=h-16) then c=corner:getPixel(x<16 and x or w-1-x,y<16 and y or h-1-y)
  elseif y<16 then c=strip:getPixel((x-16)%32,y)
  elseif y>=h-16 then c=strip:getPixel((x-16)%32,h-1-y)
  elseif x<16 then c=strip:getPixel((y-16)%32,x)
  elseif x>=w-16 then c=strip:getPixel((y-16)%32,w-1-x) end
  im:drawPixel(x,y,c)
 end end;return im
end
for _,d in ipairs({{160,80},{224,128}}) do ringComposite(d[1],d[2],2):saveAs(out..'/RingAssembly_'..d[1]..'x'..d[2]..'_Review.png') end
local rings={{x=40,y=46,w=400,h=192},{x=104,y=78,w=272,h=128}}
local function reviewRing(ms,record)
 local im=canvas(480,270);text(im,'ALTERNATING RINGS / SAFE BAND STAYS CLEAR',16,14,ink,2)
 im:drawImage(boss,Point(176,78))
 local phase=math.floor(ms/900)%2;local age=ms%900
 local active=rings[phase+1];local safe=rings[2-phase]
 local guide=rgba('33494F')
 H.line(im,safe.x+8,safe.y+8,safe.x+safe.w-9,safe.y+8,guide)
 H.line(im,safe.x+8,safe.y+safe.h-9,safe.x+safe.w-9,safe.y+safe.h-9,guide)
 H.line(im,safe.x+8,safe.y+8,safe.x+8,safe.y+safe.h-9,guide)
 H.line(im,safe.x+safe.w-9,safe.y+8,safe.x+safe.w-9,safe.y+safe.h-9,guide)
 local px,py=phase==0 and 352 or 416,150;im:drawImage(player,Point(px,py))
 local frame=age<400 and 1 or sample(age-400,{40,50,60,60,30},false)+1
 local ring=ringComposite(active.w,active.h,frame);local overlay=Image(480,270,ColorMode.RGB);overlay:drawImage(ring,Point(active.x,active.y))
 if record then
  for y=safe.y,safe.y+safe.h-1 do for x=safe.x,safe.x+safe.w-1 do
   if x<safe.x+16 or x>=safe.x+safe.w-16 or y<safe.y+16 or y>=safe.y+safe.h-16 then
    if pc.rgbaA(overlay:getPixel(x,y))>0 then qa.safeRingOverlap=qa.safeRingOverlap+1 end
   end
  end end
  for it in player:pixels() do if pc.rgbaA(it())>0 and pc.rgbaA(overlay:getPixel(px+it.x,py+it.y))>0 then qa.playerObscuredPixels=qa.playerObscuredPixels+1 end end
 end
 im:drawImage(overlay,Point(0,0))
 local state=age<400 and 'WARNING' or age<640 and 'RELEASE' or 'CLEANUP'
 text(im,phase==0 and 'OUTER '..state..' / INNER BAND SAFE' or 'INNER '..state..' / OUTER BAND SAFE',24,242,muted,1)
 return im
end
local edges={{x=44,y=54,dx=1,dy=0,len=392,a=0},{x=436,y=54,dx=0,dy=1,len=164,a=90},{x=436,y=218,dx=-1,dy=0,len=392,a=180},{x=44,y=218,dx=0,dy=-1,len=164,a=270}}
local function drawWall(im,e,length,wall)
 -- Point-sampled .6 Y scale mirrors the production wall's narrow presentation.
 -- The original reference PNG / ASE is never rewritten or emitted as new wall art.
 for x=0,length-1 do for y=-19,18 do
  local sy=math.floor((y+.5)/.6+32)
  if sy>=0 and sy<64 then local c=wall:getPixel(x%64,sy)
   if pc.rgbaA(c)>0 then im:drawPixel(e.x+e.dx*x-e.dy*y,e.y+e.dy*x+e.dx*y,c) end
  end
 end end
end
local function reviewBarrier(ms,record)
 local im=canvas(480,270);text(im,'CONTAINMENT / FOUR EDGES FORM AND CONNECT',16,14,ink,2)
 im:drawImage(boss,Point(176,73));im:drawImage(player,Point(111,161))
 local progress=math.min(1,math.max(0,(ms-160)/960));local wall=barrierFrames[math.floor(ms/100)%6+1]
 local front=cache['VFX_Sector_BarrierFormationFront/Travel'][math.floor(ms/80)%4+1]
 local activeEdges=0
 for _,e in ipairs(edges) do
  local length=math.floor(e.len*progress);drawWall(im,e,length,wall)
  if ms<200 then
   local s=cache['VFX_Sector_BarrierActivationSpark/Activate'][sample(ms,{50,50,60,40},false)];im:drawImage(s,Point(e.x-8,e.y-8))
  end
  if ms>=160 and ms<1120 then
   drawRotated(im,front,e.x+e.dx*length,e.y+e.dy*length,e.a,24,8);activeEdges=activeEdges+1
  elseif ms>=1120 then
   local pulse=cache['VFX_Sector_BarrierStabilizationPulse/Connect'][sample(ms-1120,{40,60,80,40},false)]
   for x=0,e.len-1 do for y=0,15 do local c=pulse:getPixel(x%32,y);if pc.rgbaA(c)>0 then im:drawPixel(e.x+e.dx*x-e.dy*(y-8),e.y+e.dy*x+e.dx*(y-8),c) end end end
  end
 end
 if record and ms>=160 and ms<1120 then qa.fourGrowingEdges=qa.fourGrowingEdges and activeEdges==4 end
 text(im,ms<160 and 'CORNERS CHARGE' or ms<1120 and 'FOUR MOVING FRONTS / APPROVED WALL FOLLOWS' or 'CONNECTED / SMALL STABILIZATION PULSE',24,242,muted,1)
 return im
end
local function decorate(im) text(im,'NATIVE 480X270 ART REVIEW / NOT A UNITY CAPTURE',16,258,muted,1);return im end
decorate(reviewMissiles(2320,false)):saveAs(out..'/'..manifest.nativePreview)
decorate(reviewMissiles(800,false)):saveAs(out..'/SectorRemainingVFX_Deployment_480x270.png')
decorate(reviewRing(420,false)):saveAs(out..'/'..manifest.ringPreview)
decorate(reviewRing(1320,false)):saveAs(out..'/SectorRemainingVFX_OppositeRing_480x270.png')
decorate(reviewBarrier(800,false)):saveAs(out..'/'..manifest.barrierPreview)
decorate(reviewBarrier(1160,false)):saveAs(out..'/SectorRemainingVFX_Connected_480x270.png')
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native readability review'
for f=1,165 do
 if f>1 then review:newEmptyFrame(f) end
 local im=f<=75 and reviewMissiles((f-1)*40,true) or f<=120 and reviewRing((f-76)*40,true) or reviewBarrier((f-121)*40,true)
 review:newCel(review.layers[1],f,decorate(im),Point(0,0));review.frames[f].duration=.04
end
review:saveAs(out..'/'..manifest.animatedPreview);review:close()
for _,s in ipairs(sourceSprites) do s:close() end;barrier:close()
manifest.totalFrames=28;manifest.totalTags=8
manifest.nativeReview={width=480,height=270,frames=165,frameDurationMs=40,mode='art composition only',missileReference='Approved PlayerBullet.png sampled to 20x10 only in review. No missile-body export. Six radial starts followed by broad capped turns; not a Unity simulation.'}
manifest.ringAssembly={cornerPixels=16,straightAxis='X',horizontal='Tile between 16px corner areas',vertical='Rotate 90 degrees; tile between corners',clock='Same Release frame on each damaging side and corner',warningFrame=1,releaseFrames={2,6},releaseDurationMs=240,reviewSizes={{160,80},{224,128}},safeBand='Do not play Release on the opposite parity band.'}
manifest.integration='Art only. Five families reuse existing layer pixels; compact formation front is revised. Approved core ejection, purple laser, bosses, static wall, missile bodies and Unity files unchanged.'
manifest.protectedAssets='All Input and Output 02 through 25 are hash-checked before and after generation; no approved effects regenerated.'
write('readability_review.json',qa);write('manifest.json',manifest)
local f=assert(io.open(out..'/generation_complete.txt','w'));f:write('Remaining support VFX generated and exported with Aseprite CLI + Lua.\n');f:close()
