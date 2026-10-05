-- PHASE GATEKEEPER: original Region C lens/spear geometry, on an exact 2x grid.
-- Generic pixel primitives are shared; no geometry from the Region A/B bosses.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,pairRect,pairLine,pairPoly,oct,diamond,mirror,scale,text=h.put,h.rect,h.line,h.poly,h.pairRect,h.pairLine,h.pairPoly,h.oct,h.diamond,h.mirror,h.scale,h.text
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local energyHex={'163454','285E91','388ED1','68C7ED','ABE8F4','E8FAFF'}
local m,e={},{};for i,v in ipairs(metalHex) do m[i]=rgba(v) end;for i,v in ipairs(energyHex) do e[i]=rgba(v) end
local support=assert(Image{fromFile=sourceDir..'/system_support_blue.png'})
local navigation=assert(Image{fromFile=sourceDir..'/system_blue_navigation_node.png'})
local core=assert(Image{fromFile=sourceDir..'/core_blue_active.png'})
assert(support.width==64 and support.height==64 and navigation.width==96 and navigation.height==96 and core.width==256 and core.height==64,'Approved blue dimensions changed')
local referenceColors={};for _,im in ipairs({support,navigation}) do for it in im:pixels() do if pc.rgbaA(it())>0 then referenceColors[it()]=true end end end
for _,i in ipairs({1,2,3,4,5,6,7}) do assert(referenceColors[m[i]],'Approved neutral palette changed') end
for _,i in ipairs({1,3,6}) do assert(referenceColors[e[i]],'Approved blue palette changed') end
local function ellipse(im,cx,cy,rx,ry,p)
 for y=math.floor(cy-ry),math.ceil(cy+ry) do for x=math.floor(cx-rx),math.ceil(cx+rx) do
  if ((x-cx)/rx)^2+((y-cy)/ry)^2<=1 then put(im,x,y,p) end
 end end
end
local layerNames={'Main Body','Forward Needle and Tail','Central Lens','Left Phase Emitter','Right Phase Emitter','Lens Vane L','Lens Vane R','Routing Energy','SYSTEM Registration','Phase Lock VFX'}
local states={{id='idle',label='IDLE',description='Protected navigation lens, narrow guidance needle and two crescent emitters at standby.'},
 {id='phase_lock',label='PHASE LOCK',description='External emitter arcs and routing signals are locked; protective lens vanes remain closed.'},
 {id='lens_exposed',label='LENS EXPOSED',description='Both lens vanes retract laterally on visible guides, revealing a broad oval optic; external phase arcs and emitters dim.'}}
local function body(im)
 -- A long light spine encloses a vertically elongated optical cradle.
 poly(im,{{30,9},{33,9},{36,19},{40,23},{43,30},{43,36},{40,43},{36,47},{34,56},{29,56},{27,47},{23,43},{20,36},{20,30},{23,23},{27,19}},m[1])
 poly(im,{{30,12},{33,12},{35,20},{39,24},{41,30},{41,36},{38,42},{34,46},{33,54},{30,54},{29,46},{25,42},{22,36},{22,30},{25,24},{28,20}},m[3])
 ellipse(im,31.5,32.5,10.5,13.5,m[1]);ellipse(im,31.5,32.5,9.5,12.5,m[4]);ellipse(im,31.5,32.5,8.5,11.5,m[2])
 -- Thin sliding guides link two small emitters and the movable optical guards.
 pairRect(im,18,31,7,4,m[1]);pairRect(im,19,32,6,2,m[3]);pairRect(im,20,32,4,1,m[5])
 pairRect(im,20,28,4,2,m[1]);pairRect(im,20,38,4,2,m[1])
 pairRect(im,21,28,3,1,m[4]);pairRect(im,21,38,3,1,m[4])
end
local function needleAndTail(im)
 -- An uninterrupted forward needle, not a gun battery or a separate head pod.
 poly(im,{{31,2},{32,2},{34,9},{35,13},{37,18},{36,23},{33,25},{30,25},{27,23},{26,18},{28,13},{29,9}},m[1])
 poly(im,{{31,4},{32,4},{33,11},{35,18},{34,22},{32,23},{31,23},{29,22},{28,18},{30,11}},m[6])
 pairLine(im,30,11,28,18,m[7]);pairLine(im,28,18,29,21,m[4]);pairRect(im,29,20,1,2,m[4])
 rect(im,31,10,2,10,m[2]);pairLine(im,29,17,30,15,m[3]);rect(im,30,21,4,1,m[3])
 -- The aft routing keel tapers into a flat-capped fork, marking the rear.
 poly(im,{{27,42},{36,42},{38,46},{35,52},{34,58},{29,58},{28,52},{25,46}},m[1])
 poly(im,{{28,43},{35,43},{36,46},{33,52},{33,56},{30,56},{30,52},{27,46}},m[5])
 pairLine(im,28,44,27,46,m[7]);pairLine(im,27,46,30,51,m[4])
 rect(im,30,45,4,8,m[2]);rect(im,31,46,2,6,m[3])
 pairRect(im,28,54,2,7,m[1]);pairRect(im,28,55,1,5,m[5]);pairRect(im,28,59,2,1,m[4])
 rect(im,30,55,4,2,m[3]);rect(im,31,56,2,2,m[1])
end
local function lens(im,state)
 -- Oval lens and crosshair distinguish navigation hardware from a reactor box.
 ellipse(im,31.5,32.5,8.5,10.5,m[1]);ellipse(im,31.5,32.5,7.5,9.5,e[1])
 ellipse(im,31.5,32.5,6.5,8.5,e[2]);ellipse(im,31.5,32.5,5.5,7.5,e[3])
 if state=='lens_exposed' then
  -- Large hard-edged exposed optic, with a single flat highlight and reticle.
  ellipse(im,31.5,32.5,6.5,8.5,e[3]);ellipse(im,31.5,32.5,4.5,6.5,e[4])
  pairLine(im,27,27,29,25,e[5]);pairLine(im,26,29,26,31,e[5])
  pairLine(im,27,38,29,40,e[2]);rect(im,31,25,2,3,e[2]);rect(im,31,38,2,3,e[2])
  pairRect(im,25,32,3,2,e[2]);diamond(im,31.5,32.5,4,e[1]);diamond(im,31.5,32.5,3,e[5]);diamond(im,31.5,32.5,1,e[6])
 else
  -- The approved diamond module remains the small focusing optic within the lens.
  for y=10,21 do for x=10,21 do if math.abs(x-15.5)+math.abs(y-15.5)<=6 then
   put(im,26+x-10,27+y-10,support:getPixel(x*2,y*2))
  end end end
  rect(im,31,24,2,2,e[2]);rect(im,31,39,2,2,e[2]);pairRect(im,25,32,2,2,e[2])
 end
end
local function emitter(im,state)
 local charged=state=='phase_lock';local exposed=state=='lens_exposed'
 -- A slim crescent antenna, not a detached heavy weapon housing.
 poly(im,{{18,23},{20,25},{18,29},{17,32},{17,35},{19,39},{19,43},{17,43},{14,37},{13,34},{14,28},{16,25}},m[1])
 poly(im,{{18,25},{18,27},{16,30},{15,34},{16,38},{18,41},{18,42},{15,37},{14,34},{15,29}},m[6])
 line(im,16,27,15,30,m[7]);line(im,14,33,14,35,m[7]);line(im,16,38,18,41,m[4])
 poly(im,{{18,28},{19,29},{18,32},{18,35},{20,39},{19,40},{17,36},{16,33}},m[2])
 line(im,18,29,17,33,exposed and m[3] or charged and e[5] or e[2])
 line(im,17,34,19,38,exposed and m[3] or charged and e[4] or e[1])
 rect(im,14,32,2,3,m[2]);rect(im,14,33,2,1,charged and e[6] or exposed and m[3] or e[3])
 put(im,18,25,charged and e[4] or m[4]);put(im,18,41,charged and e[4] or m[4])
end
local function vane(im,state)
 local d=state=='lens_exposed' and 3 or 0
 -- Left and right protective vanes move as whole mechanical pieces on guides.
 poly(im,{{27-d,22},{30-d,24},{29-d,27},{27-d,30},{27-d,35},{29-d,39},{30-d,41},{27-d,44},{23-d,39},{21-d,34},{22-d,28}},m[1])
 poly(im,{{27-d,24},{28-d,25},{26-d,29},{25-d,33},{26-d,37},{28-d,41},{27-d,42},{24-d,38},{23-d,34},{24-d,29}},m[6])
 line(im,27-d,24,24-d,29,m[7]);line(im,24-d,29,23-d,33,m[7]);line(im,24-d,37,27-d,41,m[4])
 poly(im,{{24-d,30},{25-d,29},{24-d,33},{25-d,37},{24-d,36},{23-d,33}},m[5])
 rect(im,24-d,32,1,3,m[3]);put(im,26-d,26,m[4]);put(im,26-d,40,m[3])
end
local function routing(im,state)
 local locked=state=='phase_lock';local exposed=state=='lens_exposed';local c=exposed and e[1] or locked and e[4] or e[2]
 rect(im,31,12,2,6,c);rect(im,31,19,2,1,locked and e[6] or e[3])
 rect(im,31,47,2,4,c);rect(im,31,54,2,1,c)
 pairLine(im,19,33,21,33,c)
 -- Tick-like positioning signals, kept sparse and aligned to the long axis.
 if locked then
  rect(im,31,13,2,2,e[5]);rect(im,31,47,2,2,e[5])
  pairRect(im,29,8,1,2,e[3]);pairRect(im,29,51,1,2,e[3])
 end
end
local function marks(im,state)
 pairRect(im,29,18,1,2,m[3]);pairRect(im,29,45,1,2,m[3]);pairRect(im,28,57,1,1,m[4])
 -- The two plates carry their own markings on their movable layers.
end
local function effects(im,state)
 if state~='phase_lock' then return end
 -- Stable portal brackets sit OUTSIDE the body. No soft glow or gradient.
 pairLine(im,14,23,11,28,e[3]);pairLine(im,11,28,10,31,e[3]);pairLine(im,10,31,10,36,e[4]);pairLine(im,10,36,12,40,e[3]);pairLine(im,12,40,14,43,e[3])
 pairLine(im,12,28,11,31,e[5]);pairLine(im,11,32,11,35,e[6]);pairLine(im,11,36,13,40,e[5])
 pairRect(im,13,22,3,1,e[4]);pairRect(im,13,44,3,1,e[4])
 pairRect(im,9,32,1,3,e[3]);pairRect(im,12,33,1,1,e[6])
end
local flats,allLayers={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 body(l[1]);needleAndTail(l[2]);lens(l[3],state.id);emitter(l[4],state.id);l[5]=mirror(l[4])
 vane(l[6],state.id);l[7]=mirror(l[6]);routing(l[8],state.id);marks(l[9],state.id);effects(l[10],state.id)
 for _,index in ipairs({1,2,3,8,9,10}) do for y=0,63 do for x=0,31 do put(l[index],63-x,y,l[index]:getPixel(x,y)) end end end
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=layerNames[i];local doubled=scale(im,2);allLayers[state.id][i]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2;local stem='phase_gatekeeper_'..state.id;flats[state.id]=Image(s)
 s:saveAs(outputDir..'/'..stem..'.aseprite');flats[state.id]:saveAs(outputDir..'/'..stem..'.png');s:close()
end
local master=Sprite(128,128,ColorMode.RGB)
for i,name in ipairs(layerNames) do local layer=i==1 and master.layers[1] or master:newLayer();layer.name=name end
for frame,state in ipairs(states) do
 if frame>1 then master:newEmptyFrame(frame) end
 for i,im in ipairs(allLayers[state.id]) do master:newCel(master.layers[i],frame,im,Point(0,0)) end
 master.frames[frame].duration=0.2
end
for frame,state in ipairs(states) do local tag=master:newTag(frame,frame);tag.name=state.id end
master:saveAs(outputDir..'/phase_gatekeeper_states.aseprite');master:close()
local strip=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flats[state.id],Point((i-1)*128,0)) end;strip:saveAs(outputDir..'/phase_gatekeeper_states.png')
local preview=Image(1000,792,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'PHASE GATEKEEPER',32,24,m[6],4);text(preview,'SYSTEM / REGION C',34,57,e[4],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,96+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flats[state.id],2),Point(x+20,108));text(preview,state.label,x+math.floor((296-#state.label*8)/2),392,e[5],2)
 preview:drawImage(flats[state.id],Point(x+84,417));text(preview,'128 X 128',x+112,550,m[4],2)
end
rect(preview,28,580,944,1,m[3]);text(preview,'FAMILY AND SILHOUETTE REFERENCES',32,602,m[4],2)
preview:drawImage(support,Point(60,663));text(preview,'PRECISION',56,766,m[5],2)
preview:drawImage(navigation,Point(231,647));text(preview,'NAVIGATION',239,766,m[5],2)
local regionA=assert(Image{fromFile=sourceDir..'/sector_administrator_idle.png'})
local regionB=assert(Image{fromFile=sourceDir..'/defense_overseer_idle.png'})
preview:drawImage(regionA,Point(485,628));text(preview,'REGION A',517,766,m[4],2)
preview:drawImage(regionB,Point(785,628));text(preview,'REGION B',817,766,m[4],2)
preview:saveAs(outputDir..'/phase_gatekeeper_comparison.png')
if app.params.qaDir then
 local qa=Image(384,192,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,96,384,96,m[6])
 for i,state in ipairs(states) do
  local small=Image(64,64,ColorMode.RGB);for y=0,63 do for x=0,63 do put(small,x,y,flats[state.id]:getPixel(x*2,y*2)) end end
  qa:drawImage(small,Point(32+(i-1)*128,20));qa:drawImage(small,Point(32+(i-1)*128,116))
  text(qa,state.label,(i-1)*128+math.floor((128-#state.label*4)/2),6,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_64px.png')
end
local manifest={generatorId='void-scrapper-phase-gatekeeper-v1',name='PHASE GATEKEEPER',faction='SYSTEM',region='C',family='blue',width=128,height=128,pixelScale=2,front='up / negative Y',anchor={x=64,y=64},lensCenter={x=63.5,y=65.5},palette={metal=metalHex,energy=energyHex},layers=layerNames,assets={},
 referencePolicy='New narrow navigation needle and oval-lens body with two crescent emitters. Approved blue units define the palette and optical/routing motif. Region A/B images are comparison-only; no boss geometry or pixels are reused.',
 animationLayers={lens='Central Lens',emitters={'Left Phase Emitter','Right Phase Emitter'},guards={'Lens Vane L','Lens Vane R'},routing='Routing Energy',vfx='Phase Lock VFX'},guardRetractionPixels=6,
 master='phase_gatekeeper_states.aseprite',sheet='phase_gatekeeper_states.png',sheetLayout='Three horizontal 128x128 cells: idle, phase_lock, lens_exposed; no padding or trimming.',stateTimeline='Three tagged state poses, not a finished looping animation; 200ms per pose.',preview='phase_gatekeeper_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='phase_gatekeeper_'..state.id..'.aseprite',png='phase_gatekeeper_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('PHASE GATEKEEPER: three poses, layered sources, transparent strip and comparison.\n');done:close()
print('PHASE_GATEKEEPER_GENERATED')
