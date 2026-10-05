-- Compact Region A support effects. All art authored in Aseprite Lua.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local palettes={green={'254F39','4FA96C','B3EDAA','F0F2E9'},purple={'452D66','BA79FF','D9A5FF','FFF6FF'},hostile={'662C32','FF211F','E9913C','F0F2E9'}}
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
local function ports(l,c,small)
 local a={{1,7},{13,7},{4,2},{10,2},{4,12},{10,12}}
 for _,q in ipairs(a) do H.rect(l,q[1],q[2],small and 1 or 2,small and 1 or 2,c) end
end
local function core(f,family)
 local l=layers(16,16);local c=P[family]
 if f==1 then
  for _,x in ipairs({4,10}) do for _,y in ipairs({4,10}) do H.rect(l[3],x,y,2,1,c[2]);H.rect(l[3],x,y,1,2,c[2]) end end
  H.diamond(l[1],7.5,7.5,1.5,c[3])
 elseif f==2 then
  H.diamond(l[1],7.5,7.5,4.5,c[2]);H.diamond(l[1],7.5,7.5,2.5,c[3]);H.diamond(l[2],7.5,7.5,1.5,c[4])
 elseif f==3 then
  for _,x in ipairs({3,10}) do for _,y in ipairs({3,10}) do H.rect(l[3],x,y,3,1,c[2]);H.rect(l[3],x,y,1,3,c[3]) end end
  H.rect(l[1],5,7,6,2,c[2]);H.rect(l[1],7,5,2,6,c[2]);H.rect(l[2],7,7,2,2,c[4])
 elseif f==4 then ports(l[4],c[3],false);H.rect(l[1],7,7,2,2,c[2])
 elseif f==5 then ports(l[4],c[1],true) end
 return scaleLayers(l)
end
local function trail(f)
 local l=layers(16,8);local c=P.hostile
 H.rect(l[1],11,3,3,3,c[2]);H.rect(l[2],13,4,1,1,c[4])
 H.rect(l[3],10,3,1,1,c[1]);H.rect(l[3],10,5,1,1,c[1])
 for x=2,10 do local phase=(x+f-1)%4;if phase<2 then H.rect(l[1],x,4,1,1,x<5 and c[1] or c[2]) end end
 if f==2 then H.rect(l[1],10,4,1,1,c[2]) end
 if f==3 then H.rect(l[3],8,3,1,1,c[1]);H.rect(l[3],8,5,1,1,c[1]) end
 return scaleLayers(l)
end
local function ring(f)
 local l=layers(16,8);local c=P.hostile
 for x=0,15 do local u=x%8
  if f==1 then if u%4<2 then H.put(l[1],x,1,c[2]);H.put(l[1],x,6,c[2]) end
  elseif f==2 then H.put(l[1],x,1,c[2]);H.put(l[1],x,6,c[2]);H.put(l[2],x,2,c[4]);H.put(l[2],x,5,c[4])
  elseif f==3 then
   if u<4 then H.put(l[1],x,1,c[2]);H.put(l[1],x,6,c[2]) end
   if u==2 or u==3 then H.put(l[3],x,2,c[3]);H.put(l[3],x,5,c[3]) end
   if u==6 then H.put(l[2],x,3,c[4]);H.put(l[2],x,4,c[4]) end
  elseif f==4 then
   if u==1 or u==5 then H.put(l[4],x,1,c[2]);H.put(l[4],x,6,c[2]) end
   if u==3 then H.put(l[4],x,2,c[3]);H.put(l[4],x,5,c[3]) end
  elseif f==5 then if u==2 or u==6 then H.put(l[4],x,2,c[1]);H.put(l[4],x,5,c[1]) end end
 end
 return scaleLayers(l)
end
local function corner(f)
 local strips=ring(f);local l=layers(16,16)
 for i,im in ipairs(strips) do for y=0,15 do for x=0,15 do l[i]:drawPixel(x,y,im:getPixel(math.max(x,y),math.min(x,y))) end end end
 return l
end
local function front(f)
 local l=layers(32,32);local c=P.green
 -- Exact approved barrier pixels provide the small stabilizing wake, never a new wall.
 for y=0,31 do for x=0,19 do l[1]:drawPixel(x,y,barrierFrames[1]:getPixel(x,y+16)) end end
 for _,q in ipairs({{6,c[1]},{12,c[2]},{16,c[3]},{20,c[2]},{26,c[1]}}) do H.rect(l[1],20,q[1],4,2,q[2]) end
 H.rect(l[1],22,8,2,18,c[2]);H.rect(l[1],24,12,2,10,c[3]);H.rect(l[1],26,16,2,2,c[3])
 local h=({6,10,6,2})[f];H.rect(l[2],24,17-h/2,2,h,c[4]);H.rect(l[2],26,16,2,2,c[4])
 local x=({26,28,26,24})[f];H.rect(l[4],x,6,2,2,c[3]);H.rect(l[4],x,26,2,2,c[3])
 return l
end
local function spark(f)
 local l=layers(8,8);local c=P.green
 if f==1 then H.rect(l[1],3,3,2,2,c[2]);H.put(l[3],1,3,c[2]);H.put(l[3],6,4,c[2])
 elseif f==2 then H.diamond(l[1],3.5,3.5,2.5,c[2]);H.rect(l[2],3,3,2,2,c[4]);H.rect(l[3],1,3,1,2,c[3]);H.rect(l[3],6,3,1,2,c[3])
 elseif f==3 then for _,q in ipairs({{2,2},{5,2},{2,5},{5,5}}) do H.put(l[4],q[1],q[2],c[3]) end end
 return scaleLayers(l)
end
local function pulse(f)
 local l=layers(16,8);local c=P.green
 for x=0,15 do local u=x%8
  if f==1 and u<4 then H.put(l[1],x,4,c[2])
  elseif f==2 then
   H.put(l[1],x,4,c[3]);if u==2 or u==3 then H.put(l[2],x,4,c[4]);H.put(l[3],x,2,c[2]);H.put(l[3],x,6,c[2]) end
  elseif f==3 and (u==3 or u==4) then H.put(l[4],x,4,c[2]) end
 end
 return scaleLayers(l)
end
local coreMs={60,40,50,60,60,30};local releaseMs={100,40,50,60,60,30}
local specs={
 {id='VFX_Sector_CoreMissileFlash',w=32,h=32,pivot={x=16,y=16},clips={{name='Green',label='CORE MISSILE FLASH / GREEN',palette='green',ms=coreMs,make=function(f)return core(f,'green')end},{name='Purple',label='CORE MISSILE FLASH / PURPLE',palette='purple',ms=coreMs,make=function(f)return core(f,'purple')end}},mode='one_shot',peak=3,notes='Six emission ports; same geometry/timing in both phases. Core flash clears before the outward ports settle. No missiles baked in.'},
 {id='VFX_Sector_EnemyMissileTrail',w=32,h=16,pivot={x=28,y=8},clips={{name='Loop',label='ENEMY MISSILE TRAIL',palette='hostile',ms={60,60,60,60},make=trail}},mode='loop',peak=2,notes='Exhaust extends toward -X behind a +X-facing missile. Attach pivot to tail. Approved missile body is not included or changed.'},
 {id='VFX_Sector_RectangleRelease',w=32,h=16,pivot={x=0,y=8},clips={{name='Sequence',label='RECTANGULAR RING RELEASE',palette='hostile',ms=releaseMs,make=ring}},splitRelease=true,tileAxis='X',mode='one_shot',peak=2,notes='Warning seed then synchronized 240ms Release. Tile X on straight sections; use the matching corner on each bend. Transparent band center preserves player position.'},
 {id='VFX_Sector_RectangleCorner',w=16,h=16,pivot={x=0,y=0},clips={{name='Sequence',label='RING CORNER / OPTIONAL',palette='hostile',ms=releaseMs,make=corner}},splitRelease=true,mode='one_shot',peak=2,notes='Top-left miter derived from the strip itself. Flip X/Y for other corners. Share the strip frame clock; do not layer corners over straight sections.'},
 {id='VFX_Sector_BarrierFormationFront',w=32,h=32,pivot={x=24,y=16},clips={{name='Travel',label='BARRIER FORMATION FRONT',palette='green',ms={80,80,80,80},make=front}},mode='loop',peak=2,notes='Travel-compatible +X leading edge. Move this sprite with the growing wall endpoint; stabilize approved wall behind it. Exact approved pixels retained in its short trailing wake.'},
 {id='VFX_Sector_BarrierActivationSpark',w=16,h=16,pivot={x=8,y=8},clips={{name='Activate',label='BARRIER ACTIVATION SPARK',palette='green',ms={50,50,60,40},make=spark}},mode='one_shot',peak=2,notes='Compact charge and activation spark at an existing corner/emitter. No device body or separate charge node is created.'},
 {id='VFX_Sector_BarrierStabilizationPulse',w=32,h=16,pivot={x=0,y=8},clips={{name='Connect',label='BARRIER STABILIZATION PULSE',palette='green',ms={40,60,80,40},make=pulse}},tileAxis='X',mode='one_shot',peak=2,notes='Thin 16px-period connection pulse. Tile along the completed edge, aligned to the existing barrier center stripe; finishes transparent.'}}
local manifest={generatorId='void-scrapper-sector-support-v1',name='Sector Administrator Support VFX',layers=names,palettes=palettes,pixelsPerUnit=32,constructionPixelScale=2,families={},preview='SectorSupportVFX_ContactSheet.png',nativePreview='SectorSupportVFX_480x270.png',ringPreview='SectorSupportVFX_Rings_480x270.png',barrierPreview='SectorSupportVFX_Barrier_480x270.png',animatedPreview='SectorSupportVFX_480x270.gif'}
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
 local rec={id=v.id,width=v.w,height=v.h,frames=total,aseprite=v.id..'.aseprite',sheet=v.id..'.png',metadata=v.id..'.json',clips=clips,tags=tags,pivotPixels=v.pivot,unityPivot=pivot,mode=v.mode,tileAxis=v.tileAxis,tilePeriodPixels=v.tileAxis and 16 or nil,peakFrame=v.peak,splitRelease=v.splitRelease or false,notes=v.notes};manifest.families[#manifest.families+1]=rec
 write(rec.metadata,{frames=jf,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=sheet.width,h=sheet.height},scale='1',frameTags=jsonTags},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot=pivot,loop=v.mode=='loop',tileAxis=v.tileAxis,clips=clips,releaseSheet=v.splitRelease and v.id..'_Release.png' or nil,warningSheet=v.splitRelease and v.id..'_Warning.png' or nil,warningSeedHold=v.splitRelease or false}})
end
local contact=canvas(960,1168);text(contact,'SECTOR ADMINISTRATOR / SUPPORT VFX',20,20,ink,3);text(contact,'GREEN CONTAINMENT / PURPLE OVERDRIVE / RED HOSTILE',20,48,muted,1)
for i,row in ipairs(reviewRows) do local y=76+(i-1)*134;H.rect(contact,12,y,936,122,panel);text(contact,row.clip.label,24,y+10,ink,2);text(contact,row.v.w..'X'..row.v.h..' / '..row.v.mode:upper():gsub('_',' '),24,y+32,muted,1)
 for f,im in ipairs(row.frames) do local x=352+(f-1)*96;text(contact,tostring(f),x+26,y+10,muted,1);contact:drawImage(H.scale(im,2),Point(x,y+40)) end
end
contact:saveAs(out..'/'..manifest.preview)
-- Pure rendering helpers for native reviews; never exported as gameplay sprite geometry.
local bosses={Image{fromFile=src..'/boss_green.png'},Image{fromFile=src..'/boss_purple.png'}};local player=Image{fromFile=src..'/player.png'};local missile=Image{fromFile=src..'/missile_body.png'}
local function sample(ms,durations,loop) local total=0;for _,d in ipairs(durations) do total=total+d end;if loop then ms=ms%total end;for i,d in ipairs(durations) do if ms<d then return i end;ms=ms-d end;return #durations end
local function drawRotated(dest,im,cx,cy,angle,px,py)
 local a=angle*math.pi/180;local co,si=math.cos(a),math.sin(a);local extent=math.ceil(math.sqrt(im.width^2+im.height^2))
 for y=math.max(0,math.floor(cy-extent)),math.min(dest.height-1,math.ceil(cy+extent)) do for x=math.max(0,math.floor(cx-extent)),math.min(dest.width-1,math.ceil(cx+extent)) do
  local dx,dy=x+0.5-cx,y+0.5-cy;local u=math.floor(dx*co+dy*si+px);local v=math.floor(-dx*si+dy*co+py)
  if u>=0 and u<im.width and v>=0 and v<im.height then local c=im:getPixel(u,v);if pc.rgbaA(c)>0 then dest:drawPixel(x,y,c) end end
 end end
end
local smallMissile=Image(20,10,ColorMode.RGB)
for y=0,9 do for x=0,19 do smallMissile:drawPixel(x,y,missile:getPixel(math.floor(y*missile.width/10),missile.height-1-math.floor(x*missile.height/20))) end end
local function reviewCore(ms)
 local im=canvas(480,270);text(im,'CORE EJECTION / SAME GEOMETRY BOTH PHASES',16,14,ink,2)
 for phase=1,2 do local cx=phase==1 and 120 or 360;local missiles={}
  if ms>=100 and ms<1050 then local r=math.min(92,18+math.floor((ms-100)*0.1))
   for n=0,5 do local a=n*60-30;local co,si=math.cos(a*math.pi/180),math.sin(a*math.pi/180);local x,y=cx+co*r,138+si*r
    local trailFrame=cache['VFX_Sector_EnemyMissileTrail/Loop'][math.floor(ms/60)%4+1]
    drawRotated(im,trailFrame,x-co*8,y-si*8,a,28,8);missiles[#missiles+1]={x=x,y=y,a=a}
   end
  end
  im:drawImage(bosses[phase],Point(cx-64,74));local frames=cache['VFX_Sector_CoreMissileFlash/'..(phase==1 and 'Green' or 'Purple')]
  im:drawImage(frames[sample(ms,coreMs,false)],Point(cx-16,122))
  for _,m in ipairs(missiles) do drawRotated(im,smallMissile,m.x,m.y,m.a,10,5) end
 end
 text(im,'GREEN / WHITE',60,224,P.green[3],1);text(im,'PURPLE / WHITE',300,224,P.purple[3],1);im:drawImage(player,Point(232,214));return im
end
local function ringComposite(w,h,f)
 local im=Image(w,h,ColorMode.RGB);local strip=cache['VFX_Sector_RectangleRelease/Sequence'][f];local cornerIm=cache['VFX_Sector_RectangleCorner/Sequence'][f]
 for y=0,h-1 do for x=0,w-1 do local c=0
  if (x<16 or x>=w-16) and (y<16 or y>=h-16) then c=cornerIm:getPixel(x<16 and x or w-1-x,y<16 and y or h-1-y)
  elseif y<16 then c=strip:getPixel((x-16)%32,y)
  elseif y>=h-16 then c=strip:getPixel((x-16)%32,h-1-y)
  elseif x<16 then c=strip:getPixel((y-16)%32,x)
  elseif x>=w-16 then c=strip:getPixel((y-16)%32,w-1-x) end
  im:drawPixel(x,y,c)
 end end;return im
end
-- Demonstration rings use the same reusable sources at unrelated dimensions.
for _,d in ipairs({{160,80},{224,128}}) do local r=ringComposite(d[1],d[2],2);r:saveAs(out..'/RingAssembly_'..d[1]..'x'..d[2]..'_Review.png') end
local function reviewRing(ms)
 local im=canvas(480,270);text(im,'RECTANGULAR RELEASE / SHARED STRIP CLOCK',16,14,ink,2)
 local f=ms<480 and 1 or sample(ms-480,{40,50,60,60,30},false)+1
 im:drawImage(bosses[1],Point(176,74));im:drawImage(ringComposite(400,192,f),Point(40,46));im:drawImage(ringComposite(272,128,f),Point(104,78));im:drawImage(player,Point(349,157))
 text(im,ms<480 and 'WARNING / NO WHITE RELEASE' or 'ALL FOUR SIDES RELEASE TOGETHER',24,240,muted,1);return im
end
local function reviewBarrier(ms)
 local im=canvas(480,270);text(im,'CONTAINMENT / FORMATION TO STABILITY',16,14,ink,2);im:drawImage(bosses[1],Point(176,84));im:drawImage(player,Point(120,200))
 local length=math.min(408,math.floor(math.max(0,ms-120)*408/720));local wall=barrierFrames[math.floor(ms/100)%6+1]
 for y=0,31 do for x=0,length-1 do local c=wall:getPixel(x%64,y+16);if pc.rgbaA(c)>0 then im:drawPixel(36+x,50+y,c) end end end
 if ms<840 then local f=math.floor(ms/80)%4+1;im:drawImage(cache['VFX_Sector_BarrierFormationFront/Travel'][f],Point(36+length-24,50)) end
 if ms<200 then im:drawImage(cache['VFX_Sector_BarrierActivationSpark/Activate'][sample(ms,{50,50,60,40},false)],Point(28,58)) end
 if ms>=840 then local pulseFrame=cache['VFX_Sector_BarrierStabilizationPulse/Connect'][sample(ms-840,{40,60,80,40},false)];for y=0,15 do for x=0,407 do local c=pulseFrame:getPixel(x%32,y);if pc.rgbaA(c)>0 then im:drawPixel(36+x,58+y,c) end end end end
 text(im,'APPROVED GREEN BARRIER / MOVING FRONT',44,240,muted,1);return im
end
local function decorate(im) text(im,'NATIVE 480X270 ART REVIEW / NOT A GAME CAPTURE',16,258,muted,1);return im end
decorate(reviewCore(180)):saveAs(out..'/'..manifest.nativePreview);decorate(reviewRing(500)):saveAs(out..'/'..manifest.ringPreview);decorate(reviewBarrier(720)):saveAs(out..'/'..manifest.barrierPreview)
decorate(reviewCore(80)):saveAs(out..'/SectorSupportVFX_CoreCharge_480x270.png');decorate(reviewCore(940)):saveAs(out..'/SectorSupportVFX_Missiles_480x270.png');decorate(reviewBarrier(900)):saveAs(out..'/SectorSupportVFX_BarrierConnected_480x270.png')
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native encounter examples'
for f=1,90 do if f>1 then review:newEmptyFrame(f) end;local scene=math.floor((f-1)/30);local ms=((f-1)%30)*40;local im=scene==0 and reviewCore(ms) or scene==1 and reviewRing(ms) or reviewBarrier(ms);review:newCel(review.layers[1],f,decorate(im),Point(0,0));review.frames[f].duration=0.04 end
review:saveAs(out..'/'..manifest.animatedPreview);review:close();barrier:close()
manifest.totalFrames=40;manifest.totalTags=10;manifest.nativeReview={width=480,height=270,frames=90,frameDurationMs=40,mode='art composition only',missileReference='Original PlayerBullet.png sampled for a 20x10 horizontal preview; no missile-body output generated.'}
manifest.ringAssembly={cornerPixels=16,straightAxis='X',horizontal='Tile between 16px corner areas',vertical='Rotate 90 degrees; tile between corners',clock='Same Release frame on every side and corner',warningFrame=1,releaseFrames={2,6},releaseDurationMs=240,reviewSizes={{160,80},{224,128}}}
manifest.integration='Art exports only. Existing projectile bodies, approved boss art, static barrier, Unity prefabs/code/timing and pooling remain unchanged.'
write('manifest.json',manifest);local f=assert(io.open(out..'/generation_complete.txt','w'));f:write('Sector support VFX generated by Aseprite CLI + Lua.\n');f:close()
