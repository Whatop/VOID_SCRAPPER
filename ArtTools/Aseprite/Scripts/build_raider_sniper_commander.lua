-- RAIDER SNIPER COMMANDER: salvaged rail hunter, authored on the approved 2px grid.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,scale,text=h.put,h.rect,h.line,h.poly,h.scale,h.text
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'171E25','303941','515F67','78888C','ADB9B7','D6DEDA','F0F2E9'}
local redHex={'662C32','B84445','ED7563'}
local blueHex={'234871','529EDC','B8EFFF'}
local m,r,b={},{},{}
for i,v in ipairs(metalHex) do m[i]=rgba(v) end
for i,v in ipairs(redHex) do r[i]=rgba(v) end
for i,v in ipairs(blueHex) do b[i]=rgba(v) end
local references={}
for _,id in ipairs({'basic','shotgun','sniper_charging','elite'}) do
 local im=assert(Image{fromFile=sourceDir..'/raider_'..id..'.png'});assert(im.width==64 and im.height==64,'Approved Raider size changed');references[id]=im
end
local commander=assert(Image{fromFile=sourceDir..'/raider_assault_commander_idle.png'})
local carrier=assert(Image{fromFile=sourceDir..'/raider_salvage_carrier_idle.png'})
assert(commander.width==128 and commander.height==128 and carrier.width==128 and carrier.height==128)
local colors={};for _,im in pairs(references) do for it in im:pixels() do if pc.rgbaA(it())>0 then colors[it()]=true end end end
for _,palette in ipairs({m,r,b}) do for _,p in ipairs(palette) do assert(colors[p],'Color outside approved Raider palette') end end
local layerNames={'Main Hull','Exposed Machinery and Wiring','Railgun','Targeting Sensor','Left Mine Rack','Right Utility Pod','Salvaged Armor','Cockpit and Faction Paint','Engine','Target Lock VFX','Damage and Debris'}
local states={
 {id='idle',label='IDLE',description='Long paired rail barrel, shuttered offset binocular rangefinder, capped mine rack, and dim utility cells.'},
 {id='target_lock',label='TARGET LOCK',description='Muzzle extends, rail shrouds slide apart, blue-white coils and dual targeting optics engage; utility cells charge.'},
 {id='critical_damage',label='CRITICAL DAMAGE',description='Left rail breaks, rangefinder loses half its housing, right utility pod ruptures, and hull plates uncover cabling; localized electrical shorts.'}
}
local function hull(im)
 -- Long keel with small swept shoulders; no radial body or wide cargo deck.
 poly(im,{{28,25},{35,25},{40,31},{42,43},{40,53},{36,58},{27,56},{23,49},{23,35}},m[1])
 poly(im,{{29,27},{34,27},{38,32},{40,43},{38,51},{35,55},{28,54},{25,48},{25,35}},m[3])
 poly(im,{{28,31},{35,30},{38,37},{37,51},{33,55},{29,51},{27,40}},m[2])
 -- Narrow exposed cross-members support mismatched equipment pods.
 poly(im,{{22,39},{26,37},{27,42},{23,45},{18,45},{18,41}},m[1]);line(im,19,42,25,40,m[4]);line(im,20,44,24,42,m[2])
 poly(im,{{38,37},{43,36},{46,40},{44,44},{39,43}},m[1]);rect(im,39,39,5,2,m[4]);rect(im,40,40,4,2,m[2])
 rect(im,25,52,3,3,m[4]);rect(im,37,50,3,4,m[4]);put(im,25,52,m[6])
end
local function machinery(im,state)
 -- Dark, linear mechanical cavities continue below removable plating.
 rect(im,26,34,12,19,m[1]);rect(im,27,35,10,17,m[2])
 for y=37,49,3 do rect(im,31,y,4,1,m[4]);put(im,34,y+1,m[3]) end
 line(im,27,37,27,46,r[1]);line(im,27,46,30,49,r[2]);line(im,30,49,30,53,r[1])
 line(im,36,37,36,45,m[5]);line(im,36,45,34,47,m[4]);line(im,34,47,34,53,m[3])
 rect(im,28,50,2,3,m[4]);rect(im,32,51,2,3,m[5]);put(im,32,52,m[2])
 -- A visibly hand-routed external cable, away from the optical barrel axis.
 line(im,23,30,22,33,m[1]);line(im,22,33,24,37,m[1]);line(im,23,32,23,34,r[1]);put(im,24,35,r[2])
 line(im,39,33,42,35,m[1]);line(im,42,35,42,38,m[1]);put(im,41,35,r[2]);put(im,42,37,r[1])
end
local function railgun(im,state)
 local active=state=='target_lock';local broken=state=='critical_damage';local top=active and 2 or 4
 -- Forked industrial rail barrel. The bore is negative space, not a glowing core.
 rect(im,28,top,3,24-top,m[1]);rect(im,34,top,3,24-top,m[1])
 rect(im,29,top+1,1,22-top,m[5]);rect(im,35,top+1,1,22-top,m[4])
 rect(im,30,top+2,1,20-top,active and b[2] or m[2]);rect(im,34,top+2,1,20-top,active and b[2] or m[2])
 rect(im,27,top+1,2,3,m[3]);rect(im,35,top,3,4,m[3]);put(im,27,top+1,m[6]);put(im,36,top,m[5])
 -- Unequal scavenged barrel clamps leave dark gaps between charging segments.
 for _,y in ipairs({9,16,22}) do
  rect(im,27,y,4,2,m[1]);rect(im,28,y,2,1,m[5]);rect(im,34,y,4,2,m[1]);rect(im,35,y,2,1,m[4])
 end
 rect(im,28,11,1,3,r[2]);put(im,28,11,r[3]);rect(im,35,18,1,3,r[1])
 -- Mechanical receiver with red warning plate; the two sleeves physically retract.
 poly(im,{{27,24},{37,24},{39,28},{37,36},{27,36},{25,29}},m[1])
 rect(im,27,25,10,9,m[3]);rect(im,29,25,6,9,m[1]);rect(im,31,24,2,11,m[2])
 for y=25,31,3 do rect(im,29,y,2,2,active and b[3] or m[4]);rect(im,33,y,2,2,active and b[2] or m[3]) end
 local lx,rx=active and 24 or 26,active and 37 or 36
 rect(im,lx,25,3,9,m[1]);rect(im,lx,25,2,7,m[5]);rect(im,lx,25,1,5,m[6]);rect(im,lx,30,2,2,r[2])
 rect(im,rx,24,3,10,m[1]);rect(im,rx+1,25,2,7,m[4]);rect(im,rx+1,25,2,1,m[6]);rect(im,rx+1,29,1,3,r[1])
 rect(im,29,34,6,2,m[1]);rect(im,30,34,4,1,r[2]);put(im,30,34,r[3])
 if broken then
  -- Remove a section of the left rail, preserving one jagged tooth at each end.
  rect(im,27,13,4,5,0);put(im,29,13,m[3]);put(im,28,17,m[4]);put(im,30,18,m[5])
  rect(im,27,26,4,7,m[1]);rect(im,28,27,2,3,m[3]);line(im,28,28,30,30,r[1]);put(im,29,29,r[2])
  line(im,35,28,36,30,m[1]);put(im,35,31,m[2]);rect(im,32,31,2,2,m[4])
 end
end
local function sensor(im,state)
 local active=state=='target_lock';local broken=state=='critical_damage'
 -- Offset twin square lenses in a welded box, never a central energy lens.
 rect(im,22,26,4,5,m[1]);rect(im,23,27,2,3,m[3]);put(im,23,27,m[5])
 poly(im,{{17,21},{24,21},{26,23},{26,29},{23,31},{17,29},{16,26},{16,23}},m[1])
 poly(im,{{18,22},{23,22},{25,24},{25,28},{23,29},{18,28},{17,25}},m[4])
 rect(im,18,23,3,3,m[1]);rect(im,22,23,3,3,m[1])
 rect(im,18,24,2,1,active and b[3] or b[1]);rect(im,22,24,2,1,active and b[3] or b[1])
 if active then
  rect(im,18,22,3,1,m[6]);rect(im,22,22,3,1,m[5]);rect(im,17,26,1,2,b[2]);rect(im,24,26,1,2,b[2])
  rect(im,18,20,6,1,m[1]);rect(im,19,20,4,1,m[5]);put(im,24,21,r[3])
 else rect(im,18,23,3,1,m[5]);rect(im,22,23,3,1,m[6]) end
 rect(im,18,27,5,1,r[2]);put(im,18,27,r[3]);put(im,24,28,m[6]);put(im,19,29,m[2])
 -- Thin rangefinder mast gives the upper-left flank a recognizable silhouette.
 rect(im,17,18,1,4,m[1]);rect(im,18,18,1,3,m[4]);rect(im,17,17,3,2,m[1]);put(im,18,17,m[5])
 if broken then
  rect(im,16,17,5,11,0);rect(im,20,23,2,5,m[1]);put(im,20,23,m[5]);put(im,21,27,m[4]);put(im,19,28,m[3])
  line(im,18,28,17,30,r[1]);line(im,17,30,19,32,r[2]);put(im,19,32,m[5]);put(im,22,24,m[2]);put(im,24,26,m[1])
 end
end
local function mineRack(im,state)
 local active=state=='target_lock';local broken=state=='critical_damage'
 poly(im,{{17,39},{21,40},{22,44},{21,53},{17,54},{14,51},{14,43}},m[1])
 rect(im,15,43,6,8,m[3]);rect(im,16,41,4,2,m[5]);rect(im,15,43,1,7,m[6]);rect(im,20,44,1,7,m[2])
 -- Two dark circular mine caps held in an open external launch rack.
 for _,y in ipairs({44,49}) do
  poly(im,{{17,y-1},{19,y-1},{20,y},{20,y+1},{19,y+2},{17,y+2},{16,y+1},{16,y}},m[1])
  rect(im,17,y,2,2,m[4]);put(im,17,y,m[5]);put(im,18,y+1,active and r[3] or r[1])
 end
 rect(im,15,51,2,2,r[2]);put(im,15,51,r[3]);rect(im,19,52,2,1,m[5]);put(im,20,41,m[6])
 if broken then rect(im,14,49,4,4,0);put(im,17,50,m[3]);line(im,18,50,20,52,m[1]);put(im,18,52,r[1]) end
end
local function utility(im,state)
 local active=state=='target_lock';local broken=state=='critical_damage'
 poly(im,{{44,36},{48,38},{49,41},{49,49},{46,51},{42,49},{42,39}},m[1])
 rect(im,43,39,5,9,m[2]);rect(im,43,38,4,1,m[5]);rect(im,43,39,1,8,m[4]);rect(im,48,41,1,6,m[3])
 for y=40,46,3 do rect(im,45,y,2,2,active and b[2] or b[1]);put(im,45,y,active and b[3] or m[4]) end
 rect(im,43,48,4,2,m[5]);rect(im,43,48,3,1,r[2]);put(im,43,48,r[3])
 rect(im,47,38,1,2,m[6]);put(im,47,49,m[3])
 if broken then
  rect(im,46,39,4,10,0);rect(im,44,42,2,6,m[1]);put(im,45,42,m[5]);put(im,46,49,m[4])
  line(im,44,42,46,44,r[1]);line(im,46,44,46,47,r[2]);line(im,45,47,48,49,m[4]);put(im,48,49,m[6]);put(im,44,45,b[1])
 end
end
local function armor(im,state)
 local broken=state=='critical_damage'
 -- Long unequal salvaged hull plates; a narrow unarmored spine remains visible.
 poly(im,{{25,36},{29,37},{30,45},{28,50},{26,51},{23,46},{23,40}},m[1])
 poly(im,{{25,38},{28,39},{29,45},{27,49},{25,46},{24,41}},m[5])
 line(im,25,38,24,41,m[6]);line(im,24,42,25,46,m[6]);put(im,27,39,m[7]);line(im,27,46,27,48,m[4])
 rect(im,24,42,2,3,r[2]);put(im,24,42,r[3]);line(im,25,46,27,47,r[1])
 -- Improvised diagonal repair strap differs from the original plate underneath.
 line(im,25,39,28,42,m[2]);put(im,25,39,m[6]);put(im,28,42,m[6]);put(im,26,40,m[4])
 if not broken then
  poly(im,{{36,37},{39,38},{41,43},{40,49},{37,54},{34,51},{35,44}},m[1])
  poly(im,{{37,39},{38,40},{40,44},{39,48},{37,52},{35,50},{36,44}},m[5])
  line(im,37,39,38,40,m[6]);line(im,36,44,35,50,m[6]);put(im,39,45,m[4]);line(im,38,48,36,51,r[2]);put(im,38,48,r[3])
  rect(im,37,42,2,3,m[3]);put(im,37,42,m[6]);put(im,38,44,m[4])
 else
  -- The starboard plate is physically absent; only upper and rear mounting shards survive.
  poly(im,{{36,37},{39,38},{40,41},{38,41},{37,40},{36,42}},m[4]);line(im,36,37,39,38,m[6]);put(im,38,39,r[1])
  poly(im,{{38,50},{39,49},{38,53},{36,53},{34,51},{36,50}},m[3]);line(im,35,51,37,52,m[5]);put(im,37,52,r[2])
  rect(im,25,43,3,3,m[1]);line(im,25,43,27,44,m[3]);put(im,27,45,r[1])
 end
end
local function bridge(im)
 -- Approved Raider rectangular cockpit motif, sampled from the copied Elite.
 rect(im,28,36,6,10,m[1]);rect(im,29,37,4,8,m[4])
 for y=0,6 do for x=0,3 do put(im,29+x,37+y,references.elite:getPixel(28+x*2,12+y*2)) end end
 rect(im,28,46,3,1,r[2]);put(im,28,46,r[3]);rect(im,36,34,2,2,r[2]);put(im,36,34,r[3])
 rect(im,27,52,3,1,r[2]);rect(im,37,52,2,1,r[1]);put(im,27,52,r[3])
end
local function engine(im,state)
 local active=state=='target_lock';local broken=state=='critical_damage'
 -- One long drive and a small mismatched lateral maneuvering thruster.
 poly(im,{{29,53},{35,53},{37,56},{36,60},{34,61},{29,60},{28,57}},m[1])
 rect(im,30,54,5,4,m[3]);rect(im,30,54,4,1,m[5]);rect(im,31,55,3,3,m[1]);rect(im,29,58,1,2,m[4]);rect(im,35,58,1,2,m[4])
 rect(im,30,59,5,1,r[1]);rect(im,31,59,3,1,active and r[3] or r[2]);put(im,32,60,active and r[2] or r[1])
 poly(im,{{39,51},{42,51},{43,54},{42,57},{39,57},{38,54}},m[1]);rect(im,40,52,2,3,m[3]);put(im,40,52,m[5]);rect(im,39,55,3,1,r[1])
 if broken then rect(im,41,53,2,4,0);put(im,40,54,m[1]);put(im,40,56,r[3]);rect(im,33,55,2,3,m[1]) end
end
local function charge(im,state)
 if state~='target_lock' then return end
 -- Hard-edged segmented conducting rails and a small muzzle reticle, not a beam.
 for _,range in ipairs({{5,8},{11,15},{18,21}}) do
  rect(im,30,range[1],1,range[2]-range[1]+1,b[3]);rect(im,34,range[1],1,range[2]-range[1]+1,b[3])
 end
 rect(im,31,7,1,2,b[2]);rect(im,33,13,1,2,b[2]);rect(im,31,20,1,2,b[2])
 line(im,24,3,24,6,b[2]);line(im,24,3,26,3,b[3]);line(im,40,3,40,6,b[2]);line(im,38,3,40,3,b[3])
 rect(im,31,2,3,1,b[3]);put(im,32,4,b[2]);rect(im,30,32,4,1,b[2])
 put(im,18,24,b[3]);put(im,23,24,b[3]);rect(im,18,26,5,1,b[2]);put(im,23,22,r[3])
 -- Arm status on the spine is red, keeping the ship Raider-coded at small scale.
 rect(im,33,46,1,3,r[3]);put(im,35,48,r[2])
end
local function damage(im,state)
 if state~='critical_damage' then return end
 -- Local short circuits, broken clamps, and loose wires; no clean exposed reactor.
 line(im,29,13,30,14,b[2]);line(im,30,14,28,15,b[3]);put(im,28,16,b[2]);put(im,26,14,b[1])
 line(im,38,43,40,44,r[2]);line(im,40,44,39,47,r[1]);put(im,38,43,m[5])
 line(im,35,44,37,45,m[5]);line(im,37,45,35,47,r[1]);put(im,36,46,r[3])
 line(im,46,43,48,42,b[2]);put(im,49,41,b[3]);put(im,51,43,b[2]);put(im,48,39,b[1])
 rect(im,18,18,2,2,m[3]);put(im,18,18,m[6]);put(im,16,21,m[4]);line(im,15,26,14,27,r[2]);put(im,14,29,r[3])
 poly(im,{{44,53},{46,54},{46,56},{43,56}},m[3]);line(im,44,53,46,54,m[5]);put(im,45,55,r[1]);put(im,48,57,m[4]);put(im,46,59,r[2])
 put(im,25,49,m[1]);put(im,26,50,m[2]);put(im,37,50,m[1])
end
local flats,allLayers={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 hull(l[1]);machinery(l[2],state.id);railgun(l[3],state.id);sensor(l[4],state.id);mineRack(l[5],state.id);utility(l[6],state.id)
 armor(l[7],state.id);bridge(l[8]);engine(l[9],state.id);charge(l[10],state.id);damage(l[11],state.id)
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=layerNames[i];local doubled=scale(im,2);allLayers[state.id][i]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2;local stem='raider_sniper_commander_'..state.id;flats[state.id]=Image(s)
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
master:saveAs(outputDir..'/raider_sniper_commander_states.aseprite');master:close()
local strip=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flats[state.id],Point((i-1)*128,0)) end;strip:saveAs(outputDir..'/raider_sniper_commander_states.png')
local preview=Image(1000,792,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'RAIDER SNIPER COMMANDER',32,24,m[6],3);text(preview,'TRACK / LOCK / ELIMINATE',34,55,b[2],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,92+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flats[state.id],2),Point(x+20,104));text(preview,state.label,x+math.floor((296-#state.label*8)/2),389,r[3],2)
 preview:drawImage(flats[state.id],Point(x+84,414));text(preview,'128 X 128',x+112,550,m[4],2)
end
rect(preview,28,580,944,1,m[3]);text(preview,'APPROVED RAIDER FAMILY',32,602,m[4],2)
for i,ref in ipairs({{id='basic',label='BASIC'},{id='shotgun',label='SHOTGUN'},{id='sniper_charging',label='SNIPER'},{id='elite',label='ELITE'}}) do
 local cx=78+(i-1)*133;preview:drawImage(references[ref.id],Point(cx-32,661));text(preview,ref.label,cx-#ref.label*4,766,m[5],2)
end
preview:drawImage(commander,Point(615,630));text(preview,'ASSAULT',651,766,m[5],2)
preview:drawImage(carrier,Point(809,630));text(preview,'CARRIER',845,766,m[5],2)
preview:saveAs(outputDir..'/raider_sniper_commander_comparison.png')
if app.params.qaDir then
 local qa=Image(384,192,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,96,384,96,m[6])
 for i,state in ipairs(states) do
  local small=Image(64,64,ColorMode.RGB);for y=0,63 do for x=0,63 do put(small,x,y,flats[state.id]:getPixel(x*2,y*2)) end end
  qa:drawImage(small,Point(32+(i-1)*128,20));qa:drawImage(small,Point(32+(i-1)*128,116))
  text(qa,state.label,(i-1)*128+math.floor((128-#state.label*4)/2),6,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_64px.png')
end
local manifest={generatorId='void-scrapper-raider-sniper-commander-v1',name='RAIDER SNIPER COMMANDER',faction='RAIDER',role='Long-range replacement command ship with railgun, rangefinder and mine rack',width=128,height=128,pixelScale=2,front='up / negative Y',anchor={x=64,y=64},palette={metal=metalHex,factionRed=redHex,precisionBlue=blueHex},layers=layerNames,assets={},
 referencePolicy='Approved Raider palette and small Elite cockpit motif. New narrow keel, rail assembly, offset rangefinder and mine geometry; both prior boss silhouettes used for comparison only.',
 master='raider_sniper_commander_states.aseprite',sheet='raider_sniper_commander_states.png',sheetLayout='Three horizontal 128x128 cells: idle, target_lock, critical_damage; no padding or trimming.',stateTimeline='Three tagged static state poses, not a finished looping animation; 200ms per pose.',preview='raider_sniper_commander_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='raider_sniper_commander_'..state.id..'.aseprite',png='raider_sniper_commander_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('RAIDER SNIPER COMMANDER: three states, layered sources, transparent strip and comparison.\n');done:close()
print('RAIDER_SNIPER_COMMANDER_GENERATED')
