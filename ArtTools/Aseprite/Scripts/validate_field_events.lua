local root=assert(app.params.outputDir)
local f=assert(io.open(root..'/manifest.json','r'));local manifest=json.decode(f:read('*a'));f:close()
local pc=app.pixelColor
local report={passed=true,objects={},pixelComparisons=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local lightMetal,red,hot={},{},{}
for _,v in ipairs({'ADB9B7','D6DEDA'}) do lightMetal[color(v)]=true end
for _,v in ipairs({'73373E','D35B58'}) do red[color(v)]=true end
for _,v in ipairs({'E9913C','FFE0A2'}) do hot[color(v)]=true end
local flats,metrics={},{}
local expectedSizes={rescue_signal=64,unknown_device=64,unstable_reactor=96,black_box=64}
local expectedCounts={rescue_signal=3,unknown_device=3,unstable_reactor=4,black_box=3}
check(#manifest.objects==4,'Expected four event objects')
local function layerImage(layer,frame,size)
 local out=Image(size,size,ColorMode.RGB);local cel=layer:cel(frame);if cel then out:drawImage(cel.image,cel.position) end;return out
end
local function opaqueCount(im) local n=0;for it in im:pixels() do if pc.rgbaA(it())>0 then n=n+1 end end;return n end
for _,unit in ipairs(manifest.objects) do
 local size=assert(expectedSizes[unit.id]);local frameCount=expectedCounts[unit.id];local master=assert(app.open(root..'/'..unit.master));local sheet=assert(Image{fromFile=root..'/'..unit.sheet})
 local allowed,accent,bright={},{},{}
 for _,v in ipairs(unit.palette) do allowed[color(v)]=true end
 for i,v in ipairs(unit.accent) do accent[color(v)]=true;if i>=#unit.accent-1 then bright[color(v)]=true end end
 check(master.width==size and master.height==size and #master.frames==frameCount and #master.layers==8,'Master size/frames/layers: '..unit.id)
 check(#master.tags==frameCount,'Master tags: '..unit.id);check(sheet.width==size*frameCount and sheet.height==size,'Sheet dimensions: '..unit.id)
 for i,name in ipairs(unit.layers) do check(master.layers[i].name==name,'Layer names: '..unit.id) end
 local entry={id=unit.id,width=size,height=size,layers=8,states={},differences={}};metrics[unit.id]={};flats[unit.id]={}
 for index,a in ipairs(unit.assets) do
  local im=Image(size,size,ColorMode.RGB);im:drawSprite(master,index,Point(0,0));flats[unit.id][a.id]=im;local png=assert(Image{fromFile=root..'/'..a.png})
  check(png.width==size and png.height==size,'PNG dimensions: '..unit.id..' '..a.id)
  check(master.tags[index].name==a.id and master.tags[index].fromFrame.frameNumber==index and master.tags[index].toFrame.frameNumber==index,'Tag: '..unit.id..' '..a.id)
  local colors,count,visible={},0,0;local minX,minY,maxX,maxY=size,size,-1,-1;local accents,brights,reds,heats,metal=0,0,0,0,0
  for it in im:pixels() do
   local p=it();local alpha=pc.rgbaA(p)
   check(alpha==0 or alpha==255,'Partial alpha: '..unit.id..' '..a.id)
   check(equal(p,png:getPixel(it.x,it.y)),'PNG mismatch: '..unit.id..' '..a.id);check(equal(p,sheet:getPixel((index-1)*size+it.x,it.y)),'Sheet mismatch: '..unit.id..' '..a.id)
   report.pixelComparisons=report.pixelComparisons+2
   if alpha>0 then
    check(allowed[p],'Object palette mismatch: '..unit.id..' '..a.id);visible=visible+1;if not colors[p] then colors[p]=true;count=count+1 end
    if accent[p] then accents=accents+1 end;if bright[p] then brights=brights+1 end;if red[p] then reds=reds+1 end;if hot[p] then heats=heats+1 end;if lightMetal[p] then metal=metal+1 end
    minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
    check(it.x>=4 and it.y>=4 and it.x<size-2 and it.y<size-2,'Canvas margin: '..unit.id..' '..a.id)
   end
  end
  for y=0,size-2,2 do for x=0,size-2,2 do local p=im:getPixel(x,y);check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer pixel scale: '..unit.id..' '..a.id) end end
  check(count<=14,'Palette limit: '..unit.id..' '..a.id);check(visible>=280,'Empty/unreadable pose: '..unit.id..' '..a.id)
  local result={id=a.id,visibleColors=count,opaquePixels=visible,accentPixels=accents,brightAccentPixels=brights,warningRedPixels=reds,hotOrangePixels=heats,lightMetalPixels=metal,bounds={x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1},binaryAlpha=true}
  entry.states[#entry.states+1]=result;metrics[unit.id][a.id]=result
 end
 for i=1,#unit.assets do for j=i+1,#unit.assets do
  local a,b=unit.assets[i].id,unit.assets[j].id;local diff,small=0,0
  for y=0,size-1 do for x=0,size-1 do if not equal(flats[unit.id][a]:getPixel(x,y),flats[unit.id][b]:getPixel(x,y)) then diff=diff+1;if x%2==0 and y%2==0 then small=small+1 end end end end
  check(diff>=size*2,'Weak state difference: '..unit.id..' '..a..' / '..b);entry.differences[#entry.differences+1]={first=a,second=b,differingPixels=diff,differingPixelsAtHalfSize=small}
 end end
 if unit.id=='rescue_signal' then
  local a=layerImage(master.layers[3],1,size);local b=layerImage(master.layers[3],3,size);local mask=0
  for y=0,size-1 do for x=0,size-1 do if (pc.rgbaA(a:getPixel(x,y))>0)~=(pc.rgbaA(b:getPixel(x,y))>0) then mask=mask+1 end end end
  check(mask>=100,'Completed rescue must physically open the hatch');check(opaqueCount(layerImage(master.layers[8],3,size))==0,'Completed rescue must stop distress pulses');entry.completedHatchMaskDifference=mask
 elseif unit.id=='black_box' then
  check(opaqueCount(layerImage(master.layers[3],3,size))==0,'Recovered recorder case must be absent');check(opaqueCount(layerImage(master.layers[8],3,size))==0,'Recovered recorder must stop transmitting')
 end
 report.objects[#report.objects+1]=entry;master:close()
end
check(metrics.rescue_signal.signal_active.brightAccentPixels>metrics.rescue_signal.idle.brightAccentPixels*2,'Rescue signal brightness escalation')
check(metrics.unknown_device.analyzing.brightAccentPixels>=metrics.unknown_device.dormant.brightAccentPixels+40,'Purple charging cue')
check(metrics.unstable_reactor.critical.warningRedPixels>metrics.unstable_reactor.active.warningRedPixels+100,'Critical reactor must visibly warn red')
check(metrics.unstable_reactor.critical.hotOrangePixels>metrics.unstable_reactor.active.hotOrangePixels*1.5,'Critical reactor must expand hot pressure stack')
check(metrics.unstable_reactor.destroyed.opaquePixels<metrics.unstable_reactor.active.opaquePixels*0.75,'Destroyed reactor must lose substantial geometry')
check(metrics.unstable_reactor.destroyed.hotOrangePixels<=metrics.unstable_reactor.active.hotOrangePixels*0.15,'Destroyed reactor must cease active energy')
check(metrics.black_box.data_recovery.brightAccentPixels>metrics.black_box.dormant.brightAccentPixels+40,'Data recovery lights')
check(metrics.black_box.recovered.opaquePixels<metrics.black_box.dormant.opaquePixels*0.5,'Recovered Black Box must leave a smaller empty cradle')
check(metrics.black_box.recovered.accentPixels==0,'Recovered Black Box must be unlit')
check(metrics.unstable_reactor.dormant.opaquePixels>metrics.rescue_signal.idle.opaquePixels*2,'Reactor must be visibly heavier than the capsule')
check(metrics.black_box.dormant.opaquePixels<metrics.rescue_signal.idle.opaquePixels,'Black Box must read as more portable than rescue capsule')
local normalized={}
for _,unit in ipairs(manifest.objects) do local im=flats[unit.id][unit.assets[1].id];local mask={};for y=0,47 do for x=0,47 do mask[y*48+x+1]=pc.rgbaA(im:getPixel(math.floor(x*im.width/48),math.floor(y*im.height/48)))>0 end end;normalized[unit.id]=mask end
report.roleSilhouetteDifferences={}
for i=1,#manifest.objects do for j=i+1,#manifest.objects do
 local a,b=manifest.objects[i].id,manifest.objects[j].id;local diff=0
 for p=1,48*48 do if normalized[a][p]~=normalized[b][p] then diff=diff+1 end end
 check(diff>=300,'Event silhouettes too similar: '..a..' / '..b);report.roleSilhouetteDifferences[#report.roleSilhouetteDifferences+1]={first=a,second=b,differentOpaqueMaskPixelsAt48px=diff}
end end
local preview=assert(Image{fromFile=root..'/'..manifest.preview});check(preview.width==1000 and preview.height==1060,'Preview dimensions')
report.passed=#report.failures==0;local output=assert(io.open(root..'/validation.json','w'));output:write(json.encode(report));output:close()
assert(report.passed,'Field Event validation failed; read validation.json');print('FIELD_EVENTS_VALIDATED')
