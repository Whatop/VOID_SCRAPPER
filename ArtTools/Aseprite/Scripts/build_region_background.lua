-- Dedicated full-screen combat plates, developed from verified copies of the previous plates.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgb(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function write(n,t)local f=assert(io.open(out..'/'..n,'w'));f:write(json.encode(t));f:close()end
local function path(im,p,c)for i=1,#p-1 do H.line(im,p[i][1],p[i][2],p[i+1][1],p[i+1][2],c)end end
local function box(im,x,y,w,h,c)path(im,{{x,y},{x+w-1,y},{x+w-1,y+h-1},{x,y+h-1},{x,y}},c)end
local function arc(im,x,y,rx,ry,a,b,c)
 local prev
 for deg=a,b,3 do local t=math.rad(deg);local p={math.floor(x+rx*math.cos(t)+.5),math.floor(y+ry*math.sin(t)+.5)}
  if prev then H.line(im,prev[1],prev[2],p[1],p[2],c)end;prev=p
 end
end
local function image(w,h)return Image(w,h,ColorMode.RGB)end
local function flat(s)local im=image(s.width,s.height);im:drawSprite(s,1,Point(0,0));return im end
local specs={
 {id='Tutorial',label='TUTORIAL / OUTER SALVAGE ZONE',palette={'0B111A','101A24','192633','263643','354550','41515A'},role='Sparse training remnants and dim cyan guidance.'},
 {id='RegionA',label='REGION A / SALVAGE TRACES',palette={'0B1216','111D20','1A2B29','294137','30483E','3C5349'},role='Broken hull ribs, small salvage fragments and green recovery traces.'},
 {id='RegionB',label='REGION B / COMPRESSION AND STORAGE',palette={'130F11','211B1B','302625','45352B','514032','614A38'},role='Compression jaws, container cells and heavy defensive manufacturing remnants.'},
 {id='RegionC',label='REGION C / PHASE DRIFT',palette={'0A101B','111B2B','1B2B3D','293D52','314862','3B5370'},role='Broken phase lens, displaced routing segments and directional navigation traces.'},
 {id='Final',label='FINAL / NULL NETWORK',palette={'08090F','11111D','1D182D','2D223B','3A2A48','473355','575362'},role='Fractured authority frame, interrupted network buses and void cuts.'}
}
local layerNames={'Void Base','Distant Starfield','Peripheral Remnants','Mechanical Structure','Regional Traces','Fractures and Residue'}
local manifest={generatorId='region-background-v1',canvas={w=480,h=270},backgrounds={},quietCenter={x=96,y=54,w=288,h=162},pixelGrid=2,sourcePolicy='Verified copies only',seamless=false,unityIntegrated=false}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local rendered,allLayers={},{}
for region,spec in ipairs(specs)do
 local base=assert(app.open(src..'/base_'..spec.id..'.aseprite'));assert(base.width==480 and base.height==270 and #base.layers==4)
 local l={};for i=1,6 do l[i]=image(240,135)end
 -- Preserve the source palette, restrained field and broad edge arrangement.
 for old,new in pairs({[1]=1,[2]=2,[3]=3,[4]=5})do
  local full=image(480,270);local cel=assert(base.layers[old]:cel(1));full:drawImage(cel.image,cel.position)
  for y=0,134 do for x=0,239 do l[new]:drawPixel(x,y,full:getPixel(x*2,y*2))end end
 end;base:close()
 local c={};for i,h in ipairs(spec.palette)do c[i]=rgb(h)end
 if region==1 then
  -- Open, sparse practice sector. Small disconnected guide rails, no central reticle.
  H.poly(l[3],{{0,17},{19,17},{27,25},{23,31},{10,31},{0,27}},c[2])
  path(l[4],{{0,20},{16,20},{22,26},{18,28},{7,28}},c[3])
  H.rect(l[4],6,22,7,2,c[1]);H.rect(l[5],8,22,3,1,c[4])
  path(l[4],{{27,108},{39,108},{44,113},{44,125}},c[2])
  H.rect(l[4],27,111,7,1,c[3]);H.rect(l[5],43,121,1,3,c[4])
  H.poly(l[3],{{218,118},{239,118},{239,134},{225,134},{218,127}},c[2])
  path(l[4],{{222,121},{236,121},{236,129},{227,129},{222,124}},c[3])
  H.rect(l[4],226,124,8,2,c[1]);H.rect(l[5],228,123,2,1,c[4])
  path(l[5],{{60,14},{63,17},{60,20}},c[3]);path(l[5],{{176,117},{173,120},{176,123}},c[3])
  H.rect(l[6],32,18,3,1,c[2]);H.rect(l[6],211,129,2,2,c[2])
 elseif region==2 then
  -- A wrecked foredeck and a sheared drive block: salvage, not intact enemies.
  H.poly(l[3],{{0,59},{18,59},{30,67},{29,75},{37,84},{25,91},{0,88}},c[2])
  H.poly(l[4],{{0,64},{15,64},{23,70},{18,75},{28,83},{21,86},{0,83}},c[3])
  H.poly(l[4],{{0,70},{12,70},{17,74},{10,78},{0,77}},c[1])
  for y=63,83,5 do H.line(l[4],3,y,9,y+2,c[2])end
  path(l[4],{{20,62},{27,67},{26,75},{31,82},{24,87}},c[4])
  H.rect(l[5],20,79,3,1,c[5]);H.rect(l[5],27,69,1,3,c[4])
  H.poly(l[3],{{76,0},{106,0},{110,8},{98,15},{83,13},{76,7}},c[2])
  path(l[4],{{79,3},{102,3},{106,7},{98,11},{85,10}},c[3])
  for x=84,98,7 do H.rect(l[4],x,4,3,4,c[1])end
  H.rect(l[5],98,11,3,1,c[4])
  -- Recessed drive ribs inside the existing right-hand wreckage.
  H.poly(l[4],{{214,71},{227,71},{225,81},{215,85},{209,78}},c[1])
  for y=73,79,3 do H.line(l[4],216,y,223,y-1,c[3])end
  H.rect(l[5],227,75,1,3,c[5])
  path(l[4],{{196,119},{209,117},{219,121},{214,125},{201,127}},c[3])
  for x=199,211,6 do H.rect(l[4],x,120,2,4,c[1])end
  for _,p in ipairs({{34,94,3,2},{39,89,2,1},{201,56,4,1},{195,113,3,2},{68,13,3,1}})do H.rect(l[6],p[1],p[2],p[3],p[4],c[3])end
 elseif region==3 then
  -- Broken compression station along the sides; no broad arena-spanning rails.
  path(l[4],{{1,40},{24,40},{39,54},{36,79},{24,94},{1,94}},c[4])
  for y=53,77,6 do H.rect(l[4],2,y,12,3,c[2]);H.rect(l[4],3,y,8,1,c[3]);H.rect(l[4],14,y+1,3,2,c[4])end
  H.rect(l[4],20,56,4,19,c[2]);H.rect(l[4],21,59,2,13,c[4]);H.rect(l[5],21,61,1,3,c[5])
  H.poly(l[3],{{9,2},{40,2},{48,10},{44,24},{14,24},{9,18}},c[2])
  for x=15,33,9 do H.rect(l[4],x,7,6,12,c[3]);H.rect(l[4],x+1,9,4,7,c[1]);H.rect(l[4],x+2,10,2,1,c[4])end
  path(l[4],{{13,4},{38,4},{44,10}},c[4]);H.rect(l[5],14,21,5,1,c[5])
  H.poly(l[4],{{216,41},{224,41},{224,57},{220,61},{211,51}},c[3])
  H.rect(l[4],217,44,4,8,c[1]);H.rect(l[5],220,45,1,3,c[5])
  -- Bottom cargo shelves and exposed piston recesses.
  for x=126,168,14 do H.rect(l[4],x,129,10,3,c[2]);H.rect(l[4],x+2,130,6,1,c[4])end
  H.rect(l[4],119,118,9,5,c[2]);H.rect(l[4],120,119,6,2,c[1])
  H.rect(l[5],174,123,1,4,c[5]);H.rect(l[5],175,129,3,1,c[4])
  H.poly(l[3],{{211,105},{237,105},{239,111},{236,122},{214,122},{208,116}},c[2])
  box(l[4],215,108,18,11,c[3]);H.rect(l[4],217,111,14,4,c[1]);H.rect(l[5],228,118,4,1,c[4])
  H.rect(l[6],203,126,4,2,c[2]);H.rect(l[6],199,121,2,1,c[3])
 elseif region==4 then
  -- A broken navigation lens, offset guide arcs and short route terminals.
  for _,a in ipairs({{-75,-18},{8,66},{96,157},{182,228},{252,273}})do
   arc(l[3],217,67,21,34,a[1],a[2],c[3]);arc(l[4],217,67,18,29,a[1]+3,a[2]-3,c[2])
  end
  arc(l[5],217,67,21,34,194,213,c[4]);arc(l[5],217,67,21,34,25,42,c[4])
  H.poly(l[3],{{203,39},{208,34},{214,34},{214,39},{209,42}},c[2])
  path(l[4],{{205,39},{209,36},{212,36}},c[4])
  H.poly(l[3],{{214,96},{219,96},{225,91},{229,94},{222,102},{214,102}},c[2])
  path(l[5],{{218,98},{222,98},{225,95}},c[4]);H.rect(l[5],219,99,2,1,c[5])
  -- Duplicated-but-offset route edges suggest phase drift without noisy glitch.
  path(l[4],{{1,8},{26,8},{33,15},{58,15}},c[3])
  path(l[3],{{0,12},{23,12},{30,19},{54,19}},c[2])
  H.rect(l[3],36,18,5,2,0);H.rect(l[4],48,15,5,1,0)
  H.rect(l[5],56,14,3,2,c[4]);H.rect(l[6],63,15,3,1,c[3])
  H.poly(l[3],{{2,98},{18,98},{32,110},{30,119},{22,121},{9,110},{2,110}},c[2])
  path(l[4],{{8,102},{15,102},{27,111},{27,115}},c[3]);H.rect(l[5],17,107,2,2,c[4])
  path(l[4],{{159,115},{174,115},{181,123},{201,123}},c[2])
  path(l[5],{{175,119},{179,123},{184,123}},c[3]);H.rect(l[6],195,126,4,1,c[3])
 elseif region==5 then
  -- Fractured SYSTEM foundations; dark holes and interrupted buses, not bright haze.
  path(l[4],{{1,35},{26,35},{37,46},{29,60}},c[3])
  path(l[4],{{32,65},{38,78},{26,99},{1,94}},c[3])
  H.poly(l[4],{{7,38},{19,38},{29,47},{23,55},{15,49},{7,49}},c[2])
  path(l[5],{{8,41},{16,41},{22,47},{19,50}},c[5])
  H.rect(l[5],21,44,2,1,c[7]);H.rect(l[5],26,59,3,1,c[4])
  path(l[5],{{3,78},{13,78},{17,84},{27,84}},c[4])
  H.rect(l[5],14,80,3,1,0);H.rect(l[6],19,86,3,1,c[5])
  -- Cropped authority bus at the top with disconnected, displaced sockets.
  path(l[4],{{90,2},{90,9},{100,17},{111,12},{123,18},{137,19}},c[3])
  path(l[5],{{117,1},{117,8},{123,13},{132,13}},c[4])
  H.rect(l[5],123,13,3,1,0);H.rect(l[6],124,16,3,1,c[5])
  H.rect(l[5],151,5,2,5,c[4]);H.rect(l[6],154,10,3,1,c[7])
  H.poly(l[4],{{222,45},{239,45},{239,50},{229,50},{221,62},{216,59}},c[3])
  path(l[5],{{224,50},{221,54},{224,60},{221,67}},c[5])
  H.rect(l[5],223,57,2,1,0);H.rect(l[6],226,58,3,1,c[4])
  H.poly(l[4],{{219,86},{224,90},{236,90},{239,94},{225,97},{215,91}},c[3])
  H.rect(l[5],228,92,4,1,c[5]);H.rect(l[6],230,89,2,1,c[7])
  path(l[4],{{170,132},{170,120},{176,120}},c[3])
  path(l[5],{{185,134},{185,124},{191,124},{195,128}},c[4])
  H.rect(l[5],185,128,1,3,0);H.rect(l[6],188,129,1,3,c[5])
  for _,q in ipairs({{36,105},{42,111},{155,119},{162,122},{203,113},{65,18},{68,22}})do H.rect(l[6],q[1],q[2],3,1,c[3])end
 end
 -- Final source uses six independently editable layers and an explicitly limited palette.
 local s=Sprite(480,270,ColorMode.RGB);local scaled={}
 for i,name in ipairs(layerNames)do local layer=i==1 and s.layers[1]or s:newLayer();layer.name=name;scaled[i]=H.scale(l[i],2);s:newCel(layer,1,scaled[i],Point(0,0))end
 local pal=Palette(#spec.palette+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0})
 for i,h in ipairs(spec.palette)do local col=rgb(h);pal:setColor(i,Color{r=pc.rgbaR(col),g=pc.rgbaG(col),b=pc.rgbaB(col),a=255})end;s:setPalette(pal)
 local tag=s:newTag(1,1);tag.name='Background';s.frames[1].duration=.1
 local id='BG_'..spec.id;local im=flat(s);im:saveAs(out..'/'..id..'.png');s:saveAs(out..'/'..id..'.aseprite');s:close()
 local overlay=image(480,270);for i=2,6 do overlay:drawImage(scaled[i],Point(0,0))end;overlay:saveAs(out..'/'..id..'_Overlay.png')
 rendered[region]=im;allLayers[region]=scaled
 manifest.backgrounds[#manifest.backgrounds+1]={id=id,sourceCopy='base_'..spec.id..'.aseprite',role=spec.role,layers=layerNames,palette=spec.palette}
end

-- All five full-size plates on one sheet, with no resizing or labels inside the art.
local ink=rgb('B2C2C9');local muted=rgb('6D8794');local sheet=image(1008,1002);H.rect(sheet,0,0,1008,1002,rgb('111821'))
H.text(sheet,'VOID SCRAPPER / REGION BACKGROUNDS',16,16,ink,3)
for i,spec in ipairs(specs)do local x=i%2==1 and 16 or 512;local y=58+math.floor((i-1)/2)*310
 H.text(sheet,spec.label,x,y,ink,2);sheet:drawImage(rendered[i],Point(x,y+24))
end
H.text(sheet,'NATIVE RESOLUTION PLATES',536,732,ink,2)
H.text(sheet,'OPEN DARK COMBAT CENTERS',536,762,muted,2)
H.text(sheet,'EDGE WEIGHTED REGION IDENTITY',536,792,muted,2)
H.text(sheet,'HARD PIXELS / LIMITED PALETTES',536,822,muted,2)
H.text(sheet,'SIX EDITABLE LAYERS PER REGION',536,852,muted,2)
sheet:saveAs(out..'/RegionBackground_ContactSheet.png')

-- Optional native composition checks use unchanged player/enemy/pickup references.
-- These previews are not Unity runtime evidence or baked gameplay content.
local player=Image{fromFile=src..'/player.png'};local enemy=Image{fromFile=src..'/enemy.png'};local guard=Image{fromFile=src..'/guard.png'};local credit=Image{fromFile=src..'/credit.png'}
assert(player.width==16 and player.height==16);assert(enemy.width==64 and guard.width==64)
local previewSheet=image(1008,1002);H.rect(previewSheet,0,0,1008,1002,rgb('111821'));H.text(previewSheet,'NATIVE COMBAT READABILITY / ASSET COMPOSITES',16,16,ink,2)
for i,spec in ipairs(specs)do
 local im=image(480,270);im:drawImage(rendered[i],Point(0,0));im:drawImage(enemy,Point(78,76));im:drawImage(guard,Point(330,152));im:drawImage(player,Point(232,127));im:drawImage(credit,Point(282,84))
 -- A few tiny stand-in projectile marks, not additional deliverable VFX.
 H.rect(im,178,125,2,6,rgb('D9E9D7'));H.rect(im,216,91,2,4,rgb('CFDEAA'));H.rect(im,268,159,3,2,rgb('E4A25D'));H.rect(im,311,113,4,2,rgb('AFDAE6'))
 im:saveAs(out..'/Readability_'..spec.id..'_480x270.png')
 local x=i%2==1 and 16 or 512;local y=58+math.floor((i-1)/2)*310;H.text(previewSheet,spec.label,x,y,ink,2);previewSheet:drawImage(im,Point(x,y+24))
end
H.text(previewSheet,'ART REVIEW / NO UNITY CAPTURE',536,738,ink,2)
H.text(previewSheet,'SPRITES APPEAR ONLY IN PREVIEWS',536,770,muted,2)
H.text(previewSheet,'BACKGROUND EXPORTS ARE CLEAN',536,802,muted,2)
previewSheet:saveAs(out..'/RegionBackground_Readability.png')

-- Verify final files and the larger-than-center-third quiet area.
local report={passed=true,backgrounds={},pixelComparisons=0,previewIsUnityCapture=false}
local masks={}
for i,spec in ipairs(specs)do
 local id='BG_'..spec.id;local s=assert(app.open(out..'/'..id..'.aseprite'));local png=Image{fromFile=out..'/'..id..'.png'};local im=flat(s)
 assert(s.width==480 and s.height==270 and #s.frames==1 and #s.layers==6 and #s.tags==1 and s.tags[1].name=='Background')
 local allowed,seen={},{};for _,h in ipairs(spec.palette)do allowed[rgb(h)]=true end
 local maxLum,centralStars,detailCount,changed=0,0,0,0;local centerBase=rgb(spec.palette[1]);local base=assert(app.open(src..'/base_'..spec.id..'.aseprite'));local original=flat(base);base:close()
 local overlay=Image{fromFile=out..'/'..id..'_Overlay.png'};masks[i]=overlay
 for p in im:pixels()do local color=p();assert(pc.rgbaA(color)==255 and allowed[color],'Background palette or opacity violation')
  assert(color==png:getPixel(p.x,p.y),'Source and PNG differ');report.pixelComparisons=report.pixelComparisons+1
  assert(color==im:getPixel(p.x-p.x%2,p.y-p.y%2),'Non-integer construction grid')
  seen[color]=true;maxLum=math.max(maxLum,.2126*pc.rgbaR(color)+.7152*pc.rgbaG(color)+.0722*pc.rgbaB(color))
  if color~=original:getPixel(p.x,p.y)then changed=changed+1 end
  if pc.rgbaA(overlay:getPixel(p.x,p.y))>0 then detailCount=detailCount+1 end
  if p.x>=96 and p.x<=383 and p.y>=54 and p.y<=215 then
   for n=3,6 do assert(pc.rgbaA(allLayers[i][n]:getPixel(p.x,p.y))==0,'Structural pixel in clear combat center')end
   if color~=centerBase then centralStars=centralStars+1;assert(color==rgb(spec.palette[2])or color==rgb(spec.palette[3]),'Center star is too bright')end
  end
 end
 local colors=0;for _ in pairs(seen)do colors=colors+1 end
 assert(colors<=7 and maxLum<=86 and centralStars<=100 and detailCount<480*270*.23,'Density/contrast limit failed for '..id)
 assert(changed>200,'Background was not developed beyond the base plate')
 for n,layer in ipairs(s.layers)do assert(layer.name==layerNames[n]and layer.opacity==255 and layer.isVisible)
  for p in allLayers[i][n]:pixels()do local a=pc.rgbaA(p());assert(a==0 or a==255,'Soft alpha in source layer')end
 end
 local remade=image(480,270);for n=1,6 do remade:drawImage(allLayers[i][n],Point(0,0))end
 for p in remade:pixels()do assert(p()==png:getPixel(p.x,p.y),'Layer reassembly mismatch')end
 s:close();report.backgrounds[#report.backgrounds+1]={id=id,canvas='480x270',layers=6,colors=colors,maxLuminance=maxLum,centerStructurePixels=0,centerStarPixels=centralStars,overlayPixels=detailCount,changedFromReference=changed}
end
for i=1,5 do for j=i+1,5 do local different=0;for p in masks[i]:pixels()do if pc.rgbaA(p())~=pc.rgbaA(masks[j]:getPixel(p.x,p.y))then different=different+1 end end;assert(different>1000,'Background identity is just a palette swap')end end
-- Native sheet art panels must exactly equal the source PNGs.
local savedSheet=Image{fromFile=out..'/RegionBackground_ContactSheet.png'}
for i,im in ipairs(rendered)do local x=i%2==1 and 16 or 512;local y=82+math.floor((i-1)/2)*310;for p in im:pixels()do assert(p()==savedSheet:getPixel(x+p.x,y+p.y),'Contact sheet changes native pixels')end end
manifest.previews={contact='RegionBackground_ContactSheet.png',readability='RegionBackground_Readability.png'}
write('manifest.json',manifest);write('validation.json',report)
