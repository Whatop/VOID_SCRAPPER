-- Extract the existing field unchanged; author only the frontal Raider Ram effect.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function write(name,t)local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(t));f:close()end
local function render(s,f)local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function paletteFor(s,hex)local p=Palette(#hex+1);p:setColor(0,Color{r=0,g=0,b=0,a=0});for i,h in ipairs(hex)do local c=rgba(h);p:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255})end;s:setPalette(p)end
write('manifest.json',{generatorId='raider-ram-shield-v1',status='generation in progress'})

-- Exact layer name is authoritative. Never select it by index.
local source=assert(app.open(src..'/normal_shield_source.aseprite'))
assert(source.width==128 and source.height==128 and #source.frames==1)
local found={};local function walk(list)for _,l in ipairs(list)do if l.name=='Shield Field'then found[#found+1]=l end;if l.isGroup then walk(l.layers)end end end
walk(source.layers);assert(#found==1 and not found[1].isGroup,'Expected exactly one image layer named Shield Field')
local fieldLayer=found[1];local cel=assert(fieldLayer:cel(1));assert(fieldLayer.opacity==255 and cel.opacity==255)
local field=Image(128,128,ColorMode.RGB);field:drawImage(cel.image,cel.position)
local fieldColors={};local fieldCount=0
for it in field:pixels()do local c=it();if pc.rgbaA(c)>0 then
 assert(pc.rgbaA(c)==255);local h=string.format('%02X%02X%02X',pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c))
 assert(h=='64CDD9'or h=='C2F2EF','Unexpected source-layer color');fieldColors[h]=(fieldColors[h]or 0)+1;fieldCount=fieldCount+1
end end
assert(fieldCount==664);source:close()
field:saveAs(out..'/Raider_ShieldField.png')
local extracted=Sprite(128,128,ColorMode.RGB);extracted.layers[1].name='Shield Field'
extracted:newCel(extracted.layers[1],1,field,Point(0,0));extracted.frames[1].duration=.2
paletteFor(extracted,{'64CDD9','C2F2EF'});extracted:saveAs(out..'/Raider_ShieldField.aseprite');extracted:close()
write('Raider_ShieldField.json',{source='Output/12_SalvageTradingStation/salvage_trading_station_shield_active.aseprite',selectedLayerName='Shield Field',sourceFrame=1,width=128,height=128,opaquePixels=fieldCount,colors=fieldColors,sourcePixelsUnchanged=true,pivot={x=.5,y=.5},pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect'})

-- Shield Hit is reference-only. Verify imported sheet against approved source.
local hs=assert(app.open(src..'/shield_hit.aseprite'));local hitSheet=Image{fromFile=src..'/shield_hit_imported.png'}
assert(hs.width==64 and hs.height==64 and #hs.frames==4 and hitSheet.width==256 and hitSheet.height==64)
local hit={};local hitCompared=0
for f=1,4 do hit[f]=render(hs,f);for it in hit[f]:pixels()do assert(it()==hitSheet:getPixel((f-1)*64+it.x,it.y),'Shield Hit imported pixels differ');hitCompared=hitCompared+1 end end;hs:close()

local id='VFX_Raider_RamShield'
local hex={'662C32','B84445','ED7563','E9913C','FFE0A2','F0F2E9'}
local C,allowed={},{};for i,h in ipairs(hex)do C[i]=rgba(h);allowed[C[i]]=true end
local names={'Frontal Segments','Concentrated Energy','Forward Hotspot','Leakage and Motion'}
local pieces={
 {poly={{2,17},{5,12},{9,9},{10,11},{7,14},{5,19},{3,20}},path={{4,17},{6,13},{9,11}}},
 {poly={{11,8},{16,5},{18,5},{18,8},{14,10},{12,11}},path={{12,9},{16,7},{17,7}}},
 {poly={{18,6},{20,3},{24,2},{29,4},{30,7},{27,9},{21,8}},path={{20,6},{24,4},{28,6}}},
 {poly={{31,5},{36,7},{40,11},{38,13},{35,10},{31,8}},path={{32,7},{35,8},{38,11}}},
 {poly={{41,13},{44,15},{46,19},{44,21},{42,18},{40,16}},path={{42,15},{44,18},{44,19}}}
}
local function poly(im,p,c,dx,dy)local q={};for _,v in ipairs(p)do q[#q+1]={v[1]+(dx or 0),v[2]+(dy or 0)}end;H.poly(im,q,c)end
local function path(im,p,c,dx,dy)for i=1,#p-1 do H.line(im,p[i][1]+(dx or 0),p[i][2]+(dy or 0),p[i+1][1]+(dx or 0),p[i+1][2]+(dy or 0),c)end end
local function make(f)
 local ls={};for i=1,4 do ls[i]=Image(48,24,ColorMode.RGB)end
 if f<=7 then
  for i,p in ipairs(pieces)do
   if f==1 then path(ls[1],p.path,C[1]);if i==3 then H.put(ls[2],24,4,C[2])end
   elseif f==2 then
    if i==3 then poly(ls[1],p.poly,C[1])end
    path(ls[2],p.path,i==3 and C[3]or C[2])
   else
    poly(ls[1],p.poly,C[1])
    if f==3 then path(ls[2],p.path,C[2]);if i==3 then path(ls[3],{{22,5},{24,4},{26,5}},C[4])end
    elseif f==4 then path(ls[2],p.path,C[3]);path(ls[2],p.path,C[2],0,1)
     if i==3 then path(ls[3],{{22,5},{24,4},{26,5}},C[4])end
    else
     path(ls[2],p.path,i==3 and C[5]or C[4]);path(ls[2],p.path,C[3],0,1)
     if i==3 then
      path(ls[3],{{21,5},{24,3},{28,5}},C[5]);H.rect(ls[3],23,3,2,2,C[6])
      H.put(ls[3],22,4,C[6]);H.put(ls[3],25,4,C[6])
      H.put(ls[3],({21,26,24})[f-4],({6,6,5})[f-4],C[6])
     end
    end
   end
  end
  if f>=5 then
   -- Short backward flow lives within the same fixed three-pixel tracks.
   local phase=f-5
   for t=0,2 do H.put(ls[4],14-t,13+t,(t==phase)and C[3]or C[1]);H.put(ls[4],34+t,12+t,(t==(phase+1)%3)and C[4]or C[1])end
  end
 elseif f<=10 then
  local k=f-7
  local offsets={{-1,1},{-1,1},{0,1},{1,1},{0,0}}
  for i,p in ipairs(pieces)do
   local o=offsets[i];local dx,dy=o[1],o[2]
   if i~=3 then
    if k==1 or(k==2 and(i==1 or i==4))or(k==3 and i==4)then poly(ls[1],p.poly,C[1],dx,dy)end
    path(ls[2],p.path,(k==1 or(k==2 and(i==1 or i==4))or(k==3 and i==1))and C[2]or C[1],dx,dy)
    if k==2 and i==1 then H.put(ls[2],5,15,C[3])end
   else
    -- Central white nose is gone; two separated red tabs keep this recoverable.
    H.line(ls[2],20,7+k,22,6+k,k==1 and C[2]or C[1])
    H.line(ls[2],27,6+k,29,8+k,C[1])
   end
  end
  local leaks={{{17,13},{30,11},{8,19}},{{16,15},{32,13},{38,18}},{{18,15},{29,14}}}
  for i,q in ipairs(leaks[k])do H.put(ls[4],q[1],q[2],i==1 and C[4]or C[2])end
 elseif f==11 then
  -- Thin realigned rim, no white or yellow startup flash.
  for i,p in ipairs(pieces)do path(ls[1],p.path,C[1]);if i==2 or i==4 then H.put(ls[2],p.path[2][1],p.path[2][2],C[2])end end
 elseif f==12 then
  for _,q in ipairs({{16,7},{17,7},{20,6},{21,5},{27,5},{28,6},{32,7},{33,7}})do H.put(ls[1],q[1],q[2],C[1])end
 end
 local result={};for i,im in ipairs(ls)do result[i]=H.scale(im,2)end;return result
end
local tags={
 {name='Charge',from=1,to=4,ms={120,100,90,90},loop=false,completion='Hold frame 4 or switch to Active on gameplay commit'},
 {name='Active',from=5,to=7,ms={70,70,70},loop=true,completion='Loop frames 5-7 or hold frame 6 during committed movement'},
 {name='Destabilize',from=8,to=10,ms={80,100,140},loop=false,completion='Hold frame 10 for the runtime punish window; no automatic recovery'},
 {name='Recover',from=11,to=13,ms={100,100,80},loop=false,completion='Ends transparent; show the unchanged normal Shield Field independently'}}
local ms={};for _,t in ipairs(tags)do for _,d in ipairs(t.ms)do ms[#ms+1]=d end end
local s=Sprite(96,48,ColorMode.RGB);for i,n in ipairs(names)do local l=i==1 and s.layers[1]or s:newLayer();l.name=n end
local frames={};local sheet=Image(1248,48,ColorMode.RGB);local metadata={}
for f=1,13 do
 if f>1 then s:newEmptyFrame(f)end
 for i,im in ipairs(make(f))do s:newCel(s.layers[i],f,im,Point(0,0))end
 s.frames[f].duration=ms[f]/1000;frames[f]=render(s,f);sheet:drawImage(frames[f],Point((f-1)*96,0))
 metadata[f]={filename=id..'_'..string.format('%02d',f),frame={x=(f-1)*96,y=0,w=96,h=48},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=96,h=48},sourceSize={w=96,h=48},duration=ms[f]}
end
local jt={}
for _,t in ipairs(tags)do
 local tag=s:newTag(t.from,t.to);tag.name=t.name
 local strip=Image((t.to-t.from+1)*96,48,ColorMode.RGB);for f=t.from,t.to do strip:drawImage(frames[f],Point((f-t.from)*96,0))end
 strip:saveAs(out..'/'..id..'_'..t.name..'.png')
 jt[#jt+1]={name=t.name,from=t.from-1,to=t.to-1,direction='forward'}
end
paletteFor(s,hex);s:saveAs(out..'/'..id..'.aseprite');s:close();sheet:saveAs(out..'/'..id..'.png')
write(id..'.json',{frames=metadata,meta={app='Aseprite CLI + Lua',image=id..'.png',format='RGBA8888',size={w=1248,h=48},scale='1',frameTags=jt},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot={x=.5,y=0},pivotPixelsTopLeft={x=48,y=48},forward='local +Y in Unity / up in image',clips=tags,playWholeSheetAutomatically=false}})
-- PREVIEWS
local bg,ink,muted,panel=rgba('101820'),rgba('DCE5E3'),rgba('83949D'),rgba('18232D')
local function canvas(w,h)local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local extra={['5']={'111','100','110','001','110'},['7']={'111','001','010','100','100'},['9']={'111','101','111','001','110'}}
local function text(im,t,x,y,c,n)
 for i=1,#t do local ch=t:sub(i,i);local xx=x+(i-1)*4*n
  if extra[ch]then for yy,row in ipairs(extra[ch])do for col=1,3 do if row:sub(col,col)=='1'then H.rect(im,xx+(col-1)*n,y+(yy-1)*n,n,n,c)end end end else H.text(im,ch,xx,y,c,n)end
 end
end
local boss=Image{fromFile=src..'/boss_idle.png'};local player=Image{fromFile=src..'/player.png'}
local function at(im,asset,x,y)im:drawImage(asset,Point(x-asset.width/2,y-asset.height/2))end
local function ram(im,f,x,y)im:drawImage(frames[f],Point(x-48,y-48))end
local function quarter(im)local q=Image(im.height,im.width,ColorMode.RGB);for it in im:pixels()do q:drawPixel(im.height-1-it.y,it.x,it())end;return q end
local contact=canvas(896,680);text(contact,'RAIDER SHIELD RAM / 13 FRAMES / FOUR RUNTIME STAGES',16,18,ink,2)
text(contact,'FORWARD UP / REAR CENTER PIVOT / STAGE TIMING IS NOT GAMEPLAY TIMING',16,40,muted,1)
for row,t in ipairs(tags)do
 local y=62+(row-1)*150;H.rect(contact,12,y,872,142,panel);text(contact,t.name:upper(),24,y+10,C[3],2)
 local preview=canvas(896,168);text(preview,'RAIDER SHIELD RAM / '..t.name:upper(),16,12,ink,2)
 text(preview,t.loop and 'LOOP OR HOLD / FIXED GEOMETRY' or 'INDEPENDENTLY TRIGGERED STAGE',16,34,muted,1)
 for f=t.from,t.to do local x=24+(f-t.from)*216
  text(contact,tostring(f),x,y+33,muted,1);contact:drawImage(H.scale(frames[f],2),Point(x,y+42))
  text(preview,tostring(f),x,52,muted,1);preview:drawImage(H.scale(frames[f],2),Point(x,64))
 end
 preview:saveAs(out..'/'..id..'_'..t.name..'_Preview.png')
end;contact:saveAs(out..'/RaiderRamShield_ContactSheet.png')
local compare=canvas(1248,344);text(compare,'RAIDER SHIELD / EXISTING FIELD AND HIT / NEW FRONTAL RAM',16,18,ink,3)
local labels={'NORMAL FIELD','EXISTING HIT','RAM CHARGE','RAM ACTIVE','DESTABILIZE','RECOVER'}
for col=1,6 do
 local x=104+(col-1)*208;H.rect(compare,(col-1)*208+6,60,196,252,panel);text(compare,labels[col],x-88,72,ink,2)
 if col<=2 or col==6 then at(compare,field,x,214)end;at(compare,boss,x,214)
 if col==2 then compare:drawImage(hit[2],Point(x+10,182))end
 if col>=3 then ram(compare,({[3]=4,[4]=6,[5]=9,[6]=12})[col],x,150)end
 text(compare,col<=2 and 'UNCHANGED REFERENCE' or col==6 and 'RETURN TO NORMAL' or col==5 and 'PUNISH WINDOW' or 'FRONTAL ATTACHMENT',x-88,290,muted,1)
end
text(compare,'BOSS AND NORMAL FIELD AT NATIVE SIZE / NO SOURCE RESAMPLING / ART REVIEW ONLY',16,326,muted,1)
compare:saveAs(out..'/RaiderRamShield_Comparison.png')
local function sample(t,ds)for f,d in ipairs(ds)do if t<d then return f end;t=t-d end;return #ds end
local function native(f,normal,cy,title)
 local im=canvas(480,270);if normal then at(im,field,240,cy)end;at(im,boss,240,cy)
 if f then ram(im,f,240,cy-64)end
 im:drawImage(player,Point(104,84))
 -- Sparse nearby projectile reference marks are outside the field and boss.
 H.rect(im,316,72,2,6,C[5]);H.rect(im,338,106,2,6,C[3]);H.rect(im,152,126,2,6,ink)
 text(im,title,16,16,ink,2);text(im,'FORWARD UP / CHASSIS CLEAR / INDEPENDENT STAGE CONTROL',16,246,muted,1)
 text(im,'NATIVE 480X270 ART PREVIEW / NOT A UNITY CAPTURE',16,260,muted,1);return im
end
native(6,false,174,'RAM ACTIVE / COMMITTED DIRECTION'):saveAs(out..'/RaiderRamShield_480x270.png')
native(9,false,174,'DESTABILIZED / PUNISH WINDOW'):saveAs(out..'/RaiderRamShield_Punish_480x270.png')
native(nil,true,174,'EXISTING NORMAL SHIELD / UNCHANGED'):saveAs(out..'/RaiderRamShield_Normal_480x270.png')
local gs=Sprite(480,270,ColorMode.RGB);gs.layers[1].name='Native reference-only state presentation'
for n=0,119 do local t=n*20;local f,normal,cy,title=nil,false,174,'EXISTING NORMAL SHIELD'
 if t<400 then normal=true
 elseif t<800 then f=sample(t-400,tags[1].ms);normal=true;title='CHARGE / FRONT CONCENTRATES'
 elseif t<1220 then f=4+sample((t-800)%210,tags[2].ms);cy=174-2*math.floor((t-800)/24);title='RAM / COMMITTED STRAIGHT DIRECTION'
 elseif t<1540 then f=7+sample(t-1220,tags[3].ms);cy=140;title='MISS / PUNISH WINDOW'
 elseif t<1840 then f=10;cy=140;title='STAGGER / HOLD PUNISH STATE'
 elseif t<2120 then f=10+sample(t-1840,tags[4].ms);cy=140;normal=t>=1940;title='RECOVER / NORMAL FIELD RETURNS'
 else cy=140;normal=true end
 if n>0 then gs:newEmptyFrame(n+1)end;gs:newCel(gs.layers[1],n+1,native(f,normal,cy,title),Point(0,0));gs.frames[n+1].duration=.02
end
gs:saveAs(out..'/RaiderRamShield_480x270.gif');gs:close()
-- Quarter-turn review at exact pixel coordinates, with rear pivot cross shown only in review.
local directions=canvas(480,270);text(directions,'FRONTAL RAM / FOUR ROTATION CHECKS',16,16,ink,2)
local rotated=frames[6];local pivotX,pivotY=48,48
local placements={{96,144},{214,132},{358,104},{322,220}};local directionNames={'UP','RIGHT','DOWN','LEFT'}
for r=1,4 do local pos=placements[r];directions:drawImage(rotated,Point(pos[1]-pivotX,pos[2]-pivotY))
 H.line(directions,pos[1]-2,pos[2],pos[1]+2,pos[2],muted);H.line(directions,pos[1],pos[2]-2,pos[1],pos[2]+2,muted)
 text(directions,directionNames[r],pos[1]+8,pos[2]+8,ink,1)
 pivotX,pivotY=rotated.height-pivotY,pivotX;rotated=quarter(rotated)
end
text(directions,'CROSS IS ATTACHMENT PIVOT / EFFECT ALWAYS EXTENDS FORWARD',16,254,muted,1)
directions:saveAs(out..'/RaiderRamShield_Direction_480x270.png')
-- VALIDATION
local report={passed=true,normalSelectedByExactName=true,normalOpaquePixels=fieldCount,normalColors=fieldColors,normalPixelComparisons=0,ramPixelComparisons=0,shieldHitReferencePixelComparisons=hitCompared,frames={},tags=tags,previewIsUnityCapture=false}
local normalPng=Image{fromFile=out..'/Raider_ShieldField.png'};local ns=assert(app.open(out..'/Raider_ShieldField.aseprite'));assert(#ns.layers==1 and ns.layers[1].name=='Shield Field');local nim=render(ns,1)
for it in field:pixels()do assert(it()==normalPng:getPixel(it.x,it.y)and it()==nim:getPixel(it.x,it.y),'Extraction changed source pixels');report.normalPixelComparisons=report.normalPixelComparisons+2 end;ns:close()
local saved=assert(app.open(out..'/'..id..'.aseprite'));local exported=Image{fromFile=out..'/'..id..'.png'}
assert(saved.width==96 and saved.height==48 and #saved.frames==13 and #saved.tags==4 and #saved.layers==4)
local function measure(im)
 local m={opaque=0,white=0,hot=0,luminance=0,frontHot=0,colors=0,x=96,y=48,right=-1,bottom=-1};local seen={}
 for it in im:pixels()do local c=it();if pc.rgbaA(c)>0 then
  m.opaque=m.opaque+1;seen[c]=true;m.x=math.min(m.x,it.x);m.y=math.min(m.y,it.y);m.right=math.max(m.right,it.x);m.bottom=math.max(m.bottom,it.y)
  m.luminance=m.luminance+.2126*pc.rgbaR(c)+.7152*pc.rgbaG(c)+.0722*pc.rgbaB(c)
  if c==C[6]then m.white=m.white+1 end;if c==C[5]or c==C[6]then m.hot=m.hot+1;if it.x>=38 and it.x<=58 and it.y<18 then m.frontHot=m.frontHot+1 end end
 end end;for _ in pairs(seen)do m.colors=m.colors+1 end;return m
end
for f=1,13 do
 local im=render(saved,f);local m=measure(im);report.frames[f]=m
 assert(math.floor(saved.frames[f].duration*1000+.5)==ms[f]);assert(m.colors<=6)
 if f==13 then assert(m.opaque==0,'Recover must clean up completely')else assert(m.opaque>0 and m.opaque<96*48*.3);assert(m.x>=2 and m.y>=2 and m.right<94 and m.bottom<46,'Canvas clipping')end
 for it in im:pixels()do local c=it();local a=pc.rgbaA(c);assert(a==0 or a==255);if a>0 then assert(allowed[c],'Foreign color')end
  assert(c==exported:getPixel((f-1)*96+it.x,it.y),'Saved sheet differs');assert(c==im:getPixel(it.x-it.x%2,it.y-it.y%2),'Non-integer construction grid')
  report.ramPixelComparisons=report.ramPixelComparisons+1
 end
 if f>1 then local different=0;for it in im:pixels()do if it()~=frames[f-1]:getPixel(it.x,it.y)then different=different+1 end end;assert(different>0,'Duplicate frame')end
end
for i,t in ipairs(tags)do local a=saved.tags[i];assert(a.name==t.name and a.fromFrame.frameNumber==t.from and a.toFrame.frameNumber==t.to)
 local strip=Image{fromFile=out..'/'..id..'_'..t.name..'.png'}
 for f=t.from,t.to do for it in frames[f]:pixels()do assert(it()==strip:getPixel((f-t.from)*96+it.x,it.y),'Stage strip mismatch')end end
end
for i,l in ipairs(saved.layers)do assert(l.name==names[i]and l.isVisible and l.opacity==255)end;saved:close()
local M=report.frames
assert(M[1].luminance<M[4].luminance*.35,'Charge starts too bright')
for f=2,4 do assert(M[f].luminance>M[f-1].luminance,'Charge must build')end
for f=5,7 do assert(M[f].white>=20 and M[f].frontHot==M[f].hot,'Active hotspot must be at the forward center')
 for it in frames[f]:pixels()do assert((pc.rgbaA(it())>0)==(pc.rgbaA(frames[5]:getPixel(it.x,it.y))>0),'Active silhouette jitters')end
end
for f=1,4 do assert(M[f].white==0)end
for f=8,12 do assert(M[f].hot==0 and M[f].luminance<M[6].luminance*.6,'Punish/recovery too attack-like')end
assert(M[8].luminance>M[9].luminance and M[9].luminance>M[10].luminance)
assert(M[11].luminance>M[12].luminance and M[11].luminance<M[4].luminance*.5)
local function diff(a,b)local n=0;for it in a:pixels()do if it()~=b:getPixel(it.x,it.y)then n=n+1 end end;return n end
report.activeSeamDifferences={diff(frames[5],frames[6]),diff(frames[6],frames[7]),diff(frames[7],frames[5])}
assert(report.activeSeamDifferences[3]<=math.max(report.activeSeamDifferences[1],report.activeSeamDifferences[2]),'Loop seam spike')
local spin=frames[6];for i=1,4 do spin=quarter(spin)end;assert(diff(spin,frames[6])==0)
report.activeVsDestabilizedChangedPixels=diff(frames[6],frames[9]);assert(report.activeVsDestabilizedChangedPixels>400)
-- Native active composition cannot cover any original opaque boss pixel.
local nativeIm=Image{fromFile=out..'/RaiderRamShield_480x270.png'};report.nativeBossPixelsPreserved=0
for it in boss:pixels()do if pc.rgbaA(it())>0 then assert(it()==nativeIm:getPixel(176+it.x,110+it.y),'Ram preview hides boss pixels');report.nativeBossPixelsPreserved=report.nativeBossPixelsPreserved+1 end end
for _,name in ipairs({'RaiderRamShield_480x270.png','RaiderRamShield_Punish_480x270.png','RaiderRamShield_Normal_480x270.png','RaiderRamShield_Direction_480x270.png'})do local im=Image{fromFile=out..'/'..name};assert(im.width==480 and im.height==270)end
local gif=assert(app.open(out..'/RaiderRamShield_480x270.gif'));assert(#gif.frames==120 and gif.width==480 and gif.height==270);gif:close()
write('manifest.json',{generatorId='raider-ram-shield-v1',name='Raider Normal Shield Extraction and Shield Ram',normalSourceLayer='Shield Field',normalPixelsUnchanged=true,ram={id=id,width=96,height=48,frames=13,layers=names,palette=hex,tags=tags,pivotPixelsTopLeft={x=48,y=48},unityPivot={x=.5,y=0},forward='up / Unity local +Y'},referenceOnly={'Shield Hit','Raider Commander'},constraints={noBossPixelsInVfx=true,noNewNormalShieldArt=true,noUnityGameplayEdits=true},preview='RaiderRamShield_Comparison.png',contact='RaiderRamShield_ContactSheet.png',nativePreview='RaiderRamShield_480x270.png',animatedPreview='RaiderRamShield_480x270.gif'})
write('validation.json',report)

