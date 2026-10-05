-- NULL DISPATCHER: three functions integrated into one broken authority frame.
-- The approved Purple Core is copied pixel-for-pixel, never reconstructed.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,scale=h.put,h.rect,h.line,h.poly,h.oct,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local energyHex={'35204F','603785','9650C1','CF77EB','F0B3FB','FCE9FF'}
local m,e={},{};for i,v in ipairs(metalHex) do m[i]=rgba(v) end;for i,v in ipairs(energyHex) do e[i]=rgba(v) end
local function saveJson(path,v) local f=assert(io.open(path,'w'));f:write(json.encode(v));f:close() end
local function image() return Image(80,80,ColorMode.RGB) end
local function stroke(im,points,c) for i=1,#points-1 do line(im,points[i][1],points[i][2],points[i+1][1],points[i+1][2],c) end end
local function shift(im,dx,dy) local n=image();for it in im:pixels() do if pc.rgbaA(it())>0 then put(n,it.x+dx,it.y+dy,it()) end end;return n end
local function flatten(images,size) local im=Image(size,size,ColorMode.RGB);for _,v in ipairs(images) do im:drawImage(v,Point(0,0)) end;return im end
local function flip(im,xflip,yflip) local n=image();for it in im:pixels() do if pc.rgbaA(it())>0 then put(n,xflip and 79-it.x or it.x,yflip and 79-it.y or it.y,it()) end end;return n end
local sourceCores={}
for _,name in ipairs({'inactive','active','overloaded'}) do
 local s=assert(app.open(sourceDir..'/core_purple_corrupted_'..name..'.aseprite'))
 assert(s.width==64 and s.height==64 and #s.layers==3,'Approved core dimensions or layers changed')
 local frames={}
 for fi=1,#s.frames do
  frames[fi]={}
  for li=1,3 do local cel=assert(s.layers[li]:cel(fi));assert(cel.opacity==255 and s.layers[li].opacity==255);local im=Image(64,64,ColorMode.RGB);im:drawImage(cel.image,cel.position);frames[fi][li]=im end
 end
 sourceCores[name]=frames;s:close()
end
local states={
 {id='dormant_sealed',tag='Dormant - Sealed',label='DORMANT / SEALED',core='inactive',sourceFrame=1,coreScale=1,spread=0,desc='Intact authority frame; overlapping shutters hide most of the inactive core.'},
 {id='active',tag='Active',label='ACTIVE',core='active',sourceFrame=1,coreScale=1,spread=3,desc='Shutters withdrawn; three functions receive visible network links and the approved active core is uncovered.'},
 {id='phase2_unbound',tag='Phase 2 - Unbound',label='PHASE 2 / UNBOUND',core='overloaded',sourceFrame=1,coreScale=2,spread=9,desc='Exact nearest-neighbor doubling of the overloaded core; floating shell quadrants, dislocated fragments and interrupted routing.'},
 {id='critical_exposed',tag='Critical - Core Exposed',label='CRITICAL / EXPOSED',core='overloaded',sourceFrame=3,coreScale=2,spread=9,desc='Failed outer frame and broken functional hardware; unstable dark-purple core frame, spatial seams and severed links.'}
}
local layerNames={'Network Lines','Purple Core - Housing','Purple Core - Energy','Purple Core - Details','Main Shell','Control Section','Recovery Section','Phase Section','Outer Fragments','Corruption VFX','Damage'}
local function quarter(d)
 local im=image()
 local function p(points,c) local q={};for _,v in ipairs(points) do q[#q+1]={v[1]-d,v[2]-d} end;poly(im,q,c) end
 local function l(points,c) local q={};for _,v in ipairs(points) do q[#q+1]={v[1]-d,v[2]-d} end;stroke(im,q,c) end
 p({{28,13},{36,13},{36,19},{30,22},{24,28},{21,35},{14,35},{14,27}},m[1])
 p({{29,14},{35,14},{35,18},{29,21},{22,28},{20,33},{15,33},{16,27}},m[3])
 p({{28,15},{34,15},{34,17},{28,20},{21,27},{19,31},{16,31},{18,26}},m[6])
 l({{28,15},{19,24},{17,28}},m[7]);l({{20,31},{23,27},{29,21},{34,18}},m[4])
 l({{26,20},{28,18},{31,18}},m[2]);l({{19,29},{21,25}},m[2])
 l({{23,23},{25,21}},m[4]);l({{15,32},{19,32}},m[4])
 return im
end
local function shell(st,si)
 local im=image();local q=quarter(st.spread)
 for i,flips in ipairs({{false,false},{true,false},{false,true},{true,true}}) do
  local part=flip(q,flips[1],flips[2])
  if si==4 then
   -- Remove real pixels from two quadrants, leaving actual transparent gaps.
   if i==2 then rect(part,53,4,20,15,0);rect(part,70,19,5,5,0)
   elseif i==3 then rect(part,5,59,20,17,0);rect(part,22,54,5,8,0) end
  end
  im:drawImage(part,Point(0,0))
 end
 if si<=2 then
  -- A structural aft yoke makes this one chassis, not a collection of boss parts.
  local d=si==2 and 2 or 0
  poly(im,{{26-d,64},{31-d,60},{48+d,60},{53+d,64},{48+d,70},{31-d,70}},m[1])
  poly(im,{{29-d,64},{32-d,62},{47+d,62},{50+d,64},{47+d,68},{32-d,68}},m[3])
  line(im,32-d,63,47+d,63,m[6]);line(im,34,67,45,67,si==1 and e[1] or e[3])
  rect(im,37,62,6,2,m[1]);rect(im,38,62,4,1,m[4])
 end
 if si==1 then
  local a=image()
  poly(a,{{29,23},{33,23},{38,34},{38,45},{31,55},{25,50},{24,29}},m[1])
  poly(a,{{29,25},{32,25},{36,35},{36,44},{30,52},{27,49},{26,30}},m[5])
  stroke(a,{{29,25},{27,30},{28,46},{30,49}},m[7]);line(a,34,35,34,43,m[3])
  rect(a,29,34,3,10,m[2]);rect(a,30,35,1,3,e[2]);line(a,30,42,31,42,m[4])
  im:drawImage(a,Point(0,0));im:drawImage(flip(a,true,false),Point(0,0))
  -- Upper and lower locking tongues stop short of the central sealed seam.
  poly(im,{{35,24},{44,24},{43,29},{40,32},{37,29}},m[4]);line(im,36,25,43,25,m[6])
  poly(im,{{37,49},{40,47},{43,49},{44,54},{35,54}},m[3]);line(im,37,53,42,53,m[5])
 end
 return im
end
local function control(si)
 local im=image();local active=si>1
 -- Server-register crown and blunt address fins, with an obvious top/front key.
 poly(im,{{29,11},{35,11},{37,8},{42,8},{44,11},{50,11},{54,16},{53,23},{46,25},{33,25},{26,23},{25,16}},m[1])
 poly(im,{{29,13},{36,13},{38,10},{41,10},{43,13},{50,13},{52,16},{51,21},{46,23},{33,23},{28,21},{27,16}},m[3])
 poly(im,{{29,13},{36,13},{38,10},{41,10},{43,13},{50,13},{51,15},{43,16},{36,16},{28,15}},m[6])
 line(im,30,13,35,13,m[7]);line(im,38,10,41,10,m[7])
 rect(im,30,17,20,4,m[1]);rect(im,32,18,4,2,active and e[4] or e[1]);rect(im,38,18,4,2,active and e[5] or e[2]);rect(im,44,18,4,2,active and e[3] or e[1])
 for _,x in ipairs({29,49}) do rect(im,x,10,2,4,m[4]);put(im,x,10,m[6]) end
 rect(im,37,23,6,3,m[1]);line(im,38,23,41,23,active and e[4] or m[4])
 if si==4 then
  rect(im,43,8,12,11,0);poly(im,{{42,14},{46,16},{44,20},{49,20},{49,23},{43,23}},m[2]);line(im,43,17,46,19,e[2])
  rect(im,30,17,6,3,m[1]);line(im,31,18,34,18,m[3])
 end
 return shift(im,si==4 and -2 or 0,si==1 and 0 or si==2 and -2 or -4)
end
local function recovery(si)
 local im=image();local active=si>1
 -- Rectangular compression throat: opposed jaws and stacked material slots.
 poly(im,{{13,44},{21,43},{27,48},{24,51},{18,49},{15,51},{15,60},{19,63},{25,60},{28,64},{22,68},{12,66},{9,60},{9,49}},m[1])
 poly(im,{{13,46},{20,45},{24,48},{22,49},{16,48},{12,51},{12,60},{17,64},{21,64},{24,62},{25,64},{21,66},{13,64},{11,59},{11,50}},m[5])
 stroke(im,{{13,46},{11,51},{11,58}},m[7]);line(im,14,64,19,66,m[3])
 rect(im,14,51,5,10,m[2]);for _,y in ipairs({51,55,59}) do rect(im,15,y,3,2,m[4]);put(im,15,y,m[6]) end
 poly(im,{{18,49},{24,50},{25,53},{20,53},{18,51}},m[3]);poly(im,{{18,61},{24,60},{25,57},{20,57},{18,59}},m[3])
 line(im,20,52,23,52,active and e[4] or e[1]);line(im,20,58,23,58,active and e[3] or e[1])
 rect(im,10,54,2,4,m[1]);rect(im,10,55,1,2,active and e[3] or m[4])
 if si==4 then
  rect(im,14,58,15,12,0);poly(im,{{11,58},{14,58},{14,62},{18,63},{16,65},{12,63}},m[2]);line(im,13,60,16,62,m[4]);rect(im,18,49,8,4,m[1])
 end
 return shift(im,si==1 and 0 or si==2 and -2 or -6,si>=3 and 2 or 0)
end
local function phase(si)
 local im=image();local active=si>1
 -- Open phase-routing clevis, thin and split, rather than the compression throat.
 poly(im,{{58,43},{64,44},{70,50},{71,57},{68,64},{61,69},{56,66},{60,63},{65,60},{67,56},{66,51},{62,48},{56,47}},m[1])
 poly(im,{{59,45},{63,46},{68,51},{69,56},{66,62},{61,66},{59,66},{62,62},{66,58},{67,54},{64,49},{60,48},{57,47}},m[5])
 stroke(im,{{60,45},{64,47},{68,52}},m[7]);stroke(im,{{67,59},{64,63},{61,65}},m[3])
 stroke(im,{{60,49},{63,51},{64,55},{62,59},{58,62}},active and e[3] or e[1])
 rect(im,67,53,4,4,m[1]);rect(im,68,54,2,2,active and e[5] or m[4])
 rect(im,55,46,3,2,m[4]);rect(im,55,62,4,2,m[4])
 if active then line(im,58,51,58,58,e[2]);rect(im,57,53,3,3,e[4]);put(im,58,54,e[6]) end
 if si==4 then rect(im,61,60,12,10,0);rect(im,58,53,3,6,0);line(im,66,57,68,60,e[2]) end
 return shift(im,si==1 and 0 or si==2 and 2 or 6,si>=3 and 1 or 0)
end
local function network(si)
 local im=image();local c=si==1 and m[2] or e[1];local bright=si==1 and e[1] or e[3]
 if si<=2 then
  stroke(im,{{39,23},{39,29},{36,32}},c);stroke(im,{{40,23},{40,29},{43,32}},bright)
  stroke(im,{{23,54},{29,54},{32,49},{33,47}},c);stroke(im,{{56,54},{50,54},{47,49},{46,47}},c)
  if si==2 then stroke(im,{{24,53},{28,53},{31,48}},e[4]);stroke(im,{{55,53},{51,53},{48,48}},e[3]);rect(im,38,26,4,1,e[4]) end
 else
  -- Interrupted links: conspicuous void gaps rather than a solid wireframe halo.
  stroke(im,{{39,20},{39,23},{42,23}},e[3]);stroke(im,{{43,25},{43,27},{41,29}},e[2])
  stroke(im,{{19,55},{23,55},{25,52}},e[3]);line(im,27,50,28,48,e[4])
  stroke(im,{{62,54},{57,54},{56,51}},e[3]);line(im,52,49,51,47,e[4])
  stroke(im,{{23,22},{26,22},{29,25}},e[2]);stroke(im,{{59,23},{55,23},{53,25}},e[3])
  if si==3 then stroke(im,{{25,64},{28,64},{30,60}},e[2]);stroke(im,{{54,65},{50,65},{48,61}},e[3])
  else rect(im,39,23,4,3,0);rect(im,22,52,6,5,0);rect(im,55,51,5,5,0);line(im,55,52,57,52,e[4]) end
 end
 return im
end
local function fragments(si)
 local im=image()
 if si==1 then return im end
 local parts=si==2 and {{12,38,0},{66,39,1}} or {{5,34,0},{72,31,1},{30,70,0},{47,71,1},{20,8,0},{58,9,1}}
 for _,p in ipairs(parts) do
  local x,y=p[1],p[2];poly(im,{{x,y},{x+3,y},{x+4,y+2},{x+2,y+6},{x,y+5}},m[1]);poly(im,{{x+1,y+1},{x+3,y+1},{x+2,y+4},{x+1,y+4}},p[3]==0 and m[5] or m[3]);put(im,x+1,y+1,m[7])
 end
 if si==4 then poly(im,{{52,11},{57,10},{59,13},{54,16}},m[3]);line(im,54,11,56,11,m[6]);poly(im,{{19,67},{23,67},{21,72},{18,70}},m[4]) end
 return im
end
local function corruption(si)
 local im=image();if si==1 then return im end
 if si==2 then
  rect(im,19,32,4,1,e[2]);rect(im,57,37,5,1,e[3]);rect(im,59,39,2,1,e[5]);rect(im,33,59,4,1,e[2])
 elseif si==3 then
  -- Broad but sparse displaced scan fragments retain the source core as the focal point.
  rect(im,9,28,7,1,e[3]);rect(im,12,30,3,1,e[5]);rect(im,64,24,6,1,e[2]);rect(im,66,26,3,1,e[4])
  rect(im,3,42,8,2,e[1]);rect(im,5,42,5,1,e[4]);rect(im,69,44,7,1,e[4]);rect(im,72,46,3,1,e[5])
  rect(im,26,66,6,1,e[3]);rect(im,49,68,7,1,e[2]);rect(im,32,11,5,1,e[4]);rect(im,44,9,3,1,e[2])
 else
  rect(im,9,31,9,2,e[1]);rect(im,12,31,4,1,e[4]);rect(im,61,35,13,1,e[4]);rect(im,66,37,4,1,e[2]);rect(im,13,47,6,1,e[3])
  rect(im,31,64,7,1,e[4]);rect(im,43,66,9,1,e[2]);rect(im,27,15,5,1,e[3]);rect(im,47,17,6,1,e[5])
  -- Offset echo of a severed link; deliberately off symmetry without random noise.
  stroke(im,{{57,61},{58,63},{62,63}},e[2]);line(im,60,65,63,65,e[4])
 end
 return im
end
local function damage(si)
 local im=image();if si~=4 then return im end
 poly(im,{{60,16},{65,18},{69,22},{68,26},{64,24},{64,21},{59,19}},m[2]);stroke(im,{{60,17},{63,18},{64,21},{67,23}},m[4]);line(im,65,20,68,20,e[3])
 poly(im,{{7,59},{10,56},{14,56},{16,59},{14,62},{10,62}},m[2]);line(im,10,58,14,60,m[4]);line(im,12,57,15,57,e[2])
 stroke(im,{{36,20},{33,22},{34,24}},e[2]);line(im,32,24,35,24,e[4])
 rect(im,64,63,2,2,e[3]);put(im,67,65,e[5]);rect(im,18,68,3,1,e[4]);put(im,16,70,e[2])
 return im
end
local allImages={};local allLayers={};local sheet=Image(640,160,ColorMode.RGB)
local manifest={generatorId='void-scrapper-null-dispatcher-v1',name='NULL DISPATCHER',width=160,height=160,pixelsPerUnit=32,pivot={x=0.5,y=0.5},front='up',statePolicy='Four static gameplay poses selected by tags, not a four-frame loop.',layers=layerNames,states={},metalPalette=metalHex,networkPalette=energyHex,corePolicy='Original approved Purple Core Housing/Energy/Details pixels. 1x in sealed/active, exact 2x nearest-neighbor in unbound/critical. No core redraw or recoloring.'}
local s=Sprite(160,160,ColorMode.RGB)
for i,name in ipairs(layerNames) do local l=i==1 and s.layers[1] or s:newLayer();l.name=name end
for si,st in ipairs(states) do
 if si>1 then s:newEmptyFrame(si) end
 local core=sourceCores[st.core][st.sourceFrame];local inset=(160-64*st.coreScale)//2
 local ls={scale(network(si),2)}
 for li=1,3 do local im=Image(160,160,ColorMode.RGB);im:drawImage(scale(core[li],st.coreScale),Point(inset,inset));ls[#ls+1]=im end
 ls[#ls+1]=scale(shell(st,si),2);ls[#ls+1]=scale(control(si),2);ls[#ls+1]=scale(recovery(si),2);ls[#ls+1]=scale(phase(si),2);ls[#ls+1]=scale(fragments(si),2);ls[#ls+1]=scale(corruption(si),2);ls[#ls+1]=scale(damage(si),2)
 for li,im in ipairs(ls) do s:newCel(s.layers[li],si,im,Point(0,0)) end
 s.frames[si].duration=1
 local flat=flatten(ls,160);local png='NULL_Dispatcher_'..st.id..'.png';flat:saveAs(outputDir..'/'..png);sheet:drawImage(flat,Point((si-1)*160,0))
 allImages[si]=flat;allLayers[si]=ls
 manifest.states[#manifest.states+1]={id=st.id,tag=st.tag,label=st.label,frame=si,png=png,description=st.desc,coreSource='core_purple_corrupted_'..st.core..'.aseprite',coreSourceFrame=st.sourceFrame,coreScale=st.coreScale,coreOffset={x=inset,y=inset},sourceSize={w=160,h=160},durationMs=1000}
end
-- Add tags after all frames exist: inserting frames can otherwise extend old tags.
for si,st in ipairs(states) do local tag=s:newTag(si,si);tag.name=st.tag end
local palColors={};local palSeen={}
for _,im in ipairs(allImages) do for it in im:pixels() do local p=it();if pc.rgbaA(p)>0 and not palSeen[p] then palSeen[p]=true;palColors[#palColors+1]=p end end end
table.sort(palColors);local pal=Palette(#palColors+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,p in ipairs(palColors) do pal:setColor(i,Color{r=pc.rgbaR(p),g=pc.rgbaG(p),b=pc.rgbaB(p),a=255}) end;s:setPalette(pal)
s:saveAs(outputDir..'/NULL_Dispatcher.aseprite');s:close();sheet:saveAs(outputDir..'/NULL_Dispatcher_States.png')
manifest.aseprite='NULL_Dispatcher.aseprite';manifest.sheet='NULL_Dispatcher_States.png';manifest.visiblePaletteCount=#palColors
local meta={frames={},meta={app='Aseprite CLI + Lua',image=manifest.sheet,format='RGBA8888',size={w=640,h=160},frameTags={}},unity={pixelsPerUnit=32,pivot=manifest.pivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',loop=false}}
for i,st in ipairs(states) do meta.frames[#meta.frames+1]={filename=st.id,frame={x=(i-1)*160,y=0,w=160,h=160},sourceSize={w=160,h=160},spriteSourceSize={x=0,y=0,w=160,h=160},trimmed=false,rotated=false,duration=1000};meta.meta.frameTags[#meta.meta.frameTags+1]={name=st.tag,from=i-1,to=i-1,direction='forward'} end
saveJson(outputDir..'/NULL_Dispatcher.json',meta)
local bg=rgba('111820');local tile1=rgba('202B36');local tile2=rgba('24313D')
local function checker(im,x,y,w,hg) for yy=y,y+hg-1 do for xx=x,x+w-1 do put(im,xx,yy,((xx-x)//8+(yy-y)//8)%2==0 and tile1 or tile2) end end end
local function text(im,str,x,y,col,n)
 for i=1,#str do local ch=str:sub(i,i);local xx=x+(i-1)*4*n
  if ch=='7' then for yy,row in ipairs({'111','001','010','010','010'}) do for gx=1,3 do if row:sub(gx,gx)=='1' then rect(im,xx+(gx-1)*n,y+(yy-1)*n,n,n,col) end end end
  else h.text(im,ch,xx,y,col,n) end
 end
end
-- Contact sheet: exact 2x views, plus unscaled comparisons and provenance.
local preview=Image(1400,760,ColorMode.RGB);rect(preview,0,0,1400,760,bg)
text(preview,'NULL DISPATCHER',24,20,m[6],3);text(preview,'FINAL AUTHORITY / CORRUPTED SYSTEM NETWORK',24,48,e[4],1)
for i,st in ipairs(states) do local x=20+(i-1)*345;checker(preview,x,80,320,320);preview:drawImage(scale(allImages[i],2),Point(x,80));text(preview,st.label,x,418,m[6],1);preview:drawImage(allImages[i],Point(x+80,448));text(preview,'160 X 160 / NATIVE',x+72,624,m[4],1) end
line(preview,20,651,1379,651,m[3]);text(preview,'APPROVED PURPLE CORE / UNCHANGED PIXELS',24,677,m[5],1)
local approvedFlat=flatten(sourceCores.active[1],64);preview:drawImage(approvedFlat,Point(200,690))
text(preview,'CONTROL',400,677,m[5],1);text(preview,'REGISTER CROWN',400,692,e[3],1)
text(preview,'RECOVERY',624,677,m[5],1);text(preview,'COMPRESSION THROAT',624,692,e[3],1)
text(preview,'PHASE',884,677,m[5],1);text(preview,'ROUTING CLEVIS',884,692,e[3],1)
text(preview,'FOUR SELECTABLE STATES',1112,677,m[4],1);text(preview,'NO TWEEN / NO BLUR',1112,692,m[4],1)
preview:saveAs(outputDir..'/NULL_Dispatcher_Comparison.png')
-- Native gameplay-scale proof: one boss at a time; no downscaled four-up cheats.
local native=Sprite(480,270,ColorMode.RGB);native.layers[1].name='Native Review'
for i,st in ipairs(states) do
 if i>1 then native:newEmptyFrame(i) end
 local im=Image(480,270,ColorMode.RGB);rect(im,0,0,480,270,bg)
 -- Sparse context marks and an unscaled 32px player square make scale legible.
 for _,q in ipairs({{24,53},{437,37},{87,126},{389,215},{107,227},{428,145}}) do rect(im,q[1],q[2],2,2,m[2]) end
 text(im,'NULL DISPATCHER',16,12,m[6],2);text(im,st.label,16,32,e[4],1)
 im:drawImage(allImages[i],Point(192,52))
 oct(im,47,149,32,32,5,m[1]);oct(im,51,153,24,24,3,m[5]);rect(im,57,159,12,12,m[2]);rect(im,60,162,6,6,m[6]);text(im,'32 PX PLAYER',39,192,m[4],1)
 text(im,'480 X 270 / PPU 32 / NATIVE PIXELS',16,248,m[4],1)
 native:newCel(native.layers[1],i,im,Point(0,0));native.frames[i].duration=1.5
 im:saveAs(outputDir..'/NULL_Dispatcher_480x270_'..st.id..'.png')
end
native:saveAs(outputDir..'/NULL_Dispatcher_480x270.gif');native:close()
manifest.comparison='NULL_Dispatcher_Comparison.png';manifest.nativePreview='NULL_Dispatcher_480x270.gif';manifest.previewPolicy='Opaque review images; each native preview shows the boss at exactly 160x160 within 480x270. Player scale marker is review-only.'
saveJson(outputDir..'/manifest.json',manifest)
local f=assert(io.open(outputDir..'/generation_complete.txt','w'));f:write('NULL DISPATCHER / 4 states / 160x160 / copied approved purple core\n');f:close()
