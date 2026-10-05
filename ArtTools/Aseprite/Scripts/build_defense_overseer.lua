-- DEFENSE OVERSEER: a new connected, broad Region B defense platform.
-- Only drawing/export helpers are shared with the Region A workflow; no A geometry.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local helperPath=assert(app.params.helperPath)
local h=dofile(helperPath)
local put,rect,line,poly,pairRect,pairLine,pairPoly,oct,diamond,mirror,scale,text=h.put,h.rect,h.line,h.poly,h.pairRect,h.pairLine,h.pairPoly,h.oct,h.diamond,h.mirror,h.scale,h.text
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local energyHex={'553020','995028','DB8432','FFBA53','FFE397','FFF7DA'}
local m,e={},{};for i,v in ipairs(metalHex) do m[i]=rgba(v) end;for i,v in ipairs(energyHex) do e[i]=rgba(v) end
local support=assert(Image{fromFile=sourceDir..'/system_support_orange.png'})
local defense=assert(Image{fromFile=sourceDir..'/system_orange_defense_node.png'})
local core=assert(Image{fromFile=sourceDir..'/core_orange_active.png'})
assert(support.width==64 and support.height==64 and defense.width==96 and defense.height==96 and core.width==256 and core.height==64,'Approved source dimensions changed')
local sourceColors={};for _,im in ipairs({support,defense}) do for it in im:pixels() do if pc.rgbaA(it())>0 then sourceColors[it()]=true end end end
for _,i in ipairs({1,2,3,4,5,6,7}) do assert(sourceColors[m[i]],'Approved armor palette changed') end
for _,i in ipairs({1,3,6}) do assert(sourceColors[e[i]],'Approved orange palette changed') end
local layerNames={'Connected Armored Chassis','Reinforced Perimeter','Battery L','Battery R','Reactor and Cradle','Blast Armor L','Blast Armor R','Fore and Aft Armor','Power Distribution','SYSTEM Registration'}
local states={{id='idle',label='IDLE',description='Closed reactor blast armor and covered suppression cells; six heavy guns at standby.'},
 {id='barrage_charged',label='BARRAGE CHARGED',description='Six gun bores and shoulder suppression cells charged; reinforced central armor remains closed.'},
 {id='armor_broken',label='ARMOR BROKEN',description='Central blast armor and forward collar lost; a large reactor cavity replaces the plated chest; batteries dimmed.'}}
local function chassis(im)
 -- A single broad armored mass, with integrated wings rather than detached pods.
 poly(im,{{5,24},{10,20},{21,20},{25,16},{38,16},{42,20},{53,20},{58,24},{61,30},{61,42},{56,48},{44,48},{41,51},{22,51},{19,48},{7,48},{2,42},{2,30}},m[1])
 poly(im,{{6,25},{10,22},{22,22},{26,18},{37,18},{41,22},{53,22},{57,25},{59,31},{59,41},{55,46},{43,46},{40,49},{23,49},{20,46},{8,46},{4,41},{4,31}},m[3])
 poly(im,{{8,28},{21,24},{42,24},{55,28},{57,32},{57,40},{52,44},{40,44},{38,47},{25,47},{23,44},{11,44},{6,40},{6,32}},m[2])
 -- Thick crossdeck spines visibly join both batteries to the reactor hull.
 rect(im,9,30,46,8,m[3]);rect(im,12,31,40,2,m[4]);rect(im,12,35,40,2,m[1])
 oct(im,21,22,22,24,4,m[1]);oct(im,22,23,20,22,3,m[3]);oct(im,23,24,18,20,3,m[2])
 -- Low aft machinery is boxed into the perimeter, not dangling below it.
 pairRect(im,10,43,10,4,m[1]);pairRect(im,11,44,8,2,m[4]);pairRect(im,12,45,6,1,m[2])
 rect(im,26,46,12,3,m[1]);rect(im,27,46,10,1,m[4]);rect(im,29,47,6,1,m[2])
end
local function perimeter(im)
 -- White slab armor and dark lower bevels carry the reinforced SYSTEM silhouette.
 pairPoly(im,{{6,27},{9,25},{12,27},{12,38},{16,41},{20,41},{22,45},{19,47},{8,47},{3,41},{3,32}},m[1])
 pairPoly(im,{{6,28},{8,27},{10,29},{10,39},{15,43},{18,43},{19,45},{9,45},{5,41},{5,32}},m[6])
 pairLine(im,6,28,8,27,m[7]);pairLine(im,5,33,5,40,m[7]);pairLine(im,9,45,17,45,m[4])
 pairRect(im,6,34,3,5,m[2]);pairRect(im,6,35,1,3,m[4])
 -- Two bolted transverse rear armor beams keep the body broad and weighty.
 pairPoly(im,{{15,39},{24,39},{27,44},{25,47},{21,46},{19,43},{15,43}},m[1])
 pairPoly(im,{{16,40},{23,40},{25,44},{24,45},{21,44},{20,42},{16,42}},m[5])
 pairLine(im,16,40,23,40,m[7]);pairRect(im,16,41,3,1,m[6])
end
local function battery(im,state)
 local charged=state=='barrage_charged';local broken=state=='armor_broken'
 -- An integrated turret well carries a visible triple heavy-gun battery.
 poly(im,{{8,20},{18,20},{22,24},{22,32},{19,36},{7,36},{4,32},{4,25}},m[1])
 poly(im,{{8,21},{17,21},{20,24},{20,31},{17,34},{8,34},{6,31},{6,25}},m[4])
 poly(im,{{8,22},{17,22},{19,24},{19,30},{17,32},{8,32},{7,30},{7,25}},m[6])
 line(im,8,22,17,22,m[7]);rect(im,18,25,1,5,m[3]);rect(im,9,32,8,1,m[3])
 -- Stepped barrel lengths point forward and distinguish this from support pods.
 for _,spec in ipairs({{7,12},{12,9},{17,14}}) do
  local x,y=spec[1],spec[2]
  rect(im,x,y,4,14,m[1]);rect(im,x+1,y+1,2,11,m[5]);rect(im,x+1,y+1,1,10,m[7])
  rect(im,x,y,4,3,m[1]);rect(im,x+1,y+1,2,1,charged and e[5] or broken and m[2] or e[1])
  rect(im,x,y+7,4,2,m[3]);rect(im,x+1,y+7,2,1,m[6])
  rect(im,x+1,y+10,2,3,charged and e[3] or m[3]);if charged then put(im,x+1,y+10,e[6]) end
 end
 rect(im,9,28,9,3,m[1]);rect(im,10,29,7,1,broken and m[3] or charged and e[4] or e[2])
 if charged then rect(im,11,29,5,1,e[5]) end
 -- Recessed six-cell suppression magazine, covered in idle and damaged poses.
 oct(im,10,35,11,7,1,m[1]);rect(im,11,36,9,5,m[3])
 if charged then
  for yy=36,39,3 do for xx=11,17,3 do rect(im,xx,yy,2,2,e[1]);put(im,xx,yy,e[4]);put(im,xx+1,yy+1,e[6]) end end
 else
  rect(im,11,36,9,2,m[5]);rect(im,11,39,9,2,m[4]);rect(im,12,36,7,1,m[6])
  rect(im,12,38,7,1,broken and m[1] or e[1])
 end
end
local function reactor(im,state)
 -- Dark rectangular engine vault anchors the reactor in the broad hull.
 oct(im,22,25,20,19,3,m[1]);oct(im,23,26,18,17,2,m[2]);pairRect(im,23,31,2,8,m[3])
 pairLine(im,24,28,28,25,m[4]);pairLine(im,24,40,28,43,m[4])
 diamond(im,31.5,34.5,10,m[1]);diamond(im,31.5,34.5,9,m[4]);diamond(im,31.5,34.5,8,e[1])
 diamond(im,31.5,34.5,7,e[2]);diamond(im,31.5,34.5,5,e[3])
 -- Approved orange diamond center is embedded beneath the blast covers.
 for y=10,21 do for x=10,21 do if math.abs(x-15.5)+math.abs(y-15.5)<=6 then
  put(im,26+x-10,29+y-10,support:getPixel(x*2,y*2))
 end end end
 if state=='armor_broken' then
  diamond(im,31.5,34.5,7,e[3]);diamond(im,31.5,34.5,5,e[4]);diamond(im,31.5,34.5,3,e[5]);diamond(im,31.5,34.5,1,e[6])
  pairLine(im,25,34,29,30,e[5]);pairLine(im,27,38,29,40,e[2])
 end
 -- Reactor tie rods and two grounded feed rails become visible after armor loss.
 pairRect(im,24,32,2,1,m[4]);pairRect(im,24,37,2,1,m[4]);rect(im,31,25,2,2,m[3]);rect(im,31,43,2,2,m[3])
end
local function blastArmor(im,state)
 if state=='armor_broken' then
  -- Missing plate: only a bolted outside rim and torn mounting teeth remain.
  poly(im,{{21,24},{24,24},{24,27},{22,27},{22,30},{21,31},{20,31},{20,27}},m[1])
  poly(im,{{21,25},{23,25},{23,26},{21,28}},m[5]);put(im,21,26,m[7])
  poly(im,{{20,36},{22,36},{22,39},{24,39},{24,42},{27,44},{27,46},{23,46},{20,42}},m[1])
  poly(im,{{21,37},{21,41},{24,44},{25,44},{25,45},{23,45},{21,42}},m[4])
  put(im,22,40,m[6]);put(im,24,44,m[6])
 else
  -- Two full blast covers create the broad armored chest and a small sight port.
  poly(im,{{23,23},{30,23},{31,27},{31,30},{28,32},{28,36},{31,39},{31,45},{27,47},{21,44},{19,39},{19,28}},m[1])
  poly(im,{{24,24},{29,24},{29,28},{30,29},{27,31},{26,34},{27,38},{29,40},{29,44},{27,45},{22,42},{21,38},{21,29}},m[6])
  line(im,24,24,29,24,m[7]);line(im,21,29,24,24,m[7]);line(im,22,41,27,44,m[4])
  poly(im,{{22,32},{24,30},{25,30},{24,33},{24,36},{25,39},{23,38},{22,36}},m[5])
  rect(im,22,28,2,2,m[3]);put(im,22,28,m[5]);rect(im,26,41,2,1,m[3])
 end
end
local function collar(im,state)
 if state=='armor_broken' then
  -- The forward cover is absent as well: black vault, rails and mounting sockets.
  poly(im,{{25,17},{38,17},{41,21},{39,24},{24,24},{22,21}},m[1])
  rect(im,26,19,12,3,m[2]);pairRect(im,25,20,3,1,m[4]);rect(im,30,20,4,1,m[3])
  pairPoly(im,{{22,22},{25,22},{26,24},{23,26},{21,25}},m[3]);pairRect(im,24,22,1,1,m[5])
 else
  poly(im,{{25,16},{38,16},{43,21},{40,26},{34,28},{29,28},{23,26},{20,21}},m[1])
  poly(im,{{26,17},{37,17},{41,21},{39,24},{34,26},{29,26},{24,24},{22,21}},m[6])
  line(im,26,17,37,17,m[7]);pairLine(im,22,21,26,17,m[7]);pairLine(im,24,24,29,26,m[4])
  poly(im,{{28,19},{35,19},{37,21},{34,23},{29,23},{26,21}},m[2])
  rect(im,29,20,6,1,state=='barrage_charged' and e[4] or e[2]);rect(im,30,21,4,1,e[1])
 end
 -- A solid aft crossbar supports the mobile defense-platform silhouette.
 poly(im,{{25,44},{38,44},{41,47},{38,50},{25,50},{22,47}},m[1])
 poly(im,{{26,45},{37,45},{39,47},{37,49},{26,49},{24,47}},m[5])
 rect(im,26,45,12,1,m[7]);rect(im,27,48,10,1,m[3]);rect(im,28,46,8,1,m[2])
end
local function power(im,state)
 local charged=state=='barrage_charged';local broken=state=='armor_broken';local p=broken and m[3] or charged and e[4] or e[2]
 pairRect(im,7,40,2,2,p);pairRect(im,13,44,5,1,p);rect(im,29,46,6,1,p)
 if charged then pairRect(im,7,40,1,1,e[6]);pairRect(im,13,44,3,1,e[5]) end
 if broken then
  pairLine(im,23,28,25,26,e[1]);pairLine(im,23,41,26,43,e[2]);rect(im,30,24,4,1,e[1])
 else
  -- A controlled narrow viewing port stays small even during a full barrage.
  diamond(im,31.5,34.5,4,m[1]);diamond(im,31.5,34.5,3,charged and e[3] or e[2]);diamond(im,31.5,34.5,2,charged and e[5] or e[4])
 end
end
local function registration(im,state)
 pairRect(im,8,30,2,1,m[3]);pairRect(im,8,32,2,1,m[3]);pairRect(im,8,43,1,1,m[4])
 pairRect(im,18,44,1,1,m[2]);pairRect(im,26,48,1,1,m[4])
 if state~='armor_broken' then pairRect(im,23,36,1,2,m[3]);pairRect(im,26,25,2,1,m[4]) end
end
local allLayers,flats={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 chassis(l[1]);perimeter(l[2]);battery(l[3],state.id);l[4]=mirror(l[3]);reactor(l[5],state.id)
 blastArmor(l[6],state.id);l[7]=mirror(l[6]);collar(l[8],state.id);power(l[9],state.id);registration(l[10],state.id)
 for _,index in ipairs({1,2,5,8,9,10}) do for y=0,63 do for x=0,31 do put(l[index],63-x,y,l[index]:getPixel(x,y)) end end end
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=layerNames[i];local doubled=scale(im,2);allLayers[state.id][i]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2;local stem='defense_overseer_'..state.id;flats[state.id]=Image(s)
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
master:saveAs(outputDir..'/defense_overseer_states.aseprite');master:close()
local sheet=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do sheet:drawImage(flats[state.id],Point((i-1)*128,0)) end;sheet:saveAs(outputDir..'/defense_overseer_states.png')
local preview=Image(1000,744,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'DEFENSE OVERSEER',32,24,m[6],4);text(preview,'SYSTEM / REGION B',34,57,e[4],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,96+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flats[state.id],2),Point(x+20,108));text(preview,state.label,x+math.floor((296-#state.label*8)/2),392,e[5],2)
 preview:drawImage(flats[state.id],Point(x+84,417));text(preview,'128 X 128',x+112,550,m[4],2)
end
rect(preview,28,580,944,1,m[3]);text(preview,'APPROVED ORANGE FAMILY',32,610,m[4],2)
preview:drawImage(support,Point(32,646));text(preview,'ASSAULT',110,672,m[5],2)
preview:drawImage(defense,Point(276,626));text(preview,'DEFENSE',384,672,m[5],2)
local comparisonA=assert(Image{fromFile=sourceDir..'/sector_administrator_idle.png'})
preview:drawImage(comparisonA,Point(610,600));text(preview,'REGION A',756,644,m[4],2);text(preview,'COMPARISON',756,664,m[4],2)
preview:saveAs(outputDir..'/defense_overseer_comparison.png')
if app.params.qaDir then
 local qa=Image(384,192,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,96,384,96,m[6])
 for i,state in ipairs(states) do
  local small=Image(64,64,ColorMode.RGB);for y=0,63 do for x=0,63 do put(small,x,y,flats[state.id]:getPixel(x*2,y*2)) end end
  qa:drawImage(small,Point(32+(i-1)*128,20));qa:drawImage(small,Point(32+(i-1)*128,116))
  text(qa,state.label,(i-1)*128+math.floor((128-#state.label*4)/2),6,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_64px.png')
end
local manifest={generatorId='void-scrapper-defense-overseer-v1',name='DEFENSE OVERSEER',faction='SYSTEM',region='B',family='orange',width=128,height=128,pixelScale=2,front='up / negative Y',anchor={x=64,y=64},reactorCenter={x=63.5,y=69.5},palette={metal=metalHex,energy=energyHex},layers=layerNames,assets={},
 referencePolicy='New broad connected hull and integrated triple batteries, authored independently of Sector Administrator. Approved Orange Assault and Orange Defense supply the palette and defense construction language; the approved diamond core is beneath the blast covers. No ReferenceOnly pixels copied or downscaled.',
 animationLayers={batteries={'Battery L','Battery R'},reactor='Reactor and Cradle',blastCovers={'Blast Armor L','Blast Armor R'},collar='Fore and Aft Armor'},
 master='defense_overseer_states.aseprite',sheet='defense_overseer_states.png',sheetLayout='Three horizontal 128x128 cells: idle, barrage_charged, armor_broken; no padding or trimming.',stateTimeline='Three tagged state poses, not a finished looping animation; 200ms per pose.',preview='defense_overseer_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='defense_overseer_'..state.id..'.aseprite',png='defense_overseer_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('DEFENSE OVERSEER: three states, layered sources, sprite sheet and comparison.\n');done:close()
print('DEFENSE_OVERSEER_GENERATED')
