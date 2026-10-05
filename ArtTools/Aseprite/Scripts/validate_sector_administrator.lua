local root=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,approvedCorePixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local allowed={};for _,list in pairs(manifest.palette) do for _,h in ipairs(list) do allowed[pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)]=true end end
local master=assert(app.open(root..'/'..manifest.master));local sheet=assert(Image{fromFile=root..'/'..manifest.sheet})
local source=assert(Image{fromFile=sourceDir..'/system_support_green.png'})
check(master.width==128 and master.height==128 and #master.frames==3 and #master.layers==10,'Master dimensions/frames/layers')
check(#master.tags==3,'Master state tags')
check(sheet.width==384 and sheet.height==128,'Sprite sheet dimensions')
local flats={}
for index,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local im=Image(s);local png=assert(Image{fromFile=root..'/'..a.png})
 local masterFrame=Image(128,128,ColorMode.RGB);masterFrame:drawSprite(master,index,Point(0,0));flats[a.id]=im
 check(s.width==128 and s.height==128 and png.width==128 and png.height==128,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==10,'Frames and layers: '..a.id)
 check(master.tags[index].name==a.id and master.tags[index].fromFrame.frameNumber==index and master.tags[index].toFrame.frameNumber==index,'State tag: '..a.id)
 for i,name in ipairs(manifest.layers) do check(s.layers[i].name==name and master.layers[i].name==name,'Layer names: '..a.id) end
 local colors,count,visible,green={},0,0,0;local minX,minY,maxX,maxY=128,128,-1,-1
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
   if pc.rgbaG(p)>pc.rgbaR(p)*1.15 and pc.rgbaG(p)>pc.rgbaB(p)*1.1 then green=green+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<124 and it.y<124,'Canvas margin: '..a.id)
  end
 end
 for y=0,126,2 do for x=0,126,2 do local p=im:getPixel(x,y)
  check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..a.id)
 end end
 -- Negative-space channels above and below the two connector arms remain clear.
 for _,r in ipairs({{36,30,6,10},{36,58,2,14},{36,94,6,12}}) do
  for y=r[2],r[2]+r[4]-1 do for x=r[1],r[1]+r[3]-1 do
   check(pc.rgbaA(im:getPixel(x,y))==0 and pc.rgbaA(im:getPixel(127-x,y))==0,'Module separation: '..a.id)
  end end
 end
 check(count<=13,'Palette limit: '..a.id);check(visible>3500 and visible<10000,'Unexpected occupied area: '..a.id)
 report.assets[#report.assets+1]={id=a.id,width=128,height=128,visibleColors=count,opaquePixels=visible,greenPixels=green,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},bilateralSymmetry=true,binaryAlpha=true,layers=10}
 s:close()
end
-- The idle state's diamond center retains approved source pixels verbatim.
for y=10,21 do for x=10,21 do if math.abs(x-15.5)+math.abs(y-15.5)<=6 then
 for yy=0,1 do for xx=0,1 do
  check(equal(flats.idle:getPixel((26+x-10)*2+xx,(26+y-10)*2+yy),source:getPixel(x*2+xx,y*2+yy)),'Approved idle core pixel')
  report.approvedCorePixelComparisons=report.approvedCorePixelComparisons+1
 end end
end end end
report.stateDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local differences=0
 for y=0,127 do for x=0,127 do if not equal(flats[a]:getPixel(x,y),flats[b]:getPixel(x,y)) then differences=differences+1 end end end
 check(differences>=300,'States not visibly distinct: '..a..' / '..b)
 report.stateDifferences[#report.stateDifferences+1]={first=a,second=b,differingPixels=differences}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==700,'Comparison preview')
master:close();report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Boss validation failed; read validation.json')
print('SECTOR_ADMINISTRATOR_VALIDATED')
