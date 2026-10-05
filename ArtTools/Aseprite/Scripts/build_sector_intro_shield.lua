-- Separate SYSTEM intro/shield layers; approved boss pixels are reference-only.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgba(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function write(name,t)local f=assert(io.open(out..'/'..name,'w'));f:write(json.encode(t));f:close()end
local function read(path)local f=assert(io.open(path,'r'));local t=json.decode(f:read('*a'));f:close();return t end
local function render(s,f)local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local extra={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,s,x,y,c,n)
 for i=1,#s do local ch=s:sub(i,i);local xx=x+(i-1)*4*n
  if extra[ch] then for yy,row in ipairs(extra[ch]) do for col=1,3 do if row:sub(col,col)=='1' then H.rect(im,xx+(col-1)*n,y+(yy-1)*n,n,n,c)end end end else H.text(im,ch,xx,y,c,n)end
 end
end
local palettes={green={'254F39','4FA96C','B3EDAA','F0F2E9'},purple={'452D66','BA79FF','D9A5FF','FFF6FF'}}
local P={};for k,a in pairs(palettes)do P[k]={};for i,h in ipairs(a)do P[k][i]=rgba(h)end end
local names={'Field Geometry','Hot Nodes','Routing Marks','Residual Energy'}
local function layers(w,h)local r={};for i=1,4 do r[i]=Image(w,h,ColorMode.RGB)end;return r end
local function scaleLayers(ls)local r={};for i,im in ipairs(ls)do r[i]=H.scale(im,2)end;return r end
local bosses={Image{fromFile=src..'/boss_green.png'},Image{fromFile=src..'/boss_purple.png'}}
local production=Image{fromFile=src..'/production_boss.png'}
for it in bosses[1]:pixels()do assert(it()==production:getPixel(it.x,it.y),'Approved/production boss mismatch')end
local transition=assert(app.open(src..'/boss_transition.aseprite'));local transitionPNG=Image{fromFile=src..'/boss_transition.png'}
local transitionFrames,transitionMs={},{}
assert(transition.width==128 and transition.height==128 and #transition.frames==5)
for f=1,5 do local im=render(transition,f);transitionFrames[f]=im;transitionMs[f]=math.floor(transition.frames[f].duration*1000+.5)
 for it in im:pixels()do assert(it()==transitionPNG:getPixel((f-1)*128+it.x,it.y),'Approved transition ASE/PNG mismatch')end
end;transition:close()
local function points(x0,y0,x1,y1)
 local r={};local dx,dy=math.abs(x1-x0),-math.abs(y1-y0);local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1;local err=dx+dy
 while true do r[#r+1]={x=x0,y=y0};if x0==x1 and y0==y1 then break end;local e=2*err;if e>=dy then err=err+dy;x0=x0+sx end;if e<=dx then err=err+dx;y0=y0+sy end end
 return r
end
local function outline(vertices)
 local r={};for s,a in ipairs(vertices)do local b=vertices[s%#vertices+1];local line=points(a[1],a[2],b[1],b[2]);for j,p in ipairs(line)do r[#r+1]={x=p.x,y=p.y,segment=s,j=j-1,n=#line,k=math.min(j-1,#line-j)}end end;return r
end
local markerPath=outline({{12,3},{35,3},{44,12},{44,35},{35,44},{12,44},{3,35},{3,12}})
local shieldPath={}
for _,p in ipairs(outline({{10,1},{53,1},{62,10},{62,53},{53,62},{10,62},{1,53},{1,10}}))do
 local overlap=false;for dy=0,1 do for dx=0,1 do if pc.rgbaA(bosses[1]:getPixel(p.x*2+dx,p.y*2+dy))>0 then overlap=true end end end
 if not overlap and p.k%12<9 then shieldPath[#shieldPath+1]=p end
end
local function marker(f)
 local ls=layers(48,48);local c=P.green
 if f==6 then return scaleLayers(ls)end
 for _,p in ipairs(markerPath)do
  local on=f==1 and p.segment%2==0 and p.k<3 or f==2 and (p.segment%2==0 or p.k%10<5) or f>=3 and p.k%12<9
  if on then H.put(ls[1],p.x,p.y,f==1 and c[1] or f==5 and c[1] or c[2])end
  if f>=3 and p.segment%2==0 and p.k==2 then H.put(ls[3],p.x,p.y,c[3])end
 end
 local r=f<=2 and 14 or f==3 and 12 or 10
 for _,d in ipairs({{0,-1},{1,0},{0,1},{-1,0}})do
  local x,y=24+d[1]*r,24+d[2]*r;local tx,ty=-d[2],d[1]
  H.line(ls[3],x+tx*2+d[1]*2,y+ty*2+d[2]*2,x,y,c[f<4 and 2 or 3])
  H.line(ls[3],x-tx*2+d[1]*2,y-ty*2+d[2]*2,x,y,c[f<4 and 2 or 3])
 end
 if f==4 then H.rect(ls[2],23,23,2,2,c[3])elseif f==5 then H.rect(ls[2],23,23,2,2,c[4])end
 return scaleLayers(ls)
end
local function materialize(f)
 local ls=layers(48,48);local c=P.green
 if f==1 then H.diamond(ls[1],23.5,23.5,3.5,c[2]);H.rect(ls[2],23,23,2,2,c[4])
 elseif f==2 then
  H.diamond(ls[1],23.5,23.5,7.5,c[2]);H.diamond(ls[1],23.5,23.5,4.5,c[3]);H.rect(ls[2],22,22,4,4,c[4])
  for _,d in ipairs({{0,-1},{1,0},{0,1},{-1,0}})do local x,y=24+d[1]*10,24+d[2]*10;H.line(ls[3],x-d[1]*2,y-d[2]*2,x+d[1]*2,y+d[2]*2,c[3]);H.put(ls[2],x,y,c[4])end
 elseif f==3 then
  for _,d in ipairs({{0,-1},{1,0},{0,1},{-1,0},{1,1},{1,-1},{-1,1},{-1,-1}})do
   local r=(d[1]~=0 and d[2]~=0)and 10 or 16;local x,y=24+d[1]*r,24+d[2]*r
   H.line(ls[3],x-d[1],y-d[2],x+d[1],y+d[2],c[2]);H.put(ls[4],x,y,c[3])
  end
 elseif f==4 then
  for _,q in ipairs({{24,4},{24,43},{4,24},{43,24},{9,9},{38,9},{9,38},{38,38}})do H.put(ls[4],q[1],q[2],c[2])end
 end
 return scaleLayers(ls)
end
local function shield(f,family)
 local ls=layers(64,64);local c=P[family];local shift=({0,2,4,2})[f]
 for _,p in ipairs(shieldPath)do
  H.put(ls[1],p.x,p.y,c[2])
  if p.k%8==shift then H.put(ls[3],p.x,p.y,c[3])end
  if (p.segment==1 or p.segment==5)and p.k==4+shift then H.put(ls[2],p.x,p.y,c[4])end
 end
 return scaleLayers(ls)
end
local function hit(f,family)
 local ls=layers(16,16);local c=P[family]
 if f==1 then H.rect(ls[1],7,7,2,2,c[3]);H.put(ls[2],7,7,c[4])
 elseif f==2 then
  H.line(ls[1],3,9,5,8,c[2]);H.line(ls[1],5,8,10,8,c[3]);H.line(ls[1],10,8,12,9,c[2]);H.rect(ls[2],7,8,2,1,c[4]);H.put(ls[3],7,5,c[3])
 elseif f==3 then H.line(ls[4],2,9,4,8,c[2]);H.line(ls[4],11,8,13,9,c[2]);H.put(ls[4],5,6,c[3]);H.put(ls[4],10,6,c[3])end
 return scaleLayers(ls)
end
local function release(f,family)
 local ls=layers(64,64);local c=P[family]
 if f==6 then return scaleLayers(ls)end
 for _,p in ipairs(shieldPath)do
  if f==1 then H.put(ls[1],p.x,p.y,c[3]);if p.segment%2==0 and p.k==3 then H.put(ls[2],p.x,p.y,c[4])end
  elseif f==2 and p.k%8<4 then H.put(ls[1],p.x,p.y,c[2]);if p.k%8==0 then H.put(ls[3],p.x,p.y,c[3])end
  elseif f==3 and p.k%12<2 then H.put(ls[3],p.x,p.y,c[3])
  elseif f==4 and p.k==2 then H.put(ls[4],p.x,p.y,c[2])
  elseif f==5 and p.segment%2==0 and p.k==0 then H.put(ls[4],p.x,p.y,c[1])end
 end
 return scaleLayers(ls)
end
local function phasePulse(f)
 local ls=layers(56,56);if f==5 then return scaleLayers(ls)end
 local r=({12,17,22,25})[f];local a,b=28-r,27+r;local bevel=math.floor(r*.45)
 local c=f==1 and P.green or P.purple
 for _,p in ipairs(outline({{a+bevel,a},{b-bevel,a},{b,a+bevel},{b,b-bevel},{b-bevel,b},{a+bevel,b},{a,b-bevel},{a,a+bevel}}))do
  if p.k%9<6 then H.put(ls[1],p.x,p.y,f==1 and c[3] or f==2 and c[3] or f==3 and c[2] or c[1])end
  if f<=2 and p.segment%2==0 and p.k==1 then H.put(ls[2],p.x,p.y,c[4])end
 end
 return scaleLayers(ls)
end
local function dual(make,ms)return {{name='Green',palette='green',ms=ms,make=function(f)return make(f,'green')end},{name='Purple',palette='purple',ms=ms,make=function(f)return make(f,'purple')end}}end
local specs={
 {id='VFX_Sector_BossArrivalMarker',label='BOSS ARRIVAL MARKER',w=96,h=96,mode='one_shot',clips={{name='Arrival',palette='green',ms={100,100,100,80,60,40},make=marker}}},
 {id='VFX_Sector_BossMaterialization',label='BOSS MATERIALIZATION',w=96,h=96,mode='one_shot',clips={{name='Materialize',palette='green',ms={50,40,60,70,50},make=materialize}}},
 {id='VFX_Sector_BossShield',label='BOSS SHIELD',w=128,h=128,mode='loop',clips=dual(shield,{120,120,120,120})},
 {id='VFX_Sector_BossShieldHit',label='LOCAL SHIELD HIT',w=32,h=32,mode='one_shot',clips=dual(hit,{30,40,50,30})},
 {id='VFX_Sector_BossShieldRelease',label='SHIELD BREAK / RELEASE',w=128,h=128,mode='one_shot',clips=dual(release,{40,50,60,60,60,40})},
 {id='VFX_Sector_PhaseTransitionRing',label='PHASE TRANSITION PULSE',w=112,h=112,mode='one_shot',clips={{name='Overdrive',palette='green_purple',ms={50,50,50,60,90},make=phasePulse}}}
}
local manifest={generatorId='void-scrapper-sector-intro-shield-v1',name='Sector Administrator Boss Intro / Shield VFX',pixelsPerUnit=32,constructionPixelScale=2,palettes=palettes,layers=names,families={},preview='SectorIntroShield_ContactSheet.png',nativePreview='SectorIntroShield_480x270.png',animatedPreview='SectorIntroShield_480x270.gif',arrivalPreview='SectorIntroShield_Arrival_480x270.gif',overdrivePreview='SectorIntroShield_Overdrive_480x270.gif'}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local bg,ink,muted,panel=rgba('101820'),rgba('DCE5E3'),rgba('83949D'),rgba('18232D')
local function canvas(w,h)local im=Image(w,h,ColorMode.RGB);H.rect(im,0,0,w,h,bg);return im end
local cache,rows={},{ }
local effectLayers=names
for _,v in ipairs(specs) do
 v.pivot={v.w/2,v.h/2};v.maxColors=4
 local s=Sprite(v.w,v.h,ColorMode.RGB);local names=v.layerNames or effectLayers
 for i,n in ipairs(names) do local layer=i==1 and s.layers[1] or s:newLayer();layer.name=n end
 local cols=0;for _,c in ipairs(v.clips) do cols=math.max(cols,#c.ms) end
 local sheet=Image(cols*v.w,#v.clips*v.h,ColorMode.RGB);local metadata,clips,tags={},{},{};local n=0
 for row,clip in ipairs(v.clips) do
  local from=n+1;local frames={};local strip=Image(#clip.ms*v.w,v.h,ColorMode.RGB)
  for f,ms in ipairs(clip.ms) do
   n=n+1;if n>1 then s:newEmptyFrame(n) end
   for i,im in ipairs(clip.make(f)) do s:newCel(s.layers[i],n,im,Point(0,0)) end
   s.frames[n].duration=ms/1000;local im=render(s,n);frames[f]=im
   sheet:drawImage(im,Point((f-1)*v.w,(row-1)*v.h));strip:drawImage(im,Point((f-1)*v.w,0))
   metadata[#metadata+1]={filename=v.id..'_'..clip.name..'_'..f,frame={x=(f-1)*v.w,y=(row-1)*v.h,w=v.w,h=v.h},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=v.w,h=v.h},sourceSize={w=v.w,h=v.h},duration=ms}
  end
  local filename=#v.clips>1 and v.id..'_'..clip.name..'.png' or v.id..'.png';if #v.clips>1 then strip:saveAs(out..'/'..filename) end
  tags[#tags+1]={name=clip.name,from=from,to=n}
  local rec={name=clip.name,from=from,to=n,durationsMs=clip.ms,palette=clip.palette,sheet=filename,preview=v.id..'_'..clip.name..'_Preview.png'};clips[#clips+1]=rec
  local preview=canvas(1024,224);text(preview,v.label..' / '..clip.name:upper(),16,16,ink,2);text(preview,'NATIVE CELLS / TRANSPARENT GAMEPLAY EXPORTS',16,38,muted,1)
  for f,im in ipairs(frames) do text(preview,tostring(f),30+(f-1)*164,63,muted,1);preview:drawImage(H.scale(im,v.w<=64 and 2 or 1),Point(20+(f-1)*164,88)) end
  preview:saveAs(out..'/'..rec.preview);cache[v.id..'/'..clip.name]=frames;rows[#rows+1]={v=v,clip=clip,frames=frames}
 end
 for _,t in ipairs(tags) do local tag=s:newTag(t.from,t.to);tag.name=t.name end
 local colors,seen={},{};for f=1,#s.frames do for it in render(s,f):pixels() do local c=it();if pc.rgbaA(c)>0 and not seen[c] then colors[#colors+1]=c;seen[c]=true end end end
 local pal=Palette(#colors+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,c in ipairs(colors) do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end;s:setPalette(pal)
 s:saveAs(out..'/'..v.id..'.aseprite');s:close();sheet:saveAs(out..'/'..v.id..'.png')
 local pivot={x=v.pivot[1]/v.w,y=1-v.pivot[2]/v.h};local jt={};for _,t in ipairs(tags) do jt[#jt+1]={name=t.name,from=t.from-1,to=t.to-1,direction='forward'} end
 local rec={id=v.id,width=v.w,height=v.h,frames=n,mode=v.mode,layers=names,maxColors=v.maxColors,pivotPixels={x=v.pivot[1],y=v.pivot[2]},unityPivot=pivot,tags=tags,clips=clips,aseprite=v.id..'.aseprite',sheet=v.id..'.png',metadata=v.id..'.json'}
 manifest.families[#manifest.families+1]=rec
 write(rec.metadata,{frames=metadata,meta={app='Aseprite CLI + Lua',image=rec.sheet,format='RGBA8888',size={w=sheet.width,h=sheet.height},scale='1',frameTags=jt},unity={pixelsPerUnit=32,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',pivot=pivot,clips=clips,loop=v.mode=='loop'}})
end

-- PREVIEWS
local contact=canvas(1280,1664);text(contact,'SECTOR ADMINISTRATOR / INTRO AND SHIELD VFX',20,20,ink,3);text(contact,'ANTICIPATION / APPEARANCE / PROTECTION / LOCAL RESPONSE / RELEASE',20,48,muted,1)
for i,row in ipairs(rows)do
 local y=76+(i-1)*174;H.rect(contact,12,y,1256,162,panel)
 text(contact,row.v.label,24,y+12,ink,2);text(contact,row.clip.name:upper()..' / '..row.v.w..'X'..row.v.h,24,y+36,muted,1)
 for f,im in ipairs(row.frames)do local x=280+(f-1)*164;text(contact,tostring(f),x+28,y+10,muted,1);contact:drawImage(H.scale(im,row.v.w<=64 and 2 or 1),Point(x,y+30))end
end;contact:saveAs(out..'/'..manifest.preview)
local player=Image{fromFile=src..'/player.png'}
local function sample(ms,durations,loop)
 local total=0;for _,d in ipairs(durations)do total=total+d end;if loop then ms=ms%total end
 for f,d in ipairs(durations)do if ms<d then return f end;ms=ms-d end;return #durations
end
local function at(im,sprite,x,y)im:drawImage(sprite,Point(math.floor(x-sprite.width/2),math.floor(y-sprite.height/2)))end
local function quarter(im)
 local r=Image(im.height,im.width,ColorMode.RGB);for it in im:pixels()do r:drawPixel(im.height-1-it.y,it.x,it())end;return r
end
local function loopShield(im,family,ms,x,y)
 local frames=cache['VFX_Sector_BossShield/'..family];at(im,frames[sample(ms,{120,120,120,120},true)],x,y)
end
local function localHit(im,family,ms,x,y,vertical)
 local frames=cache['VFX_Sector_BossShieldHit/'..family];local effect=frames[sample(ms,{30,40,50,30},false)]
 at(im,vertical and quarter(effect)or effect,x,y)
end
local function decorate(im,title,subtitle)
 text(im,title,16,14,ink,2);text(im,subtitle,16,239,muted,1);text(im,'NATIVE 480X270 ART REVIEW / NOT A UNITY CAPTURE',16,258,muted,1);return im
end
local function intro(ms)
 local im=canvas(480,270);im:drawImage(player,Point(99,184))
 if ms<480 then at(im,cache['VFX_Sector_BossArrivalMarker/Arrival'][sample(ms,{100,100,100,80,60,40},false)],240,138)end
 if ms>=470 then at(im,bosses[1],240,138)end
 if ms>=380 and ms<650 then at(im,cache['VFX_Sector_BossMaterialization/Materialize'][sample(ms-380,{50,40,60,70,50},false)],240,138)end
 if ms>=600 then loopShield(im,'Green',ms-600,240,138)end
 if ms>=900 and ms<1050 then localHit(im,'Green',ms-900,240,76,false)end
 if ms>=1180 and ms<1330 then localHit(im,'Green',ms-1180,300,138,true)end
 return decorate(im,ms<380 and 'SYSTEM DEPLOYMENT / ARRIVAL MARKER' or ms<600 and 'MATERIALIZATION / BOSS APPEARS' or 'GREEN SHIELD / LOCAL HIT RESPONSE',ms<470 and 'CENTER CLEAR / BOSS NOT YET VISIBLE' or 'BOSS SILHOUETTE AND WEAPON ORIGINS REMAIN CLEAR')
end
local function overdrive(ms)
 local im=canvas(480,270);im:drawImage(player,Point(99,184));local b=bosses[1]
 if ms>=400 and ms<700 then b=transitionFrames[sample(ms-400,transitionMs,false)]elseif ms>=700 then b=bosses[2]end
 at(im,b,240,138);loopShield(im,ms<500 and 'Green' or 'Purple',ms,240,138)
 if ms>=400 and ms<700 then at(im,cache['VFX_Sector_PhaseTransitionRing/Overdrive'][sample(ms-400,{50,50,50,60,90},false)],240,138)end
 if ms>=1000 and ms<1150 then localHit(im,'Purple',ms-1000,300,138,true)end
 return decorate(im,ms<400 and 'GREEN SYSTEM / PROTECTED STATE' or ms<700 and 'ENERGY TRANSITION / SAME SHIELD' or 'PURPLE OVERDRIVE / SAME CHASSIS','APPROVED BOSS TRANSITION SHOWN AS REFERENCE ONLY')
end
local function ending(ms)
 local im=canvas(480,270);im:drawImage(player,Point(99,184));at(im,bosses[2],240,138)
 if ms<320 then loopShield(im,'Purple',ms,240,138)
 elseif ms<630 then at(im,cache['VFX_Sector_BossShieldRelease/Purple'][sample(ms-320,{40,50,60,60,60,40},false)],240,138)end
 return decorate(im,ms<320 and 'PURPLE SHIELD / STABLE FIELD' or 'SHIELD RELEASE / BOSS REMAINS','SEGMENTS RETRACT / NO GLASS OR PHYSICAL DEBRIS')
end
local native=canvas(480,270);text(native,'GREEN SHIELD',72,18,P.green[3],2);text(native,'PURPLE OVERDRIVE',294,18,P.purple[3],2)
at(native,bosses[1],124,138);loopShield(native,'Green',0,124,138);at(native,bosses[2],356,138);loopShield(native,'Purple',0,356,138)
text(native,'SAME GEOMETRY / CLEAR CENTER / SEPARATE VFX LAYER',24,236,muted,1);text(native,'NATIVE 480X270 ART REVIEW / NOT A UNITY CAPTURE',16,258,muted,1)
native:saveAs(out..'/'..manifest.nativePreview)
intro(280):saveAs(out..'/SectorIntroShield_ArrivalMarker_480x270.png')
intro(440):saveAs(out..'/SectorIntroShield_Materialize_480x270.png')
intro(840):saveAs(out..'/SectorIntroShield_GreenShield_480x270.png')
intro(950):saveAs(out..'/SectorIntroShield_LocalHit_480x270.png')
overdrive(540):saveAs(out..'/SectorIntroShield_Transition_480x270.png')
ending(420):saveAs(out..'/SectorIntroShield_Release_480x270.png')
local function saveSequence(name,count,make)
 local s=Sprite(480,270,ColorMode.RGB);s.layers[1].name='Reference-only presentation sequence'
 for f=1,count do if f>1 then s:newEmptyFrame(f)end;s:newCel(s.layers[1],f,make((f-1)*40),Point(0,0));s.frames[f].duration=.04 end
 s:saveAs(out..'/'..name);s:close()
end
saveSequence(manifest.arrivalPreview,40,intro);saveSequence(manifest.overdrivePreview,35,overdrive)
saveSequence(manifest.animatedPreview,100,function(ms)if ms<1600 then return intro(ms)elseif ms<3000 then return overdrive(ms-1600)else return ending(ms-3000)end end)
manifest.totalFrames=44;manifest.totalTags=9
manifest.handoff={markerMs=480,materializationMs=270,materializationStartsAfterMarkerMs=380,bossRevealAfterMaterializationMs=90,greenShieldStartsAfterMarkerMs=600,shieldLoopMs=480,localHitMs=150,releaseMs=310,transitionPulseMs=300,transitionBossSource='Output/23_SectorAdministrator_Overdrive/sector_administrator_overdrive_transition.aseprite'}
manifest.constraints={separateFromBoss=true,greenPurpleSameGeometry=true,filledShieldSurface=false,shieldDoesNotCoverApprovedBossPixels=true,redUsed=false,approvedBossModified=false,unityGameplayModified=false}
manifest.presentation={arrival='Low-intensity segmented deployment geometry',materialization='Brief white-green concentration and controlled outward spokes',shield='Stable sparse perimeter, no interior fill',hit='Localized 32x32 response, never restart the full shield',release='Orderly gaps and perimeter retraction',phase='Same field structure, purple powered state'}
manifest.nativeReview={width=480,height=270,frameDurationMs=40,combinedFrames=100,arrivalFrames=40,overdriveFrames=35,previewIsUnityCapture=false}
write('manifest.json',manifest)
local f=assert(io.open(out..'/generation_complete.txt','w'));f:write('Sector intro and shield VFX authored with Aseprite CLI + Lua.\n');f:close()
