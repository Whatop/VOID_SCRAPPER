local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local allowed,red,heat,lightMetal={},{},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.factionRed) do red[color(v)]=true end
for _,v in ipairs(manifest.palette.industrialHeat) do heat[color(v)]=true end
for _,i in ipairs({5,6,7}) do lightMetal[color(manifest.palette.metal[i])]=true end
local master=assert(app.open(root..'/'..manifest.master));local sheet=assert(Image{fromFile=root..'/'..manifest.sheet})
check(master.width==128 and master.height==128 and #master.frames==3 and #master.layers==12,'Master dimensions/frames/layers')
check(#master.tags==3,'Master tags');check(sheet.width==384 and sheet.height==128,'Sprite sheet dimensions')
local flats,metrics={},{}
for index,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local im=Image(s);local png=assert(Image{fromFile=root..'/'..a.png})
 local masterFrame=Image(128,128,ColorMode.RGB);masterFrame:drawSprite(master,index,Point(0,0));flats[a.id]=im
 check(s.width==128 and s.height==128 and png.width==128 and png.height==128,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==12,'Frames and layers: '..a.id)
 check(master.tags[index].name==a.id and master.tags[index].fromFrame.frameNumber==index and master.tags[index].toFrame.frameNumber==index,'Tag: '..a.id)
 for i,name in ipairs(manifest.layers) do check(s.layers[i].name==name and master.layers[i].name==name,'Layer names: '..a.id) end
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=128,128,-1,-1;local asymmetric,redCount,heatCount,cargoLight,cargoHeat,forward,rear=0,0,0,0,0,0,0
 for it in im:pixels() do
  local p=it();local alpha=pc.rgbaA(p)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check(equal(p,png:getPixel(it.x,it.y)),'PNG mismatch: '..a.id)
  check(equal(p,masterFrame:getPixel(it.x,it.y)),'Master mismatch: '..a.id)
  check(equal(p,sheet:getPixel((index-1)*128+it.x,it.y)),'Sheet mismatch: '..a.id)
  if not equal(p,im:getPixel(127-it.x,it.y)) then asymmetric=asymmetric+1 end
  report.pixelComparisons=report.pixelComparisons+3
  if alpha>0 then
   check(allowed[p],'Color not in approved Raider palette: '..a.id);visible=visible+1
   if not colors[p] then colors[p]=true;count=count+1 end
   if red[p] then redCount=redCount+1 end;if heat[p] then heatCount=heatCount+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<124 and it.y<124,'Canvas margin: '..a.id)
   if it.y>=8 and it.y<56 then forward=forward+1 end;if it.y>=64 and it.y<112 then rear=rear+1 end
   if (it.x>=12 and it.x<40 and it.y>=68 and it.y<108) or (it.x>=86 and it.x<118 and it.y>=62 and it.y<108) then
    if lightMetal[p] then cargoLight=cargoLight+1 end;if heat[p] then cargoHeat=cargoHeat+1 end
   end
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 check(count<=13,'Palette limit: '..a.id);check(visible>6000,'Carrier too sparse: '..a.id)
 check(maxX-minX+1>=108,'Rear cargo should fill a broad silhouette: '..a.id)
 check(asymmetric>1500,'Mismatched Raider construction not visible: '..a.id)
 check(redCount>=150,'Red Raider markings insufficient: '..a.id)
 check(rear>forward*2,'Silhouette not rear-heavy enough: '..a.id)
 metrics[a.id]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,redFactionPixels=redCount,industrialHeatPixels=heatCount,cargoLightArmorPixels=cargoLight,cargoHeatPixels=cargoHeat,forwardOpaquePixels=forward,rearOpaquePixels=rear,asymmetricPixels=asymmetric,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true,layers=12}
 report.assets[#report.assets+1]=metrics[a.id];s:close()
end
check(metrics.cargo_overload.cargoLightArmorPixels<metrics.idle.cargoLightArmorPixels*0.55,'Overload must remove substantial cargo plating')
check(metrics.cargo_overload.cargoHeatPixels>metrics.salvage_active.cargoHeatPixels+40,'Overload must show unstable cargo contents')
local function layerImage(layer,frame)
 local out=Image(128,128,ColorMode.RGB);local cel=layer:cel(frame);if cel then out:drawImage(cel.image,cel.position) end;return out
end
report.modulePoseChanges={}
for _,index in ipairs({5,6,9}) do
 local a=layerImage(master.layers[index],1);local b=layerImage(master.layers[index],2);local mask=0;local diff=0
 for y=0,127 do for x=0,127 do local p,q=a:getPixel(x,y),b:getPixel(x,y);if not equal(p,q) then diff=diff+1 end;if (pc.rgbaA(p)>0)~=(pc.rgbaA(q)>0) then mask=mask+1 end end end
 check(mask>=100,'Salvage module changed color without a clear physical pose: '..master.layers[index].name)
 report.modulePoseChanges[#report.modulePoseChanges+1]={layer=master.layers[index].name,differentPixels=diff,silhouetteDifferentPixels=mask}
end
local commander=assert(Image{fromFile=sourceDir..'/raider_assault_commander_idle.png'});local silhouetteDiff=0
for y=0,127 do for x=0,127 do if (pc.rgbaA(commander:getPixel(x,y))>0)~=(pc.rgbaA(flats.idle:getPixel(x,y))>0) then silhouetteDiff=silhouetteDiff+1 end end end
check(silhouetteDiff>3500,'Carrier silhouette too similar to Assault Commander');report.commanderSilhouetteDifference=silhouetteDiff
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local diff,smallDiff=0,0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
 check(diff>=600,'State difference: '..a..' / '..b)
 if a=='salvage_active' and b=='cargo_overload' then check(smallDiff>=350,'Active/overload difference at 64px') end
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=diff,differingPixelsAt64px=smallDiff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==792,'Preview dimensions')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Raider Salvage Carrier validation failed; read validation.json')
print('RAIDER_SALVAGE_CARRIER_VALIDATED')
