local root=assert(app.params.outputDir)
local f=assert(io.open(root..'/manifest.json','r')); local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={assets=0,frames=0,pixelComparisons=0,maxVisibleColorsPerFrame=0,failures={}}
local function check(ok,message) if not ok then report.failures[#report.failures+1]=message end end
for _,entry in ipairs(manifest.assets) do
 local s=assert(app.open(root..'/'..entry.aseprite))
 local png=assert(Image{fromFile=root..'/'..entry.png})
 local metaFile=assert(io.open(root..'/'..entry.json,'r'));local data=json.decode(metaFile:read('*a'));metaFile:close()
 check(s.width==entry.width and s.height==entry.height,'Canvas: '..entry.aseprite)
 check(#s.frames==entry.frames,'Frames: '..entry.aseprite)
 check(#s.layers==3,'Layers: '..entry.aseprite)
 check(#s.tags==1 and s.tags[1].name==entry.state,'Tag: '..entry.aseprite)
 check(png.width==entry.width*entry.frames and png.height==entry.height,'Sheet: '..entry.png)
 for n=1,#s.frames do
  local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,n)
  local colors,count,visible={},0,0
  for it in im:pixels() do
   local p=it();local a=pc.rgbaA(p)
   check(a==0 or a==255,'Partial alpha: '..entry.aseprite..' frame '..n)
   if a>0 then
    visible=visible+1
    if not colors[p] then colors[p]=true;count=count+1 end
    check(it.x>0 and it.y>0 and it.x<s.width-1 and it.y<s.height-1,'Canvas clipping: '..entry.aseprite)
   end
   local q=png:getPixel((n-1)*s.width+it.x,it.y)
   check((a==0 and pc.rgbaA(q)==0) or p==q,'PNG mismatch: '..entry.png..' frame '..n)
   report.pixelComparisons=report.pixelComparisons+1
  end
  check(visible>0,'Empty frame: '..entry.aseprite)
  check(count<=20,'Palette too large: '..entry.aseprite..' '..count)
  check(math.abs(s.frames[n].duration*1000-data.frames[n].duration)<0.1,'Timing mismatch: '..entry.json)
  report.maxVisibleColorsPerFrame=math.max(report.maxVisibleColorsPerFrame,count)
  report.frames=report.frames+1
 end
 report.assets=report.assets+1;s:close()
end
check(report.assets==30,'Expected 30 assets')
check(report.frames==115,'Expected 115 frames')
report.passed=#report.failures==0
local out=assert(io.open(root..'/validation.json','w'));out:write(json.encode(report));out:close()
assert(report.passed,'Core validation failed; see validation.json')
print('CORE_SET_VALIDATED')
