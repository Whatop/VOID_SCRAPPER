-- Exploration objects, not a military faction. New silhouettes; references are comparison-only.
local sourceDir=assert(app.params.sourceDir)
local outputDir=assert(app.params.outputDir)
local h=dofile(assert(app.params.helperPath))
local put,rect,line,poly,oct,scale,text=h.put,h.rect,h.line,h.poly,h.oct,h.scale,h.text
local baseText=text
text=function(im,label,x,y,color,n)
 for i=1,#label do local ch=label:sub(i,i);local px=x+(i-1)*4*n
  if ch=='9' then for yy,row in ipairs({'111','101','111','001','110'}) do for xx=1,3 do if row:sub(xx,xx)=='1' then rect(im,px+(xx-1)*n,y+(yy-1)*n,n,n,color) end end end
  else baseText(im,ch,px,y,color,n) end
 end
end
local pc=app.pixelColor
local function rgba(v) return pc.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),255) end
local metalHex={'171E25','303941','515F67','78888C','ADB9B7','D6DEDA'}
local wornHex={'68533D','AD8C5C','D6BB84'}
local greenHex={'385D52','8AB9A1','D3EADC'}
local purpleHex={'382A50','725194','AF89CC','DDC9EC'}
local heatHex={'6F3A26','E9913C','FFE0A2'}
local warningHex={'73373E','D35B58'}
local blueHex={'2C536F','549AB6','B7DFE9'}
local function palette(values) local p={};for i,v in ipairs(values) do p[i]=rgba(v) end;return p end
local m,o,g,p,w,r,b=palette(metalHex),palette(wornHex),palette(greenHex),palette(purpleHex),palette(heatHex),palette(warningHex),palette(blueHex)
for _,spec in ipairs({{'system_support_blue',64},{'salvage_trading_station_neutral',128},{'raider_power_node_normal',64}}) do
 local im=assert(Image{fromFile=sourceDir..'/'..spec[1]..'.png'});assert(im.width==spec[2] and im.height==spec[2],'Inspected reference dimensions changed')
end
local layerNames={'Main Body','Mechanisms and Cables','Shell and Covers','Energy or Data','Antenna and Ports','Signal and Lights','Damage','VFX'}
local function newLayers(n) local l={};for i=1,#layerNames do l[i]=Image(n,n,ColorMode.RGB) end;return l end
local function box(im,x,y,bw,bh,body)
 rect(im,x,y,bw,bh,m[1]);rect(im,x+1,y+1,bw-2,bh-2,body);line(im,x+1,y+1,x+bw-2,y+1,m[6]);line(im,x+1,y+2,x+1,y+bh-3,m[5])
end
local function pipe(im,points)
 for i=1,#points-1 do local a,bp=points[i],points[i+1];for dx=-1,1 do for dy=-1,1 do line(im,a[1]+dx,a[2]+dy,bp[1]+dx,bp[2]+dy,m[1]) end end end
 for i=1,#points-1 do local a,bp=points[i],points[i+1];line(im,a[1],a[2],bp[1],bp[2],m[4]) end
end
local function rescue(state)
 local l=newLayers(32);local active=state=='signal_active';local done=state=='completed';local im=l[1]
 -- Narrow life capsule with a damaged flank and an offset beacon mast.
 poly(im,{{14,7},{19,7},{23,12},{24,22},{21,28},{14,29},{10,25},{10,13}},m[1])
 poly(im,{{14,9},{18,9},{21,13},{22,21},{20,26},{15,27},{12,24},{12,14}},m[3])
 rect(im,10,17,3,6,m[1]);rect(im,21,22,4,3,m[1]);rect(im,13,27,4,3,m[1]);put(im,14,28,m[4])
 im=l[2];rect(im,14,12,6,13,m[1]);rect(im,15,13,4,11,m[2]);rect(im,15,14,4,2,m[4]);rect(im,15,20,4,3,m[3]);line(im,16,17,16,19,m[4])
 line(im,22,14,24,16,m[1]);line(im,24,16,24,20,m[1]);line(im,24,20,22,22,m[1]);put(im,24,18,o[1]);put(im,22,22,m[4])
 line(im,9,13,11,16,m[2]);line(im,9,12,9,14,m[4])
 im=l[3]
 -- Worn ivory pressure shell, with a hatch that physically opens after rescue.
 poly(im,{{13,9},{15,8},{20,9},{22,13},{21,16},{19,14},{15,13},{12,16},{11,13}},m[1]);poly(im,{{14,10},{18,10},{20,12},{19,13},{15,12},{13,13}},m[5]);line(im,14,10,18,10,m[6]);put(im,19,11,o[2])
 poly(im,{{12,17},{14,18},{14,25},{16,27},{20,26},{21,23},{23,23},{21,28},{15,29},{11,25}},m[1]);line(im,12,18,12,24,m[5]);line(im,13,25,15,27,m[6]);line(im,16,27,19,27,m[5]);rect(im,13,24,1,2,o[2]);put(im,20,26,o[1])
 if not done then
  poly(im,{{15,13},{19,14},{21,18},{20,24},{16,25},{14,22},{14,16}},m[1]);poly(im,{{16,14},{18,15},{19,18},{18,23},{16,23},{15,21},{15,17}},m[5]);line(im,16,14,18,15,m[6]);rect(im,16,18,3,3,g[1]);rect(im,17,17,1,5,g[2]);rect(im,16,19,3,1,g[2])
 else
  -- Empty seating cavity and a displaced lid mark completion rather than destruction.
  poly(im,{{7,16},{10,16},{12,20},{11,25},{8,24},{6,21}},m[1]);poly(im,{{8,17},{9,17},{10,20},{9,23},{8,22},{7,20}},m[5]);put(im,8,17,m[6]);rect(im,8,20,2,1,g[1]);line(im,11,18,13,19,m[4])
 end
 im=l[4];if active then rect(im,16,19,3,1,g[3]);put(im,17,18,g[2]);put(im,17,20,g[2]) elseif done then rect(im,15,22,4,1,m[4]);put(im,18,14,m[5]) end
 im=l[5];rect(im,7,6,3,8,m[1]);rect(im,8,8,1,5,m[4]);rect(im,6,5,5,4,m[1]);rect(im,7,6,3,2,m[3]);rect(im,8,3,1,3,m[4]);put(im,8,3,m[5])
 rect(im,11,10,2,3,m[1]);put(im,12,10,m[4]);rect(im,21,12,3,3,m[1]);put(im,22,13,m[4])
 -- Attached life-support canister makes the rescue pod heavier than a hand-carried recorder.
 poly(im,{{25,14},{27,15},{29,18},{28,23},{26,25},{24,23},{24,17}},m[1]);poly(im,{{26,16},{27,17},{27,21},{26,23},{25,21},{25,18}},m[4]);put(im,26,16,m[6]);line(im,25,18,25,20,m[5]);rect(im,24,18,5,1,m[2]);put(im,26,18,o[2]);rect(im,25,21,3,1,m[2]);put(im,27,22,g[1])
 im=l[6];rect(im,7,6,3,1,active and g[3] or g[1]);put(im,8,7,active and g[2] or (done and g[2] or g[1]));put(im,22,13,done and m[2] or r[2]);rect(im,15,25,3,1,done and g[2] or m[2])
 im=l[7];line(im,19,10,19,12,m[1]);put(im,20,12,m[4]);rect(im,21,17,2,4,m[1]);line(im,21,18,22,19,o[1]);put(im,22,20,m[5]);put(im,13,25,m[3]);put(im,15,28,o[1]);rect(im,25,24,2,2,m[3]);put(im,25,24,m[5])
 im=l[8];if active then
  -- Stepped pale-green radio pulses, confined to the beacon side.
  line(im,4,4,3,6,g[2]);line(im,3,6,3,9,g[2]);line(im,3,9,4,11,g[2]);put(im,2,7,g[3]);line(im,12,4,14,6,g[2]);line(im,14,6,14,8,g[3]);line(im,14,8,12,10,g[2]);put(im,8,2,g[3])
  line(im,16,3,18,6,g[2]);line(im,18,6,18,9,g[3]);line(im,18,9,16,12,g[2])
 end
 return l
end
local function unknown(state)
 local l=newLayers(32);local active=state=='analyzing';local unstable=state=='unstable';local im=l[1]
 -- A hollow, folded triangular ribbon, with unequal floating corner stones.
 -- It has no human rivets, military mounts, corrupt tendrils or broad glitch bands.
 poly(im,{{15,4},{18,4},{28,23},{25,27},{5,27},{3,23}},m[1])
 poly(im,{{16,7},{24,23},{9,23}},0)
 poly(im,{{15,5},{17,5},{26,23},{24,25},{5,25},{5,23}},m[3]);poly(im,{{16,9},{23,22},{10,22}},0)
 im=l[2];line(im,15,10,11,21,m[1]);line(im,11,21,21,21,m[1]);line(im,21,21,18,16,m[1])
 line(im,12,20,18,20,p[1]);line(im,17,14,20,20,m[4]);put(im,17,14,p[2])
 im=l[3];local off=unstable and 2 or 0
 poly(im,{{15,4},{18,4},{20,10},{18,13},{15,9},{12,14},{9,13}},m[4]);line(im,15,4,18,4,m[6]);line(im,15,5,11,12,m[5]);line(im,18,6,19,10,m[3])
 poly(im,{{5,19},{8,18},{9,22},{19,22},{20,25},{5,26},{3,23}},m[4]);line(im,5,19,7,19,m[6]);line(im,6,23,17,23,m[5]);line(im,7,25,16,25,m[3])
 poly(im,{{21+off,12},{23+off,13},{28,22},{26,26},{22,25},{23,22},{19+off,15}},m[3]);line(im,22+off,14,26,22,m[5]);put(im,26,23,m[6]);line(im,25,24,23,24,m[2])
 -- A detached corner above a lower rail sells the impossible-looking join.
 rect(im,7,15,3,2,m[1]);line(im,7,15,9,15,m[5]);put(im,8,16,p[1]);rect(im,20,26,3,2,m[1]);put(im,21,26,m[4])
 im=l[4];local cy=unstable and 15 or 16
 poly(im,{{16,cy-3},{19,cy+1},{15,cy+3},{13,cy}},p[1]);line(im,16,cy-2,18,cy+1,active and p[4] or p[2]);line(im,14,cy,15,cy+2,active and p[3] or p[2]);put(im,16,cy,active and p[3] or p[1])
 if unstable then put(im,17,cy-1,p[4]);line(im,13,cy,14,cy+2,p[3]);put(im,18,cy+1,p[4]) end
 im=l[5];rect(im,12,7,2,2,m[1]);put(im,12,7,m[5]);rect(im,25,18,3,2,m[1]);put(im,27,18,m[4]);rect(im,10,24,4,2,m[1]);line(im,11,24,13,24,m[4])
 im=l[6];line(im,13,9,11,13,active and p[3] or p[1]);line(im,10,23,17,23,active and p[3] or p[1]);line(im,24,18,26,22,active and p[3] or p[1]);put(im,17,7,active and p[4] or p[2]);put(im,21,26,p[2])
 if unstable then line(im,14,8,12,11,p[2]);line(im,23,16,25,20,p[3]);put(im,10,23,p[4]);put(im,17,23,p[4]);put(im,25,24,p[3]) end
 im=l[7];if unstable then
  -- Misalignment is local and controlled; the artifact is still intact.
  rect(im,19,10,2,3,0);line(im,20,12,21,13,p[1]);put(im,22,13,m[6]);put(im,8,25,m[2])
 end
 im=l[8]
 if active then line(im,11,18,13,18,p[3]);line(im,18,13,19,15,p[2]);put(im,17,20,p[4]);line(im,9,11,8,13,p[2]);put(im,23,10,p[3])
 elseif unstable then line(im,11,16,10,18,p[3]);line(im,10,18,12,19,p[4]);line(im,20,16,22,17,p[3]);put(im,23,16,p[4]);rect(im,5,12,2,1,p[2]);put(im,26,11,p[3]);put(im,17,28,p[2]);line(im,18,7,21,8,p[3]) end
 return l
end
local function reactor(state)
 local l=newLayers(48);local active=state=='active';local critical=state=='critical';local dead=state=='destroyed';local im=l[1]
 -- A heavy industrial pressure frame and two broad cooling vessels.
 oct(im,8,10,32,30,4,m[1]);oct(im,10,12,28,26,3,m[3]);rect(im,5,17,38,5,m[1]);rect(im,6,18,36,3,m[3])
 for _,q in ipairs({{8,8},{34,8},{7,36},{35,36}}) do box(im,q[1],q[2],6,5,m[4]) end
 rect(im,16,12,16,26,m[1]);rect(im,17,13,14,24,m[2]);rect(im,19,36,10,3,m[3]);rect(im,20,37,8,1,m[5])
 if dead then rect(im,15,14,18,22,0);poly(im,{{15,34},{18,31},{23,34},{29,31},{33,35},{31,39},{17,39}},m[1]);line(im,19,35,27,35,m[2]) end
 im=l[2]
 if not dead then
  pipe(im,{{10,15},{10,6},{17,6},{17,12}});pipe(im,{{36,15},{38,13},{41,15},{41,30},{36,31}});pipe(im,{{13,32},{13,42},{22,42},{22,38}})
  rect(im,9,9,3,2,m[5]);rect(im,40,21,3,3,m[5]);put(im,13,39,o[2])
  rect(im,16,17,3,16,m[1]);rect(im,30,16,3,17,m[1]);line(im,17,19,17,30,m[4]);line(im,31,17,31,29,m[4]);line(im,29,34,31,36,o[1])
 else
  pipe(im,{{10,15},{10,9},{13,6},{16,7}});pipe(im,{{37,28},{41,29},{41,32},{36,33}});pipe(im,{{13,34},{13,41},{18,42}});line(im,29,34,30,37,o[1]);line(im,18,27,21,30,m[3])
 end
 im=l[3]
 if not dead then
  -- Broad unequal boiler casings with industrial caution bands.
  poly(im,{{8,14},{12,12},{16,16},{16,31},{13,35},{8,33},{6,29},{6,19}},m[1]);poly(im,{{9,15},{11,15},{14,18},{14,29},{12,32},{9,30},{8,27},{8,20}},m[4]);line(im,9,15,11,15,m[6]);line(im,8,21,8,27,m[5]);rect(im,7,19,8,2,m[1]);rect(im,8,19,6,1,m[5]);rect(im,8,29,6,1,m[2])
  poly(im,{{34,14},{38,15},{40,20},{40,30},{37,35},{32,32},{32,19}},m[1]);poly(im,{{35,16},{37,17},{38,21},{38,29},{36,32},{34,30},{34,20}},m[5]);line(im,35,16,37,17,m[6]);rect(im,33,23,6,2,m[1]);rect(im,34,24,4,1,m[4])
  rect(im,14,10,20,4,m[1]);rect(im,15,10,18,2,m[4]);rect(im,16,11,16,1,m[5]);rect(im,17,35,14,3,m[1]);rect(im,18,36,12,1,m[4])
  -- Cage bars stay mechanical; Critical spreads them and exposes the pressure stack.
  local lx,rx=critical and 16 or 18,critical and 31 or 29
  rect(im,lx,15,2,19,m[1]);rect(im,lx,15,1,18,m[5]);rect(im,rx,15,2,19,m[1]);rect(im,rx+1,15,1,17,m[4])
  rect(im,20,14,8,2,m[3]);rect(im,20,33,8,2,m[3]);put(im,20,14,m[6])
  for y=17,31,7 do rect(im,9,y,4,2,o[1]);line(im,9,y,10,y+1,o[3]);line(im,12,y,13,y+1,o[3]) end
  if critical then rect(im,35,17,4,5,m[1]);poly(im,{{35,17},{39,18},{41,22},{38,21}},m[4]);line(im,36,17,39,19,m[6]);line(im,11,25,13,26,m[1]) end
 else
  -- Broken casings and fallen cage segments leave a genuine empty central wreck.
  poly(im,{{8,16},{12,15},{14,19},{12,21},{9,20},{7,23},{6,20}},m[3]);line(im,8,16,12,16,m[5]);put(im,10,18,m[6])
  poly(im,{{7,27},{10,28},{12,26},{15,31},{13,35},{8,33}},m[3]);line(im,9,31,13,32,m[5]);put(im,11,32,o[1])
  poly(im,{{34,24},{36,26},{38,24},{40,30},{37,35},{33,32}},m[3]);line(im,35,29,37,32,m[5]);put(im,38,30,m[4])
  poly(im,{{15,10},{20,10},{23,13},{21,15},{17,13},{14,13}},m[3]);line(im,15,10,20,10,m[5]);poly(im,{{26,13},{31,10},{34,12},{31,15},{29,15}},m[3]);put(im,31,11,m[5])
  line(im,18,31,24,35,m[4]);line(im,23,19,29,26,m[3]);put(im,26,22,m[5]);rect(im,18,36,11,1,m[3])
 end
 im=l[4]
 if not dead then
  local x=critical and 19 or 21;local wide=critical and 10 or 6
  rect(im,x,16,wide,17,m[1])
  for y=17,29,4 do
   rect(im,x+1,y,wide-2,3,critical and r[2] or (active and w[2] or w[1]));rect(im,x+2,y,wide-4,1,critical and w[3] or (active and w[3] or m[3]));rect(im,x+1,y+2,wide-2,1,critical and w[2] or (active and w[1] or m[2]))
  end
  if critical then line(im,24,17,22,22,w[3]);line(im,22,22,25,26,w[3]);line(im,25,26,23,30,w[3]);put(im,27,31,w[2]) end
 else put(im,23,35,w[1]);put(im,25,34,w[2]);put(im,21,32,w[1]) end
 im=l[5]
 if not dead then box(im,34,8,8,6,m[3]);rect(im,36,10,4,2,m[1]);rect(im,5,25,3,4,m[1]);rect(im,6,26,1,2,m[4]);rect(im,27,6,3,5,m[1]);rect(im,28,7,1,3,m[3])
 else rect(im,35,9,4,3,m[2]);put(im,36,9,m[5]);rect(im,27,8,3,2,m[1]);put(im,28,8,m[4]) end
 im=l[6]
 if not dead then
  -- Five pressure/countdown segments: dark, three amber, then all five warning red.
  for i=0,4 do rect(im,17+i*3,11,2,1,critical and r[2] or (active and i<3 and w[2] or m[2])) end
  rect(im,36,10,3,1,critical and r[2] or (active and w[2] or m[2]));put(im,28,7,critical and r[2] or (active and w[2] or m[2]))
  for _,q in ipairs({{9,9},{36,9},{8,38},{37,38}}) do rect(im,q[1],q[2],3,1,critical and r[2] or m[2]) end
  if critical then rect(im,7,22,2,3,r[2]);rect(im,38,26,2,3,r[2]);rect(im,19,36,10,1,r[1]) end
 end
 im=l[7]
 if critical then line(im,36,18,34,21,m[1]);line(im,34,21,35,22,o[1]);put(im,39,22,m[3]);line(im,17,31,19,32,m[1]);put(im,12,32,m[2])
 elseif dead then
  rect(im,4,34,3,2,m[3]);put(im,4,34,m[5]);rect(im,41,37,3,3,m[2]);put(im,42,37,m[5]);rect(im,30,41,4,2,m[3]);put(im,30,41,m[5]);put(im,38,42,o[1]);poly(im,{{20,24},{23,23},{24,26},{21,28},{18,26}},m[2]);put(im,20,24,m[4]);put(im,26,17,m[3])
 end
 im=l[8]
 if active then put(im,15,16,w[2]);put(im,32,28,w[2])
 elseif critical then
  line(im,15,16,13,14,w[2]);put(im,12,12,w[3]);line(im,32,19,34,17,w[2]);put(im,35,16,w[3]);line(im,30,31,33,33,w[2]);put(im,34,35,w[3]);line(im,17,28,15,30,w[2]);put(im,14,32,w[3]);put(im,24,5,r[2]);put(im,24,40,r[2])
 elseif dead then put(im,39,38,w[1]);put(im,20,36,w[1]) end
 return l
end
local function blackbox(state)
 local l=newLayers(32);local active=state=='data_recovery';local done=state=='recovered';local im=l[1]
 -- Small portable recorder in a shallow mounting cradle. Recovery removes the case.
 poly(im,{{9,18},{24,18},{26,21},{24,26},{10,26},{8,23}},m[1]);rect(im,10,20,14,5,m[3]);rect(im,11,20,12,3,m[1]);rect(im,10,24,14,1,m[4])
 if done then rect(im,11,19,13,4,0) end
 rect(im,8,20,3,4,m[1]);rect(im,9,21,1,2,m[5]);rect(im,24,20,3,4,m[1]);rect(im,25,21,1,2,m[4])
 im=l[2];line(im,25,23,28,25,m[1]);line(im,28,25,26,28,m[1]);put(im,28,26,m[4]);put(im,26,28,o[1]);rect(im,22,24,3,2,m[1]);put(im,23,24,m[5])
 if done then for x=12,21,3 do rect(im,x,20,1,3,m[5]) end;rect(im,14,24,5,1,m[2]);line(im,24,24,23,26,m[3]);put(im,23,26,m[5]) end
 im=l[3]
 if not done then
  oct(im,8,11,18,13,2,m[1]);oct(im,10,12,14,10,1,m[3]);rect(im,11,13,12,8,m[2]);line(im,11,12,22,12,m[5]);rect(im,10,15,1,4,o[2]);rect(im,23,17,1,3,o[1]);put(im,23,17,o[3])
  -- Reinforced corners and a carrying handle make the recorder visibly portable.
  rect(im,9,12,3,2,m[4]);put(im,10,12,m[6]);rect(im,22,12,3,2,m[4]);rect(im,9,21,3,2,m[4]);rect(im,22,21,3,2,m[4])
  rect(im,12,7,9,4,m[1]);rect(im,14,9,5,2,0);rect(im,13,8,7,1,m[5]);put(im,13,9,m[3]);put(im,20,9,m[3])
  if active then
   -- Sliding data cover uncovers banks of recorder cells.
   box(im,5,13,5,10,m[3]);rect(im,6,15,2,5,m[2]);put(im,6,14,o[2])
  else rect(im,12,14,10,6,m[3]);rect(im,13,15,7,1,m[4]);rect(im,13,17,4,1,m[5]);rect(im,18,17,3,1,m[2]);rect(im,13,19,8,1,m[2]) end
 end
 im=l[4]
 if active then
  rect(im,12,14,10,7,m[1]);for y=15,19,2 do for x=13,19,3 do rect(im,x,y,2,1,(x+y)%3==0 and b[3] or b[2]) end end;rect(im,22,15,1,4,b[1])
 end
 im=l[5]
 if not done then
  rect(im,25,10,2,9,m[1]);rect(im,26,11,1,7,m[4]);rect(im,26,7,1,4,m[3]);rect(im,25,6,3,2,m[1]);put(im,26,6,m[5])
  rect(im,14,22,7,3,m[1]);for x=15,19,2 do rect(im,x,23,1,1,m[5]) end
 else rect(im,22,24,4,2,m[2]);put(im,22,24,m[5]);put(im,24,25,m[4]) end
 im=l[6];if not done then put(im,26,7,active and b[3] or b[1]);rect(im,12,20,3,1,active and b[3] or b[1]);put(im,22,13,active and b[2] or b[1]) end
 im=l[7];if not done then line(im,20,13,21,14,m[1]);put(im,21,15,m[4]);put(im,12,22,m[2]) else put(im,11,24,m[2]);line(im,19,23,21,24,m[2]) end
 im=l[8];if active then
  -- Discrete data packets, visually different from the rescue beacon's radio arcs.
  rect(im,28,10,2,1,b[2]);rect(im,28,13,2,1,b[3]);rect(im,28,16,2,1,b[2]);rect(im,23,5,2,1,b[2]);put(im,27,3,b[3]);put(im,23,9,b[1])
 end
 return l
end
local units={
 {id='rescue_signal',label='RESCUE SIGNAL',size=64,role='Damaged rescue capsule with an emergency beacon; completion opens the hatch and silences the signal.',make=rescue,palettes={metalHex,wornHex,greenHex,warningHex},accent=greenHex,states={{id='idle',label='IDLE'},{id='signal_active',label='SIGNAL ACTIVE'},{id='completed',label='COMPLETED'}}},
 {id='unknown_device',label='UNKNOWN DEVICE',size=64,role='Hollow folded artifact with restrained purple energy and floating misaligned joins; not a fully corrupted NULL object.',make=unknown,palettes={metalHex,purpleHex},accent=purpleHex,states={{id='dormant',label='DORMANT'},{id='analyzing',label='ANALYZING'},{id='unstable',label='UNSTABLE'}}},
 {id='unstable_reactor',label='UNSTABLE REACTOR',size=96,role='Heavy industrial pressure reactor with containment rods, cooling vessels and five escalating warning segments.',make=reactor,palettes={metalHex,wornHex,heatHex,warningHex},accent=heatHex,states={{id='dormant',label='DORMANT'},{id='active',label='ACTIVE'},{id='critical',label='CRITICAL / OVERLOAD'},{id='destroyed',label='DESTROYED'}}},
 {id='black_box',label='BLACK BOX',size=64,role='Portable reinforced data recorder with carrying handle, ports and packet indicators; recovery leaves an empty mounting cradle.',make=blackbox,palettes={metalHex,wornHex,blueHex},accent=blueHex,states={{id='dormant',label='DORMANT'},{id='data_recovery',label='DATA RECOVERY'},{id='recovered',label='RECOVERED'}}}
}
local flats,allLayers={},{}
local manifest={generatorId='void-scrapper-field-events-v1',name='FIELD EVENT OBJECTS',family='Exploration objects / no military faction',pixelScale=2,objects={},
 referencePolicy='Current SYSTEM, Raider and Neutral art was inspected for world scale, contrast and material consistency. No reference pixels or silhouettes are copied. These are independently authored exploration objects with purpose-specific accents.',
 stateTimeline='Single-frame tagged state poses, 200ms each for inspection; not finished looping animations or an actual gameplay timer.',preview='field_events_comparison.png'}
for _,unit in ipairs(units) do
 flats[unit.id]={};allLayers[unit.id]={};local master=Sprite(unit.size,unit.size,ColorMode.RGB)
 for i,name in ipairs(layerNames) do local layer=i==1 and master.layers[1] or master:newLayer();layer.name=name end
 local paletteHex={};for _,list in ipairs(unit.palettes) do for _,v in ipairs(list) do paletteHex[#paletteHex+1]=v end end
 local record={id=unit.id,label=unit.label,width=unit.size,height=unit.size,role=unit.role,anchor={x=unit.size/2,y=unit.size/2},layers=layerNames,palette=paletteHex,accent=unit.accent,master=unit.id..'.aseprite',sheet=unit.id..'_states.png',sheetLayout='Horizontal native-size cells in the listed state order; no padding or trimming.',assets={}}
 for index,state in ipairs(unit.states) do
  local l=unit.make(state.id);allLayers[unit.id][state.id]={};if index>1 then master:newEmptyFrame(index) end
  local flat=Image(unit.size,unit.size,ColorMode.RGB)
  for i,im in ipairs(l) do local doubled=scale(im,2);allLayers[unit.id][state.id][i]=doubled;master:newCel(master.layers[i],index,doubled,Point(0,0));flat:drawImage(doubled,Point(0,0)) end
  master.frames[index].duration=0.2;flats[unit.id][state.id]=flat;flat:saveAs(outputDir..'/'..unit.id..'_'..state.id..'.png')
  record.assets[#record.assets+1]={id=state.id,label=state.label,frame=index,png=unit.id..'_'..state.id..'.png'}
 end
 for index,state in ipairs(unit.states) do local tag=master:newTag(index,index);tag.name=state.id end
 master:saveAs(outputDir..'/'..record.master);master:close()
 local strip=Image(unit.size*#unit.states,unit.size,ColorMode.RGB);for i,state in ipairs(unit.states) do strip:drawImage(flats[unit.id][state.id],Point((i-1)*unit.size,0)) end;strip:saveAs(outputDir..'/'..record.sheet)
 manifest.objects[#manifest.objects+1]=record
end
local preview=Image(1000,1060,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'FIELD EVENT OBJECTS',30,22,m[6],3);text(preview,'RESCUE / DISCOVER / SURVIVE / RECOVER',32,51,m[4],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for col,unit in ipairs(units) do
 local x=24+(col-1)*244;local accent=rgba(unit.accent[#unit.accent]);text(preview,unit.label,x+math.floor((220-#unit.label*8)/2),80,accent,2)
 for row,state in ipairs(unit.states) do
  local top=106+(row-1)*228
  for y=0,199 do for xx=0,219 do put(preview,x+xx,top+y,(math.floor(xx/12)+math.floor(y/12))%2==0 and bgA or bgB) end end
  local im=scale(flats[unit.id][state.id],2);preview:drawImage(im,Point(x+math.floor((220-im.width)/2),top+math.floor((200-im.height)/2)))
  text(preview,state.label,x+math.floor((220-#state.label*8)/2),top+209,accent,2)
 end
 if #unit.states==3 then
  text(preview,'NATIVE / 64 X 64',x+50,816,m[4],2);preview:drawImage(flats[unit.id][unit.states[1].id],Point(x+78,857))
  local note=col==1 and 'HATCH OPENS ON COMPLETION' or (col==2 and 'HOLLOW FOLDED GEOMETRY' or 'RECORDER LEAVES EMPTY CRADLE')
  text(preview,note,x+math.floor((220-#note*4)/2),951,m[4],1)
 end
end
rect(preview,24,1030,952,1,m[3]);text(preview,'HARD PIXELS / FIXED CANVASES / REACTOR 96 X 96',30,1042,m[4],1)
preview:saveAs(outputDir..'/field_events_comparison.png')
if app.params.qaDir then
 local qa=Image(640,472,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,236,640,236,m[6])
 for col,unit in ipairs(units) do
  for row,state in ipairs(unit.states) do
   local small=Image(unit.size/2,unit.size/2,ColorMode.RGB);local im=flats[unit.id][state.id]
   for y=0,small.height-1 do for x=0,small.width-1 do put(small,x,y,im:getPixel(x*2,y*2)) end end
   local x=(col-1)*160+16+(48-small.width)/2;local y=20+(row-1)*52
   qa:drawImage(small,Point(x,y));qa:drawImage(small,Point(x,y+236));text(qa,state.label,(col-1)*160+70,y+18,m[5],1)
  end
  text(qa,unit.label,(col-1)*160+8,5,m[6],1)
 end
 qa:saveAs(app.params.qaDir..'/readability_half_size.png')
 local silhouettes=Image(384,96,ColorMode.RGB);silhouettes:clear(Color{r=18,g=24,b=32,a=255})
 for i,unit in ipairs(units) do local im=flats[unit.id][unit.states[1].id];for it in im:pixels() do if pc.rgbaA(it())>0 then put(silhouettes,(i-1)*96+it.x+math.floor((96-unit.size)/2),it.y+math.floor((96-unit.size)/2),m[6]) end end end
 silhouettes:saveAs(app.params.qaDir..'/event_silhouettes.png')
end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('FIELD EVENTS: four objects, thirteen state poses, layered sources, transparent PNGs and comparison.\n');done:close()
print('FIELD_EVENTS_GENERATED')
