-- Targeted revision of frame 3 only. Open a copied layered source.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local m={};for i,v in ipairs({'0E131B','283641','4A5D68','83949D','B7C4C7','DCE5E3','F3F7F2'}) do m[i]=rgba(v) end
local e={};for i,v in ipairs({'35204F','603785','9650C1','CF77EB','F0B3FB','FCE9FF'}) do e[i]=rgba(v) end
local function blank() return Image(160,160,ColorMode.RGB) end
local function layerImage(l,f) local im=blank();local c=l:cel(f);if c then im:drawImage(c.image,c.position) end;return im end
local function flat(s,f) local im=blank();for _,l in ipairs(s.layers) do im:drawImage(layerImage(l,f),Point(0,0)) end;return im end
local function copy(name) local f=assert(io.open(sourceDir..'/'..name,'rb'));local data=f:read('*a');f:close();local g=assert(io.open(outputDir..'/'..name,'wb'));g:write(data);g:close() end
local function readJson(name) local f=assert(io.open(sourceDir..'/'..name,'r'));local obj=json.decode(f:read('*a'));f:close();return obj end
local function writeJson(name,obj) local f=assert(io.open(outputDir..'/'..name,'w'));f:write(json.encode(obj));f:close() end
local function shift(im,dx,dy) local out=blank();for it in im:pixels() do if pc.rgbaA(it())>0 then h.put(out,it.x+dx,it.y+dy,it()) end end;return out end
local s=assert(app.open(sourceDir..'/NULL_Dispatcher.aseprite'))
assert(s.width==160 and s.height==160 and #s.frames==4 and #s.layers==11,'Unexpected source structure')
local original={};for i,l in ipairs(s.layers) do original[i]=layerImage(l,3) end
local before=flat(s,3)
local revised={}
-- Original core occupied a 128px raster at (16,16). Read one pixel per
-- original 2x block, nearest-neighbor resample 64 to 52, then restore 2x blocks.
-- 104/128 = 81.25%; no new colors, opacity changes, blur or core redesign.
for li=2,4 do
 local im=blank()
 for y=0,51 do for x=0,51 do
  local sx=math.floor((x+0.5)*64/52);local sy=math.floor((y+0.5)*64/52)
  local p=original[li]:getPixel(16+sx*2,16+sy*2)
  h.rect(im,28+x*2,28+y*2,2,2,p)
 end end
 revised[li]=im
end
-- Maintain the intact Phase 2 hardware; separate whole pieces, do not add damage.
local shell=blank()
for it in original[5]:pixels() do if pc.rgbaA(it())>0 then h.put(shell,it.x+(it.x<80 and -2 or 2),it.y+(it.y<80 and -2 or 2),it()) end end
revised[5]=shell;revised[6]=shift(original[6],0,-4)
revised[7]=shift(original[7],-2,2);revised[8]=shift(original[8],2,2)
local fragments=blank()
local pieces={{10,68,-2,0},{144,62,2,0},{60,140,-4,0},{94,142,4,0},{40,16,-2,-2},{116,18,2,-2}}
for _,q in ipairs(pieces) do
 for y=q[2],q[2]+13 do for x=q[1],q[1]+9 do local p=original[9]:getPixel(x,y)
  if pc.rgbaA(p)>0 then
   if p==m[3] then p=m[5] elseif p==m[5] then p=m[6] end
   h.put(fragments,x+q[3],y+q[4],p)
  end
 end end
end
revised[9]=fragments
local net=Image(80,80,ColorMode.RGB)
local function route(points,c) for i=1,#points-1 do h.line(net,points[i][1],points[i][2],points[i+1][1],points[i+1][2],c) end end
-- Visible bright stubs on both sides of each deliberate disconnected gap.
route({{39,18},{39,22},{41,22}},e[4]);route({{43,25},{43,28},{41,30}},e[3]);h.rect(net,40,22,2,1,e[5])
route({{18,56},{24,56},{27,53}},e[4]);route({{30,50},{31,48},{32,48}},e[3]);h.rect(net,23,55,2,1,e[5])
route({{63,55},{58,55},{56,52}},e[4]);route({{53,50},{52,48}},e[3]);h.rect(net,58,54,2,1,e[5])
route({{22,21},{27,21},{30,24}},e[3]);route({{33,27},{34,28}},e[4])
route({{60,22},{55,22},{52,25}},e[4]);route({{50,28},{49,29}},e[3])
route({{24,65},{28,65},{31,61}},e[3]);h.rect(net,26,65,2,1,e[5])
route({{55,66},{51,66},{48,62}},e[4]);h.rect(net,51,66,2,1,e[5])
revised[1]=h.scale(net,2)
for li,im in pairs(revised) do local cel=assert(s.layers[li]:cel(3));cel.image=im;cel.position=Point(0,0) end
s:saveAs(outputDir..'/NULL_Dispatcher.aseprite')
local images={};local sheet=Image(640,160,ColorMode.RGB)
for i=1,4 do images[i]=flat(s,i);sheet:drawImage(images[i],Point((i-1)*160,0)) end
s:close();images[3]:saveAs(outputDir..'/NULL_Dispatcher_phase2_unbound.png');sheet:saveAs(outputDir..'/NULL_Dispatcher_States.png')
local ids={'dormant_sealed','active','phase2_unbound','critical_exposed'}
for i,id in ipairs(ids) do if i~=3 then copy('NULL_Dispatcher_'..id..'.png');copy('NULL_Dispatcher_480x270_'..id..'.png') end end
copy('NULL_Dispatcher.json')
local bg=rgba('111820');local tile1=rgba('202B36');local tile2=rgba('24313D')
local contact=Image{fromFile=sourceDir..'/NULL_Dispatcher_Comparison.png'}
for y=80,399 do for x=710,1029 do h.put(contact,x,y,((x-710)//8+(y-80)//8)%2==0 and tile1 or tile2) end end
contact:drawImage(h.scale(images[3],2),Point(710,80));h.rect(contact,790,448,160,160,bg);contact:drawImage(images[3],Point(790,448))
contact:saveAs(outputDir..'/NULL_Dispatcher_Comparison.png')
local native=Image{fromFile=sourceDir..'/NULL_Dispatcher_480x270_phase2_unbound.png'}
h.rect(native,192,52,160,160,bg);native:drawImage(images[3],Point(192,52));native:saveAs(outputDir..'/NULL_Dispatcher_480x270_phase2_unbound.png')
local gif=Sprite(480,270,ColorMode.RGB);gif.layers[1].name='Native Review';local strip=Image(1920,270,ColorMode.RGB)
for i,id in ipairs(ids) do
 local im=Image{fromFile=outputDir..'/NULL_Dispatcher_480x270_'..id..'.png'}
 if i>1 then gif:newEmptyFrame(i) end;gif:newCel(gif.layers[1],i,im,Point(0,0));gif.frames[i].duration=1.5
 strip:drawImage(im,Point((i-1)*480,0))
end
gif:saveAs(outputDir..'/NULL_Dispatcher_480x270.gif');gif:close();strip:saveAs(outputDir..'/NULL_Dispatcher_4State_Native_Comparison.png')
local proof=Image(480,270,ColorMode.RGB);h.rect(proof,0,0,480,270,bg)
h.text(proof,'PHASE 2 / SILHOUETTE REVISION',16,14,m[6],2)
h.text(proof,'BEFORE',48,40,m[4],1);h.text(proof,'REVISED',272,40,e[5],1)
proof:drawImage(before,Point(48,58));proof:drawImage(images[3],Point(272,58))
h.text(proof,'NATIVE PIXELS / SAME PALETTE AND PEAK BRIGHTNESS',16,246,m[4],1)
proof:saveAs(outputDir..'/NULL_Dispatcher_Phase2_BeforeAfter_480x270.png')
local manifest=readJson('manifest.json')
manifest.generatorId='void-scrapper-null-dispatcher-phase2-v2'
manifest.corePolicy='Dormant/Active/Critical are unchanged. Phase 2 resamples the approved overloaded core from the original 128px raster to 104px, nearest-neighbor on a 2px grid, with identical colors and binary alpha.'
manifest.states[3].coreScale=1.625;manifest.states[3].coreOffset={x=28,y=28}
manifest.states[3].description='18.75 percent smaller core raster footprint; intact sections spread 2-4px outward, brighter interrupted links and floating fragments. Critical retains the most damaged hardware.'
manifest.revision={onlyFrame=3,source='Output/20_NullDispatcher/NULL_Dispatcher.aseprite',linearReductionPercent=18.75,coreRasterBefore=128,coreRasterAfter=104,sampling='Nearest-neighbor to a 52x52 logical raster, expanded exactly 2x',paletteChanges=false,unchangedStates={1,2,4},nativeComparison='NULL_Dispatcher_4State_Native_Comparison.png',nativeComparisonCell={w=480,h=270},beforeAfter='NULL_Dispatcher_Phase2_BeforeAfter_480x270.png'}
writeJson('manifest.json',manifest)
local marker=assert(io.open(outputDir..'/generation_complete.txt','w'));marker:write('NULL DISPATCHER / Phase 2 revision only / four-state source\n');marker:close()
