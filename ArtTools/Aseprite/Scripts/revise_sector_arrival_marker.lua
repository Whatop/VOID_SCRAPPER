-- Marker-only revision. All approved inputs are read from verified copies.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function write(name,t)local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(t));f:close()end
local function render(s,f)local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local id='VFX_Sector_BossArrivalMarker'
write('manifest.json',{generatorId='sector-arrival-marker-revision-2',status='generation in progress'})
local palette={'1F443B','4CB18F','7EF4CB','D9FFE5'}
local C,allowed={},{};for i,h in ipairs(palette)do C[i]=rgba(h);allowed[C[i]]=true end
local before=assert(app.open(src..'/marker_before.aseprite'))
assert(before.width==96 and before.height==96 and #before.frames==6,'Unexpected source')
local beforeSheet=Image{fromFile=src..'/marker_before.png'}
local beforeFrames,ms,layerNames={},{},{}
for f=1,6 do
 beforeFrames[f]=render(before,f);ms[f]=math.floor(before.frames[f].duration*1000+.5)
 for it in beforeFrames[f]:pixels()do assert(it()==beforeSheet:getPixel((f-1)*96+it.x,it.y),'Before ASE/PNG mismatch')end
end
for i,l in ipairs(before.layers)do layerNames[i]=l.name end
assert(#layerNames==4);before:close()
local function rotate(x,y,n)for i=1,n do x,y=63-y,x end;return x,y end
local function dot(im,x,y,r,c)local xx,yy=rotate(x,y,r);H.put(im,xx,yy,c)end
local function line(im,x1,y1,x2,y2,r,c)local a,b=rotate(x1,y1,r);local d,e=rotate(x2,y2,r);H.line(im,a,b,d,e,c)end
local function make(f)
 local ls={};for i=1,4 do ls[i]=Image(64,64,ColorMode.RGB)end
 for r=0,3 do
  -- 112px fixed diameter: registration bars and four secondary inward corner modules.
  local support=(f==1 or f==6) and C[1] or C[2]
  line(ls[1],23,4,29,4,r,support);line(ls[1],34,4,40,4,r,support)
  line(ls[1],23,4,23,7,r,support);line(ls[1],40,4,40,7,r,support)
  local corner=(f<3 or f==6) and C[1] or C[3]
  line(ls[1],47,7,50,7,r,support);line(ls[1],52,9,54,11,r,support);line(ls[1],56,13,56,16,r,support)
  if f~=6 then
   line(ls[3],46,12,46,17,r,corner);line(ls[3],46,17,51,17,r,corner)
   if f>=3 then line(ls[3],48,13,48,15,r,C[2]);line(ls[3],48,15,50,15,r,C[2])end
  else
   line(ls[4],46,16,46,17,r,C[2]);line(ls[4],46,17,47,17,r,C[2])
  end
  if f>=2 and f<=5 then
   -- Four broad inward brackets. Major strokes remain crisp integer pixel blocks.
   for dy=0,1 do
    line(ls[3],24,8+dy,30,14+dy,r,C[3])
    line(ls[3],30,14+dy,33,14+dy,r,C[3])
    line(ls[3],33,14+dy,39,8+dy,r,C[3])
   end
   line(ls[1],27,7,29,9,r,C[2]);line(ls[1],34,9,36,7,r,C[2])
  elseif f==6 then
   line(ls[4],28,12,30,14,r,C[2]);line(ls[4],30,14,33,14,r,C[2]);line(ls[4],33,14,35,12,r,C[2])
  end
  if f==5 then
   dot(ls[3],30,14,r,0);dot(ls[3],33,14,r,0);dot(ls[3],46,17,r,0)
   dot(ls[2],30,14,r,C[4]);dot(ls[2],33,14,r,C[4]);dot(ls[2],46,17,r,C[4])
   line(ls[1],24,4,28,4,r,C[3]);line(ls[1],35,4,39,4,r,C[3])
  end
 end
 if f==4 then
  H.rect(ls[2],31,30,2,4,C[3]);H.rect(ls[2],30,31,4,2,C[3]);H.rect(ls[2],31,31,2,2,C[4])
 end
 local result={};for i,im in ipairs(ls)do result[i]=H.scale(im,2)end;return result
end
local s=Sprite(128,128,ColorMode.RGB)
for i,name in ipairs(layerNames)do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=name end
local frames={};local sheet=Image(768,128,ColorMode.RGB);local metadata={}
for f=1,6 do
 if f>1 then s:newEmptyFrame(f)end
 for i,im in ipairs(make(f))do s:newCel(s.layers[i],f,im,Point(0,0))end
 s.frames[f].duration=ms[f]/1000;frames[f]=render(s,f);sheet:drawImage(frames[f],Point((f-1)*128,0))
 metadata[f]={filename=id..'_Arrival_'..f,frame={x=(f-1)*128,y=0,w=128,h=128},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=128,h=128},sourceSize={w=128,h=128},duration=ms[f]}
end
local tag=s:newTag(1,6);tag.name='Arrival'
local pal=Palette(5);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,c in ipairs(C)do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255})end;s:setPalette(pal)
s:saveAs(out..'/'..id..'.aseprite');s:close();sheet:saveAs(out..'/'..id..'.png')
write(id..'.json',{frames=metadata,meta={app='Aseprite CLI + Lua',image=id..'.png',format='RGBA8888',size={w=768,h=128},scale='1',frameTags={{name='Arrival',from=0,to=5,direction='forward'}}},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot={x=.5,y=.5},loop=false,clearAfterEnd=true,canvasChangedFrom=96,visibleDiameterBefore=84,visibleDiameterAfter=112}})
local bg,ink,muted=rgba('101820'),rgba('DCE5E3'),rgba('83949D')
local function canvas(w,h)local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local extra={['5']={'111','100','110','001','110'},['7']={'111','001','010','100','100'},['9']={'111','101','111','001','110'}}
local function text(im,t,x,y,c,n)
 for i=1,#t do local ch=t:sub(i,i);local xx=x+(i-1)*4*n
  if extra[ch]then for yy,row in ipairs(extra[ch])do for col=1,3 do if row:sub(col,col)=='1'then H.rect(im,xx+(col-1)*n,y+(yy-1)*n,n,n,c)end end end else H.text(im,ch,xx,y,c,n)end
 end
end
local function at(im,asset,x,y)im:drawImage(asset,Point(x-asset.width/2,y-asset.height/2))end
local function sample(t,durations)for f,d in ipairs(durations)do if t<d then return f end;t=t-d end;return nil end
local player=Image{fromFile=src..'/player.png'};local boss=Image{fromFile=src..'/boss_green.png'}
local materialSheet=Image{fromFile=src..'/materialization.png'};local material={}
for f=1,5 do local im=Image(96,96,ColorMode.RGB);im:drawImage(materialSheet,Point(-(f-1)*96,0));material[f]=im end
local function decorate(im,label)
 text(im,label,16,16,ink,2);text(im,'CENTER OPEN / SYSTEM DEPLOYMENT / NO DAMAGE',16,236,muted,1)
 text(im,'NATIVE 480X270 ART PREVIEW / NOT A UNITY CAPTURE',16,256,muted,1);return im
end
local function native(f)
 local im=canvas(480,270);at(im,frames[f],240,138);im:drawImage(player,Point(99,184));return decorate(im,'MAJOR SYSTEM UNIT / ARRIVAL MARKER')
end
native(3):saveAs(out..'/SectorArrivalMarker_480x270.png');native(5):saveAs(out..'/SectorArrivalMarker_Peak_480x270.png')
local function compare(f)
 local im=canvas(480,270);text(im,'BOSS ARRIVAL / BEFORE AND AFTER',16,16,ink,2)
 text(im,'BEFORE / 84PX',76,52,muted,2);text(im,'AFTER / 112PX',298,52,C[3],2)
 at(im,beforeFrames[f],128,140);at(im,frames[f],352,140)
 text(im,'SAME NATIVE SCALE / DIAMETER INCREASED BY ONE THIRD',16,236,muted,1)
 text(im,'NATIVE 480X270 ART PREVIEW / NOT A UNITY CAPTURE',16,256,muted,1);return im
end
compare(3):saveAs(out..'/SectorArrivalMarker_BeforeAfter_480x270.png')
local preview=canvas(1024,224);text(preview,'BOSS ARRIVAL MARKER / REVISION 2 / 128X128',16,16,ink,2)
text(preview,'FAINT / PRIMARY BRACKETS / CORNERS / CENTER / PEAK / HANDOFF',16,40,muted,1)
for f,im in ipairs(frames)do text(preview,tostring(f),20+(f-1)*168,65,muted,1);preview:drawImage(im,Point(20+(f-1)*168,82))end
preview:saveAs(out..'/'..id..'_Arrival_Preview.png')
local function gif(name,list,durations)
 local gs=Sprite(480,270,ColorMode.RGB);gs.layers[1].name='Marker review / references unchanged'
 for f,im in ipairs(list)do if f>1 then gs:newEmptyFrame(f)end;gs:newCel(gs.layers[1],f,im,Point(0,0));gs.frames[f].duration=durations[f]/1000 end
 gs:saveAs(out..'/'..name);gs:close()
end
local comparisonsPreview={};for f=1,6 do comparisonsPreview[f]=compare(f)end
gif('SectorArrivalMarker_BeforeAfter_480x270.gif',comparisonsPreview,ms)
local sequence,times={},{}
for tick=0,55 do
 local t=tick*20;local im=canvas(480,270);im:drawImage(player,Point(99,184))
 local f=sample(t,ms);if f then at(im,frames[f],240,138)end
 if t>=530 then at(im,boss,240,138)end
 if t>=440 then local f=sample(t-440,{50,40,60,70,50});if f then at(im,material[f],240,138)end end
 sequence[#sequence+1]=decorate(im,t<440 and 'SYSTEM DEPLOYMENT / ARRIVAL' or 'MATERIALIZATION / APPROVED REFERENCE');times[#times+1]=20
end
gif('SectorArrivalMarker_480x270.gif',sequence,times)
-- Validate the reopened source and actual exported strip, not only in-memory drawings.
local saved=assert(app.open(out..'/'..id..'.aseprite'));local exported=Image{fromFile=out..'/'..id..'.png'}
assert(saved.width==128 and saved.height==128 and #saved.frames==6 and #saved.layers==4)
assert(#saved.tags==1 and saved.tags[1].name=='Arrival' and saved.tags[1].fromFrame.frameNumber==1 and saved.tags[1].toFrame.frameNumber==6)
local function stats(im)
 local r={opaque=0,white=0,center=0,luminance=0,x=im.width,y=im.height,right=-1,bottom=-1}
 for it in im:pixels()do local c=it();if pc.rgbaA(c)>0 then
  r.opaque=r.opaque+1;r.luminance=r.luminance+.2126*pc.rgbaR(c)+.7152*pc.rgbaG(c)+.0722*pc.rgbaB(c)
  r.x=math.min(r.x,it.x);r.y=math.min(r.y,it.y);r.right=math.max(r.right,it.x);r.bottom=math.max(r.bottom,it.y)
  if c==C[4]then r.white=r.white+1 end
  if it.x>=im.width/2-24 and it.x<im.width/2+24 and it.y>=im.height/2-24 and it.y<im.height/2+24 then r.center=r.center+1 end
 end end
 r.meanLuminance=r.luminance/math.max(1,r.opaque);r.width=r.right-r.x+1;r.height=r.bottom-r.y+1;return r
end
local measurements={};local comparisons=0
for f=1,6 do
 assert(math.floor(saved.frames[f].duration*1000+.5)==ms[f],'Timing changed')
 local im=render(saved,f);local m=stats(im);measurements[f]=m
 assert(m.opaque>0 and m.opaque<128*128*.11,'Sparse marker occupancy invalid')
 assert(m.width==112 and m.height==112,'Visible diameter incorrect')
 assert(m.center==(f==4 and 48 or 0),'Center should only pulse briefly on frame 4')
 for it in im:pixels()do
  local c=it();local a=pc.rgbaA(c);assert(a==0 or a==255,'Non-binary alpha');if a>0 then assert(allowed[c],'Unexpected color')end
  assert(c==exported:getPixel((f-1)*128+it.x,it.y),'ASE/PNG mismatch')
  assert(c==im:getPixel(127-it.y,it.x),'Directional geometry lost fixed symmetry')
  assert(c==im:getPixel(it.x-it.x%2,it.y-it.y%2),'Pixel grid violation');comparisons=comparisons+1
 end
 if f>1 then local changed=0;for it in im:pixels()do if it()~=frames[f-1]:getPixel(it.x,it.y)then changed=changed+1 end end;assert(changed>0,'Duplicate frame')end
end
for i,l in ipairs(saved.layers)do assert(l.name==layerNames[i] and l.isVisible and l.opacity==255,'Layer structure changed')end;saved:close()
local oldStats=stats(beforeFrames[3]);assert(oldStats.width==84 and oldStats.height==84)
write('pixel_measurements.json',{frames=measurements,beforeActive=oldStats})
assert(measurements[3].meanLuminance>oldStats.meanLuminance*1.2,'Active geometry needs more contrast')
assert(measurements[5].luminance>measurements[4].luminance,'Frame 5 must peak')
assert(measurements[6].luminance<measurements[5].luminance*.35,'Frame 6 should fade')
assert(measurements[1].luminance<measurements[2].luminance*.2,'Frame 1 should be faint')
assert(measurements[5].white<=48,'White highlights too dominant')
for _,name in ipairs({'SectorArrivalMarker_480x270.png','SectorArrivalMarker_Peak_480x270.png','SectorArrivalMarker_BeforeAfter_480x270.png'})do local im=Image{fromFile=out..'/'..name};assert(im.width==480 and im.height==270)end
for _,entry in ipairs({{'SectorArrivalMarker_BeforeAfter_480x270.gif',6},{'SectorArrivalMarker_480x270.gif',56}})do local gs=assert(app.open(out..'/'..entry[1]));assert(gs.width==480 and gs.height==270 and #gs.frames==entry[2]);gs:close()end
write('manifest.json',{generatorId='sector-arrival-marker-revision-2',asset=id,revision=2,canvas={w=128,h=128},visibleDiameter={before=84,after=112,increasePercent=100/3},durationsMs=ms,palette=palette,layers=layerNames,tags={{name='Arrival',from=1,to=6}},onlyMarkerChanged=true,otherVfxModified=false,approvedBossModified=false,previewIsUnityCapture=false,frame6='Dim handoff tail; clear renderer at 480 ms',sequencePreview='Unchanged materialization/boss references; no gameplay edits'})
write('validation.json',{passed=true,pixelComparisons=comparisons,frames=measurements,beforeActive=oldStats,visibleDiameterIncreasePercent=100/3,activeLuminanceRatio=measurements[3].meanLuminance/oldStats.meanLuminance,transparentCenterExceptFrame4=true,frameDurationsUnchanged=true,binaryAlpha=true,fourColors=true,fixedFourfoldGeometry=true,otherVfxModified=false,previewIsUnityCapture=false})

