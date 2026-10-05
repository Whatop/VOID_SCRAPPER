-- Physical loot silhouettes with separate rarity frames. Authored via Aseprite CLI + Lua.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,diamond,scale=h.put,h.rect,h.line,h.poly,h.oct,h.diamond,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local hex={metal={'18252B','394A52','788B93','B7C6C9','E5EBE7'},gold={'80562C','CA9D43','F3D989'},teal={'28626A','59AFAD','B8E9DA'},green={'286C49','76D879','B7F3AF'},orange={'995028','FFBA53','FFE397'},blue={'285E91','68C7ED','ABE8F4'},neutral={'39474D','61727A','8C9FA5','B4C5C9','D7E6E3','F0F2E9'},purple={'603785','AF89CC','DDC9EC'}}
local P={};for k,values in pairs(hex) do P[k]={};for i,v in ipairs(values) do P[k][i]=rgba(v) end end
local M=P.metal;local white=P.neutral[6]
local font={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,label,x,y,c,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if font[ch] then for yy,row in ipairs(font[ch]) do for xx=1,3 do if row:sub(xx,xx)=='1' then rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,c) end end end else h.text(im,ch,px,y,c,n) end
 end
end
local layerNames={'Body','Material Accent','Details','Idle Sparkle'}
local function layers(size) return {Image(size,size,ColorMode.RGB),Image(size,size,ColorMode.RGB),Image(size,size,ColorMode.RGB),Image(size,size,ColorMode.RGB)} end
local function outline(im,pts,c) for i=1,#pts do local a,b=pts[i],pts[i%#pts+1];line(im,a[1],a[2],b[1],b[2],c) end end
local function credits()
 local l=layers(16);local c=P.gold
 oct(l[1],2,6,10,8,1,M[1]);oct(l[1],3,7,8,6,1,c[1]);line(l[3],4,12,9,12,c[2])
 oct(l[1],4,2,10,9,2,c[1]);oct(l[2],5,3,8,7,1,c[2]);line(l[2],6,3,11,3,c[3]);line(l[2],5,4,5,8,c[3])
 rect(l[3],7,5,2,3,c[1]);rect(l[3],10,5,1,2,c[3]);rect(l[3],7,8,4,1,c[3])
 return l
end
local function scrap()
 local l=layers(16)
 poly(l[1],{{2,4},{5,2},{8,4},{11,3},{13,6},{11,8},{14,10},{12,13},{8,12},{5,14},{2,11},{3,8},{1,6}},M[1])
 poly(l[1],{{3,5},{5,3},{9,6},{12,5},{11,8},{8,9},{6,12},{3,10},{4,7}},M[3]);line(l[2],4,4,8,7,M[4])
 poly(l[2],{{9,8},{12,9},{12,11},{10,12},{7,10}},M[2]);line(l[3],10,9,12,10,M[4]);rect(l[3],6,6,3,2,M[1]);rect(l[3],3,10,3,2,P.orange[1]);put(l[3],3,10,P.orange[2])
 return l
end
local function chip()
 local l=layers(16);local c=P.teal
 for _,y in ipairs({5,8,10}) do rect(l[1],2,y,2,1,M[3]);rect(l[1],12,y,2,1,M[3]) end
 for _,x in ipairs({5,8,10}) do rect(l[1],x,2,1,2,M[3]);rect(l[1],x,12,1,2,M[3]) end
 rect(l[1],4,4,8,8,M[1]);rect(l[1],5,5,6,6,c[1]);rect(l[2],6,6,4,4,c[2]);line(l[2],6,6,9,6,c[3]);line(l[3],6,7,6,9,c[3]);line(l[3],7,9,9,9,c[1]);put(l[3],9,8,c[3])
 return l
end
local function resourceA()
 local l=layers(16);local c=P.green
 poly(l[1],{{6,2},{10,2},{12,5},{11,12},{9,14},{5,12},{4,5}},M[1])
 poly(l[2],{{6,4},{10,4},{11,6},{10,11},{8,13},{5,11},{5,6}},c[1]);poly(l[2],{{6,4},{8,4},{8,12},{6,10}},c[2]);line(l[2],7,5,7,10,c[3])
 rect(l[1],6,2,4,2,M[3]);line(l[3],6,2,9,2,M[4]);line(l[3],5,11,7,13,M[3]);line(l[3],9,12,10,10,M[3])
 return l
end
local function resourceB()
 local l=layers(16);local c=P.orange
 poly(l[1],{{4,4},{10,3},{13,6},{12,11},{8,13},{3,11},{2,7}},M[1])
 poly(l[2],{{4,5},{10,4},{12,6},{11,10},{8,12},{4,10},{3,7}},c[1]);poly(l[2],{{4,5},{9,4},{10,7},{7,10},{3,7}},c[2]);line(l[2],4,5,8,4,c[3]);poly(l[2],{{10,7},{12,6},{11,10},{8,12},{8,10}},c[2])
 line(l[3],4,9,10,8,c[1]);rect(l[3],3,10,3,2,M[3]);rect(l[3],10,9,2,2,M[3]);put(l[3],4,10,M[4])
 return l
end
local function resourceC()
 local l=layers(16);local c=P.blue
 oct(l[1],2,3,12,11,3,M[1]);oct(l[2],3,4,10,9,3,c[1]);oct(l[2],4,4,8,8,2,c[2]);oct(l[2],6,6,4,4,1,0)
 rect(l[2],10,4,3,3,0);rect(l[1],11,3,3,3,0);line(l[3],5,4,8,4,c[3]);line(l[3],4,5,4,8,c[3]);line(l[3],9,10,11,8,c[3])
 poly(l[3],{{10,2},{13,4},{10,6}},c[3]);rect(l[1],6,12,4,2,M[3]);line(l[3],6,12,9,12,M[4])
 return l
end
local function heal()
 local l=layers(16);local c=P.green
 rect(l[1],6,2,4,3,M[2]);rect(l[1],7,3,2,2,0)
 oct(l[1],2,5,12,9,2,M[1]);oct(l[1],3,6,10,7,1,M[4]);line(l[2],4,6,11,6,M[5]);rect(l[3],3,10,1,2,M[2]);rect(l[3],12,9,1,2,M[2])
 rect(l[2],7,7,2,5,c[1]);rect(l[2],5,9,6,2,c[1]);rect(l[3],7,7,2,2,c[2]);put(l[3],5,9,c[2]);line(l[3],6,13,9,13,M[3])
 return l
end
local function traitContainer()
 local l=layers(24)
 oct(l[1],5,3,14,18,3,M[1]);oct(l[1],6,4,12,16,2,M[3]);oct(l[2],7,5,10,14,1,M[4]);line(l[2],8,4,15,4,M[5])
 rect(l[1],4,8,2,8,M[2]);rect(l[1],18,7,2,8,M[2]);rect(l[3],4,9,1,4,M[4]);rect(l[3],19,8,1,4,M[4])
 rect(l[2],8,7,8,8,M[1]);line(l[3],8,6,15,6,M[2]);line(l[3],8,15,15,15,M[2])
 rect(l[3],9,17,3,1,M[2]);rect(l[3],12,18,3,1,M[2]);rect(l[3],10,20,4,1,M[4])
 return l
end
local function reinforcementContainer()
 local l=layers(24)
 oct(l[1],3,6,18,14,2,M[1]);oct(l[1],4,7,16,12,1,M[3]);rect(l[2],5,8,14,10,M[2])
 rect(l[1],1,9,3,7,M[1]);rect(l[1],20,9,3,7,M[1]);line(l[3],1,10,1,14,M[3]);line(l[3],22,10,22,14,M[3])
 rect(l[2],6,4,12,2,M[2]);line(l[3],7,4,16,4,M[4]);rect(l[3],5,7,3,2,M[4]);rect(l[3],16,7,3,2,M[4])
 rect(l[2],8,8,8,8,M[1]);rect(l[3],5,17,3,2,M[4]);rect(l[3],16,17,3,2,M[4]);rect(l[3],6,19,3,2,M[2]);rect(l[3],15,19,3,2,M[2]);line(l[3],10,18,13,18,M[3])
 return l
end
-- Preserve the approved chipped diamond exactly, recoloring only its six-step ramp.
local approved=assert(app.open(sourceDir..'/core_green_shard.aseprite'));assert(approved.width==32 and approved.height==32 and #approved.layers==3,'Approved shard changed')
local greenHex={'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'};local map={};for i,v in ipairs(greenHex) do map[rgba(v)]=P.neutral[i] end
local shardLayers={}
for li=1,3 do local cel=approved.layers[li]:cel(1);local im=Image(32,32,ColorMode.RGB);im:drawImage(cel.image,cel.position)
 for it in im:pixels() do if pc.rgbaA(it())>0 then it(assert(map[it()],'Unexpected approved shard color')) end end;shardLayers[li]=im
end
approved:close()
local function shard(f)
 local l={Image(shardLayers[1]),Image(shardLayers[2]),Image(shardLayers[3]),Image(32,32,ColorMode.RGB)}
 if f==2 then put(l[4],23,9,P.neutral[5]) elseif f==3 then line(l[4],22,9,24,9,white);line(l[4],23,8,23,10,white) elseif f==4 then put(l[4],24,10,P.neutral[3]) end
 return l
end
local items={
 {id='Pickup_Credit',label='CREDIT',size=16,scale=1,make=credits,colors={'metal','gold'}},
 {id='Pickup_Scrap',label='SCRAP',size=16,scale=1,make=scrap,colors={'metal','orange'}},
 {id='Pickup_CoreShard',label='CORE SHARD',size=32,scale=1,make=shard,colors={'neutral'},durations={900,120,100,380},tag='Idle',approvedMask=true},
 {id='Pickup_TuningChip',label='TUNING CHIP',size=32,scale=2,make=chip,colors={'metal','teal'}},
 {id='Pickup_RegionResource_A',label='REGION A',size=32,scale=2,make=resourceA,colors={'metal','green'}},
 {id='Pickup_RegionResource_B',label='REGION B',size=32,scale=2,make=resourceB,colors={'metal','orange'}},
 {id='Pickup_RegionResource_C',label='REGION C',size=32,scale=2,make=resourceC,colors={'metal','blue'}},
 {id='Pickup_Heal',label='REPAIR KIT',size=32,scale=2,make=heal,colors={'metal','green'}},
 {id='Pickup_TraitContainer',label='TRAIT CONTAINER',size=48,scale=2,make=traitContainer,colors={'metal'},socket={x=16,y=14,w=16,h=16}},
 {id='Pickup_ReinforcementContainer',label='REINFORCEMENT',size=48,scale=2,make=reinforcementContainer,colors={'metal'},socket={x=16,y=16,w=16,h=16}}
}
local rarities={{id='Common',colors={P.neutral[3],P.neutral[5]},hex={hex.neutral[3],hex.neutral[5]}},{id='Rare',colors={P.blue[1],P.blue[3]},hex={hex.blue[1],hex.blue[3]}},{id='Legendary',colors={P.gold[2],P.gold[3]},hex={hex.gold[2],hex.gold[3]}},{id='Curse',colors={P.purple[1],P.purple[2]},hex={hex.purple[1],hex.purple[2]}}}
local function rarity(size,kind)
 local frame,glow=Image(size,size,ColorMode.RGB),Image(size,size,ColorMode.RGB)
 local m=size==16 and 1 or size==32 and 2 or 3;local arm=size==16 and 2 or size==32 and 4 or 6;local far=size-1-m;local c=kind.colors
 for _,sx in ipairs({0,1}) do for _,sy in ipairs({0,1}) do
  local x=sx==0 and m or far;local y=sy==0 and m or far;local dx=sx==0 and 1 or -1;local dy=sy==0 and 1 or -1
  if kind.id=='Curse' then
   local shift=(sx==sy) and 1 or 0;line(frame,x,y+dy*shift,x+dx*(arm-1),y+dy*shift,c[1]);line(frame,x,y+dy*2,x,y+dy*arm,c[2]);put(glow,x+dx*arm,y,c[2])
  else line(frame,x,y,x+dx*(arm-1),y,c[1]);line(frame,x,y,x,y+dy*(arm-1),c[1]);put(glow,x,y,c[2]) end
 end end
 local mid=size//2
 if kind.id=='Rare' then line(glow,mid-1,m,mid,m,c[2]);line(glow,mid-1,far,mid,far,c[2])
 elseif kind.id=='Legendary' then
  if size==48 then line(glow,mid-2,m,mid-1,m,c[2]);line(glow,mid+1,m,mid+2,m,c[2])
  else line(glow,m,mid-1,m,mid,c[2]);line(glow,far,mid-1,far,mid,c[2]) end
 end
 return {frame,glow}
end
local function saveJson(path,v) local f=assert(io.open(path,'w'));f:write(json.encode(v));f:close() end
local manifest={generatorId='void-scrapper-world-pickups-v1',name='WORLD REWARD / PICKUP VISUAL PACK',target={width=480,height=270,pixelsPerUnit=32,binaryAlpha=true},pickups={},rarityOverlays={},preview='WorldPickups_ContactSheet.png',nativePreview='WorldPickups_480x270.png',animatedPreview='WorldPickups_480x270.gif',totalSources=13,totalFrames=25,
 overlayPolicy='One base pickup plus one matching-size rarity overlay. No baked item-rarity combinations. Rarity is optional for common currency.',iconPolicy='Assign the actual item icon at the documented socket in Unity. Blank Item Icon Overlay source layer is not a placeholder graphic.'}
local rendered={};local byId={}
for _,item in ipairs(items) do
 local size=item.size;local ms=item.durations or {100};local s=Sprite(size,size,ColorMode.RGB);local names={table.unpack(layerNames)};if item.socket then names[#names+1]='Item Icon Overlay' end
 for i,name in ipairs(names) do local l=i==1 and s.layers[1] or s:newLayer();l.name=name end
 local images={};local sheet=Image(size*#ms,size,ColorMode.RGB);local meta={};local palette={};for _,group in ipairs(item.colors) do for _,v in ipairs(hex[group]) do palette[#palette+1]=v end end
 for fi,duration in ipairs(ms) do
  if fi>1 then s:newEmptyFrame(fi) end;local ls=item.make(fi);local flat=Image(size,size,ColorMode.RGB)
  for li,im in ipairs(ls) do local pixels=scale(im,item.scale);s:newCel(s.layers[li],fi,pixels,Point(0,0));flat:drawImage(pixels,Point(0,0)) end
  s.frames[fi].duration=duration/1000;images[fi]=flat;sheet:drawImage(flat,Point((fi-1)*size,0))
  meta[#meta+1]={filename=item.id..'_'..string.format('%02d',fi-1),frame={x=(fi-1)*size,y=0,w=size,h=size},sourceSize={w=size,h=size},spriteSourceSize={x=0,y=0,w=size,h=size},rotated=false,trimmed=false,duration=duration}
 end
 local tag=s:newTag(1,#ms);tag.name=item.tag or 'Static';s:saveAs(outputDir..'/'..item.id..'.aseprite');s:close()
 images[1]:saveAs(outputDir..'/'..item.id..'.png');local sheetName=item.id..(#ms>1 and '_Idle' or '')..'.png';if #ms>1 then sheet:saveAs(outputDir..'/'..sheetName) end
 local rec={id=item.id,label=item.label,width=size,height=size,aseprite=item.id..'.aseprite',base=item.id..'.png',sheet=sheetName,metadata=item.id..'.json',frames=#ms,durationsMs=ms,tag=item.tag or 'Static',mode=#ms>1 and 'loop' or 'static',layers=names,palette=palette,constructionPixelScale=item.scale,pivot={x=0.5,y=0.5},pixelsPerUnit=32,socket=item.socket,approvedCoreMask=item.approvedMask,raritySource='Pickup_Rarity_'..size..'.aseprite'}
 saveJson(outputDir..'/'..rec.metadata,{frames=meta,meta={app='Aseprite CLI + Lua',image=sheetName,format='RGBA8888',size={w=sheet.width,h=size},frameTags={{name=rec.tag,from=0,to=#ms-1,direction='forward'}}},unity={pixelsPerUnit=32,pivot=rec.pivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',loop=#ms>1,iconSocket=item.socket}})
 manifest.pickups[#manifest.pickups+1]=rec;rendered[#rendered+1]={item=item,frames=images};byId[item.id]={item=item,frames=images}
end
local rarityImages={}
for _,size in ipairs({16,32,48}) do
 local id='Pickup_Rarity_'..size;local s=Sprite(size,size,ColorMode.RGB);s.layers[1].name='Frame';s:newLayer().name='Glow Marks';local sheet=Image(size*4,size,ColorMode.RGB);local meta={};local tags={};local rec={id=id,width=size,height=size,aseprite=id..'.aseprite',sheet=id..'.png',metadata=id..'.json',layers={'Frame','Glow Marks'},pixelsPerUnit=32,pivot={x=0.5,y=0.5},variants={}};rarityImages[size]={}
 for i,kind in ipairs(rarities) do
  if i>1 then s:newEmptyFrame(i) end;local ls=rarity(size,kind);local flat=Image(size,size,ColorMode.RGB)
  for li,im in ipairs(ls) do s:newCel(s.layers[li],i,im,Point(0,0));flat:drawImage(im,Point(0,0)) end
  s.frames[i].duration=0.1;sheet:drawImage(flat,Point((i-1)*size,0));local filename=id..'_'..kind.id..'.png';flat:saveAs(outputDir..'/'..filename);rarityImages[size][kind.id]=flat
  tags[#tags+1]={name=kind.id,from=i-1,to=i-1,direction='forward'};meta[#meta+1]={filename=id..'_'..kind.id,frame={x=(i-1)*size,y=0,w=size,h=size},sourceSize={w=size,h=size},spriteSourceSize={x=0,y=0,w=size,h=size},rotated=false,trimmed=false,duration=100}
  rec.variants[#rec.variants+1]={tag=kind.id,frame=i,png=filename,palette=kind.hex}
 end
 for i,tag in ipairs(tags) do local t=s:newTag(i,i);t.name=tag.name end
 s:saveAs(outputDir..'/'..rec.aseprite);s:close();sheet:saveAs(outputDir..'/'..rec.sheet)
 saveJson(outputDir..'/'..rec.metadata,{frames=meta,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=sheet.width,h=size},frameTags=tags},unity={pixelsPerUnit=32,pivot=rec.pivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',loop=false}});manifest.rarityOverlays[#manifest.rarityOverlays+1]=rec
end
local function checker(im,x,y,w,height,step) for yy=0,height-1 do for xx=0,w-1 do put(im,x+xx,y+yy,(xx//step+yy//step)%2==0 and rgba('222D38') or rgba('2A3540')) end end end
local contact=Image(1040,1020,ColorMode.RGB);contact:clear(Color{r=18,g=24,b=32,a=255})
text(contact,'WORLD REWARD / PICKUP PACK',20,16,white,3);text(contact,'10 PICKUPS / 4 RARITIES / 3 OVERLAY SIZES',20,45,M[4],2)
for i,r in ipairs(rendered) do
 local x=16+(i-1)%5*206;local y=85+math.floor((i-1)/5)*177;local size=r.item.size
 text(contact,r.item.label,x+4,y,white,1);text(contact,size..' X '..size..' / '..(#r.frames>1 and '4F IDLE' or 'STATIC'),x+4,y+15,M[4],1)
 checker(contact,x+4,y+32,192,112,8);local shown=scale(r.frames[1],size==16 and 6 or size==32 and 3 or 2);contact:drawImage(shown,Point(x+4+(192-shown.width)//2,y+32+(112-shown.height)//2))
 text(contact,size==16 and '6X REVIEW' or size==32 and '3X REVIEW' or '2X REVIEW',x+4,y+154,M[3],1)
end
text(contact,'REUSABLE RARITY OVERLAYS / EMPTY CENTERS',20,450,white,2)
for row,size in ipairs({16,32,48}) do
 local y=478+(row-1)*115;text(contact,size..' X '..size,20,y+42,M[4],2)
 for i,kind in ipairs(rarities) do
  local x=158+(i-1)*216;checker(contact,x,y,160,90,8);local shown=scale(rarityImages[size][kind.id],size==16 and 4 or size==32 and 2 or 1);contact:drawImage(shown,Point(x+(160-shown.width)//2,y+(90-shown.height)//2));text(contact,kind.id:upper(),x,y+97,M[4],1)
 end
end
text(contact,'ICON SOCKETS / 16 X 16 / EXAMPLE ICONS ARE PREVIEW ONLY',20,843,white,2)
for i,id in ipairs({'Pickup_TraitContainer','Pickup_ReinforcementContainer'}) do
 local r=byId[id];local example=Image(r.frames[1]);example:drawImage(byId.Pickup_Credit.frames[1],Point(r.item.socket.x,r.item.socket.y))
 local x=40+(i-1)*300;checker(contact,x,875,120,120,8);contact:drawImage(scale(example,2),Point(x+12,887));text(contact,r.item.label,x+133,906,M[4],1)
end
contact:saveAs(outputDir..'/'..manifest.preview)
local function coreAt(t) local total=0;for i,ms in ipairs(items[3].durations) do total=total+ms;if t<total then return i end end;return 1 end
local function board(t)
 local im=Image(480,270,ColorMode.RGB);im:clear(Color{r=15,g=21,b=29,a=255});text(im,'WORLD PICKUPS / 480 X 270 / PPU 32 / NATIVE SIZE',8,6,M[4],1)
 for i,r in ipairs(rendered) do
  local x=(i-1)%5*96;local y=24+math.floor((i-1)/5)*79;rect(im,x+1,y,94,76,rgba(i%2==0 and '1C2732' or '17212C'))
  local frame=r.item.id=='Pickup_CoreShard' and coreAt(t) or 1;im:drawImage(r.frames[frame],Point(x+(96-r.item.size)//2,y+3+(48-r.item.size)//2));text(im,r.item.label,x+4,y+62,M[4],1)
 end
 text(im,'SAME CORE SHARD / OPTIONAL RARITY OVERLAY',8,191,M[4],1)
 for i,kind in ipairs(rarities) do local x=(i-1)*120;im:drawImage(byId.Pickup_CoreShard.frames[coreAt(t)],Point(x+44,208));im:drawImage(rarityImages[32][kind.id],Point(x+44,208));text(im,kind.id:upper(),x+30,250,kind.colors[2],1) end
 return im
end
board(0):saveAs(outputDir..'/'..manifest.nativePreview)
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native Pickup Review'
for i=1,75 do if i>1 then review:newEmptyFrame(i) end;review:newCel(review.layers[1],i,board((i-1)*20),Point(0,0));review.frames[i].duration=0.02 end
review:saveAs(outputDir..'/'..manifest.animatedPreview);review:close()
saveJson(outputDir..'/manifest.json',manifest)
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('10 physical pickups / 4 reusable rarity treatments in 3 sizes / 13 sources / 25 frames.\n');done:close();print('WORLD_PICKUPS_GENERATED')
