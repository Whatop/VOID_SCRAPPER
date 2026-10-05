local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local allowed,red,heat,lightMetal,darkMetal={},{},{},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.factionRed) do red[color(v)]=true end
for _,v in ipairs(manifest.palette.localHeat) do heat[color(v)]=true end
for _,i in ipairs({5,6,7}) do lightMetal[color(manifest.palette.metal[i])]=true end
for _,i in ipairs({1,2,3}) do darkMetal[color(manifest.palette.metal[i])]=true end
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
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=128,128,-1,-1;local asymmetric,redCount,heatCount,centralLight,centralDark,centralHeat=0,0,0,0,0,0
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
   if it.x>=46 and it.x<84 and it.y>=54 and it.y<90 then
    if lightMetal[p] then centralLight=centralLight+1 end;if darkMetal[p] then centralDark=centralDark+1 end;if heat[p] then centralHeat=centralHeat+1 end
   end
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 check(count<=13,'Palette limit: '..a.id);check(visible>6000,'Boss hull too thin: '..a.id)
 check(maxX-minX+1>=104,'Boss not substantially wider than 64px elite: '..a.id)
 check(asymmetric>1500,'Mismatched Raider construction not visible: '..a.id)
 check(redCount>=180,'Red Raider markings insufficient: '..a.id)
 check(centralHeat<=64,'Central machinery should not become a glowing core: '..a.id)
 metrics[a.id]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,redFactionPixels=redCount,localHeatPixels=heatCount,centralLightArmorPixels=centralLight,centralDarkMachineryPixels=centralDark,centralHeatPixels=centralHeat,asymmetricPixels=asymmetric,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true,layers=10}
 report.assets[#report.assets+1]=metrics[a.id];s:close()
end
check(metrics.critical_damage.centralLightArmorPixels<metrics.idle.centralLightArmorPixels*0.6,'Damage must remove central plating')
check(metrics.critical_damage.centralDarkMachineryPixels>metrics.idle.centralDarkMachineryPixels*1.15,'Damage must expose machinery')
check(metrics.weapons_hot.localHeatPixels>metrics.critical_damage.localHeatPixels*2,'Hot weapons/engines not distinct from damage sparks')
-- Confirm critical armor coverage shrinks in the actual armor layer.
local function layerOpaque(layer,frame)
 local cel=layer:cel(frame);local count=0;if cel then for it in cel.image:pixels() do if pc.rgbaA(it())>0 then count=count+1 end end end;return count
end
local idleArmor=layerOpaque(master.layers[6],1);local damagedArmor=layerOpaque(master.layers[6],3)
check(damagedArmor<idleArmor*0.75,'Critical armor layer remains too intact');report.armorCoverage={idle=idleArmor,critical=damagedArmor}
report.systemSilhouetteComparisons={}
for _,ref in ipairs({{id='Sector Administrator',file='sector_administrator_idle.png'},{id='Defense Overseer',file='defense_overseer_idle.png'},{id='Phase Gatekeeper',file='phase_gatekeeper_idle.png'}}) do
 local prior=assert(Image{fromFile=sourceDir..'/'..ref.file});local differences=0
 for y=0,127 do for x=0,127 do if (pc.rgbaA(prior:getPixel(x,y))>0)~=(pc.rgbaA(flats.idle:getPixel(x,y))>0) then differences=differences+1 end end end
 check(differences>3000,'Silhouette too close to SYSTEM boss: '..ref.id)
 report.systemSilhouetteComparisons[#report.systemSilhouetteComparisons+1]={reference=ref.id,differentPixels=differences}
end
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local diff,smallDiff=0,0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
 check(diff>=300,'State difference: '..a..' / '..b)
 if a=='weapons_hot' and b=='critical_damage' then check(smallDiff>=300,'Hot/critical difference at 64px') end
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=diff,differingPixelsAt64px=smallDiff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==756,'Preview dimensions')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Raider commander validation failed; read validation.json')
print('RAIDER_ASSAULT_COMMANDER_VALIDATED')
