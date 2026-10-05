-- Aseprite 1.3+: derive every asset from copied, approved core1..core9 PNGs.
-- No source saves, resampling filters, partial transparency, or random effects.
local srcDir = assert(app.params.sourceDir)
local outDir = assert(app.params.outputDir)
local pc = app.pixelColor
local function rgba(hex)
  return pc.rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255)
end
local function palette(list)
  local result={}; for _,hex in ipairs(list) do result[#result+1]=rgba(hex) end; return result
end
local families={
  {id='green',label='GREEN A',type='regular',energy=palette{'183E30','286C49','41A966','76D879','B7F3AF','EDFFDC'}},
  {id='orange',label='ORANGE B',type='regular',energy=palette{'553020','995028','DB8432','FFBA53','FFE397','FFF7DA'}},
  {id='blue',label='BLUE C',type='regular',energy=palette{'163454','285E91','388ED1','68C7ED','ABE8F4','E8FAFF'}},
  {id='purple_corrupted',label='PURPLE',type='purple',energy=palette{'35204F','603785','9650C1','CF77EB','F0B3FB','FCE9FF'}},
  {id='gray_raider',label='RAIDER',type='gray',energy=palette{'303636','515D5D','778783','A9B3A4','CFD3BB','E7E8D2'}}
}
local metal=palette{'0E131B','242D39','354454','526572','83949D','B7C4C7'}
local purpleMetal=palette{'151223','2A233D','403450','655271','92809F','C6B2D5'}
local grayMetal=palette{'141719','303638','4A5052','6E7678','A1A7A5','CCD0C9'}
local red=palette{'612E33','AB4647','E97964'}
local sources={}
for i=1,9 do
  local path=srcDir .. '/core' .. i .. '/source.png'
  sources[i]=assert(Image{fromFile=path})
  assert(sources[i].width==32 and sources[i].height==32,'Source must be 32x32: '..path)
end
local function empty() return Image(32,32,ColorMode.RGB) end
local function inside(x,y) return x>=0 and y>=0 and x<32 and y<32 end
local function put(im,x,y,p) if inside(x,y) then im:drawPixel(x,y,p) end end
local function energyPixel(p)
  local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
  return (r>=110 and r>b*1.25 and r>=g*0.96) or (r+g+b>700)
end
local function energyLevel(p)
  local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
  if r+g+b>720 then return 6 end
  if g>=220 then return 5 end
  if g>=175 then return 4 end
  if g>=125 then return 3 end
  if g>=85 then return 2 end
  return 1
end
local function metalLevel(p)
  local v=(pc.rgbaR(p)+pc.rgbaG(p)+pc.rgbaB(p))/3
  if v<12 then return 1 elseif v<30 then return 2 elseif v<39 then return 3
  elseif v<53 then return 4 elseif v<80 then return 5 else return 6 end
end
local function metalFor(f) return f.type=='purple' and purpleMetal or (f.type=='gray' and grayMetal or metal) end
local function initialLayers(f,index,tone,state)
  local housing,energy,detail=empty(),empty(),empty()
  local m=metalFor(f)
  for it in sources[7]:pixels() do
    local p=it(); if pc.rgbaA(p)>0 then housing:drawPixel(it.x,it.y,m[metalLevel(p)]) end
  end
  -- Keep the original poses and emitted shapes; quantize the source colors.
  for it in sources[index]:pixels() do
    local p=it()
    if pc.rgbaA(p)>0 then
      if energyPixel(p) then
        local level=math.max(1,math.min(6,energyLevel(p)+tone))
        if f.type=='gray' then level=math.min(level,4) end
        energy:drawPixel(it.x,it.y,f.energy[level])
      else
        housing:drawPixel(it.x,it.y,m[metalLevel(p)])
      end
    end
  end
  if state=='inactive' then
    for y=13,17 do for x=13,17 do
      if math.abs(x-15)+math.abs(y-15)<=3 then put(energy,x,y,f.energy[1]) end
    end end
  end
  return {housing=housing,energy=energy,detail=detail}
end
local function clippedPaint(im,mask,points,color)
  for _,p in ipairs(points) do
    if inside(p[1],p[2]) and pc.rgbaA(mask:getPixel(p[1],p[2]))>0 then put(im,p[1],p[2],color) end
  end
end
local function addRaider(f,layers,phase,state)
  local h,e,d=layers.housing,layers.energy,layers.detail
  -- A bolted diagonal patch and two red identification marks, on the old frame.
  clippedPaint(d,h,{{9,13},{10,12},{11,11},{12,10},{10,13},{11,12}},grayMetal[5])
  clippedPaint(d,h,{{9,13},{12,10}},grayMetal[2])
  clippedPaint(d,h,{{10,12},{11,11},{21,15},{22,16}},red[2])
  clippedPaint(d,h,{{10,11},{21,16}},red[3])
  clippedPaint(d,h,{{19,20},{20,19},{18,21},{21,18}},grayMetal[1])
  clippedPaint(d,h,{{19,19},{18,20},{13,21}},grayMetal[5])
  if state~='inactive' then
    for _,p in ipairs({{11,15},{12,16},{19,14},{20,15}}) do
      if pc.rgbaA(e:getPixel(p[1],p[2]))>0 then put(e,p[1],p[2],0) end
    end
    if state=='overloaded' then
      put(d,24,18,red[2]); if phase%2==0 then put(d,25,18,red[3]); put(d,7,21,f.energy[3]) end
    end
  end
end
local function shiftBand(im,y,dx)
  local old=Image(im)
  for x=2,29 do put(im,x,y,0) end
  for x=2,29 do
    local p=old:getPixel(x,y); if pc.rgbaA(p)>0 then put(im,x+dx,y,p) end
  end
end
local function addPurple(f,layers,phase,state)
  local h,e,d=layers.housing,layers.energy,layers.detail
  local on=state~='inactive'
  local color=f.energy[on and 3 or 1]
  -- Small asymmetric circuit terminals; the diamond housing stays anchored.
  for _,p in ipairs({{6,14},{5,14},{4,14},{4,13},{24,17},{25,17},{26,17},{26,18}}) do put(d,p[1],p[2],color) end
  put(d,3,12,f.energy[on and 4 or 2]); put(d,27,19,f.energy[on and 4 or 2])
  if on and phase%2==0 then shiftBand(e,12,1); put(d,22,9,f.energy[3]) end
  if state=='overloaded' then
    shiftBand(e,18,(phase%2==0) and -2 or 2)
    shiftBand(e,14,(phase%2==0) and 1 or -1)
    put(d,4,10,f.energy[4]); put(d,5,10,f.energy[2])
    put(d,26,22,f.energy[3]); put(d,27,22,f.energy[5])
    put(d,20,5,f.energy[(phase%2==0) and 5 or 2])
    put(e,16,17,0); put(e,17,17,0)
  end
end
local function makeLayers(f,index,tone,state,phase)
  local layers=initialLayers(f,index,tone,state)
  if f.type=='purple' then addPurple(f,layers,phase,state)
  elseif f.type=='gray' then addRaider(f,layers,phase,state) end
  return layers
end
local function makeShard(f,phase)
  -- Isolate a chipped fragment of the existing lit diamond, not a new silhouette.
  local parent=initialLayers(f,1,(phase==2) and -1 or 0,'active')
  local h,e,d=empty(),empty(),empty()
  local function retained(x,y)
    return inside(x,y) and math.abs(x-15)+math.abs(y-15)<=7
      and not (x>=17 and y>=16 and x+y>=34)
      and pc.rgbaA(parent.energy:getPixel(x,y))>0
  end
  for y=7,23 do for x=7,23 do
    if retained(x,y) then
      local edge=not retained(x-1,y) or not retained(x+1,y) or not retained(x,y-1) or not retained(x,y+1)
      if edge then put(h,x,y,f.energy[1]) else put(e,x,y,parent.energy:getPixel(x,y)) end
    end
  end end
  put(d,22,20,f.energy[3]); put(d,22,21,f.energy[2]); put(d,20,23,f.energy[2])
  if f.type=='purple' and phase%2==0 then shiftBand(e,14,1); put(d,8,12,f.energy[3]) end
  if f.type=='gray' then put(d,13,11,red[2]); put(d,12,12,red[2]) end
  return {housing=h,energy=e,detail=d}
end
local function upscale(im,factor)
  if factor==1 then return Image(im) end
  local result=Image(im.width*factor,im.height*factor,ColorMode.RGB)
  for it in im:pixels() do
    local p=it(); if pc.rgbaA(p)>0 then
      for dy=0,factor-1 do for dx=0,factor-1 do result:drawPixel(it.x*factor+dx,it.y*factor+dy,p) end end
    end
  end
  return result
end
local function writeJson(path,value)
  local f=assert(io.open(path,'w')); f:write(json.encode(value)); f:close()
end
local manifest={generatorId='void-scrapper-core-set-v1',sourceOrder={1,2,3,4,5,6,7,8,9},
  timingNote='Authored timings; the source PNG files contain no timing metadata.',assets={}}
local review={}
local function exportAsset(f,state,poses,durations,size)
  local sprite=Sprite(size,size,ColorMode.RGB)
  sprite.layers[1].name='Housing'
  local energyLayer=sprite:newLayer(); energyLayer.name='Energy'
  local detailsLayer=sprite:newLayer(); detailsLayer.name='Details'
  for frame,pose in ipairs(poses) do
    if frame>1 then sprite:newEmptyFrame(frame) end
    sprite.frames[frame].duration=durations[frame]/1000
    local layers=state=='shard' and makeShard(f,frame) or makeLayers(f,pose[1],pose[2],state,frame)
    sprite:newCel(sprite.layers[1],frame,upscale(layers.housing,size/32),Point(0,0))
    sprite:newCel(energyLayer,frame,upscale(layers.energy,size/32),Point(0,0))
    sprite:newCel(detailsLayer,frame,upscale(layers.detail,size/32),Point(0,0))
  end
  local tag=sprite:newTag(1,#poses); tag.name=state
  local stem='core_'..f.id..'_'..state
  local rel=f.id..'/'..stem
  sprite:saveAs(outDir..'/'..rel..'.aseprite')
  local sheet=Image(size*#poses,size,ColorMode.RGB)
  local data={frames={},meta={app='Aseprite CLI + Lua',image=stem..'.png',format='RGBA8888',
    size={w=sheet.width,h=sheet.height},scale='1',frameTags={{name=state,from=0,to=#poses-1,direction='forward'}},
    family=f.id,state=state,pivot={x=size/2,y=size/2},source='Input/02_Core/core1.png..core9.png'}}
  local rendered={}
  for frame=1,#poses do
    sheet:drawSprite(sprite,frame,Point((frame-1)*size,0))
    rendered[frame]=Image(size,size,ColorMode.RGB); rendered[frame]:drawSprite(sprite,frame)
    data.frames[#data.frames+1]={filename=stem..'_'..string.format('%02d',frame),frame={x=(frame-1)*size,y=0,w=size,h=size},
      rotated=false,trimmed=false,spriteSourceSize={x=0,y=0,w=size,h=size},sourceSize={w=size,h=size},duration=durations[frame]}
  end
  sheet:saveAs(outDir..'/'..rel..'.png')
  writeJson(outDir..'/'..rel..'.json',data)
  manifest.assets[#manifest.assets+1]={family=f.id,state=state,width=size,height=size,frames=#poses,
    aseprite=rel..'.aseprite',png=rel..'.png',json=rel..'.json'}
  review[f.id][state]=rendered
  sprite:close()
end
for _,f in ipairs(families) do
  review[f.id]={}
  exportAsset(f,'inactive',{{7,0}},{100},64)
  local activation={}; for i=1,9 do activation[i]={i,0} end
  exportAsset(f,'activation',activation,{80,70,60,90,90,110,140,100,100},64)
  if f.type=='regular' then
    exportAsset(f,'active',{{1,0},{1,0},{1,-1},{1,0}},{160,120,160,160},64)
    exportAsset(f,'highlighted',{{2,0},{3,0},{4,0},{1,0}},{100,60,100,140},64)
  elseif f.type=='purple' then
    exportAsset(f,'active',{{1,0},{9,0},{1,-1},{1,0}},{120,70,100,150},64)
    exportAsset(f,'overloaded',{{3,0},{4,0},{5,1},{2,0}},{70,50,90,70},64)
  else
    exportAsset(f,'active',{{9,0},{9,-1},{8,0},{9,0}},{180,70,90,180},64)
    exportAsset(f,'overloaded',{{5,0},{9,-1},{6,0},{9,0}},{110,70,150,170},64)
  end
  exportAsset(f,'icon',{{f.type=='gray' and 9 or 1,0}},{100},32)
  exportAsset(f,'shard',{{1,0},{1,-1},{1,0},{1,0}},{180,100,180,180},32)
end

-- A labelled review sheet, using a tiny hard-edged pixel font.
local font={
 A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},
 D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},
 G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},
 J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},
 M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},
 P={'110','101','110','100','100'},Q={'010','101','101','111','011'},R={'110','101','110','101','101'},
 S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},
 V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},
 Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},[' ']={'000','000','000','000','000'}
}
local function text(im,label,x,y,color,scale)
  for n=1,#label do
    local glyph=font[label:sub(n,n)] or font[' ']
    for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then
      for dy=0,scale-1 do for dx=0,scale-1 do im:drawPixel(x+(n-1)*4*scale+(xx-1)*scale+dx,y+(yy-1)*scale+dy,color) end end
    end end end
  end
end
local function board(tick)
  local canvas=Image(976,796,ColorMode.RGB); canvas:clear(Color{r=18,g=23,b=32,a=255})
  local labels={'INACTIVE','ACTIVATION','ACTIVE','HIGHLIGHT','ICON','SHARD'}
  for col,label in ipairs(labels) do text(canvas,label,110+(col-1)*144,20,rgba('A8B7C4'),2) end
  for row,f in ipairs(families) do
    local y=48+(row-1)*148
    text(canvas,f.label,8,y+60,f.energy[4],2)
    local special=f.type=='regular' and 'highlighted' or 'overloaded'
    local states={'inactive','activation','active',special,'icon','shard'}
    for col,state in ipairs(states) do
      local x=104+(col-1)*144
      for yy=0,135 do for xx=0,135 do
        local even=(math.floor(xx/16)+math.floor(yy/16))%2==0
        canvas:drawPixel(x+xx,y+yy,rgba(even and '202936' or '252F3D'))
      end end
      local frames=review[f.id][state]
      local frame
      if tick then frame=frames[((tick-1)%#frames)+1] else frame=frames[state=='activation' and 5 or (state=='highlighted' and 3 or 1)] end
      local scale=frame.width==64 and 2 or 3
      local display=upscale(frame,scale)
      canvas:drawImage(display,Point(x+math.floor((136-display.width)/2),y+math.floor((136-display.height)/2)))
    end
  end
  return canvas
end
board():saveAs(outDir..'/core_set_overview.png')
local preview=Sprite(976,796,ColorMode.RGB)
for n=1,9 do
  if n>1 then preview:newEmptyFrame(n) end
  preview.frames[n].duration=0.14
  preview:newCel(preview.layers[1],n,board(n),Point(0,0))
end
preview:saveAs(outDir..'/core_set_motion_preview.gif')
preview:close()
writeJson(outDir..'/manifest.json',manifest)
local done=assert(io.open(outDir..'/generation_complete.txt','w'))
done:write('30 assets; 115 frames; 5 families; source order preserved.\n');done:close()
print('CORE_SET_GENERATED')
