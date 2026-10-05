local outputDir=assert(app.params.outputDir)
local sourceDir=assert(app.params.sourceDir)
local pc=app.pixelColor
local f=assert(io.open(outputDir..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local errors={};local function check(ok,msg) if not ok then errors[#errors+1]=msg end end
local report={passed=false,failures=errors,states={},pixelComparisons=0,corePixelComparisons=0,sourceCorePixelsExact=true,nativePreviews={},staticStateTags=true}
local function flat(s,fi) local im=Image(s.width,s.height,ColorMode.RGB);for _,l in ipairs(s.layers) do local c=l:cel(fi);if c and l.isVisible then im:drawImage(c.image,c.position) end end;return im end
local function equals(a,b) return a==b or (pc.rgbaA(a)==0 and pc.rgbaA(b)==0) end
local function compare(a,b,label)
 check(a.width==b.width and a.height==b.height,label..' dimensions')
 local bad=0;for y=0,a.height-1 do for x=0,a.width-1 do if not equals(a:getPixel(x,y),b:getPixel(x,y)) then bad=bad+1 end;report.pixelComparisons=report.pixelComparisons+1 end end
 check(bad==0,label..' has '..bad..' mismatched pixels')
end
local function layerImage(l,frame)
 local im=Image(160,160,ColorMode.RGB);local c=l:cel(frame);if c then im:drawImage(c.image,c.position) end;return im
end
local s=assert(app.open(outputDir..'/'..manifest.aseprite))
check(s.width==160 and s.height==160,'Boss canvas must be 160x160')
check(#s.frames==4 and #s.tags==4,'Four tagged static states required')
check(#s.layers==#manifest.layers,'Editable layer count')
for i,l in ipairs(s.layers) do check(l.name==manifest.layers[i],'Layer name/order '..i);check(l.opacity==255 and l.isVisible,'Unexpected layer opacity/visibility '..l.name) end
local spritesheet=Image{fromFile=outputDir..'/'..manifest.sheet};check(spritesheet.width==640 and spritesheet.height==160,'Sprite sheet dimensions')
local allFlats={};local allColors={};local coreVisibility={};local shellPixels={}
for fi,st in ipairs(manifest.states) do
 check(s.tags[fi].name==st.tag and s.tags[fi].fromFrame.frameNumber==fi and s.tags[fi].toFrame.frameNumber==fi,'State tag '..st.id)
 check(math.abs(s.frames[fi].duration-1)<0.001,'Static frame placeholder duration '..st.id)
 local rendered=flat(s,fi);allFlats[fi]=rendered
 local png=Image{fromFile=outputDir..'/'..st.png};compare(rendered,png,st.png)
 local frame=Image(160,160,ColorMode.RGB);frame:drawImage(spritesheet,Point(-(fi-1)*160,0));compare(rendered,frame,'sheet '..st.id)
 local rec={id=st.id,opaquePixels=0,colors=0,bounds={x=160,y=160,r=-1,b=-1},coreVisiblePixels=0,coreOpaquePixels=0,whiteArmorPixels=0}
 local colors={};local alphaErrors=0;local edgeErrors=0
 for it in rendered:pixels() do local p=it();local alpha=pc.rgbaA(p)
  if alpha~=0 and alpha~=255 then alphaErrors=alphaErrors+1 end
  if alpha>0 then
   rec.opaquePixels=rec.opaquePixels+1;colors[p]=true;allColors[p]=true
   rec.bounds.x=math.min(rec.bounds.x,it.x);rec.bounds.y=math.min(rec.bounds.y,it.y);rec.bounds.r=math.max(rec.bounds.r,it.x);rec.bounds.b=math.max(rec.bounds.b,it.y)
   if it.x<2 or it.y<2 or it.x>=158 or it.y>=158 then edgeErrors=edgeErrors+1 end
   if p==pc.rgba(220,229,227,255) or p==pc.rgba(243,247,242,255) or p==pc.rgba(183,196,199,255) then rec.whiteArmorPixels=rec.whiteArmorPixels+1 end
  end
 end
 for _ in pairs(colors) do rec.colors=rec.colors+1 end
 check(alphaErrors==0,'Partial alpha '..st.id);check(edgeErrors==0,'Clipped canvas edge '..st.id)
 check(rec.colors<=18,'Palette budget '..st.id);check(rec.opaquePixels>2500,'Unreadably sparse state '..st.id)
 -- Every copied core layer is checked independently, including pixels hidden by shutters.
 local source=assert(app.open(sourceDir..'/'..st.coreSource));local expectedCore=Image(160,160,ColorMode.RGB)
 for li=1,3 do
  local expected=Image(160,160,ColorMode.RGB);local cel=assert(source.layers[li]:cel(st.coreSourceFrame))
  local src=Image(64,64,ColorMode.RGB);src:drawImage(cel.image,cel.position)
  for y=0,64*st.coreScale-1 do for x=0,64*st.coreScale-1 do expected:drawPixel(st.coreOffset.x+x,st.coreOffset.y+y,src:getPixel(x//st.coreScale,y//st.coreScale)) end end
  local actual=layerImage(s.layers[li+1],fi);local mismatches=0
  for y=0,159 do for x=0,159 do if not equals(expected:getPixel(x,y),actual:getPixel(x,y)) then mismatches=mismatches+1 end;report.corePixelComparisons=report.corePixelComparisons+1 end end
  if mismatches>0 then report.sourceCorePixelsExact=false end
  check(mismatches==0,'Approved core pixels altered '..st.id..' / '..li);expectedCore:drawImage(expected,Point(0,0))
 end
 source:close()
 for it in expectedCore:pixels() do if pc.rgbaA(it())>0 then rec.coreOpaquePixels=rec.coreOpaquePixels+1;if rendered:getPixel(it.x,it.y)==it() then rec.coreVisiblePixels=rec.coreVisiblePixels+1 end end end
 rec.coreVisibility=rec.coreVisiblePixels/rec.coreOpaquePixels
 coreVisibility[fi]=rec.coreVisibility
 -- New frame/section shapes are authored on an exact 2px grid.
 for li,l in ipairs(s.layers) do if li==1 or li>=5 then
  local im=layerImage(l,fi);local bad=0
  for y=0,159,2 do for x=0,159,2 do local p=im:getPixel(x,y);for dy=0,1 do for dx=0,1 do if not equals(p,im:getPixel(x+dx,y+dy)) then bad=bad+1 end end end end end
  check(bad==0,'Noninteger construction grid '..st.id..' / '..l.name)
 end end
 local shell=layerImage(s.layers[5],fi);shellPixels[fi]=0;for it in shell:pixels() do if pc.rgbaA(it())>0 then shellPixels[fi]=shellPixels[fi]+1 end end
 local nativePath='NULL_Dispatcher_480x270_'..st.id..'.png';local native=Image{fromFile=outputDir..'/'..nativePath}
 check(native.width==480 and native.height==270,'Native review size '..st.id)
 local displayBad=0;for it in rendered:pixels() do if pc.rgbaA(it())>0 and native:getPixel(it.x+192,it.y+52)~=it() then displayBad=displayBad+1 end end
 check(displayBad==0,'Native preview scaled or obscured '..st.id);report.nativePreviews[#report.nativePreviews+1]={file=nativePath,width=native.width,height=native.height,bossScale=1}
 report.states[#report.states+1]=rec
end
s:close()
check(coreVisibility[1]<0.5,'Sealed shutters must obscure the core')
check(coreVisibility[2]>0.95,'Active core must be fully readable')
check(shellPixels[4]<shellPixels[3]*0.7,'Critical must remove substantial shell geometry')
check(report.states[4].whiteArmorPixels<report.states[3].whiteArmorPixels*0.75,'Critical armor loss must read at gameplay scale')
report.pairwiseDifferences={}
for a=1,3 do for b=a+1,4 do
 local diff,maskDiff=0,0;for y=0,159 do for x=0,159 do local p,q=allFlats[a]:getPixel(x,y),allFlats[b]:getPixel(x,y);if not equals(p,q) then diff=diff+1 end;if (pc.rgbaA(p)>0)~=(pc.rgbaA(q)>0) then maskDiff=maskDiff+1 end end end
 check(diff>700 and maskDiff>500,'States too similar '..a..' / '..b);report.pairwiseDifferences[#report.pairwiseDifferences+1]={a=a,b=b,pixelChanges=diff,silhouetteChanges=maskDiff}
end end
local count=0;for _ in pairs(allColors) do count=count+1 end;report.totalVisibleColors=count;check(count<=18,'Total palette exceeds 18 colors')
local contact=Image{fromFile=outputDir..'/NULL_Dispatcher_Comparison.png'};check(contact.width==1400 and contact.height==760,'Comparison dimensions')
local gif=assert(app.open(outputDir..'/NULL_Dispatcher_480x270.gif'));check(gif.width==480 and gif.height==270 and #gif.frames==4,'Native review GIF');for _,fr in ipairs(gif.frames) do check(math.abs(fr.duration-1.5)<0.001,'Native review GIF duration') end;gif:close()
report.shellOpaquePixels=shellPixels;report.passed=#errors==0
local f=assert(io.open(outputDir..'/validation.json','w'));f:write(json.encode(report));f:close()
assert(report.passed,table.concat(errors,'\n'))
