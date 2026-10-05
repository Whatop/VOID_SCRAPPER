-- Discrete Raider effects: rough mechanical fire, damaged machinery, salvage flow.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,diamond,scale=h.put,h.rect,h.line,h.poly,h.oct,h.diamond,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local palettes={red={'662C32','B84445','ED7563','E9913C','FFE0A2','F0F2E9'},smoke={'30383E','596369','B84445','E9913C','F0F2E9'},heat={'662C32','B84445','ED7563','E9913C','FFE0A2'},salvage={'662C32','B84445','E9913C','FFE0A2','F0F2E9'},rail={'662C32','B84445','ED7563','B8EFFF','F0F2E9'}}
local P={};for k,colors in pairs(palettes) do P[k]={};for i,v in ipairs(colors) do P[k][i]=rgba(v) end end
local R=P.red;local white=R[6];local muted=rgba('ADB9B7')
local font={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,label,x,y,c,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if font[ch] then for yy,row in ipairs(font[ch]) do for xx=1,3 do if row:sub(xx,xx)=='1' then rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,c) end end end else h.text(im,ch,px,y,c,n) end
 end
end
for _,name in ipairs({'raider_assault_commander_weapons_hot','raider_salvage_carrier_salvage_active','raider_sniper_commander_target_lock'}) do local ref=assert(Image{fromFile=sourceDir..'/'..name..'.png'});assert(ref.width==128 and ref.height==128,'Boss reference size changed') end
local layerNames={'Primary Energy','White Hot Center','Sparks and Flow','Smoke and Debris'}
local function layers(w,height) local l={};for i=1,4 do l[i]=Image(w/2,height/2,ColorMode.RGB) end;return l end
local function path(im,pts,c,closed) for i=1,#pts-(closed==false and 1 or 0) do local a,b=pts[i],pts[i%#pts+1];line(im,a[1],a[2],b[1],b[2],c) end end
local function heavyMuzzle(f)
 local l=layers(64,64);if f==4 then return l end
 if f==1 then
  poly(l[1],{{4,14},{9,11},{12,6},{15,11},{22,8},{20,13},{28,16},{23,19},{25,24},{17,22},{14,27},{10,21},{4,18}},R[2])
  poly(l[1],{{4,14},{10,12},{15,10},{15,14},{24,16},{18,18},{19,22},{12,20},{8,19},{4,18}},R[4])
  poly(l[2],{{4,15},{10,13},{17,16},{13,18},{4,18}},R[5]);rect(l[2],4,15,6,3,white)
  line(l[3],21,5,23,4,R[4]);line(l[3],27,22,29,23,R[3])
 elseif f==2 then
  poly(l[1],{{4,15},{10,13},{12,10},{14,14},{24,17},{17,19},{18,23},{10,21},{4,18}},R[1])
  poly(l[1],{{4,15},{11,14},{19,17},{12,20},{7,18}},R[2]);line(l[2],4,16,15,17,R[4]);line(l[2],5,16,11,16,R[5])
  path(l[3],{{23,6},{26,5},{28,6}},R[4],false);line(l[3],24,24,27,26,R[3]);rect(l[3],28,16,2,1,R[4])
 else rect(l[1],5,16,5,1,R[1]);rect(l[3],27,5,3,1,R[4]);line(l[3],26,26,28,27,R[2]);rect(l[3],23,18,3,1,R[2]);put(l[3],16,23,R[3]) end
 return l
end
local function barrage(f)
 local l=layers(64,64)
 -- Chipped, skewed warning boundary. No closed precision ring or regular countdown grid.
 path(l[1],{{5,11},{4,7},{9,4},{13,5}},R[2],false)
 path(l[1],{{19,4},{25,5},{28,10},{27,13}},R[2],false)
 path(l[1],{{28,19},{28,24},{23,28},{20,27}},R[2],false)
 path(l[1],{{13,28},{7,27},{3,23},{4,18}},R[2],false)
 line(l[3],6,8,10,5,R[3]);line(l[3],26,21,25,25,R[3]);put(l[3],4,22,R[4])
 local r=({3,4,5,6,5,4})[f]
 line(l[1],16-r,16-r,16+r,16+r,R[2]);line(l[1],16+r,16-r-1,16-r,16+r,R[2])
 if f>=2 then line(l[3],7,13,9,12,R[3]);line(l[3],22,21,24,22,R[3]) end
 if f>=3 then line(l[3],13,4,15,3,R[4]);line(l[3],3,17,4,14,R[3]) end
 if f>=4 then rect(l[2],15,15,3,3,R[3]);line(l[3],20,28,23,29,R[4]) end
 if f>=5 then line(l[2],13,16,18,16,R[4]);line(l[2],16,13,16,18,R[4]) end
 if f==6 then diamond(l[2],16,16,2,R[4]);put(l[2],16,16,R[5]);rect(l[3],27,14,2,2,R[3]) end
 return l
end
local function damageSparks(f)
 local l=layers(32,32);if f==4 then return l end
 if f==1 then
  poly(l[1],{{5,5},{8,6},{11,4},{10,8},{13,10},{8,10},{6,13},{6,9},{3,7}},R[2])
  line(l[2],5,7,10,9,R[4]);line(l[2],7,5,8,10,R[5]);rect(l[2],7,7,2,2,white)
  line(l[3],10,5,12,3,R[4]);put(l[3],3,11,R[3])
 elseif f==2 then
  line(l[3],10,4,13,2,R[5]);line(l[3],11,10,14,11,R[4]);line(l[3],5,10,3,13,R[4]);line(l[3],4,6,2,5,R[3]);put(l[2],8,8,white)
 else rect(l[3],12,2,2,1,R[4]);put(l[3],14,12,R[2]);rect(l[3],2,13,2,1,R[3]);put(l[3],2,4,R[2]);put(l[3],8,10,R[1]) end
 return l
end
local function smokePuff(im,x,y,age)
 local c=P.smoke
 if age==0 then rect(im,x,y,2,2,c[2]);return end
 if age>=6 then rect(im,x+1,y,2,1,c[1]);put(im,x+4,y+2,c[2]);return end
 poly(im,{{x,y+1},{x+2,y},{x+3,y+1},{x+5,y+1},{x+6,y+3},{x+4,y+5},{x+2,y+4},{x,y+4}},c[1])
 rect(im,x+1,y+1,2,2,c[2]);put(im,x+4,y+3,c[2])
 if age>=4 then rect(im,x+2,y+3,2,2,0);put(im,x,y+4,0) end
end
local function critical(f)
 local l=layers(64,64);local c=P.smoke
 for k=0,1 do
  local age=(f-1+k*4)%8;local x=12+k*6+math.floor(age/3);local y=17-age*2
  smokePuff(l[4],x,y,age)
 end
 if f==1 or f==5 then
  local x=f==1 and 12 or 21;local y=f==1 and 21 or 18
  line(l[1],x-3,y-1,x+3,y+1,c[3]);line(l[2],x,y-2,x,y+1,c[4]);put(l[2],x,y,white)
  line(l[3],x+3,y-3,x+5,y-5,c[4]);line(l[3],x-4,y+1,x-6,y+3,c[3])
 elseif f==2 or f==6 then
  local x=f==2 and 12 or 21;local y=f==2 and 21 or 18
  line(l[3],x+6,y-6,x+7,y-7,c[4]);line(l[3],x-6,y+3,x-8,y+4,c[3]);put(l[3],x+3,y+3,c[4])
 end
 put(l[1],12,22,(f==1 or f==2) and c[4] or c[3]);put(l[1],21,19,(f==5 or f==6) and c[4] or c[3])
 return l
end
local function weaponsHot(f)
 local l=layers(32,32);local c=P.heat;local heat=({2,3,4,3,2,1})[f]
 -- Uneven barrel edges and staggered vent tongues, with an empty mount center.
 path(l[1],{{3,5},{5,4},{8,5},{10,4},{11,5}},c[2],false)
 path(l[1],{{3,11},{6,12},{8,11},{11,12},{12,11}},c[2],false)
 rect(l[1],3,6,1,4,c[1]);line(l[1],11,6,12,7,c[1]);line(l[1],12,9,11,10,c[1])
 line(l[2],5,4,5+heat,4,c[4]);line(l[2],7,12,8+heat,12,c[heat>=3 and 4 or 3])
 for i=0,2 do local x=({6,9,11})[i+1];local y=6+i%2
  rect(l[1],x,y,2,3,c[2]);line(l[2],x+1,y,x+1+math.floor(heat/2),y-1,c[heat>=3 and 5 or 4])
 end
 if f==2 or f==3 then put(l[3],13,3,c[4]);line(l[3],12,13,13,14,c[3]) elseif f==4 then put(l[3],14,2,c[3]);put(l[3],14,14,c[2]) end
 return l
end
local function salvage(f)
 local l=layers(96,32);local c=P.salvage;local shift=(f-1)*2
 -- A repeating 24-pixel flow period supports an unbroken local-X tile.
 for x=0,47 do
  local k=x%12
  if k<=7 then put(l[1],x,5+(k>=5 and 1 or 0),c[2]) end
  if k>=3 then put(l[1],x,11-(k>=10 and 1 or 0),c[1]) end
  if k<9 then put(l[1],x,8,c[2]) end
  if k>=2 and k<=6 then put(l[2],x,8,c[4]) end
 end
 local function wp(im,x,y,c) put(im,x%48,y,c) end
 for p=10,46,12 do
  local x=p-shift
  -- The pale cargo-sized center and orange '<' brackets all move four pixels left per frame.
  for i=0,2 do wp(l[3],x+2-i,6+i,c[3]);wp(l[3],x+2-i,10-i,c[3]) end
  wp(l[3],x+2,8,c[5]);wp(l[3],x+3,8,c[5]);wp(l[3],x+3,9,c[4])
 end
 return l
end
local function rail(tag,f)
 local l=layers(96,32);local c=P.rail
 if tag=='Shot' and f==3 then return l end
 if tag=='Lock' then
  for x=1,46 do if f==3 or x%6<(f==1 and 2 or 4) then put(l[1],x,8,c[2]) end end
  path(l[3],{{2,5},{4,4},{7,5}},c[2],false);path(l[3],{{2,11},{5,12},{8,11}},c[1],false)
  local x=({43,44,45})[f];line(l[3],x-2,5,x+1,10,c[3]);line(l[3],x+1,5,x-2,10,c[2])
  if f>=2 then line(l[2],4,8,10,8,c[3]) end
  if f==3 then put(l[2],44,8,c[3]);rect(l[3],12,5,2,1,c[2]) end
 elseif f==1 then
  poly(l[1],{{1,7},{7,5},{14,7},{41,7},{46,8},{39,9},{13,9},{7,11},{1,9}},c[4])
  line(l[2],1,8,44,8,white);rect(l[2],2,7,8,3,white)
  path(l[3],{{7,3},{10,4},{13,3}},c[3],false);line(l[3],9,12,15,13,c[2]);rect(l[3],24,5,4,1,c[2]);rect(l[3],32,11,5,1,c[3])
 else
  line(l[1],13,8,29,8,c[4]);line(l[2],32,8,46,8,white)
  rect(l[3],5,5,3,1,c[2]);rect(l[3],16,12,4,1,c[2]);rect(l[3],35,4,3,1,c[3]);put(l[3],40,11,c[1])
 end
 return l
end
local function clip(tag,ms,mode,fn,peak) return {tag=tag,durations=ms,mode=mode,make=fn,peak=peak or 1} end
local families={
 {id='VFX_Raider_HeavyMuzzle',label='RAIDER HEAVY MUZZLE',short='HEAVY MUZZLE',width=64,height=64,palette='red',pivot={x=8,y=32},clips={clip('Fire',{45,60,70,35},'one_shot',heavyMuzzle,1)},direction='+X'},
 {id='VFX_Raider_BarrageTelegraph',label='RAIDER BARRAGE TELEGRAPH',short='BARRAGE WARN',width=64,height=64,palette='red',pivot={x=32,y=32},clips={clip('Warning',{160,140,130,120,100,90},'handoff',barrage,6)},completion='Warning only. Hide at 740ms and hand off to the gameplay impact. Do not loop or hold the terminal marker.'},
 {id='VFX_Raider_DamageSparks',label='RAIDER DAMAGE SPARKS',short='DAMAGE SPARKS',width=32,height=32,palette='red',pivot={x=16,y=16},clips={clip('Burst',{35,55,70,40},'one_shot',damageSparks,1)}},
 {id='VFX_Raider_CriticalDamage',label='RAIDER CRITICAL DAMAGE',short='CRITICAL LOOP',width=64,height=64,palette='smoke',pivot={x=32,y=32},clips={clip('Loop',{100,100,120,160,100,100,160,160},'loop',critical,1)},completion='Eight-frame lightweight smoke loop. Sparks fire intermittently on frames 1-2 and 5-6. Remove when damage state ends.'},
 {id='VFX_Raider_Assault_WeaponsHot',label='ASSAULT WEAPONS HOT',short='WEAPONS HOT',width=32,height=32,palette='heat',pivot={x=12,y=16},clips={clip('Heat',{100,70,80,110,160,120},'loop',weaponsHot,3)},direction='+X vent/weapon orientation',completion='Place at weapon mounts. Loop while weapons are hot; heat overlay contains no gun/body pixels.'},
 {id='VFX_Raider_Salvage_Beam',label='SALVAGE TRACTOR BEAM',short='SALVAGE PULL',width=96,height=32,palette='salvage',pivot={x=0,y=16},clips={clip('Pull',{90,90,90,90,90,90},'loop',salvage,1)},tileAxis='X',tilePeriodPixels=24,flowPixelsPerFrame=-4,direction='Emitter at local X=0. Resources flow from +X toward -X.',completion='Loop Pull while collecting. Prefer repeating whole 24-pixel periods; keep height fixed if stretching length.'},
 {id='VFX_Raider_Sniper_Rail',label='SNIPER RAIL LOCK AND SHOT',short='RAIL',width=96,height=32,palette='rail',pivot={x=0,y=16},clips={clip('Lock',{220,180,140},'handoff',function(f) return rail('Lock',f) end,3),clip('Shot',{35,55,40},'one_shot',function(f) return rail('Shot',f) end,1)},direction='+X',completion='Six frames total: red Lock 540ms, then white-hot Shot 130ms. No blue aperture or SYSTEM precision ring.'}
}
local function saveJson(path,value) local f=assert(io.open(path,'w'));f:write(json.encode(value));f:close() end
local manifest={generatorId='void-scrapper-raider-boss-vfx-v1',name='RAIDER BOSS VFX PACK',target={width=480,height=270,pixelsPerUnit=32,constructionPixelScale=2,binaryAlpha=true},layers=layerNames,families={},totalFamilies=7,totalClips=8,totalFrames=40,
 preview='RaiderBossVFX_ContactSheet.png',nativePreview='RaiderBossVFX_480x270.png',animatedPreview='RaiderBossVFX_480x270.gif',sourcePolicy='Verified copied approved Raider references. No ship sprites are composited into effects.'}
local clips,byId={},{}
for _,family in ipairs(families) do
 local w,height=family.width,family.height;local maxN=0;for _,c in ipairs(family.clips) do maxN=math.max(maxN,#c.durations) end
 local s=Sprite(w,height,ColorMode.RGB);for i,name in ipairs(layerNames) do local l=i==1 and s.layers[1] or s:newLayer();l.name=name end
 local atlas=Image(w*maxN,height*#family.clips,ColorMode.RGB);local framesMeta,tags={},{};local index=0
 local record={id=family.id,label=family.label,width=w,height=height,aseprite=family.id..'.aseprite',sheet=family.id..'.png',metadata=family.id..'.json',atlasColumns=maxN,pixelsPerUnit=32,pivotPixels=family.pivot,unityPivot={x=family.pivot.x/w,y=1-family.pivot.y/height},palette=palettes[family.palette],clips={},direction=family.direction or 'Centered',completion=family.completion or 'Play once including transparent cleanup, then hide or recycle.',tileAxis=family.tileAxis,tilePeriodPixels=family.tilePeriodPixels,flowPixelsPerFrame=family.flowPixelsPerFrame}
 byId[family.id]={}
 for row,c in ipairs(family.clips) do
  local start=index+1;local strip=Image(w*#c.durations,height,ColorMode.RGB);local images={}
  for fi,ms in ipairs(c.durations) do
   index=index+1;if index>1 then s:newEmptyFrame(index) end
   local ls=c.make(fi);local flat=Image(w,height,ColorMode.RGB)
   for li,im in ipairs(ls) do local doubled=scale(im,2);s:newCel(s.layers[li],index,doubled,Point(0,0));flat:drawImage(doubled,Point(0,0)) end
   s.frames[index].duration=ms/1000;images[fi]=flat;strip:drawImage(flat,Point((fi-1)*w,0));atlas:drawImage(flat,Point((fi-1)*w,(row-1)*height))
   framesMeta[#framesMeta+1]={filename=family.id..'_'..c.tag..'_'..string.format('%02d',fi-1),frame={x=(fi-1)*w,y=(row-1)*height,w=w,h=height},rotated=false,trimmed=false,sourceSize={w=w,h=height},spriteSourceSize={x=0,y=0,w=w,h=height},duration=ms}
  end
  local name=family.id..(#family.clips>1 and '_'..c.tag or '')..'.png';if #family.clips>1 then strip:saveAs(outputDir..'/'..name) end
  record.clips[#record.clips+1]={tag=c.tag,fromFrame=start,toFrame=index,frames=#c.durations,durationsMs=c.durations,mode=c.mode,sheet=name,sheetRow=row-1}
  tags[#tags+1]={name=c.tag,from=start-1,to=index-1,direction='forward'}
  local r={family=family,clip=c,frames=images};clips[#clips+1]=r;byId[family.id][c.tag]=r
 end
 for _,t in ipairs(tags) do local tag=s:newTag(t.from+1,t.to+1);tag.name=t.name end
 s:saveAs(outputDir..'/'..record.aseprite);s:close();atlas:saveAs(outputDir..'/'..record.sheet)
 saveJson(outputDir..'/'..record.metadata,{frames=framesMeta,meta={app='Aseprite CLI + Lua',image=record.sheet,format='RGBA8888',size={w=atlas.width,h=atlas.height},frameTags=tags},unity={pixelsPerUnit=32,pivot=record.unityPivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',clips=record.clips}})
 manifest.families[#manifest.families+1]=record
end
local contact=Image(1320,748,ColorMode.RGB);contact:clear(Color{r=18,g=24,b=32,a=255})
text(contact,'RAIDER BOSS VFX PACK',20,16,white,3);text(contact,'7 FAMILIES / 8 CLIPS / 40 FRAMES',20,44,muted,2)
for i,r in ipairs(clips) do
 local x=14+(i-1)%2*658;local y=78+math.floor((i-1)/2)*166;local c=r.clip;local f=r.family
 text(contact,f.label..' / '..c.tag:upper(),x,y,R[5],1)
 text(contact,f.width..' X '..f.height..' / '..#r.frames..'F / '..c.mode:upper():gsub('_',' '),x,y+15,muted,1)
 local cell=f.width==96 and 96 or 64;local step=cell+10
 for fi,im in ipairs(r.frames) do
  local bx=x+(fi-1)*step;local by=y+32
  for yy=0,63 do for xx=0,cell-1 do put(contact,bx+xx,by+yy,(math.floor(xx/8)+math.floor(yy/8))%2==0 and rgba('222D38') or rgba('2A3540')) end end
  local shown=scale(im,f.width==32 and 2 or 1);contact:drawImage(shown,Point(bx+(cell-shown.width)//2,by+(64-shown.height)//2))
  text(contact,'F'..fi..' '..c.durations[fi]..'MS',bx+2,by+73,muted,1)
 end
 text(contact,f.width==32 and '2X REVIEW / NATIVE 32PX' or '1X REVIEW / NATIVE PIXELS',x,y+127,rgba('637078'),1)
end
contact:saveAs(outputDir..'/'..manifest.preview)
local function at(r,t)
 local total=0;for _,d in ipairs(r.clip.durations) do total=total+d end;if r.clip.mode=='loop' then t=t%total end
 local elapsed=0;for i,d in ipairs(r.clip.durations) do elapsed=elapsed+d;if t<elapsed then return r.frames[i] end end
end
local function board(t,peak)
 local im=Image(480,270,ColorMode.RGB);im:clear(Color{r=15,g=21,b=29,a=255});text(im,'RAIDER BOSS VFX / 480 X 270 / PPU 32',8,6,muted,1)
 for i,r in ipairs(clips) do
  local x=(i-1)%4*120;local y=24+math.floor((i-1)/4)*120;rect(im,x+1,y,118,116,rgba((i%2==0) and '1C2732' or '17212C'))
  local label=r.family.short..(r.family.short=='RAIL' and ' '..r.clip.tag:upper() or '');text(im,label,x+6,y+101,R[5],1)
  local shown
  if peak then shown=r.frames[r.clip.peak]
  else
   local time=t
   if r.family.id=='VFX_Raider_HeavyMuzzle' or r.family.id=='VFX_Raider_DamageSparks' then time=t%600 end
   if r.family.short=='RAIL' and r.clip.tag=='Shot' then time=t-540 end
   if time>=0 then shown=at(r,time) end
  end
  if shown then im:drawImage(shown,Point(x+(120-shown.width)//2,y+14+(64-shown.height)//2)) end
  if r.family.id=='VFX_Raider_Salvage_Beam' then text(im,'PULLS LEFT',x+6,y+87,muted,1) end
 end
 return im
end
board(0,true):saveAs(outputDir..'/'..manifest.nativePreview)
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native Pixel Review'
for i=1,60 do if i>1 then review:newEmptyFrame(i) end;review:newCel(review.layers[1],i,board((i-1)*20,false),Point(0,0));review.frames[i].duration=0.02 end
review:saveAs(outputDir..'/'..manifest.animatedPreview);review:close()
saveJson(outputDir..'/manifest.json',manifest)
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('7 Raider VFX families / 8 clips / 40 frames.\n');done:close();print('RAIDER_BOSS_VFX_GENERATED')
