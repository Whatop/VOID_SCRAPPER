local root=assert(app.params.outputDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,assets={},pixelComparisons=0,failures={}}
local function check(ok,message) if not ok then report.failures[#report.failures+1]=message end end
local masks={}
for _,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local png=assert(Image{fromFile=root..'/'..a.png});local im=Image(s)
 check(s.width==64 and s.height==64 and png.width==64 and png.height==64,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==4,'Structure: '..a.id)
 for i,name in ipairs(a.layers) do check(s.layers[i].name==name,'Layer names: '..a.id) end
 local colors,count,visible={},0,0;local minX,minY,maxX,maxY=64,64,-1,-1;local mask={}
 for it in im:pixels() do
  local p=it();local alpha=pc.rgbaA(p);local q=png:getPixel(it.x,it.y)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check((alpha==0 and pc.rgbaA(q)==0) or p==q,'PNG mismatch: '..a.id)
  local mirrored=im:getPixel(63-it.x,it.y)
  check((alpha==0 and pc.rgbaA(mirrored)==0) or p==mirrored,'Bilateral symmetry: '..a.id)
  if alpha>0 then
   visible=visible+1;mask[it.y*64+it.x]=true
   if not colors[p] then colors[p]=true;count=count+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>=4 and it.y>=4 and it.x<=59 and it.y<=59,'Insufficient canvas margin: '..a.id)
  end
  report.pixelComparisons=report.pixelComparisons+1
 end
 for y=0,62,2 do for x=0,62,2 do local p=im:getPixel(x,y)
  check(p==im:getPixel(x+1,y) and p==im:getPixel(x,y+1) and p==im:getPixel(x+1,y+1),'Pixel scale: '..a.id)
 end end
 check(count<=13,'Palette exceeds 13 colors: '..a.id);check(visible>0 and visible<2200,'Support-unit footprint: '..a.id)
 report.assets[#report.assets+1]={id=a.id,width=64,height=64,visibleColors=count,opaquePixels=visible,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},bilateralSymmetry=true}
 masks[a.id]=mask;s:close()
end
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local differences=0
 for p=0,4095 do if masks[a][p]~=masks[b][p] then differences=differences+1 end end
 check(differences>=80,'Silhouettes too similar: '..a..' and '..b)
end end
check(#report.assets==3,'Expected 3 units');report.passed=#report.failures==0
local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'System support validation failed; read validation.json')
print('SYSTEM_SUPPORT_VALIDATED')
