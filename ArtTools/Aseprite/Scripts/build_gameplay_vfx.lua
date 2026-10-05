-- Integer-pixel gameplay VFX for 480x270, PPU 32. Opaque pixels and transparent cleanup only.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,diamond,scale=h.put,h.rect,h.line,h.poly,h.oct,h.diamond,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local extraFont={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,label,x,y,c,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if extraFont[ch] then for yy,row in ipairs(extraFont[ch]) do for xx=1,3 do if row:sub(xx,xx)=='1' then rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,c) end end end
  else h.text(im,ch,px,y,c,n) end
 end
end
local hex={neutral={'515F67','ADB9B7','F0F2E9'},green={'254F39','4FA96C','B3EDAA'},orange={'6F3A26','E9913C','FFE0A2'},blue={'234871','529EDC','B8EFFF'},purple={'382A50','725194','AF89CC','DDC9EC'},cyan={'2C536F','64CDD9','C2F2EF'},red={'662C32','B84445','ED7563'}}
local P={};for key,values in pairs(hex) do P[key]={};for i,v in ipairs(values) do P[key][i]=rgba(v) end end
local white=rgba('F0F2E9')
local player=assert(Image{fromFile=sourceDir..'/Px_Player.png'});assert(player.width==16 and player.height==16,'Player reference size changed')
for _,name in ipairs({'raider_basic','raider_shotgun','raider_sniper_charging','unknown_device_analyzing'}) do local im=assert(Image{fromFile=sourceDir..'/'..name..'.png'});assert(im.width==64 and im.height==64,'Palette reference size changed') end
local layerNames={'Primary Shape','Highlights','Fragments','Secondary Marks'}
local function layers(w,hgt) local l={};for i=1,#layerNames do l[i]=Image(w/2,hgt/2,ColorMode.RGB) end;return l end
local function outline(im,pts,c) for i=1,#pts do local a,b=pts[i],pts[i%#pts+1];line(im,a[1],a[2],b[1],b[2],c) end end
local function ring(im,cx,cy,radius,c)
 local k=math.max(1,math.floor(radius*0.43));outline(im,{{cx-k,cy-radius},{cx+k,cy-radius},{cx+radius,cy-k},{cx+radius,cy+k},{cx+k,cy+radius},{cx-k,cy+radius},{cx-radius,cy+k},{cx-radius,cy-k}},c)
end
local function ringArcs(im,cx,cy,radius,c)
 local k=math.floor(radius*0.43)
 line(im,cx-k,cy-radius,cx-1,cy-radius,c);line(im,cx+radius,cy-k,cx+radius,cy-1,c)
 line(im,cx+1,cy+radius,cx+k,cy+radius,c);line(im,cx-radius,cy+1,cx-radius,cy+k,c)
 line(im,cx+k+2,cy-radius+2,cx+radius-2,cy-k-2,c);line(im,cx-radius+2,cy+k+2,cx-k-2,cy+radius-2,c)
end
local function flash(kind,f)
 local sniper=kind=='sniper';local l=layers(sniper and 64 or 32,32)
 local c=kind=='mg' and P.green or kind=='shotgun' and P.orange or sniper and P.blue or P.red
 if (kind=='mg' or kind=='raider') and f==3 then return l end
 if (kind=='shotgun' or sniper) and f==4 then return l end
 if kind=='mg' then
  if f==1 then poly(l[1],{{4,7},{8,4},{8,6},{12,8},{8,10},{8,12},{4,9}},c[2]);poly(l[2],{{4,7},{8,6},{10,8},{7,9},{4,8}},c[3]);line(l[2],4,8,8,8,white)
  else poly(l[1],{{4,7},{7,6},{10,8},{7,9},{4,9}},c[1]);line(l[2],4,8,8,8,c[2]);put(l[2],5,8,c[3]);rect(l[3],10,5,2,1,c[2]);put(l[3],11,10,c[1]) end
 elseif kind=='shotgun' then
  if f==1 then poly(l[1],{{4,7},{9,3},{9,6},{14,7},{11,9},{12,13},{7,10},{4,9}},c[2]);poly(l[2],{{4,7},{8,6},{11,7},{8,9},{5,9}},c[3]);rect(l[2],4,7,3,2,white)
  elseif f==2 then poly(l[1],{{4,7},{8,5},{10,5},{9,7},{13,8},{10,10},{9,12},{7,10},{4,9}},c[1]);poly(l[2],{{5,7},{9,6},{8,8},{10,9},{7,10}},c[2]);line(l[2],5,8,8,8,c[3]);line(l[3],11,3,13,4,c[2]);line(l[3],12,11,13,13,c[2])
  else rect(l[3],11,4,2,1,c[2]);rect(l[3],13,8,1,2,c[1]);rect(l[3],11,12,2,1,c[1]);put(l[1],6,8,c[1]) end
 elseif sniper then
  if f==1 then poly(l[1],{{4,7},{9,5},{12,7},{25,8},{12,9},{9,11},{4,9}},c[2]);line(l[2],4,8,24,8,c[3]);line(l[2],4,8,13,8,white);rect(l[4],7,5,2,1,c[3]);rect(l[4],7,11,2,1,c[3])
  elseif f==2 then line(l[1],4,8,29,8,c[2]);line(l[2],7,8,24,8,c[3]);line(l[3],10,5,14,6,c[1]);line(l[3],16,10,20,10,c[2]);put(l[3],26,7,c[3])
  else rect(l[3],17,8,5,1,c[1]);rect(l[3],26,8,3,1,c[2]);rect(l[3],11,6,2,1,c[1]);put(l[3],22,10,c[1]) end
 else
  if f==1 then poly(l[1],{{4,7},{7,4},{8,6},{11,5},{10,8},{13,10},{9,10},{8,12},{5,9}},c[2]);poly(l[2],{{4,7},{7,6},{10,8},{7,10},{4,9}},P.orange[2]);line(l[2],4,8,8,8,P.orange[3]);put(l[2],5,8,white)
  else poly(l[1],{{4,8},{8,6},{7,9},{10,11},{6,10}},c[1]);line(l[2],4,8,7,8,c[3]);rect(l[3],11,5,2,1,P.orange[2]);rect(l[3],11,11,1,2,c[2]);put(l[3],9,7,c[2]) end
 end
 return l
end
local function hit(f)
 local l=layers(32,32);if f==4 then return l end
 if f==1 then diamond(l[1],8,8,3,P.neutral[2]);line(l[2],8,3,8,12,white);line(l[2],3,8,12,8,white);diamond(l[2],8,8,2,white)
 elseif f==2 then line(l[1],8,3,8,5,P.neutral[2]);line(l[1],11,8,13,8,P.neutral[2]);line(l[1],4,11,3,12,P.neutral[2]);line(l[2],10,5,12,3,white);line(l[2],5,6,3,5,white);line(l[2],9,11,10,13,white);put(l[2],8,8,white)
 else put(l[3],12,3,P.neutral[2]);put(l[3],3,12,P.neutral[1]);rect(l[3],12,12,2,1,P.neutral[1]);put(l[3],2,5,P.neutral[1]) end
 return l
end
local function explosion(small,f)
 local l=layers(small and 32 or 64,small and 32 or 64);local c=small and 8 or 16
 if f==(small and 5 or 6) then return l end
 if f==1 then diamond(l[1],c,c,small and 3 or 5,P.orange[2]);diamond(l[2],c,c,small and 2 or 4,white);put(l[2],c+2,c-1,P.orange[3])
 elseif f==2 then
  local r=small and 5 or 9;oct(l[1],c-r,c-r,r*2+1,r*2+1,math.floor(r/2),P.red[2]);oct(l[1],c-r+1,c-r+1,r*2-1,r*2-1,math.floor(r/2),P.orange[2]);diamond(l[2],c-1,c-1,small and 3 or 5,P.orange[3]);diamond(l[2],c-1,c-1,small and 1 or 3,white)
  rect(l[3],c+r,c-2,1,2,P.red[3]);rect(l[3],c-2,c+r,2,1,P.orange[2])
 elseif small and f==3 then
  poly(l[1],{{3,4},{6,2},{9,3},{12,3},{14,7},{12,11},{9,13},{5,12},{2,9}},P.red[2]);oct(l[1],5,5,7,7,2,0)
  line(l[2],4,4,7,3,P.orange[2]);line(l[2],12,5,13,8,P.orange[2]);line(l[2],9,12,11,11,P.orange[2]);put(l[3],3,11,P.red[3])
 elseif small then rect(l[3],3,4,2,1,P.red[2]);put(l[3],13,9,P.orange[2]);rect(l[3],8,13,2,1,P.red[1])
 elseif f==3 then
  ring(l[1],16,16,12,P.red[2]);ringArcs(l[2],16,16,11,P.orange[2]);diamond(l[1],16,16,5,P.red[2]);diamond(l[1],16,16,3,0);put(l[2],15,12,P.orange[3]);rect(l[3],4,8,2,2,P.orange[2]);rect(l[3],26,23,2,2,P.red[3]);rect(l[3],9,27,2,1,P.orange[2]);rect(l[3],24,4,2,1,P.red[2])
 elseif f==4 then
  ringArcs(l[1],16,16,14,P.red[1]);line(l[2],4,7,6,5,P.red[2]);line(l[2],26,26,28,24,P.orange[2]);rect(l[3],3,12,2,1,P.red[2]);rect(l[3],12,29,2,1,P.red[2]);put(l[3],28,5,P.orange[2])
 else put(l[3],3,8,P.red[1]);rect(l[3],27,27,2,1,P.red[2]);rect(l[3],10,29,2,1,P.red[1]);put(l[3],28,3,P.red[1]) end
 return l
end
local function dash(cursed,f)
 local l=layers(32,32);if f==4 then return l end
 local c=cursed and {P.purple[1],P.purple[3],P.purple[4]} or P.cyan
 if not cursed then
  if f==1 then outline(l[1],{{5,4},{12,4},{12,11},{5,11}},c[2]);line(l[2],6,4,11,4,white);line(l[2],5,5,5,10,c[3]);rect(l[4],6,5,2,2,c[1]);rect(l[4],9,9,2,2,c[1]);line(l[3],2,6,3,6,c[2]);line(l[3],1,10,3,10,c[1])
  elseif f==2 then line(l[1],3,4,9,4,c[2]);line(l[1],3,4,3,9,c[1]);line(l[1],4,11,8,11,c[2]);rect(l[3],9,8,2,1,c[1]);put(l[2],4,4,c[3]);rect(l[3],1,7,2,1,c[1])
  else rect(l[3],2,4,3,1,c[1]);rect(l[3],1,10,2,1,c[1]);put(l[3],6,11,c[2]);put(l[3],7,6,c[1]) end
 else
  if f==1 then line(l[1],5,4,11,4,c[2]);line(l[1],4,5,4,7,c[2]);line(l[1],7,8,13,8,c[1]);line(l[1],6,11,12,11,c[2]);line(l[2],6,4,10,4,c[3]);rect(l[4],7,5,3,2,c[1]);rect(l[4],5,9,2,2,c[1]);rect(l[3],2,6,2,1,c[2]);put(l[3],14,10,c[2])
  elseif f==2 then rect(l[1],3,4,5,1,c[2]);rect(l[1],6,7,5,1,c[1]);rect(l[1],2,11,6,1,c[2]);put(l[2],4,4,c[3]);rect(l[3],1,7,2,1,c[1]);rect(l[3],10,10,2,1,c[1])
  else rect(l[3],1,4,3,1,c[1]);rect(l[3],5,8,3,1,c[1]);rect(l[3],2,12,2,1,c[2]);put(l[3],10,6,c[1]) end
 end
 return l
end
local telegraphRadii={14,12,10,8,5,2}
local function telegraph(f)
 local l=layers(64,64);local rad=telegraphRadii[f]
 ring(l[1],16,16,rad,P.red[2]);line(l[2],16-math.max(1,math.floor(rad*0.4)),16-rad,16,16-rad,P.red[3])
 put(l[4],16,16,f>=5 and P.red[3] or P.red[1]);if f<6 then put(l[4],14,16,P.red[1]);put(l[4],18,16,P.red[1]);put(l[4],16,14,P.red[1]);put(l[4],16,18,P.red[1]) else put(l[2],16,16,P.orange[3]) end
 return l
end
local function arrival(f)
 local l=layers(64,64);if f==4 then return l end
 if f==1 then ring(l[1],16,16,5,P.red[3]);diamond(l[2],16,16,3,P.orange[3]);put(l[2],16,16,white)
 elseif f==2 then ring(l[1],16,16,10,P.red[2]);line(l[2],12,6,17,6,P.orange[2]);line(l[3],7,7,5,5,P.orange[3]);line(l[3],25,24,27,26,P.orange[2]);line(l[3],8,24,6,26,P.red[3]);line(l[3],24,8,26,6,P.red[3])
 else ringArcs(l[1],16,16,14,P.red[1]);rect(l[3],3,5,2,1,P.red[2]);put(l[3],28,26,P.orange[2]);put(l[3],4,28,P.red[2]);rect(l[3],27,3,2,1,P.red[1]) end
 return l
end
local function shield(f)
 local l=layers(64,64);if f==4 then return l end
 if f==1 then
  outline(l[1],{{19,9},{25,12},{28,17},{24,23},{18,25}},P.cyan[2]);line(l[1],19,9,16,12,P.cyan[1]);line(l[1],18,25,15,22,P.cyan[1]);diamond(l[2],24,16,3,P.cyan[3]);line(l[2],23,16,27,16,white);put(l[2],24,15,white)
 elseif f==2 then
  line(l[1],18,7,25,11,P.cyan[2]);line(l[1],25,11,29,17,P.cyan[2]);line(l[1],29,17,25,24,P.cyan[1]);line(l[1],25,24,18,27,P.cyan[2]);line(l[4],17,12,21,14,P.cyan[1]);line(l[4],21,14,21,20,P.cyan[1]);line(l[4],21,20,17,23,P.cyan[1]);line(l[2],26,14,28,17,P.cyan[3]);rect(l[3],27,10,2,1,P.cyan[2]);put(l[3],26,26,P.cyan[2])
 else line(l[1],18,6,22,8,P.cyan[1]);line(l[1],28,12,29,15,P.cyan[1]);line(l[1],26,25,22,28,P.cyan[1]);put(l[3],16,25,P.cyan[2]);put(l[3],28,22,P.cyan[2]) end
 return l
end
local function pickup(f)
 local l=layers(32,32)
 if f==1 then rect(l[1],7,7,2,2,P.orange[2]);put(l[2],8,7,P.orange[3])
 elseif f==2 then diamond(l[1],8,8,2,P.orange[2]);line(l[1],8,3,8,13,P.orange[2]);line(l[1],3,8,13,8,P.orange[2]);line(l[2],8,5,8,11,P.orange[3]);line(l[2],5,8,11,8,P.orange[3]);diamond(l[2],8,8,1,white);put(l[3],12,4,P.orange[3])
 elseif f==3 then line(l[1],8,5,8,11,P.orange[2]);line(l[1],5,8,11,8,P.orange[2]);rect(l[2],7,7,2,2,P.orange[3]);put(l[3],12,4,P.orange[1]);put(l[3],4,12,P.orange[2])
 else put(l[1],8,8,P.orange[1]);put(l[2],8,7,P.orange[2]) end
 return l
end
local function activation(f,c)
 local l=layers(64,64);if f==6 then return l end
 if f==1 then diamond(l[1],16,16,4,c[2]);diamond(l[2],16,16,2,c[3]);put(l[2],16,16,white)
 elseif f==2 then ring(l[1],16,16,8,c[2]);diamond(l[2],16,16,3,c[3]);line(l[3],16,5,16,7,c[2]);line(l[3],25,16,27,16,c[2]);line(l[3],16,25,16,27,c[2]);line(l[3],5,16,7,16,c[2]);line(l[2],13,8,16,8,c[3])
 elseif f==3 then ring(l[1],16,16,11,c[2]);line(l[2],12,5,16,5,c[3]);line(l[2],27,13,27,16,c[3]);put(l[2],16,16,c[3]);rect(l[3],6,6,2,1,c[2]);rect(l[3],25,25,2,1,c[2])
 elseif f==4 then ringArcs(l[1],16,16,14,c[1]);line(l[2],11,2,15,2,c[2]);line(l[2],30,12,30,15,c[2]);line(l[3],4,7,6,5,c[2]);line(l[3],27,26,28,24,c[2])
 else rect(l[3],9,3,3,1,c[1]);rect(l[3],29,22,1,2,c[1]);rect(l[3],22,29,2,1,c[1]);rect(l[3],3,11,1,2,c[1]) end
 return l
end
local families={}
local function add(id,label,short,wid,hgt,ms,pivot,mode,make,groups,extra)
 local family={id=id,label=label,short=short,width=wid,height=hgt,frameDurationsMs=ms,pivotPixels=pivot,mode=mode,make=make,groups=groups,variants={{tag='Default',paletteKey='default'}}}
 if extra then for k,v in pairs(extra) do family[k]=v end end;families[#families+1]=family
end
add('VFX_Player_MachineGunMuzzle','PLAYER MACHINE GUN','MG',32,32,{30,40,30},{x=8,y=16},'one_shot',function(f) return flash('mg',f) end,{'green','neutral'},{direction='+X',peakFrame=1})
add('VFX_Player_ShotgunMuzzle','PLAYER SHOTGUN','SHOTGUN',32,32,{45,50,60,35},{x=8,y=16},'one_shot',function(f) return flash('shotgun',f) end,{'orange','neutral'},{direction='+X',peakFrame=1})
add('VFX_Player_SniperMuzzle','PLAYER SNIPER','SNIPER',64,32,{35,45,50,30},{x=8,y=16},'one_shot',function(f) return flash('sniper',f) end,{'blue','neutral'},{direction='+X',peakFrame=1})
add('VFX_Raider_Muzzle','RAIDER MUZZLE','RAIDER',32,32,{40,45,35},{x=8,y=16},'one_shot',function(f) return flash('raider',f) end,{'red','orange','neutral'},{direction='+X',peakFrame=1})
add('VFX_Hit_Generic','GENERIC HIT','HIT',32,32,{35,45,60,30},{x=16,y=16},'one_shot',hit,{'neutral'},{peakFrame=1})
add('VFX_Explosion_Small','SMALL EXPLOSION','SMALL',32,32,{40,55,55,65,35},{x=16,y=16},'one_shot',function(f) return explosion(true,f) end,{'red','orange','neutral'},{peakFrame=2})
add('VFX_Explosion_Medium','MEDIUM EXPLOSION','MEDIUM',64,64,{45,60,70,80,85,40},{x=32,y=32},'one_shot',function(f) return explosion(false,f) end,{'red','orange','neutral'},{peakFrame=2})
add('VFX_Player_Dash','PLAYER DASH','DASH',32,32,{40,50,60,30},{x=24,y=16},'one_shot',function(f) return dash(false,f) end,{'cyan','neutral'},{direction='+X travel; trail extends toward -X',peakFrame=1})
add('VFX_Player_Dash_Curse','CURSED DASH','CURSE',32,32,{40,50,60,30},{x=24,y=16},'one_shot',function(f) return dash(true,f) end,{'purple'},{direction='+X travel; trail extends toward -X',peakFrame=1})
add('VFX_EnemyArrival_Telegraph','ARRIVAL TELEGRAPH','ARRIVAL',64,64,{120,120,120,100,100,80},{x=32,y=32},'handoff',telegraph,{'red','orange'},{ringRadiiPixels={28,24,20,16,10,4},completion='Hide the telegraph and trigger Arrival Impact after 640ms. Do not loop or freeze the final marker.',peakFrame=2})
add('VFX_EnemyArrival_Impact','ARRIVAL IMPACT','IMPACT',64,64,{45,60,75,40},{x=32,y=32},'one_shot',arrival,{'red','orange','neutral'},{peakFrame=2})
add('VFX_ShieldHit','SHIELD HIT','SHIELD',64,64,{40,60,75,35},{x=48,y=32},'one_shot',shield,{'cyan','neutral'},{direction='Surface normal +X; pivot is localized contact point',peakFrame=1})
add('VFX_PickupSparkle','PICKUP SPARKLE','PICKUP',32,32,{110,80,100,190},{x=16,y=16},'loop',pickup,{'orange','neutral'},{peakFrame=2,completion='Four-frame twinkle loop; dim terminal point returns to the small starting glint.'})
add('VFX_CoreActivation','CORE ACTIVATION','CORE',64,64,{40,50,65,75,80,40},{x=32,y=32},'one_shot',activation,{'neutral','green','orange','blue','purple'},{peakFrame=3,variants={{tag='Neutral',paletteKey='neutral'},{tag='Green',paletteKey='green'},{tag='Orange',paletteKey='orange'},{tag='Blue',paletteKey='blue'},{tag='Purple',paletteKey='purple'}}})
local manifest={generatorId='void-scrapper-gameplay-vfx-v1',name='CORE GAMEPLAY VFX PACK',target={width=480,height=270,pixelsPerUnit=32,constructionPixelScale=2,binaryAlpha=true},layers=layerNames,families={},
 preview='VFX_ContactSheet.png',nativePreview='VFX_480x270_Preview.png',animatedPreview='VFX_480x270_Preview.gif',
 pivotConvention='pivotPixels use PNG top-left coordinates. Unity normalized pivot is (x/width, 1-y/height). Muzzles face +X; dash trails extend -X.',
 animationPolicy='One-shots finish with a transparent cleanup cel; Arrival Telegraph hands off to Arrival Impact. Only Pickup Sparkle loops. Do not trim fixed sprite cells.',
 referencePolicy='Inspected copied 16x16 square player and approved role colors. Existing approved art remains unchanged.'}
local clips={}
local function saveJson(path,value) local f=assert(io.open(path,'w'));f:write(json.encode(value));f:close() end
for _,family in ipairs(families) do
 local n=#family.frameDurationsMs;local sprite=Sprite(family.width,family.height,ColorMode.RGB)
 for i,name in ipairs(layerNames) do local layer=i==1 and sprite.layers[1] or sprite:newLayer();layer.name=name end
 local all=Image(family.width*n,family.height*#family.variants,ColorMode.RGB);local tags={};local metaFrames={}
 local allowedHex,seen={},{};for _,key in ipairs(family.groups) do for _,v in ipairs(hex[key]) do if not seen[v] then allowedHex[#allowedHex+1]=v;seen[v]=true end end end
 local record={id=family.id,label=family.label,width=family.width,height=family.height,pixelsPerUnit=32,layers=layerNames,mode=family.mode,framesPerVariant=n,frameDurationsMs=family.frameDurationsMs,
  pivotPixels=family.pivotPixels,unityPivot={x=family.pivotPixels.x/family.width,y=1-family.pivotPixels.y/family.height},direction=family.direction or 'Radial / centered',palette=allowedHex,
  aseprite=family.id..'.aseprite',sheet=family.id..'.png',metadata=family.id..'.json',variants={},completion=family.completion or 'Hide or recycle after the transparent final cel.',ringRadiiPixels=family.ringRadiiPixels}
 for row,variant in ipairs(family.variants) do
  local strip=Image(family.width*n,family.height,ColorMode.RGB);local images={};local start=(row-1)*n+1
  for frame=1,n do
   local index=start+frame-1;if index>1 then sprite:newEmptyFrame(index) end
   local colors=variant.paletteKey~='default' and P[variant.paletteKey] or nil
   local ls=family.make(frame,colors);local flat=Image(family.width,family.height,ColorMode.RGB)
   for li,im in ipairs(ls) do local doubled=scale(im,2);sprite:newCel(sprite.layers[li],index,doubled,Point(0,0));flat:drawImage(doubled,Point(0,0)) end
   sprite.frames[index].duration=family.frameDurationsMs[frame]/1000;images[frame]=flat
   strip:drawImage(flat,Point((frame-1)*family.width,0));all:drawImage(flat,Point((frame-1)*family.width,(row-1)*family.height))
   metaFrames[#metaFrames+1]={filename=family.id..'_'..variant.tag..'_'..string.format('%02d',frame-1),frame={x=(frame-1)*family.width,y=(row-1)*family.height,w=family.width,h=family.height},rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=family.width,h=family.height},sourceSize={w=family.width,h=family.height},duration=family.frameDurationsMs[frame]}
  end
  tags[#tags+1]={name=variant.tag,from=start-1,to=start+n-2,direction='forward'}
  local filename=family.id..(#family.variants>1 and '_'..variant.tag or '')..'.png'
  if #family.variants>1 then strip:saveAs(outputDir..'/'..filename) end
  record.variants[#record.variants+1]={tag=variant.tag,fromFrame=start,toFrame=start+n-1,sheet=filename,sheetRow=row-1,paletteKey=variant.paletteKey}
  clips[#clips+1]={family=family,tag=variant.tag,frames=images,sheet=filename,record=record}
 end
 for _,tag in ipairs(tags) do local t=sprite:newTag(tag.from+1,tag.to+1);t.name=tag.name end
 sprite:saveAs(outputDir..'/'..record.aseprite);sprite:close();all:saveAs(outputDir..'/'..record.sheet)
 saveJson(outputDir..'/'..record.metadata,{frames=metaFrames,meta={app='Aseprite CLI + Lua',version='1',image=record.sheet,format='RGBA8888',size={w=all.width,h=all.height},scale='1',frameTags=tags},unity={pixelsPerUnit=32,pivot=record.unityPivot,filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',loop=family.mode=='loop',frameDurationsMs=family.frameDurationsMs}})
 manifest.families[#manifest.families+1]=record
end
-- Contact sheet: 18 clips in 2 columns. Small effects are enlarged 2x, 64px effects shown 1x.
local contact=Image(1000,1446,ColorMode.RGB);contact:clear(Color{r=18,g=24,b=32,a=255})
text(contact,'CORE GAMEPLAY VFX',24,18,P.neutral[3],3);text(contact,'14 FAMILIES / 18 CLIPS / 85 FRAMES',24,45,P.neutral[2],2)
local bgA,bgB=rgba('222D38'),rgba('2A3540')
for i,clip in ipairs(clips) do
 local col=(i-1)%2;local row=math.floor((i-1)/2);local x=18+col*496;local y=78+row*150
 local label=clip.family.label..(clip.tag=='Default' and '' or ' / '..clip.tag:upper());text(contact,label,x+6,y,P.neutral[3],1)
 local dimensions=clip.family.width..' X '..clip.family.height..' / '..#clip.frames..'F / '..(clip.family.mode=='loop' and 'LOOP' or clip.family.mode=='handoff' and 'HANDOFF' or 'ONE SHOT')
 text(contact,dimensions,x+6,y+14,P.neutral[2],1)
 for frame,im in ipairs(clip.frames) do
  local bx=x+frame*72-66;local by=y+31
  for yy=0,63 do for xx=0,63 do put(contact,bx+xx,by+yy,(math.floor(xx/8)+math.floor(yy/8))%2==0 and bgA or bgB) end end
  local factor=im.width==32 and 2 or 1;local shown=scale(im,factor)
  contact:drawImage(shown,Point(bx+math.floor((64-shown.width)/2),by+math.floor((64-shown.height)/2)))
  text(contact,tostring(frame),bx+2,by+69,P.neutral[2],1);text(contact,tostring(clip.family.frameDurationsMs[frame])..'MS',bx+16,by+69,P.neutral[2],1)
 end
 text(contact,clip.family.width==32 and '2X REVIEW / NATIVE 32PX' or '1X REVIEW / NATIVE PIXELS',x+6,y+117,P.neutral[1],1)
end
contact:saveAs(outputDir..'/VFX_ContactSheet.png')
local function board(t,peak)
 local im=Image(480,270,ColorMode.RGB);im:clear(Color{r=15,g=21,b=29,a=255});text(im,'480 X 270 / PPU 32 / NATIVE PIXELS',8,6,P.neutral[2],1)
 for i,clip in ipairs(clips) do
  local col=(i-1)%6;local row=math.floor((i-1)/6);local x=col*80;local y=24+row*80
  rect(im,x+1,y,78,78,(col+row)%2==0 and rgba('17212C') or rgba('1C2732'))
  local label=clip.family.short..(clip.tag=='Default' and '' or ' '..clip.tag:upper());text(im,label,x+math.floor((80-#label*4)/2),y+68,P.neutral[2],1)
  local frame=nil
  if peak then frame=clip.family.peakFrame else local time=t;if clip.family.mode=='loop' then local total=0;for _,d in ipairs(clip.family.frameDurationsMs) do total=total+d end;time=time%total end
   local total=0;for fi,d in ipairs(clip.family.frameDurationsMs) do total=total+d;if time<total then frame=fi;break end end
  end
  local muzzle=clip.family.id:find('Muzzle')~=nil;local dashFx=clip.family.id=='VFX_Player_Dash' or clip.family.id=='VFX_Player_Dash_Curse';local shieldFx=clip.family.id=='VFX_ShieldHit'
  local ax,ay=x+40,y+34;if muzzle then ax=x+26 elseif dashFx then ax=x+44 elseif shieldFx then ax=x+53 end
  if muzzle then im:drawImage(player,Point(x+10,y+26)) elseif shieldFx then im:drawImage(player,Point(x+30,y+26)) end
  if frame then im:drawImage(clip.frames[frame],Point(ax-clip.family.pivotPixels.x,ay-clip.family.pivotPixels.y)) end
  if dashFx then im:drawImage(player,Point(ax-8,ay-8)) end
  -- Preview-only handoff illustrates the contracting marker followed by impact.
  if not peak and clip.family.mode=='handoff' and t>=640 then
   local impact=clips[11];local elapsed=t-640;local total=0;local fi=nil
   for index,d in ipairs(impact.family.frameDurationsMs) do total=total+d;if elapsed<total then fi=index;break end end
   if fi then im:drawImage(impact.frames[fi],Point(ax-32,ay-32)) end
  end
 end
 return im
end
board(0,true):saveAs(outputDir..'/VFX_480x270_Preview.png')
-- GIF is a composited review artifact, not a gameplay sprite. Repeats once per second.
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native Pixel Review'
for i=1,50 do if i>1 then review:newEmptyFrame(i) end;review:newCel(review.layers[1],i,board((i-1)*20,false),Point(0,0));review.frames[i].duration=0.02 end
review:saveAs(outputDir..'/VFX_480x270_Preview.gif');review:close()
saveJson(outputDir..'/manifest.json',manifest)
local f=assert(io.open(outputDir..'/generation_complete.txt','w'));f:write('14 editable VFX families, 18 clips, 85 frames, 480x270 native animated review.\n');f:close()
print('GAMEPLAY_VFX_GENERATED')
