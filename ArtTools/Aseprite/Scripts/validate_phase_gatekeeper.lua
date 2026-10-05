local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,guardPixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local allowed,blue={},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.energy) do blue[color(v)]=true end
local master=assert(app.open(root..'/'..manifest.master));local sheet=assert(Image{fromFile=root..'/'..manifest.sheet})
check(master.width==128 and master.height==128 and #master.frames==3 and #master.layers==10,'Master dimensions/frames/layers')
check(#master.tags==3,'Master tags');check(sheet.width==384 and sheet.height==128,'Sprite sheet dimensions')
local flats,metrics={},{}
for index,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local im=Image(s);local png=assert(Image{fromFile=root..'/'..a.png})
 local masterFrame=Image(128,128,ColorMode.RGB);masterFrame:drawSprite(master,index,Point(0,0));flats[a.id]=im
 check(s.width==128 and s.height==128 and png.width==128 and png.height==128,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==10,'Frames and layers: '..a.id)
 check(master.tags[index].name==a.id and master.tags[index].fromFrame.frameNumber==index and master.tags[index].toFrame.frameNumber==index,'Tag: '..a.id)
 for i,name in ipairs(manifest.layers) do check(s.layers[i].name==name and master.layers[i].name==name,'Layer names: '..a.id) end
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=128,128,-1,-1;local lensBlue,externalBlue=0,0
 for it in im:pixels() do
  local p=it();local alpha=pc.rgbaA(p)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check(equal(p,png:getPixel(it.x,it.y)),'PNG mismatch: '..a.id)
  check(equal(p,masterFrame:getPixel(it.x,it.y)),'Master mismatch: '..a.id)
  check(equal(p,sheet:getPixel((index-1)*128+it.x,it.y)),'Sheet mismatch: '..a.id)
  check(equal(p,im:getPixel(127-it.x,it.y)),'Bilateral symmetry: '..a.id)
  report.pixelComparisons=report.pixelComparisons+3
  if alpha>0 then
   check(allowed[p],'Unapproved color: '..a.id);visible=visible+1
   if not colors[p] then colors[p]=true;count=count+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<124 and it.y<124,'Canvas margin: '..a.id)
   if blue[p] then
    if it.x>=46 and it.x<82 and it.y>=46 and it.y<86 then lensBlue=lensBlue+1 end
    if it.x<42 or it.x>=86 then externalBlue=externalBlue+1 end
   end
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 check(count<=13,'Palette limit: '..a.id);check(visible>2500 and visible<6500,'Light spear body mass: '..a.id)
 check((maxY-minY+1)/(maxX-minX+1)>=1.2,'Tall narrow silhouette: '..a.id)
 metrics[a.id]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,centralLensBluePixels=lensBlue,externalBluePixels=externalBlue,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true,bilateralSymmetry=true,layers=10}
 report.assets[#report.assets+1]=metrics[a.id];s:close()
end
check(metrics.lens_exposed.centralLensBluePixels>metrics.phase_lock.centralLensBluePixels*1.6,'Lens aperture not visibly larger when exposed')
check(metrics.phase_lock.externalBluePixels>metrics.idle.externalBluePixels*4,'Phase emitters insufficiently active')
check(metrics.lens_exposed.externalBluePixels<metrics.phase_lock.externalBluePixels*0.2,'Exposed state external effects should be dimmed')
-- Verify a real translation of both whole guard pieces, not a change of color.
local function layerImage(layer,frame)
 local out=Image(128,128,ColorMode.RGB);local cel=layer:cel(frame);if cel then out:drawImage(cel.image,cel.position) end;return out
end
for _,spec in ipairs({{index=6,shift=-6},{index=7,shift=6}}) do
 local idle=layerImage(master.layers[spec.index],1);local locked=layerImage(master.layers[spec.index],2);local exposed=layerImage(master.layers[spec.index],3)
 for y=0,127 do for x=0,127 do
  check(equal(idle:getPixel(x,y),locked:getPixel(x,y)),'Phase Lock guard should remain closed')
  local oldX=x-spec.shift;local expected=oldX>=0 and oldX<128 and idle:getPixel(oldX,y) or 0
  check(equal(expected,exposed:getPixel(x,y)),'Exposed guard translation')
  report.guardPixelComparisons=report.guardPixelComparisons+2
 end end
end
report.regionComparisons={}
for _,ref in ipairs({{id='Region A',file='sector_administrator_idle.png'},{id='Region B',file='defense_overseer_idle.png'}}) do
 local prior=assert(Image{fromFile=sourceDir..'/'..ref.file});local differences,priorOpaque=0,0
 for y=0,127 do for x=0,127 do local a=pc.rgbaA(prior:getPixel(x,y))>0;local b=pc.rgbaA(flats.idle:getPixel(x,y))>0
  if a~=b then differences=differences+1 end;if a then priorOpaque=priorOpaque+1 end
 end end
 check(differences>4000,'Silhouette too similar to '..ref.id)
 if ref.id=='Region B' then check(metrics.idle.opaquePixels<priorOpaque*0.8,'Not lighter than Region B') end
 report.regionComparisons[#report.regionComparisons+1]={reference=ref.id,silhouetteDifferentPixels=differences,referenceOpaquePixels=priorOpaque}
end
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local diff,smallDiff=0,0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
 check(diff>=300,'State difference: '..a..' / '..b)
 if a=='phase_lock' and b=='lens_exposed' then check(smallDiff>=300,'Phase/exposed difference at 64px') end
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=diff,differingPixelsAt64px=smallDiff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==792,'Preview dimensions')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Phase Gatekeeper validation failed; read validation.json')
print('PHASE_GATEKEEPER_VALIDATED')
