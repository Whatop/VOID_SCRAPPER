-- Compact supporting traces only: no player pixels are included in VFX exports.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgb(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function image(w,h)return Image(w,h,ColorMode.RGB)end
local function write(n,t)local f=assert(io.open(out..'/'..n,'w'));f:write(json.encode(t));f:close()end
local function render(s,f)local im=image(s.width,s.height);im:drawSprite(s,f,Point(0,0));return im end
local function corner(im,x,y,dx,dy,len,c)H.line(im,x,y,x+dx*(len-1),y,c);H.line(im,x,y,x,y+dy*(len-1),c)end
local inner={{2,2,1,1},{13,2,-1,1},{13,13,-1,-1},{2,13,1,-1}}
local outer={{1,1,1,1},{14,1,-1,1},{14,14,-1,-1},{1,14,1,-1}}
local function corners(im,points,len,c)for _,q in ipairs(points)do corner(im,q[1],q[2],q[3],q[4],len,c)end end
local layers={'Rectangular Traces','Energy Nodes','Displaced Fragments'}
local clips={
 {name='Idle',label='IDLE TRACE',first=1,last=2,loop=true,ms={400,400}},
 {name='Active',label='ACTIVE TRACE',first=3,last=6,loop=true,ms={140,140,140,140}},
 {name='DashBurst',label='DASH BURST',first=7,last=10,loop=false,ms={40,50,70,60}},
 {name='HitError',label='HIT ERROR',first=11,last=14,loop=false,ms={40,50,70,60}}
}
local palettes={
 {id='Neutral',region='Tutorial',label='NEUTRAL',hex={'3C5861','719EA9','ABCED6','E4F1EF'}},
 {id='RegionA',region='RegionA',label='GREEN',hex={'37584D','669F83','A2D2B3','E5F1E8'}},
 {id='RegionB',region='RegionB',label='ORANGE',hex={'65513B','B38C59','EDC98A','F5EAD5'}},
 {id='RegionC',region='RegionC',label='BLUE',hex={'3E5369','739FC8','AACFEB','E5F0F6'}},
 {id='Final',region='Final',label='PURPLE',hex={'554663','A17BBF','D0ABE8','F0E7F6'}}
}
local manifest={generatorId='player-rect-trace-v1',canvas={w=32,h=32},playerReference={w=16,h=16},clearCenter={x=6,y=6,w=20,h=20},paletteRoles={'Dim trace','Active energy','Bright node','Brief white flash'},families={},approvedArtModified=false,unityIntegrated=false}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local player=Image{fromFile=src..'/player.png'};assert(player.width==16 and player.height==16)
local old=assert(app.open(src..'/trace_reference.aseprite'));assert(old.width==32 and old.height==32 and #old.layers==3);old:close()
local dashRef=assert(app.open(src..'/dash_reference.aseprite'));assert(dashRef.width==32 and dashRef.height==32);dashRef:close()

local function draw(kind,f,C)
 local l={image(16,16),image(16,16),image(16,16)}
 if kind=='Idle'then
  corners(l[1],{inner[1],inner[3]},2,C[1]);H.put(l[1],13,2,C[1]);H.put(l[1],2,13,C[1])
  local q=f==1 and inner[1]or inner[3];H.put(l[2],q[1],q[2],C[2])
 elseif kind=='Active'then
  corners(l[1],inner,2,C[2]);H.rect(l[3],1,7,1,2,C[1]);H.rect(l[3],14,7,1,2,C[1])
  local q=inner[f];H.put(l[2],q[1],q[2],C[3])
 elseif kind=='DashBurst'then
  if f==1 then
   corners(l[1],inner,2,C[2]);H.rect(l[3],6,1,4,1,C[3]);H.put(l[2],2,2,C[4]);H.put(l[2],13,2,C[4])
  elseif f==2 then
   corners(l[1],outer,3,C[2]);H.put(l[2],1,1,C[4]);H.put(l[2],14,1,C[4])
   H.rect(l[3],5,14,2,1,C[1]);H.rect(l[3],9,14,2,1,C[1])
  elseif f==3 then
   H.rect(l[1],2,11,1,3,C[1]);H.rect(l[1],13,11,1,3,C[1])
   H.rect(l[3],5,14,2,1,C[1]);H.rect(l[3],9,14,2,1,C[1])
   H.rect(l[3],2,1,2,1,C[2]);H.rect(l[3],12,1,2,1,C[2])
  end
 elseif kind=='HitError'then
  if f==1 then
   corners(l[1],{{1,3,1,1},{13,1,-1,1},{2,14,1,-1},{14,12,-1,-1}},2,C[2])
   H.put(l[2],1,3,C[4]);H.put(l[2],13,1,C[4]);H.rect(l[3],14,7,1,2,C[2]);H.rect(l[3],7,2,2,1,C[3])
  elseif f==2 then
   H.rect(l[1],2,6,1,2,C[1]);H.rect(l[1],13,9,1,2,C[1]);H.rect(l[3],6,14,3,1,C[2]);H.rect(l[3],8,1,2,1,C[1])
  elseif f==3 then
   H.rect(l[1],1,8,2,1,C[1]);H.rect(l[1],13,5,2,1,C[1]);H.rect(l[3],2,2,2,1,C[2]);H.rect(l[3],11,13,3,1,C[1])
  end
 end
 local doubled={};for i,im in ipairs(l)do doubled[i]=H.scale(im,2)end;return doubled
end

local assets={};local frameClip,frameMs={},{}
for _,clip in ipairs(clips)do for f=clip.first,clip.last do frameClip[f]=clip;frameMs[f]=clip.ms[f-clip.first+1]end end
for r,spec in ipairs(palettes)do
 local C={};for i,h in ipairs(spec.hex)do C[i]=rgb(h)end
 local id='VFX_Player_RectTrace_'..spec.id;local s=Sprite(32,32,ColorMode.RGB)
 for i,name in ipairs(layers)do local l=i==1 and s.layers[1]or s:newLayer();l.name=name end
 local frames,flat,metadata,tags={},{},{},{};local sheet=image(448,32)
 for f=1,14 do
  if f>1 then s:newEmptyFrame(f)end;local clip=frameClip[f];frames[f]=draw(clip.name,f-clip.first+1,C)
  for n,l in ipairs(frames[f])do s:newCel(s.layers[n],f,l,Point(0,0))end;s.frames[f].duration=frameMs[f]/1000
  flat[f]=render(s,f);sheet:drawImage(flat[f],Point((f-1)*32,0))
  metadata[f]={filename=id..'_'..f,frame={x=(f-1)*32,y=0,w=32,h=32},rotated=false,trimmed=false,sourceSize={w=32,h=32},spriteSourceSize={x=0,y=0,w=32,h=32},duration=frameMs[f]}
 end
 for _,clip in ipairs(clips)do
  local t=s:newTag(clip.first,clip.last);t.name=clip.name;tags[#tags+1]={name=clip.name,from=clip.first-1,to=clip.last-1,direction='forward'}
  local strip=image((clip.last-clip.first+1)*32,32);for f=clip.first,clip.last do strip:drawImage(flat[f],Point((f-clip.first)*32,0))end;strip:saveAs(out..'/'..id..'_'..clip.name..'.png')
 end
 local pal=Palette(5);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,c in ipairs(C)do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255})end;s:setPalette(pal)
 s:saveAs(out..'/'..id..'.aseprite');s:close();sheet:saveAs(out..'/'..id..'.png')
 write(id..'.json',{frames=metadata,meta={app='Aseprite CLI + Lua',image=id..'.png',format='RGBA8888',size={w=448,h=32},frameTags=tags,scale='1'},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot={x=.5,y=.5},clips=clips,clearCenter=manifest.clearCenter,dashForward='up in source image',notes='VFX only. Center on the 16px player. Idle/Active are alternative loops. DashBurst/HitError end transparent. Do not add collision or treat as a shield.'}})
 assets[r]={id=id,frames=frames,flat=flat,C=C,spec=spec}
 manifest.families[r]={id=id,frames=14,tags=tags,layers=layers,palette=spec.hex}
end

local function playerTile(r,f)local im=image(32,32);im:drawImage(player,Point(8,8));im:drawImage(assets[r].flat[f],Point(0,0));return im end
local ink=rgb('CCDCE2');local muted=rgb('78959F');local dark=rgb('0D161E')
local contact=image(736,768);H.rect(contact,0,0,736,768,dark)
H.text(contact,'PLAYER RECTANGULAR TRACE',16,16,ink,3)
H.text(contact,'PLAYER INCLUDED ONLY FOR SCALE / EXPORTS ARE VFX ONLY',16,38,muted,1)
for row,clip in ipairs(clips)do local y=64+(row-1)*126;H.text(contact,clip.label,16,y+36,muted,2)
 for f=clip.first,clip.last do local x=172+(f-clip.first)*128;contact:drawImage(H.scale(playerTile(1,f),3),Point(x,y+16));H.text(contact,'F'..(f-clip.first+1),x,y+114,muted,1)end
end
H.text(contact,'SLOW LIGHT LOOP',444,102,muted,2);H.text(contact,'BODY STAYS CLEAR',444,122,muted,2)
H.text(contact,'MATCHED REGION PALETTES / ACTIVE TRACE',16,588,ink,2)
for r,spec in ipairs(palettes)do local x=24+(r-1)*140;H.text(contact,spec.label,x,614,assets[r].C[3],2);contact:drawImage(H.scale(playerTile(r,3),3),Point(x,638))end
H.text(contact,'IDLE AND ACTIVE LOOP / DASH AND ERROR END CLEAR',16,754,muted,1)
contact:saveAs(out..'/PlayerRectTrace_ContactSheet.png')

local backgrounds={};for r,spec in ipairs(palettes)do backgrounds[r]=Image{fromFile=src..'/background_'..spec.region..'.png'}end
local enemy=Image{fromFile=src..'/enemy.png'};local guard=Image{fromFile=src..'/guard.png'};local credit=Image{fromFile=src..'/credit.png'}
local function scene(r,f)
 local im=image(480,270);im:drawImage(backgrounds[r],Point(0,0));im:drawImage(enemy,Point(72,76));im:drawImage(guard,Point(350,152));im:drawImage(credit,Point(278,88))
 im:drawImage(playerTile(r,f),Point(224,118))
 -- Sparse projectile placeholders are review context, never included in VFX exports.
 H.rect(im,190,108,2,4,ink);H.rect(im,281,157,2,4,rgb('D8AF71'));H.rect(im,311,117,4,2,rgb('A5CDDA'))
 H.text(im,'PLAYER RECTANGULAR TRACE',16,14,ink,2);H.text(im,frameClip[f].label,16,34,assets[r].C[3],2)
 H.text(im,'ART PREVIEW / UNCHANGED PLAYER / NO UNITY CAPTURE',16,256,muted,1);return im
end
for r,spec in ipairs(palettes)do scene(r,3):saveAs(out..'/PlayerRectTrace_'..spec.id..'_480x270.png')end
scene(1,3):saveAs(out..'/PlayerRectTrace_480x270.png')
local sequence={1,2,1,2,1,2,3,4,5,6,3,4,5,6,7,8,9,10,1,2,11,12,13,14,1,2}
local gif=Sprite(480,270,ColorMode.RGB);gif.layers[1].name='Native art review only'
for i,f in ipairs(sequence)do if i>1 then gif:newEmptyFrame(i)end;gif:newCel(gif.layers[1],i,scene(1,f),Point(0,0));gif.frames[i].duration=frameMs[f]/1000 end
gif:saveAs(out..'/PlayerRectTrace_480x270.gif');gif:close()

local function difference(a,b,alpha)local n=0;for p in a:pixels()do local x=p();local y=b:getPixel(p.x,p.y);if alpha then x=pc.rgbaA(x);y=pc.rgbaA(y)end;if x~=y then n=n+1 end end;return n end
local function stats(im,white)local result={opaque=0,white=0,colors=0};local colors={}
 for p in im:pixels()do local c=p();local a=pc.rgbaA(c);assert(a==0 or a==255,'Soft alpha in art')
  if a>0 then result.opaque=result.opaque+1;colors[c]=true;if c==white then result.white=result.white+1 end end
 end;for _ in pairs(colors)do result.colors=result.colors+1 end;return result
end
local report={passed=true,pixelComparisons=0,families={},playerPixelIdentical=true,clearCenter=manifest.clearCenter,previewIsUnityCapture=false}
for r,a in ipairs(assets)do
 local saved=assert(app.open(out..'/'..a.id..'.aseprite'));assert(saved.width==32 and saved.height==32 and #saved.frames==14 and #saved.layers==3 and #saved.tags==4)
 local png=Image{fromFile=out..'/'..a.id..'.png'};assert(png.width==448 and png.height==32)
 local allowed={};for _,c in ipairs(a.C)do allowed[c]=true end;local maxima={Idle=0,Active=0,DashBurst=0,HitError=0}
 for f=1,14 do local im=render(saved,f);local m=stats(im,a.C[4]);local clip=frameClip[f]
  assert(math.floor(saved.frames[f].duration*1000+.5)==frameMs[f]);assert(m.colors<=4 and m.opaque<=96 and m.white<=8)
  maxima[clip.name]=math.max(maxima[clip.name],m.opaque)
  if f==10 or f==14 then assert(m.opaque==0,'One-shot cleanup is not transparent')else assert(m.opaque>0,'Unexpected blank frame')end
  if clip.loop then assert(m.white==0,'White flash in baseline loop')end
  for p in im:pixels()do local c=p();if pc.rgbaA(c)>0 then assert(allowed[c])end
   assert(c==png:getPixel((f-1)*32+p.x,p.y),'Source/export mismatch');report.pixelComparisons=report.pixelComparisons+1
   assert(c==im:getPixel(p.x-p.x%2,p.y-p.y%2),'Subpixel or non-grid art')
   if p.x>=6 and p.x<=25 and p.y>=6 and p.y<=25 then assert(pc.rgbaA(c)==0,'Player clear center violated')end
   if p.x<2 or p.y<2 or p.x>29 or p.y>29 then assert(pc.rgbaA(c)==0,'Effect clips canvas edge')end
  end
  local tile=playerTile(r,f);for p in player:pixels()do assert(p()==tile:getPixel(p.x+8,p.y+8),'Player source obscured')end
  assert(difference(im,assets[1].flat[f],true)==0,'Region variants changed geometry')
  for n,layer in ipairs(saved.layers)do local cel=layer:cel(f);if cel then stats(cel.image,a.C[4])else assert(stats(a.frames[f][n],a.C[4]).opaque==0,'Missing painted cel')end end
 end
 for n,l in ipairs(saved.layers)do assert(l.name==layers[n]and l.isVisible and l.opacity==255)end
 for n,clip in ipairs(clips)do local tag=saved.tags[n];assert(tag.name==clip.name and tag.fromFrame.frameNumber==clip.first and tag.toFrame.frameNumber==clip.last)
  local strip=Image{fromFile=out..'/'..a.id..'_'..clip.name..'.png'};assert(strip.width==(clip.last-clip.first+1)*32 and strip.height==32)
  for f=clip.first,clip.last do for p in a.flat[f]:pixels()do assert(p()==strip:getPixel((f-clip.first)*32+p.x,p.y))end end
  if clip.loop then local maxStep=0;for f=clip.first+1,clip.last do assert(difference(a.flat[clip.first],a.flat[f],true)==0,'Loop geometry jitters');maxStep=math.max(maxStep,difference(a.flat[f-1],a.flat[f],false))end
   assert(difference(a.flat[clip.last],a.flat[clip.first],false)<=maxStep,'Loop seam spike')
  end
 end
 assert(maxima.Idle==32 and maxima.Active==64 and maxima.DashBurst==96 and maxima.HitError<=64,'Readability hierarchy failed')
 assert(difference(a.flat[7],a.flat[11],true)>32,'Dash and error silhouettes are too similar')
 saved:close();report.families[r]={id=a.id,frames=14,tags=4,layers=3,maxOpaquePixelsByClip=maxima,regionGeometryMatched=true}
end
local native=Image{fromFile=out..'/PlayerRectTrace_480x270.png'};assert(native.width==480 and native.height==270)
local savedGif=assert(app.open(out..'/PlayerRectTrace_480x270.gif'));assert(savedGif.width==480 and savedGif.height==270 and #savedGif.frames==#sequence)
local duration=0;for i,f in ipairs(sequence)do local ms=math.floor(savedGif.frames[i].duration*1000+.5);assert(ms==frameMs[f]);duration=duration+ms end;savedGif:close()
report.nativePreviewFrames=#sequence;report.nativePreviewDurationMs=duration
manifest.previews={contact='PlayerRectTrace_ContactSheet.png',native='PlayerRectTrace_480x270.png',animated='PlayerRectTrace_480x270.gif'}
manifest.clips=clips;write('manifest.json',manifest);write('validation.json',report)
