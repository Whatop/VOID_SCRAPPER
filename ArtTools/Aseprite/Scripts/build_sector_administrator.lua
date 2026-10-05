-- SECTOR ADMINISTRATOR: Region A SYSTEM boss, authored on a 2x pixel grid.
-- Approved source COPIES define the palette and diamond/control-ring language.
-- ReferenceOnly assets are never sampled or downscaled by this generator.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local pc=app.pixelColor
local function rgba(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local metalHex={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local energyHex={'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'}
local metal,energy={},{}
for i,h in ipairs(metalHex) do metal[i]=rgba(h) end
for i,h in ipairs(energyHex) do energy[i]=rgba(h) end
local approved=assert(Image{fromFile=sourceDir..'/system_support_green.png'})
local control=assert(Image{fromFile=sourceDir..'/system_green_control_node.png'})
assert(approved.width==64 and approved.height==64 and control.width==64 and control.height==64)
local referenceColors={}
for _,im in ipairs({approved,control}) do for it in im:pixels() do if pc.rgbaA(it())>0 then referenceColors[it()]=true end end end
for _,index in ipairs({1,2,3,4,5,6,7}) do assert(referenceColors[metal[index]],'Approved SYSTEM metal palette changed') end
for _,index in ipairs({1,3,4,6}) do assert(referenceColors[energy[index]],'Approved green palette changed') end
local function put(im,x,y,p) assert(x>=0 and y>=0 and x<im.width and y<im.height,'Pixel outside canvas');im:drawPixel(x,y,p) end
local function rect(im,x,y,w,h,p) for yy=y,y+h-1 do for xx=x,x+w-1 do put(im,xx,yy,p) end end end
local function line(im,x0,y0,x1,y1,p)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0);local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1;local err=dx+dy
 while true do put(im,x0,y0,p);if x0==x1 and y0==y1 then break end;local e=2*err;if e>=dy then err=err+dy;x0=x0+sx end;if e<=dx then err=err+dx;y0=y0+sy end end
end
local function poly(im,points,p)
 local minY,maxY=im.height,-1;for _,q in ipairs(points) do minY=math.min(minY,q[2]);maxY=math.max(maxY,q[2]) end
 for y=minY,maxY do local nodes={};local j=#points
  for i=1,#points do local a,b=points[i],points[j];if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then nodes[#nodes+1]=a[1]+(y-a[2])/(b[2]-a[2])*(b[1]-a[1]) end;j=i end
  table.sort(nodes);for n=1,#nodes-1,2 do for x=math.ceil(nodes[n]),math.floor(nodes[n+1]) do put(im,x,y,p) end end
 end
 for i=1,#points do local a,b=points[i],points[i%#points+1];line(im,a[1],a[2],b[1],b[2],p) end
end
local function pairRect(im,x,y,w,h,p) rect(im,x,y,w,h,p);rect(im,im.width-x-w,y,w,h,p) end
local function pairLine(im,x0,y0,x1,y1,p) line(im,x0,y0,x1,y1,p);line(im,im.width-1-x0,y0,im.width-1-x1,y1,p) end
local function pairPoly(im,points,p) poly(im,points,p);local q={};for _,v in ipairs(points) do q[#q+1]={im.width-1-v[1],v[2]} end;poly(im,q,p) end
local function oct(im,x,y,w,h,c,p) poly(im,{{x+c,y},{x+w-1-c,y},{x+w-1,y+c},{x+w-1,y+h-1-c},{x+w-1-c,y+h-1},{x+c,y+h-1},{x,y+h-1-c},{x,y+c}},p) end
local function diamond(im,cx,cy,r,p)
 for y=math.floor(cy-r),math.ceil(cy+r) do for x=math.floor(cx-r),math.ceil(cx+r) do if math.abs(x-cx)+math.abs(y-cy)<=r then put(im,x,y,p) end end end
end
local function mirror(im) local result=Image(im.width,im.height,ColorMode.RGB);for it in im:pixels() do put(result,im.width-1-it.x,it.y,it()) end;return result end
local function scale(im,n) local result=Image(im.width*n,im.height*n,ColorMode.RGB);for it in im:pixels() do if pc.rgbaA(it())>0 then rect(result,it.x*n,it.y*n,n,n,it()) end end;return result end
local layerNames={'Structural Spine','Central Armor','Laser Module L','Laser Module R','Containment Pylon L','Containment Pylon R','Control Core','Armored Iris','Energy Routing','SYSTEM Registration'}
local states={{id='idle',label='IDLE',description='Stable command core; shield gates closed; laser optics at standby.'},
 {id='charged',label='CHARGED',description='Green routing lit; laser shutters spread; containment gates open and armed.'},
 {id='exposed_core',label='EXPOSED CORE',description='Retracted armored iris exposes the large diamond reactor; outer weapons powered down.'}}
local function spine(im)
 -- Narrow, unarmored bridges leave large visible gaps around all four modules.
 pairPoly(im,{{13,21},{22,21},{26,25},{23,28},{19,25},{13,25}},metal[1])
 pairPoly(im,{{14,22},{21,22},{24,25},{22,26},{19,24},{14,24}},metal[3])
 pairLine(im,17,22,21,22,metal[5])
 pairPoly(im,{{14,40},{20,40},{25,37},{28,40},{22,44},{14,44}},metal[1])
 pairPoly(im,{{15,41},{20,41},{24,39},{25,40},{21,43},{15,43}},metal[3])
 pairLine(im,17,41,20,41,metal[5])
 oct(im,22,12,20,44,5,metal[1]);oct(im,23,13,18,42,4,metal[3])
 -- Short stern management terminal, not an engine/exhaust.
 poly(im,{{25,48},{38,48},{39,56},{36,59},{27,59},{24,56}},metal[1])
 poly(im,{{26,49},{37,49},{38,55},{35,57},{28,57},{25,55}},metal[3])
 rect(im,28,55,8,2,metal[2]);pairRect(im,27,52,2,2,metal[5])
end
local function centralArmor(im)
 oct(im,21,18,22,30,6,metal[1]);oct(im,22,19,20,28,5,metal[5]);oct(im,23,20,18,26,4,metal[2])
 -- An upward-pointing crown gives an unambiguous front direction.
 poly(im,{{31,7},{32,7},{39,14},{39,18},{36,21},{27,21},{24,18},{24,14}},metal[1])
 poly(im,{{31,9},{32,9},{37,14},{37,17},{35,19},{28,19},{26,17},{26,14}},metal[6])
 pairLine(im,26,14,30,10,metal[7]);pairLine(im,26,17,28,19,metal[4])
 poly(im,{{31,12},{32,12},{35,15},{34,16},{29,16},{28,15}},metal[2])
 rect(im,30,17,4,1,metal[4])
 -- Lower command collar repeats the station's segmented defensive ring.
 poly(im,{{26,43},{37,43},{40,47},{37,52},{26,52},{23,47}},metal[1])
 poly(im,{{27,44},{36,44},{38,47},{36,50},{27,50},{25,47}},metal[6])
 rect(im,27,44,10,1,metal[7]);rect(im,28,50,8,1,metal[4])
 rect(im,28,46,8,2,metal[2]);pairRect(im,26,46,1,2,metal[4])
end
local function laser(im,state)
 local charged=state=='charged';local exposed=state=='exposed_core'
 -- Single compact forward emitter with two white focusing prongs.
 poly(im,{{8,6},{13,6},{17,11},{17,24},{14,28},{7,28},{4,24},{4,12}},metal[1])
 poly(im,{{8,8},{12,8},{15,11},{15,23},{13,26},{8,26},{6,23},{6,12}},metal[4])
 poly(im,{{6,14},{8,12},{13,12},{15,14},{15,22},{12,25},{8,25},{6,23}},metal[6])
 line(im,6,14,8,12,metal[7]);line(im,8,12,13,12,metal[7]);rect(im,14,16,1,6,metal[4])
 oct(im,8,15,5,9,1,metal[2]);rect(im,9,17,3,4,energy[1]);rect(im,10,16,1,6,exposed and energy[1] or energy[3])
 rect(im,8,25,5,1,metal[3]);rect(im,9,26,3,1,metal[2])
 -- Standby lens and charged optic change both aperture and jaw silhouette.
 local spread=charged and 1 or 0
 poly(im,{{7-spread,6},{9-spread,6},{9-spread,13},{7-spread,15},{6-spread,14},{6-spread,8}},metal[1])
 rect(im,7-spread,7,2,6,metal[6]);rect(im,7-spread,7,1,5,metal[7])
 poly(im,{{12+spread,6},{14+spread,6},{15+spread,8},{15+spread,14},{14+spread,15},{12+spread,13}},metal[1])
 rect(im,12+spread,7,2,6,metal[6]);rect(im,13+spread,7,1,5,metal[7])
 rect(im,9,7,3,7,metal[2]);rect(im,10,7,1,7,exposed and energy[1] or energy[3])
 if charged then rect(im,9,7,3,2,energy[5]);rect(im,10,9,1,6,energy[6]);rect(im,9,18,3,3,energy[4]);put(im,10,18,energy[6])
 else rect(im,9,11,3,2,metal[3]);put(im,10,8,exposed and energy[1] or energy[4]) end
 rect(im,6,21,2,1,metal[3]);rect(im,6,23,2,1,metal[3])
end
local function containment(im,state)
 local charged=state=='charged';local exposed=state=='exposed_core'
 -- Broad field-generator fins form the heavier rear pair of major modules.
 poly(im,{{7,35},{12,35},{17,40},{17,49},{13,55},{6,55},{2,51},{2,41}},metal[1])
 poly(im,{{7,37},{11,37},{15,41},{15,48},{12,53},{7,53},{4,50},{4,42}},metal[3])
 poly(im,{{6,38},{9,38},{9,41},{6,44},{6,49},{9,52},{7,53},{3,49},{3,43}},metal[6])
 line(im,6,38,8,38,metal[7]);line(im,4,42,6,40,metal[7]);line(im,4,49,7,52,metal[4])
 poly(im,{{12,38},{15,41},{15,48},{12,52},{10,52},{10,49},{12,46},{12,43},{10,41},{10,38}},metal[6])
 line(im,12,39,14,41,metal[7]);line(im,14,46,12,49,metal[4])
 rect(im,7,43,4,6,metal[1]);rect(im,8,43,2,6,energy[1])
 if charged then
  rect(im,7,44,4,4,energy[3]);rect(im,8,44,2,4,energy[5]);rect(im,8,45,2,2,energy[6])
  -- Open two containment gates along the outer edge, with a crisp energy slot.
  rect(im,2,42,2,8,metal[1]);rect(im,2,42,1,8,metal[6]);rect(im,3,43,1,6,energy[4])
  rect(im,12,44,2,3,energy[4]);rect(im,12,45,1,1,energy[6])
 else
  rect(im,7,44,4,1,metal[5]);rect(im,7,47,4,1,metal[4]);rect(im,8,45,2,2,exposed and energy[1] or energy[3])
 end
 rect(im,7,39,2,2,metal[2]);put(im,7,39,metal[4]);rect(im,7,51,4,1,metal[2])
end
local function controlCore(im,state)
 -- Large stable reactor, same nested diamond motif as the approved family.
 diamond(im,31.5,31.5,13,metal[1]);diamond(im,31.5,31.5,12,metal[4]);diamond(im,31.5,31.5,11,metal[2])
 diamond(im,31.5,31.5,10,energy[1]);diamond(im,31.5,31.5,8,energy[2]);diamond(im,31.5,31.5,6,energy[3])
 -- Keep the exact approved 12x12 logical diamond center in idle mode.
 for y=10,21 do for x=10,21 do if math.abs(x-15.5)+math.abs(y-15.5)<=6 then
  put(im,26+x-10,26+y-10,approved:getPixel(x*2,y*2))
 end end end
 if state=='charged' then
  diamond(im,31.5,31.5,9,energy[3]);diamond(im,31.5,31.5,7,energy[4]);diamond(im,31.5,31.5,4,energy[5]);diamond(im,31.5,31.5,2,energy[6])
  pairLine(im,24,31,29,26,energy[5]);pairLine(im,25,33,29,37,energy[2])
 elseif state=='exposed_core' then
  diamond(im,31.5,31.5,11,energy[1]);diamond(im,31.5,31.5,9,energy[4]);diamond(im,31.5,31.5,7,energy[2])
  diamond(im,31.5,31.5,6,energy[3]);diamond(im,31.5,31.5,4,energy[5]);diamond(im,31.5,31.5,2,energy[6])
  pairLine(im,23,31,29,25,energy[5]);pairLine(im,25,34,29,38,energy[3])
  pairRect(im,21,31,2,2,metal[2]);rect(im,31,21,2,2,metal[2]);rect(im,31,40,2,2,metal[2])
 end
end
local function iris(im,state)
 -- Four diagonal armor shutters: retract visibly to expose the reactor.
 local d=state=='exposed_core' and 3 or state=='charged' and 1 or 0
 local upper={{27-d,20-d},{30-d,20-d},{30-d,26-d},{26-d,30-d},{20-d,30-d},{20-d,27-d}}
 pairPoly(im,upper,metal[1])
 pairPoly(im,{{27-d,21-d},{29-d,21-d},{29-d,26-d},{26-d,29-d},{21-d,29-d},{21-d,27-d}},metal[6])
 pairLine(im,21-d,27-d,27-d,21-d,metal[7]);pairLine(im,27-d,21-d,29-d,21-d,metal[7])
 pairLine(im,25-d,29-d,29-d,25-d,metal[4])
 pairPoly(im,{{20-d,33+d},{26-d,33+d},{30-d,37+d},{30-d,43+d},{27-d,43+d},{20-d,36+d}},metal[1])
 pairPoly(im,{{21-d,34+d},{26-d,34+d},{29-d,37+d},{29-d,42+d},{27-d,42+d},{21-d,36+d}},metal[5])
 pairLine(im,22-d,34+d,26-d,34+d,metal[6]);pairLine(im,26-d,34+d,29-d,37+d,metal[6])
 pairLine(im,22-d,37+d,27-d,42+d,metal[3])
end
local function routing(im,state)
 local lit=state=='charged';local off=state=='exposed_core';local p=off and energy[1] or lit and energy[4] or energy[2]
 pairLine(im,17,23,20,23,p);pairLine(im,20,23,22,25,p)
 pairLine(im,17,42,20,42,p);pairLine(im,20,42,23,40,p)
 rect(im,30,14,4,1,off and energy[1] or energy[3]);rect(im,31,13,2,1,off and energy[1] or energy[4])
 rect(im,29,46,6,1,p);rect(im,30,55,4,1,p)
 -- Four cardinal registration sockets echo the Green Control node.
 pairRect(im,21,30,1,4,metal[1]);pairRect(im,21,31,1,2,off and energy[1] or lit and energy[5] or energy[3])
 rect(im,30,21,4,1,metal[1]);rect(im,31,21,2,1,off and energy[1] or lit and energy[5] or energy[3])
 rect(im,30,42,4,1,metal[1]);rect(im,31,42,2,1,off and energy[1] or lit and energy[5] or energy[3])
end
local function registration(im)
 -- Sparse official markings; no wear, asymmetric patches, or faction-red paint.
 pairRect(im,7,18,1,2,metal[3]);pairRect(im,12,23,1,1,metal[4])
 pairRect(im,4,46,1,2,metal[4]);pairRect(im,13,50,1,1,metal[2])
 pairRect(im,28,18,2,1,metal[3]);pairRect(im,27,48,2,1,metal[4])
end
local allLayers,flattened={},{}
for _,state in ipairs(states) do
 local l={};for i=1,#layerNames do l[i]=Image(64,64,ColorMode.RGB) end
 spine(l[1]);centralArmor(l[2]);laser(l[3],state.id);l[4]=mirror(l[3]);containment(l[5],state.id);l[6]=mirror(l[5])
 controlCore(l[7],state.id);iris(l[8],state.id);routing(l[9],state.id);registration(l[10])
 -- Resolve polygon edge rounding to the same bilateral construction grid.
 for _,index in ipairs({1,2,7,8,9,10}) do for y=0,63 do for x=0,31 do put(l[index],63-x,y,l[index]:getPixel(x,y)) end end end
 local s=Sprite(128,128,ColorMode.RGB);allLayers[state.id]={}
 for index,im in ipairs(l) do local layer=index==1 and s.layers[1] or s:newLayer();layer.name=layerNames[index];local doubled=scale(im,2);allLayers[state.id][index]=doubled;s:newCel(layer,1,doubled,Point(0,0)) end
 s.frames[1].duration=0.2
 local stem='sector_administrator_'..state.id
 local flat=Image(s);flattened[state.id]=flat;s:saveAs(outputDir..'/'..stem..'.aseprite');flat:saveAs(outputDir..'/'..stem..'.png');s:close()
end
local master=Sprite(128,128,ColorMode.RGB)
for index,name in ipairs(layerNames) do local layer=index==1 and master.layers[1] or master:newLayer();layer.name=name end
for frame,state in ipairs(states) do
 if frame>1 then master:newEmptyFrame(frame) end
 for index,im in ipairs(allLayers[state.id]) do master:newCel(master.layers[index],frame,im,Point(0,0)) end
 master.frames[frame].duration=0.2
end
for frame,state in ipairs(states) do local tag=master:newTag(frame,frame);tag.name=state.id end
master:saveAs(outputDir..'/sector_administrator_states.aseprite');master:close()
local strip=Image(384,128,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flattened[state.id],Point((i-1)*128,0)) end
strip:saveAs(outputDir..'/sector_administrator_states.png')
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},Q={'010','101','101','111','011'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},['2']={'110','001','010','100','111'},['3']={'110','001','010','001','110'},['4']={'101','101','111','001','001'},['6']={'011','100','110','101','010'},['8']={'010','101','010','101','010'},[' ']={'000','000','000','000','000'},['/']={'001','001','010','100','100'}}
local function text(im,label,x,y,color,n)
 for i=1,#label do local glyph=assert(font[label:sub(i,i)],'Font glyph missing: '..label:sub(i,i));for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then rect(im,x+(i-1)*4*n+(xx-1)*n,y+(yy-1)*n,n,n,color) end end end end
end
local preview=Image(1000,700,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'SECTOR ADMINISTRATOR',32,24,metal[6],4);text(preview,'SYSTEM / REGION A',34,57,energy[4],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,state in ipairs(states) do
 local x=28+(i-1)*324
 for y=0,279 do for xx=0,295 do put(preview,x+xx,96+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 preview:drawImage(scale(flattened[state.id],2),Point(x+20,108));text(preview,state.label,x+math.floor((296-#state.label*8)/2),392,energy[5],2)
 preview:drawImage(flattened[state.id],Point(x+84,427));text(preview,'128 X 128',x+112,568,metal[4],2)
end
rect(preview,28,600,944,1,metal[3]);text(preview,'APPROVED FAMILY',32,632,metal[4],2)
preview:drawImage(approved,Point(300,619));text(preview,'GREEN GUARD',380,646,metal[5],2)
preview:drawImage(control,Point(600,619));text(preview,'GREEN CONTROL',680,646,metal[5],2)
preview:saveAs(outputDir..'/sector_administrator_comparison.png')
local manifest={generatorId='void-scrapper-sector-administrator-v1',name='SECTOR ADMINISTRATOR',faction='SYSTEM',region='A',family='green',width=128,height=128,pixelScale=2,front='up / negative Y',anchor={x=64,y=64},coreCenter={x=63.5,y=63.5},palette={metal=metalHex,energy=energyHex},layers=layerNames,assets={},
 referencePolicy='Approved Green Guard and Green Control copies define the palette and diamond/segmented-ring motif. The idle reactor retains their exact diamond center. Large ReferenceOnly ships inform broad composition only; no pixels copied or downscaled.',
 modulePivots={{layer='Laser Module L',x=21,y=36},{layer='Laser Module R',x=106,y=36},{layer='Containment Pylon L',x=19,y=90},{layer='Containment Pylon R',x=108,y=90}},
 master='sector_administrator_states.aseprite',sheet='sector_administrator_states.png',sheetLayout='3 horizontal cells: idle, charged, exposed_core; 128x128 each; no padding',stateTimeline='Three tagged pose references, not a finished looping animation. 200ms per pose.',preview='sector_administrator_comparison.png'}
for i,state in ipairs(states) do manifest.assets[#manifest.assets+1]={id=state.id,label=state.label,description=state.description,width=128,height=128,frame=i,aseprite='sector_administrator_'..state.id..'.aseprite',png='sector_administrator_'..state.id..'.png'} end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('SECTOR ADMINISTRATOR: three poses, layered sources and comparison.\n');done:close()
print('SECTOR_ADMINISTRATOR_GENERATED')
