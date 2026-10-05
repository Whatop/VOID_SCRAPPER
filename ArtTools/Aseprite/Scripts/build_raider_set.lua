-- Aseprite CLI / Lua. Derive four north-facing units from copied style anchors.
-- Shared normal hull: common sheet, row 2 column 5 (one-based).
-- Heavy hull: elite sheet, row 4 column 1, with the shared cockpit module.
local sourceDir=assert(app.params.sourceDir)
local outDir=assert(app.params.outputDir)
local pc=app.pixelColor
local function color(hex) return pc.rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255) end
local function colors(list) local out={};for _,hex in ipairs(list) do out[#out+1]=color(hex) end;return out end
local metal=colors{'171E25','303941','515F67','78888C','ADB9B7','D6DEDA','F0F2E9'}
local red=colors{'662C32','B84445','ED7563'}
local units={
 {id='basic',label='BASIC',role=colors{'254F39','4FA96C','B3EDAA'}},
 {id='shotgun',label='SHOTGUN',role=colors{'6F3A26','E9913C','FFE0A2'}},
 {id='sniper_charging',label='SNIPER',role=colors{'234871','529EDC','B8EFFF'}},
 {id='elite',label='ELITE',role=colors{'662C32','B84445','ED7563'}}
}
local common=assert(Image{fromFile=sourceDir..'/common.png'})
local elite=assert(Image{fromFile=sourceDir..'/elite.png'})
assert(common.width==256 and common.height==256 and elite.width==256 and elite.height==256,'Unexpected anchor size')
local normalCrop=Image(common,Rectangle(128,32,32,32))
local eliteCrop=Image(elite,Rectangle(0,192,64,64))
local function put(im,x,y,p) if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,p) end end
local function rect(im,x,y,w,h,p) for yy=y,y+h-1 do for xx=x,x+w-1 do put(im,xx,yy,p) end end end
local function line(im,x0,y0,x1,y1,p)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0)
 local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1
 local err=dx+dy
 while true do
  put(im,x0,y0,p);if x0==x1 and y0==y1 then break end
  local e2=2*err;if e2>=dy then err=err+dy;x0=x0+sx end;if e2<=dx then err=err+dx;y0=y0+sy end
 end
end
local function poly(im,points,p)
 local minY,maxY=im.height,-1
 for _,q in ipairs(points) do minY=math.min(minY,q[2]);maxY=math.max(maxY,q[2]) end
 for y=minY,maxY do
  local nodes={};local j=#points
  for i=1,#points do
   local a,b=points[i],points[j]
   if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then nodes[#nodes+1]=a[1]+(y-a[2])/(b[2]-a[2])*(b[1]-a[1]) end
   j=i
  end
  table.sort(nodes)
  for i=1,#nodes-1,2 do for x=math.ceil(nodes[i]),math.floor(nodes[i+1]) do put(im,x,y,p) end end
 end
 for i=1,#points do local a,b=points[i],points[(i%#points)+1];line(im,a[1],a[2],b[1],b[2],p) end
end
local function scale(im,factor)
 local out=Image(im.width*factor,im.height*factor,ColorMode.RGB)
 for it in im:pixels() do local p=it();if pc.rgbaA(p)>0 then rect(out,it.x*factor,it.y*factor,factor,factor,p) end end
 return out
end
local function mapPixel(p,u)
 local r,g,b,a=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),pc.rgbaA(p)
 assert(a==0 or a==255,'Source contains partial alpha')
 if a==0 then return 0 end
 if g>r*1.18 and b>r*1.18 and g+b>100 then
  return u.role[g+b<180 and 1 or (g+b<330 and 2 or 3)]
 elseif r>45 and r>g*1.23 and r>b*1.16 then
  return red[r<105 and 1 or (r<180 and 2 or 3)]
 end
 local lum=(r*3+g*6+b)/10
 return metal[lum<24 and 1 or lum<50 and 2 or lum<80 and 3 or lum<115 and 4 or lum<155 and 5 or lum<190 and 6 or 7]
end
local function remap(im,u)
 local out=Image(im.width,im.height,ColorMode.RGB)
 for it in im:pixels() do out:drawPixel(it.x,it.y,mapPixel(it(),u)) end
 return out
end
local function normalHull(u)
 local hull=Image(32,32,ColorMode.RGB)
 hull:drawImage(remap(normalCrop,u),Point(0,u.id=='sniper_charging' and 2 or 0))
 return hull
end
local function baseGun(im,lights,u)
 -- Compact, single offset machine-gun mount.
 rect(im,21,12,7,5,metal[2])
 poly(im,{{24,10},{27,10},{29,12},{29,18},{27,20},{23,18},{23,12}},metal[1])
 rect(im,24,11,4,7,metal[3]);rect(im,24,12,2,5,metal[6]);rect(im,25,18,3,1,metal[2])
 rect(im,24,4,4,8,metal[1]);rect(im,25,5,2,6,metal[4]);rect(im,25,5,1,5,metal[6])
 rect(im,25,4,2,1,metal[1]);rect(im,24,10,4,2,metal[2])
 rect(lights,26,12,2,2,u.role[2]);put(lights,26,12,u.role[3]);put(lights,26,16,u.role[1])
end
local function scatterGun(im,lights,u)
 -- Wide, short four-bore scatter mount with mirrored receivers.
 for side=0,1 do
  local function x(v) return side==0 and v or 31-v end
  local function r(xx,yy,w,h,p) for yy2=yy,yy+h-1 do for xx2=xx,xx+w-1 do put(im,x(xx2),yy2,p) end end end
  local points={{3,9},{8,9},{10,11},{10,18},{8,20},{3,20},{2,18},{2,11}}
  for _,p in ipairs(points) do p[1]=x(p[1]) end
  poly(im,points,metal[1]);r(3,11,6,7,metal[3]);r(3,11,2,6,metal[6]);r(4,18,4,1,metal[2])
  for _,bx in ipairs({3,7}) do
   r(bx,5,3,6,metal[1]);r(bx,6,2,4,metal[5]);r(bx+1,5,1,1,metal[1]);r(bx,9,3,2,metal[2])
  end
  for xx=5,8 do put(lights,x(xx),12,u.role[2]) end
  put(lights,x(5),12,u.role[3]);put(lights,x(7),14,u.role[1]);put(lights,x(8),15,u.role[2])
 end
end
local function railGun(im,lights,u)
 -- A central long rail and an exposed side charging pack.
 rect(im,14,1,4,11,metal[1]);rect(im,15,2,2,8,metal[3]);rect(im,14,2,1,6,metal[5])
 rect(im,15,1,2,1,metal[1]);rect(im,13,8,6,3,metal[2]);rect(im,13,8,1,3,metal[6]);rect(im,18,8,1,3,metal[5])
 rect(lights,16,3,1,5,u.role[2]);put(lights,16,3,u.role[3]);rect(lights,14,10,4,1,u.role[1])
 poly(im,{{24,11},{27,12},{28,14},{28,22},{26,24},{23,22},{23,13}},metal[1])
 rect(im,24,13,3,9,metal[2]);rect(im,24,13,1,9,metal[5])
 for _,y in ipairs({14,17,20}) do rect(lights,25,y,2,2,u.role[2]);put(lights,25,y,u.role[3]) end
 line(im,8,4,8,9,metal[3]);put(im,8,4,metal[6])
end
local function eliteGuns(im,lights,u)
 -- Side-mounted armored cannon pods widen the heavy donor hull.
 for side=0,1 do
  local function x(v) return side==0 and v or 63-v end
  local function r(xx,yy,w,h,p) for yy2=yy,yy+h-1 do for xx2=xx,xx+w-1 do put(im,x(xx2),yy2,p) end end end
  r(7,28,8,9,metal[2]);r(8,29,6,2,metal[4])
  local points={{4,16},{9,16},{11,19},{11,41},{9,44},{3,44},{2,41},{2,19}}
  for _,p in ipairs(points) do p[1]=x(p[1]) end
  poly(im,points,metal[1]);r(3,21,7,19,metal[3]);r(3,22,3,14,metal[6]);r(3,22,1,12,metal[7])
  r(4,10,6,10,metal[1]);r(5,11,4,7,metal[4]);r(5,11,1,6,metal[6]);r(6,10,2,2,metal[1])
  r(3,18,8,3,metal[2]);r(4,39,5,3,metal[5]);r(5,42,4,1,metal[2])
  for yy=24,27 do for xx=3,6 do put(lights,x(xx),yy,red[2]) end end
  put(lights,x(3),24,red[3]);put(lights,x(7),32,red[2]);put(lights,x(7),33,red[3])
 end
end
local function factionMarks(im,u)
 if u.id=='elite' then return end
 local offset=u.id=='sniper_charging' and 2 or 0
 -- Shared red leg bands and an asymmetric repair patch are kept in every normal.
 rect(im,10,21+offset,3,1,red[2]);rect(im,18,21+offset,3,1,red[2])
 rect(im,10,14+offset,2,3,metal[5]);put(im,10,14+offset,metal[7]);put(im,11,16+offset,metal[3])
end
local outputs={}
local manifest={generatorId='void-scrapper-raider-set-v1',orientation='north',assets={},
 sources={common={file='Input/00_StyleAnchors/Pirate_Common_Candidates_32x32.png',rect={x=128,y=32,w=32,h=32},row=2,column=5},
 elite={file='Input/00_StyleAnchors/Pirate_Elite_Candidates_64x64.png',rect={x=0,y=192,w=64,h=64},row=4,column=1}}}
for _,u in ipairs(units) do
 local size=u.id=='elite' and 64 or 32
 local hull=u.id=='elite' and remap(eliteCrop,u) or normalHull(u)
 local weapons=Image(size,size,ColorMode.RGB)
 local marks=Image(size,size,ColorMode.RGB)
 local lights=Image(size,size,ColorMode.RGB)
 if u.id=='basic' then baseGun(weapons,lights,u)
 elseif u.id=='shotgun' then scatterGun(weapons,lights,u)
 elseif u.id=='sniper_charging' then railGun(weapons,lights,u)
 else
  eliteGuns(weapons,lights,u)
  -- Reuse the shared normal cockpit on the heavier body.
  local module=Image(remap(normalCrop,u),Rectangle(14,7,4,7))
  marks:drawImage(scale(module,2),Point(28,12))
 end
 factionMarks(marks,u)
 local layers={hull,weapons,marks,lights}
 local names={'Shared Hull','Weapon Mounts','Faction Markings','Role Lights'}
 local s=Sprite(64,64,ColorMode.RGB)
 for i,im in ipairs(layers) do
  local layer=i==1 and s.layers[1] or s:newLayer();layer.name=names[i]
  s:newCel(layer,1,size==32 and scale(im,2) or im,Point(0,0))
 end
 local flat=Image(s)
 local stem='raider_'..u.id
 s:saveAs(outDir..'/'..stem..'.aseprite');flat:saveAs(outDir..'/'..stem..'.png')
 manifest.assets[#manifest.assets+1]={id=u.id,width=64,height=64,frames=1,layers=names,aseprite=stem..'.aseprite',png=stem..'.png',
 source=u.id=='elite' and 'elite_r4c1_with_common_cockpit' or 'common_r2c5',pixelScale=u.id=='elite' and 1 or 2}
 outputs[u.id]=flat;s:close()
end
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},
 D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},
 H={'101','101','111','101','101'},I={'111','010','010','010','111'},J={'001','001','001','101','010'},K={'101','101','110','101','101'},
 L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},
 P={'110','101','110','100','100'},Q={'010','101','101','111','011'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},
 T={'111','010','010','010','010'},U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},
 X={'101','101','010','101','101'},Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},[' ']={'000','000','000','000','000'},
 ['6']={'011','100','110','101','010'},['4']={'101','101','111','001','001'},['3']={'110','001','010','001','110'}}
local function text(im,label,x,y,p,factor)
 for n=1,#label do local glyph=font[label:sub(n,n)] or font[' ']
  for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then rect(im,x+(n-1)*4*factor+(xx-1)*factor,y+(yy-1)*factor,factor,factor,p) end end end
 end
end
local preview=Image(960,424,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'RAIDER ENEMY SET',28,22,metal[6],3);text(preview,'64 X 64',752,26,metal[4],2)
local tileA,tileB=color('242E39'),color('293541')
for index,u in ipairs(units) do
 local x=24+(index-1)*234
 for yy=0,207 do for xx=0,207 do preview:drawPixel(x+xx,64+yy,(math.floor(xx/16)+math.floor(yy/16))%2==0 and tileA or tileB) end end
 preview:drawImage(scale(outputs[u.id],3),Point(x+8,72))
 preview:drawImage(outputs[u.id],Point(x+72,296))
 local labelWidth=#u.label*8;text(preview,u.label,x+math.floor((208-labelWidth)/2),382,u.role[3],2)
end
preview:saveAs(outDir..'/raider_enemy_preview.png')
local file=assert(io.open(outDir..'/manifest.json','w'));file:write(json.encode(manifest));file:close()
local done=assert(io.open(outDir..'/generation_complete.txt','w'));done:write('Four 64x64 units generated from approved copies.\n');done:close()
print('RAIDER_SET_GENERATED')
