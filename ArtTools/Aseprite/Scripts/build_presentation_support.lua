-- New presentation assets only. All approved references are read from verified copies.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function write(n,t)local f=assert(io.open(out..'/'..n,'w'));f:write(json.encode(t));f:close()end
local function render(s,f)local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function palette(s,hex)local p=Palette(#hex+1);p:setColor(0,Color{r=0,g=0,b=0,a=0});for i,h in ipairs(hex)do local c=rgba(h);p:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255})end;s:setPalette(p)end
local function layers(w,h,n)local l={};for i=1,n do l[i]=Image(w,h,ColorMode.RGB)end;return l end
local function doubled(ls)local r={};for i,im in ipairs(ls)do r[i]=H.scale(im,2)end;return r end
local function box(im,x,y,w,h,c)H.line(im,x,y,x+w-1,y,c);H.line(im,x,y+h-1,x+w-1,y+h-1,c);H.line(im,x,y,x,y+h-1,c);H.line(im,x+w-1,y,x+w-1,y+h-1,c)end
local function path(im,points,c)for i=1,#points-1 do H.line(im,points[i][1],points[i][2],points[i+1][1],points[i+1][2],c)end end
local manifest={generatorId='presentation-support-v1',families={},pixelsPerUnit=32,sourcePolicy='Only verified reference copies; no approved art or Unity configuration changed'}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local assets={}
local function export(v)
 local s=Sprite(v.w,v.h,ColorMode.RGB);for i,n in ipairs(v.layers)do local l=i==1 and s.layers[1]or s:newLayer();l.name=n end
 local rendered={};local strip=Image(v.w*#v.frames,v.h,ColorMode.RGB);local meta={}
 for f,ls in ipairs(v.frames)do
  if f>1 then s:newEmptyFrame(f)end
  for i,im in ipairs(ls)do s:newCel(s.layers[i],f,im,Point(0,0))end
  s.frames[f].duration=v.ms[f]/1000;rendered[f]=render(s,f);strip:drawImage(rendered[f],Point((f-1)*v.w,0))
  meta[f]={filename=v.id..'_'..f,frame={x=(f-1)*v.w,y=0,w=v.w,h=v.h},sourceSize={w=v.w,h=v.h},spriteSourceSize={x=0,y=0,w=v.w,h=v.h},rotated=false,trimmed=false,duration=v.ms[f]}
 end
 local tags={}
 for _,t in ipairs(v.tags)do
  local tag=s:newTag(t.from,t.to);tag.name=t.name;tags[#tags+1]={name=t.name,from=t.from-1,to=t.to-1,direction='forward'}
  if #v.tags>1 then local im=Image((t.to-t.from+1)*v.w,v.h,ColorMode.RGB);for f=t.from,t.to do im:drawImage(rendered[f],Point((f-t.from)*v.w,0))end;im:saveAs(out..'/'..v.id..'_'..t.name..'.png')end
 end
 palette(s,v.palette);s:saveAs(out..'/'..v.id..'.aseprite');s:close();strip:saveAs(out..'/'..v.id..'.png')
 write(v.id..'.json',{frames=meta,meta={app='Aseprite CLI + Lua',image=v.id..'.png',format='RGBA8888',size={w=strip.width,h=v.h},scale='1',frameTags=tags},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot={x=.5,y=.5},clips=v.tags,notes=v.notes}})
 v.rendered=rendered;assets[#assets+1]=v
 manifest.families[#manifest.families+1]={id=v.id,width=v.w,height=v.h,frames=#v.frames,layers=v.layers,palette=v.palette,tags=v.tags,notes=v.notes}
 return rendered
end

-- A: five representative plates. These are layered fixed-view plates, not claimed seamless tiles.
local regionSpecs={
 {name='Tutorial',label='TUTORIAL / QUIET APPROACH',palette={'0B111A','101A24','192633','263643','354550','41515A'}},
 {name='RegionA',label='REGION A / SALVAGE TRACES',palette={'0B1216','111D20','1A2B29','294137','30483E','3C5349'}},
 {name='RegionB',label='REGION B / INDUSTRIAL PRESSURE',palette={'130F11','211B1B','302625','45352B','514032','614A38'}},
 {name='RegionC',label='REGION C / PHASE ROUTES',palette={'0A101B','111B2B','1B2B3D','293D52','314862','3B5370'}},
 {name='Final',label='FINAL / NULL PERIPHERY',palette={'08090F','11111D','1D182D','2D223B','3A2A48','473355'}}
}
local backgroundLayers={'Void Base','Distant Points','Peripheral Remnants','Faint Regional Traces'}
local backgrounds,backgroundOverlays={},{}
for n,reg in ipairs(regionSpecs)do
 local c={};for i,h in ipairs(reg.palette)do c[i]=rgba(h)end
 local l=layers(240,135,4);H.rect(l[1],0,0,240,135,c[1])
 -- Deterministic sparse stars, never large glints or collectible-looking highlights.
 for i=1,(n==1 and 25 or n==5 and 27 or 39)do
  local x=(i*71+n*29)%236+2;local y=(i*43+n*17)%131+2
  H.put(l[2],x,y,c[(x>48 and x<192 and y>27 and y<108)and 2 or i%5==0 and 4 or 3])
 end
 if n==1 then
  path(l[3],{{5,75},{5,52},{10,47},{18,47}},c[3]);path(l[3],{{8,75},{8,55},{12,51}},c[2])
  H.rect(l[3],16,112,22,3,c[2]);H.rect(l[3],16,117,13,2,c[2]);H.rect(l[4],18,112,4,1,c[4])
  path(l[3],{{205,12},{222,12},{227,17},{227,26}},c[3]);H.rect(l[4],206,15,6,1,c[4])
  H.rect(l[3],225,107,9,2,c[2]);H.rect(l[3],231,111,3,9,c[2])
 elseif n==2 then
  H.poly(l[3],{{25,8},{56,8},{62,14},{57,22},{34,24},{25,18}},c[2])
  path(l[3],{{27,10},{52,10},{59,15},{54,20},{36,21}},c[3]);H.line(l[3],37,12,47,12,c[4])
  H.rect(l[3],5,43,34,5,c[3]);H.rect(l[3],8,50,22,3,c[2])
  for x=10,34,8 do H.rect(l[3],x,43,2,13,c[2]);H.put(l[4],x,45,c[5])end
  H.poly(l[3],{{205,64},{223,61},{234,68},{230,85},{212,91},{202,79}},c[2])
  path(l[3],{{210,66},{221,65},{229,70},{226,81},{214,85}},c[3])
  path(l[4],{{213,69},{218,69},{218,74}},c[4])
  H.poly(l[3],{{184,116},{212,111},{234,118},{226,130},{197,131}},c[2])
  path(l[3],{{190,117},{210,114},{226,119},{219,126}},c[3]);H.rect(l[4],210,120,7,1,c[4])
  H.rect(l[3],110,121,12,3,c[2]);H.rect(l[3],117,126,4,2,c[3])
 elseif n==3 then
  H.poly(l[3],{{0,39},{28,39},{42,53},{39,82},{27,97},{0,97}},c[2])
  H.poly(l[3],{{0,44},{23,44},{35,55},{33,78},{22,89},{0,89}},c[3])
  H.poly(l[3],{{0,51},{18,51},{26,59},{24,75},{17,82},{0,82}},0)
  for y=49,81,8 do H.rect(l[3],29,y,6,2,c[4])end
  H.rect(l[4],19,42,8,1,c[5]);H.rect(l[4],31,60,2,4,c[5])
  H.poly(l[3],{{210,32},{239,32},{239,79},{226,76},{204,53}},c[2])
  path(l[3],{{218,36},{235,36},{235,68},{229,68},{210,51}},c[4])
  for y=40,60,5 do H.rect(l[3],228,y,7,2,c[3])end
  H.poly(l[3],{{119,117},{131,111},{167,111},{182,124},{178,134},{116,134}},c[3])
  H.rect(l[3],134,116,30,12,0);for x=137,159,7 do H.rect(l[3],x,118,2,8,c[2])end
  H.rect(l[4],120,127,9,1,c[5]);H.rect(l[4],164,113,5,1,c[5])
  H.rect(l[3],51,7,47,11,c[2]);for x=55,89,8 do H.rect(l[3],x,10,4,5,c[3])end
 elseif n==4 then
  path(l[3],{{6,87},{6,57},{15,48},{32,48},{37,53}},c[3])
  path(l[3],{{12,91},{12,63},{20,55},{36,55}},c[2])
  path(l[4],{{9,76},{9,60},{18,51},{24,51}},c[4]);H.rect(l[4],29,51,6,1,c[4])
  box(l[3],171,9,45,11,c[3]);H.rect(l[3],188,9,8,1,0);H.rect(l[3],207,19,9,1,0)
  box(l[3],183,14,43,11,c[2]);H.rect(l[4],216,14,4,1,c[5])
  path(l[3],{{219,44},{230,55},{230,83},{220,93},{205,93}},c[3])
  path(l[4],{{217,49},{226,58},{226,69}},c[4]);H.rect(l[4],226,75,1,5,c[4])
  path(l[3],{{70,129},{84,115},{125,115},{133,123},{157,123}},c[3])
  path(l[3],{{94,130},{104,120},{137,120}},c[2])
  H.rect(l[4],108,115,8,1,c[4]);H.rect(l[4],141,123,6,1,c[5])
 elseif n==5 then
  H.poly(l[3],{{0,32},{30,32},{43,46},{37,60},{44,77},{30,103},{0,99}},c[2])
  H.poly(l[3],{{0,38},{22,38},{32,48},{22,59},{31,73},{24,91},{0,87}},0)
  path(l[3],{{29,35},{39,46},{32,60},{40,78},{28,97}},c[3])
  path(l[4],{{29,41},{34,46},{28,52}},c[4]);H.rect(l[4],32,69,2,7,c[5])
  H.poly(l[3],{{86,0},{159,0},{153,18},{133,22},{114,15},{99,21},{89,13}},c[2])
  H.rect(l[3],105,0,8,12,0);H.rect(l[3],138,0,11,15,0)
  path(l[4],{{93,10},{101,10},{106,15}},c[4]);H.rect(l[4],131,17,7,1,c[4])
  H.poly(l[3],{{216,39},{239,39},{239,105},{216,101},{207,85},{214,69},{205,57}},c[2])
  H.rect(l[3],226,49,13,35,0);H.rect(l[4],211,55,7,2,c[4]);H.rect(l[4],215,60,3,1,c[5])
  H.rect(l[3],169,119,41,16,c[2]);H.rect(l[3],178,119,5,16,0);H.rect(l[3],199,128,11,7,0)
  path(l[4],{{170,123},{175,123},{175,128}},c[4]);H.rect(l[4],189,127,6,1,c[5])
  for _,q in ipairs({{47,18},{55,22},{218,111},{224,117},{143,113},{36,116}})do H.rect(l[4],q[1],q[2],3,1,c[3])end
 end
 local ls=doubled(l);local v={id='BG_'..reg.name,w=480,h=270,frames={ls},ms={200},layers=backgroundLayers,palette=reg.palette,tags={{name='Plate',from=1,to=1,loop=false}},notes='Representative fixed-view plate. Separate transparent layers. Quiet center x96..383 y54..215. Not a seamless scroll tile.'}
 backgrounds[n]=export(v)[1];local overlay=Image(480,270,ColorMode.RGB)
 for i,im in ipairs(ls)do im:saveAs(out..'/'..v.id..'_Layer'..i..'.png');if i>1 then overlay:drawImage(im,Point(0,0))end end
 overlay:saveAs(out..'/'..v.id..'_Overlay.png');backgroundOverlays[n]=overlay
end

-- B: fixed-size icon frames. Borders stay thin at both resolutions.
local uiHex={'1D2B35','405663','7E98A3','66C4D6','DFEDF0','4D6670'}
local U={};for i,h in ipairs(uiHex)do U[i]=rgba(h)end
local states={'Idle','Selected','Equipped','Locked'};local ui={}
local function frameArt(size,state)
 local n=size/2;local ls=layers(n,n,3)
 local p={{4,1},{n-5,1},{n-2,4},{n-2,n-5},{n-5,n-2},{4,n-2},{1,n-5},{1,4},{4,1}}
 path(ls[1],p,U[1])
 path(ls[1],{{4,2},{n-5,2},{n-3,4},{n-3,n-5},{n-5,n-3},{4,n-3},{2,n-5},{2,4},{4,2}},state==4 and U[1]or U[2])
 -- Four practical stepped corner clamps, with an empty icon window.
 for _,q in ipairs({{3,3,1,1},{n-4,3,-1,1},{n-4,n-4,-1,-1},{3,n-4,1,-1}})do
  local x,y,dx,dy=q[1],q[2],q[3],q[4];local c=state==2 and U[3]or state==4 and U[2]or U[3]
  H.line(ls[2],x,y,x+dx*3,y,c);H.line(ls[2],x,y,x,y+dy*3,c)
  H.put(ls[1],x+dx,y+dy,U[1])
  if state==2 then H.line(ls[3],x+dx,y,x+dx*2,y,U[4]);H.put(ls[3],x,y,U[5])end
 end
 -- Keep rails interrupted around status sockets; symbols never enter the icon window.
 H.rect(ls[1],n/2-3,n-3,6,1,0)
 if state==1 then H.rect(ls[3],n/2-2,n-3,4,1,U[3])
 elseif state==2 then
  H.rect(ls[3],n/2-3,1,6,1,U[4]);H.rect(ls[3],n/2-2,n-3,4,1,U[4])
 elseif state==3 then
  H.line(ls[3],5,2,n-6,2,U[3])
  for x=n/2-3,n/2+1,2 do H.rect(ls[3],x,n-3,1,1,U[4])end
  -- Small checked latch, in the bottom border only.
  H.line(ls[3],n-7,n-5,n-6,n-4,U[4]);H.line(ls[3],n-6,n-4,n-4,n-6,U[4])
 elseif state==4 then
  local cx=n/2;H.rect(ls[3],cx-2,3,4,2,U[2]);H.line(ls[3],cx-1,1,cx,1,U[3]);H.put(ls[3],cx-1,2,U[3]);H.put(ls[3],cx,2,U[3])
  H.rect(ls[3],cx-2,n-3,4,1,U[2]);H.put(ls[3],cx-1,n-3,0)
 end
 return doubled(ls)
end
for _,size in ipairs({64,96})do
 local frames,tags,ms={},{},{}
 for f,name in ipairs(states)do frames[f]=frameArt(size,f);tags[f]={name=name,from=f,to=f,loop=false};ms[f]=200 end
 ui[size]=export({id='UI_EquipmentFrame_'..size,w=size,h=size,layers={'Frame Rails','Corner Hardware','State Indicators'},palette=uiHex,frames=frames,ms=ms,tags=tags,notes='Fixed-size transparent border. Clear center inset12px. 64px frame for32px icons; 96px frame for64px icons. Do not stretch status glyphs.'})
end

-- C: tiny rectangular boundary/error accents; a 20x20 exclusion window protects the 16x16 body.
local playerHex={'385762','72AAB5','B8D7DC','E0EFEB','79618F'}
local P={};for i,h in ipairs(playerHex)do P[i]=rgba(h)end
local function corners(ls,a,col,length)
 for _,q in ipairs({{a,a,1,1},{15-a,a,-1,1},{15-a,15-a,-1,-1},{a,15-a,1,-1}})do
  H.line(ls,q[1],q[2],q[1]+q[3]*(length-1),q[2],col)
  H.line(ls,q[1],q[2],q[1],q[2]+q[4]*(length-1),col)
 end
end
local function playerFx(kind,f)
 local ls=layers(16,16,3);local pulse=({0,1,2,3,2,1})[f]
 if kind=='Idle'then
  corners(ls[1],1,P[1],2)
  local q=({{1,1},{14,1},{14,14},{1,14}})[pulse+1];H.put(ls[2],q[1],q[2],P[2])
  H.put(ls[3],1,7,P[1]);H.put(ls[3],14,8,P[1])
 elseif kind=='Activate'then
  if f==1 then corners(ls[1],2,P[1],2)
  elseif f==2 then corners(ls[1],2,P[2],2)
  elseif f==3 then corners(ls[1],1,P[2],3);for _,q in ipairs({{1,1},{14,1},{14,14},{1,14}})do H.put(ls[2],q[1],q[2],P[4])end
  elseif f==4 then corners(ls[1],1,P[2],2);H.put(ls[3],1,7,P[3]);H.put(ls[3],14,8,P[3])
  elseif f==5 then for _,q in ipairs({{1,1},{14,1},{14,14},{1,14}})do H.put(ls[1],q[1],q[2],P[1])end end
 else
  corners(ls[1],1,P[2],2)
  local q=({{1,1},{14,1},{14,14},{1,14}})[pulse+1];H.put(ls[2],q[1],q[2],P[3])
  H.rect(ls[3],1,6,1,2,P[1]);H.rect(ls[3],14,8,1,2,P[1])
  H.put(ls[3],1,6+(pulse%2),P[5]);H.put(ls[3],14,8+(pulse%2),P[2])
 end
 return doubled(ls)
end
local fxFrames,fxMs,fxTags={},{},{}
for _,spec in ipairs({{'Idle',{120,120,120,120,120,120},true},{'Activate',{40,50,60,60,80,50},false},{'Intensified',{80,80,80,80,80,80},true}})do
 local start=#fxFrames+1
 for f=1,6 do fxFrames[#fxFrames+1]=playerFx(spec[1],f);fxMs[#fxMs+1]=spec[2][f]end
 fxTags[#fxTags+1]={name=spec[1],from=start,to=#fxFrames,loop=spec[3]}
end
local fx=export({id='VFX_Player_RectBoundary',w=32,h=32,layers={'Corner Fragments','Activation Nodes','Error Bits'},palette=playerHex,frames=fxFrames,ms=fxMs,tags=fxTags,notes='Centered on the unchanged16px player. Central20x20 always transparent. Idle720ms loop, Activate340ms one-shot ends clear, Intensified480ms loop. Visual accent only; no protection or boundary gameplay.'})

-- Reference-only native compositions and review sheets.
local ink,muted,bg=rgba('DFE8EA'),rgba('82969F'),rgba('101820')
local function canvas(w,h)local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local extra={['5']={'111','100','110','001','110'},['7']={'111','001','010','100','100'},['9']={'111','101','111','001','110'}}
local function text(im,t,x,y,c,n)
 for i=1,#t do local ch=t:sub(i,i);local xx=x+(i-1)*4*n
  if extra[ch]then for yy,row in ipairs(extra[ch])do for col=1,3 do if row:sub(col,col)=='1'then H.rect(im,xx+(col-1)*n,y+(yy-1)*n,n,n,c)end end end else H.text(im,ch,xx,y,c,n)end
 end
end
local player=Image{fromFile=src..'/player.png'};assert(player.width==16 and player.height==16)
local enemy=Image{fromFile=src..'/enemy.png'};local guard=Image{fromFile=src..'/guard.png'}
local icons={Image{fromFile=src..'/icon_machinegun.png'},Image{fromFile=src..'/icon_shotgun.png'},Image{fromFile=src..'/icon_precision.png'}}
local function half(im)local r=Image(im.width/2,im.height/2,ColorMode.RGB);for y=0,r.height-1 do for x=0,r.width-1 do r:drawPixel(x,y,im:getPixel(x*2,y*2))end end;return r end
local smallIcons={};for i,im in ipairs(icons)do assert(im.width==64 and im.height==64);smallIcons[i]=half(im)end
local function frameWithIcon(size,state)
 local tile=Image(size,size,ColorMode.RGB);local icon=size==96 and icons[(state-1)%3+1]or smallIcons[(state-1)%3+1]
 tile:drawImage(icon,Point((size-icon.width)/2,(size-icon.height)/2));tile:drawImage(ui[size][state],Point(0,0));return tile
end
local function playerWithFx(f)
 local tile=Image(32,32,ColorMode.RGB);tile:drawImage(player,Point(8,8));tile:drawImage(fx[f],Point(0,0));return tile
end
local function scene(region,f)
 local im=Image(480,270,ColorMode.RGB);im:drawImage(backgrounds[region],Point(0,0))
 im:drawImage(enemy,Point(70,69));im:drawImage(guard,Point(336,75));im:drawImage(playerWithFx(f),Point(224,112))
 -- Fixed reference projectiles allow background contrast review without exporting new projectile art.
 H.rect(im,200,100,2,6,rgba('E3EBD8'));H.rect(im,275,110,2,6,rgba('E9913C'));H.rect(im,290,140,4,2,rgba('B8EFFF'))
 text(im,regionSpecs[region].label,16,14,ink,2)
 text(im,'PLAYER RECTANGULAR ACCENT / REFERENCE SPRITES',16,32,muted,1)
 for state=1,4 do local x=44+(state-1)*108;im:drawImage(frameWithIcon(64,state),Point(x,190));text(im,states[state]:upper(),x+4,177,muted,1)end
 text(im,'NATIVE 480X270 ASSET REVIEW / NOT A UNITY CAPTURE',16,260,muted,1);return im
end
for i,r in ipairs(regionSpecs)do scene(i,2):saveAs(out..'/Presentation_'..r.name..'_480x270.png')end
scene(2,14):saveAs(out..'/PresentationSupport_480x270.png')
local uiPreview=canvas(480,270);text(uiPreview,'EQUIPMENT FRAMES / CLEAR ICON WINDOWS',16,14,ink,2)
for state=1,4 do
 local x=24+(state-1)*112;text(uiPreview,states[state]:upper(),x+4,38,muted,1)
 uiPreview:drawImage(frameWithIcon(96,state),Point(x,50))
 uiPreview:drawImage(frameWithIcon(64,state),Point(x+16,172))
end
text(uiPreview,'96PX WITH 64PX ICONS / 64PX WITH 32PX DISPLAY ICONS',16,248,muted,1)
text(uiPreview,'ORIGINAL ICONS UNCHANGED / FRAMES EXPORT WITHOUT ICONS',16,260,muted,1)
uiPreview:saveAs(out..'/EquipmentFrames_480x270.png')
local uiContact=canvas(1008,398);text(uiContact,'EQUIPMENT FRAME STATES / TRANSPARENT CENTERS',16,16,ink,2)
for row,size in ipairs({64,96})do
 local y=52+(row-1)*162;text(uiContact,tostring(size)..'PX',16,y+10,muted,2)
 for f=1,4 do local x=100+(f-1)*216;text(uiContact,states[f]:upper(),x,y+8,ink,2)
  uiContact:drawImage(ui[size][f],Point(x,y+34));uiContact:drawImage(frameWithIcon(size,f),Point(x+size+8,y+34))
 end
end
uiContact:saveAs(out..'/EquipmentFrames_ContactSheet.png')
local fxContact=canvas(864,444);text(fxContact,'PLAYER RECTANGULAR ERROR / BODY STAYS CLEAR',16,16,ink,2)
text(fxContact,'PLAYER REFERENCE INCLUDED ONLY IN PREVIEWS / EFFECT CANVAS 32X32',16,36,muted,1)
for row,t in ipairs(fxTags)do local y=58+(row-1)*126;text(fxContact,t.name:upper(),16,y+4,P[2],2)
 for f=t.from,t.to do local x=126+(f-t.from)*120;fxContact:drawImage(H.scale(playerWithFx(f),3),Point(x,y+18))end
end;fxContact:saveAs(out..'/PlayerBoundary_ContactSheet.png')
local contact=canvas(1008,1374);text(contact,'VOID SCRAPPER / PRESENTATION SUPPORT',16,16,ink,3)
for i,r in ipairs(regionSpecs)do
 local x=i%2==1 and 16 or 512;local y=58+math.floor((i-1)/2)*306
 text(contact,r.label,x,y,ink,2);contact:drawImage(backgrounds[i],Point(x,y+18))
end
contact:drawImage(uiPreview,Point(512,688))
text(contact,'PLAYER ACCENTS / UNCHANGED 16PX BODY',16,984,ink,2)
for row,t in ipairs(fxTags)do local y=1022+(row-1)*110;text(contact,t.name:upper(),24,y+24,P[2],2)
 for f=t.from,t.to do local x=240+(f-t.from)*120;contact:drawImage(H.scale(playerWithFx(f),2),Point(x,y+10))end
end;contact:saveAs(out..'/PresentationSupport_ContactSheet.png')
local gs=Sprite(480,270,ColorMode.RGB);gs.layers[1].name='Reference-only native support-pack review'
local sequence={};for f=1,12 do sequence[#sequence+1]=f end;for repeatIndex=1,2 do for f=13,18 do sequence[#sequence+1]=f end end;for f=1,6 do sequence[#sequence+1]=f end
for i,f in ipairs(sequence)do if i>1 then gs:newEmptyFrame(i)end;gs:newCel(gs.layers[1],i,scene(2,f),Point(0,0));gs.frames[i].duration=fxMs[f]/1000 end
gs:saveAs(out..'/PresentationSupport_480x270.gif');gs:close()

-- Validate saved editable sources, PNG exports, backgrounds and clear-space guarantees.
local report={passed=true,assets={},pixelComparisons=0,backgrounds={},ui={},player={},previewIsUnityCapture=false}
local function stats(im)
 local m={opaque=0,colors=0,maxLuminance=0};local seen={}
 for it in im:pixels()do local c=it();local a=pc.rgbaA(c);assert(a==0 or a==255,'Non-binary alpha')
  if a>0 then m.opaque=m.opaque+1;seen[c]=true;m.maxLuminance=math.max(m.maxLuminance,.2126*pc.rgbaR(c)+.7152*pc.rgbaG(c)+.0722*pc.rgbaB(c))end
 end;for _ in pairs(seen)do m.colors=m.colors+1 end;return m
end
for _,v in ipairs(assets)do
 local saved=assert(app.open(out..'/'..v.id..'.aseprite'));local png=Image{fromFile=out..'/'..v.id..'.png'}
 assert(saved.width==v.w and saved.height==v.h and #saved.frames==#v.frames and #saved.layers==#v.layers and #saved.tags==#v.tags)
 local allowed={};for _,h in ipairs(v.palette)do allowed[rgba(h)]=true end
 local record={id=v.id,frames=#v.frames,colors=0}
 for f=1,#v.frames do local im=render(saved,f);local m=stats(im);record.colors=math.max(record.colors,m.colors)
  assert(math.floor(saved.frames[f].duration*1000+.5)==v.ms[f])
  for it in im:pixels()do local c=it();if pc.rgbaA(c)>0 then assert(allowed[c],'Unexpected palette color')end
   assert(c==png:getPixel((f-1)*v.w+it.x,it.y),'ASE/PNG mismatch')
   assert(c==im:getPixel(it.x-it.x%2,it.y-it.y%2),'Non-integer 2px construction')
   report.pixelComparisons=report.pixelComparisons+1
  end
 end
 for i,l in ipairs(saved.layers)do assert(l.name==v.layers[i]and l.isVisible and l.opacity==255)end
 for i,t in ipairs(v.tags)do local tag=saved.tags[i];assert(tag.name==t.name and tag.fromFrame.frameNumber==t.from and tag.toFrame.frameNumber==t.to)
  if #v.tags>1 then local strip=Image{fromFile=out..'/'..v.id..'_'..t.name..'.png'}
   for f=t.from,t.to do for it in v.rendered[f]:pixels()do assert(it()==strip:getPixel((f-t.from)*v.w+it.x,it.y))end end
  end
 end
 saved:close();report.assets[#report.assets+1]=record
end
for i,reg in ipairs(regionSpecs)do
 local m=stats(backgrounds[i]);assert(m.opaque==480*270 and m.colors<=6 and m.maxLuminance<=82,'Background too bright or complex')
 local layersPng={};for n=1,4 do layersPng[n]=Image{fromFile=out..'/BG_'..reg.name..'_Layer'..n..'.png'}end
 local composite=Image(480,270,ColorMode.RGB);for n=1,4 do composite:drawImage(layersPng[n],Point(0,0))end
 for it in composite:pixels()do assert(it()==backgrounds[i]:getPixel(it.x,it.y),'Layer reassembly differs')end
 local centralDetails,centralStars=0,0
 for y=54,215 do for x=96,383 do
  assert(pc.rgbaA(layersPng[3]:getPixel(x,y))==0 and pc.rgbaA(layersPng[4]:getPixel(x,y))==0,'Central gameplay area cluttered')
  if pc.rgbaA(layersPng[2]:getPixel(x,y))>0 then centralStars=centralStars+1 end
 end end
 assert(centralStars<=100,'Too many stars in center');local overlayStats=stats(backgroundOverlays[i]);assert(overlayStats.opaque<480*270*.2,'Background overlay too dense: '..reg.name..' '..overlayStats.opaque)
 report.backgrounds[#report.backgrounds+1]={region=reg.name,colors=m.colors,maxLuminance=m.maxLuminance,centralStructurePixels=centralDetails,centralStarPixels=centralStars,overlayOpaquePixels=overlayStats.opaque}
end
for _,size in ipairs({64,96})do
 for f=1,4 do local im=ui[size][f]
  for y=12,size-13 do for x=12,size-13 do assert(pc.rgbaA(im:getPixel(x,y))==0,'UI frame covers icon-safe center')end end
  local icon=size==96 and icons[(f-1)%3+1]or smallIcons[(f-1)%3+1];local tile=frameWithIcon(size,f);local offset=(size-icon.width)/2
  for it in icon:pixels()do if pc.rgbaA(it())>0 then assert(it()==tile:getPixel(offset+it.x,offset+it.y),'UI border covers reference icon')end end
 end
 report.ui[#report.ui+1]={size=size,states=4,clearCenterSize=size-24,borderCoversIcon=false}
end
local function difference(a,b,alpha)local count=0;for it in a:pixels()do local x=it();local y=b:getPixel(it.x,it.y);if alpha then x=pc.rgbaA(x);y=pc.rgbaA(y)end;if x~=y then count=count+1 end end;return count end
for i=1,5 do for j=i+1,5 do assert(difference(backgroundOverlays[i],backgroundOverlays[j],true)>500,'Backgrounds should not be palette swaps')end end
local maxFx=0
for f=1,18 do
 local m=stats(fx[f]);maxFx=math.max(maxFx,m.opaque);assert(m.opaque<=96,'Player accent too dense')
 for y=6,25 do for x=6,25 do assert(pc.rgbaA(fx[f]:getPixel(x,y))==0,'Player exclusion region violated')end end
 local tile=playerWithFx(f);for it in player:pixels()do assert(it()==tile:getPixel(8+it.x,8+it.y),'Player source pixel obscured')end
 if f==12 then assert(m.opaque==0)else assert(m.opaque>0)end
end
for _,t in ipairs(fxTags)do
 if t.loop then
  local maxDifference=0;for f=t.from+1,t.to do assert(difference(fx[t.from],fx[f],true)==0,'Loop geometry jitters');maxDifference=math.max(maxDifference,difference(fx[f-1],fx[f],false))end
  assert(difference(fx[t.to],fx[t.from],false)<=maxDifference,'Loop seam spike')
 end
end
report.player={frames=18,tags=3,centralExclusion=20,maxOpaquePixels=maxFx,playerPixelsUnchanged=true,idleLoop=true,intensifiedLoop=true,activationEndsTransparent=true}
for _,n in ipairs({'PresentationSupport_480x270.png','EquipmentFrames_480x270.png'})do local im=Image{fromFile=out..'/'..n};assert(im.width==480 and im.height==270)end
local gif=assert(app.open(out..'/PresentationSupport_480x270.gif'));assert(gif.width==480 and gif.height==270 and #gif.frames==30);gif:close()
manifest.previews={contact='PresentationSupport_ContactSheet.png',native='PresentationSupport_480x270.png',animated='PresentationSupport_480x270.gif',ui='EquipmentFrames_480x270.png',player='PlayerBoundary_ContactSheet.png'}
manifest.constraints={backgroundsAreRepresentativePlates=true,seamlessTilingClaimed=false,noApprovedArtModified=true,noUnityIntegration=true,backgroundQuietCenter={x=96,y=54,w=288,h=162},playerExclusionWindow=20}
write('manifest.json',manifest);write('validation.json',report)


