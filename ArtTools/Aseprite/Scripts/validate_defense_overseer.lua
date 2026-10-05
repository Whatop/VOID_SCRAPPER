local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local allowed,orange,lightArmor={},{},{}
for _,list in pairs(manifest.palette) do for _,v in ipairs(list) do allowed[color(v)]=true end end
for _,v in ipairs(manifest.palette.energy) do orange[color(v)]=true end
for _,i in ipairs({5,6,7}) do lightArmor[color(manifest.palette.metal[i])]=true end
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
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=128,128,-1,-1;local centralOrange,centralArmor=0,0
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
   if it.x>=40 and it.x<88 and it.y>=34 and it.y<92 then
    if orange[p] then centralOrange=centralOrange+1 end
    if lightArmor[p] then centralArmor=centralArmor+1 end
   end
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 check(count<=13,'Palette limit: '..a.id);check(visible>7500,'Unexpectedly thin body: '..a.id)
 check((maxX-minX+1)/(maxY-minY+1)>=1.35,'Broad horizontal silhouette: '..a.id)
 -- The mid-deck is continuously occupied across the whole platform, not four pods.
 local fullWidthRows=0
 for y=60,79 do local row=0;for x=4,123 do if pc.rgbaA(im:getPixel(x,y))>0 then row=row+1 end end;if row>=112 then fullWidthRows=fullWidthRows+1 end end
 check(fullWidthRows>=12,'Connected broad deck: '..a.id)
 metrics[a.id]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,centralOrangePixels=centralOrange,centralArmorPixels=centralArmor,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},fullWidthDeckRows=fullWidthRows,binaryAlpha=true,bilateralSymmetry=true,layers=10}
 report.assets[#report.assets+1]=metrics[a.id];s:close()
end
-- Breach must remove substantial armor, not merely alter glow or intensity.
check(metrics.armor_broken.centralArmorPixels<metrics.barrage_charged.centralArmorPixels*0.4,'Broken state did not remove enough armor')
check(metrics.armor_broken.centralOrangePixels>metrics.barrage_charged.centralOrangePixels*2.5,'Exposed reactor not distinct from charged')
local prior=assert(Image{fromFile=sourceDir..'/sector_administrator_idle.png'})
local silhouetteDifference,priorDeck,newDeck=0,0,0
for y=0,127 do for x=0,127 do
 local a=pc.rgbaA(prior:getPixel(x,y))>0;local b=pc.rgbaA(flats.idle:getPixel(x,y))>0
 if a~=b then silhouetteDifference=silhouetteDifference+1 end
 if y>=60 and y<80 then if a then priorDeck=priorDeck+1 end;if b then newDeck=newDeck+1 end end
end end
check(silhouetteDifference>4000,'Silhouette is too similar to Region A')
check(newDeck>priorDeck*1.5,'Horizontal mass is not broader than Region A')
report.regionAComparison={silhouetteDifferentPixels=silhouetteDifference,regionAMidDeckPixels=priorDeck,regionBMidDeckPixels=newDeck}
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local diff,smallDiff=0,0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then smallDiff=smallDiff+1 end end end end
 check(diff>=300,'State difference: '..a..' / '..b)
 if a=='barrage_charged' and b=='armor_broken' then check(smallDiff>=300,'Charged/broken difference at 64px') end
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=diff,differingPixelsAt64px=smallDiff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==744,'Preview dimensions')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Defense Overseer validation failed; read validation.json')
print('DEFENSE_OVERSEER_VALIDATED')
