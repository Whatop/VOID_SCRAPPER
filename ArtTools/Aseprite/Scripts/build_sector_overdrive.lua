-- Pixel-preserving SYSTEM overdrive. Read only inspected, hashed copies.
local refs=assert(app.params.sourceDir)
local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath))
-- Extend the review font locally; approved shared helper remains untouched.
local baseText=H.text
H.text=function(im,label,x,y,color,n)
 for i=1,#label do local ch=label:sub(i,i)
  if ch=='7' then local rows={'111','001','010','010','010'};for yy=1,5 do for xx=1,3 do if rows[yy]:sub(xx,xx)=='1' then H.rect(im,x+(i-1)*4*n+(xx-1)*n,y+(yy-1)*n,n,n,color) end end end
  else baseText(im,ch,x+(i-1)*4*n,y,color,n) end
 end
end
local pc=app.pixelColor
local function rgba(s) return pc.rgba(tonumber(s:sub(1,2),16),tonumber(s:sub(3,4),16),tonumber(s:sub(5,6),16),255) end
local function writeJson(name,t) local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(t));f:close() end
local energy={'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'}
local ramps={energy,
 {'284934','49955F','9DEAAD','D9F4DA','F1FFE7','FFFFFF'},
 {'513970','9172C2','D7B7F3','EAD6FF','F6E8FF','FFFFFF'},
 {'58367D','A167DA','D59AFF','E4BBFF','F5DEFF','FFFFFF'},
 {'452D66','8353B8','BA79FF','D9A5FF','F0D3FF','FFF6FF'}}
local energyIndex={};for i,c in ipairs(energy) do energyIndex[rgba(c)]=i end
local metal={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}
local function setEditingPalette(sprite,first,last)
 local colors,seen={},{}
 local function add(hex) if not seen[hex] then colors[#colors+1]=hex;seen[hex]=true end end
 for _,hex in ipairs(metal) do add(hex) end
 for fi=first,last do for _,hex in ipairs(ramps[fi]) do add(hex) end end
 local pal=Palette(#colors+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0})
 for i,hex in ipairs(colors) do local c=rgba(hex);pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end
 sprite:setPalette(pal)
end
local durations={50,50,50,60,90}
local s=assert(app.open(refs..'/approved_idle.aseprite'))
assert(s.width==128 and s.height==128 and #s.frames==1 and #s.layers==10)
local production=Image{fromFile=refs..'/production.png'}
local originalRender=Image(128,128,ColorMode.RGB);originalRender:drawSprite(s,1,Point(0,0))
for it in originalRender:pixels() do assert(it()==production:getPixel(it.x,it.y),'Copied ASE differs from production') end
local originalCels,layerNames={},{}
for i,l in ipairs(s.layers) do local c=assert(l:cel(1));originalCels[i]={image=Image(c.image),position=Point(c.position),opacity=c.opacity};layerNames[i]=l.name end
local oldTags={};for _,t in ipairs(s.tags) do oldTags[#oldTags+1]=t end;for _,t in ipairs(oldTags) do s:deleteTag(t) end
local frames={}
local function makeCel(base,name,fi)
 local im=Image(base.image)
 if fi>1 then
  for it in im:pixels() do local idx=energyIndex[it()]
   if idx then
    -- Existing routing carries the controlled power spill; no extra alpha or bloom.
    if name=='Energy Routing' and idx>=2 then idx=math.min(idx+1,4) end
    it(rgba(ramps[fi][idx]))
   end
  end
 end
 return im
end
for fi=1,5 do
 if fi>1 then s:newEmptyFrame(fi) end
 for li,l in ipairs(s.layers) do
  local base=originalCels[li];local im=makeCel(base,l.name,fi)
  if fi==1 then l:cel(fi).image=im else local c=s:newCel(l,fi,im,base.position);c.opacity=base.opacity end
 end
 s.frames[fi].duration=durations[fi]/1000
 local im=Image(128,128,ColorMode.RGB);im:drawSprite(s,fi,Point(0,0));frames[fi]=im
end
local tag=s:newTag(1,5);tag.name='Green to Purple Overdrive'
setEditingPalette(s,1,5)
s:saveAs(out..'/sector_administrator_overdrive_transition.aseprite')
local static=assert(app.open(refs..'/approved_idle.aseprite'))
local old={};for _,t in ipairs(static.tags) do old[#old+1]=t end;for _,t in ipairs(old) do static:deleteTag(t) end
for li,l in ipairs(static.layers) do l:cel(1).image=makeCel(originalCels[li],l.name,5) end
local ot=static:newTag(1,1);ot.name='Purple Overdrive';static.frames[1].duration=0.1
setEditingPalette(static,5,5)
static:saveAs(out..'/sector_administrator_overdrive.aseprite');static:close()
frames[5]:saveAs(out..'/sector_administrator_overdrive.png')
local strip=Image(640,128,ColorMode.RGB)
local cannonStrip=Image(150,32,ColorMode.RGB)
local jsonFrames={}
for fi,im in ipairs(frames) do
 strip:drawImage(im,Point((fi-1)*128,0))
 local crop=Image(30,32,ColorMode.RGB)
 for y=0,31 do for x=0,29 do crop:drawPixel(x,y,im:getPixel(x+49,y+16)) end end
 cannonStrip:drawImage(crop,Point((fi-1)*30,0))
 if fi==5 then crop:saveAs(out..'/sector_administrator_overdrive_center_cannon.png') end
 jsonFrames[#jsonFrames+1]={filename='sector_overdrive_'..fi,frame={x=(fi-1)*128,y=0,w=128,h=128},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=128,h=128},sourceSize={w=128,h=128},duration=durations[fi]}
end
strip:saveAs(out..'/sector_administrator_overdrive_transition.png')
cannonStrip:saveAs(out..'/sector_administrator_overdrive_center_cannon_transition.png')
writeJson('sector_administrator_overdrive_transition.json',{frames=jsonFrames,meta={app='Aseprite CLI + Lua',image='sector_administrator_overdrive_transition.png',format='RGBA8888',size={w=640,h=128},scale='1',frameTags={{name='Green to Purple Overdrive',from=0,to=4,direction='forward'}}},unity={pixelsPerUnit=87.671234,pivot={x=0.5,y=0.5},filterMode='Point',compression='None',mipmaps=false,loop=false,holdLastFrame=true},centerCannon={image='sector_administrator_overdrive_center_cannon_transition.png',frameWidth=30,frameHeight=32,cropTopLeft={x=49,y=16},cropUnity={x=49,y=80,w=30,h=32},pivot={x=0.5,y=0.5},pixelsPerUnit=87.671234,existingLocalScale=1.25}})
local bg=rgba('101820');local border=rgba('283641');local text=rgba('DCE5E3');local muted=rgba('83949D');local violet=rgba('D9A5FF')
local function canvas(w,h) local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local function board(fi)
 local im=canvas(480,270)
 H.text(im,'SECTOR ADMINISTRATOR',20,16,text,2)
 H.text(im,'PHASE 1',76,50,rgba('76D879'),1);H.text(im,'PURPLE OVERDRIVE',294,50,violet,1)
 im:drawImage(frames[1],Point(56,68));im:drawImage(frames[fi],Point(296,68))
 H.line(im,239,44,239,220,border)
 H.text(im,'SAME CHASSIS / POWERED PIXELS ONLY',46,226,muted,1)
 H.text(im,'NATIVE 128X128 ART ON 480X270',70,242,muted,1)
 return im
end
board(5):saveAs(out..'/sector_administrator_overdrive_480x270.png')
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native size review'
local sequence={{1,0.8},{2,0.05},{3,0.05},{4,0.06},{5,1.2}}
for fi,v in ipairs(sequence) do if fi>1 then review:newEmptyFrame(fi) end;review:newCel(review.layers[1],fi,board(v[1]),Point(0,0));review.frames[fi].duration=v[2] end
review:saveAs(out..'/sector_administrator_overdrive_review.gif');review:close()
local contact=canvas(800,648)
H.text(contact,'SECTOR ADMINISTRATOR / OVERDRIVE',28,20,text,3)
H.text(contact,'APPROVED GREEN',86,58,rgba('76D879'),2);H.text(contact,'PURPLE OVERDRIVE',458,58,violet,2)
contact:drawImage(H.scale(frames[1],3),Point(12,82));contact:drawImage(H.scale(frames[5],3),Point(404,82))
H.text(contact,'GREEN / SURGE / PALE VIOLET / PEAK / SETTLED',72,482,text,2)
for fi,im in ipairs(frames) do contact:drawImage(im,Point(40+(fi-1)*144,506)) end
contact:saveAs(out..'/sector_administrator_overdrive_comparison.png')
writeJson('manifest.json',{generatorId='void-scrapper-sector-overdrive-v1',name='Sector Administrator - Phase 2 / Purple Overdrive',canvas={w=128,h=128},front='up / negative Y',pixelGrid=2,source='approved production idle / exact render match',aseprite='sector_administrator_overdrive.aseprite',png='sector_administrator_overdrive.png',transitionAseprite='sector_administrator_overdrive_transition.aseprite',transitionSheet='sector_administrator_overdrive_transition.png',transitionMetadata='sector_administrator_overdrive_transition.json',transitionFrames=5,durationsMs=durations,transitionTotalMs=300,layers=layerNames,energyRamps=ramps,metalPalette={'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'},constraints={sameAlphaMask=true,nonEnergyPixelsUnchanged=true,originalLayerNamesAndPositions=true,noChassisAnimation=true,noExternalBloom=true},centerCannon={png='sector_administrator_overdrive_center_cannon.png',transitionSheet='sector_administrator_overdrive_center_cannon_transition.png',cropTopLeft={x=49,y=16,w=30,h=32}},comparison='sector_administrator_overdrive_comparison.png',nativePreview='sector_administrator_overdrive_480x270.png',animatedPreview='sector_administrator_overdrive_review.gif',integration='New art outputs only; existing production SpriteRenderer, cannon asset, prefab and imports unchanged.'})
s:close()
local marker=assert(io.open(out..'/generation_complete.txt','w'));marker:write('Aseprite generation complete: energy-only derivative of production idle.\n');marker:close()
