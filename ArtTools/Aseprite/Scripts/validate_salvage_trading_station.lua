local root=assert(app.params.outputDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local allowed,red,teal,cyan,lightMetal={},{},{},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.warningRed) do red[color(v)]=true end
for _,v in ipairs(manifest.palette.utilityTeal) do teal[color(v)]=true end
for _,v in ipairs(manifest.palette.shieldCyan) do cyan[color(v)]=true end
for _,i in ipairs({5,6}) do lightMetal[color(manifest.palette.metal[i])]=true end
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
 local redCount,tealCount,cyanCount,sideCladding=0,0,0,0
 for it in im:pixels() do
  local p=it();local alpha=pc.rgbaA(p)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check(equal(p,png:getPixel(it.x,it.y)),'PNG mismatch: '..a.id)
  check(equal(p,masterFrame:getPixel(it.x,it.y)),'Master mismatch: '..a.id)
  check(equal(p,sheet:getPixel((index-1)*128+it.x,it.y)),'Sheet mismatch: '..a.id)
  report.pixelComparisons=report.pixelComparisons+3
  if alpha>0 then
   check(allowed[p],'Color outside station palette: '..a.id);visible=visible+1
   if not colors[p] then colors[p]=true;count=count+1 end
   if red[p] then redCount=redCount+1 end;if teal[p] then tealCount=tealCount+1 end;if cyan[p] then cyanCount=cyanCount+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<124 and it.y<124,'Canvas margin: '..a.id)
   if it.x>=86 and it.x<100 and it.y>=54 and it.y<80 and lightMetal[p] then sideCladding=sideCladding+1 end
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 for y=90,117 do for x=58,69 do check(pc.rgbaA(im:getPixel(x,y))==0,'Lower docking approach must remain open: '..a.id) end end
 check(count<=15,'Palette limit: '..a.id);check(visible>6000 and visible<11000,'Station density: '..a.id)
 check(tealCount>=200,'Civilian teal identity: '..a.id)
 metrics[a.id]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,warningRedPixels=redCount,utilityTealPixels=tealCount,shieldCyanPixels=cyanCount,starboardLightCladdingPixels=sideCladding,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true,layers=11}
 report.assets[#report.assets+1]=metrics[a.id];s:close()
end
check(metrics.neutral.warningRedPixels==0 and metrics.shield_active.warningRedPixels==0,'Red reserved for hostile state')
check(metrics.hostile_shield_broken.warningRedPixels>=160,'Hostile warning lights must be readable')
check(metrics.neutral.shieldCyanPixels==0,'Neutral state must use calm utility lights')
check(metrics.shield_active.shieldCyanPixels>=500,'Active protective field must be visible')
check(metrics.hostile_shield_broken.shieldCyanPixels<metrics.shield_active.shieldCyanPixels*0.1,'Broken shield must lose its perimeter field')
check(metrics.hostile_shield_broken.starboardLightCladdingPixels<metrics.neutral.starboardLightCladdingPixels*0.6,'Hostile must expose services behind cladding')
local function layerImage(layer,frame)
 local out=Image(128,128,ColorMode.RGB);local cel=layer:cel(frame);if cel then out:drawImage(cel.image,cel.position) end;return out
end
report.shieldPreservedLayers={}
for _,index in ipairs({1,2,4,5,6,8}) do
 local a=layerImage(master.layers[index],1);local b=layerImage(master.layers[index],2);local diff=0
 for y=0,127 do for x=0,127 do if not equal(a:getPixel(x,y),b:getPixel(x,y)) then diff=diff+1 end end end
 check(diff==0,'Shield changed structural layer: '..master.layers[index].name)
 report.shieldPreservedLayers[#report.shieldPreservedLayers+1]=master.layers[index].name
end
local shield=layerImage(master.layers[10],2);local outside,total,lost,hostileOverlap=0,0,0,0
for y=0,127 do for x=0,127 do
 local n=pc.rgbaA(flats.neutral:getPixel(x,y))>0;local s=pc.rgbaA(flats.shield_active:getPixel(x,y))>0;local d=pc.rgbaA(flats.hostile_shield_broken:getPixel(x,y))>0
 if n and not s then lost=lost+1 end;if n and d then hostileOverlap=hostileOverlap+1 end
 if pc.rgbaA(shield:getPixel(x,y))>0 then total=total+1;if not n then outside=outside+1 end end
end end
check(lost==0,'Shield must preserve the underlying station silhouette')
check(total>400 and outside>total*0.85,'Shield field should outline rather than obscure station')
check(hostileOverlap/metrics.neutral.opaquePixels>0.95,'Hostile must remain the same station')
report.shieldField={opaquePixels=total,pixelsOutsideNeutralSilhouette=outside,neutralSilhouettePixelsLost=lost}
report.hostileSilhouetteOverlapFraction=hostileOverlap/metrics.neutral.opaquePixels
report.physicalDamage={}
for _,spec in ipairs({{layer=6,min=60},{layer=7,min=60}}) do
 local a=layerImage(master.layers[spec.layer],1);local b=layerImage(master.layers[spec.layer],3);local removed=0
 for y=0,127 do for x=0,127 do if pc.rgbaA(a:getPixel(x,y))>0 and pc.rgbaA(b:getPixel(x,y))==0 then removed=removed+1 end end end
 check(removed>=spec.min,'Physical damage missing: '..master.layers[spec.layer].name)
 report.physicalDamage[#report.physicalDamage+1]={layer=master.layers[spec.layer].name,removedOpaquePixels=removed}
end
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local diff,smallDiff=0,0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
 check(diff>=600,'State difference: '..a..' / '..b)
 if a=='shield_active' and b=='hostile_shield_broken' then check(smallDiff>=300,'Shield/hostile difference at 64px') end
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=diff,differingPixelsAt64px=smallDiff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==728,'Preview dimensions')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Salvage Trading Station validation failed; read validation.json')
print('SALVAGE_TRADING_STATION_VALIDATED')
