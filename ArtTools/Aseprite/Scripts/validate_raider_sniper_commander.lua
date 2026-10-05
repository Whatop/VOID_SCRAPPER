local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local allowed,red,blue,lightMetal={},{},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.factionRed) do red[color(v)]=true end
for _,v in ipairs(manifest.palette.precisionBlue) do blue[color(v)]=true end
for _,i in ipairs({5,6,7}) do lightMetal[color(manifest.palette.metal[i])]=true end
local master=assert(app.open(root..'/'..manifest.master));local sheet=assert(Image{fromFile=root..'/'..manifest.sheet})
check(master.width==128 and master.height==128 and #master.frames==3 and #master.layers==11,'Master dimensions/frames/layers')
check(#master.tags==3,'Master tags');check(sheet.width==384 and sheet.height==128,'Sprite sheet dimensions')
local flats,metrics={},{}
for index,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local im=Image(s);local png=assert(Image{fromFile=root..'/'..a.png})
 local masterFrame=Image(128,128,ColorMode.RGB);masterFrame:drawSprite(master,index,Point(0,0));flats[a.id]=im
 check(s.width==128 and s.height==128 and png.width==128 and png.height==128,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==11,'Frames and layers: '..a.id)
 check(master.tags[index].name==a.id and master.tags[index].fromFrame.frameNumber==index and master.tags[index].toFrame.frameNumber==index,'Tag: '..a.id)
 for i,name in ipairs(manifest.layers) do check(s.layers[i].name==name and master.layers[i].name==name,'Layer names: '..a.id) end
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=128,128,-1,-1
 local asymmetric,redCount,blueCount,forwardBlue,armorLight,sensorCount,utilityCount=0,0,0,0,0,0,0
 for it in im:pixels() do
  local p=it();local alpha=pc.rgbaA(p)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check(equal(p,png:getPixel(it.x,it.y)),'PNG mismatch: '..a.id)
  check(equal(p,masterFrame:getPixel(it.x,it.y)),'Master mismatch: '..a.id)
  check(equal(p,sheet:getPixel((index-1)*128+it.x,it.y)),'Sheet mismatch: '..a.id)
  if not equal(p,im:getPixel(127-it.x,it.y)) then asymmetric=asymmetric+1 end
  report.pixelComparisons=report.pixelComparisons+3
  if alpha>0 then
   check(allowed[p],'Color outside approved Raider palette: '..a.id);visible=visible+1
   if not colors[p] then colors[p]=true;count=count+1 end
   if red[p] then redCount=redCount+1 end
   if blue[p] then blueCount=blueCount+1;if it.y<68 then forwardBlue=forwardBlue+1 end end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<124 and it.y<124,'Canvas margin: '..a.id)
   if it.x>=68 and it.x<84 and it.y>=74 and it.y<108 and lightMetal[p] then armorLight=armorLight+1 end
   if it.x>=32 and it.x<52 and it.y>=34 and it.y<58 then sensorCount=sensorCount+1 end
   if it.x>=84 and it.x<100 and it.y>=76 and it.y<100 then utilityCount=utilityCount+1 end
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 check(count<=13,'Palette limit: '..a.id);check(visible>2800,'Hunter too sparse: '..a.id)
 check(maxX-minX+1<=80 and maxY-minY+1>=110,'Long narrow silhouette: '..a.id)
 check(asymmetric>1000,'Mismatched Raider construction not visible: '..a.id)
 check(redCount>=100,'Red faction markings insufficient: '..a.id)
 metrics[a.id]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,redFactionPixels=redCount,precisionBluePixels=blueCount,forwardBluePixels=forwardBlue,starboardArmorLightPixels=armorLight,sensorOpaquePixels=sensorCount,utilityOpaquePixels=utilityCount,asymmetricPixels=asymmetric,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true,layers=11}
 report.assets[#report.assets+1]=metrics[a.id];s:close()
end
check(metrics.target_lock.precisionBluePixels>metrics.idle.precisionBluePixels*3 and metrics.target_lock.forwardBluePixels>=240,'Charge must concentrate blue energy along the cannon and optics')
check(metrics.critical_damage.starboardArmorLightPixels<metrics.idle.starboardArmorLightPixels*0.5,'Critical damage must remove hull armor')
check(metrics.critical_damage.sensorOpaquePixels<metrics.idle.sensorOpaquePixels*0.75,'Critical damage must break the sensor silhouette')
check(metrics.critical_damage.utilityOpaquePixels<metrics.idle.utilityOpaquePixels*0.75,'Critical damage must break the utility pod silhouette')
local function layerImage(layer,frame)
 local out=Image(128,128,ColorMode.RGB);local cel=layer:cel(frame);if cel then out:drawImage(cel.image,cel.position) end;return out
end
report.modulePoseChanges={}
for _,spec in ipairs({{layer=3,frame=2,min=80},{layer=3,frame=3,min=40},{layer=4,frame=3,min=100},{layer=6,frame=3,min=80},{layer=7,frame=3,min=100}}) do
 local a=layerImage(master.layers[spec.layer],1);local b=layerImage(master.layers[spec.layer],spec.frame);local mask,diff=0,0
 for y=0,127 do for x=0,127 do local p,q=a:getPixel(x,y),b:getPixel(x,y);if not equal(p,q) then diff=diff+1 end;if (pc.rgbaA(p)>0)~=(pc.rgbaA(q)>0) then mask=mask+1 end end end
 check(mask>=spec.min,'Module must change geometry: '..master.layers[spec.layer].name..' frame '..spec.frame)
 report.modulePoseChanges[#report.modulePoseChanges+1]={layer=master.layers[spec.layer].name,frame=spec.frame,differentPixels=diff,silhouetteDifferentPixels=mask}
end
report.priorBossSilhouetteComparisons={}
for _,file in ipairs({'raider_assault_commander_idle.png','raider_salvage_carrier_idle.png'}) do
 local prior=assert(Image{fromFile=sourceDir..'/'..file});local diff=0
 for y=0,127 do for x=0,127 do if (pc.rgbaA(prior:getPixel(x,y))>0)~=(pc.rgbaA(flats.idle:getPixel(x,y))>0) then diff=diff+1 end end end
 check(diff>3500,'Hunter silhouette too similar to '..file);report.priorBossSilhouetteComparisons[#report.priorBossSilhouetteComparisons+1]={reference=file,differentOpaqueMaskPixels=diff}
end
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local diff,smallDiff=0,0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
 check(diff>=600,'State difference: '..a..' / '..b)
 if a=='target_lock' and b=='critical_damage' then check(smallDiff>=250,'Charged/damaged difference at 64px') end
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=diff,differingPixelsAt64px=smallDiff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==792,'Preview dimensions')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Raider Sniper Commander validation failed; read validation.json')
print('RAIDER_SNIPER_COMMANDER_VALIDATED')
