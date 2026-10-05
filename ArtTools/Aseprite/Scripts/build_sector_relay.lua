-- SYSTEM relay and local connection effects. Approved assets are read-only copies.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local function write(n,t) local f=assert(io.open(out..'/'..n,'w'));f:write(json.encode(t));f:close() end
local function render(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local extra={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,s,x,y,c,n)
 for i=1,#s do local ch=s:sub(i,i);local xx=x+(i-1)*4*n
  if extra[ch] then for yy,row in ipairs(extra[ch]) do for col=1,3 do if row:sub(col,col)=='1' then H.rect(im,xx+(col-1)*n,y+(yy-1)*n,n,n,c) end end end
  else H.text(im,ch,xx,y,c,n) end
 end
end
local palettes={metal={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'},green={'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'},purple={'452D66','8353B8','BA79FF','D9A5FF','F0D3FF','FFF6FF'},cyan={'163454','388ED1','68C7ED','ABE8F4','E8FAFF'}}
local P={};for k,colors in pairs(palettes) do P[k]={};for i,h in ipairs(colors) do P[k][i]=rgba(h) end end
local function layers(w,h,n) local ls={};for i=1,n or 4 do ls[i]=Image(w,h,ColorMode.RGB) end;return ls end
local function scaleLayers(ls) local r={};for i,im in ipairs(ls) do r[i]=H.scale(im,2) end;return r end
local original=assert(app.open(src..'/relay_source.aseprite'));local source=render(original,1)
local png=Image{fromFile=src..'/relay_source.png'};local production=Image{fromFile=src..'/production_relay.png'}
assert(source.width==64 and source.height==64 and #original.frames==1)
for it in source:pixels() do assert(it()==png:getPixel(it.x,it.y) and it()==production:getPixel(it.x,it.y),'Approved relay source mismatch') end
original:close()
local energyIndex={};for i,c in ipairs(P.green) do energyIndex[c]=i end
local greenRelay=layers(48,48,3)
-- 64px at the production 42.666668 PPU = 48px at 32 PPU.
-- Point sampling preserves the established construction, symmetry and colors.
for y=0,47 do for x=0,47 do
 -- Resolve exact half-pixel sampling ties consistently across the symmetry axis.
 local sx=x<24 and math.floor((x+.5)*64/48) or 63-math.floor((47-x+.5)*64/48)
 local c=source:getPixel(sx,math.floor((y+.5)*64/48))
 if pc.rgbaA(c)>0 then
  local e=energyIndex[c];local layer=e and (math.abs(x-23.5)+math.abs(y-23.5)<=10 and 2 or 3) or 1
  greenRelay[layer]:drawPixel(x,y,c)
 end
end end
-- Small regulated connection socket and emitter center, shared by both states.
H.rect(greenRelay[3],22,12,4,2,P.green[3]);H.rect(greenRelay[3],23,14,2,3,P.green[2])
H.rect(greenRelay[2],23,23,2,2,P.green[6])
local function unit(family)
 local ls=layers(48,48,3)
 for i,im in ipairs(greenRelay) do for it in im:pixels() do local c=it();if family=='purple' and i>1 and energyIndex[c] then c=P.purple[energyIndex[c]] end;ls[i]:drawPixel(it.x,it.y,c) end end
 return ls
end
local unitMask=Image(48,48,ColorMode.RGB);for _,im in ipairs(greenRelay) do unitMask:drawImage(im,Point(0,0)) end
local function brackets(im,cx,cy,r,len,c)
 for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do
  local x,y=cx+sx*r,cy+sy*r
  H.line(im,x,y,x-sx*len,y,c);H.line(im,x,y,x,y-sy*len,c)
 end end
end
local function deploy(f)
 local ls=layers(24,24);local c=P.cyan;local p=P.purple
 if f==1 then brackets(ls[3],12,12,5,2,c[3]);H.rect(ls[1],11,11,2,2,c[2])
 elseif f==2 then
  brackets(ls[3],12,12,7,2,c[4]);H.diamond(ls[1],11.5,11.5,4.5,c[3]);H.rect(ls[2],10,10,4,4,c[5])
 elseif f==3 then
  brackets(ls[3],12,12,9,2,c[3]);H.rect(ls[2],11,11,2,2,c[5])
  -- A short materialization scan follows this relay's contour, never a portal.
  for y=8,15 do for x=3,20 do
   if pc.rgbaA(unitMask:getPixel(x*2,y*2))>0 and (pc.rgbaA(unitMask:getPixel(x*2-2,y*2))==0 or pc.rgbaA(unitMask:getPixel(x*2+2,y*2))==0) then H.put(ls[1],x,y,c[4]) end
  end end
 elseif f==4 then
  brackets(ls[3],12,12,8,1,p[3]);H.rect(ls[1],11,10,2,4,p[4]);H.rect(ls[2],11,11,2,2,p[6])
  for _,x in ipairs({4,19}) do H.put(ls[4],x,8,c[4]);H.put(ls[4],x,15,p[4]) end
 elseif f==5 then
  for _,q in ipairs({{8,8},{15,8},{8,15},{15,15}}) do H.put(ls[4],q[1],q[2],p[3]) end
 end
 return scaleLayers(ls)
end
local function disconnect(f)
 local ls=layers(8,8);local c=P.green
 if f==1 then H.rect(ls[1],2,3,5,2,c[4]);H.rect(ls[2],2,3,2,2,c[6])
 elseif f==2 then H.rect(ls[1],2,4,4,1,c[3]);H.put(ls[3],1,3,c[2]);H.put(ls[3],1,5,c[2])
 elseif f==3 then H.put(ls[1],2,4,c[3]);H.put(ls[3],4,4,c[2])
 elseif f==4 then H.put(ls[4],2,4,c[5]) end
 return scaleLayers(ls)
end
local function reconnect(f)
 local ls=layers(16,8);local c=P.purple
 if f==1 then H.diamond(ls[1],3.5,3.5,1.5,c[3]);H.put(ls[3],2,1,c[2]);H.put(ls[3],2,6,c[2])
 elseif f==2 then H.diamond(ls[1],3.5,3.5,2.5,c[3]);H.rect(ls[2],3,3,2,2,c[6]);H.put(ls[3],6,2,c[4]);H.put(ls[3],6,5,c[4])
 elseif f==3 then
  H.rect(ls[1],3,2,11,4,c[3]);H.rect(ls[3],3,1,10,1,c[1]);H.rect(ls[3],3,6,10,1,c[1])
  H.rect(ls[2],4,3,10,2,c[6]);H.rect(ls[1],14,3,1,2,c[4])
 elseif f==4 then H.rect(ls[1],3,3,6,2,c[3]);H.rect(ls[2],3,3,2,2,c[6]);for _,q in ipairs({{10,2},{12,3},{10,5}}) do H.put(ls[4],q[1],q[2],c[4]) end end
 return scaleLayers(ls)
end
local function spark(f,family)
 local ls=layers(8,8);local c=P[family]
 if f==1 then H.rect(ls[1],3,3,2,2,c[4]);H.rect(ls[2],3,3,1,1,c[6])
 elseif f==2 then for _,q in ipairs({{2,2},{5,2},{2,5},{5,5}}) do H.put(ls[4],q[1],q[2],c[4]) end end
 return scaleLayers(ls)
end
local effectLayers={'Energy Shape','White Center','Connection Marks','Spark'}
local specs={
 {id='Sector_LaserRelay',label='LASER RELAY',w=48,h=48,mode='states',pivot={24,24},layerNames={'Relay Chassis','Emitter','Energy'},maxColors=13,clips={{name='Green',palette='green',ms={1000},make=function()return unit('green')end},{name='Purple',palette='purple',ms={1000},make=function()return unit('purple')end}}},
 {id='VFX_Sector_RelayDeploy',label='RELAY DEPLOY',w=48,h=48,mode='one_shot',pivot={24,24},maxColors=6,clips={{name='Deploy',palette='cyan_purple',ms={50,40,50,60,70,40},make=deploy}}},
 {id='VFX_Sector_RelayDisconnect',label='GREEN DISCONNECT',w=16,h=16,mode='one_shot',pivot={4,8},maxColors=4,clips={{name='Disconnect',palette='green',ms={40,50,50,60,40},make=disconnect}}},
 {id='VFX_Sector_RelayReconnect',label='PURPLE RECONNECT',w=32,h=16,mode='one_shot',pivot={8,8},maxColors=4,clips={{name='Reconnect',palette='purple',ms={50,50,40,60,30},make=reconnect}}},
 {id='VFX_Sector_RelayStabilize',label='STABILIZATION',w=16,h=16,mode='one_shot',pivot={8,8},maxColors=3,clips={{name='Green',palette='green',ms={40,50,40},make=function(f)return spark(f,'green')end},{name='Purple',palette='purple',ms={40,50,40},make=function(f)return spark(f,'purple')end}}}
}
local manifest={generatorId='void-scrapper-sector-relay-v1',name='Sector Administrator Phase 2 Laser Relay Pack',pixelsPerUnit=32,palettes=palettes,families={},preview='SectorRelay_ContactSheet.png',comparison='SectorRelay_GreenPurple_Comparison.png',nativePreview='SectorRelay_480x270.png',animatedPreview='SectorRelay_480x270.gif',formationPreview='SectorRelay_SixUnitFormation_Review.png'}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local bg,ink,muted,panel=rgba('101820'),rgba('DCE5E3'),rgba('83949D'),rgba('18232D')
local function canvas(w,h) local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local cache,rows={},{ }
for _,v in ipairs(specs) do
 local s=Sprite(v.w,v.h,ColorMode.RGB);local names=v.layerNames or effectLayers
 for i,n in ipairs(names) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=n end
 local cols=0;for _,c in ipairs(v.clips) do cols=math.max(cols,#c.ms) end
 local sheet=Image(cols*v.w,#v.clips*v.h,ColorMode.RGB);local metadata,clips,tags={},{},{};local n=0
 for row,clip in ipairs(v.clips) do
  local from=n+1;local frames={};local strip=Image(#clip.ms*v.w,v.h,ColorMode.RGB)
  for f,ms in ipairs(clip.ms) do
   n=n+1;if n>1 then s:newEmptyFrame(n) end
   for i,im in ipairs(clip.make(f)) do s:newCel(s.layers[i],n,im,Point(0,0)) end
   s.frames[n].duration=ms/1000;local im=render(s,n);frames[f]=im
   sheet:drawImage(im,Point((f-1)*v.w,(row-1)*v.h));strip:drawImage(im,Point((f-1)*v.w,0))
   metadata[#metadata+1]={filename=v.id..'_'..clip.name..'_'..f,frame={x=(f-1)*v.w,y=(row-1)*v.h,w=v.w,h=v.h},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=v.w,h=v.h},sourceSize={w=v.w,h=v.h},duration=ms}
  end
  local filename=#v.clips>1 and v.id..'_'..clip.name..'.png' or v.id..'.png';if #v.clips>1 then strip:saveAs(out..'/'..filename) end
  tags[#tags+1]={name=clip.name,from=from,to=n}
  local rec={name=clip.name,from=from,to=n,durationsMs=clip.ms,palette=clip.palette,sheet=filename,preview=v.id..'_'..clip.name..'_Preview.png'};clips[#clips+1]=rec
  local preview=canvas(640,224);text(preview,v.label..' / '..clip.name:upper(),16,16,ink,2);text(preview,'INTEGER 2X REVIEW / TRANSPARENT GAMEPLAY EXPORTS',16,38,muted,1)
  for f,im in ipairs(frames) do text(preview,tostring(f),30+(f-1)*102,63,muted,1);preview:drawImage(H.scale(im,2),Point(20+(f-1)*102,88)) end
  preview:saveAs(out..'/'..rec.preview);cache[v.id..'/'..clip.name]=frames;rows[#rows+1]={v=v,clip=clip,frames=frames}
 end
 for _,t in ipairs(tags) do local tag=s:newTag(t.from,t.to);tag.name=t.name end
 local colors,seen={},{};for f=1,#s.frames do for it in render(s,f):pixels() do local c=it();if pc.rgbaA(c)>0 and not seen[c] then colors[#colors+1]=c;seen[c]=true end end end
 local pal=Palette(#colors+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,c in ipairs(colors) do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end;s:setPalette(pal)
 s:saveAs(out..'/'..v.id..'.aseprite');s:close();sheet:saveAs(out..'/'..v.id..'.png')
 local pivot={x=v.pivot[1]/v.w,y=1-v.pivot[2]/v.h};local jt={};for _,t in ipairs(tags) do jt[#jt+1]={name=t.name,from=t.from-1,to=t.to-1,direction='forward'} end
 local rec={id=v.id,width=v.w,height=v.h,frames=n,mode=v.mode,layers=names,maxColors=v.maxColors,pivotPixels={x=v.pivot[1],y=v.pivot[2]},unityPivot=pivot,tags=tags,clips=clips,aseprite=v.id..'.aseprite',sheet=v.id..'.png',metadata=v.id..'.json'}
 manifest.families[#manifest.families+1]=rec
 write(rec.metadata,{frames=metadata,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=sheet.width,h=sheet.height},scale='1',frameTags=jt},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot=pivot,clips=clips,loop=false}})
end
local contact=canvas(960,1190);text(contact,'SECTOR ADMINISTRATOR / LASER RELAY PACK',20,20,ink,3);text(contact,'ONE STANDARD CHASSIS / GREEN TO PURPLE OVERDRIVE',20,46,muted,1)
for i,row in ipairs(rows) do local y=76+(i-1)*154;H.rect(contact,12,y,936,144,panel);text(contact,row.v.label,24,y+12,ink,2);text(contact,row.clip.name:upper()..' / '..row.v.w..'X'..row.v.h,24,y+36,muted,1)
 for f,im in ipairs(row.frames) do local x=324+(f-1)*102;text(contact,tostring(f),x+26,y+10,muted,1);contact:drawImage(H.scale(im,2),Point(x,y+36)) end
end;contact:saveAs(out..'/'..manifest.preview)
local comparison=canvas(640,296);text(comparison,'GREEN / PURPLE / SAME RELAY CHASSIS',32,16,ink,2)
for i,family in ipairs({'Green','Purple'}) do local cx=i==1 and 160 or 480;local im=cache['Sector_LaserRelay/'..family][1];text(comparison,family:upper(),cx-36,40,i==1 and P.green[5] or P.purple[4],1);comparison:drawImage(H.scale(im,3),Point(cx-72,60));comparison:drawImage(im,Point(cx-24,226)) end
text(comparison,'INTEGER 3X ABOVE / NATIVE 48X48 BELOW',152,284,muted,1);comparison:saveAs(out..'/'..manifest.comparison)

-- NATIVE_PREVIEWS
local bosses={Image{fromFile=src..'/boss_green.png'},Image{fromFile=src..'/boss_purple.png'}}
local player=Image{fromFile=src..'/player.png'}
local function loadBeam(name)
 local s=assert(app.open(src..'/'..name));local frames={}
 for _,t in ipairs(s.tags) do if t.name=='Sustain' then for f=t.fromFrame.frameNumber,t.toFrame.frameNumber do frames[#frames+1]=render(s,f) end end end
 assert(#frames==4,'Expected four approved Sustain frames');s:close();return frames
end
local beams={loadBeam('green_beam.aseprite'),loadBeam('purple_beam.aseprite')}
local purplePNG=Image{fromFile=src..'/purple_beam.png'};local prodPurple=Image{fromFile=src..'/production_purple_beam.png'}
for it in purplePNG:pixels() do assert(it()==prodPurple:getPixel(it.x,it.y),'Production purple beam differs from approved source') end
local function sample(ms,ds)
 for f,d in ipairs(ds) do if ms<d then return f end;ms=ms-d end;return #ds
end
local function drawRotated(dest,im,cx,cy,angle,px,py)
 local a=angle*math.pi/180;local co,si=math.cos(a),math.sin(a);local r=math.ceil(math.sqrt(im.width^2+im.height^2))
 for y=math.max(0,math.floor(cy-r)),math.min(dest.height-1,math.ceil(cy+r)) do for x=math.max(0,math.floor(cx-r)),math.min(dest.width-1,math.ceil(cx+r)) do
  local dx,dy=x+.5-cx,y+.5-cy;local u=math.floor(dx*co+dy*si+px);local v=math.floor(-dx*si+dy*co+py)
  if u>=0 and u<im.width and v>=0 and v<im.height then local c=im:getPixel(u,v);if pc.rgbaA(c)>0 then dest:drawPixel(x,y,c) end end
 end end
end
local function beam(dest,a,b,phase,frame,fraction)
 local dx,dy=b.x-a.x,b.y-a.y;local len=math.sqrt(dx*dx+dy*dy);if len<1 or fraction<=0 then return end
 local co,si=dx/len,dy/len;len=len*fraction;local x1,y1=a.x+co*len,a.y+si*len
 local im=beams[phase][frame]
 for y=math.max(0,math.floor(math.min(a.y,y1)-12)),math.min(269,math.ceil(math.max(a.y,y1)+12)) do
  for x=math.max(0,math.floor(math.min(a.x,x1)-12)),math.min(479,math.ceil(math.max(a.x,x1)+12)) do
   local ox,oy=x+.5-a.x,y+.5-a.y;local u=ox*co+oy*si;local v=-ox*si+oy*co
   if u>=0 and u<len then local sy=math.floor(v/.5+17);if sy>=0 and sy<32 then local c=im:getPixel(math.floor(u)%64,sy);if pc.rgbaA(c)>0 then dest:drawPixel(x,y,c) end end end
  end
 end
end
local cx,cy=240,136
local initial={{x=128,y=202},{x=128,y=70},{x=352,y=70},{x=352,y=202}}
local final={{x=162,y=181},{x=162,y=91},{x=318,y=91},{x=318,y=181},{x=240,y=46},{x=240,y=226}}
local function position(i,ms,rotate)
 local finish=final[i];local p=math.min(1,math.max(0,(ms-880)/480));local first=initial[i] or finish
 local x,y=first.x+(finish.x-first.x)*p,first.y+(finish.y-first.y)*p
 if rotate and ms>=1800 then local a=-(ms-1800)*.015*math.pi/180;local dx,dy=x-cx,y-cy;x=cx+dx*math.cos(a)-dy*math.sin(a);y=cy+dx*math.sin(a)+dy*math.cos(a) end
 return {x=math.floor(x+.5),y=math.floor(y+.5)}
end
local function view(ms,rotate)
 local im=canvas(480,270);local phase=ms<1240 and 1 or 2;local count=ms<1050 and 4 or 6
 local points={};for i=1,6 do points[i]=position(i,ms,rotate) end
 local fraction=ms<600 and 1 or ms<840 and math.max(0,1-(ms-600)/160) or ms>=1540 and math.min(1,(ms-1540)/140) or 0
 local beamFrame=math.floor(ms/60)%4+1
 if fraction>0 then
  local order=count==4 and {1,2,3,4} or {5,2,1,6,4,3}
  for k,i in ipairs(order) do beam(im,points[i],points[order[k%#order+1]],phase,beamFrame,fraction) end
  for i=1,count do beam(im,points[i],{x=cx,y=cy},phase,beamFrame,fraction) end
 end
 im:drawImage(bosses[phase],Point(cx-64,cy-64));im:drawImage(player,Point(396,167))
 for i=1,6 do
  local p=points[i];local angle=math.atan(cy-p.y,cx-p.x)*180/math.pi;local rotation=angle+90
  if i<=count then
   local sprite=cache['Sector_LaserRelay/'..(phase==1 and 'Green' or 'Purple')][1]
   drawRotated(im,sprite,p.x,p.y,rotation,24,24)
  end
  if i<=4 and ms>=600 and ms<840 then
   local f=sample(ms-600,{40,50,50,60,40});drawRotated(im,cache['VFX_Sector_RelayDisconnect/Disconnect'][f],p.x,p.y,angle,4,8)
  end
  if i>=5 and ms>=960 and ms<1270 then
   local f=sample(ms-960,{50,40,50,60,70,40});drawRotated(im,cache['VFX_Sector_RelayDeploy/Deploy'][f],p.x,p.y,rotation,24,24)
  end
  if ms>=1300 and ms<1430 then
   local f=sample(ms-1300,{40,50,40});drawRotated(im,cache['VFX_Sector_RelayStabilize/Purple'][f],p.x,p.y,0,8,8)
  end
  if ms>=1400 and ms<1630 then
   local f=sample(ms-1400,{50,50,40,60,30});drawRotated(im,cache['VFX_Sector_RelayReconnect/Reconnect'][f],p.x,p.y,angle,8,8)
  end
  if i<=count then
   local tx=p.x<cx and p.x-29 or p.x+25;local label=tostring(i)..(i>=5 and ' NEW' or '')
   text(im,label,tx,p.y-3,i>=5 and P.purple[4] or muted,1)
  end
 end
 local title=ms<600 and 'PHASE 1 / FOUR EXISTING RELAYS' or ms<840 and 'GREEN LASER CONNECTIONS DISCONNECT' or ms<1300 and 'TWO STANDARD RELAYS DEPLOY' or ms<1680 and 'PURPLE CONNECTIONS REBUILD' or 'PHASE 2 / SIX STANDARD RELAYS'
 text(im,title,14,12,ink,2);text(im,'NATIVE 480X270 ART REVIEW / NOT A UNITY CAPTURE',14,258,muted,1)
 return im
end
local native=view(1800,false);native:saveAs(out..'/'..manifest.nativePreview)
H.scale(native,2):saveAs(out..'/'..manifest.formationPreview)
local formationComparison=canvas(960,308);text(formationComparison,'FOUR EXISTING POSITIONS / TWO ADDED TOP AND BOTTOM',20,14,ink,2)
formationComparison:drawImage(view(0,false),Point(0,34));formationComparison:drawImage(native,Point(480,34));formationComparison:saveAs(out..'/SectorRelay_FormationComparison_Review.png')
view(720,false):saveAs(out..'/SectorRelay_Disconnect_480x270.png')
view(1080,false):saveAs(out..'/SectorRelay_Deploy_480x270.png')
view(1560,false):saveAs(out..'/SectorRelay_Reconnect_480x270.png')
view(3400,true):saveAs(out..'/SectorRelay_Rotation_480x270.png')
local animation=Sprite(480,270,ColorMode.RGB);animation.layers[1].name='Reference-only transition and rotating formation'
for f=1,100 do if f>1 then animation:newEmptyFrame(f) end;animation:newCel(animation.layers[1],f,view((f-1)*40,true),Point(0,0));animation.frames[f].duration=.04 end
animation:saveAs(out..'/'..manifest.animatedPreview);animation:close()
manifest.totalFrames=24;manifest.totalTags=7
manifest.relay={source='Output/04_SystemSupport/system_support_green.aseprite',fit='64 to 48 point sampling matches 64/42.666668 and 48/32 world extents',sourceCanvas={w=64,h=64},originalProductionPPU=42.666668,sharedDesigns=1,stateNames={'Green','Purple'},chassisPixelIdentical=true,silhouettePixelIdentical=true,orientation='north / negative image Y',emitterPixels={x=24,y=24},socketLightPixels={x=24,y=13},unchangedArmor=true}
manifest.handoff={deployRevealAfterMs=90,deployUsesIndependentRelaySprite=true,reconnectBeamAfterMs=140,approvedBeam='Output/24_SectorPurpleLaserVFX/VFX_Sector_PurpleLaserBeam.aseprite',approvedBeamRegenerated=false,redUsed=false,unityFilesModified=false}
manifest.formation={existingIds={1,2,3,4},additionalIds={5,6},existingInitialPositions=initial,sixFinalPositions=final,perimeterOrder={5,2,1,6,4,3},referenceOnly=true,scale='native art review; world positions remain owned by Unity',rotationDegreesPerSecond=-15,previewBeamTransverseScale=.5}
manifest.nativeReview={width=480,height=270,frames=100,frameDurationMs=40,previewIsUnityCapture=false}
write('manifest.json',manifest)
local f=assert(io.open(out..'/generation_complete.txt','w'));f:write('Sector relay pack authored with Aseprite CLI + Lua.\n');f:close()
