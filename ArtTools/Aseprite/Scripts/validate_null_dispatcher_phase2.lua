local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local pc=app.pixelColor
local failures={};local function check(ok,msg) if not ok then failures[#failures+1]=msg end end
local report={passed=false,failures=failures,pixelComparisons=0,unchangedStates={},phase2={},binaryAlpha=true}
local function eq(p,q) return p==q or (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) end
local function layer(s,li,fi) local im=Image(s.width,s.height,ColorMode.RGB);local c=s.layers[li]:cel(fi);if c then im:drawImage(c.image,c.position) end;return im end
local function render(s,fi) local im=Image(s.width,s.height,ColorMode.RGB);for li=1,#s.layers do im:drawImage(layer(s,li,fi),Point(0,0)) end;return im end
local function compare(a,b,label)
 check(a.width==b.width and a.height==b.height,label..' dimensions');local n=0
 for y=0,a.height-1 do for x=0,a.width-1 do if not eq(a:getPixel(x,y),b:getPixel(x,y)) then n=n+1 end;report.pixelComparisons=report.pixelComparisons+1 end end
 check(n==0,label..' '..n..' mismatches');return n
end
local function count(im) local n=0;for it in im:pixels() do if pc.rgbaA(it())>0 then n=n+1 end end;return n end
local source=assert(app.open(sourceDir..'/NULL_Dispatcher.aseprite'))
local revised=assert(app.open(outputDir..'/NULL_Dispatcher.aseprite'))
check(revised.width==160 and revised.height==160,'Canvas');check(#revised.frames==4 and #revised.layers==11 and #revised.tags==4,'Structure')
for i,l in ipairs(source.layers) do check(revised.layers[i].name==l.name and revised.layers[i].opacity==l.opacity and revised.layers[i].isVisible==l.isVisible,'Layer properties '..i) end
for i,t in ipairs(source.tags) do local q=revised.tags[i];check(q.name==t.name and q.fromFrame.frameNumber==t.fromFrame.frameNumber and q.toFrame.frameNumber==t.toFrame.frameNumber,'Tag '..i) end
local ids={'dormant_sealed','active','phase2_unbound','critical_exposed'}
local sheet=Image{fromFile=outputDir..'/NULL_Dispatcher_States.png'};check(sheet.width==640 and sheet.height==160,'Sheet size')
local colors={};local allFrames={}
for fi,id in ipairs(ids) do
 check(revised.frames[fi].duration==source.frames[fi].duration,'Timing '..id)
 if fi~=3 then
  local changes=0
  for li=1,11 do changes=changes+compare(layer(source,li,fi),layer(revised,li,fi),'Unchanged cel '..id..' / '..li) end
  report.unchangedStates[#report.unchangedStates+1]={id=id,celPixelsChanged=changes,layersChecked=11}
 end
 local im=render(revised,fi);allFrames[fi]=im
 compare(im,Image{fromFile=outputDir..'/NULL_Dispatcher_'..id..'.png'},'State export '..id)
 local cell=Image(160,160,ColorMode.RGB);cell:drawImage(sheet,Point(-(fi-1)*160,0));compare(im,cell,'Sheet cell '..id)
 local badAlpha,edge=0,0
 for it in im:pixels() do local p=it();local a=pc.rgbaA(p)
  if a~=0 and a~=255 then badAlpha=badAlpha+1 end
  if a>0 then colors[p]=true;if it.x<2 or it.y<2 or it.x>157 or it.y>157 then edge=edge+1 end end
 end
 check(badAlpha==0,'Binary alpha '..id);check(edge==0,'Canvas margins '..id)
 local native=Image{fromFile=outputDir..'/NULL_Dispatcher_480x270_'..id..'.png'};check(native.width==480 and native.height==270,'Native dimensions '..id)
 if fi~=3 then compare(native,Image{fromFile=sourceDir..'/NULL_Dispatcher_480x270_'..id..'.png'},'Unchanged native '..id) end
 for it in im:pixels() do if pc.rgbaA(it())>0 then check(native:getPixel(it.x+192,it.y+52)==it(),'Native sprite placement '..id) end end
end
local oldCore=Image(160,160,ColorMode.RGB);local newCore=Image(160,160,ColorMode.RGB)
for li=2,4 do
 local old=layer(source,li,3);local new=layer(revised,li,3);local expected=Image(160,160,ColorMode.RGB)
 for y=0,103 do for x=0,103 do
  local sx=math.floor((x//2+0.5)*64/52)*2+16;local sy=math.floor((y//2+0.5)*64/52)*2+16
  expected:drawPixel(x+28,y+28,old:getPixel(sx,sy))
 end end
 compare(expected,new,'Exact nearest-neighbor core '..li);oldCore:drawImage(old,Point(0,0));newCore:drawImage(new,Point(0,0))
end
local bright={};for _,v in ipairs({{207,119,235},{240,179,251},{252,233,255}}) do bright[pc.rgba(v[1],v[2],v[3],255)]=true end
local function stats(im)
 local a={x=160,y=160,r=-1,b=-1,count=0,peak=0,sum=0,brightPixels=0}
 for it in im:pixels() do local p=it();if pc.rgbaA(p)>0 then
  local lum=0.2126*pc.rgbaR(p)+0.7152*pc.rgbaG(p)+0.0722*pc.rgbaB(p);a.count=a.count+1;a.sum=a.sum+lum;a.peak=math.max(a.peak,lum)
  if bright[p] then a.brightPixels=a.brightPixels+1;a.x=math.min(a.x,it.x);a.y=math.min(a.y,it.y);a.r=math.max(a.r,it.x);a.b=math.max(a.b,it.y) end
 end end
 a.width=a.r-a.x+1;a.height=a.b-a.y+1;a.mean=a.sum/a.count;return a
end
local a,b=stats(oldCore),stats(newCore)
local rw,rh=1-b.width/a.width,1-b.height/a.height
report.phase2={oldBloom=a,newBloom=b,visibleBloomWidthReductionPercent=100*rw,visibleBloomHeightReductionPercent=100*rh,rasterReductionPercent=18.75,peakBrightnessPreserved=b.peak==a.peak,nearestNeighborCoreVerified=true,hardwareOpaqueCounts={}}
check(rw>=0.15 and rw<=0.21 and rh>=0.15 and rh<=0.21,'Visible bloom should shrink roughly 15-20 percent')
check(b.peak==a.peak and b.mean>=a.mean*0.97,'Core intensity must be preserved')
-- Hardware translation preserves every occupied pixel; damage stays exclusive to Critical.
for li=5,9 do local before=count(layer(source,li,3));local after=count(layer(revised,li,3));check(before==after,'Hardware removed from Phase 2 layer '..li);report.phase2.hardwareOpaqueCounts[#report.phase2.hardwareOpaqueCounts+1]={layer=revised.layers[li].name,before=before,after=after} end
compare(layer(source,10,3),layer(revised,10,3),'Existing Phase 2 corruption marks')
compare(layer(source,11,3),layer(revised,11,3),'No new Phase 2 damage')
check(count(layer(revised,5,4))<count(layer(revised,5,3))*0.7,'Critical must retain substantially more shell loss')
local oldNet=stats(layer(source,1,3));local newNet=stats(layer(revised,1,3));report.phase2.network={before=oldNet,after=newNet};check(newNet.mean>oldNet.mean,'Network links not brighter')
local oldFrag=stats(layer(source,9,3));local newFrag=stats(layer(revised,9,3));report.phase2.fragments={meanBefore=oldFrag.mean,meanAfter=newFrag.mean};check(newFrag.mean>oldFrag.mean,'Fragments not brighter')
local n=0;for _ in pairs(colors) do n=n+1 end;report.paletteColors=n;check(n<=18,'Palette expanded')
-- Exact integer 2x construction for every Phase 2 layer, including the resized core.
for li=1,11 do local im=layer(revised,li,3);local bad=0;for y=0,159,2 do for x=0,159,2 do local p=im:getPixel(x,y);for dy=0,1 do for dx=0,1 do if not eq(p,im:getPixel(x+dx,y+dy)) then bad=bad+1 end end end end end;check(bad==0,'Phase 2 grid '..li) end
local strip=Image{fromFile=outputDir..'/NULL_Dispatcher_4State_Native_Comparison.png'};check(strip.width==1920 and strip.height==270,'Four native comparison panels')
for i,id in ipairs(ids) do local crop=Image(480,270,ColorMode.RGB);crop:drawImage(strip,Point(-(i-1)*480,0));compare(crop,Image{fromFile=outputDir..'/NULL_Dispatcher_480x270_'..id..'.png'},'Native comparison panel '..i) end
local gif=assert(app.open(outputDir..'/NULL_Dispatcher_480x270.gif'));check(gif.width==480 and gif.height==270 and #gif.frames==4,'Native review GIF');for _,fr in ipairs(gif.frames) do check(math.abs(fr.duration-1.5)<0.001,'Review GIF timing') end;gif:close()
source:close();revised:close()
report.passed=#failures==0
local f=assert(io.open(outputDir..'/validation.json','w'));f:write(json.encode(report));f:close();assert(report.passed,table.concat(failures,'\n'))
