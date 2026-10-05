-- Civilian modular trading station. ReferenceOnly pixels are never resampled.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,scale,text=h.put,h.rect,h.line,h.poly,h.scale,h.text
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'171E25','303941','515F67','78888C','ADB9B7','D6DEDA'}
local tealHex={'264C52','508C91','9FCBC8'}
local shieldHex={'64CDD9','C2F2EF'}
local serviceHex={'A77539','E3B260'}
local alertHex={'823C44','E65C60'}
local function palette(values) local p={};for i,v in ipairs(values) do p[i]=rgba(v) end;return p end
local m,t,c,a,r=palette(metalHex),palette(tealHex),palette(shieldHex),palette(serviceHex),palette(alertHex)
local references={}
for _,spec in ipairs({{'station_1_reference',146,214},{'station_2_reference',318,318},{'system_green_control_node',64,64},{'raider_salvage_carrier_idle',128,128}}) do
 local im=assert(Image{fromFile=sourceDir..'/'..spec[1]..'.png'});assert(im.width==spec[2] and im.height==spec[3],'Reference dimensions changed');references[spec[1]]=im
end
local layerNames={'Main Hull','Exposed Services','Docking and Trade Module','Left Repair Utility','Right Cargo Module','Station Cladding','Shield Emitters','Antenna and Comms','Utility and Warning Lights','Shield Field','Damage and Debris'}
local states={
 {id='neutral',label='NEUTRAL',description='Open lower docking entrance, trade counter, repair workshop and freight storage; muted teal and amber lights, no red.'},
 {id='shield_active',label='SHIELD ACTIVE',description='Four corner emitters energize a segmented cyan perimeter field without filling or obscuring the station.'},
 {id='hostile_shield_broken',label='HOSTILE / BROKEN',description='A broken emitter and lost starboard cladding expose wiring; red alarms engage and a striped safety gate blocks docking.'}
}
local function hull(im)
 -- C-shaped orbital dock. The visitor enters from the lower side of the canvas.
 poly(im,{{22,12},{42,12},{51,22},{52,39},{43,52},{37,55},{27,55},{20,52},{12,40},{12,24}},m[1])
 poly(im,{{23,14},{41,14},{49,23},{50,38},{42,50},{37,53},{27,53},{22,50},{14,39},{14,25}},m[3])
 poly(im,{{25,24},{38,24},{43,29},{43,38},{36,46},{27,46},{21,38},{21,30}},0)
 rect(im,27,44,10,13,0)
 -- Four box-section outriggers join civilian service modules to the ring.
 rect(im,8,27,10,4,m[1]);rect(im,9,28,8,2,m[4]);rect(im,9,36,9,4,m[1]);rect(im,10,37,7,1,m[4])
 rect(im,47,26,10,4,m[1]);rect(im,48,27,8,2,m[4]);rect(im,47,38,10,4,m[1]);rect(im,48,39,8,1,m[4])
 -- Short docking shoes, not engines or aft weapon mounts.
 rect(im,23,49,4,6,m[1]);rect(im,24,50,2,4,m[5]);rect(im,37,49,4,6,m[1]);rect(im,38,50,2,4,m[5])
end
local function services(im)
 -- Service conduits below the cladding are functional and tidy, not faction armor.
 line(im,19,25,16,29,m[1]);line(im,16,29,16,39,m[1]);line(im,16,39,22,46,m[1])
 line(im,20,25,17,29,t[1]);line(im,17,29,17,39,t[1]);line(im,17,39,22,45,t[2])
 rect(im,44,25,5,14,m[1]);rect(im,45,26,3,12,m[2])
 line(im,46,26,46,31,m[5]);line(im,46,31,44,34,m[4]);line(im,44,34,47,37,t[1])
 for y=27,36,3 do rect(im,47,y,2,1,m[4]) end
 rect(im,41,41,6,5,m[2]);rect(im,42,42,4,2,m[4]);rect(im,43,42,2,2,m[1])
 rect(im,21,41,3,6,m[2]);line(im,22,42,23,45,m[4])
end
local function trade(im,state)
 local hostile=state=='hostile_shield_broken'
 -- Flat-roofed reception office across the top of the dock, with a civilian sign.
 poly(im,{{21,13},{42,13},{46,17},{45,24},{40,27},{24,27},{18,23},{18,17}},m[1])
 poly(im,{{22,14},{41,14},{44,17},{43,23},{40,25},{25,25},{20,22},{20,17}},m[5])
 line(im,22,14,41,14,m[6]);line(im,20,17,20,21,m[6]);rect(im,24,15,16,7,m[1]);text(im,'SHOP',25,16,t[3],1)
 rect(im,23,23,18,2,m[2]);rect(im,24,23,16,1,t[2]);rect(im,25,26,14,2,m[1]);rect(im,27,26,10,1,m[4])
 put(im,22,22,m[6]);put(im,42,22,m[6]);rect(im,21,18,1,3,m[3]);rect(im,42,18,1,3,m[3])
 -- Recessed landing/service platform: a dark deck, never an energy-core centerpiece.
 poly(im,{{26,28},{38,28},{40,31},{40,38},{36,43},{28,43},{24,38},{24,31}},m[1])
 poly(im,{{27,29},{37,29},{39,32},{39,37},{35,41},{29,41},{25,37},{25,32}},m[2])
 line(im,27,30,37,30,m[4]);line(im,26,32,26,36,m[3]);line(im,38,32,38,36,m[3]);line(im,29,40,35,40,m[4])
 -- Square landing registration with a service arrow pointing toward the counter.
 line(im,29,33,29,36,m[5]);line(im,29,33,30,33,m[5]);line(im,35,33,35,36,m[5]);line(im,34,33,35,33,m[5])
 line(im,31,36,32,35,t[2]);line(im,32,35,33,36,t[2]);put(im,32,37,t[2]);put(im,32,38,t[1])
 rect(im,24,40,3,7,m[1]);rect(im,25,41,1,5,m[4]);rect(im,37,40,3,7,m[1]);rect(im,38,41,1,5,m[4])
 if hostile then
  -- Dock lockdown remains a safety fixture, not an added weapon battery.
  rect(im,25,38,14,4,m[1]);rect(im,26,39,12,2,m[4]);rect(im,26,39,12,1,r[1])
  for x=27,35,4 do line(im,x,39,x+1,40,a[2]) end
  rect(im,23,23,18,2,m[2]);rect(im,24,23,3,1,r[2]);rect(im,37,23,3,1,r[2])
 end
end
local function repair(im,state)
 local hostile=state=='hostile_shield_broken'
 -- Rectangular workshop and short manipulator, without forward-facing barrels.
 poly(im,{{7,24},{12,23},{16,26},{16,39},{13,42},{6,40},{4,36},{4,28}},m[1])
 poly(im,{{7,25},{11,25},{14,27},{14,38},{12,40},{7,38},{6,35},{6,28}},m[5])
 line(im,7,25,11,25,m[6]);line(im,6,28,6,34,m[6]);rect(im,7,28,6,7,m[2]);rect(im,8,29,4,5,t[1])
 -- Wrench stencil on the workshop roof.
 put(im,8,29,m[6]);put(im,11,29,m[6]);rect(im,8,30,4,1,m[6]);rect(im,9,31,2,3,m[6]);put(im,10,34,m[4])
 rect(im,7,36,6,2,m[3]);rect(im,8,36,4,1,m[1]);rect(im,8,38,3,1,a[1]);put(im,8,38,a[2])
 -- Compact folded repair arm over the lower-left service apron.
 poly(im,{{13,40},{16,39},{20,43},{22,46},{20,48},{17,44},{14,44}},m[1])
 line(im,15,41,18,43,m[4]);line(im,18,43,20,46,m[5]);put(im,15,41,m[6]);rect(im,14,41,2,2,m[2])
 line(im,20,46,19,48,m[3]);line(im,22,46,22,48,m[3]);put(im,19,48,m[5]);put(im,22,48,m[5])
 rect(im,11,21,3,4,m[1]);rect(im,12,22,1,2,m[4]);put(im,12,21,t[2])
 if hostile then put(im,12,21,r[2]);line(im,10,35,12,37,m[1]);put(im,12,37,m[4]) end
end
local function cargo(im,state)
 -- Two different freight crates and a cylindrical coolant/repair tank.
 rect(im,48,25,11,19,m[1]);rect(im,49,26,9,17,m[3]);rect(im,49,26,1,16,m[5])
 rect(im,51,25,8,9,m[1]);rect(im,52,26,6,7,m[5]);rect(im,52,26,6,1,m[6]);rect(im,53,27,1,5,m[3]);rect(im,56,27,1,5,m[3]);rect(im,54,28,2,2,a[1]);put(im,54,28,a[2])
 rect(im,50,35,9,8,m[1]);rect(im,51,36,7,6,m[4]);rect(im,51,36,7,1,m[5]);rect(im,51,38,7,1,t[1]);rect(im,54,36,1,6,m[2]);put(im,55,40,m[6])
 poly(im,{{48,18},{52,18},{54,20},{54,23},{52,25},{48,25},{46,23},{46,20}},m[1])
 poly(im,{{48,19},{51,19},{52,21},{52,22},{51,24},{48,24},{47,22},{47,21}},m[5]);line(im,48,19,51,19,m[6]);rect(im,47,21,6,1,m[3]);put(im,50,23,t[2])
 rect(im,53,43,3,3,m[1]);rect(im,54,44,1,1,m[4]);rect(im,49,44,3,2,m[1]);put(im,50,44,a[1])
 if state=='hostile_shield_broken' then line(im,56,35,55,37,m[1]);line(im,55,37,57,39,m[1]);put(im,57,39,m[6]) end
end
local function cladding(im,state)
 local hostile=state=='hostile_shield_broken'
 -- Segmented civilian maintenance walkways wrap the dock. No pointed armor wings.
 poly(im,{{17,22},{21,25},{19,30},{19,37},{21,41},{18,43},{14,38},{14,29}},m[1])
 poly(im,{{17,24},{19,26},{17,30},{17,37},{19,40},{18,41},{15,37},{15,29}},m[5])
 line(im,15,29,15,36,m[6]);rect(im,16,30,1,6,t[2]);put(im,18,26,m[6]);put(im,18,39,m[4])
 if not hostile then
  poly(im,{{45,24},{49,27},{49,37},{46,41},{43,40},{45,35},{44,29}},m[1])
  poly(im,{{46,26},{47,28},{47,36},{45,39},{44,39},{46,34},{45,29}},m[5]);line(im,46,26,47,28,m[6]);rect(im,47,29,1,6,t[2]);put(im,45,38,m[4])
 else
  poly(im,{{45,24},{49,27},{48,30},{46,29},{45,27}},m[4]);line(im,45,24,48,27,m[6]);put(im,47,29,m[1])
  poly(im,{{47,37},{46,41},{43,40},{44,38}},m[3]);line(im,44,38,44,40,m[5])
 end
 poly(im,{{20,43},{23,42},{27,47},{26,53},{22,52},{19,48}},m[1])
 poly(im,{{22,44},{23,44},{25,48},{25,51},{23,50},{21,47}},m[5]);line(im,22,44,24,47,m[6]);put(im,24,50,m[4])
 poly(im,{{40,42},{44,43},{46,47},{42,52},{38,53},{37,47}},m[1])
 poly(im,{{40,44},{42,44},{44,47},{41,50},{39,51},{39,47}},m[5]);line(im,40,44,42,44,m[6]);line(im,39,47,39,50,m[6]);put(im,42,48,m[4])
 -- Short industrial expansion joints, regular rather than patchwork repairs.
 line(im,18,37,19,39,m[2]);line(im,22,46,24,48,m[3]);line(im,40,47,42,46,m[3])
end
local function emitters(im,state)
 local active=state=='shield_active';local hostile=state=='hostile_shield_broken'
 for i,q in ipairs({{16,18},{44,15},{16,45},{45,44}}) do
  local x,y=q[1],q[2]
  poly(im,{{x+1,y},{x+4,y},{x+5,y+1},{x+5,y+4},{x+4,y+5},{x+1,y+5},{x,y+4},{x,y+1}},m[1])
  rect(im,x+1,y+1,4,4,m[4]);rect(im,x+1,y+1,3,1,m[6]);rect(im,x+2,y+2,2,2,active and c[1] or t[1]);put(im,x+2,y+2,active and c[2] or t[2])
  if active then put(im,x+1,y+3,c[1]);put(im,x+4,y+3,c[1]) end
  if hostile then
   if i==2 then
    rect(im,x+2,y,4,5,0);rect(im,x+1,y+2,2,3,m[1]);put(im,x+1,y+2,m[5]);put(im,x+3,y+5,m[4]);put(im,x+2,y+4,r[1])
   elseif i==4 then
    rect(im,x+3,y+1,3,4,0);put(im,x+2,y+2,m[1]);put(im,x+2,y+4,r[2]);put(im,x+3,y+5,m[3])
   else put(im,x+2,y+2,r[2]);put(im,x+3,y+3,r[1]) end
  end
 end
end
local function comms(im,state)
 -- A low transmitter dish and cargo-office rooftop vents, rather than target optics.
 rect(im,38,9,3,6,m[1]);rect(im,39,10,1,4,m[4])
 poly(im,{{36,6},{42,6},{45,9},{43,12},{37,12},{34,9}},m[1])
 poly(im,{{37,7},{41,7},{43,9},{41,11},{37,10},{36,9}},m[5]);line(im,37,7,41,7,m[6]);line(im,37,8,40,10,m[3]);put(im,40,8,t[2])
 rect(im,30,8,1,5,m[1]);rect(im,29,7,3,2,m[1]);put(im,30,7,t[2])
 rect(im,23,9,5,4,m[1]);rect(im,24,10,3,2,m[3]);rect(im,24,10,3,1,m[5])
 if state=='hostile_shield_broken' then put(im,30,7,r[2]);line(im,42,9,41,11,m[1]);put(im,42,10,m[4]) end
end
local function lights(im,state)
 local hostile=state=='hostile_shield_broken';local active=state=='shield_active'
 -- Warm approach lights distinguish the service entrance from a ship's stern.
 for _,q in ipairs({{26,44},{37,44},{26,49},{37,49},{24,52},{39,52}}) do
  rect(im,q[1],q[2],1,2,hostile and r[2] or a[2])
 end
 rect(im,20,20,1,2,hostile and r[2] or t[2]);rect(im,43,20,1,2,hostile and r[2] or t[2])
 rect(im,7,27,4,1,t[2]);rect(im,52,32,5,1,hostile and r[1] or t[2]);put(im,53,32,hostile and r[2] or t[3])
 rect(im,23,11,4,1,t[1]);put(im,24,11,hostile and r[2] or t[2])
 if active then rect(im,28,26,8,1,c[1]);put(im,28,26,c[2]) end
 if hostile then
  -- Broad red alarm strips are confined to the hostile state.
  rect(im,21,14,4,1,r[2]);rect(im,39,14,4,1,r[2]);rect(im,15,31,1,4,r[2]);rect(im,47,30,1,3,r[1])
  rect(im,26,39,12,1,r[1]);rect(im,27,39,2,1,r[2]);rect(im,35,39,2,1,r[2]);put(im,32,39,a[2])
 end
end
local function shield(im,state)
 if state~='shield_active' then return end
 -- Segmented, single-pixel field outlines: transparent interior, no smooth glow.
 for _,points in ipairs({{{5,21},{10,13},{19,6},{27,4}},{{36,4},{44,6},{55,13},{59,20}},{{3,42},{8,50},{17,57},{24,59}},{{40,59},{49,56},{57,48},{61,40}}}) do
  for i=1,#points-1 do line(im,points[i][1],points[i][2],points[i+1][1],points[i+1][2],c[1]) end
 end
 line(im,11,14,18,8,c[2]);line(im,47,9,53,13,c[2]);line(im,9,49,16,54,c[2]);line(im,51,52,56,47,c[2])
 line(im,3,25,2,30,c[1]);line(im,2,33,2,37,c[1]);line(im,61,25,61,29,c[1]);line(im,61,32,61,36,c[1])
 -- Short pulses sit immediately outside the projector housings.
 line(im,14,18,12,16,c[2]);line(im,49,15,51,13,c[2]);line(im,14,48,12,50,c[2]);line(im,50,48,52,50,c[2])
end
local function damage(im,state)
 if state~='hostile_shield_broken' then return end
 line(im,47,16,49,15,c[1]);put(im,50,13,c[2]);put(im,51,16,c[1]);put(im,48,12,m[4])
 line(im,45,28,44,30,a[1]);line(im,44,30,46,33,t[1]);put(im,45,29,m[5]);line(im,47,34,48,36,r[1])
 line(im,48,46,50,47,a[1]);put(im,51,47,a[2]);put(im,52,49,a[2])
 rect(im,49,51,2,2,m[3]);put(im,49,51,m[6]);rect(im,47,55,2,1,m[4]);put(im,51,54,m[3])
 line(im,41,23,40,25,m[1]);put(im,42,24,m[3]);put(im,45,18,m[2])
end
local flats,allLayers={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 hull(l[1]);services(l[2]);trade(l[3],state.id);repair(l[4],state.id);cargo(l[5],state.id);cladding(l[6],state.id)
 emitters(l[7],state.id);comms(l[8],state.id);lights(l[9],state.id);shield(l[10],state.id);damage(l[11],state.id)
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=layerNames[i];local doubled=scale(im,2);allLayers[state.id][i]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2;local stem='salvage_trading_station_'..state.id;flats[state.id]=Image(s)
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
master:saveAs(outputDir..'/salvage_trading_station_states.aseprite');master:close()
local strip=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flats[state.id],Point((i-1)*128,0)) end;strip:saveAs(outputDir..'/salvage_trading_station_states.png')
local preview=Image(1000,728,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'SALVAGE TRADING STATION',32,24,m[6],3);text(preview,'NEUTRAL / TRADE / REPAIR',34,55,t[3],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,92+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flats[state.id],2),Point(x+20,104));text(preview,state.label,x+math.floor((296-#state.label*8)/2),389,i==3 and r[2] or t[3],2)
 preview:drawImage(flats[state.id],Point(x+84,414));text(preview,'128 X 128',x+112,550,m[4],2)
end
rect(preview,28,580,944,1,m[3]);text(preview,'CIVILIAN SERVICE MODULES',32,602,m[5],2)
-- Native-size layer excerpts identify the civilian service functions.
for _,spec in ipairs({
 {layer=3,label='DOCK / TRADE',note='OPEN LOWER APPROACH',crop={36,26,56,68},x=49,tx=127},
 {layer=4,label='REPAIR',note='FOLDED SERVICE ARM',crop={8,42,38,58},x=388,tx=450},
 {layer=5,label='CARGO',note='FREIGHT AND COOLANT',crop={92,36,28,56},x=719,tx=769}}) do
 local q=spec.crop;local sample=Image(q[3],q[4],ColorMode.RGB)
 for y=0,q[4]-1 do for x=0,q[3]-1 do put(sample,x,y,allLayers.neutral[spec.layer]:getPixel(q[1]+x,q[2]+y)) end end
 preview:drawImage(sample,Point(spec.x,626));text(preview,spec.label,spec.tx,642,t[3],2);text(preview,spec.note,spec.tx,665,m[4],1)
end
for i,p in ipairs({m[1],m[2],m[3],m[4],m[5],m[6],t[1],t[2],t[3],a[1],a[2],c[1],c[2],r[1],r[2]}) do rect(preview,762+(i-1)*14,701,10,6,p) end
preview:saveAs(outputDir..'/salvage_trading_station_comparison.png')
if app.params.qaDir then
 local qa=Image(384,192,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,96,384,96,m[6])
 for i,state in ipairs(states) do
  local small=Image(64,64,ColorMode.RGB);for y=0,63 do for x=0,63 do put(small,x,y,flats[state.id]:getPixel(x*2,y*2)) end end
  qa:drawImage(small,Point(32+(i-1)*128,20));qa:drawImage(small,Point(32+(i-1)*128,116));text(qa,state.label,(i-1)*128+math.floor((128-#state.label*4)/2),6,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_64px.png')
end
local manifest={generatorId='void-scrapper-salvage-trading-station-v1',name='SALVAGE TRADING STATION',faction='NEUTRAL / TRADER',role='Mobile salvage-sector shop offering repairs, Traits and Reinforcements',width=128,height=128,pixelScale=2,front='Docking entrance faces down / positive Y',anchor={x=64,y=64},interactionApproach={x=64,y=112},palette={metal=metalHex,utilityTeal=tealHex,shieldCyan=shieldHex,serviceAmber=serviceHex,warningRed=alertHex},layers=layerNames,assets={},
 referencePolicy='Station 1 and Station 2 were inspected for modular and circular docking geometry only. No source pixels, hulls, or downscaled ReferenceOnly art are used. Existing factions were inspected to preserve the world pixel scale and neutral metal range while establishing distinct civilian geometry and utility colors.',
 master='salvage_trading_station_states.aseprite',sheet='salvage_trading_station_states.png',sheetLayout='Three horizontal 128x128 cells: neutral, shield_active, hostile_shield_broken; no padding or trimming.',stateTimeline='Three tagged static state poses, not a finished looping animation; 200ms per pose.',preview='salvage_trading_station_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='salvage_trading_station_'..state.id..'.aseprite',png='salvage_trading_station_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('SALVAGE TRADING STATION: three states, layered sources, transparent strip and comparison.\n');done:close()
print('SALVAGE_TRADING_STATION_GENERATED')
