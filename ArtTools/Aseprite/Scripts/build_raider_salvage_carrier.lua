-- RAIDER SALVAGE CARRIER: rear-heavy industrial cargo hauler, no gun batteries.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,scale,text=h.put,h.rect,h.line,h.poly,h.scale,h.text
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'171E25','303941','515F67','78888C','ADB9B7','D6DEDA','F0F2E9'}
local redHex={'662C32','B84445','ED7563'}
local heatHex={'6F3A26','E9913C','FFE0A2'}
local m,r,warm={},{},{};for i,v in ipairs(metalHex) do m[i]=rgba(v) end;for i,v in ipairs(redHex) do r[i]=rgba(v) end;for i,v in ipairs(heatHex) do warm[i]=rgba(v) end
local references={}
for _,id in ipairs({'basic','shotgun','sniper_charging','elite'}) do local im=assert(Image{fromFile=sourceDir..'/raider_'..id..'.png'});assert(im.width==64 and im.height==64,'Approved Raider size changed');references[id]=im end
local commander=assert(Image{fromFile=sourceDir..'/raider_assault_commander_idle.png'});assert(commander.width==128 and commander.height==128)
local colors={};for _,im in pairs(references) do for it in im:pixels() do if pc.rgbaA(it())>0 then colors[it()]=true end end end
for _,palette in ipairs({m,r,warm}) do for _,p in ipairs(palette) do assert(colors[p],'Color outside approved Raider palette') end end
local function beam(im,x0,y0,x1,y1,width,p)
 local length=math.max(math.abs(x1-x0),math.abs(y1-y0));local half=math.floor(width/2)
 for t=0,length do local u=length==0 and 0 or t/length;local x=math.floor(x0+(x1-x0)*u+0.5);local y=math.floor(y0+(y1-y0)*u+0.5);rect(im,x-half,y-half,width,width,p) end
end
local function boom(im,x0,y0,x1,y1)
 beam(im,x0,y0,x1,y1,5,m[1]);beam(im,x0,y0,x1,y1,3,m[3]);line(im,x0-1,y0,x1-1,y1,m[5])
end
local function pivot(im,x,y)
 rect(im,x-2,y-2,5,5,m[1]);rect(im,x-1,y-1,3,3,m[4]);put(im,x,y,m[2]);put(im,x-1,y-1,m[6])
end
local layerNames={'Main Hull','Cargo Contents and Machinery','Port Cargo Armor','Starboard Cargo Armor','Left Grapple','Right Magnet Crane','Intake Conveyor','Bridge and Docking Rails','Utility Drones','Engine Assemblies','Industrial Indicators','Damage and Debris'}
local states={{id='idle',label='IDLE',description='Rear cargo holds strapped shut; grapple and magnet parked; both utility drones docked.'},
 {id='salvage_active',label='SALVAGE ACTIVE',description='Grapple extends and opens, magnet swings inward, conveyor processes scrap, and one utility drone undocks.'},
 {id='cargo_overload',label='CARGO OVERLOAD',description='Cargo casings rupture, mixed scrap and damaged cells show through, one arm sags, and sparks and debris leak from storage.'}}
local function hull(im)
 -- Wide rear container sleds joined around an open-ended forward intake.
 poly(im,{{8,28},{17,26},{23,29},{25,26},{37,26},{41,29},{47,25},{57,28},{60,34},{61,51},{57,57},{43,58},{38,56},{25,57},{19,59},{7,57},{3,52},{3,35}},m[1])
 poly(im,{{9,30},{16,28},{23,31},{26,28},{36,28},{42,31},{48,27},{55,30},{58,35},{59,50},{55,55},{43,56},{38,54},{25,55},{18,57},{8,55},{5,51},{5,36}},m[3])
 rect(im,20,35,24,17,m[2]);rect(im,22,38,19,2,m[4]);rect(im,22,48,19,4,m[1]);rect(im,24,50,15,1,m[4])
 -- Two asymmetric crane turntables, with no forward barrels or firing apertures.
 poly(im,{{10,27},{17,27},{21,31},{20,36},{14,39},{9,35},{8,30}},m[1]);rect(im,11,29,7,6,m[4]);rect(im,12,30,5,4,m[2])
 poly(im,{{45,25},{52,26},{55,31},{52,36},{46,35},{42,31}},m[1]);rect(im,46,28,6,5,m[3]);rect(im,47,29,4,3,m[5])
 -- Boxed rear loading bumper, not a ram bow.
 rect(im,21,52,22,4,m[1]);rect(im,23,53,17,2,m[4]);rect(im,26,54,10,1,m[2])
end
local function cargoContents(im,state)
 local broken=state=='cargo_overload'
 -- Stacks of ingots, batteries and compacted scrap inside rectangular hold frames.
 rect(im,6,34,14,19,m[1]);rect(im,7,35,12,17,m[2]);rect(im,44,31,14,22,m[1]);rect(im,45,32,12,20,m[2])
 for _,q in ipairs({{8,36,5,4},{14,36,4,5},{8,42,7,3},{9,47,4,4},{14,47,4,3}}) do
  rect(im,q[1],q[2],q[3],q[4],m[3]);rect(im,q[1],q[2],q[3],1,m[5]);put(im,q[1]+1,q[2]+1,m[4])
 end
 line(im,13,40,16,43,m[5]);line(im,8,45,11,48,m[4]);rect(im,16,43,2,3,r[1]);put(im,16,43,r[2])
 for _,q in ipairs({{46,33,4,7},{52,34,4,5},{46,42,5,4},{52,41,3,9},{47,48,4,3}}) do
  rect(im,q[1],q[2],q[3],q[4],m[3]);rect(im,q[1],q[2],1,q[4],m[5]);rect(im,q[1]+1,q[2]+1,q[3]-1,1,broken and warm[2] or r[1])
 end
 if broken then
  rect(im,15,38,2,2,warm[2]);put(im,15,38,warm[3]);rect(im,9,48,2,2,warm[1]);put(im,10,48,warm[2])
  rect(im,48,34,1,4,warm[2]);put(im,48,34,warm[3]);rect(im,54,36,1,3,r[3]);rect(im,48,43,2,2,warm[2]);put(im,48,43,warm[3]);rect(im,53,43,2,4,warm[1]);put(im,53,43,warm[2])
 end
 -- External pipes and cable loops remain exposed between the containers.
 line(im,20,35,23,39,m[1]);line(im,23,39,21,46,m[1]);line(im,21,46,24,49,m[1]);line(im,21,35,24,39,r[1]);line(im,24,39,22,46,r[2]);line(im,22,46,25,49,r[1])
 line(im,41,32,43,39,m[4]);line(im,43,39,42,47,m[3]);rect(im,42,40,2,1,m[5]);rect(im,22,41,2,1,m[5])
end
local function portArmor(im,state)
 local broken=state=='cargo_overload'
 -- Long port container with a salvaged lid, locking bands and repair patches.
 poly(im,{{7,30},{16,30},{21,35},{20,52},{17,56},{7,55},{4,52},{4,35}},m[1])
 if not broken then
  poly(im,{{8,32},{15,32},{18,36},{18,50},{16,53},{8,52},{6,50},{6,36}},m[6])
  line(im,8,32,15,32,m[7]);rect(im,16,36,2,14,m[4]);rect(im,8,50,8,2,m[4])
  rect(im,6,38,13,2,m[2]);rect(im,7,38,11,1,m[4]);rect(im,6,47,13,2,m[2]);rect(im,7,47,11,1,m[4])
  rect(im,8,40,5,5,m[5]);put(im,8,40,m[3]);put(im,12,44,m[3]);rect(im,14,41,2,4,m[3])
  line(im,8,34,13,36,r[2]);line(im,8,35,10,36,r[3]);rect(im,8,49,4,1,r[2])
 else
  -- Keep only the bent outside rim and two torn tabs; contents are exposed.
  rect(im,6,35,2,16,m[5]);line(im,7,51,11,53,m[4]);rect(im,8,32,7,2,m[5]);rect(im,15,34,2,2,m[4])
  rect(im,17,38,2,7,m[4]);rect(im,16,49,2,3,m[3]);rect(im,8,34,3,1,r[2])
  -- Explicitly clear the broad lid area, including its original backing.
  for y=36,51 do for x=8,16 do put(im,x,y,0) end end
  rect(im,8,38,3,1,m[4]);rect(im,14,48,3,1,m[3]);put(im,9,39,m[5])
 end
 rect(im,5,36,1,3,m[3]);rect(im,17,32,1,2,r[1]);put(im,7,53,m[2])
end
local function starboardArmor(im,state)
 local broken=state=='cargo_overload'
 -- Shorter stacked storage cans, deliberately different from the long port box.
 poly(im,{{46,28},{55,29},{59,33},{60,50},{57,55},{45,54},{42,49},{43,34}},m[1])
 if not broken then
  poly(im,{{47,30},{54,31},{56,34},{57,49},{55,52},{47,51},{45,47},{45,35}},m[5])
  line(im,47,30,54,31,m[6]);rect(im,55,36,2,12,m[3]);rect(im,46,38,12,2,m[1]);rect(im,46,39,10,1,m[4])
  rect(im,47,33,4,4,m[6]);rect(im,52,33,2,4,m[3]);put(im,48,34,m[3])
  rect(im,47,42,7,7,m[4]);rect(im,48,43,5,4,m[6]);put(im,48,43,m[3]);put(im,52,46,m[3])
  line(im,49,33,51,36,r[2]);line(im,51,33,53,36,r[3]);rect(im,48,48,6,2,r[2]);put(im,52,49,m[5])
 else
  -- Ruptured doors have uneven edges, with exposed rectangular resource cells.
  rect(im,45,35,2,13,m[4]);rect(im,47,31,7,2,m[5]);line(im,54,32,56,35,m[3])
  rect(im,56,35,2,6,m[3]);rect(im,57,46,1,4,m[4]);rect(im,48,51,7,1,m[4]);put(im,46,48,m[5])
  for y=33,50 do for x=47,55 do put(im,x,y,0) end end
  rect(im,47,39,3,1,m[4]);rect(im,53,46,3,1,m[3]);put(im,54,32,r[1]);rect(im,48,51,3,1,r[2])
 end
end
local function leftGrapple(im,state)
 local active=state=='salvage_active';local broken=state=='cargo_overload'
 local ex,ey,cx,cy=13,23,22,17
 if active then ex,ey,cx,cy=12,19,25,10 elseif broken then ex,ey,cx,cy=11,26,19,29 end
 boom(im,14,32,ex,ey);boom(im,ex,ey,cx-3,cy);pivot(im,14,32);pivot(im,ex,ey)
 -- Hydraulic piston runs alongside the lower arm.
 line(im,16,30,ex+2,ey+1,m[1]);line(im,17,30,ex+3,ey+1,m[6]);rect(im,15,27,2,3,r[2])
 local spread=active and 5 or 3
 rect(im,cx-3,cy-2,4,5,m[1]);rect(im,cx-2,cy-1,2,3,m[4]);put(im,cx-2,cy-1,m[6])
 -- Angular pincer jaws wrap sideways around cargo, never read as gun tubes.
 poly(im,{{cx-1,cy-1},{cx+2,cy-spread},{cx+6,cy-spread},{cx+8,cy-2},{cx+6,cy-2},{cx+5,cy-spread+2},{cx+2,cy-spread+2},{cx+1,cy}},m[1])
 line(im,cx+1,cy-2,cx+3,cy-spread+1,m[6]);line(im,cx+3,cy-spread+1,cx+5,cy-spread+1,m[6]);put(im,cx+6,cy-3,m[4])
 poly(im,{{cx-1,cy+1},{cx+2,cy+spread},{cx+6,cy+spread},{cx+8,cy+2},{cx+6,cy+2},{cx+5,cy+spread-2},{cx+2,cy+spread-2},{cx+1,cy}},m[1])
 line(im,cx+1,cy+2,cx+3,cy+spread-1,m[5]);line(im,cx+3,cy+spread-1,cx+5,cy+spread-1,m[5]);put(im,cx+6,cy+3,m[3])
 rect(im,cx-2,cy,2,1,active and warm[2] or r[1]);put(im,ex,ey,active and r[3] or m[2])
end
local function magnet(im,state)
 local active=state=='salvage_active';local broken=state=='cargo_overload'
 local x,y=39,12;if active then x,y=34,7 elseif broken then x,y=42,18 end
 boom(im,49,30,49,21);boom(im,49,21,x+7,y+8);pivot(im,49,30);pivot(im,49,21)
 line(im,51,28,51,20,m[1]);line(im,51,20,x+8,y+6,m[1]);line(im,52,28,52,20,m[4]);line(im,52,20,x+9,y+6,m[4])
 -- Open horseshoe magnet: two square pole shoes joined at the back.
 rect(im,x,y,3,10,m[1]);rect(im,x+7,y,3,10,m[1]);rect(im,x,y+7,10,4,m[1])
 rect(im,x+1,y+1,2,7,m[5]);rect(im,x+7,y+1,2,7,m[6]);rect(im,x+2,y+8,6,2,m[4])
 rect(im,x+1,y+1,2,2,r[2]);rect(im,x+7,y+1,2,2,r[2]);put(im,x+7,y+1,r[3])
 rect(im,x+2,y+7,6,1,m[2]);rect(im,x+3,y+9,4,1,r[1]);put(im,x+1,y+5,m[3])
 if active then rect(im,x+1,y,2,1,warm[3]);rect(im,x+7,y,2,1,warm[3]);rect(im,x+3,y+9,4,1,warm[2]) end
 if broken then rect(im,x+7,y,3,4,0);rect(im,x+7,y+4,2,1,m[3]);put(im,x+7,y+3,m[4]);line(im,46,26,45,28,r[1]) end
end
local function intake(im,state)
 -- A forward-open rectangular conveyor trench leads into the cargo processing deck.
 poly(im,{{23,25},{26,23},{36,23},{39,27},{38,43},{34,47},{26,46},{22,41}},m[1])
 rect(im,25,25,11,18,m[2]);rect(im,24,26,2,16,m[5]);rect(im,36,27,2,15,m[4]);line(im,25,25,27,23,m[6])
 for y=27,42,3 do rect(im,27,y,8,1,m[4]);rect(im,27,y+1,8,1,m[1]) end
 rect(im,25,43,12,2,m[3]);rect(im,27,45,7,1,m[5]);rect(im,23,35,2,4,r[1]);rect(im,37,34,1,4,r[2])
 if state=='salvage_active' then
  -- Distinct flat scrap fragments on the moving rollers, never an energy diamond.
  poly(im,{{29,27},{33,27},{34,29},{32,31},{28,30}},m[5]);line(im,29,28,32,28,m[7]);put(im,31,30,r[1])
  rect(im,28,36,4,3,m[3]);rect(im,28,36,3,1,m[6]);put(im,32,35,m[5])
  for _,y in ipairs({26,32,39}) do rect(im,24,y,1,2,warm[2]);rect(im,37,y+1,1,2,warm[2]) end
 elseif state=='cargo_overload' then
  rect(im,27,37,8,2,m[1]);line(im,29,35,33,40,m[3]);put(im,30,36,m[5]);rect(im,25,40,2,2,m[1])
 end
end
local function bridge(im)
 -- Offset rectangular Raider cockpit and a welded radio, safely away from intake.
 poly(im,{{36,37},{40,37},{43,41},{42,48},{38,50},{35,47}},m[1])
 poly(im,{{37,38},{39,38},{41,41},{40,47},{38,48},{37,46}},m[5])
 for y=0,6 do for x=0,3 do put(im,37+x,39+y,references.elite:getPixel(28+x*2,12+y*2)) end end
 rect(im,41,39,1,6,m[3]);rect(im,41,34,1,6,m[1]);put(im,41,34,m[5])
 -- Twin utility-drone docking cradles across the rear deck.
 rect(im,23,45,10,7,m[1]);rect(im,24,46,8,5,m[3]);rect(im,24,46,1,5,m[5]);rect(im,31,46,1,5,m[4])
 rect(im,33,49,10,5,m[1]);rect(im,34,50,8,3,m[3]);rect(im,35,50,6,1,m[4])
end
local function drones(im,state)
 local function drone(x,y,off)
  rect(im,x+1,y,4,6,m[1]);rect(im,x+2,y+1,2,4,m[5]);rect(im,x,y+2,1,2,m[6]);rect(im,x+5,y+2,1,2,m[4])
  rect(im,x+2,y+2,2,1,off and m[2] or r[2]);put(im,x+2,y+1,m[6]);put(im,x+1,y+5,m[4]);put(im,x+4,y+5,m[4])
 end
 if state=='salvage_active' then drone(30,17,false) else drone(25,46,state=='cargo_overload') end
 drone(35,49,false)
end
local function engines(im,state)
 -- Stubby tow engines sit below the cargo sleds rather than leading the silhouette.
 poly(im,{{21,52},{28,52},{29,56},{27,60},{22,60},{20,57}},m[1]);rect(im,22,54,5,3,m[4]);rect(im,23,54,3,3,m[2]);rect(im,22,58,5,1,r[1])
 poly(im,{{36,54},{42,54},{44,57},{42,60},{36,59},{35,57}},m[1]);rect(im,37,55,4,3,m[3]);rect(im,37,55,4,1,m[5]);rect(im,37,58,4,1,r[1])
 if state=='salvage_active' then rect(im,23,59,3,1,r[3]);rect(im,38,59,3,1,r[2]) end
 if state=='cargo_overload' then rect(im,37,56,4,3,m[1]);put(im,37,56,m[4]) end
end
local function indicators(im,state)
 local active=state=='salvage_active';local broken=state=='cargo_overload'
 -- Fixed Raider paint survives cargo-door loss on the welded carrier frame.
 rect(im,23,53,5,1,r[2]);rect(im,33,53,6,1,r[2]);put(im,24,53,r[3])
 rect(im,5,42,1,4,r[2]);rect(im,58,42,1,5,r[2]);put(im,58,42,r[3]);rect(im,42,44,1,3,r[1])
 rect(im,19,36,1,3,active and warm[2] or r[1]);rect(im,43,35,1,3,broken and r[3] or r[1])
 rect(im,30,53,4,1,active and r[3] or r[1]);put(im,41,46,broken and r[3] or r[1])
 if active then
  -- Scrap held between the pincers and a small plate attracted to the magnet.
  poly(im,{{28,8},{30,9},{31,12},{28,13},{26,11}},m[4]);line(im,28,9,30,10,m[6]);put(im,29,12,r[1])
  rect(im,37,5,3,3,m[3]);rect(im,37,5,2,1,m[6]);put(im,38,7,m[4])
  line(im,36,11,37,12,warm[2]);line(im,41,11,40,12,warm[2]);put(im,38,15,warm[2])
  rect(im,19,37,1,1,warm[3]);put(im,24,33,warm[3]);put(im,37,40,warm[3])
 end
end
local function damage(im,state)
 if state~='cargo_overload' then return end
 -- Broken straps, escaping storage sparks, and spilled mixed debris.
 line(im,9,39,11,41,r[2]);line(im,11,41,10,44,r[1]);line(im,53,37,55,39,r[3]);put(im,54,41,m[5])
 line(im,54,44,57,42,warm[2]);put(im,54,44,warm[3]);put(im,59,40,warm[2]);put(im,59,37,warm[3])
 line(im,9,49,7,51,warm[2]);put(im,9,49,warm[3]);put(im,5,53,warm[2]);put(im,7,56,warm[3])
 poly(im,{{56,55},{58,55},{59,57},{57,58},{55,57}},m[3]);line(im,56,55,58,55,m[5]);put(im,57,56,r[1])
 rect(im,60,58,2,2,m[4]);put(im,60,58,m[6]);rect(im,53,59,3,2,m[2]);put(im,54,59,m[5]);put(im,57,61,warm[2])
 rect(im,3,57,2,2,m[3]);put(im,3,57,m[5]);put(im,8,59,m[4]);line(im,48,29,49,31,m[1])
end
local flats,allLayers={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 hull(l[1]);cargoContents(l[2],state.id);portArmor(l[3],state.id);starboardArmor(l[4],state.id)
 leftGrapple(l[5],state.id);magnet(l[6],state.id);intake(l[7],state.id);bridge(l[8]);drones(l[9],state.id);engines(l[10],state.id);indicators(l[11],state.id);damage(l[12],state.id)
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=layerNames[i];local doubled=scale(im,2);allLayers[state.id][i]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2;local stem='raider_salvage_carrier_'..state.id;flats[state.id]=Image(s)
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
master:saveAs(outputDir..'/raider_salvage_carrier_states.aseprite');master:close()
local strip=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flats[state.id],Point((i-1)*128,0)) end;strip:saveAs(outputDir..'/raider_salvage_carrier_states.png')
local preview=Image(1000,792,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'RAIDER SALVAGE CARRIER',32,24,m[6],3);text(preview,'HARVEST / PROCESS / HAUL',34,55,r[3],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,92+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flats[state.id],2),Point(x+20,104));text(preview,state.label,x+math.floor((296-#state.label*8)/2),389,r[3],2)
 preview:drawImage(flats[state.id],Point(x+84,414));text(preview,'128 X 128',x+112,550,m[4],2)
end
rect(preview,28,580,944,1,m[3]);text(preview,'APPROVED RAIDER FAMILY',32,602,m[4],2)
for i,ref in ipairs({{id='basic',label='BASIC'},{id='shotgun',label='SHOTGUN'},{id='sniper_charging',label='SNIPER'},{id='elite',label='ELITE'}}) do
 local x=84+(i-1)*171;preview:drawImage(references[ref.id],Point(x-32,661));text(preview,ref.label,x-#ref.label*4,766,m[5],2)
end
preview:drawImage(commander,Point(786,630));text(preview,'ASSAULT',822,766,m[5],2)
preview:saveAs(outputDir..'/raider_salvage_carrier_comparison.png')
if app.params.qaDir then
 local qa=Image(384,192,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,96,384,96,m[6])
 for i,state in ipairs(states) do
  local small=Image(64,64,ColorMode.RGB);for y=0,63 do for x=0,63 do put(small,x,y,flats[state.id]:getPixel(x*2,y*2)) end end
  qa:drawImage(small,Point(32+(i-1)*128,20));qa:drawImage(small,Point(32+(i-1)*128,116))
  text(qa,state.label,(i-1)*128+math.floor((128-#state.label*4)/2),6,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_64px.png')
end
local manifest={generatorId='void-scrapper-raider-salvage-carrier-v1',name='RAIDER SALVAGE CARRIER',faction='RAIDER',role='Replacement harvesting and cargo command ship',width=128,height=128,pixelScale=2,front='up / negative Y',anchor={x=64,y=64},palette={metal=metalHex,factionRed=redHex,industrialHeat=heatHex},layers=layerNames,assets={},
 referencePolicy='Approved Raider colors and rectangular cockpit motif. New cargo-hauler hull, grapple, magnet and conveyor geometry. Assault Commander is used for comparison only; no weapon or hull geometry is copied.',
 animationLayers={hull='Main Hull',cargo={'Port Cargo Armor','Starboard Cargo Armor','Cargo Contents and Machinery'},collectors={'Left Grapple','Right Magnet Crane','Intake Conveyor'},drones='Utility Drones',engine='Engine Assemblies',indicators='Industrial Indicators',damage='Damage and Debris'},
 master='raider_salvage_carrier_states.aseprite',sheet='raider_salvage_carrier_states.png',sheetLayout='Three horizontal 128x128 cells: idle, salvage_active, cargo_overload; no padding or trimming.',stateTimeline='Three tagged state poses, not a finished looping animation; 200ms per pose.',preview='raider_salvage_carrier_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='raider_salvage_carrier_'..state.id..'.aseprite',png='raider_salvage_carrier_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('RAIDER SALVAGE CARRIER: three states, layered sources, transparent strip and comparison.\n');done:close()
print('RAIDER_SALVAGE_CARRIER_GENERATED')
