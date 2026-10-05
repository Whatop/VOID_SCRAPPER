local root=assert(app.params.outputDir)
local srcDir=assert(app.params.sourceDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,approvedCorePixelComparisons=0,failures={}}
local function check(ok,message) if not ok then report.failures[#report.failures+1]=message end end
local masks={}
for _,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local png=assert(Image{fromFile=root..'/'..a.png});local im=Image(s)
 local approved=assert(Image{fromFile=srcDir..'/system_support_'..a.family..'.png'})
 check(s.width==a.width and s.height==a.height and png.width==a.width and png.height==a.height,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==5,'Structure: '..a.id)
 for i,name in ipairs(a.layers) do check(s.layers[i].name==name,'Layer names: '..a.id) end
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=s.width,s.height,-1,-1;local symmetric=true
 for it in im:pixels() do
  local p=it();local alpha=pc.rgbaA(p);local q=png:getPixel(it.x,it.y)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check((alpha==0 and pc.rgbaA(q)==0) or p==q,'PNG mismatch: '..a.id)
  local mirrored=im:getPixel(s.width-1-it.x,it.y)
  local same=(alpha==0 and pc.rgbaA(mirrored)==0) or p==mirrored
  if not same then symmetric=false end
  check(same,'Bilateral symmetry: '..a.id)
  if alpha>0 then
   visible=visible+1;if not colors[p] then colors[p]=true;count=count+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<s.width-4 and it.y<s.height-4,'Canvas margin: '..a.id)
  end
  report.pixelComparisons=report.pixelComparisons+1
 end
 for y=0,s.height-2,2 do for x=0,s.width-2,2 do local p=im:getPixel(x,y)
  check(p==im:getPixel(x+1,y) and p==im:getPixel(x,y+1) and p==im:getPixel(x+1,y+1),'Pixel scale: '..a.id)
 end end
 for y=10,21 do for x=10,21 do if math.abs(x-15.5)+math.abs(y-15.5)<=6 then
  for yy=0,1 do for xx=0,1 do
   local expected=approved:getPixel(x*2+xx,y*2+yy)
   check(im:getPixel(a.coreOffset+(x-10)*2+xx,a.coreOffset+(y-10)*2+yy)==expected,'Approved core changed: '..a.id)
   report.approvedCorePixelComparisons=report.approvedCorePixelComparisons+1
  end end
 end end end
 check(count<=13,'Palette exceeds 13 colors: '..a.id);check(visible>0,'Empty structure: '..a.id)
 -- Compare normalized silhouette occupancy across the differing canvas sizes.
 local mask={};for y=0,47 do for x=0,47 do mask[y*48+x]=pc.rgbaA(im:getPixel(math.floor(x*s.width/48),math.floor(y*s.height/48)))>0 end end
 masks[a.id]=mask
 report.assets[#report.assets+1]={id=a.id,width=s.width,height=s.height,visibleColors=count,opaquePixels=visible,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},bilateralSymmetry=symmetric}
 s:close()
end
report.silhouetteDifferences={}
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local differences=0
 for p=0,2303 do if masks[a][p]~=masks[b][p] then differences=differences+1 end end
 check(differences>=120,'Silhouettes too similar: '..a..' / '..b)
 report.silhouetteDifferences[#report.silhouetteDifferences+1]={first=a,second=b,differingCells=differences,comparisonGrid='48x48'}
end end
check(#report.assets==3,'Expected 3 structures');report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Structure validation failed; read validation.json')
print('SYSTEM_STRUCTURES_VALIDATED')
