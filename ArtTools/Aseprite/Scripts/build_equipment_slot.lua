-- Equipment frame states, built from a verified copy of the approved 64px frame.
-- Static states only. Item art, labels and actual interaction remain in Unity.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgb(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function image(w,h)return Image(w,h,ColorMode.RGB)end
local function write(n,t)local f=assert(io.open(out..'/'..n,'w'));f:write(json.encode(t));f:close()end
local function flat(s,frame)local im=image(s.width,s.height);im:drawSprite(s,frame or 1,Point(0,0));return im end
local function composite(ls)local im=image(64,64);for _,l in ipairs(ls)do im:drawImage(l,Point(0,0))end;return im end
local function crop(im,x,y,w,h)local im2=image(w,h);im2:drawImage(im,Point(-x,-y));return im2 end
local function same(a,b)for p in a:pixels()do if p()~=b:getPixel(p.x,p.y)then return false end end;return true end
local paletteHex={'1D2B35','405663','7E98A3','66C4D6','DFEDF0','78BEA6','1D3936','141D25','29343D','46545E','91A2AA','0D1620'}
local C={};for i,h in ipairs(paletteHex)do C[i]=rgb(h)end
local id='UI_EquipmentSlot_64';local states={'Idle','Selected','Equipped','Locked'}
local layerNames={'Shared Frame Rails','Shared Corner Hardware','State Overlay','Persistent Marker'}
local manifest={generatorId='equipment-slot-v1',family=id,canvas={w=64,h=64},states=states,frameCount=4,staticStates=true,iconWindow={x=12,y=12,w=40,h=40},recommendedIconSize=32,sourcePolicy='Verified copies only'}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local reference=assert(app.open(src..'/frame_reference.aseprite'))
assert(reference.width==64 and reference.height==64 and #reference.frames==4 and #reference.layers==3)
local originalIdle=flat(reference,1);local rails=image(64,64);local corners=image(64,64)
for _,n in ipairs({1,3})do local cel=assert(reference.layers[n]:cel(1));rails:drawImage(cel.image,cel.position)end
local cel=assert(reference.layers[2]:cel(1));corners:drawImage(cel.image,cel.position);reference:close()
local base=composite({rails,corners});assert(same(base,originalIdle),'Approved Idle source changed during extraction')
base:saveAs(out..'/'..id..'_Base.png')
local function mirroredRect(im,x,y,w,h,color)
 for _,p in ipairs({{x,y},{64-x-w,y},{x,64-y-h},{64-x-w,64-y-h}})do H.rect(im,p[1],p[2],w,h,color)end
end
local function glyph(im,pattern,x,y,color)for row,line in ipairs(pattern)do for col=1,#line do if line:sub(col,col)=='1'then H.put(im,x+col-1,y+row-1,color)end end end end
local frames,rendered,meta={}, {}, {};local sprite=Sprite(64,64,ColorMode.RGB)
for i,name in ipairs(layerNames)do local l=i==1 and sprite.layers[1]or sprite:newLayer();l.name=name end
local strip=image(256,64)
for f,name in ipairs(states)do
 local overlay=image(64,64);local marker=image(64,64)
 if f==2 then
  -- Four separate focus brackets change the outline, not just its brightness.
  mirroredRect(overlay,1,1,12,2,C[4]);mirroredRect(overlay,1,1,2,12,C[4]);mirroredRect(overlay,1,1,2,2,C[5])
  for p in corners:pixels()do if pc.rgbaA(p())>0 then overlay:drawPixel(p.x,p.y,C[4])end end
  mirroredRect(overlay,6,6,2,2,C[5])
  H.rect(overlay,24,2,16,2,C[4])
  H.line(overlay,2,28,6,32,C[4]);H.line(overlay,6,32,2,36,C[4]);H.put(overlay,6,32,C[5])
  H.line(overlay,61,28,57,32,C[4]);H.line(overlay,57,32,61,36,C[4]);H.put(overlay,57,32,C[5])
  H.rect(overlay,28,58,8,2,C[4])
 elseif f==3 then
  H.rect(overlay,28,58,8,2,C[6])
  H.oct(marker,25,1,14,10,1,C[6]);H.rect(marker,26,2,12,8,C[7])
  glyph(marker,{'0000000011','0000000110','0000001100','1100011000','0110110000','0011100000','0001000000'},27,3,C[5])
 elseif f==4 then
  -- Repaint only the frame; the item icon is never dimmed or covered by the art.
  local dim={[C[1]]=C[8],[C[2]]=C[9],[C[3]]=C[10]}
  for p in base:pixels()do if pc.rgbaA(p())>0 then overlay:drawPixel(p.x,p.y,assert(dim[p()],'Unrecognized source palette'))end end
  H.oct(marker,25,1,14,10,1,C[10]);H.rect(marker,26,2,12,8,C[12])
  glyph(marker,{'00111100','01100110','01100110','11111111','11100111','11100111','11100111','11111111'},28,2,C[11])
 end
 local ls={rails,corners,overlay,marker};frames[f]=ls
 if f>1 then sprite:newEmptyFrame(f)end
 for n,im in ipairs(ls)do sprite:newCel(sprite.layers[n],f,im,Point(0,0))end;sprite.frames[f].duration=.2
 rendered[f]=flat(sprite,f);rendered[f]:saveAs(out..'/'..id..'_'..name..'.png');strip:drawImage(rendered[f],Point((f-1)*64,0))
 if f>1 then overlay:saveAs(out..'/'..id..'_'..name..'Overlay.png')end
 if f>2 then crop(marker,24,0,16,12):saveAs(out..'/'..id..'_'..name..'Marker.png')end
 meta[f]={filename=id..'_'..name,frame={x=(f-1)*64,y=0,w=64,h=64},rotated=false,trimmed=false,sourceSize={w=64,h=64},spriteSourceSize={x=0,y=0,w=64,h=64},duration=200}
end
local tags={};for f,name in ipairs(states)do local t=sprite:newTag(f,f);t.name=name;tags[f]={name=name,from=f-1,to=f-1,direction='forward'}end
local p=Palette(#C+1);p:setColor(0,Color{r=0,g=0,b=0,a=0});for i,c in ipairs(C)do p:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255})end;sprite:setPalette(p)
sprite:saveAs(out..'/'..id..'.aseprite');sprite:close();strip:saveAs(out..'/'..id..'.png')
write(id..'.json',{frames=meta,meta={app='Aseprite CLI + Lua',image=id..'.png',format='RGBA8888',size={w=256,h=64},scale='1',frameTags=tags},unity={staticStates=true,filterMode='Point',compression='None',mipmaps=false,pivot={x=.5,y=.5},iconWindow=manifest.iconWindow,recommendedIconSize=32,markerPosition={x=24,y=0},markerSize={w=16,h=12},coordinateOrigin='top-left',compositionOrder={'Base','StateOverlay','PersistentMarker'},notes='Use full state PNGs or compose separate parts. For Selected+Equipped, use Base, SelectedOverlay, EquippedOverlay, EquippedMarker. Do not animate the four-state strip.'}})

-- Approved 64px examples shown at 32px using exact nearest-neighbor sampling.
local icons={};for i,name in ipairs({'icon_precision.png','icon_shotgun.png','icon_machinegun.png'})do
 local original=Image{fromFile=src..'/'..name};assert(original.width==64 and original.height==64);local small=image(32,32)
 for y=0,31 do for x=0,31 do small:drawPixel(x,y,original:getPixel(x*2,y*2))end end;icons[i]=small
end
local function withIcon(frame,icon)local im=image(64,64);im:drawImage(icon,Point(16,16));im:drawImage(rendered[frame],Point(0,0));return im end
local function label(im,txt,x,y,color,n)H.text(im,txt:upper(),x,y,color,n)end
local contact=image(768,460);H.rect(contact,0,0,768,460,C[12]);label(contact,'VOID SCRAPPER / EQUIPMENT SLOTS',16,16,C[5],3)
for f,name in ipairs(states)do local x=24+(f-1)*180;label(contact,name,x+64-#name*4,56,C[3],2)
 contact:drawImage(rendered[f],Point(x+32,80));contact:drawImage(withIcon(f,icons[1]),Point(x+32,170));contact:drawImage(H.scale(rendered[f],2),Point(x,276))
end
label(contact,'NATIVE FRAMES WITH THE SAME EXAMPLE ICON',16,152,C[3],1)
label(contact,'DOUBLE SIZE DETAIL / NEAREST PIXELS',16,252,C[3],1)
label(contact,'TOP CHECK / EQUIPPED    PADLOCK / LOCKED',16,424,C[3],2)
label(contact,'CYAN BRACKETS / FOCUS    ITEM ICONS ARE NOT BAKED INTO EXPORTS',16,448,C[3],1)
contact:saveAs(out..'/EquipmentSlot_Comparison.png')
local native=image(480,270);H.rect(native,0,0,480,270,C[12]);label(native,'EQUIPMENT SLOTS / NATIVE UI',16,16,C[5],2)
label(native,'SAME ICON ACROSS STATES / CLEAR ICON WINDOWS',16,34,C[3],1)
for f,name in ipairs(states)do local x=52+(f-1)*104;label(native,name,x+32-#name*4,50,C[3],2)
 native:drawImage(withIcon(f,icons[1]),Point(x,72));native:drawImage(withIcon(f,icons[(f-1)%3+1]),Point(x,162))
end
label(native,'TOOLS / MODULES / EQUIPMENT EXAMPLES',16,146,C[3],1)
label(native,'TOP CHECK / IN USE    PADLOCK / UNAVAILABLE',16,244,C[3],1)
label(native,'ART MOCKUP / SOURCE ICONS UNCHANGED',16,258,C[3],1)
native:saveAs(out..'/EquipmentSlot_480x270.png')

local function stats(im)local seen={};local r={opaque=0,colors=0,luminanceSum=0}
 for p in im:pixels()do local c=p();local a=pc.rgbaA(c);assert(a==0 or a==255,'Non-binary alpha')
  if a>0 then seen[c]=true;r.opaque=r.opaque+1;r.luminanceSum=r.luminanceSum+.2126*pc.rgbaR(c)+.7152*pc.rgbaG(c)+.0722*pc.rgbaB(c)end
 end;for _ in pairs(seen)do r.colors=r.colors+1 end;r.meanLuminance=r.luminanceSum/r.opaque;return r
end
local report={passed=true,staticStates=true,assets={},pixelComparisons=0,idleMatchesApprovedReference=true,iconPixelsPreserved=true,previewIsUnityCapture=false}
local source=assert(app.open(out..'/'..id..'.aseprite'));assert(source.width==64 and source.height==64 and #source.frames==4 and #source.layers==4 and #source.tags==4)
local allowed={};for _,c in ipairs(C)do allowed[c]=true end
local sheet=Image{fromFile=out..'/'..id..'.png'};assert(sheet.width==256 and sheet.height==64)
local measurements={}
for f,name in ipairs(states)do local im=flat(source,f);local png=Image{fromFile=out..'/'..id..'_'..name..'.png'};local m=stats(im);measurements[f]=m
 -- Budget added state geometry relative to the retained approved frame footprint.
 local extraBudget=f==2 and 160 or f>2 and 100 or 0
 assert(png.width==64 and png.height==64 and m.colors<=6 and m.opaque<=stats(base).opaque+extraBudget,'State adds excessive border detail')
 assert(source.tags[f].name==name and source.tags[f].fromFrame.frameNumber==f and source.tags[f].toFrame.frameNumber==f)
 assert(math.floor(source.frames[f].duration*1000+.5)==200)
 for p in im:pixels()do local c=p();if pc.rgbaA(c)>0 then assert(allowed[c])end
  assert(c==png:getPixel(p.x,p.y)and c==sheet:getPixel((f-1)*64+p.x,p.y),'ASE/export mismatch');report.pixelComparisons=report.pixelComparisons+1
  if p.x>=12 and p.x<=51 and p.y>=12 and p.y<=51 then assert(pc.rgbaA(c)==0,'Icon window obscured')end
 end
 for n,l in ipairs(source.layers)do assert(l.name==layerNames[n]and l.isVisible and l.opacity==255);local cel=assert(l:cel(f));stats(cel.image)end
 local parts=image(64,64);parts:drawImage(Image{fromFile=out..'/'..id..'_Base.png'},Point(0,0))
 if f>1 then parts:drawImage(Image{fromFile=out..'/'..id..'_'..name..'Overlay.png'},Point(0,0))end
 if f>2 then local marker=Image{fromFile=out..'/'..id..'_'..name..'Marker.png'};assert(marker.width==16 and marker.height==12);parts:drawImage(marker,Point(24,0))end
 assert(same(parts,im),'Component reassembly mismatch')
 for _,icon in ipairs(icons)do local preview=withIcon(f,icon);for p in icon:pixels()do assert(p()==preview:getPixel(p.x+16,p.y+16),'Frame covers or changes example icon')end end
 report.assets[f]={state=name,colors=m.colors,opaquePixels=m.opaque,meanLuminance=m.meanLuminance,iconWindowClear=true,componentsReassembleExactly=true}
end
source:close();assert(same(rendered[1],originalIdle))
local extraFocus=0;for p in rendered[2]:pixels()do if pc.rgbaA(p())>0 and pc.rgbaA(base:getPixel(p.x,p.y))==0 then extraFocus=extraFocus+1 end end
assert(extraFocus>=120 and measurements[2].luminanceSum>measurements[1].luminanceSum*1.6,'Selected is too subtle')
assert(measurements[4].meanLuminance<measurements[1].meanLuminance*.8,'Locked is insufficiently dim')
assert(not same(frames[3][4],frames[4][4]),'Equipped and Locked symbols must differ')
local combined=composite({base,frames[2][3],frames[3][3],frames[3][4]})
for y=12,51 do for x=12,51 do assert(pc.rgbaA(combined:getPixel(x,y))==0,'Stacked focus/equipped blocks icon')end end
report.selectedAddsVisiblePixels=extraFocus;report.selectedLuminanceRatio=measurements[2].luminanceSum/measurements[1].luminanceSum;report.lockedMeanLuminanceRatio=measurements[4].meanLuminance/measurements[1].meanLuminance;report.focusCanStackWithEquipped=true
local reviewed=Image{fromFile=out..'/EquipmentSlot_Comparison.png'};for f,im in ipairs(rendered)do local x=56+(f-1)*180;for p in im:pixels()do if pc.rgbaA(p())>0 then assert(p()==reviewed:getPixel(x+p.x,80+p.y))end end end
local nativeSaved=Image{fromFile=out..'/EquipmentSlot_480x270.png'};assert(nativeSaved.width==480 and nativeSaved.height==270)
manifest.layers=layerNames;manifest.palette=paletteHex;manifest.previews={comparison='EquipmentSlot_Comparison.png',native='EquipmentSlot_480x270.png'}
write('manifest.json',manifest);write('validation.json',report)
