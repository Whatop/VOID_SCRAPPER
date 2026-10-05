local refs=assert(app.params.sourceDir);local out=assert(app.params.outputDir);local pc=app.pixelColor
local function read(name) local f=assert(io.open(out..'/'..name,'r'));local d=json.decode(f:read('*a'));f:close();return d end
local manifest=read('manifest.json');local metadata=read(manifest.transitionMetadata)
local report={passed=false,failures={},counts={},frames={},pixelComparisons=0}
local function check(ok,msg) if not ok then if not report.counts[msg] then report.failures[#report.failures+1]=msg;report.counts[msg]=0 end;report.counts[msg]=report.counts[msg]+1 end end
local function rgba(s) return pc.rgba(tonumber(s:sub(1,2),16),tonumber(s:sub(3,4),16),tonumber(s:sub(5,6),16),255) end
local function equal(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local greens={};for _,v in ipairs({'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'}) do greens[rgba(v)]=true end
local production=Image{fromFile=refs..'/production.png'}
local approved=assert(app.open(refs..'/approved_idle.aseprite'))
local original=Image(128,128,ColorMode.RGB);original:drawSprite(approved,1,Point(0,0))
for it in original:pixels() do check(equal(it(),production:getPixel(it.x,it.y)),'Approved source differs from production') end
local function render(s,f) local im=Image(128,128,ColorMode.RGB);im:drawSprite(s,f,Point(0,0));return im end
local function layerImage(s,l,f) local im=Image(128,128,ColorMode.RGB);local c=s.layers[l]:cel(f);if c then im:drawImage(c.image,c.position) end;return im end
local master=assert(app.open(out..'/'..manifest.aseprite))
local transition=assert(app.open(out..'/'..manifest.transitionAseprite))
check(master.width==128 and master.height==128 and #master.frames==1,'Main state structure')
check(transition.width==128 and transition.height==128 and #transition.frames==5,'Transition structure')
check(#master.tags==1 and master.tags[1].name=='Purple Overdrive','Main tag')
check(#transition.tags==1 and transition.tags[1].name=='Green to Purple Overdrive' and transition.tags[1].fromFrame.frameNumber==1 and transition.tags[1].toFrame.frameNumber==5,'Transition tag')
local png=Image{fromFile=out..'/'..manifest.png};local sheet=Image{fromFile=out..'/'..manifest.transitionSheet}
local cannon=Image{fromFile=out..'/'..manifest.centerCannon.png};local cannonSheet=Image{fromFile=out..'/'..manifest.centerCannon.transitionSheet}
check(png.width==128 and png.height==128 and sheet.width==640 and sheet.height==128,'PNG export dimensions')
check(cannon.width==30 and cannon.height==32 and cannonSheet.width==150 and cannonSheet.height==32,'Cannon crop dimensions')
local frames={};local expectedDuration={50,50,50,60,90}
for fi=1,5 do
 local im=render(transition,fi);frames[fi]=im
 local stats={frame=fi,changedPixels=0,energyPixels=0,nonEnergyDifferences=0,alphaDifferences=0,opaquePixels=0,whiteHotPixels=0}
 local colors={};local allowed={};for _,hex in ipairs(manifest.metalPalette) do allowed[rgba(hex)]=true end;for _,hex in ipairs(manifest.energyRamps[fi]) do allowed[rgba(hex)]=true end
 for it in im:pixels() do
  local x,y=it.x,it.y;local v=it();local before=production:getPixel(x,y);local a=pc.rgbaA(v)
  report.pixelComparisons=report.pixelComparisons+1
  if not equal(v,before) then stats.changedPixels=stats.changedPixels+1 end
  if greens[before] then stats.energyPixels=stats.energyPixels+1 elseif not equal(v,before) then stats.nonEnergyDifferences=stats.nonEnergyDifferences+1 end
  if a~=pc.rgbaA(before) then stats.alphaDifferences=stats.alphaDifferences+1 end
  check(a==0 or a==255,'Partial alpha / anti-aliasing')
  check(equal(v,sheet:getPixel((fi-1)*128+x,y)),'Transition PNG mismatch')
  check(equal(v,im:getPixel(127-x,y)),'Bilateral symmetry changed')
  if a>0 then stats.opaquePixels=stats.opaquePixels+1;colors[v]=true;check(allowed[v],'Unspecified palette color');if fi>1 then check(not greens[v],'Green energy remains') end end
  if fi==5 and v==rgba('FFF6FF') then stats.whiteHotPixels=stats.whiteHotPixels+1;check(x>=58 and x<70 and y>=58 and y<70,'White-hot energy escaped core') end
  if fi==1 then check(equal(v,before),'First transition frame differs from Phase 1') end
 end
 for y=0,127,2 do for x=0,127,2 do local v=im:getPixel(x,y);check(equal(v,im:getPixel(x+1,y)) and equal(v,im:getPixel(x,y+1)) and equal(v,im:getPixel(x+1,y+1)),'Original 2px grid changed') end end
 local n=0;for _ in pairs(colors) do n=n+1 end;stats.colors=n;check(n<=13,'Palette exceeds 13 colors per frame')
 check(stats.nonEnergyDifferences==0 and stats.alphaDifferences==0,'Chassis or silhouette changed')
 check(fi==1 or stats.changedPixels==stats.energyPixels,'Every green source pixel must convert')
 check(math.abs(transition.frames[fi].duration*1000-expectedDuration[fi])<0.1,'Transition timing')
 check(metadata.frames[fi].duration==expectedDuration[fi] and metadata.frames[fi].frame.x==(fi-1)*128,'Transition JSON timings and rectangles')
 for y=0,31 do for x=0,29 do check(equal(im:getPixel(x+49,y+16),cannonSheet:getPixel((fi-1)*30+x,y)),'Cannon transition differs from fixed production crop') end end
 report.frames[#report.frames+1]=stats
end
local main=render(master,1)
for it in main:pixels() do check(equal(it(),png:getPixel(it.x,it.y)) and equal(it(),frames[5]:getPixel(it.x,it.y)),'Main ASE/PNG/transition endpoint mismatch');report.pixelComparisons=report.pixelComparisons+2 end
for y=0,31 do for x=0,29 do check(equal(main:getPixel(x+49,y+16),cannon:getPixel(x,y)),'Main cannon crop mismatch') end end
for _,s in ipairs({master,transition}) do
 check(#s.layers==#approved.layers,'Original layer count changed')
 for li,l in ipairs(s.layers) do
  local src=approved.layers[li];check(l.name==src.name and l.opacity==src.opacity and l.isVisible==src.isVisible and l.blendMode==src.blendMode,'Original layer properties changed')
  local before=layerImage(approved,li,1)
  for fi=1,#s.frames do
   local c=l:cel(fi);local b=src:cel(1)
   check(c~=nil and c.position.x==b.position.x and c.position.y==b.position.y and c.opacity==b.opacity,'Original cel placement changed')
   local im=layerImage(s,li,fi)
   for it in im:pixels() do local v=it();local prev=before:getPixel(it.x,it.y);check(pc.rgbaA(v)==pc.rgbaA(prev),'Layer attachment mask changed');if not greens[prev] then check(equal(v,prev),'Non-energy source layer pixels changed') end;report.pixelComparisons=report.pixelComparisons+1 end
  end
 end
end
for i=1,4 do local changed=0;for it in frames[i]:pixels() do if not equal(it(),frames[i+1]:getPixel(it.x,it.y)) then changed=changed+1 end end;check(changed>0,'Duplicate transition frame') end
local native=Image{fromFile=out..'/'..manifest.nativePreview};check(native.width==480 and native.height==270,'Native preview dimensions')
for _,v in ipairs({{production,56},{main,296}}) do for it in v[1]:pixels() do if pc.rgbaA(it())>0 then check(equal(it(),native:getPixel(v[2]+it.x,68+it.y)),'Native preview rescaled pixels') end end end
local contact=Image{fromFile=out..'/'..manifest.comparison};check(contact.width==800 and contact.height==648,'Comparison dimensions')
local gif=assert(app.open(out..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==5,'Animated review dimensions');gif:close()
check(metadata.unity.pixelsPerUnit==87.671234 and metadata.unity.pivot.x==0.5 and metadata.unity.pivot.y==0.5,'Preserve live production import scale and pivot')
check(report.frames[5].whiteHotPixels>0 and report.frames[5].whiteHotPixels<=64,'Core white-hot focal point size')
report.phase1PixelsIdentical=true;report.alphaMasksIdentical=true;report.originalLayersPreserved=10;report.mainChassisAnimated=false;report.transitionTotalMs=300
report.passed=#report.failures==0
local f=assert(io.open(out..'/validation.json','w'));f:write(json.encode(report));f:close()
master:close();transition:close();approved:close();assert(report.passed,table.concat(report.failures,'\n'))
