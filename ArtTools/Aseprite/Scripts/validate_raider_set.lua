local root=assert(app.params.outputDir)
local file=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(file:read('*a'));file:close()
local pc=app.pixelColor
local result={passed=true,assets={},pixelComparisons=0,failures={}}
local function check(ok,message) if not ok then result.failures[#result.failures+1]=message end end
local silhouettes={}
for _,a in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..a.aseprite));local png=assert(Image{fromFile=root..'/'..a.png})
 check(s.width==64 and s.height==64 and png.width==64 and png.height==64,'Size: '..a.id)
 check(#s.frames==1 and #s.layers==4,'Structure: '..a.id)
 for i,name in ipairs(a.layers) do check(s.layers[i].name==name,'Layer name: '..a.id) end
 local flat=Image(s);local colors,n,visible={},0,0;local minX,minY,maxX,maxY=64,64,-1,-1;local mask={}
 for it in flat:pixels() do
  local p=it();local alpha=pc.rgbaA(p);local q=png:getPixel(it.x,it.y)
  check(alpha==0 or alpha==255,'Partial alpha: '..a.id)
  check((alpha==0 and pc.rgbaA(q)==0) or p==q,'PNG mismatch: '..a.id)
  if alpha>0 then
   visible=visible+1;mask[it.y*64+it.x]=true
   if not colors[p] then colors[p]=true;n=n+1 end
   minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
   check(it.x>0 and it.y>0 and it.x<63 and it.y<63,'Clipped border: '..a.id)
  end
  result.pixelComparisons=result.pixelComparisons+1
 end
 check(n<=16,'Too many colors: '..a.id..' '..n);check(visible>0,'Empty image: '..a.id)
 if a.id~='elite' then
  for y=0,62,2 do for x=0,62,2 do
   local p=flat:getPixel(x,y)
   check(p==flat:getPixel(x+1,y) and p==flat:getPixel(x,y+1) and p==flat:getPixel(x+1,y+1),'Non-integer scaling: '..a.id)
  end end
 end
 result.assets[#result.assets+1]={id=a.id,width=64,height=64,visibleColors=n,opaquePixels=visible,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1}}
 silhouettes[a.id]=mask;s:close()
end
for i=1,#manifest.assets do for j=i+1,#manifest.assets do
 local a,b=manifest.assets[i].id,manifest.assets[j].id;local differences=0
 for p=0,4095 do if silhouettes[a][p]~=silhouettes[b][p] then differences=differences+1 end end
 check(differences>=80,'Silhouettes too similar: '..a..' and '..b)
end end
check(#result.assets==4,'Expected four units')
result.passed=#result.failures==0
local report=assert(io.open(root..'/validation.json','w'));report:write(json.encode(result));report:close()
assert(result.passed,'Validation failed; read validation.json')
print('RAIDER_SET_VALIDATED')
