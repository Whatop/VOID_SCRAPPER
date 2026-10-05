-- RAIDER ASSAULT COMMANDER: salvaged warship geometry, not SYSTEM hardware.
-- Only generic pixel/font primitives are shared with other CLI workflows.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,scale,text=h.put,h.rect,h.line,h.poly,h.scale,h.text
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'171E25','303941','515F67','78888C','ADB9B7','D6DEDA','F0F2E9'}
local redHex={'662C32','B84445','ED7563'}
local heatHex={'6F3A26','E9913C','FFE0A2'}
local m,r,heat={},{},{};for i,v in ipairs(metalHex) do m[i]=rgba(v) end;for i,v in ipairs(redHex) do r[i]=rgba(v) end;for i,v in ipairs(heatHex) do heat[i]=rgba(v) end
local references={}
for _,name in ipairs({'basic','shotgun','sniper_charging','elite'}) do local im=assert(Image{fromFile=sourceDir..'/raider_'..name..'.png'});assert(im.width==64 and im.height==64,'Approved Raider dimensions changed');references[name]=im end
local referenceColors={};for _,im in pairs(references) do for it in im:pixels() do if pc.rgbaA(it())>0 then referenceColors[it()]=true end end end
for _,palette in ipairs({m,r,heat}) do for _,p in ipairs(palette) do assert(referenceColors[p],'Color not present in approved Raider family') end end
local layerNames={'Main Hull','Exposed Machinery and Cables','Left Rotary Weapon','Right Twin Breacher','Engine Assemblies','Salvaged Armor Plates','Cockpit and Command Mast','Red Faction Paint','Weapon Heat and Engine Plumes','Critical Damage VFX'}
local states={{id='idle',label='IDLE',description='Mismatched heavy mounts, repaired plate hull, red faction paint and low engine output.'},
 {id='weapons_hot',label='WEAPONS HOT',description='Extended exposed barrels, open feed shutters, red firing indicators and active hot engine plumes.'},
 {id='critical_damage',label='CRITICAL DAMAGE',description='Central and port plating lost, one engine disabled, a shortened damaged gun, exposed machinery and local sparks.'}}
local function hull(im,state)
 -- A tall, off-center bifurcated ram makes this a forward assault warship.
 poly(im,{{22,2},{28,2},{30,6},{34,3},{38,6},{40,15},{37,21},{25,21},{20,14},{19,9}},m[1])
 poly(im,{{23,4},{27,4},{29,9},{34,5},{36,7},{38,15},{35,19},{26,19},{22,13},{21,9}},m[3])
 -- Irregular swept shoulders and a long keel, not a flat symmetric defense deck.
 poly(im,{{25,12},{35,11},{40,16},{43,21},{52,23},{60,28},{60,37},{56,43},{46,46},{42,52},{36,55},{23,54},{18,49},{16,44},{7,41},{3,35},{4,28},{14,23},{20,22}},m[1])
 poly(im,{{25,14},{34,13},{38,17},{41,23},{51,25},{58,29},{58,36},{54,41},{44,44},{40,50},{35,53},{24,52},{20,47},{18,42},{8,39},{5,34},{6,29},{15,25},{22,24}},m[3])
 poly(im,{{26,17},{34,16},{39,23},{39,45},{35,50},{25,49},{22,43},{22,26}},m[2])
 -- Rough overlapping crossbraces carry the weapon masses into the hull.
 poly(im,{{13,27},{19,27},{28,32},{26,36},{17,32},{13,33}},m[1]);line(im,15,29,25,34,m[4]);line(im,15,30,24,35,m[2])
 poly(im,{{38,28},{45,24},{52,27},{51,32},{44,30},{40,33}},m[1]);line(im,40,30,46,27,m[4]);line(im,46,27,50,29,m[5])
 rect(im,24,45,15,6,m[1]);rect(im,25,46,13,4,m[3]);rect(im,26,49,10,1,m[2])
 -- No sector-core socket or circular/diamond energy centerpiece is constructed.
end
local function machinery(im,state)
 -- Offset gearbox, hydraulic rods and external cable runs remain under the armor.
 rect(im,25,28,14,17,m[1]);rect(im,26,29,5,14,m[3]);rect(im,33,29,5,13,m[2])
 rect(im,27,30,2,12,m[4]);rect(im,27,31,1,10,m[6]);rect(im,26,32,5,2,m[2]);rect(im,26,38,5,2,m[2])
 rect(im,33,31,4,9,m[1]);for y=32,38,2 do rect(im,33,y,3,1,m[4]) end
 rect(im,31,30,1,13,r[1]);line(im,32,41,35,43,m[5]);rect(im,33,43,4,1,m[3])
 -- Thick dark-bordered hose around the port shoulder, and a thinner repaired one.
 line(im,18,26,16,32,m[1]);line(im,16,32,19,39,m[1]);line(im,19,39,24,41,m[1])
 line(im,19,26,17,32,r[1]);line(im,17,32,20,38,r[2]);line(im,20,38,24,40,r[1])
 line(im,41,25,46,32,m[1]);line(im,46,32,44,39,m[1]);line(im,44,39,39,43,m[1])
 line(im,42,25,47,32,m[4]);line(im,47,32,45,39,m[3]);line(im,45,39,40,43,m[4])
 for _,p in ipairs({{19,27},{18,34},{21,39},{43,28},{46,35},{42,41}}) do rect(im,p[1],p[2],2,1,m[5]) end
 rect(im,8,36,8,4,m[1]);for x=9,14,2 do rect(im,x,37,1,2,m[4]) end
 rect(im,48,37,8,5,m[1]);rect(im,49,38,6,3,m[3]);line(im,50,38,53,40,m[5])
 if state=='critical_damage' then
  line(im,31,31,32,35,r[2]);line(im,32,35,31,37,r[1]);line(im,31,38,30,40,m[1])
  rect(im,34,33,2,3,m[1]);put(im,33,34,m[5]);line(im,22,35,24,38,m[4])
 end
end
local function rotary(im,state)
 local hot=state=='weapons_hot';local critical=state=='critical_damage'
 -- Port rotary cannon has a longer receiver, three barrels and an external feed.
 poly(im,{{7,20},{15,19},{19,24},{18,36},{14,41},{7,40},{3,35},{3,25}},m[1])
 poly(im,{{7,22},{14,21},{17,25},{16,34},{13,38},{8,37},{5,34},{5,26}},m[3])
 poly(im,{{6,26},{8,24},{10,24},{10,35},{8,36},{6,33}},m[6]);line(im,6,27,6,32,m[7])
 rect(im,11,26,5,8,m[2]);for y=27,31,2 do rect(im,12,y,3,1,m[4]) end
 -- Physical barrel extension in the hot pose; a shortened third barrel in damage.
 for i,x in ipairs({6,10,14}) do
  local y=hot and 6+(i%2) or 10+(i%2)
  if critical and i==3 then y=16 end
  rect(im,x,y,3,24-y,m[1]);rect(im,x+1,y+1,1,21-y,m[5]);rect(im,x,y+5,3,2,m[2]);put(im,x+1,y+5,m[4])
  rect(im,x,y,3,2,m[1]);put(im,x+1,y+1,hot and heat[2] or m[3]);rect(im,x,20,3,2,m[3])
  if hot then rect(im,x+1,16,1,4,r[3]) end
 end
 rect(im,5,23,12,3,m[1]);rect(im,6,24,10,1,m[5]);rect(im,8,25,6,1,m[3])
 -- Bolted ammunition box and uneven exposed belt teeth.
 rect(im,3,28,3,7,m[1]);rect(im,3,29,2,5,m[4]);put(im,3,29,m[6])
 for y=28,34,2 do rect(im,17,y,3,1,m[5]) end
 rect(im,8,34,6,3,m[1]);rect(im,9,35,4,1,hot and r[3] or r[1])
end
local function breacher(im,state)
 local hot=state=='weapons_hot'
 -- Starboard salvaged twin breacher is broader and shorter than the rotary gun.
 poly(im,{{48,19},{57,20},{61,25},{61,36},{57,41},{48,40},{44,34},{45,24}},m[1])
 poly(im,{{49,21},{56,22},{59,26},{59,35},{56,38},{49,37},{46,33},{47,25}},m[4])
 poly(im,{{54,23},{57,24},{58,27},{58,34},{55,36},{53,34},{53,25}},m[6]);line(im,57,26,57,32,m[7])
 for i,x in ipairs({47,54}) do
  local y=(i==1 and 13 or 10)-(hot and 3 or 0)
  rect(im,x,y,5,25-y,m[1]);rect(im,x+1,y+1,3,22-y,m[3]);rect(im,x+1,y+1,1,21-y,m[6]);rect(im,x+3,y+2,1,19-y,m[4])
  rect(im,x,y,5,3,m[1]);rect(im,x+1,y+1,3,1,hot and heat[2] or m[3])
  rect(im,x,y+7,5,2,m[2]);rect(im,x+1,y+7,3,1,m[5]);rect(im,x,21,5,2,m[5])
  if hot then rect(im,x+2,18,1,3,r[3]) end
 end
 rect(im,46,25,13,3,m[1]);rect(im,47,26,11,1,m[5]);rect(im,49,27,7,1,m[3])
 rect(im,48,29,5,7,m[1]);rect(im,49,30,3,5,m[3]);rect(im,49,30,1,5,m[5])
 rect(im,54,29,3,2,hot and r[3] or r[1]);rect(im,55,33,2,2,m[2]);put(im,55,33,m[5])
end
local function engines(im,state)
 -- Rebuilt engines do not match: port is longer with an improvised clamp.
 poly(im,{{18,44},{26,44},{29,49},{27,56},{23,59},{18,56},{16,49}},m[1])
 poly(im,{{19,46},{24,45},{27,49},{25,55},{22,56},{19,54},{18,49}},m[4])
 rect(im,20,48,5,7,m[1]);rect(im,21,49,3,4,m[3]);rect(im,20,51,5,1,m[5]);rect(im,19,55,6,2,m[2])
 poly(im,{{38,44},{44,44},{47,48},{46,54},{42,57},{37,54},{36,48}},m[1])
 poly(im,{{39,46},{43,45},{45,48},{44,52},{41,54},{39,53},{38,48}},m[5])
 rect(im,39,47,4,7,m[1]);rect(im,40,48,2,4,m[3]);rect(im,38,52,7,2,m[2])
 if state=='critical_damage' then
  rect(im,19,51,5,4,m[1]);line(im,18,49,20,53,m[3]);line(im,24,51,23,53,m[5]);put(im,21,54,m[4])
 end
end
local function armor(im,state)
 local broken=state=='critical_damage'
 -- Riveted ram teeth are mismatched salvage plates; the starboard tooth is shorter.
 poly(im,{{22,3},{27,3},{29,9},{27,15},{23,13},{21,9}},m[1])
 poly(im,{{23,4},{26,4},{27,8},{26,12},{24,11},{23,8}},m[6]);line(im,23,4,26,4,m[7]);put(im,25,9,m[3])
 if not broken then
  poly(im,{{34,4},{37,7},{39,15},{36,18},{32,14},{31,9}},m[1])
  poly(im,{{34,6},{35,7},{37,14},{36,16},{34,13},{33,9}},m[5]);line(im,34,6,35,7,m[6]);put(im,35,12,m[2])
 else
  poly(im,{{34,6},{37,8},{38,12},{36,12},{36,14},{34,13},{33,9}},m[1]);line(im,34,8,35,10,m[4]);put(im,35,8,m[6])
 end
 rect(im,28,10,4,3,m[1]);rect(im,28,11,3,1,m[4])
 -- Two unequal shoulder plates, riveted over a rough industrial subframe.
 if not broken then
  poly(im,{{21,19},{26,18},{28,24},{26,31},{21,34},{17,29},{18,24}},m[1])
  poly(im,{{21,21},{25,20},{26,24},{24,29},{21,31},{19,28},{20,24}},m[6])
  line(im,21,21,24,20,m[7]);line(im,21,30,23,28,m[4]);put(im,21,23,m[3]);put(im,20,28,m[3])
 else
  poly(im,{{21,19},{26,18},{27,22},{24,23},{24,26},{22,25},{20,27},{18,24}},m[1])
  poly(im,{{21,21},{25,20},{25,21},{23,22},{23,24},{21,23},{20,24}},m[5]);put(im,21,21,m[7])
 end
 poly(im,{{36,17},{42,19},{47,25},{45,30},{39,29},{35,24}},m[1])
 poly(im,{{37,19},{41,21},{44,25},{43,28},{40,27},{37,24}},m[5]);line(im,37,19,41,21,m[6]);line(im,41,27,43,27,m[3])
 rect(im,39,22,3,3,m[3]);rect(im,40,22,2,1,m[6]);put(im,42,25,m[2])
 -- Patchwork chest uses separate slab outlines, not a circular armored reactor.
 if not broken then
  poly(im,{{25,27},{30,26},{34,28},{34,36},{31,39},{26,37},{23,32}},m[1])
  poly(im,{{26,28},{30,28},{32,29},{32,35},{30,37},{27,35},{25,32}},m[6]);line(im,26,28,30,28,m[7])
  rect(im,27,30,4,3,m[5]);put(im,27,30,m[3]);put(im,30,32,m[3])
  poly(im,{{35,28},{39,29},{41,35},{39,43},{34,45},{30,41},{32,37},{35,36}},m[1])
  poly(im,{{36,30},{38,30},{39,35},{37,41},{34,43},{32,40},{34,38},{36,37}},m[5]);line(im,36,30,38,30,m[7]);line(im,34,42,37,40,m[3])
  rect(im,35,32,2,4,m[4]);rect(im,35,32,2,1,m[6]);put(im,36,39,m[2])
 else
  -- Broken teeth retain the bolt edges; broad central plating is absent.
  poly(im,{{25,27},{29,26},{29,28},{27,29},{27,31},{24,30},{23,29}},m[1]);rect(im,25,28,2,1,m[5])
  poly(im,{{39,30},{41,35},{40,38},{38,37},{38,35},{37,34},{38,31}},m[1]);line(im,39,32,40,35,m[4])
  poly(im,{{33,42},{36,43},{39,41},{39,44},{34,46},{30,44}},m[1]);line(im,33,44,35,45,m[5])
 end
 -- Swept salvage wings have deliberately different shapes and stripe layouts.
 if not broken then
  poly(im,{{6,38},{11,39},{17,36},{21,41},{18,47},{11,45},{6,43}},m[1])
  poly(im,{{8,40},{12,41},{16,39},{18,42},{16,45},{11,43},{8,42}},m[6]);line(im,9,40,12,41,m[7])
 else
  poly(im,{{10,40},{16,37},{19,40},{17,43},{15,42},{13,45},{10,44}},m[1])
  poly(im,{{12,41},{16,39},{17,40},{15,41},{13,43},{12,43}},m[4]);put(im,16,40,m[6])
 end
 poly(im,{{44,37},{50,40},{56,37},{58,41},{52,46},{46,47},{41,43}},m[1])
 poly(im,{{45,39},{49,42},{55,39},{55,41},{51,44},{47,45},{44,42}},m[5]);line(im,45,39,49,42,m[6])
 -- Rear reinforcement plate is a short repaired crossbar, offset below the chest.
 poly(im,{{27,46},{36,47},{38,50},{35,53},{28,52},{25,49}},m[1]);poly(im,{{28,47},{35,48},{36,50},{34,51},{29,50},{27,49}},m[5])
 rect(im,29,48,4,2,m[3]);put(im,34,50,m[2])
end
local function cockpit(im,state)
 -- Rectangular red cockpit: reused Raider command module, not an energy core.
 poly(im,{{27,13},{33,12},{36,16},{35,25},{32,28},{26,25},{24,19}},m[1])
 poly(im,{{28,15},{32,14},{34,17},{33,24},{31,25},{27,23},{26,19}},m[5]);line(im,28,15,31,14,m[6])
 for y=0,6 do for x=0,3 do put(im,28+x,16+y,references.elite:getPixel(28+x*2,12+y*2)) end end
 rect(im,32,17,1,6,m[3]);rect(im,27,23,6,1,m[2]);put(im,33,24,m[3])
 -- Tall offset welded aerial and a cut-down antenna on the opposite side.
 rect(im,38,7,2,12,m[1]);rect(im,38,8,1,9,m[4]);rect(im,37,12,4,2,m[2]);put(im,38,7,m[6])
 rect(im,22,13,2,6,m[1]);rect(im,22,14,1,4,m[5]);put(im,22,13,r[2])
end
local function paint(im,state)
 local broken=state=='critical_damage'
 -- Large battered red bands and diagonal elite-derived striping are the faction cue.
 rect(im,23,5,3,2,r[2]);put(im,23,5,r[3]);put(im,25,6,m[6]);rect(im,24,10,2,1,r[1])
 if not broken then line(im,34,8,36,13,r[2]);put(im,34,8,r[3]) else put(im,34,9,r[1]) end
 rect(im,6,28,3,4,r[2]);rect(im,6,28,1,3,r[3]);put(im,8,30,m[5]);rect(im,53,30,4,2,r[2]);put(im,53,30,r[3])
 line(im,38,20,42,24,r[2]);line(im,37,21,40,24,r[3]);put(im,40,22,m[5])
 if not broken then
  line(im,20,25,24,21,r[2]);line(im,20,27,25,22,r[2]);put(im,23,23,m[6])
  rect(im,26,32,2,3,r[2]);put(im,26,32,r[3]);line(im,35,39,37,37,r[2])
  line(im,9,41,13,43,r[2]);line(im,12,40,16,43,r[3]);put(im,14,42,m[6])
 else
  put(im,22,21,r[2]);put(im,21,23,r[3]);put(im,13,42,r[1])
 end
 line(im,46,42,48,44,r[2]);line(im,49,42,51,44,r[3]);put(im,50,43,m[5])
 rect(im,19,48,2,4,r[2]);put(im,19,48,r[3]);rect(im,43,47,1,4,r[2])
 rect(im,29,50,5,1,r[1]);put(im,32,50,r[2])
end
local function activeHeat(im,state)
 if state=='weapons_hot' then
  rect(im,9,35,4,1,r[3]);rect(im,54,29,3,1,r[3]);rect(im,10,36,2,1,heat[3]);put(im,55,30,heat[3])
  -- Open feed indicator bank; hotter light stays on the weapons, not the hull center.
  rect(im,12,27,3,6,r[1]);for y=27,31,2 do rect(im,12,y,3,1,r[3]);put(im,13,y,heat[3]) end
  poly(im,{{20,56},{25,56},{25,59},{23,61},{21,60},{20,58}},r[2]);rect(im,21,56,3,3,heat[2]);rect(im,22,56,1,3,heat[3])
  poly(im,{{39,54},{44,54},{44,57},{42,60},{40,59},{39,56}},r[2]);rect(im,40,54,3,3,heat[2]);rect(im,41,54,1,3,heat[3])
 elseif state=='critical_damage' then
  rect(im,40,54,3,1,r[1]);put(im,41,55,r[2]);put(im,55,29,r[1])
 else
  rect(im,21,56,3,1,r[1]);put(im,22,57,r[2]);rect(im,40,54,3,1,r[1]);put(im,41,55,r[2])
 end
end
local function damage(im,state)
 if state~='critical_damage' then return end
 -- Small displaced sparks and charred cuts; the breach is mechanical, not luminous.
 line(im,35,33,37,31,heat[2]);put(im,35,33,heat[3]);put(im,38,29,heat[2]);put(im,37,27,heat[3])
 line(im,21,50,19,48,heat[2]);put(im,21,50,heat[3]);put(im,17,46,heat[2]);put(im,18,44,heat[3])
 line(im,15,17,17,19,m[4]);put(im,16,15,m[5]);line(im,29,36,30,38,m[1]);put(im,28,39,m[3])
 rect(im,20,52,3,2,m[1]);put(im,21,53,m[4]);put(im,18,56,m[3]);put(im,16,54,m[2])
 line(im,42,34,44,36,m[1]);put(im,43,34,m[4]);put(im,46,37,m[3])
end
local flats,allLayers={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 hull(l[1],state.id);machinery(l[2],state.id);rotary(l[3],state.id);breacher(l[4],state.id);engines(l[5],state.id)
 armor(l[6],state.id);cockpit(l[7],state.id);paint(l[8],state.id);activeHeat(l[9],state.id);damage(l[10],state.id)
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=layerNames[i];local doubled=scale(im,2);allLayers[state.id][i]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2;local stem='raider_assault_commander_'..state.id;flats[state.id]=Image(s)
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
master:saveAs(outputDir..'/raider_assault_commander_states.aseprite');master:close()
local strip=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flats[state.id],Point((i-1)*128,0)) end;strip:saveAs(outputDir..'/raider_assault_commander_states.png')
local preview=Image(1000,756,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'RAIDER ASSAULT COMMANDER',32,24,m[6],3);text(preview,'REPLACEMENT BOSS / SCAVENGED WARSHIP',34,55,r[3],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,92+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flats[state.id],2),Point(x+20,104));text(preview,state.label,x+math.floor((296-#state.label*8)/2),389,r[3],2)
 preview:drawImage(flats[state.id],Point(x+84,414));text(preview,'128 X 128',x+112,550,m[4],2)
end
rect(preview,28,580,944,1,m[3]);text(preview,'APPROVED RAIDER FAMILY',32,602,m[4],2)
for i,ref in ipairs({{id='basic',label='BASIC'},{id='shotgun',label='SHOTGUN'},{id='sniper_charging',label='SNIPER'},{id='elite',label='ELITE'}}) do
 local x=112+(i-1)*236;preview:drawImage(references[ref.id],Point(x-32,639));text(preview,ref.label,x-#ref.label*4,725,m[5],2)
end
preview:saveAs(outputDir..'/raider_assault_commander_comparison.png')
if app.params.qaDir then
 local qa=Image(384,192,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,96,384,96,m[6])
 for i,state in ipairs(states) do
  local small=Image(64,64,ColorMode.RGB);for y=0,63 do for x=0,63 do put(small,x,y,flats[state.id]:getPixel(x*2,y*2)) end end
  qa:drawImage(small,Point(32+(i-1)*128,20));qa:drawImage(small,Point(32+(i-1)*128,116))
  text(qa,state.label,(i-1)*128+math.floor((128-#state.label*4)/2),6,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_64px.png')
end
local manifest={generatorId='void-scrapper-raider-assault-commander-v1',name='RAIDER ASSAULT COMMANDER',faction='RAIDER',role='Replacement assault commander',width=128,height=128,pixelScale=2,front='up / negative Y',anchor={x=64,y=64},palette={metal=metalHex,factionRed=redHex,localHeat=heatHex},layers=layerNames,assets={},
 referencePolicy='Approved Raider palette only: salvaged armor, red paint, patched machinery and the rectangular elite cockpit. Entire boss hull and mismatched mounts are newly authored. SYSTEM sprites are read by validation for silhouette comparisons only, never by this generator.',
 animationLayers={hull='Main Hull',weapons={'Left Rotary Weapon','Right Twin Breacher'},armor='Salvaged Armor Plates',engine='Engine Assemblies',heat='Weapon Heat and Engine Plumes',damage='Critical Damage VFX'},
 master='raider_assault_commander_states.aseprite',sheet='raider_assault_commander_states.png',sheetLayout='Three horizontal 128x128 cells: idle, weapons_hot, critical_damage; no padding or trimming.',stateTimeline='Three tagged state poses, not a finished looping animation; 200ms per pose.',preview='raider_assault_commander_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='raider_assault_commander_'..state.id..'.aseprite',png='raider_assault_commander_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('RAIDER ASSAULT COMMANDER: three states, layered sources, transparent strip and comparison.\n');done:close()
print('RAIDER_ASSAULT_COMMANDER_GENERATED')
