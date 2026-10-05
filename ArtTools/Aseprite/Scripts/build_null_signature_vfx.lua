-- Original NULL network effects. Existing SYSTEM effects are visual references only.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,scale=h.put,h.rect,h.line,h.poly,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local palette={'35204F','603785','9650C1','CF77EB','F0B3FB','FCE9FF','151223'}
local p={};for i,v in ipairs(palette) do p[i]=rgba(v) end
local muted=rgba('A7B7BF');local bg=rgba('111820')
local extraFont={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,str,x,y,c,n)
 for i=1,#str do local ch=str:sub(i,i);local xx=x+(i-1)*4*n
  if extraFont[ch] then for yy,row in ipairs(extraFont[ch]) do for gx=1,3 do if row:sub(gx,gx)=='1' then rect(im,xx+(gx-1)*n,y+(yy-1)*n,n,n,c) end end end else h.text(im,ch,xx,y,c,n) end
 end
end
local function layers(w,hg) local r={};for i=1,4 do r[i]=Image(w//2,hg//2,ColorMode.RGB) end;return r end
local function path(im,points,c) for i=1,#points-1 do line(im,points[i][1],points[i][2],points[i+1][1],points[i+1][2],c) end end
local function corner(im,x,y,dx,dy,n,c) line(im,x,y,x+dx*n,y,c);line(im,x,y,x,y+dy*n,c) end
local layerNames={'Corrupted Geometry','Energy / White Core','Displaced Packets','Void / Residue'}
-- Extract only the approved Purple Core's Energy layer. No Housing, Details or boss pixels.
local core={}
for _,name in ipairs({'active','overloaded'}) do
 local s=assert(app.open(sourceDir..'/core_purple_corrupted_'..name..'.aseprite'));assert(s.width==64 and s.height==64)
 local energy;for _,l in ipairs(s.layers) do if l.name=='Energy' then energy=l end end;assert(energy,'Missing approved Energy layer')
 core[name]={}
 for fi=1,#s.frames do local c=assert(energy:cel(fi));local im=Image(64,64,ColorMode.RGB);im:drawImage(c.image,c.position);core[name][fi]=im end
 s:close()
end
local function telegraph(f)
 local l=layers(32,64);local close=({2,2,3,3,4,4})[f]
 for y=0,31 do
  local q=y%8;local lit=({1,2,3,4,5,6})[f]
  if q<lit then rect(l[1],7,y,2,1,p[f>=5 and 4 or 3]) end
  if q==0 then line(l[3],close,y,5,y,p[3]);line(l[3],10,y,13-close+2,y,p[3]) end
  if q==1 or q==2 then put(l[3],close,y,p[2]);put(l[3],15-close,y,p[3]) end
  if q==5 then line(l[1],3+(f%2),y,5+(f%2),y,p[2]) end
  if q==6 then line(l[1],10,y,12,y,p[3]) end
 end
 -- Small offset address bits change in a fixed cycle; the strike axis stays at X=16.
 for y=3,27,8 do put(l[3],f%2==0 and 11 or 4,y,p[4]) end
 return l
end
local function rainBeam(f)
 local l=layers(32,64);if f==6 then return l end
 for y=0,31 do local q=y%8;local phase=(q+f-1)%8
  if f<=4 then
   local w=phase<3 and ({4,3,3,2})[f] or 2
   local offset=phase==2 and -1 or phase==5 and 1 or 0
   rect(l[1],7-w//2+offset,y,w+2,1,p[3]);rect(l[1],6,y,4,1,p[4]);rect(l[2],7,y,2,1,p[6])
   if phase==1 or phase==2 then rect(l[3],3,y,2,1,p[4]) end
   if phase==5 then rect(l[3],11,y,2,1,p[5]) end
   if phase==6 then put(l[4],10,y,p[7]) end
  else
   if phase==0 then rect(l[1],7,y,2,1,p[3]) end
   if phase==3 then rect(l[3],4,y,2,1,p[4]) end
   if phase==6 then rect(l[3],11,y,2,1,p[2]) end
  end
 end
 return l
end
local compressionRadii={13,11,9,6,3}
local function compression(f)
 local l=layers(64,64);if f==6 then return l end
 local r=compressionRadii[f];local lo,hi=16-r,15+r;local arm=math.max(2,math.floor(r/2))
 -- Four misregistered pressure jaws close inward; no expanding explosion ring.
 corner(l[1],lo,lo+1,1,1,arm,p[3]);corner(l[1],hi,lo,-1,1,arm,p[4])
 corner(l[1],lo+1,hi,1,-1,arm,p[4]);corner(l[1],hi-1,hi-1,-1,-1,arm,p[2])
 if f<=4 then
  local k=math.max(2,r-3)
  path(l[3],{{16-k,14},{18-k,16},{16-k,18}},p[4]);path(l[3],{{15+k,14},{13+k,16},{15+k,18}},p[3])
  line(l[3],14,16-k,16,18-k,p[3]);line(l[3],16,18-k,18,16-k,p[4])
  line(l[3],14,15+k,16,13+k,p[4]);line(l[3],16,13+k,18,15+k,p[3])
 end
 if f>=3 then rect(l[2],15,15,2,2,p[f==5 and 6 or 5]);put(l[2],lo,lo+1,p[5]);put(l[2],hi,hi-1,p[5])
 else corner(l[1],14,14,1,1,2,p[2]) end
 if f==5 then rect(l[4],15,16,2,1,p[7]);rect(l[3],11,14,2,1,p[4]);rect(l[3],19,17,2,1,p[4]) end
 return l
end
local function redirect(f)
 local l=layers(64,64);if f==5 then return l end
 if f<=3 then
  local dx=({0,0,4})[f];local dy=({0,0,-4})[f]
  path(l[1],{{9+dx,11+dy},{15+dx,11+dy},{17+dx,13+dy}},p[3]);path(l[1],{{8+dx,13+dy},{8+dx,19+dy},{11+dx,22+dy}},p[4])
  path(l[1],{{15+dx,23+dy},{21+dx,23+dy},{23+dx,20+dy}},p[3]);path(l[1],{{24+dx,17+dy},{24+dx,12+dy},{21+dx,9+dy}},p[2])
  if f==1 then
   rect(l[2],4,16,8,1,p[5]);line(l[2],18,14,23,9,p[4]);rect(l[3],12,24,5,1,p[2])
  elseif f==2 then
   -- An incoming horizontal strike is forcibly kinked toward the upper right.
   poly(l[1],{{3,14},{15,14},{23,6},{27,6},{19,17},{15,20},{3,18}},p[4])
   rect(l[2],4,16,12,2,p[6]);line(l[2],16,16,25,7,p[6]);line(l[2],17,16,26,7,p[5])
   rect(l[4],11,14,2,2,p[7]);rect(l[3],5,21,7,1,p[3]);rect(l[3],20,25,4,1,p[4])
  else
   line(l[2],19,12,26,5,p[5]);rect(l[3],4,17,7,1,p[3]);rect(l[3],9,20,5,1,p[2]);rect(l[3],17,24,4,1,p[4])
  end
 else
  rect(l[3],5,18,5,1,p[2]);rect(l[3],17,22,4,1,p[3]);line(l[3],24,7,27,4,p[4]);rect(l[3],25,15,3,1,p[2])
 end
 return l
end
local function intervention(f)
 local l=layers(96,32);local shift=(f-1)*2
 for x=0,47 do local q=x%16
  -- Split register rails with misaligned gates, repeated every 32 native pixels.
  if q<=5 then put(l[1],x,3,p[3]) elseif q>=10 then put(l[1],x,4,p[2]) end
  if q>=4 and q<=11 then put(l[1],x,12,p[3]) end
  if q<=2 or q>=12 then put(l[1],x,11,p[2]) end
  if q<7 then put(l[1],x,7,p[3]) elseif q>9 then put(l[1],x,8,p[4]) end
  if q==4 or q==12 then line(l[1],x,4,x,6,p[4]);line(l[1],(x+2)%48,10,(x+2)%48,12,p[3]) end
 end
 local function wrap(im,x,y,c) put(im,x%48,y,c) end
 for base=12,44,16 do
  local x=base-shift
  -- White payload blocks and their hooked purple grips travel toward local X=0.
  for yy=6,9 do for xx=0,2 do wrap(l[2],x+xx,yy,(yy==6 or xx==0) and p[5] or p[6]) end end
  for xx=-2,3 do wrap(l[3],x+xx,5,p[4]);wrap(l[3],x+xx+1,10,p[3]) end
  wrap(l[3],x-2,6,p[5]);wrap(l[3],x-2,7,p[4]);wrap(l[3],x+4,9,p[4])
  wrap(l[4],x+1,8,p[7])
 end
 return l
end
local function coreBurst(f)
 local half=layers(64,64);local l={};for i=1,4 do l[i]=scale(half[i],2) end;if f==6 then return l end
 if f==1 then
  for y=0,31 do for x=0,31 do put(l[2],x+16,y+16,core.active[1]:getPixel(x*2,y*2)) end end
 elseif f==2 then l[2]=Image(core.overloaded[1])
 elseif f==3 then
  for it in core.overloaded[2]:pixels() do if pc.rgbaA(it())>0 then
   local band=(it.y//6)%3;local dx=band==0 and -4 or band==1 and 4 or 0
   if it.y%6<4 then put(l[2],it.x+dx,it.y,it()) end
  end end
 end
 if f>=2 then
  local d=({0,12,17,22,25})[f];local arm=f<=3 and 5 or f==4 and 4 or 2
  local coords={{31-d,31-d+2,1,1},{32+d,31-d,-1,1},{31-d+2,32+d,1,-1},{32+d-2,32+d-1,-1,-1}}
  for i,q in ipairs(coords) do corner(l[3],q[1],q[2],q[3],q[4],arm,p[f==5 and 3 or 4]);if f<=4 then put(l[3],q[1]+q[3]*2,q[2],p[5]) end end
  if f==3 or f==4 then rect(l[3],8,31+f,6,2,p[3]);rect(l[3],48,25-f,5,2,p[4]) end
 end
 return l
end
local function critical(f)
 local l=layers(64,64);local phase=(f-1)%4;local side=f<=4
 -- A sparse damaged network, not a full shield or another exposed-core sprite.
 path(l[1],{{6,8},{11,8},{13,10}},p[2]);path(l[1],{{18,6},{23,6},{25,9}},p[3])
 path(l[1],{{25,18},{25,23},{22,25}},p[2]);path(l[1],{{14,25},{9,25},{6,22}},p[3])
 corner(l[1],5,13,1,1,2,p[3]);corner(l[1],27,13,-1,1,2,p[2])
 if phase==0 or phase==3 then
  rect(l[3],10,12,3,1,p[3]);rect(l[3],20,20,2,1,p[2])
 else
  local dx=phase==1 and 1 or 3
  rect(l[3],8+dx,11,3,1,p[4]);rect(l[3],20-dx,21,3,1,p[3]);rect(l[3],24,10+dx,2,1,p[4])
 end
 if phase==1 then
  local x=side and 14 or 18;local y=side and 16 or 13
  rect(l[2],x,y,2,2,p[6]);line(l[2],x-2,y+1,x+3,y+1,p[5]);rect(l[4],x,y,1,1,p[7])
 elseif phase==2 then
  rect(l[3],side and 11 or 21,side and 18 or 11,3,1,p[4])
 end
 -- Tiny opposing flicker changes make the quiet seam a deliberate transition.
 put(l[1],side and 7 or 24,side and 8 or 6,p[4])
 return l
end
local families={
 {id='VFX_NULL_LaserRain_Telegraph',label='NULL LASER RAIN TELEGRAPH',short='RAIN WARNING',w=32,h=64,make=telegraph,scale=2,ms={160,140,120,120,100,80},tag='Warning',mode='handoff',peak=6,tileAxis='Y',tilePeriod=16,pivot={x=16,y=32},notes='720ms warning. No white pixels. Hide and start the rain beam at the same X=16 axis. Repeat vertically in 16px periods; repeat lanes by gameplay spacing.'},
 {id='VFX_NULL_LaserRain_Beam',label='NULL LASER RAIN BEAM',short='RAIN BEAM',w=32,h=64,make=rainBeam,scale=2,ms={40,60,60,80,60,40},tag='Fire',mode='one_shot',peak=1,tileAxis='Y',tilePeriod=16,pivot={x=16,y=32},tags={{name='Fire',from=1,to=4},{name='Release',from=5,to=6}},notes='240ms Fire plus 100ms Release. Continuous white strike axis during Fire; broken displaced edges. Release is non-damaging, ending transparent.'},
 {id='VFX_NULL_CompressionDispatch',label='COMPRESSION DISPATCH',short='COMPRESSION',w=64,h=64,make=compression,scale=2,ms={100,100,100,80,60,40},tag='Compress',mode='one_shot',peak=2,pivot={x=32,y=32},notes='Pressure jaws and packets contract inward. White appears only in the final compression beats; transparent final frame.'},
 {id='VFX_NULL_PhaseRedirectCorruption',label='PHASE REDIRECT CORRUPTION',short='REDIRECT',w=64,h=64,make=redirect,scale=2,ms={40,60,60,80,40},tag='Redirect',mode='one_shot',peak=2,pivot={x=32,y=32},notes='Incoming +X route kinks toward upper-right, with misregistered aperture halves and a displaced afterimage. Rotate the whole effect to match gameplay.'},
 {id='VFX_NULL_InterventionBeam',label='SUPPORT / INTERVENTION BEAM',short='FORCED LINK',w=96,h=32,make=intervention,scale=2,ms={80,80,80,80,80,80,80,80},tag='Pull',mode='loop',peak=1,tileAxis='X',tilePeriod=32,flow=-4,pivot={x=0,y=16},notes='Payload blocks are forcibly pulled left toward an emitter at local X=0. Tile the 32x32 segments to extend length; keep height fixed. Stop the loop when the link ends.'},
 {id='VFX_NULL_CoreGlitchBurst',label='CORE GLITCH BURST',short='CORE BURST',w=64,h=64,make=coreBurst,scale=1,ms={60,60,60,80,100,40},tag='Burst',mode='one_shot',peak=2,pivot={x=32,y=32},notes='Only the copied Purple Core Energy layer supplies the pulse. Frame 2 preserves overloaded Energy frame 1 exactly; no Housing or boss artwork. Displaced bands dissolve into broken square fragments.'},
 {id='VFX_NULL_CriticalLoop',label='NULL CRITICAL LOOP',short='CRITICAL LOOP',w=64,h=64,make=critical,scale=2,ms={100,100,100,100,100,100,100,100},tag='Loop',mode='loop',peak=2,pivot={x=32,y=32},notes='Sparse unstable links with white flashes only on frames 2 and 6. Intended as a light overlay on damaged network hardware; stop when the state ends.'}
}
local function saveJson(name,v) local f=assert(io.open(outputDir..'/'..name,'w'));f:write(json.encode(v));f:close() end
local manifest={generatorId='void-scrapper-null-signature-vfx-v1',name='NULL DISPATCHER SIGNATURE VFX PACK',target={width=480,height=270,pixelsPerUnit=32,binaryAlpha=true},palette=palette,layers=layerNames,totalFamilies=7,totalFrames=45,totalTags=8,families={},preview='NullSignatureVFX_ContactSheet.png',nativePreview='NullSignatureVFX_480x270.png',animatedPreview='NullSignatureVFX_480x270.gif',sourcePolicy='Original network geometry. Existing SYSTEM VFX are reference-only, never recolored or composited. Only copied approved Purple Core Energy pixels are used in Core Glitch Burst. No boss sprites baked into VFX.'}
local rendered={}
for _,v in ipairs(families) do
 local s=Sprite(v.w,v.h,ColorMode.RGB);for li,name in ipairs(layerNames) do local l=li==1 and s.layers[1] or s:newLayer();l.name=name end
 local sheet=Image(v.w*#v.ms,v.h,ColorMode.RGB);local frames={};local metadata={};local used={}
 for fi,ms in ipairs(v.ms) do
  if fi>1 then s:newEmptyFrame(fi) end
  local ls=v.make(fi);local im=Image(v.w,v.h,ColorMode.RGB)
  for li,raw in ipairs(ls) do local pixels=scale(raw,v.scale);s:newCel(s.layers[li],fi,pixels,Point(0,0));im:drawImage(pixels,Point(0,0)) end
  s.frames[fi].duration=ms/1000;frames[fi]=im;sheet:drawImage(im,Point((fi-1)*v.w,0))
  for it in im:pixels() do if pc.rgbaA(it())>0 then used[it()]=true end end
  metadata[#metadata+1]={filename=v.id..'_'..string.format('%02d',fi-1),frame={x=(fi-1)*v.w,y=0,w=v.w,h=v.h},sourceSize={w=v.w,h=v.h},spriteSourceSize={x=0,y=0,w=v.w,h=v.h},rotated=false,trimmed=false,duration=ms}
 end
 local tags=v.tags or {{name=v.tag,from=1,to=#v.ms}};local jsonTags={}
 for _,t in ipairs(tags) do local tag=s:newTag(t.from,t.to);tag.name=t.name;jsonTags[#jsonTags+1]={name=t.name,from=t.from-1,to=t.to-1,direction='forward'} end
 local pal=Palette(#palette+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,col in ipairs(p) do pal:setColor(i,Color{r=pc.rgbaR(col),g=pc.rgbaG(col),b=pc.rgbaB(col),a=255}) end;s:setPalette(pal)
 s:saveAs(outputDir..'/'..v.id..'.aseprite');s:close();sheet:saveAs(outputDir..'/'..v.id..'.png')
 local rec={id=v.id,label=v.label,width=v.w,height=v.h,frames=#v.ms,durationsMs=v.ms,tags=tags,mode=v.mode,aseprite=v.id..'.aseprite',sheet=v.id..'.png',metadata=v.id..'.json',palette=palette,layers=layerNames,constructionPixelScale=v.scale,pixelsPerUnit=32,pivotPixels=v.pivot,unityPivot={x=v.pivot.x/v.w,y=1-v.pivot.y/v.h},tileAxis=v.tileAxis,tilePeriodPixels=v.tilePeriod,flowPixelsPerFrame=v.flow,notes=v.notes}
 if v.tags then
  rec.tagSheets={};for _,t in ipairs(tags) do local im=Image((t.to-t.from+1)*v.w,v.h,ColorMode.RGB);for fi=t.from,t.to do im:drawImage(frames[fi],Point((fi-t.from)*v.w,0)) end;local name=v.id..'_'..t.name..'.png';im:saveAs(outputDir..'/'..name);rec.tagSheets[#rec.tagSheets+1]={tag=t.name,sheet=name} end
 end
 if v.tileAxis=='X' then
  local tile=Image(32*#v.ms,32,ColorMode.RGB)
  for fi,im in ipairs(frames) do for y=0,31 do for x=0,31 do put(tile,(fi-1)*32+x,y,im:getPixel(x,y)) end end end
  rec.tileSheet=v.id..'_Tile32.png';rec.tileSize={w=32,h=32};tile:saveAs(outputDir..'/'..rec.tileSheet)
 end
 saveJson(rec.metadata,{frames=metadata,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=sheet.width,h=sheet.height},frameTags=jsonTags},unity={pixelsPerUnit=32,pivot=rec.unityPivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',loop=v.mode=='loop',tileAxis=v.tileAxis,tilePeriodPixels=v.tilePeriod}})
 manifest.families[#manifest.families+1]=rec;rendered[#rendered+1]={family=v,frames=frames}
end
local contact=Image(1260,1320,ColorMode.RGB);rect(contact,0,0,1260,1320,bg)
text(contact,'NULL DISPATCHER / SIGNATURE VFX',20,16,p[6],3);text(contact,'7 FAMILIES / 8 TAGS / 45 FRAMES',20,44,muted,2)
for row,r in ipairs(rendered) do
 local v=r.family;local y=82+(row-1)*174
 text(contact,v.label..' / '..#v.ms..'F / '..v.mode:upper():gsub('_',' '),20,y,p[5],1)
 for fi,im in ipairs(r.frames) do
  local x=20+(fi-1)*150;local yy=y+18
  for by=0,127 do for bx=0,135 do put(contact,x+bx,yy+by,(bx//8+by//8)%2==0 and rgba('202B36') or rgba('263340')) end end
  local shown=scale(im,v.w==96 and 1 or 2);contact:drawImage(shown,Point(x+(136-shown.width)//2,yy+(128-shown.height)//2))
  text(contact,'F'..fi..' / '..v.ms[fi]..'MS',x+2,yy+136,muted,1)
 end
end
contact:saveAs(outputDir..'/'..manifest.preview)
local function at(r,t)
 if t<0 then return nil end
 local duration=0;for _,d in ipairs(r.family.ms) do duration=duration+d end
 if r.family.mode=='loop' then t=t%duration end
 local elapsed=0;for i,d in ipairs(r.family.ms) do elapsed=elapsed+d;if t<elapsed then return r.frames[i] end end
end
local function board(t,peak)
 local im=Image(480,270,ColorMode.RGB);rect(im,0,0,480,270,bg);text(im,'NULL SIGNATURE VFX / 480 X 270 / PPU 32',8,7,muted,1)
 for i,r in ipairs(rendered) do
  local x=(i-1)%4*120;local y=24+((i-1)//4)*120;rect(im,x+1,y,118,116,rgba(i%2==0 and '1C2732' or '17212C'))
  local tm=t%1600
  if i==2 then tm=tm-720 elseif i==3 then tm=tm-800 elseif i==4 then tm=tm-1100 elseif i==6 then tm=tm-400 end
  if i==5 or i==7 then tm=t end
  local shown=peak and r.frames[r.family.peak] or at(r,tm)
  if shown then
   if i<=2 then for lane=0,2 do im:drawImage(shown,Point(x+8+lane*36,y+14)) end
   else im:drawImage(shown,Point(x+(120-shown.width)//2,y+14+(64-shown.height)//2)) end
  end
  text(im,r.family.short,x+6,y+101,p[5],1)
  if i==1 then text(im,'WARNING ONLY',x+6,y+88,muted,1) elseif i==2 then text(im,'FIRE THEN CLEAR',x+6,y+88,muted,1) elseif i==5 then text(im,'PULLS LEFT',x+6,y+88,muted,1) end
 end
 rect(im,361,144,118,116,rgba('17212C'));text(im,'NULL NETWORK',370,160,p[5],1);text(im,'WARNING / FIRE',370,179,muted,1);text(im,'DIFFERENT VALUES',370,191,muted,1);text(im,'NATIVE PIXELS',370,221,muted,1)
 return im
end
board(0,true):saveAs(outputDir..'/'..manifest.nativePreview)
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native Review'
for fi=1,160 do if fi>1 then review:newEmptyFrame(fi) end;review:newCel(review.layers[1],fi,board((fi-1)*20,false),Point(0,0));review.frames[fi].duration=0.02 end
review:saveAs(outputDir..'/'..manifest.animatedPreview);review:close()
manifest.previewDurationMs=3200;manifest.previewFrameMs=20;manifest.previewFrames=160
saveJson('manifest.json',manifest)
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('NULL signature VFX / 7 families / 8 tags / 45 frames\n');done:close()
