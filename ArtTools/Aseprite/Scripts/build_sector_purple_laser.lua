-- Phase 2 SYSTEM derivative of the approved Region A laser; integer pixels only.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(s) return pc.rgba(tonumber(s:sub(1,2),16),tonumber(s:sub(3,4),16),tonumber(s:sub(5,6),16),255) end
local hex={'452D66','8353B8','BA79FF','D9A5FF','FFF6FF'}
local p={};for i,h in ipairs(hex) do p[i]=rgba(h) end
local function read(path) local f=assert(io.open(path,'r'));local t=json.decode(f:read('*a'));f:close();return t end
local function write(name,t) local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(t));f:close() end
local overdrive=read(src..'/overdrive_manifest.json');local approvedPalette={};for _,h in ipairs(overdrive.energyRamps[5]) do approvedPalette[h]=true end
for _,h in ipairs(hex) do assert(approvedPalette[h],'Color must come from approved Purple Overdrive') end
local extraFont={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,label,x,y,c,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if extraFont[ch] then for yy,row in ipairs(extraFont[ch]) do for xx=1,3 do if row:sub(xx,xx)=='1' then H.rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,c) end end end else H.text(im,ch,px,y,c,n) end
 end
end
local names={'Energy Shape','Bright Center','Technical Marks','Fragments'}
local function layers(w,h) local r={};for i=1,4 do r[i]=Image(w,h,ColorMode.RGB) end;return r end
local function layer(s,i,f) local im=Image(s.width,s.height,ColorMode.RGB);local c=s.layers[i]:cel(f);if c then im:drawImage(c.image,c.position) end;return im end
local function render(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function verifyReference(aseName,pngName,jsonName,productionName)
 local s=assert(app.open(src..'/'..aseName));local png=Image{fromFile=src..'/'..pngName};local production=Image{fromFile=src..'/'..productionName};local data=read(src..'/'..jsonName)
 assert(s.width==64 and s.height==32 and #s.layers==4)
 for i,n in ipairs(names) do assert(s.layers[i].name==n,'Original layer structure changed') end
 assert(png.width==production.width and png.height==production.height)
 for it in png:pixels() do assert(it()==production:getPixel(it.x,it.y),'Production texture differs from approved source') end
 for f=1,#s.frames do local im=render(s,f);local r=data.frames[f].frame;for it in im:pixels() do assert(it()==png:getPixel(r.x+it.x,r.y+it.y),'Approved ASE/PNG mismatch') end end
 return s
end
local greenBeam=verifyReference('green_beam.aseprite','green_beam.png','green_beam.json','production_beam.png')
local greenWarning=verifyReference('green_warning.aseprite','green_warning.png','green_warning.json','production_warning.png')
local green={dark=rgba('254F39'),mid=rgba('4FA96C'),pale=rgba('B3EDAA'),white=rgba('F0F2E9')}
local function recolor(im,map)
 local result=Image(im);for it in result:pixels() do if pc.rgbaA(it())>0 then it(assert(map[it()],'Unexpected approved color')) end end;return result
end
local function makeBeam(f)
 local l=layers(64,32)
 -- Reuse all four approved sustain silhouettes, including their ordered markers.
 l[1]=recolor(layer(greenBeam,1,f+3),{[green.mid]=p[2],[green.pale]=p[3]})
 l[2]=recolor(layer(greenBeam,2,f+3),{[green.white]=p[5]})
 -- Steady six-pixel white core: no brightness drop in the narrow sustain frame.
 H.rect(l[2],0,14,64,6,p[5])
 l[3]=recolor(layer(greenBeam,3,f+3),{[green.mid]=p[4]})
 l[4]=layer(greenBeam,4,f+3)
 return l
end
local function makeWarning(f)
 local l=layers(64,32)
 -- Approved frame 2 dotted axis; shift by an integer 2px to make a four-step loop.
 local dotted=recolor(layer(greenWarning,1,2),{[green.mid]=p[3]})
 for it in dotted:pixels() do if pc.rgbaA(it())>0 then l[1]:drawPixel((it.x+(f-1)*2)%64,it.y,it()) end end
 -- Exact approved frame 3 endpoint brackets and technical chevrons.
 l[3]=recolor(layer(greenWarning,3,3),{[green.dark]=p[2],[green.mid]=p[3]})
 return l
end
local function makeEmitter(f)
 local l=layers(16,16)
 if f==1 then
  H.rect(l[1],3,6,5,5,p[2]);H.rect(l[1],4,7,5,3,p[3]);H.rect(l[2],4,8,4,1,p[5])
  H.line(l[3],2,6,2,10,p[2]);H.rect(l[3],7,5,2,1,p[4]);H.rect(l[3],7,11,2,1,p[4])
 elseif f==2 then
  H.poly(l[1],{{2,6},{5,6},{7,4},{9,4},{9,6},{13,7},{13,9},{9,10},{9,12},{7,12},{5,10},{2,10}},p[2])
  H.rect(l[1],3,6,6,5,p[3]);H.rect(l[1],5,7,7,3,p[4]);H.rect(l[2],3,7,6,3,p[5]);H.rect(l[2],9,8,3,1,p[5])
  H.line(l[3],2,4,4,4,p[4]);H.line(l[3],2,4,2,5,p[4]);H.line(l[3],2,11,2,12,p[4]);H.line(l[3],2,12,4,12,p[4])
  H.rect(l[3],11,5,2,1,p[3]);H.rect(l[3],11,11,2,1,p[3])
 elseif f==3 then
  H.rect(l[1],4,7,5,3,p[2]);H.rect(l[1],5,8,5,1,p[3]);H.rect(l[2],6,8,2,1,p[5])
  H.rect(l[3],10,6,2,1,p[3]);H.rect(l[3],10,10,2,1,p[3])
 end
 local r={};for i=1,4 do r[i]=H.scale(l[i],2) end;return r
end
local specs={
 {id='VFX_Sector_PurpleLaserWarning',label='PURPLE WARNING',w=64,h=32,tag='Warning',mode='loop',ms={120,120,120,120},pivot={x=0,y=17},make=makeWarning,peak=2,tileAxis='X',notes='Dotted 2px warning with original Region A brackets and chevrons. No white center. Loop only while the gameplay warning is active.'},
 {id='VFX_Sector_PurpleLaserBeam',label='PURPLE ACTIVE',w=64,h=32,tag='Sustain',mode='loop',ms={60,60,60,60},pivot={x=0,y=17},make=makeBeam,peak=2,tileAxis='X',notes='Exact four approved green sustain masks, overdriven violet body and uninterrupted 6px white-hot core. Tile X; preserve transverse size. Core-only slices support clean X stretching.'},
 {id='VFX_Sector_PurpleLaserEmitter',label='EMITTER BURST',w=32,h=32,tag='Activate',mode='one_shot',ms={40,60,70,40},pivot={x=8,y=17},make=makeEmitter,peak=2,notes='Compact technical flare aligned to the same y=17 beam axis; local +X. Three visible frames and transparent cleanup. Trigger once on damage activation.'}}
local cache={};local manifest={generatorId='void-scrapper-sector-purple-laser-v1',name='Sector Administrator Phase 2 Purple Laser',faction='SYSTEM / Region A Overdrive',palette=hex,layers=names,pixelsPerUnit=32,constructionPixelScale=2,beamAxisTopLeftY=17,preview='SectorPurpleLaser_Comparison.png',nativePreview='SectorPurpleLaser_480x270.png',warningPreview='SectorPurpleLaser_Warning_480x270.png',animatedPreview='SectorPurpleLaser_480x270.gif',families={},sourcePolicy='Derived from copied approved Region A laser layers. Purple Overdrive palette; no NULL geometry, red, external bloom, or baked boss pixels. Approved sources remain read-only.'}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
for _,v in ipairs(specs) do
 local s=Sprite(v.w,v.h,ColorMode.RGB);for i,n in ipairs(names) do local l=i==1 and s.layers[1] or s:newLayer();l.name=n end
 local frames={};local sheet=Image(v.w*4,v.h,ColorMode.RGB);local metadata={}
 for f=1,4 do
  if f>1 then s:newEmptyFrame(f) end;local l=v.make(f)
  for i,im in ipairs(l) do s:newCel(s.layers[i],f,im,Point(0,0)) end
  s.frames[f].duration=v.ms[f]/1000;local im=render(s,f);frames[f]=im;sheet:drawImage(im,Point((f-1)*v.w,0))
  metadata[#metadata+1]={filename=v.id..'_'..(f-1),frame={x=(f-1)*v.w,y=0,w=v.w,h=v.h},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=v.w,h=v.h},sourceSize={w=v.w,h=v.h},duration=v.ms[f]}
 end
 local t=s:newTag(1,4);t.name=v.tag
 local pal=Palette(6);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,c in ipairs(p) do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end;s:setPalette(pal)
 s:saveAs(out..'/'..v.id..'.aseprite');s:close();sheet:saveAs(out..'/'..v.id..'.png')
 local rec={id=v.id,label=v.label,width=v.w,height=v.h,frames=4,durationsMs=v.ms,tag=v.tag,mode=v.mode,tileAxis=v.tileAxis,pivotPixels=v.pivot,unityPivot={x=v.pivot.x/v.w,y=1-v.pivot.y/v.h},aseprite=v.id..'.aseprite',sheet=v.id..'.png',metadata=v.id..'.json',peakFrame=v.peak,notes=v.notes}
 local meta={frames=metadata,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=v.w*4,h=v.h},scale='1',frameTags={{name=v.tag,from=0,to=3,direction='forward'}}},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot=rec.unityPivot,loop=v.mode=='loop',direction='+X',tileAxis=v.tileAxis,transverseScale='Match collider width without inflating off-axis technical marks'}}
 if v.tag=='Sustain' then
  local slice=Image(8,32,ColorMode.RGB)
  for f,im in ipairs(frames) do for y=0,31 do for x=0,1 do slice:drawPixel((f-1)*2+x,y,im:getPixel(x,y)) end end end
  rec.coreSliceSheet=v.id..'_CoreSlice.png';slice:saveAs(out..'/'..rec.coreSliceSheet)
  rec.bodyBandPixels={14,18,14,10};rec.bodyBoundsTopLeftY={10,8,10,12};rec.whiteCorePixels=6
  meta.unity.bodyBandPixels=rec.bodyBandPixels;meta.unity.coreSlices={image=rec.coreSliceSheet,cellWidth=2,cellHeight=32,frames=4,pivot=rec.unityPivot,stretchAxis='X',note='Stretch/tile only the body slice. Preserve per-frame Y thickness; decorative marks are omitted.'}
 end
 write(v.id..'.json',meta);manifest.families[#manifest.families+1]=rec;cache[v.id]=frames
end
local bg=rgba('101820');local panel=rgba('18232D');local ink=rgba('DCE5E3');local muted=rgba('83949D')
local function canvas(w,h) local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local contact=canvas(800,648)
text(contact,'SECTOR ADMINISTRATOR / PURPLE LASER',24,20,ink,2)
text(contact,'SAME SYSTEM / PHASE 2 OVERDRIVE',24,44,p[4],1)
local greenFrames={};for f=1,4 do greenFrames[f]=render(greenBeam,f+3) end
local rows={{'GREEN PHASE 1',greenFrames,rgba('76D879'),'APPROVED SUSTAIN'},{'PURPLE WARNING',cache.VFX_Sector_PurpleLaserWarning,p[3],'SEGMENTED / NO WHITE'},{'PURPLE ACTIVE',cache.VFX_Sector_PurpleLaserBeam,p[4],'WHITE HOT / CLEAN EDGE'},{'EMITTER BURST',cache.VFX_Sector_PurpleLaserEmitter,p[4],'COMPACT / CLEANUP END'}}
for i,row in ipairs(rows) do local y=88+(i-1)*132
 H.rect(contact,16,y-12,768,110,panel);text(contact,row[1],28,y,row[3],2);text(contact,row[4],28,y+24,muted,1)
 for f,im in ipairs(row[2]) do
  local x=240+(f-1)*128;text(contact,tostring(f),x+56,y,muted,1)
  contact:drawImage(H.scale(im,2),Point(x,y+24))
 end
end
text(contact,'INTEGER 2X REVIEW / 64X32 BEAMS / 32X32 EMITTER',24,624,muted,1)
contact:saveAs(out..'/'..manifest.preview)
local purpleBoss=Image{fromFile=src..'/purple_boss.png'};local player=Image{fromFile=src..'/player_reference.png'}
assert(purpleBoss.width==128 and purpleBoss.height==128)
-- Review-only nearest-pixel rotation. Exported VFX themselves remain unrotated.
local function drawRay(im,tile,cx,cy,angle)
 local a=angle*math.pi/180;local co,si=math.cos(a),math.sin(a)
 for y=42,245 do for x=16,463 do
  local dx,dy=x+0.5-cx,y+0.5-cy;local u=dx*co+dy*si;local v=-dx*si+dy*co
  if u>=0 and v>=-17 and v<15 then local tx=math.floor(u)%64;local ty=math.floor(v+17);local c=tile:getPixel(tx,ty);if pc.rgbaA(c)>0 then im:drawPixel(x,y,c) end end
 end end
end
local function frameAt(ms,durations) for i,t in ipairs(durations) do if ms<t then return i end;ms=ms-t end;return #durations end
local function native(ms,forceWarning)
 local warning=forceWarning or ms<600;local elapsed=math.max(0,ms-600)
 local tile=warning and cache.VFX_Sector_PurpleLaserWarning[math.floor(ms/120)%4+1] or cache.VFX_Sector_PurpleLaserBeam[math.floor(elapsed/60)%4+1]
 local im=canvas(480,270);text(im,'SECTOR ADMINISTRATOR / PURPLE LASER',16,14,ink,2)
 text(im,warning and 'WARNING / NO DAMAGE' or 'PHASE 2 / ACTIVE SPOKES',16,30,warning and p[3] or p[4],1)
 local angle=warning and 15 or 15-elapsed*0.036
 for i=0,5 do drawRay(im,tile,240,144,angle+i*60) end
 -- Boss and player references stay exact, native size, above the beam for legibility.
 im:drawImage(purpleBoss,Point(176,80))
 im:drawImage(player,Point(334,205))
 if not warning and elapsed<210 then
  local e=cache.VFX_Sector_PurpleLaserEmitter[frameAt(elapsed,{40,60,70,40})]
  local a=angle*math.pi/180;local co,si=math.cos(a),math.sin(a);local ex,ey=240+64*co,144+64*si
  for y=math.floor(ey-32),math.ceil(ey+32) do for x=math.floor(ex-32),math.ceil(ex+32) do
   local dx,dy=x+0.5-ex,y+0.5-ey;local u=math.floor(dx*co+dy*si+8);local v=math.floor(-dx*si+dy*co+17)
   if u>=0 and u<32 and v>=0 and v<32 then local c=e:getPixel(u,v);if pc.rgbaA(c)>0 then im:drawPixel(x,y,c) end end
  end end
 end
 text(im,'NATIVE 480X270 ART REVIEW / NOT A GAME CAPTURE',16,254,muted,1)
 return im
end
native(720,false):saveAs(out..'/'..manifest.nativePreview)
native(120,true):saveAs(out..'/'..manifest.warningPreview)
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native rotating spoke review'
for f=1,60 do if f>1 then review:newEmptyFrame(f) end;review:newCel(review.layers[1],f,native((f-1)*40,false),Point(0,0));review.frames[f].duration=0.04 end
review:saveAs(out..'/'..manifest.animatedPreview);review:close()
manifest.nativeReview={width=480,height=270,frames=60,frameDurationMs=40,warningMs=600,rotationDegreesPerSecond=-36,sourceArtScale=1,playerReferenceNativeSize={w=player.width,h=player.height},note='Illustrative six-spoke composition and playback only; not a Unity capture or gameplay timing change.'}
manifest.integration={observedProductionPPU=32,observedProductionBeamPivot={x=0,y=0.46875},existingPhase2='SectorPartitionLane selects solidVisual when empowerment is positive. This pack is not wired into runtime.',activeAnimation='New four-frame Sustain clip only; existing Phase 1 code expects three Fire plus four Sustain frames, so do not assign it as a seven-frame drop-in.',optionalDetail='Integrated original technical markers; no separate support particle asset is needed.'}
write('manifest.json',manifest)
greenBeam:close();greenWarning:close()
local f=assert(io.open(out..'/generation_complete.txt','w'));f:write('Three Region A purple laser families generated by Aseprite CLI + Lua.\n');f:close()
