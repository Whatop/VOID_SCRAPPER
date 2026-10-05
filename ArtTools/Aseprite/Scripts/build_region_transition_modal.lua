-- Static modal art only. Unity owns scale-up, backdrop fade, text and reveal timing.
local src=assert(app.params.sourceDir);local out=assert(app.params.outputDir)
local H=dofile(assert(app.params.helperPath));local pc=app.pixelColor
local function rgb(h)return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)end
local function image(w,h)return Image(w,h,ColorMode.RGB)end
local function write(n,t)local f=assert(io.open(out..'/'..n,'w'));f:write(json.encode(t));f:close()end
local function path(im,p,c)for i=1,#p-1 do H.line(im,p[i][1],p[i][2],p[i+1][1],p[i+1][2],c)end end
local function composite(ls,w,h)local im=image(w or 304,h or 168);for _,l in ipairs(ls)do im:drawImage(l,Point(0,0))end;return im end
local function crop(im,r)local o=image(r.w,r.h);o:drawImage(im,Point(-r.x,-r.y));return o end
local function render(s)local im=image(s.width,s.height);im:drawSprite(s,1,Point(0,0));return im end
local function count(im)local n=0;for p in im:pixels()do if pc.rgbaA(p())>0 then n=n+1 end end;return n end
local paletteHex={'080D14','0D1620','111E2B','1D2B35','405663','7E98A3','66C4D6','DFEDF0','78BEA6','1D3936','1B3542'}
local C={};for i,h in ipairs(paletteHex)do C[i]=rgb(h)end
local names={'Panel Fill','Frame Hardware','Layout Rails','Energy Accents','Icon Well','State Symbol','Region Tag','Continue Button'}
local specs={
 {id='Base',tag='Base',label='REUSABLE FRAME',accent=C[6],fill=C[3],title='ROUTE NOTICE',sub='REGION STATE INFORMATION',region='REGION',detail='STATE NOTICE',footer='ROUTE CONTROL',button='CONTINUE'},
 {id='SafeReturn',tag='SafeReturn',label='SAFE RETURN',accent=C[9],fill=C[10],title='SAFE RETURN',sub='RETURNED TO SETTLEMENT',region='HOME',detail='ALL CLEAR',footer='LINK SECURED',button='CONFIRM'},
 {id='NextRegion',tag='NextRegion',label='NEXT REGION ENTRY',accent=C[7],fill=C[11],title='NEXT REGION',sub='ADVANCE TO NEXT SECTOR',region='REGION B',detail='ROUTE READY',footer='DEPLOY READY',button='CONTINUE'}
}
local areas={
 title={x=80,y=28,w=200,h=22},subtitle={x=80,y=56,w=200,h=12},
 symbol={x=26,y=32,w=32,h=32},regionLabel={x=36,y=98,w=72,h=12},
 bodyDetail={x=128,y=96,w=148,h=14},footerHint={x=26,y=140,w=128,h=12},
 continueLabel={x=184,y=140,w=76,h=12}
}
local components={backdrop={x=0,y=0,w=304,h=168},layout={x=0,y=0,w=304,h=168},energy={x=0,y=0,w=304,h=168},symbol=areas.symbol,regionTag={x=20,y=92,w=96,h=22},button={x=170,y=132,w=112,h=24}}
local manifest={generatorId='region-transition-modal-v1',canvas={w=304,h=168},nativePreview={w=480,h=270},families={},animationOwner='Unity',textOwner='Unity localization / TMP',bakedText=false}
write('manifest.json',{generatorId=manifest.generatorId,status='generation in progress'})
local reference=assert(app.open(src..'/ui_frame.aseprite'));assert(reference.width==96 and reference.height==96 and #reference.frames==4);reference:close()
local assets={};local backdrop,sharedLayout
for index,spec in ipairs(specs)do
 local l={};for i=1,8 do l[i]=image(152,84)end
 local outline={{7,2},{144,2},{149,7},{149,76},{144,81},{7,81},{2,76},{2,7},{7,2}}
 local shadow={};for _,p in ipairs(outline)do shadow[#shadow+1]={p[1]+1,p[2]+1}end
 H.poly(l[1],shadow,C[1]);H.poly(l[1],outline,C[2])
 path(l[2],outline,C[5]);path(l[2],{{8,3},{143,3},{148,8},{148,75},{143,80},{8,80},{3,75},{3,8},{8,3}},C[4])
 -- Small stepped corner clamps match the approved equipment-frame language.
 for _,p in ipairs({{6,6,1,1},{145,6,-1,1},{145,77,-1,-1},{6,77,1,-1}})do
  H.line(l[2],p[1],p[2],p[1]+p[3]*4,p[2],C[6]);H.line(l[2],p[1],p[2],p[1],p[2]+p[4]*4,C[6])
  H.put(l[4],p[1]+p[3],p[2],spec.accent)
  if index>1 then H.put(l[4],p[1],p[2],index==3 and C[8]or spec.accent)end
 end
 -- Broad flat fields keep text clear; the border is only one native 2px stroke.
 H.rect(l[3],8,12,136,26,C[3]);H.line(l[3],12,39,139,39,C[4])
 H.rect(l[3],8,62,136,16,C[3]);H.line(l[3],12,61,139,61,C[4])
 H.rect(l[4],63,2,26,1,C[4]);H.rect(l[4],68,2,16,1,spec.accent)
 if index==3 then H.rect(l[4],74,2,4,1,C[8]);H.rect(l[4],57,2,2,1,spec.accent);H.rect(l[4],93,2,2,1,spec.accent)end
 if index==2 then H.rect(l[4],74,3,4,1,C[8])end
 H.rect(l[4],12,39,8,1,spec.accent)
 -- Empty icon socket and separate 32px symbol.
 H.oct(l[5],10,13,22,22,3,C[5]);H.oct(l[5],11,14,20,20,2,C[2])
 H.rect(l[5],14,34,14,1,C[4])
 local symbol=image(16,16)
 if index==2 then
  path(symbol,{{3,6},{8,2},{13,6}},spec.accent)
  path(symbol,{{3,7},{3,13},{13,13},{13,7}},spec.accent)
  H.line(symbol,8,6,8,11,C[8]);path(symbol,{{6,9},{8,11},{10,9}},C[8])
 elseif index==3 then
  path(symbol,{{11,2},{14,2},{14,13},{11,13}},C[6])
  H.line(symbol,2,8,10,8,spec.accent);path(symbol,{{6,4},{10,8},{6,12}},C[8])
  H.put(symbol,1,8,C[6]);H.rect(symbol,12,6,1,4,spec.accent)
 end
 l[6]:drawImage(symbol,Point(13,16))
 -- Blank region badge; the small state pip does not encode a particular region.
 H.oct(l[7],10,46,48,11,2,C[4]);H.oct(l[7],11,47,46,9,1,C[3]);H.rect(l[7],12,50,2,2,spec.accent)
 -- Confirm/continue artwork has no text and no click logic.
 H.oct(l[8],85,66,56,12,2,C[5]);H.oct(l[8],86,67,54,10,1,spec.fill)
 H.line(l[8],89,66,136,66,spec.accent);path(l[8],{{133,70},{135,72},{133,74}},spec.accent)
 local ls={};for i,im in ipairs(l)do ls[i]=H.scale(im,2)end
 local id='UI_RegionTransition_'..spec.id;local s=Sprite(304,168,ColorMode.RGB)
 for i,name in ipairs(names)do local layer=i==1 and s.layers[1]or s:newLayer();layer.name=name;s:newCel(layer,1,ls[i],Point(0,0))end
 local pal=Palette(#paletteHex+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,p in ipairs(C)do pal:setColor(i,Color{r=pc.rgbaR(p),g=pc.rgbaG(p),b=pc.rgbaB(p),a=255})end;s:setPalette(pal)
 s.frames[1].duration=.1;local tag=s:newTag(1,1);tag.name=spec.tag
 local full=render(s);s:saveAs(out..'/'..id..'.aseprite');s:close();full:saveAs(out..'/'..id..'.png')
 ls[4]:saveAs(out..'/'..id..'_Energy.png')
 if index>1 then crop(ls[6],components.symbol):saveAs(out..'/'..id..'_Symbol.png')end
 crop(ls[7],components.regionTag):saveAs(out..'/'..id..'_RegionTag.png');crop(ls[8],components.button):saveAs(out..'/'..id..'_Continue.png')
 if index==1 then
  backdrop=composite({ls[1],ls[2]});backdrop:saveAs(out..'/UI_RegionTransition_SharedBackdrop.png')
  sharedLayout=composite({ls[3],ls[5]});sharedLayout:saveAs(out..'/UI_RegionTransition_SharedLayout.png')
 end
 assets[index]={id=id,layers=ls,rendered=full,spec=spec}
 manifest.families[index]={id=id,layers=names,tag=spec.tag,frames=1,accent=paletteHex[index==1 and 6 or index==2 and 9 or 7]}
end
write('layout.json',{canvas={w=304,h=168},coordinateOrigin='top-left',clearTextAreas=areas,components=components,pivot={x=.5,y=.5},native480x270Placement={x=88,y=51},sharedBackdropNineSlice={left=24,right=24,top=24,bottom=24},import={filter='Point',compression='None',mipmaps=false,alphaIsTransparency=true},notes='Choose either a full composite or the separate parts. Never stack both. Only SharedBackdrop supports nine-slice; re-anchor other parts when resizing. Unity owns localization, clicks, fade and scale animation.'})

-- Example text exists ONLY in review images, not source files or exported components.
local function textPreview(a)
 local im=image(304,168);im:drawImage(a.rendered,Point(0,0));local spec=a.spec
 H.text(im,spec.title,80,30,C[8],3);H.text(im,spec.sub,80,56,C[6],2)
 H.text(im,spec.region,36,98,spec.accent,2);H.text(im,spec.detail,128,96,C[6],2)
 H.text(im,spec.footer,26,140,C[6],2);H.text(im,spec.button,184,140,C[8],2)
 return im
end
local sheet=image(976,490);H.rect(sheet,0,0,976,490,C[1]);H.text(sheet,'VOID SCRAPPER / REGION TRANSITION',16,16,C[8],3)
for i,a in ipairs(assets)do local x=16+(i-1)*320;H.text(sheet,a.spec.label,x,50,C[6],2);sheet:drawImage(a.rendered,Point(x,72));sheet:drawImage(textPreview(a),Point(x,290))end
H.text(sheet,'EXAMPLE TEXT / UNITY OVERLAY / NOT BAKED INTO ART',16,264,C[7],2)
H.text(sheet,'STATIC SOURCES / SCALE AND FADE ARE OWNED BY UNITY',16,476,C[6],1)
sheet:saveAs(out..'/RegionTransitionModal_Comparison.png')
for i,a in ipairs(assets)do
 local bg=Image{fromFile=src..(i==3 and '/background_b.png'or'/background_a.png')};assert(bg.width==480 and bg.height==270)
 local im=image(480,270);for p in bg:pixels()do local c=p();im:drawPixel(p.x,p.y,pc.rgba(math.floor(pc.rgbaR(c)*.55),math.floor(pc.rgbaG(c)*.55),math.floor(pc.rgbaB(c)*.55),255))end
 im:drawImage(textPreview(a),Point(88,51));H.text(im,'NATIVE ART MOCKUP / TEXT IS A UNITY OVERLAY',12,254,C[6],1)
 im:saveAs(out..'/RegionTransitionModal_'..a.spec.id..'_480x270.png')
end

local report={passed=true,assets={},pixelComparisons=0,staticOnly=true,bakedText=false,previewIsUnityCapture=false}
local allowed={};for _,p in ipairs(C)do allowed[p]=true end
local function same(a,b)local different=0;for p in a:pixels()do if p()~=b:getPixel(p.x,p.y)then different=different+1 end end;return different==0 end
for i,a in ipairs(assets)do
 local s=assert(app.open(out..'/'..a.id..'.aseprite'));assert(s.width==304 and s.height==168 and #s.frames==1 and #s.layers==8 and #s.tags==1)
 assert(s.tags[1].name==a.spec.tag);local rendered=render(s);local png=Image{fromFile=out..'/'..a.id..'.png'};assert(png.width==304 and png.height==168)
 local colors,visible,clear={},0,0
 for p in rendered:pixels()do local c=p();local alpha=pc.rgbaA(c);assert(alpha==0 or alpha==255,'Soft alpha');assert(c==png:getPixel(p.x,p.y),'ASE/PNG mismatch');report.pixelComparisons=report.pixelComparisons+1
  if alpha>0 then assert(allowed[c]);colors[c]=true;visible=visible+1 else clear=clear+1 end
  assert(c==rendered:getPixel(p.x-p.x%2,p.y-p.y%2),'Non-integer construction grid')
  if p.x==0 or p.y==0 or p.x==303 or p.y==167 then assert(alpha==0,'Transparent outer margin missing')end
 end
 local colorCount=0;for _ in pairs(colors)do colorCount=colorCount+1 end;assert(colorCount<=11 and clear>1500 and visible>40000)
 for n,layer in ipairs(s.layers)do assert(layer.name==names[n]and layer.opacity==255 and layer.isVisible)end
 -- Common chassis and layout must remain byte-identical across the three variants.
 for _,n in ipairs({1,2,3,5})do assert(same(a.layers[n],assets[1].layers[n]),'Shared frame geometry changed')end
 for _,key in ipairs({'title','subtitle','regionLabel','bodyDetail','footerHint','continueLabel'})do local r=areas[key];local value=rendered:getPixel(r.x,r.y)
  for y=r.y,r.y+r.h-1 do for x=r.x,r.x+r.w-1 do assert(rendered:getPixel(x,y)==value,'Text safe area obstructed: '..key)end end
 end
 -- Rebuild every composite solely from the exported parts.
 local parts={Image{fromFile=out..'/UI_RegionTransition_SharedBackdrop.png'},Image{fromFile=out..'/UI_RegionTransition_SharedLayout.png'},Image{fromFile=out..'/'..a.id..'_Energy.png'}}
 local rebuilt=composite(parts)
 if i>1 then rebuilt:drawImage(Image{fromFile=out..'/'..a.id..'_Symbol.png'},Point(areas.symbol.x,areas.symbol.y))end
 rebuilt:drawImage(Image{fromFile=out..'/'..a.id..'_RegionTag.png'},Point(components.regionTag.x,components.regionTag.y))
 rebuilt:drawImage(Image{fromFile=out..'/'..a.id..'_Continue.png'},Point(components.button.x,components.button.y));assert(same(rebuilt,rendered),'Component assembly differs')
 if i==1 then assert(count(a.layers[6])==0)else assert(count(a.layers[6])>80 and count(a.layers[6])<300)end
 local preview=Image{fromFile=out..'/RegionTransitionModal_'..a.spec.id..'_480x270.png'};assert(preview.width==480 and preview.height==270)
 -- Sample preview copy must fit its declared localization windows.
 for _,v in ipairs({{a.spec.title,areas.title,3},{a.spec.sub,areas.subtitle,2},{a.spec.region,areas.regionLabel,2},{a.spec.detail,areas.bodyDetail,2},{a.spec.footer,areas.footerHint,2},{a.spec.button,areas.continueLabel,2}})do assert(#v[1]*4*v[3]-v[3]<=v[2].w and 5*v[3]<=v[2].h,'Preview text overflow')end
 s:close();report.assets[#report.assets+1]={id=a.id,layers=8,colors=colorCount,visiblePixels=visible,transparentPixels=clear,textAreasClear=true,componentsReassembleExactly=true}
end
assert(not same(assets[2].layers[6],assets[3].layers[6]),'State icons should be distinct')
local contact=Image{fromFile=out..'/RegionTransitionModal_Comparison.png'};for i,a in ipairs(assets)do local x=16+(i-1)*320
 for p in a.rendered:pixels()do if pc.rgbaA(p())>0 then assert(p()==contact:getPixel(x+p.x,72+p.y),'Comparison artwork was resized')end end
end
manifest.palette=paletteHex;manifest.layout='layout.json';manifest.previews={comparison='RegionTransitionModal_Comparison.png',safeReturn='RegionTransitionModal_SafeReturn_480x270.png',nextRegion='RegionTransitionModal_NextRegion_480x270.png'}
write('manifest.json',manifest);write('validation.json',report)
