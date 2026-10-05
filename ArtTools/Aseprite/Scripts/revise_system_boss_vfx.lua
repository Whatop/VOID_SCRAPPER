-- Revise only the three requested families, using copied editable sources.
-- Frame/cel/tag operations: https://www.aseprite.org/api/sprite
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,diamond,scale=h.put,h.rect,h.line,h.poly,h.oct,h.diamond,h.scale
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local hex={orange={'6F3A26','E9913C','FFE0A2','F0F2E9'},blue={'234871','529EDC','B8EFFF','F0F2E9'}}
local P={};for k,values in pairs(hex) do P[k]={};for i,v in ipairs(values) do P[k][i]=rgba(v) end end
local white=rgba('F0F2E9')
local font={['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['9']={'111','101','111','001','110'}}
local function text(im,label,x,y,c,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if font[ch] then for yy,row in ipairs(font[ch]) do for xx=1,3 do if row:sub(xx,xx)=='1' then rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,c) end end end else h.text(im,ch,px,y,c,n) end
 end
end
local layerNames={'Energy Shape','Bright Center','Technical Marks','Fragments'}
local function layers(w) local l={};for i=1,4 do l[i]=Image(w/2,w/2,ColorMode.RGB) end;return l end
local function outline(im,pts,c,closed)
 for i=1,#pts-(closed==false and 1 or 0) do local a,b=pts[i],pts[i%#pts+1];line(im,a[1],a[2],b[1],b[2],c) end
end
local function ring(im,cx,cy,r,c)
 local k=math.max(1,math.floor(r*0.43));outline(im,{{cx-k,cy-r},{cx+k,cy-r},{cx+r,cy-k},{cx+r,cy+k},{cx+k,cy+r},{cx-k,cy+r},{cx-r,cy+k},{cx-r,cy-k}},c)
end
local function debris(l,r,bright)
 local c=P.orange
 for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do
  local x,y=16+sx*r,16+sy*(r-2)
  line(l[4],x,y,x-sx*2,y-sy,c[bright and 3 or 2]);put(l[4],x-sx,y,c[2])
 end end
end
local function impact(f)
 local l=layers(64);local c=P.orange
 if f==1 then
  oct(l[1],6,6,21,21,6,c[2]);diamond(l[2],16,16,8,c[3]);diamond(l[2],16,16,6,white)
  line(l[3],16,2,16,5,c[3]);line(l[3],2,16,5,16,c[3]);line(l[3],27,16,30,16,c[3]);line(l[3],16,27,16,30,c[3])
 elseif f==2 then
  oct(l[1],3,3,27,27,6,c[2]);oct(l[1],4,4,25,25,6,c[3]);oct(l[2],8,8,17,17,5,white)
  ring(l[3],16,16,14,c[2]);debris(l,13,true)
 elseif f==3 then
  ring(l[1],16,16,14,c[2]);ring(l[1],16,16,13,c[3]);oct(l[1],6,6,21,21,5,c[2]);diamond(l[2],16,16,8,c[3]);diamond(l[2],16,16,5,white)
  debris(l,14,true)
 elseif f==4 then
  ring(l[1],16,16,14,c[2]);ring(l[2],16,16,13,c[3]);ring(l[1],16,16,8,c[2]);diamond(l[2],16,16,5,c[3]);diamond(l[2],16,16,2,white)
  debris(l,14,true)
 elseif f==5 then
  for _,pts in ipairs({{{9,3},{16,2},{22,3}},{{29,9},{30,16},{29,22}},{{23,29},{16,30},{10,29}},{{3,23},{2,16},{3,10}}}) do outline(l[1],pts,c[2],false) end
  ring(l[1],16,16,7,c[2]);line(l[2],13,9,17,9,c[3]);line(l[2],23,14,23,17,c[3]);debris(l,14,false)
 else
  -- All six impact frames remain populated. Runtime clears after the final 75 ms.
  line(l[1],10,3,14,2,c[2]);line(l[1],29,11,30,15,c[2]);line(l[1],18,30,22,29,c[2]);line(l[1],2,19,3,23,c[2])
  debris(l,13,true);line(l[2],13,10,16,9,c[2]);line(l[2],21,19,19,21,c[2])
 end
 return l
end
local function ellipsePoints(rx,ry)
 local k=math.max(1,rx-3);local q=math.max(2,math.floor(ry*0.45))
 return {{16-k,16-ry},{16+k,16-ry},{16+rx,16-q},{16+rx,16+q},{16+k,16+ry},{16-k,16+ry},{16-rx,16+q},{16-rx,16-q}}
end
local function aperture(rx,ry,phase)
 local l=layers(64);local c=P.blue
 poly(l[1],ellipsePoints(rx,ry),c[2])
 if rx>=4 and ry>=6 then poly(l[1],ellipsePoints(rx-2,ry-2),c[1]) end
 -- Four-pixel ring, broad broken inner bands and detached alignment brackets.
 local off=phase%3-1
 if rx>=5 then
  line(l[2],16-rx,12,16-rx,18,c[3]);line(l[2],16+rx,14,16+rx,20,c[3])
  local span=rx-3
  for _,y in ipairs({11,16,21}) do
   rect(l[2],16-span+off,y-1,span*2-1,2,c[2]);line(l[2],16-span+off+1,y,16+math.min(2,span-1)+off,y,c[3])
  end
  poly(l[2],{{15+off,8},{18+off,8},{17+off,13},{19+off,13},{17+off,19},{17+off,24},{14+off,24},{15+off,18},{13+off,18},{15+off,12}},c[3])
  line(l[2],16+off,10,16+off,14,white);line(l[2],15+off,18,15+off,22,white)
  for _,sx in ipairs({-1,1}) do
   local x=16+sx*(rx+2)
   line(l[3],x,11,x,21,c[2]);line(l[3],x,11,x-sx,11,c[3]);line(l[3],x,21,x-sx,21,c[3])
  end
  line(l[3],14,16-ry,18,16-ry,c[3]);line(l[3],14,16+ry,18,16+ry,c[3])
 else
  rect(l[2],15,16-ry+2,3,ry*2-3,c[3]);line(l[2],16,16-ry+3,16,16+ry-3,white)
 end
 return l
end
local function portal(tag,f)
 if tag=='Open' then
  local rx={3,5,7,9,10,11,10,10};local ry={7,10,12,13,14,14,14,14}
  return aperture(rx[f],ry[f],f==8 and 0 or f)
 end
 if f==8 then return layers(64) end
 if f==7 then local l=layers(64);rect(l[1],14,12,5,9,P.blue[2]);rect(l[2],15,13,3,7,P.blue[3]);line(l[2],16,14,16,18,white);return l end
 local rx={10,10,9,7,5,3};local ry={14,14,13,12,10,7}
 return aperture(rx[f],ry[f],f==1 and 0 or f)
end
local function redirect(f)
 local l=layers(32);local c=P.blue;if f==4 then return l end
 if f==1 then
  outline(l[1],{{3,5},{8,3},{12,6},{12,10},{8,13},{3,10}},c[2])
  poly(l[2],{{7,4},{12,8},{7,12},{7,10},{3,10},{3,6},{7,6}},c[3]);poly(l[2],{{7,6},{10,8},{7,10},{7,9},{4,9},{4,7},{7,7}},white)
  rect(l[4],1,7,2,2,c[2])
 elseif f==2 then
  -- Displaced arrow echoes retain the original +X direction.
  poly(l[1],{{5,5},{8,8},{5,11},{5,9},{2,9},{2,7},{5,7}},c[2])
  poly(l[2],{{10,5},{14,8},{10,11},{10,9},{7,9},{7,7},{10,7}},c[3]);line(l[2],9,8,13,8,white)
  line(l[3],3,4,6,3,c[1]);line(l[3],9,13,12,12,c[2])
 else
  outline(l[1],{{12,6},{14,8},{12,10}},c[2],false);line(l[2],10,8,13,8,c[3]);rect(l[4],2,7,3,1,c[1]);rect(l[4],7,11,3,1,c[2])
 end
 return l
end
local function clip(tag,ms,mode,fn,peak) return {tag=tag,durations=ms,mode=mode,make=fn,peak=peak} end
local families={
 {id='VFX_Barrage_HeavyImpact',label='HEAVY BARRAGE IMPACT',short='BARRAGE',width=64,height=64,color='orange',clips={clip('Impact',{65,85,90,85,80,75},'visible_tail',impact,2)},completion='All 6 frames contain visible opaque pixels. Hide or recycle at 480ms; do not freeze the debris tail.'},
 {id='VFX_Phase_Portal',label='PHASE PORTAL',short='PORTAL',width=64,height=64,color='blue',clips={clip('Open',{55,55,60,60,70,70,80,110},'hold_last',function(f) return portal('Open',f) end,8),clip('Close',{80,65,60,55,50,45,40,40},'one_shot',function(f) return portal('Close',f) end,2)},completion='Open 560ms, hold its last aperture pose if needed, then Close 435ms. Open/Close tags replace the prior Open/Hold/Close layout.'},
 {id='VFX_Phase_RedirectFlash',label='PHASE REDIRECT FLASH',short='REDIRECT',width=32,height=32,color='blue',clips={clip('Redirect',{45,60,55,40},'one_shot',redirect,1)},completion='Direction remains +X. Blue-white displaced arrow afterimage in frame 2; transparent cleanup in frame 4.'}
}
local function saveJson(path,value) local f=assert(io.open(path,'w'));f:write(json.encode(value));f:close() end
local manifest={generatorId='void-scrapper-system-boss-vfx-revisions-v1',name='SYSTEM BOSS VFX TARGETED REVISIONS',target={width=480,height=270,pixelsPerUnit=32,constructionPixelScale=2,binaryAlpha=true},layers=layerNames,families={},totalFamilies=3,totalClips=4,totalFrames=26,
 preview='SystemBossVFX_Revisions_ContactSheet.png',nativePreview='SystemBossVFX_Revisions_480x270.png',animatedPreview='SystemBossVFX_Revisions_480x270.gif',preservation='Original pack remains unchanged. Only these three copied sources are revised.'}
local clips={};local current,old={},{ }
for _,family in ipairs(families) do
 local w,height=family.width,family.height
 local s=assert(app.open(sourceDir..'/'..family.id..'.aseprite'));assert(s.width==w and s.height==height and #s.layers==4,'Unexpected editable source')
 -- Retain exact source layers/palette/identity while replacing only the copied timeline.
 old[family.id]={}
 for _,tag in ipairs(s.tags) do
  local frames,ms={},{};for i=tag.fromFrame.frameNumber,tag.toFrame.frameNumber do local im=Image(w,height,ColorMode.RGB);im:drawSprite(s,i,Point(0,0));frames[#frames+1]=im;ms[#ms+1]=math.floor(s.frames[i].duration*1000+0.5) end
  old[family.id][tag.name]={frames=frames,durations=ms}
 end
 while #s.tags>0 do s:deleteTag(s.tags[#s.tags]) end
 while #s.frames>1 do s:deleteFrame(s.frames[#s.frames]) end
 while #s.cels>0 do s:deleteCel(s.cels[#s.cels]) end
 for i,name in ipairs(layerNames) do assert(s.layers[i].name==name,'Source layer mismatch');s.layers[i].opacity=255;s.layers[i].isVisible=true end
 local maxN=0;for _,c in ipairs(family.clips) do maxN=math.max(maxN,#c.durations) end
 local atlas=Image(w*maxN,height*#family.clips,ColorMode.RGB);local meta,tags={},{};local index=0
 local record={id=family.id,width=w,height=height,aseprite=family.id..'.aseprite',sheet=family.id..'.png',metadata=family.id..'.json',atlasColumns=maxN,pixelsPerUnit=32,pivotPixels={x=w/2,y=height/2},unityPivot={x=0.5,y=0.5},palette=hex[family.color],clips={},completion=family.completion}
 current[family.id]={}
 for row,c in ipairs(family.clips) do
  local start=index+1;local strip=Image(w*#c.durations,height,ColorMode.RGB);local images={}
  for fi,ms in ipairs(c.durations) do
   index=index+1;if index>1 then s:newEmptyFrame(index) end
   local ls=c.make(fi);local flat=Image(w,height,ColorMode.RGB)
   for li,im in ipairs(ls) do local pixels=scale(im,2);local cel=s:newCel(s.layers[li],index,pixels,Point(0,0));cel.opacity=255;flat:drawImage(pixels,Point(0,0)) end
   s.frames[index].duration=ms/1000;images[fi]=flat;strip:drawImage(flat,Point((fi-1)*w,0));atlas:drawImage(flat,Point((fi-1)*w,(row-1)*height))
   meta[#meta+1]={filename=family.id..'_'..c.tag..'_'..string.format('%02d',fi-1),frame={x=(fi-1)*w,y=(row-1)*height,w=w,h=height},rotated=false,trimmed=false,sourceSize={w=w,h=height},spriteSourceSize={x=0,y=0,w=w,h=height},duration=ms}
  end
  local filename=family.id..(#family.clips>1 and '_'..c.tag or '')..'.png';if #family.clips>1 then strip:saveAs(outputDir..'/'..filename) end
  local rec={tag=c.tag,fromFrame=start,toFrame=index,frames=#c.durations,durationsMs=c.durations,mode=c.mode,sheet=filename,sheetRow=row-1};record.clips[#record.clips+1]=rec
  tags[#tags+1]={name=c.tag,from=start-1,to=index-1,direction='forward'}
  local rendered={family=family,clip=c,frames=images,durations=c.durations};clips[#clips+1]=rendered;current[family.id][c.tag]=rendered
 end
 for _,t in ipairs(tags) do local tag=s:newTag(t.from+1,t.to+1);tag.name=t.name end
 s:saveAs(outputDir..'/'..record.aseprite);s:close();atlas:saveAs(outputDir..'/'..record.sheet)
 saveJson(outputDir..'/'..record.metadata,{frames=meta,meta={app='Aseprite CLI + Lua',image=record.sheet,format='RGBA8888',size={w=atlas.width,h=atlas.height},frameTags=tags},unity={pixelsPerUnit=32,pivot={x=0.5,y=0.5},filterMode='Point',compression='None',mipmaps=false,meshType='FullRect',clips=record.clips}})
 manifest.families[#manifest.families+1]=record
end
local muted=rgba('ADB9B7')
local contact=Image(1120,744,ColorMode.RGB);contact:clear(Color{r=18,g=24,b=32,a=255})
text(contact,'SYSTEM VFX / TARGETED REVISIONS',20,16,white,3);text(contact,'3 SOURCES / 4 CLIPS / 26 FRAMES',20,44,muted,2)
for i,r in ipairs(clips) do
 local y=82+(i-1)*160;text(contact,r.family.label..' / '..r.clip.tag:upper(),20,y,P[r.family.color][3],2)
 text(contact,r.family.width..' X '..r.family.height..' / '..#r.frames..' FRAMES',20,y+22,muted,1)
 for fi,im in ipairs(r.frames) do
  local x=20+(fi-1)*136;local yy=y+38
  for py=0,79 do for px=0,119 do put(contact,x+px,yy+py,(math.floor(px/8)+math.floor(py/8))%2==0 and rgba('222D38') or rgba('2A3540')) end end
  local shown=scale(im,im.width==32 and 2 or 1);contact:drawImage(shown,Point(x+(120-shown.width)//2,yy+(80-shown.height)//2))
  text(contact,'F'..fi..' / '..r.durations[fi]..'MS',x+4,yy+88,muted,1)
 end
end
contact:saveAs(outputDir..'/'..manifest.preview)
local function at(r,t,hold)
 local total=0;for i,d in ipairs(r.durations) do total=total+d;if t<total then return r.frames[i] end end
 if hold then return r.frames[#r.frames] end
end
local function board(t,peak)
 local im=Image(480,270,ColorMode.RGB);im:clear(Color{r=15,g=21,b=29,a=255})
 text(im,'SYSTEM VFX REVISIONS / 480 X 270 / PPU 32',8,6,muted,1)
 text(im,'BEFORE',8,28,muted,1);text(im,'REVISED',8,146,white,1)
 for col,family in ipairs(families) do
  local x=(col-1)*160
  rect(im,x+1,39,158,96,rgba('17212C'));rect(im,x+1,157,158,96,rgba('1C2732'))
  text(im,family.short,x+9,121,muted,1);text(im,family.short,x+9,239,P[family.color][3],1)
  local prev,new
  if col==1 then prev=peak and old[family.id].Impact.frames[2] or at(old[family.id].Impact,t%600);new=peak and current[family.id].Impact.frames[2] or at(current[family.id].Impact,t%600)
  elseif col==2 then
   if peak then prev=old[family.id].Open.frames[6];new=current[family.id].Open.frames[8]
   elseif t<560 then prev=at(old[family.id].Open,t,true);new=at(current[family.id].Open,t,true)
   elseif t<720 then prev=old[family.id].Open.frames[7];new=current[family.id].Open.frames[8]
   else prev=at(old[family.id].Close,t-720);new=at(current[family.id].Close,t-720) end
  else prev=peak and old[family.id].Redirect.frames[1] or at(old[family.id].Redirect,t%400);new=peak and current[family.id].Redirect.frames[1] or at(current[family.id].Redirect,t%400) end
  if prev then im:drawImage(prev,Point(x+(160-family.width)//2,43+(64-family.height)//2)) end
  if new then im:drawImage(new,Point(x+(160-family.width)//2,161+(64-family.height)//2)) end
 end
 return im
end
board(0,true):saveAs(outputDir..'/'..manifest.nativePreview)
local review=Sprite(480,270,ColorMode.RGB);review.layers[1].name='Native Before After Review'
for i=1,60 do if i>1 then review:newEmptyFrame(i) end;review:newCel(review.layers[1],i,board((i-1)*20,false),Point(0,0));review.frames[i].duration=0.02 end
review:saveAs(outputDir..'/'..manifest.animatedPreview);review:close()
saveJson(outputDir..'/manifest.json',manifest)
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('3 revised editable sources / 4 clips / 26 frames.\n');done:close();print('SYSTEM_VFX_REVISIONS_GENERATED')
