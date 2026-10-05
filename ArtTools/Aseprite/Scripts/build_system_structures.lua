-- SYSTEM structures: hand-authored geometry on a 2x pixel grid.
-- Approved support PNG copies provide exact diamond core modules and palettes.
-- ReferenceOnly assets are NOT loaded here: they inform broad layouts only.
local srcDir=assert(app.params.sourceDir)
local outDir=assert(app.params.outputDir)
local pc=app.pixelColor
local function rgba(hex) return pc.rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255) end
local function palette(list) local p={};for _,h in ipairs(list) do p[#p+1]=rgba(h) end;return p end
local metal=palette{'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local units={
 {id='green_control_node',family='green',label='GREEN CONTROL',size=32,role='Guard and control',energy=palette{'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'}},
 {id='orange_defense_node',family='orange',label='ORANGE DEFENSE',size=48,role='Fortified defense and weapon support',energy=palette{'553020','995028','DB8432','FFBA53','FFE397','FFF7DA'}},
 {id='blue_navigation_node',family='blue',label='BLUE NAVIGATION',size=48,role='Sensors, navigation and routing',energy=palette{'163454','285E91','388ED1','68C7ED','ABE8F4','E8FAFF'}}
}
local function put(im,x,y,p) assert(x>=0 and y>=0 and x<im.width and y<im.height,'Out-of-canvas drawing');im:drawPixel(x,y,p) end
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
 for i=1,#points do local a,b=points[i],points[(i%#points)+1];line(im,a[1],a[2],b[1],b[2],p) end
end
local function pairPut(im,x,y,p) put(im,x,y,p);put(im,im.width-1-x,y,p) end
local function pairRect(im,x,y,w,h,p) for yy=y,y+h-1 do for xx=x,x+w-1 do pairPut(im,xx,yy,p) end end end
local function pairPoly(im,pts,p) poly(im,pts,p);local right={};for _,q in ipairs(pts) do right[#right+1]={im.width-1-q[1],q[2]} end;poly(im,right,p) end
local function pairLine(im,x0,y0,x1,y1,p) line(im,x0,y0,x1,y1,p);line(im,im.width-1-x0,y0,im.width-1-x1,y1,p) end
local function octagon(im,x,y,w,h,cut,p) poly(im,{{x+cut,y},{x+w-1-cut,y},{x+w-1,y+cut},{x+w-1,y+h-1-cut},{x+w-1-cut,y+h-1},{x+cut,y+h-1},{x,y+h-1-cut},{x,y+cut}},p) end
local function circle(im,cx,cy,r,p)
 for y=cy-r,cy+r do for x=cx-r,cx+r do if (x-cx)^2+(y-cy)^2<=r*r then put(im,x,y,p) end end end
end
local function rotate(x,y,size,q)
 for _=1,q do x,y=size-1-y,x end;return x,y
end
local function radialRect(im,x,y,w,h,p)
 for q=0,3 do for yy=y,y+h-1 do for xx=x,x+w-1 do local rx,ry=rotate(xx,yy,im.width,q);put(im,rx,ry,p) end end end
end
local function radialPoly(im,pts,p)
 for q=0,3 do local r={};for _,point in ipairs(pts) do local x,y=rotate(point[1],point[2],im.width,q);r[#r+1]={x,y} end;poly(im,r,p) end
end
local function scale(im,n)
 local out=Image(im.width*n,im.height*n,ColorMode.RGB)
 for it in im:pixels() do if pc.rgbaA(it())>0 then rect(out,it.x*n,it.y*n,n,n,it()) end end
 return out
end
local function layers(size)
 local result={};for i=1,5 do result[i]=Image(size,size,ColorMode.RGB) end;return result
end
local function coreModule(u,l)
 local approved=assert(Image{fromFile=srcDir..'/system_support_'..u.family..'.png'})
 assert(approved.width==64 and approved.height==64,'Approved support image must be 64x64')
 local target=(u.size-12)/2
 for y=10,21 do for x=10,21 do
  if math.abs(x-15.5)+math.abs(y-15.5)<=6 then
   local p=approved:getPixel(x*2,y*2);assert(pc.rgbaA(p)==255,'Approved core module contains a gap')
   put(l[4],target+x-10,target+y-10,p)
  end
 end end
 return approved
end
local function greenControl(u,l)
 local base,armor,modules,energy=l[1],l[2],l[3],l[5]
 -- Compact octagonal foundation and four continuous defensive guard segments.
 octagon(base,2,2,28,28,8,metal[1]);octagon(base,3,3,26,26,7,metal[3]);octagon(base,5,5,22,22,5,metal[2])
 radialRect(base,13,6,6,7,metal[3]);radialRect(base,14,7,4,5,metal[4])
 radialPoly(armor,{{10,3},{21,3},{25,7},{23,9},{8,9},{6,7}},metal[1])
 radialPoly(armor,{{10,4},{21,4},{23,6},{23,7},{21,8},{10,8},{8,7},{8,6}},metal[6])
 radialRect(armor,10,4,12,1,metal[7]);radialRect(armor,10,8,12,1,metal[4])
 -- Shield/control status bars are embedded in the housing, with no loose effects.
 radialRect(energy,12,5,8,1,u.energy[3]);radialRect(energy,13,6,6,1,u.energy[1])
 radialRect(modules,9,6,2,1,metal[4]);radialRect(modules,21,6,2,1,metal[4])
 octagon(armor,8,8,16,16,5,metal[1]);octagon(armor,9,9,14,14,4,metal[5]);octagon(armor,10,10,12,12,3,metal[2])
 -- Four fixed corner anchoring sockets distinguish the node from a flying drone.
 for _,p in ipairs({{6,6},{25,6},{6,25},{25,25}}) do
  rect(modules,p[1]-1,p[2]-1,3,3,metal[2]);put(modules,p[1],p[2],metal[5])
 end
 -- Resolve the corner overlaps of rotated guard segments with mirrored shading.
 for _,im in ipairs({base,armor,modules,energy}) do
  for y=0,31 do for x=0,15 do put(im,31-x,y,im:getPixel(x,y)) end end
 end
end
local function orangeDefense(u,l)
 local base,armor,modules,energy=l[1],l[2],l[3],l[5]
 -- A broad stationary foundation with reinforced corner blocks and rear footings.
 octagon(base,3,7,42,37,5,metal[1]);octagon(base,4,8,40,34,4,metal[3]);octagon(base,7,10,34,30,3,metal[2])
 pairRect(base,7,40,8,5,metal[1]);pairRect(armor,8,41,6,3,metal[5]);pairRect(armor,9,41,4,1,metal[7])
 pairPoly(armor,{{5,17},{11,17},{13,20},{13,34},{10,38},{5,38},{3,35},{3,20}},metal[1])
 pairPoly(armor,{{5,18},{10,18},{12,21},{12,33},{9,36},{5,36},{4,34},{4,21}},metal[6])
 pairLine(armor,5,18,10,18,metal[7]);pairRect(armor,11,23,1,9,metal[4])
 poly(armor,{{12,35},{35,35},{39,39},{37,42},{10,42},{8,39}},metal[1])
 poly(armor,{{13,36},{34,36},{37,39},{35,41},{12,41},{10,39}},metal[6])
 rect(armor,14,36,20,1,metal[7]);rect(armor,14,40,20,1,metal[4])
 -- Paired upper bastions contain the two visible twin-bore weapon emplacements.
 pairPoly(armor,{{6,8},{13,8},{16,11},{16,19},{13,22},{6,22},{3,19},{3,11}},metal[1])
 pairPoly(armor,{{7,9},{12,9},{14,11},{14,18},{12,20},{7,20},{5,18},{5,11}},metal[6])
 pairLine(armor,7,9,12,9,metal[7]);pairRect(armor,13,12,1,6,metal[4])
 for _,x in ipairs({6,11}) do
  pairRect(modules,x,3,3,10,metal[1]);pairRect(modules,x,4,2,7,metal[5]);pairRect(modules,x,4,1,6,metal[7])
  pairRect(modules,x,3,2,1,metal[2]);pairRect(modules,x,10,3,2,metal[3])
 end
 pairRect(modules,7,13,6,4,metal[2]);pairRect(modules,8,17,4,1,metal[4])
 pairRect(energy,8,14,4,1,u.energy[3]);pairPut(energy,8,14,u.energy[4])
 pairRect(modules,5,26,5,6,metal[3]);pairRect(armor,5,26,1,6,metal[7])
 pairPoly(energy,{{6,27},{7,27},{9,29},{9,30},{8,30},{6,28}},u.energy[3])
 -- Central command core has a continuous armored surround and fixed routing bus.
 octagon(armor,15,15,18,19,4,metal[1]);octagon(armor,16,16,16,17,4,metal[5]);octagon(armor,17,17,14,15,3,metal[2])
 pairRect(modules,15,24,2,5,metal[4]);pairRect(energy,15,25,1,3,u.energy[3])
 rect(energy,21,33,6,1,u.energy[3]);rect(modules,20,37,8,2,metal[2]);rect(energy,22,37,4,1,u.energy[1])
 pairRect(modules,14,38,3,1,metal[3])
end
local function blueNavigation(u,l)
 local base,armor,modules,energy=l[1],l[2],l[3],l[5]
 -- Open routing cross: separated sensors, a compact hub, and a terminal spine.
 pairRect(base,8,21,12,6,metal[1]);pairRect(base,9,22,10,4,metal[3]);pairRect(armor,11,22,8,1,metal[6])
 rect(base,20,9,8,31,metal[1]);rect(base,21,10,6,29,metal[3]);rect(armor,22,12,4,5,metal[5])
 pairPoly(base,{{12,13},{16,13},{19,17},{16,20},{12,17}},metal[1])
 pairPoly(armor,{{13,14},{15,14},{17,17},{15,18},{13,16}},metal[6])
 pairPoly(base,{{12,31},{16,29},{19,32},{16,36},{12,35}},metal[1])
 pairPoly(armor,{{13,32},{15,31},{17,33},{15,35},{13,34}},metal[5])
 octagon(armor,14,14,20,20,6,metal[1]);octagon(armor,15,15,18,18,5,metal[6]);octagon(armor,17,17,14,14,4,metal[4])
 pairLine(armor,20,15,16,19,metal[7]);pairRect(modules,15,22,1,4,metal[3])
 -- Two circular sensor dishes have a simple stepped rim and a crosshair center.
 for side=0,1 do
  local cx=side==0 and 8 or 39;local cy=24
  circle(modules,cx,cy,6,metal[1]);circle(modules,cx,cy,5,metal[5]);circle(modules,cx,cy,4,metal[6]);circle(modules,cx,cy,3,metal[2])
  line(modules,cx-2,cy-4,cx+2,cy-4,metal[7]);line(modules,cx-2,cy+4,cx+2,cy+4,metal[4])
  rect(energy,cx-1,cy-1,3,3,u.energy[3]);put(energy,cx,cy,u.energy[5])
  put(modules,cx,cy-2,metal[4]);put(modules,cx,cy+2,metal[4]);put(modules,cx-2,cy,metal[4]);put(modules,cx+2,cy,metal[4])
 end
 -- Thin T-capped aerials and linked mounts read as sensors rather than cannons.
 pairRect(modules,18,7,4,8,metal[1]);pairRect(armor,19,8,2,6,metal[5]);pairRect(armor,19,8,1,5,metal[7])
 pairRect(modules,19,2,2,7,metal[2]);pairRect(modules,17,3,6,2,metal[1]);pairRect(armor,18,3,4,1,metal[6])
 pairRect(energy,19,5,1,3,u.energy[3]);pairPut(energy,19,5,u.energy[5])
 pairRect(modules,17,12,4,2,metal[3]);rect(energy,23,10,2,3,u.energy[3])
 -- The lower routing terminal is an anchored block with explicit data lanes.
 octagon(armor,17,35,14,9,2,metal[1]);octagon(armor,18,36,12,7,2,metal[6]);rect(armor,20,36,8,1,metal[7])
 rect(modules,21,38,6,3,metal[2]);rect(energy,22,38,4,1,u.energy[3]);rect(energy,22,40,4,1,u.energy[1])
 rect(modules,22,32,4,4,metal[2]);rect(energy,23,32,2,3,u.energy[3]);pairRect(energy,11,24,3,1,u.energy[3])
end
local manifest={generatorId='void-scrapper-system-structures-v1',faction='System',assets={},
 referencePolicy='ReferenceOnly images were inspected for broad layout ideas only. No ReferenceOnly pixels are sampled or downscaled.',
 approvedSources={'Output/04_SystemSupport/system_support_green.png','Output/04_SystemSupport/system_support_orange.png','Output/04_SystemSupport/system_support_blue.png'},
 approvedCoreCrop={x=20,y=20,w=24,h=24,logicalMask='abs(x-15.5)+abs(y-15.5)<=6 on the 32px support grid'}}
local frames={}
local names={'Foundation and Routing','SYSTEM Armor','Role Modules','Approved Core Module','Energy and Registration'}
for _,u in ipairs(units) do
 local l=layers(u.size)
 if u.family=='green' then greenControl(u,l) elseif u.family=='orange' then orangeDefense(u,l) else blueNavigation(u,l) end
 coreModule(u,l)
 local s=Sprite(u.size*2,u.size*2,ColorMode.RGB)
 for i,im in ipairs(l) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=names[i];s:newCel(layer,1,scale(im,2),Point(0,0)) end
 local stem='system_'..u.id;local flat=Image(s);frames[u.id]=flat
 s:saveAs(outDir..'/'..stem..'.aseprite');flat:saveAs(outDir..'/'..stem..'.png')
 manifest.assets[#manifest.assets+1]={id=u.id,family=u.family,role=u.role,width=u.size*2,height=u.size*2,frames=1,layers=names,aseprite=stem..'.aseprite',png=stem..'.png',coreOffset=(u.size-12),pixelScale=2}
 s:close()
end
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},
 E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},
 I={'111','010','010','010','111'},J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},
 M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},
 Q={'010','101','101','111','011'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},
 U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},
 Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},[' ']={'000','000','000','000','000'},['6']={'011','100','110','101','010'},['4']={'101','101','111','001','001'},['9']={'010','101','011','001','110'}}
local function text(im,label,x,y,p,n)
 for i=1,#label do local glyph=font[label:sub(i,i)] or font[' ']
  for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then rect(im,x+(i-1)*4*n+(xx-1)*n,y+(yy-1)*n,n,n,p) end end end
 end
end
local preview=Image(888,480,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'SYSTEM STRUCTURES',24,22,metal[6],3);text(preview,'REGION A B C',646,27,metal[4],2)
local bgA,bgB=rgba('242E39'),rgba('293541')
for index,u in ipairs(units) do
 local x=24+(index-1)*288;local frame=frames[u.id]
 for yy=0,223 do for xx=0,263 do preview:drawPixel(x+xx,64+yy,(math.floor(xx/16)+math.floor(yy/16))%2==0 and bgA or bgB) end end
 local large=scale(frame,2);preview:drawImage(large,Point(x+(264-large.width)/2,64+(224-large.height)/2))
 text(preview,u.label,x+(264-#u.label*8)/2,306,u.energy[5],2)
 local sizeLabel=tostring(frame.width)..' X '..tostring(frame.height);text(preview,sizeLabel,x+(264-#sizeLabel*8)/2,328,metal[4],2)
 preview:drawImage(frame,Point(x+(264-frame.width)/2,360+(96-frame.height)/2))
end
preview:saveAs(outDir..'/system_structures_comparison.png')
local f=assert(io.open(outDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outDir..'/generation_complete.txt','w'));done:write('Three SYSTEM structures generated from approved support style.\n');done:close()
print('SYSTEM_STRUCTURES_GENERATED')
