local root=assert(app.params.outputDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,structures={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local allowed,red,heat,lightMetal={},{},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.factionRed) do red[color(v)]=true end
for _,v in ipairs(manifest.palette.industrialHeatAndLoot) do heat[color(v)]=true end
for _,i in ipairs({5,6,7}) do lightMetal[color(manifest.palette.metal[i])]=true end
local flats={}
local expectedSizes={raider_outpost=96,raider_turret=64,raider_power_node=64,raider_storage=96}
check(#manifest.structures==4,'Expected four structures')
local function layerImage(layer,frame,size)
 local out=Image(size,size,ColorMode.RGB);local cel=layer:cel(frame);if cel then out:drawImage(cel.image,cel.position) end;return out
end
for _,unit in ipairs(manifest.structures) do
 local size=assert(expectedSizes[unit.id]);local master=assert(app.open(root..'/'..unit.master));local sheet=assert(Image{fromFile=root..'/'..unit.sheet})
 check(master.width==size and master.height==size and #master.frames==3 and #master.layers==9,'Master size/frames/layers: '..unit.id)
 check(#master.tags==3,'Master tags: '..unit.id);check(sheet.width==size*3 and sheet.height==size,'Sheet dimensions: '..unit.id)
 for i,name in ipairs(unit.layers) do check(master.layers[i].name==name,'Layer names: '..unit.id) end
 local entry={id=unit.id,width=size,height=size,layers=9,states={},differences={}};local metrics={};flats[unit.id]={}
 for index,a in ipairs(unit.assets) do
  local im=Image(size,size,ColorMode.RGB);im:drawSprite(master,index,Point(0,0));flats[unit.id][a.id]=im
  local png=assert(Image{fromFile=root..'/'..a.png})
  check(png.width==size and png.height==size,'PNG size: '..unit.id..' '..a.id)
  check(master.tags[index].name==a.id and master.tags[index].fromFrame.frameNumber==index and master.tags[index].toFrame.frameNumber==index,'Tag: '..unit.id..' '..a.id)
  local colors,count,visible={},0,0;local minX,minY,maxX,maxY=size,size,-1,-1;local redCount,heatCount,metalCount,asym=0,0,0,0
  for it in im:pixels() do
   local p=it();local alpha=pc.rgbaA(p)
   check(alpha==0 or alpha==255,'Partial alpha: '..unit.id..' '..a.id)
   check(equal(p,png:getPixel(it.x,it.y)),'PNG mismatch: '..unit.id..' '..a.id)
   check(equal(p,sheet:getPixel((index-1)*size+it.x,it.y)),'Sheet mismatch: '..unit.id..' '..a.id)
   report.pixelComparisons=report.pixelComparisons+2
   if not equal(p,im:getPixel(size-1-it.x,it.y)) then asym=asym+1 end
   if alpha>0 then
    check(allowed[p],'Unapproved palette color: '..unit.id..' '..a.id);visible=visible+1
    if not colors[p] then colors[p]=true;count=count+1 end
    if red[p] then redCount=redCount+1 end;if heat[p] then heatCount=heatCount+1 end;if lightMetal[p] then metalCount=metalCount+1 end
    minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
    check(it.x>=4 and it.y>=4 and it.x<size-2 and it.y<size-2,'Canvas margin: '..unit.id..' '..a.id)
   end
  end
  for y=0,size-2,2 do for x=0,size-2,2 do local p=im:getPixel(x,y)
   check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel grid: '..unit.id..' '..a.id)
  end end
  check(count<=13,'Palette limit: '..unit.id..' '..a.id);check(visible>size*size*0.25,'Structure too sparse: '..unit.id..' '..a.id)
  check(redCount>=size,'Raider markings missing: '..unit.id..' '..a.id);check(asym>=size*8,'Mismatched construction missing: '..unit.id..' '..a.id)
  local result={id=a.id,visibleColors=count,opaquePixels=visible,redFactionPixels=redCount,industrialHeatOrLootPixels=heatCount,lightMetalPixels=metalCount,asymmetricPixels=asym,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true}
  entry.states[#entry.states+1]=result;metrics[a.id]=result
 end
 check(metrics.damaged.lightMetalPixels<metrics.normal.lightMetalPixels*0.82,'Damage must remove visible light cladding: '..unit.id)
 if unit.id=='raider_power_node' or unit.id=='raider_storage' then check(metrics.active.industrialHeatOrLootPixels>metrics.normal.industrialHeatOrLootPixels+40,'Active role cue: '..unit.id) end
 -- Compare authored armor geometry so damage cannot pass as a mere palette change.
 local normalArmor=layerImage(master.layers[5],1,size);local damagedArmor=layerImage(master.layers[5],3,size);local removed=0
 for y=0,size-1 do for x=0,size-1 do if pc.rgbaA(normalArmor:getPixel(x,y))>0 and pc.rgbaA(damagedArmor:getPixel(x,y))==0 then removed=removed+1 end end end
 check(removed>=40,'Physical armor damage: '..unit.id);entry.removedArmorLayerPixels=removed
 for i=1,#unit.assets do for j=i+1,#unit.assets do
  local a,b=unit.assets[i].id,unit.assets[j].id;local diff,smallDiff=0,0
  for y=0,size-1 do for x=0,size-1 do if not equal(flats[unit.id][a]:getPixel(x,y),flats[unit.id][b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
  check(diff>=size*2,'State difference too weak: '..unit.id..' '..a..' / '..b)
  if a=='active' and b=='damaged' then check(smallDiff>=size,'Active/damaged difference at half size: '..unit.id) end
  entry.differences[#entry.differences+1]={first=a,second=b,differingPixels=diff,differingPixelsAtHalfSize=smallDiff}
 end end
 report.structures[#report.structures+1]=entry;master:close()
end
-- Pairwise opaque-mask comparison after equal-canvas resampling, excluding color cues.
local normalized={}
for id,unitFlats in pairs(flats) do
 local im=unitFlats.normal;local mask={};for y=0,47 do for x=0,47 do mask[y*48+x+1]=pc.rgbaA(im:getPixel(math.floor(x*im.width/48),math.floor(y*im.height/48)))>0 end end;normalized[id]=mask
end
report.roleSilhouetteDifferences={}
for i=1,#manifest.structures do for j=i+1,#manifest.structures do
 local a,b=manifest.structures[i].id,manifest.structures[j].id;local diff=0
 for p=1,48*48 do if normalized[a][p]~=normalized[b][p] then diff=diff+1 end end
 check(diff>=300,'Role silhouettes too similar: '..a..' / '..b)
 report.roleSilhouetteDifferences[#report.roleSilhouetteDifferences+1]={first=a,second=b,differentOpaqueMaskPixelsAt48px=diff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==1000,'Preview dimensions')
report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Raider Base validation failed; read validation.json')
print('RAIDER_BASE_VALIDATED')
