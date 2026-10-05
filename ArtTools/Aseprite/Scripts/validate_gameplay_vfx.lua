local root=assert(app.params.outputDir)
local function readJson(path) local f=assert(io.open(path,'r'));local value=json.decode(f:read('*a'));f:close();return value end
local manifest=readJson(root..'/manifest.json');local pc=app.pixelColor
local report={passed=true,families={},pixelComparisons=0,totalFrames=0,totalClips=0,failures={},failureCounts={}}
local function check(ok,msg) if not ok then if not report.failureCounts[msg] then report.failures[#report.failures+1]=msg;report.failureCounts[msg]=0 end;report.failureCounts[msg]=report.failureCounts[msg]+1 end end
local function equal(p,q) return (pc.rgbaA(p)==0 and pc.rgbaA(q)==0) or p==q end
local function color(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local expected={VFX_Player_MachineGunMuzzle={32,32,3},VFX_Player_ShotgunMuzzle={32,32,4},VFX_Player_SniperMuzzle={64,32,4},VFX_Raider_Muzzle={32,32,3},VFX_Hit_Generic={32,32,4},VFX_Explosion_Small={32,32,5},VFX_Explosion_Medium={64,64,6},VFX_Player_Dash={32,32,4},VFX_Player_Dash_Curse={32,32,4},VFX_EnemyArrival_Telegraph={64,64,6},VFX_EnemyArrival_Impact={64,64,4},VFX_ShieldHit={64,64,4},VFX_PickupSparkle={32,32,4},VFX_CoreActivation={64,64,6}}
check(#manifest.families==14,'Expected 14 VFX families')
local coreMasks={};local frameCount=0
for _,family in ipairs(manifest.families) do
 local e=assert(expected[family.id],'Unexpected VFX family');local s=assert(app.open(root..'/'..family.aseprite));local sheet=assert(Image{fromFile=root..'/'..family.sheet});local meta=readJson(root..'/'..family.metadata)
 local n=family.framesPerVariant;local rows=#family.variants;local allowed={};for _,v in ipairs(family.palette) do allowed[color(v)]=true end
 check(family.width==e[1] and family.height==e[2] and n==e[3],'Requested dimensions or frame count: '..family.id)
 check(s.width==e[1] and s.height==e[2] and #s.frames==n*rows and #s.layers==4,'Aseprite dimensions/frames/layers: '..family.id)
 check(#s.tags==rows,'Frame tag count: '..family.id);check(sheet.width==family.width*n and sheet.height==family.height*rows,'Sheet dimensions: '..family.id)
 check(#meta.frames==n*rows and #meta.meta.frameTags==rows,'Metadata frame/tag count: '..family.id)
 check(family.pixelsPerUnit==32 and meta.unity.pixelsPerUnit==32,'PPU metadata: '..family.id)
 check(math.abs(family.unityPivot.x-family.pivotPixels.x/family.width)<0.000001 and math.abs(family.unityPivot.y-(1-family.pivotPixels.y/family.height))<0.000001,'Pivot conversion: '..family.id)
 for li,name in ipairs(manifest.layers) do check(s.layers[li].name==name,'Layer names: '..family.id) end
 local result={id=family.id,width=family.width,height=family.height,layers=4,mode=family.mode,clips={}}
 for row,variant in ipairs(family.variants) do
  local strip=assert(Image{fromFile=root..'/'..variant.sheet});check(strip.width==family.width*n and strip.height==family.height,'Variant strip dimensions: '..family.id..' '..variant.tag)
  local from=(row-1)*n+1;local to=from+n-1
  check(s.tags[row].name==variant.tag and s.tags[row].fromFrame.frameNumber==from and s.tags[row].toFrame.frameNumber==to,'Tag boundaries: '..family.id..' '..variant.tag)
  check(meta.meta.frameTags[row].name==variant.tag and meta.meta.frameTags[row].from==from-1 and meta.meta.frameTags[row].to==to-1,'Metadata tag boundaries: '..family.id)
  local clip={tag=variant.tag,frames={},durationMs=0};local images={};local paletteCount=0;local clipColors={};local maxVisible=0;local radii={}
  for fi=1,n do
   local frame=from+fi-1;local im=Image(family.width,family.height,ColorMode.RGB);im:drawSprite(s,frame,Point(0,0));images[fi]=im
   local duration=family.frameDurationsMs[fi];check(math.abs(s.frames[frame].duration*1000-duration)<0.6,'Frame duration: '..family.id..' '..frame)
   local fm=meta.frames[frame];check(fm.duration==duration and fm.frame.x==(fi-1)*family.width and fm.frame.y==(row-1)*family.height and fm.frame.w==family.width and fm.frame.h==family.height and not fm.trimmed,'Frame metadata: '..family.id..' '..frame)
   local visible=0;local minX,minY,maxX,maxY=family.width,family.height,-1,-1;local actualRadius=0
   for it in im:pixels() do
    local p=it();local alpha=pc.rgbaA(p);check(alpha==0 or alpha==255,'Partial alpha: '..family.id)
    check(equal(p,sheet:getPixel((fi-1)*family.width+it.x,(row-1)*family.height+it.y)),'Atlas mismatch: '..family.id)
    check(equal(p,strip:getPixel((fi-1)*family.width+it.x,it.y)),'Strip mismatch: '..family.id);report.pixelComparisons=report.pixelComparisons+2
    if alpha>0 then
     visible=visible+1;check(allowed[p],'Palette mismatch: '..family.id);if not clipColors[p] then clipColors[p]=true;paletteCount=paletteCount+1 end
     minX=math.min(minX,it.x);minY=math.min(minY,it.y);maxX=math.max(maxX,it.x);maxY=math.max(maxY,it.y)
     check(it.x>=2 and it.y>=2 and it.x<family.width-2 and it.y<family.height-2,'Clipped edge: '..family.id..' frame '..fi)
     if it.x%2==0 and it.y%2==0 then actualRadius=math.max(actualRadius,math.abs(it.x/2-16),math.abs(it.y/2-16)) end
    end
   end
   for y=0,family.height-2,2 do for x=0,family.width-2,2 do local p=im:getPixel(x,y);check(equal(p,im:getPixel(x+1,y)) and equal(p,im:getPixel(x,y+1)) and equal(p,im:getPixel(x+1,y+1)),'Non-integer construction grid: '..family.id) end end
   if family.id=='VFX_CoreActivation' then
    if row==1 then coreMasks[fi]=im else for y=0,63 do for x=0,63 do check((pc.rgbaA(im:getPixel(x,y))>0)==(pc.rgbaA(coreMasks[fi]:getPixel(x,y))>0),'Core variant geometry mismatch: '..variant.tag) end end end
   end
   radii[fi]=actualRadius*2;clip.durationMs=clip.durationMs+duration;maxVisible=math.max(maxVisible,visible)
   clip.frames[#clip.frames+1]={index=fi,durationMs=duration,opaquePixels=visible,bounds=visible>0 and {x=minX,y=minY,w=maxX-minX+1,h=maxY-minY+1} or {x=0,y=0,w=0,h=0}}
   report.totalFrames=report.totalFrames+1
  end
  check(paletteCount<=7,'Palette exceeds seven clip colors: '..family.id..' '..variant.tag);clip.visibleColors=paletteCount
  check(clip.frames[1].opaquePixels>0,'Unnecessary blank lead frame: '..family.id)
  if family.mode=='one_shot' then check(clip.frames[n].opaquePixels==0,'One-shot missing transparent cleanup: '..family.id);check(clip.frames[n-1].opaquePixels<maxVisible,'Tail must decay before cleanup: '..family.id)
  elseif family.mode=='handoff' then
   check(clip.frames[n].opaquePixels>0,'Arrival handoff marker missing')
   for i,radius in ipairs(radii) do check(radius==family.ringRadiiPixels[i],'Telegraph radius mismatch');if i>1 then check(radius<radii[i-1],'Telegraph must contract every frame') end end
   clip.measuredRadiiPixels=radii
  elseif family.mode=='loop' then
   check(family.id=='VFX_PickupSparkle','Unexpected looping family');check(clip.frames[n].opaquePixels>0 and clip.frames[n].opaquePixels<=clip.frames[1].opaquePixels,'Twinkle seam must return through small glint')
  end
  local differences={};local maxDiff=0
  for i=1,n-1 do local count=0;for y=0,family.height-1 do for x=0,family.width-1 do if not equal(images[i]:getPixel(x,y),images[i+1]:getPixel(x,y)) then count=count+1 end end end;check(count>0,'Duplicate adjacent frames: '..family.id);differences[#differences+1]=count;maxDiff=math.max(maxDiff,count) end
  if family.mode=='loop' then local seam=0;for y=0,family.height-1 do for x=0,family.width-1 do if not equal(images[n]:getPixel(x,y),images[1]:getPixel(x,y)) then seam=seam+1 end end end;check(seam<=maxDiff,'Loop seam larger than internal change');clip.loopSeamPixelDifference=seam end
  clip.adjacentFramePixelDifferences=differences;result.clips[#result.clips+1]=clip;report.totalClips=report.totalClips+1
 end
 report.families[#report.families+1]=result;s:close()
end
check(report.totalFrames==85 and report.totalClips==18,'Pack totals')
local contact=assert(Image{fromFile=root..'/'..manifest.preview});check(contact.width==1000 and contact.height==1446,'Contact sheet size')
local native=assert(Image{fromFile=root..'/'..manifest.nativePreview});check(native.width==480 and native.height==270,'Native preview dimensions')
local gif=assert(app.open(root..'/'..manifest.animatedPreview));check(gif.width==480 and gif.height==270 and #gif.frames==50,'Animated preview dimensions/frames')
local ms=0;for _,frame in ipairs(gif.frames) do check(math.abs(frame.duration-0.02)<0.001,'Preview frame timing');ms=ms+math.floor(frame.duration*1000+0.5) end;gif:close();check(ms==1000,'Preview loop duration');report.animatedPreview={width=480,height=270,frames=50,durationMs=ms}
report.coreVariantMasksIdentical=true;report.passed=#report.failures==0
local f=assert(io.open(root..'/validation.json','w'));f:write(json.encode(report));f:close()
assert(report.passed,'Gameplay VFX validation failed; read validation.json');print('GAMEPLAY_VFX_VALIDATED')
