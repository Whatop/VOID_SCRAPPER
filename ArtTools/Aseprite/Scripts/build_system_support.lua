-- Three system/security drones, derived from copied enemy4/enemy7/core9 sources.
-- Author on a 32px logical grid and replicate pixels exactly 2x for 64px assets.
local srcDir=assert(app.params.sourceDir)
local outDir=assert(app.params.outputDir)
local pc=app.pixelColor
local function color(hex) return pc.rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255) end
local function palette(list) local out={};for _,hex in ipairs(list) do out[#out+1]=color(hex) end;return out end
local metal=palette{'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local units={
 {id='green',label='GREEN GUARD',role='balanced guard / sector-security support',energy=palette{'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'}},
 {id='orange',label='ORANGE ASSAULT',role='heavier assault / security support',energy=palette{'553020','995028','DB8432','FFBA53','FFE397','FFF7DA'}},
 {id='blue',label='BLUE PRECISION',role='precision / navigation / ranged support',energy=palette{'163454','285E91','388ED1','68C7ED','ABE8F4','E8FAFF'}}
}
local chassis=assert(Image{fromFile=srcDir..'/enemy_elite4.png'})
local modules=assert(Image{fromFile=srcDir..'/enemy_elite7.png'})
local core=assert(Image{fromFile=srcDir..'/core9.png'})
assert(chassis.width==64 and chassis.height==64 and modules.width==64 and modules.height==64)
assert(core.width==32 and core.height==32)
local function image32() return Image(32,32,ColorMode.RGB) end
local function put(im,x,y,p) if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,p) end end
local function rect(im,x,y,w,h,p) for yy=y,y+h-1 do for xx=x,x+w-1 do put(im,xx,yy,p) end end end
local function line(im,x0,y0,x1,y1,p)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0);local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1;local err=dx+dy
 while true do
  put(im,x0,y0,p);if x0==x1 and y0==y1 then break end
  local e=2*err;if e>=dy then err=err+dy;x0=x0+sx end;if e<=dx then err=err+dx;y0=y0+sy end
 end
end
local function poly(im,pts,p)
 local minY,maxY=im.height,-1;for _,q in ipairs(pts) do minY=math.min(minY,q[2]);maxY=math.max(maxY,q[2]) end
 for y=minY,maxY do
  local nodes={};local j=#pts
  for i=1,#pts do local a,b=pts[i],pts[j]
   if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then nodes[#nodes+1]=a[1]+(y-a[2])/(b[2]-a[2])*(b[1]-a[1]) end;j=i
  end
  table.sort(nodes);for n=1,#nodes-1,2 do for x=math.ceil(nodes[n]),math.floor(nodes[n+1]) do put(im,x,y,p) end end
 end
 for i=1,#pts do local a,b=pts[i],pts[(i%#pts)+1];line(im,a[1],a[2],b[1],b[2],p) end
end
local function pairPut(im,x,y,p) put(im,x,y,p);put(im,31-x,y,p) end
local function pairRect(im,x,y,w,h,p) for yy=y,y+h-1 do for xx=x,x+w-1 do pairPut(im,xx,yy,p) end end end
local function pairPoly(im,pts,p)
 poly(im,pts,p);local mirrored={};for _,q in ipairs(pts) do mirrored[#mirrored+1]={31-q[1],q[2]} end;poly(im,mirrored,p)
end
local function pairLine(im,x0,y0,x1,y1,p) line(im,x0,y0,x1,y1,p);line(im,31-x0,y0,31-x1,y1,p) end
local function neutral(p)
 local alpha=pc.rgbaA(p);assert(alpha==0 or alpha==255,'Unexpected source alpha')
 if alpha==0 then return 0 end
 local v=(pc.rgbaR(p)*3+pc.rgbaG(p)*6+pc.rgbaB(p))/10
 -- Neutralize all inherited faction colors; retain structural shading only.
 return metal[v<20 and 1 or v<40 and 2 or v<64 and 3 or v<86 and 4 or v<112 and 5 or v<155 and 6 or 7]
end
local function pairedDonor(dst,source,sx,sy,w,h,dx,dy)
 for y=0,h/2-1 do for x=0,w/2-1 do
  local p=neutral(source:getPixel(sx+x*2,sy+y*2))
  if pc.rgbaA(p)>0 then pairPut(dst,dx+x,dy+y,p) end
 end end
end
local function sharedHull()
 local mechanical=image32()
 -- Mirror the original left half to remove patchwork asymmetry and keep a compact chassis.
 for y=5,27 do for x=5,15 do
  local p=neutral(chassis:getPixel(x*2,y*2));if pc.rgbaA(p)>0 then pairPut(mechanical,x,y,p) end
 end end
 local armor=image32()
 poly(armor,{{13,5},{18,5},{21,8},{21,22},{18,25},{13,25},{10,22},{10,8}},metal[1])
 poly(armor,{{13,6},{18,6},{20,8},{20,21},{18,24},{13,24},{11,21},{11,8}},metal[4])
 pairPoly(armor,{{13,6},{15,6},{15,10},{12,13},{11,11},{11,8}},metal[6])
 pairLine(armor,13,6,15,6,metal[7]);pairLine(armor,11,8,13,6,metal[7])
 pairPoly(armor,{{11,19},{13,21},{15,23},{15,25},{12,24},{10,21}},metal[6])
 pairLine(armor,11,20,13,22,metal[7])
 pairRect(armor,12,25,3,2,metal[2]);pairPut(armor,13,27,metal[3])
 -- A clean, continuous diamond bezel ties the drone to the region core frame.
 poly(armor,{{15,10},{16,10},{21,15},{21,16},{16,21},{15,21},{10,16},{10,15}},metal[1])
 poly(armor,{{15,11},{16,11},{20,15},{20,16},{16,20},{15,20},{11,16},{11,15}},metal[5])
 poly(armor,{{15,12},{16,12},{19,15},{19,16},{16,19},{15,19},{12,16},{12,15}},metal[2])
 return mechanical,armor
end
local function isEnergy(p)
 local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
 return pc.rgbaA(p)>0 and ((r>=110 and r>b*1.25 and r>=g*0.96) or r+g+b>700)
end
local coreMinX,coreMinY,coreMaxX,coreMaxY=32,32,-1,-1
for it in core:pixels() do if isEnergy(it()) then coreMinX=math.min(coreMinX,it.x);coreMinY=math.min(coreMinY,it.y);coreMaxX=math.max(coreMaxX,it.x);coreMaxY=math.max(coreMaxY,it.y) end end
assert(coreMaxX>=coreMinX,'Core energy mask is empty')
local function sourceEnergyLevel(p)
 local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
 if r+g+b>720 then return 6 elseif g>=220 then return 5 elseif g>=175 then return 4 elseif g>=125 then return 3 else return 1 end
end
local function coreWindow(dst,u)
 -- Crop the lit part of core9, fit it to an 8x8 window, and mirror its left half.
 local w,h=coreMaxX-coreMinX+1,coreMaxY-coreMinY+1
 for y=0,7 do for x=0,3 do
  local sx=coreMinX+math.min(w-1,math.floor((x+0.5)*w/8))
  local sy=coreMinY+math.min(h-1,math.floor((y+0.5)*h/8))
  local p=core:getPixel(sx,sy)
  if isEnergy(p) and math.abs((12+x)-15.5)+math.abs((12+y)-15.5)<=4.5 then pairPut(dst,12+x,12+y,u.energy[sourceEnergyLevel(p)]) end
 end end
 -- Fixed low-intensity rear indicators; no corruption, dropout, or flashing.
 pairPut(dst,13,26,u.energy[3])
end
local function guardModules(mechanical,armor,energy,u)
 pairedDonor(mechanical,modules,4,20,16,26,3,10)
 pairPoly(armor,{{6,9},{8,9},{10,11},{10,19},{8,22},{6,22},{4,20},{4,11}},metal[1])
 pairPoly(armor,{{6,10},{8,10},{9,12},{9,19},{7,21},{6,21},{5,19},{5,12}},metal[6])
 pairLine(armor,6,10,8,10,metal[7]);pairLine(armor,5,12,5,18,metal[7])
 pairRect(armor,8,12,1,7,metal[4]);pairRect(armor,5,14,2,4,metal[2])
 pairRect(energy,5,14,1,4,u.energy[4]);pairPut(energy,6,14,u.energy[3])
 pairRect(armor,6,20,2,1,metal[5])
end
local function assaultModules(mechanical,armor,energy,u)
 pairedDonor(mechanical,modules,4,20,16,26,2,9)
 pairRect(mechanical,8,13,4,6,metal[2])
 pairPoly(armor,{{4,9},{8,9},{10,11},{10,21},{8,23},{4,23},{2,21},{2,11}},metal[1])
 pairPoly(armor,{{4,10},{8,10},{9,12},{9,20},{7,22},{4,22},{3,20},{3,12}},metal[6])
 pairLine(armor,4,10,8,10,metal[7]);pairLine(armor,3,12,3,18,metal[7])
 pairRect(armor,8,12,1,8,metal[4]);pairRect(armor,4,6,5,6,metal[1])
 pairRect(armor,5,7,3,4,metal[5]);pairRect(armor,5,7,1,3,metal[7]);pairRect(armor,5,6,3,1,metal[2])
 pairRect(armor,4,12,4,2,metal[2]);pairRect(energy,4,12,4,1,u.energy[3]);pairPut(energy,4,12,u.energy[4])
 pairPoly(energy,{{4,17},{5,17},{7,19},{7,20},{6,20},{4,18}},u.energy[3])
 pairRect(armor,4,21,3,1,metal[4])
end
local function precisionModules(mechanical,armor,energy,u)
 pairedDonor(mechanical,modules,22,4,8,16,9,2)
 -- Slender paired rangefinders with a continuous rail and discrete charge lamps.
 pairRect(armor,9,2,3,9,metal[1]);pairRect(armor,10,3,1,6,metal[6]);pairPut(armor,10,2,metal[4])
 pairRect(energy,10,5,1,3,u.energy[3]);pairPut(energy,10,5,u.energy[5])
 pairPoly(armor,{{8,12},{10,12},{10,21},{8,23},{6,22},{6,15}},metal[1])
 pairPoly(armor,{{8,13},{9,13},{9,20},{8,22},{7,21},{7,15}},metal[6])
 pairLine(armor,8,13,7,15,metal[7]);pairRect(armor,8,17,1,4,metal[4])
 pairRect(mechanical,3,16,5,2,metal[2]);pairRect(armor,4,16,3,1,metal[5]);pairPut(energy,3,16,u.energy[4])
 pairRect(energy,7,16,1,3,u.energy[3]);pairPut(energy,7,16,u.energy[5])
 pairPut(armor,13,28,metal[2])
end
local function upscale(im,n)
 local out=Image(im.width*n,im.height*n,ColorMode.RGB)
 for it in im:pixels() do if pc.rgbaA(it())>0 then rect(out,it.x*n,it.y*n,n,n,it()) end end
 return out
end
local previews={}
local manifest={generatorId='void-scrapper-system-support-v1',faction='System',orientation='north',assets={},
 sources={chassis='Input/03_SpecialEnemy/enemy_elite4.png',modules='Input/03_SpecialEnemy/enemy_elite7.png',
 energy='Input/02_Core/core9.png',coreEnergyCrop={x=coreMinX,y=coreMinY,w=coreMaxX-coreMinX+1,h=coreMaxY-coreMinY+1}},
 paletteRelationship='Energy colors exactly reuse the green/orange/blue region-core family ramps.'}
for _,u in ipairs(units) do
 local mechanical,armor=sharedHull();local energy=image32();local markings=image32()
 if u.id=='green' then guardModules(mechanical,armor,energy,u)
 elseif u.id=='orange' then assaultModules(mechanical,armor,energy,u)
 else precisionModules(mechanical,armor,energy,u) end
 coreWindow(energy,u)
 -- Small, matching registration bars distinguish official equipment from raider patches.
 pairRect(markings,12,8,2,1,metal[3]);pairPut(markings,14,23,metal[3])
 local names={'Mechanical Chassis','Clean Armor','Stable Core and Role Energy','System Markings'}
 local images={mechanical,armor,energy,markings}
 local sprite=Sprite(64,64,ColorMode.RGB)
 for i,im in ipairs(images) do local layer=i==1 and sprite.layers[1] or sprite:newLayer();layer.name=names[i];sprite:newCel(layer,1,upscale(im,2),Point(0,0)) end
 local stem='system_support_'..u.id
 local flat=Image(sprite);previews[u.id]=flat
 sprite:saveAs(outDir..'/'..stem..'.aseprite');flat:saveAs(outDir..'/'..stem..'.png')
 manifest.assets[#manifest.assets+1]={id=u.id,role=u.role,width=64,height=64,frames=1,layers=names,png=stem..'.png',aseprite=stem..'.aseprite'}
 sprite:close()
end
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},
 E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},
 I={'111','010','010','010','111'},J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},
 M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},
 Q={'010','101','101','111','011'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},
 U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},
 Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},[' ']={'000','000','000','000','000'},['6']={'011','100','110','101','010'},['4']={'101','101','111','001','001'}}
local function text(im,label,x,y,p,n)
 for i=1,#label do local glyph=font[label:sub(i,i)] or font[' ']
  for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then rect(im,x+(i-1)*4*n+(xx-1)*n,y+(yy-1)*n,n,n,p) end end end
 end
end
local board=Image(768,424,ColorMode.RGB);board:clear(Color{r=18,g=24,b=32,a=255})
text(board,'SYSTEM SUPPORT UNITS',24,22,metal[6],3);text(board,'64 X 64',620,26,metal[4],2)
local bgA,bgB=color('242E39'),color('293541')
for index,u in ipairs(units) do
 local x=24+(index-1)*248
 for y=0,207 do for xx=0,207 do board:drawPixel(x+xx,64+y,(math.floor(xx/16)+math.floor(y/16))%2==0 and bgA or bgB) end end
 board:drawImage(upscale(previews[u.id],3),Point(x+8,72));board:drawImage(previews[u.id],Point(x+72,296))
 text(board,u.label,x+math.floor((208-#u.label*8)/2),382,u.energy[5],2)
end
board:saveAs(outDir..'/system_support_preview.png')
local f=assert(io.open(outDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outDir..'/generation_complete.txt','w'));done:write('Three 64x64 System support units generated.\n');done:close()
print('SYSTEM_SUPPORT_GENERATED')
