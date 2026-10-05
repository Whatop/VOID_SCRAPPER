-- Authored integer-pixel SYSTEM boss VFX. Every primitive is drawn in Aseprite Lua.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,diamond,scale=h.put,h.rect,h.line,h.poly,h.oct,h.diamond,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local hex={neutral={'515F67','ADB9B7','F0F2E9'},green={'254F39','4FA96C','B3EDAA'},orange={'6F3A26','E9913C','FFE0A2'},blue={'234871','529EDC','B8EFFF'}}
local P={};for k,values in pairs(hex) do P[k]={};for i,v in ipairs(values) do P[k][i]=rgba(v) end end
local white=P.neutral[3]
local font={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,label,x,y,c,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if font[ch] then for yy,row in ipairs(font[ch]) do for xx=1,3 do if row:sub(xx,xx)=='1' then rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,c) end end end else h.text(im,ch,px,y,c,n) end
 end
end
-- References are read only from verified copies, never opened as editable sprites.
for _,name in ipairs({'sector_administrator_charged','defense_overseer_barrage_charged','phase_gatekeeper_phase_lock'}) do
 local im=assert(Image{fromFile=sourceDir..'/'..name..'.png'});assert(im.width==128 and im.height==128,'Boss reference changed')
end
local layerNames={'Energy Shape','Bright Center','Technical Marks','Fragments'}
local function layers(w,height) local l={};for i=1,#layerNames do l[i]=Image(w/2,height/2,ColorMode.RGB) end;return l end
local function outline(im,pts,c,closed)
 for i=1,#pts-(closed==false and 1 or 0) do local a,b=pts[i],pts[i%#pts+1];line(im,a[1],a[2],b[1],b[2],c) end
end
local function ring(im,cx,cy,r,c)
 local k=math.max(1,math.floor(r*0.43));outline(im,{{cx-k,cy-r},{cx+k,cy-r},{cx+r,cy-k},{cx+r,cy+k},{cx+k,cy+r},{cx-k,cy+r},{cx-r,cy+k},{cx-r,cy-k}},c)
end
local function brackets(im,cx,cy,r,arm,c)
 for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do
  line(im,cx+sx*r,cy+sy*r,cx+sx*(r-arm),cy+sy*r,c)
  line(im,cx+sx*r,cy+sy*r,cx+sx*r,cy+sy*(r-arm),c)
 end end
end
local function sectorTelegraph(f)
 local l=layers(64,32);local c=P.green
 if f<=2 then
  for x=0,31 do if x%4<(f==1 and 1 or 2) then put(l[1],x,8,c[2]) end end
 else line(l[1],0,8,31,8,c[2]) end
 if f>=2 then for _,y in ipairs({5,11}) do line(l[3],0,y,3,y,c[1]);line(l[3],28,y,31,y,c[1]) end end
 if f>=3 then
  local offset=({0,0,2,3,4})[f]
  line(l[1],0,8-offset,31,8-offset,c[f==5 and 2 or 1]);line(l[1],0,8+offset,31,8+offset,c[f==5 and 2 or 1])
  for _,x in ipairs({6,22}) do outline(l[3],{{x,6},{x+2,8},{x,10}},c[2],false) end
 end
 if f==5 then for x=4,27,4 do put(l[2],x,8,c[3]) end end
 return l
end
local function beam(tag,f)
 local l=layers(64,32);local c=P.green
 if tag=='Release' and f==3 then return l end
 local radius
 if tag=='Fire' then radius=({1,4,3})[f] elseif tag=='Sustain' then radius=({3,4,3,2})[f] else radius=({2,0})[f] end
 if tag=='Release' and f==2 then line(l[1],0,8,31,8,c[1]);line(l[3],0,5,31,5,c[1]);line(l[3],0,11,31,11,c[1]);return l end
 rect(l[1],0,8-radius,32,radius*2+1,c[2])
 if radius>1 then rect(l[1],0,9-radius,32,radius*2-1,c[3]) end
 line(l[2],0,8,31,8,white)
 if radius>=3 then line(l[2],0,7,31,7,white);line(l[2],0,9,31,9,white) end
 local shift=({0,1,2,1})[f] or 0
 for _,x in ipairs({4,12,20,28}) do put(l[3],x+shift,3,c[tag=='Release' and 1 or 2]);put(l[3],x+shift,13,c[tag=='Release' and 1 or 2]) end
 return l
end
local function barrier(f)
 local l=layers(64,64);local c=P.green
 line(l[1],0,11,31,11,c[1]);line(l[1],0,21,31,21,c[1])
 line(l[1],0,14,31,14,c[2]);line(l[1],0,18,31,18,c[2]);line(l[2],0,16,31,16,c[3])
 for _,x in ipairs({6,22}) do
  outline(l[3],{{x-3,14},{x,11},{x+3,14}},c[2],false)
  outline(l[3],{{x-3,18},{x,21},{x+3,18}},c[2],false)
 end
 local phase=({0,1,2,3,2,1})[f]
 for _,x in ipairs({4,20}) do
  rect(l[2],x+phase,15,2,3,c[3]);put(l[3],x+phase,12,c[3]);put(l[3],x+phase,20,c[3])
 end
 return l
end
local function barrageTarget(f)
 local l=layers(64,64);local c=P.orange
 brackets(l[1],16,16,12,4,c[2]);brackets(l[3],16,16,9,2,c[1])
 outline(l[1],{{16,7},{25,16},{16,25},{7,16}},c[1])
 line(l[3],12,16,14,16,c[2]);line(l[3],18,16,20,16,c[2]);line(l[3],16,12,16,14,c[2]);line(l[3],16,18,16,20,c[2])
 for i=1,6 do rect(l[3],6+(i-1)*4,29,2,1,i<=f and c[3] or c[1]) end
 if f>=3 then brackets(l[2],16,16,12,1,c[3]) end
 if f>=4 then diamond(l[2],16,16,f-3,c[2]);put(l[2],16,16,c[3]) else put(l[2],16,16,c[2]) end
 if f==6 then brackets(l[2],16,16,6,2,c[3]) end
 return l
end
local function heavyImpact(f)
 local l=layers(64,64);local c=P.orange
 if f==7 then return l end
 if f==1 then diamond(l[1],16,16,8,c[2]);diamond(l[2],16,16,6,c[3]);diamond(l[2],16,16,4,white)
 elseif f==2 then
  poly(l[1],{{10,3},{21,3},{24,7},{28,11},{28,21},{24,24},{21,28},{10,28},{7,24},{3,21},{3,11},{7,8}},c[1])
  oct(l[1],5,5,23,23,6,c[2]);oct(l[2],8,8,17,17,4,c[3]);diamond(l[2],16,16,6,white)
  line(l[3],16,2,16,4,c[3]);line(l[3],2,16,4,16,c[3]);line(l[3],28,16,30,16,c[3]);line(l[3],16,28,16,30,c[3])
 elseif f==3 then
  ring(l[1],16,16,14,c[2]);ring(l[1],16,16,13,c[1]);diamond(l[1],16,16,10,c[2]);diamond(l[2],16,16,6,c[3]);diamond(l[2],16,16,3,white)
  rect(l[4],4,4,2,2,c[3]);rect(l[4],27,26,2,2,c[3]);rect(l[4],5,26,2,2,c[2]);rect(l[4],26,4,2,2,c[2])
 elseif f==4 then
  brackets(l[1],16,16,13,4,c[1]);ring(l[1],16,16,9,c[2]);ring(l[2],16,16,8,c[3]);diamond(l[1],16,16,3,c[1])
  line(l[4],3,7,5,8,c[2]);line(l[4],27,24,29,25,c[3]);line(l[4],7,27,6,29,c[2]);line(l[4],25,3,26,5,c[2])
 elseif f==5 then
  line(l[1],10,7,14,5,c[1]);line(l[1],24,12,25,16,c[1]);line(l[1],18,25,22,23,c[1]);line(l[1],7,19,8,22,c[1])
  rect(l[4],2,6,2,1,c[2]);rect(l[4],28,26,2,1,c[2]);rect(l[4],5,29,2,1,c[1]);rect(l[4],26,2,2,1,c[1])
 else put(l[4],2,5,c[1]);put(l[4],29,27,c[1]);put(l[4],5,29,c[1]);put(l[4],27,2,c[2]) end
 return l
end
local function battery(f)
 local l=layers(64,64);local c=P.orange;if f==4 then return l end
 if f==1 then
  poly(l[1],{{4,13},{10,10},{17,8},{16,12},{26,14},{29,16},{26,18},{16,20},{17,24},{10,22},{4,19}},c[2])
  poly(l[2],{{4,14},{12,12},{18,14},{26,16},{18,18},{12,20},{4,18}},c[3]);rect(l[2],4,15,12,3,white)
  brackets(l[3],9,16,6,2,c[1])
 elseif f==2 then
  poly(l[1],{{4,14},{11,11},{15,13},{23,16},{15,19},{11,21},{4,18}},c[1]);poly(l[2],{{4,15},{13,14},{22,16},{13,18},{4,17}},c[2]);line(l[2],5,16,17,16,c[3])
  line(l[4],19,8,24,7,c[2]);line(l[4],19,24,24,25,c[2]);rect(l[3],27,15,3,2,c[2])
 else line(l[1],5,16,11,16,c[1]);rect(l[4],24,7,3,1,c[1]);rect(l[4],24,25,3,1,c[1]);rect(l[4],28,15,2,2,c[2]) end
 return l
end
local function lens(im,rx,ry,c)
 outline(im,{{16,16-ry},{16+math.max(1,rx-2),16-ry+3},{16+rx,12},{16+rx,20},{16+math.max(1,rx-2),16+ry-3},{16,16+ry},{16-math.max(1,rx-2),16+ry-3},{16-rx,20},{16-rx,12},{16-math.max(1,rx-2),16-ry+3}},c)
end
local function portal(tag,f)
 local l=layers(64,64);local c=P.blue
 if tag=='Close' and f==6 then return l end
 if tag=='Open' and f<=2 then
  local r=f==1 and 4 or 9;line(l[1],16,16-r,16,16+r,c[2]);line(l[2],16,16-r+2,16,16+r-2,c[3])
  for _,y in ipairs({16-r,16+r}) do line(l[3],14,y,18,y,c[1]) end
  return l
 end
 if tag=='Close' and f>=4 then
  local r=f==4 and 8 or 3;line(l[1],16,16-r,16,16+r,c[f==5 and 1 or 2]);line(l[2],16,16-r+2,16,16+r-2,c[f==5 and 2 or 3]);return l
 end
 local rx,ry
 if tag=='Open' then rx=({0,0,3,6,9,11,10})[f];ry=({0,0,11,12,13,13,13})[f]
 elseif tag=='Close' then rx=({10,7,3})[f];ry=({13,12,11})[f]
 else rx=10;ry=13 end
 lens(l[1],rx,ry,c[2])
 -- A transparent central aperture and separate rails make this a spatial gate, not a filled orb.
 line(l[2],16-rx,13,16-rx,19,c[3]);line(l[2],16+rx,13,16+rx,19,c[3])
 if rx>=6 then
  line(l[3],16-rx-2,12,16-rx-2,20,c[1]);line(l[3],16+rx+2,12,16+rx+2,20,c[1])
  line(l[3],14,16-ry,18,16-ry,c[3]);line(l[3],14,16+ry,18,16+ry,c[3])
  local shift=tag=='Hold' and ({0,1,2,1})[f] or 0
  for _,sx in ipairs({-1,1}) do
   local x=16+sx*(rx-1);put(l[2],x,9+shift,c[3]);put(l[2],x,23-shift,c[3])
  end
 end
 return l
end
local lockRadii={13,11,9,7,5,3}
local function precisionLock(f)
 local l=layers(64,64);local c=P.blue;local r=lockRadii[f]
 brackets(l[1],16,16,r,math.min(3,r-1),c[2])
 for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do put(l[2],16+sx*r,16+sy*r,c[3]) end end
 if f<=4 then
  put(l[3],15,16,c[1]);put(l[3],17,16,c[1]);put(l[3],16,15,c[1]);put(l[3],16,17,c[1])
 else put(l[2],16,16,c[3]);line(l[3],16-r+1,16,14,16,c[1]);line(l[3],18,16,16+r-1,16,c[1]) end
 return l
end
local function redirect(f)
 local l=layers(32,32);local c=P.blue;if f==4 then return l end
 if f==1 then
  outline(l[1],{{3,5},{8,3},{12,6},{12,10},{8,13},{3,10}},c[2])
  poly(l[2],{{7,5},{10,8},{7,11},{7,9},{3,9},{3,7},{7,7}},c[3]);line(l[2],5,8,10,8,white)
 elseif f==2 then
  outline(l[1],{{2,4},{6,2},{10,3}},c[1],false);outline(l[1],{{7,13},{11,12},{14,9}},c[2],false)
  line(l[2],9,6,13,6,c[3]);line(l[2],10,9,14,9,c[2]);rect(l[4],3,11,2,1,c[2])
 else rect(l[4],1,4,3,1,c[1]);rect(l[4],11,10,3,1,c[2]);rect(l[4],8,13,2,1,c[1]) end
 return l
end
local families={}
local function clip(tag,ms,mode,make,peak) return {tag=tag,durations=ms,mode=mode,make=make,peak=peak or 1} end
local function add(id,label,short,w,height,pivot,color,clips,extra)
 local f={id=id,label=label,short=short,width=w,height=height,pivotPixels=pivot,color=color,clips=clips};if extra then for k,v in pairs(extra) do f[k]=v end end;families[#families+1]=f
end
add('VFX_Sector_LaserTelegraph','SECTOR LASER WARNING','LASER WARN',64,32,{x=0,y=16},'green',{
 clip('Warning',{180,160,140,120,100},'handoff',sectorTelegraph,5)}, {direction='+X',completion='Warning only. At 700ms hide this sprite and trigger laser Fire. This art does not define a damage collider.'})
add('VFX_Sector_LaserBeam','SECTOR LASER BEAM','LASER',64,32,{x=0,y=16},'green',{
 clip('Fire',{40,50,50},'transition',function(f) return beam('Fire',f) end,2),
 clip('Sustain',{60,60,60,60},'loop',function(f) return beam('Sustain',f) end,2),
 clip('Release',{50,60,40},'one_shot',function(f) return beam('Release',f) end,1)}, {tileAxis='X',direction='+X',completion='Fire once, Sustain for the attack duration, Release once. Tile along local X; keep transverse thickness fixed.'})
add('VFX_Sector_ContainmentBarrier','CONTAINMENT BARRIER','BARRIER',64,64,{x=0,y=32},'green',{
 clip('Loop',{100,100,100,100,100,100},'loop',barrier,3)}, {tileAxis='X',direction='Horizontal wall; rotate 90 degrees for vertical',completion='Repeat along local X. Remove on gameplay barrier release; no boss or emitter hardware is baked in.'})
add('VFX_Barrage_TargetTelegraph','BARRAGE TARGET','BARRAGE WARN',64,64,{x=32,y=32},'orange',{
 clip('Warning',{150,140,130,120,100,80},'handoff',barrageTarget,6)}, {countdownPips=6,completion='Six service-style countdown blocks fill left to right. At 720ms remove marker and trigger Heavy Barrage Impact.'})
add('VFX_Barrage_HeavyImpact','HEAVY BARRAGE IMPACT','HEAVY HIT',64,64,{x=32,y=32},'orange',{
 clip('Impact',{55,60,65,70,85,95,40},'one_shot',heavyImpact,2)})
add('VFX_Barrage_BatteryFlash','SUPPRESSION BATTERY FLASH','BATTERY',64,64,{x=8,y=32},'orange',{
 clip('Fire',{45,60,70,35},'one_shot',battery,1)}, {direction='+X'})
add('VFX_Phase_Portal','PHASE PORTAL','PORTAL',64,64,{x=32,y=32},'blue',{
 clip('Open',{45,50,55,60,65,70,75},'transition',function(f) return portal('Open',f) end,6),
 clip('Hold',{100,100,100,100},'loop',function(f) return portal('Hold',f) end,2),
 clip('Close',{60,60,55,50,45,40},'one_shot',function(f) return portal('Close',f) end,2)}, {completion='Play Open once (420ms), optionally loop Hold, then Close once (310ms). Open last equals Hold first; Close starts from the same aperture.'})
add('VFX_Phase_PrecisionLock','PRECISION LOCK','PRECISION',64,64,{x=32,y=32},'blue',{
 clip('Warning',{160,140,120,100,90,70},'handoff',precisionLock,5)}, {radiiPixels={26,22,18,14,10,6},completion='Blue corner reticle contracts for 680ms. Remove when the precision attack begins; no damaging beam is baked into the warning.'})
add('VFX_Phase_RedirectFlash','PHASE REDIRECT FLASH','REDIRECT',32,32,{x=16,y=16},'blue',{
 clip('Redirect',{35,50,60,35},'one_shot',redirect,1)}, {direction='+X outgoing path; rotate in integer-pixel increments where practical'})
local function saveJson(path,value) local f=assert(io.open(path,'w'));f:write(json.encode(value));f:close() end
local manifest={generatorId='void-scrapper-system-boss-vfx-v1',name='SYSTEM BOSS SIGNATURE VFX PACK',target={width=480,height=270,pixelsPerUnit=32,constructionPixelScale=2,binaryAlpha=true},layers=layerNames,families={},
 preview='SystemBossVFX_ContactSheet.png',nativePreview='SystemBossVFX_480x270_Preview.png',animatedPreview='SystemBossVFX_480x270_Preview.gif',
 pivotConvention='PNG top-left pixel coordinates; Unity normalized pivot (x/width,1-y/height). Untrimmed fixed cells.',
 totalFamilies=9,totalClips=13,totalFrames=65,referencePolicy='Only verified reference copies were read. No boss pixels are composited into VFX or review sheets.'}
local allClips={};local byId={}
for _,family in ipairs(families) do
 local w,height=family.width,family.height;local maxN=0;for _,c in ipairs(family.clips) do maxN=math.max(maxN,#c.durations) end
 local s=Sprite(w,height,ColorMode.RGB);for i,name in ipairs(layerNames) do local l=i==1 and s.layers[1] or s:newLayer();l.name=name end
 local atlas=Image(w*maxN,height*#family.clips,ColorMode.RGB);local framesMeta,tags={},{};local index=0
 local allowed={};for _,color in ipairs(hex[family.color]) do allowed[#allowed+1]=color end;allowed[#allowed+1]=hex.neutral[3]
 local rec={id=family.id,label=family.label,width=w,height=height,layers=layerNames,pixelsPerUnit=32,pivotPixels=family.pivotPixels,unityPivot={x=family.pivotPixels.x/w,y=1-family.pivotPixels.y/height},palette=allowed,
  aseprite=family.id..'.aseprite',sheet=family.id..'.png',metadata=family.id..'.json',atlasColumns=maxN,clips={},tileAxis=family.tileAxis,direction=family.direction or 'Centered',completion=family.completion or 'Play once, including transparent cleanup, then hide or recycle.',radiiPixels=family.radiiPixels}
 byId[family.id]={}
 for row,c in ipairs(family.clips) do
  local start=index+1;local strip=Image(w*#c.durations,height,ColorMode.RGB);local images={}
  for fi,ms in ipairs(c.durations) do
   index=index+1;if index>1 then s:newEmptyFrame(index) end
   local ls=c.make(fi);local flat=Image(w,height,ColorMode.RGB)
   for li,im in ipairs(ls) do local pixels=scale(im,2);s:newCel(s.layers[li],index,pixels,Point(0,0));flat:drawImage(pixels,Point(0,0)) end
   s.frames[index].duration=ms/1000;images[fi]=flat;strip:drawImage(flat,Point((fi-1)*w,0));atlas:drawImage(flat,Point((fi-1)*w,(row-1)*height))
   framesMeta[#framesMeta+1]={filename=family.id..'_'..c.tag..'_'..string.format('%02d',fi-1),frame={x=(fi-1)*w,y=(row-1)*height,w=w,h=height},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=w,h=height},sourceSize={w=w,h=height},duration=ms}
  end
  local name=family.id..(#family.clips>1 and '_'..c.tag or '')..'.png'
  if #family.clips>1 then strip:saveAs(outputDir..'/'..name) end
  local record={tag=c.tag,fromFrame=start,toFrame=index,sheet=name,sheetRow=row-1,frames=#c.durations,durationsMs=c.durations,mode=c.mode}
  rec.clips[#rec.clips+1]=record;tags[#tags+1]={name=c.tag,from=start-1,to=index-1,direction='forward'}
  local rendered={family=family,clip=c,frames=images,record=record};allClips[#allClips+1]=rendered;byId[family.id][c.tag]=rendered
 end
 for _,t in ipairs(tags) do local tag=s:newTag(t.from+1,t.to+1);tag.name=t.name end
 s:saveAs(outputDir..'/'..rec.aseprite);s:close();atlas:saveAs(outputDir..'/'..rec.sheet)
 saveJson(outputDir..'/'..rec.metadata,{frames=framesMeta,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=atlas.width,h=atlas.height},scale='1',frameTags=tags},unity={pixelsPerUnit=32,pivot=rec.unityPivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',clips=rec.clips}})
 manifest.families[#manifest.families+1]=rec
end
local contact=Image(1000,1134,ColorMode.RGB);contact:clear(Color{r=18,g=24,b=32,a=255})
text(contact,'SYSTEM BOSS SIGNATURE VFX',20,16,P.neutral[3],3);text(contact,'9 FAMILIES / 13 CLIPS / 65 FRAMES',20,42,P.neutral[2],2)
for i,r in ipairs(allClips) do
 local x=12+((i-1)%2)*498;local y=74+math.floor((i-1)/2)*150;local c=r.clip;local f=r.family
 text(contact,f.label..' / '..c.tag:upper(),x+4,y,P[f.color][3],1)
 text(contact,f.width..' X '..f.height..' / '..#r.frames..'F / '..c.mode:upper():gsub('_',' '),x+4,y+14,P.neutral[2],1)
 for fi,im in ipairs(r.frames) do
  local bx=x+4+(fi-1)*70;local by=y+30
  for yy=0,63 do for xx=0,63 do put(contact,bx+xx,by+yy,(math.floor(xx/8)+math.floor(yy/8))%2==0 and rgba('222D38') or rgba('2A3540')) end end
  local shown=scale(im,im.width==32 and 2 or 1);contact:drawImage(shown,Point(bx+(64-shown.width)//2,by+(64-shown.height)//2))
  text(contact,tostring(fi),bx+2,by+69,P.neutral[2],1);text(contact,tostring(c.durations[fi])..'MS',bx+14,by+69,P.neutral[2],1)
 end
 text(contact,f.width==32 and '2X REVIEW / NATIVE 32PX' or '1X REVIEW / NATIVE PIXELS',x+4,y+118,P.neutral[1],1)
end
contact:saveAs(outputDir..'/'..manifest.preview)
local function selectFrame(id,tag,t,hold)
 local r=byId[id][tag];local total=0;for _,d in ipairs(r.clip.durations) do total=total+d end
 if r.clip.mode=='loop' then t=t%total end
 local elapsed=0;for i,d in ipairs(r.clip.durations) do elapsed=elapsed+d;if t<elapsed then return r.frames[i] end end
 if hold then return r.frames[#r.frames] end
end
local function board(t,peak)
 local im=Image(480,270,ColorMode.RGB);im:clear(Color{r=15,g=21,b=29,a=255});text(im,'SYSTEM BOSS VFX / 480 X 270 / PPU 32',8,6,P.neutral[2],1)
 local labels={{'SECTOR LASER','CONTAINMENT','LASER DETAIL'},{'BARRAGE TARGET','HEAVY BARRAGE','BATTERY FLASH'},{'PHASE PORTAL','PRECISION LOCK','PHASE REDIRECT'}}
 local colors={P.green,P.orange,P.blue}
 for row=0,2 do for col=0,2 do local x=col*160;local y=22+row*82
  rect(im,x+1,y,158,80,(row+col)%2==0 and rgba('17212C') or rgba('1C2732'));text(im,labels[row+1][col+1],x+8,y+70,colors[row+1][3],1)
 end end
 local function draw(image,x,y) if image then im:drawImage(image,Point(x,y)) end end
 -- Review-only sequence demonstrates telegraph -> damage, with no boss sprites.
 local laser
 if peak then laser=byId.VFX_Sector_LaserBeam.Sustain.frames[2]
 elseif t<700 then laser=selectFrame('VFX_Sector_LaserTelegraph','Warning',t)
 elseif t<840 then laser=selectFrame('VFX_Sector_LaserBeam','Fire',t-700)
 elseif t<1240 then laser=selectFrame('VFX_Sector_LaserBeam','Sustain',t-840)
 else laser=selectFrame('VFX_Sector_LaserBeam','Release',t-1240) end
 draw(laser,16,42);draw(laser,80,42)
 local wall=peak and byId.VFX_Sector_ContainmentBarrier.Loop.frames[3] or selectFrame('VFX_Sector_ContainmentBarrier','Loop',t)
 draw(wall,176,26);draw(wall,240,26)
 draw(peak and byId.VFX_Sector_LaserTelegraph.Warning.frames[5] or selectFrame('VFX_Sector_LaserBeam','Sustain',t),368,42)
 draw(peak and byId.VFX_Barrage_TargetTelegraph.Warning.frames[6] or selectFrame('VFX_Barrage_TargetTelegraph','Warning',t),48,106)
 draw(peak and byId.VFX_Barrage_HeavyImpact.Impact.frames[2] or (t>=720 and selectFrame('VFX_Barrage_HeavyImpact','Impact',t-720)),208,106)
 draw(peak and byId.VFX_Barrage_BatteryFlash.Fire.frames[1] or selectFrame('VFX_Barrage_BatteryFlash','Fire',t%560),368,106)
 local gate
 if peak then gate=byId.VFX_Phase_Portal.Open.frames[6] elseif t<420 then gate=selectFrame('VFX_Phase_Portal','Open',t) elseif t<1080 then gate=selectFrame('VFX_Phase_Portal','Hold',t-420) else gate=selectFrame('VFX_Phase_Portal','Close',t-1080) end
 draw(gate,48,188)
 draw(peak and byId.VFX_Phase_PrecisionLock.Warning.frames[3] or selectFrame('VFX_Phase_PrecisionLock','Warning',t),208,188)
 draw(peak and byId.VFX_Phase_RedirectFlash.Redirect.frames[1] or (t>=680 and selectFrame('VFX_Phase_RedirectFlash','Redirect',t-680)),384,204)
 return im
end
board(0,true):saveAs(outputDir..'/'..manifest.nativePreview)
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native Pixel Review'
for i=1,80 do if i>1 then review:newEmptyFrame(i) end;review:newCel(review.layers[1],i,board((i-1)*20,false),Point(0,0));review.frames[i].duration=0.02 end
review:saveAs(outputDir..'/'..manifest.animatedPreview);review:close()
saveJson(outputDir..'/manifest.json',manifest)
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('9 SYSTEM VFX families / 13 clips / 65 frames. No boss sprites baked in.\n');done:close()
print('SYSTEM_BOSS_VFX_GENERATED')
