-- Four distinct Raider base structures. All geometry is authored on a 2px grid.
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
local metalHex={'171E25','303941','515F67','78888C','ADB9B7','D6DEDA','F0F2E9'}
local redHex={'662C32','B84445','ED7563'}
local heatHex={'6F3A26','E9913C','FFE0A2'}
local function palette(values) local p={};for i,v in ipairs(values) do p[i]=rgba(v) end;return p end
local m,r,w=palette(metalHex),palette(redHex),palette(heatHex)
local references={};local approvedColors={}
for _,spec in ipairs({{'raider_basic',64},{'raider_shotgun',64},{'raider_sniper_charging',64},{'raider_elite',64},{'raider_assault_commander_idle',128},{'raider_salvage_carrier_idle',128},{'raider_sniper_commander_idle',128}}) do
 local im=assert(Image{fromFile=sourceDir..'/'..spec[1]..'.png'});assert(im.width==spec[2] and im.height==spec[2],'Approved reference changed');references[spec[1]]=im
 for it in im:pixels() do if pc.rgbaA(it())>0 then approvedColors[it()]=true end end
end
for _,p in ipairs({m,r,w}) do for _,v in ipairs(p) do assert(approvedColors[v],'Color outside approved Raider palette') end end
local states={'normal','active','damaged'}
local commonLayers={'Main Hull','Cables','Exposed Machinery','Weapon or Utility Modules','Salvaged Armor','Faction Markings','Lights','Damage','VFX'}
local function newLayers(n) local l={};for i=1,#commonLayers do l[i]=Image(n,n,ColorMode.RGB) end;return l end
local function box(im,x,y,bw,bh,body)
 rect(im,x,y,bw,bh,m[1]);rect(im,x+1,y+1,bw-2,bh-2,body);line(im,x+1,y+1,x+bw-2,y+1,m[6]);line(im,x+1,y+2,x+1,y+bh-3,m[5])
end
local function cable(im,points)
 for i=1,#points-1 do local a,b=points[i],points[i+1];for dx=-1,1 do for dy=-1,1 do line(im,a[1]+dx,a[2]+dy,b[1]+dx,b[2]+dy,m[1]) end end end
 for i=1,#points-1 do local a,b=points[i],points[i+1];line(im,a[1],a[2],b[1],b[2],m[3]) end
end
local function outpost(state)
 local l=newLayers(48);local active=state=='active';local broken=state=='damaged';local im=l[1]
 -- Grounded command bunker with irregular annexes, not an engine-driven warship.
 poly(im,{{15,14},{33,13},{39,20},{39,34},{33,40},{16,41},{10,35},{10,22}},m[1])
 poly(im,{{16,16},{31,15},{37,21},{37,33},{32,38},{17,39},{12,34},{12,23}},m[3])
 rect(im,5,22,10,15,m[1]);rect(im,6,23,8,13,m[3]);rect(im,34,16,9,13,m[1]);rect(im,35,17,7,11,m[2])
 rect(im,14,37,6,5,m[1]);rect(im,15,38,4,3,m[4]);rect(im,29,37,7,4,m[1]);rect(im,30,38,5,2,m[3])
 rect(im,18,35,12,9,m[1]);rect(im,19,36,10,7,m[3]);rect(im,20,41,8,2,m[4])
 im=l[2];cable(im,{{7,18},{5,20},{5,29}});put(im,5,24,r[2]);cable(im,{{38,28},{41,31},{40,35},{35,36}});put(im,40,33,r[1])
 line(im,12,36,17,39,m[2]);line(im,17,39,18,37,r[1]);rect(im,38,30,2,3,r[1])
 im=l[3];rect(im,15,18,19,17,m[1]);rect(im,16,19,17,15,m[2])
 for y=20,32,3 do rect(im,17,y,6,1,m[4]);rect(im,27,y,4,1,m[3]) end
 line(im,24,19,24,28,m[5]);line(im,24,28,29,32,r[1]);line(im,29,32,29,35,r[2]);rect(im,20,35,8,6,m[1]);rect(im,21,36,6,4,m[2])
 im=l[4]
 -- Offset command mast and torn territorial pennant establish base ownership.
 rect(im,35,7,3,12,m[1]);rect(im,36,8,1,10,m[4]);rect(im,32,7,9,3,m[1]);rect(im,33,8,7,1,m[5])
 rect(im,36,4,1,4,m[4]);put(im,36,4,m[6]);rect(im,38,11,4,5,m[1]);rect(im,39,12,2,3,m[3])
 rect(im,8,7,2,14,m[1]);rect(im,9,8,1,11,m[4]);poly(im,{{10,8},{17,9},{15,11},{17,13},{10,12}},r[1]);poly(im,{{10,8},{15,9},{14,10},{15,11},{10,11}},r[2]);put(im,11,8,r[3])
 -- Secondary switchbox, a small roof vent and service entrance.
 box(im,6,24,7,9,m[4]);rect(im,8,26,3,3,m[2]);rect(im,8,30,3,1,m[5]);box(im,35,18,7,8,m[5]);rect(im,37,20,3,4,m[2]);line(im,37,21,39,21,m[4])
 rect(im,20,35,8,7,m[1])
 if active then rect(im,20,35,2,7,m[4]);rect(im,27,35,1,7,m[5]);rect(im,23,37,3,2,r[1]);put(im,24,37,r[3]);rect(im,22,41,4,1,m[3])
 elseif broken then poly(im,{{20,35},{23,35},{27,40},{27,42},{24,41},{21,38}},m[3]);line(im,21,36,25,40,m[5])
 else rect(im,21,36,6,5,m[4]);rect(im,23,36,1,5,m[2]);line(im,21,38,26,38,m[3]) end
 if broken then rect(im,36,4,6,6,0);put(im,36,9,m[3]);rect(im,38,11,4,3,0);line(im,36,10,39,12,m[2]);put(im,39,12,m[5]) end
 im=l[5]
 -- Two overlapping roof plates, an improvised repair strap and unequal eaves.
 poly(im,{{17,16},{27,16},{27,27},{24,32},{16,31},{13,26},{14,21}},m[1])
 poly(im,{{17,18},{25,18},{25,26},{23,30},{17,29},{15,25},{16,21}},m[5]);line(im,17,18,24,18,m[6]);line(im,15,23,15,25,m[6]);put(im,18,19,m[7])
 line(im,16,22,21,27,m[3]);put(im,16,22,m[6]);put(im,21,27,m[6]);rect(im,18,20,4,1,m[4])
 if not broken then
  poly(im,{{28,16},{33,17},{36,22},{35,30},{30,34},{25,32},{27,25}},m[1]);poly(im,{{29,18},{32,19},{34,23},{33,29},{30,32},{27,31},{29,25}},m[5]);line(im,29,18,32,19,m[6]);put(im,33,26,m[4]);rect(im,29,23,3,4,m[3]);line(im,29,23,31,23,m[6])
 else
  poly(im,{{28,16},{33,17},{35,20},{31,21},{29,19},{28,21}},m[4]);line(im,28,16,32,17,m[6]);poly(im,{{31,30},{34,29},{33,33},{28,34},{26,32},{29,31}},m[3]);line(im,28,32,31,32,m[5]);rect(im,20,23,6,7,m[1]);line(im,21,24,24,27,m[3]);put(im,22,26,r[1])
 end
 rect(im,14,32,7,3,m[1]);rect(im,15,32,5,2,m[5]);rect(im,29,34,6,3,m[1]);rect(im,30,34,4,2,m[4])
 im=l[6];rect(im,16,27,3,5,r[2]);line(im,16,27,18,29,r[3]);rect(im,30,18,2,3,r[2]);rect(im,7,31,2,4,r[2]);put(im,7,31,r[3]);rect(im,40,23,2,3,r[1]);rect(im,15,39,4,1,r[2]);rect(im,30,38,4,1,r[2]);rect(im,21,42,5,1,r[1])
 im=l[7];put(im,36,6,active and r[3] or r[1]);rect(im,21,34,6,1,active and r[3] or r[1]);put(im,8,26,active and w[2] or r[1]);rect(im,37,21,2,1,active and r[3] or r[1]);put(im,32,35,r[2])
 if broken then im=l[8];line(im,29,24,32,25,r[2]);line(im,32,25,30,28,r[1]);put(im,31,26,m[5]);line(im,25,29,26,31,m[4]);rect(im,36,39,3,2,m[3]);put(im,36,39,m[6]);rect(im,40,37,2,2,m[4]);put(im,38,42,r[1]);put(im,42,40,m[3]) end
 im=l[9]
 if active then line(im,32,4,33,5,r[2]);line(im,40,4,39,5,r[2]);put(im,35,3,r[3]);rect(im,22,38,1,2,w[2]);put(im,25,37,w[3])
 elseif broken then line(im,31,26,33,27,w[2]);put(im,34,25,w[3]);put(im,34,29,w[2]);put(im,39,37,w[2]) end
 return l
end
local function turret(state)
 local l=newLayers(32);local active=state=='active';local broken=state=='damaged';local im=l[1]
 -- Unequal anchor feet and a fixed turntable, clearly a ground emplacement.
 oct(im,6,13,21,16,4,m[1]);oct(im,8,15,17,12,3,m[3])
 poly(im,{{6,14},{9,15},{8,18},{3,19},{3,16}},m[1]);rect(im,4,16,3,2,m[5])
 poly(im,{{24,15},{28,14},{29,16},{28,20},{25,20}},m[1]);rect(im,26,16,2,2,m[4])
 poly(im,{{8,25},{10,27},{7,30},{3,29},{4,27}},m[1]);line(im,5,28,8,27,m[5])
 poly(im,{{23,25},{26,26},{28,29},{25,30},{22,28}},m[1]);line(im,24,27,26,28,m[4])
 im=l[2];cable(im,{{25,21},{28,22},{28,25},{24,25}});put(im,28,23,r[2]);line(im,8,21,6,23,r[1]);line(im,6,23,9,25,m[4])
 im=l[3];oct(im,8,14,16,13,4,m[4]);oct(im,10,16,12,9,3,m[1]);rect(im,12,18,9,6,m[2]);rect(im,14,21,5,2,m[4]);rect(im,23,17,3,7,m[2])
 for y=17,23,2 do rect(im,24,y,2,1,m[5]) end
 im=l[4];local top=active and 2 or 4
 -- Twin exposed autocannon tubes match Raider direct-fire hardware.
 rect(im,10,top,4,13-top,m[1]);rect(im,11,top+1,2,11-top,m[4]);rect(im,11,top+1,1,8-top,m[6]);rect(im,10,top,4,2,m[1]);rect(im,11,top,2,1,m[5]);put(im,11,top+1,active and w[3] or m[1])
 rect(im,17,top+1,4,12-top,m[1]);rect(im,18,top+2,2,10-top,m[3]);rect(im,18,top+2,1,8-top,m[5]);rect(im,17,top+1,4,2,m[1]);rect(im,18,top+1,2,1,m[4]);put(im,18,top+2,active and w[2] or m[1])
 rect(im,9,10,13,3,m[1]);rect(im,10,10,11,1,m[3]);rect(im,12,11,2,3,m[5]);rect(im,18,11,2,3,m[4])
 box(im,10,13,12,8,m[3]);rect(im,12,14,8,5,m[2]);rect(im,15,14,2,7,m[4]);rect(im,12,15,2,3,active and w[2] or m[1]);rect(im,18,15,2,3,active and w[2] or m[1])
 box(im,24,14,5,8,m[4]);rect(im,25,16,2,4,m[2]);put(im,25,16,m[5])
 if broken then rect(im,17,top+1,4,7,0);put(im,18,9,m[4]);put(im,20,10,m[5]);rect(im,17,14,4,5,m[1]);line(im,18,15,19,17,m[3]);rect(im,26,17,3,4,0);put(im,26,20,m[4]) end
 im=l[5];local left=active and 7 or 8
 poly(im,{{left,13},{left+3,12},{left+5,16},{left+4,22},{left+1,23},{left-1,19}},m[1]);poly(im,{{left+1,14},{left+2,14},{left+3,17},{left+2,21},{left,19}},m[5]);line(im,left+1,14,left+2,16,m[6]);put(im,left+1,20,m[4])
 if not broken then poly(im,{{21,13},{23,14},{24,19},{22,23},{19,22},{20,17}},m[1]);poly(im,{{21,15},{22,16},{22,20},{21,21},{20,21}},m[5]);put(im,21,15,m[6]) else rect(im,20,20,3,3,m[3]);put(im,20,20,m[5]) end
 rect(im,10,24,5,2,m[5]);rect(im,19,24,4,2,m[4]);put(im,10,24,m[6])
 im=l[6];rect(im,9,17,2,3,r[2]);put(im,9,17,r[3]);rect(im,20,21,2,2,r[1]);rect(im,11,24,3,1,r[2]);rect(im,25,15,2,1,r[2]);put(im,25,15,r[3]);rect(im,11,8,1,2,r[1])
 im=l[7];rect(im,14,22,5,1,active and r[3] or r[1]);put(im,26,18,active and w[2] or r[1]);put(im,11,16,active and r[3] or r[1])
 if broken then im=l[8];line(im,19,16,21,17,r[2]);line(im,21,17,20,19,r[1]);rect(im,22,8,2,2,m[3]);put(im,22,8,m[5]);put(im,24,11,m[4]);rect(im,26,26,2,1,m[3]);put(im,27,28,r[1]) end
 im=l[9];if active then rect(im,12,16,1,2,w[3]);put(im,18,15,w[3]);put(im,16,12,r[3]);line(im,5,12,6,13,r[2]);put(im,24,12,r[2]) elseif broken then line(im,20,18,22,19,w[2]);put(im,23,18,w[3]);put(im,23,21,w[2]) end
 return l
end
local function power(state)
 local l=newLayers(32);local active=state=='active';local broken=state=='damaged';local im=l[1]
 -- Generator skid and lateral output lugs; it has no barrel or firing direction.
 poly(im,{{7,9},{11,6},{18,7},{24,11},{26,22},{22,27},{9,26},{6,22}},m[1]);poly(im,{{9,10},{12,8},{17,9},{22,12},{24,21},{21,25},{10,24},{8,21}},m[3])
 rect(im,8,24,5,4,m[1]);rect(im,9,25,3,2,m[4]);rect(im,20,24,5,3,m[1]);rect(im,21,25,3,1,m[5])
 im=l[2];cable(im,{{8,12},{4,12},{3,16},{5,19},{7,18}});put(im,3,15,r[2]);put(im,5,18,m[5])
 cable(im,{{22,12},{27,12},{28,8},{25,6}});put(im,28,9,r[2]);rect(im,24,4,3,3,m[1]);rect(im,25,5,1,1,m[5])
 cable(im,{{13,25},{14,29},{19,29},{21,27}});put(im,17,29,r[2]);put(im,15,29,m[5])
 if broken then rect(im,25,10,4,4,0);line(im,25,6,27,8,m[4]);put(im,27,9,r[2]);put(im,24,12,m[3]) end
 im=l[3];rect(im,11,10,12,13,m[1]);rect(im,12,11,10,11,m[2]);rect(im,12,12,3,8,m[3]);rect(im,20,12,2,9,m[3])
 for y=12,20,3 do rect(im,14,y,6,1,active and w[2] or m[4]);put(im,15,y,active and w[3] or m[5]) end
 line(im,21,13,20,17,r[1]);line(im,20,17,22,20,r[2]);rect(im,16,22,5,2,m[4]);rect(im,17,22,3,1,m[1])
 im=l[4]
 -- Mismatched cylindrical heat exchanger and rectangular switchgear.
 poly(im,{{8,7},{11,6},{14,8},{14,20},{12,23},{8,22},{6,19},{6,10}},m[1]);poly(im,{{9,8},{11,8},{12,10},{12,19},{11,21},{8,19},{8,11}},m[4]);line(im,9,8,11,8,m[6]);line(im,8,12,8,18,m[5])
 for y=11,19,4 do rect(im,7,y,6,1,m[1]);rect(im,8,y,4,1,m[5]) end
 box(im,23,16,5,9,m[4]);rect(im,24,18,2,4,m[2]);rect(im,24,18,1,2,active and r[3] or r[1]);put(im,25,22,m[5])
 -- The sliding vent grille opens sideways to reveal linear windings.
 local gx=active and 12 or 15;rect(im,gx,10,2,12,m[1]);rect(im,gx,10,1,11,m[5]);rect(im,21,10,2,4,m[3]);put(im,21,10,m[6])
 rect(im,11,4,2,3,m[1]);put(im,11,4,m[4]);rect(im,18,6,3,4,m[1]);rect(im,19,7,1,2,m[3])
 if broken then rect(im,23,17,5,5,0);rect(im,23,22,3,2,m[3]);put(im,24,23,m[5]);rect(im,19,6,2,3,0);put(im,19,9,m[4]) end
 im=l[5]
 poly(im,{{13,7},{18,7},{21,10},{20,12},{14,11},{12,9}},m[1]);poly(im,{{14,8},{17,8},{19,10},{15,10},{14,9}},m[5]);line(im,14,8,17,8,m[6])
 if not broken then poly(im,{{20,13},{23,14},{24,21},{21,25},{18,24},{20,21}},m[1]);poly(im,{{21,15},{22,16},{22,20},{20,23},{19,23},{21,20}},m[5]);put(im,21,15,m[6]) else poly(im,{{21,14},{23,14},{23,17},{21,16}},m[3]);put(im,21,14,m[5]);rect(im,20,23,2,2,m[3]) end
 im=l[6];rect(im,8,14,2,3,r[2]);put(im,8,14,r[3]);rect(im,14,8,3,1,r[2]);rect(im,9,25,3,1,r[2]);rect(im,24,23,2,1,r[1]);put(im,17,22,r[2])
 im=l[7];put(im,11,4,active and r[3] or r[1]);put(im,25,5,active and w[3] or r[1]);rect(im,16,23,3,1,active and r[3] or r[1]);put(im,27,21,active and r[3] or r[1])
 if broken then im=l[8];rect(im,16,15,5,5,m[1]);line(im,16,16,18,18,m[4]);line(im,18,18,20,17,r[1]);line(im,20,17,21,20,r[2]);put(im,17,19,m[3]);rect(im,27,26,2,2,m[3]);put(im,27,26,m[5]);put(im,25,29,m[4]) end
 im=l[9];if active then rect(im,18,13,1,2,w[3]);rect(im,18,19,1,2,w[3]);put(im,4,16,r[3]);put(im,27,10,r[3]) elseif broken then line(im,22,18,24,19,w[2]);put(im,25,18,w[3]);put(im,26,20,w[2]);put(im,28,10,w[2]) end
 return l
end
local function storage(state)
 local l=newLayers(48);local active=state=='active';local broken=state=='damaged';local im=l[1]
 -- A cargo rack with three unequal container footprints; no prow, engine or mast.
 -- Separate freight skids leave an open central lane and stepped lower outline.
 rect(im,7,13,14,21,m[1]);rect(im,8,14,12,19,m[3])
 rect(im,23,23,19,13,m[1]);rect(im,24,24,17,11,m[3])
 rect(im,20,18,9,3,m[1]);rect(im,21,19,7,1,m[4]);rect(im,20,31,6,3,m[1]);rect(im,21,32,4,1,m[4])
 rect(im,15,33,5,6,m[1]);rect(im,16,34,3,4,m[3]);rect(im,31,34,5,5,m[1]);rect(im,32,35,3,3,m[3])
 rect(im,6,16,3,5,m[1]);rect(im,6,28,3,6,m[1]);rect(im,39,26,4,5,m[1]);rect(im,35,37,4,3,m[1])
 im=l[2];cable(im,{{9,35},{8,39},{13,41},{17,39}});put(im,11,40,r[2]);cable(im,{{38,18},{42,20},{42,25},{39,27}});put(im,42,22,r[1]);line(im,20,14,23,18,r[1])
 im=l[3];rect(im,9,10,12,23,m[1]);rect(im,10,11,10,21,m[2]);rect(im,24,23,16,12,m[1]);rect(im,25,24,14,10,m[2])
 -- Visible stacked ingots and bundled salvage, not a luminous resource core.
 for _,q in ipairs({{11,14,4,3},{15,18,4,3},{11,23,6,3},{16,27,3,3},{26,26,5,3},{33,27,4,4},{28,31,6,2}}) do
  rect(im,q[1],q[2],q[3],q[4],m[3]);rect(im,q[1],q[2],q[3],1,m[5]);put(im,q[1]+1,q[2]+1,m[4])
 end
 if active then rect(im,11,15,4,2,w[2]);rect(im,11,14,4,1,w[3]);rect(im,15,23,4,2,w[1]);rect(im,15,23,4,1,w[2]);rect(im,27,26,4,2,w[2]);rect(im,27,26,4,1,w[3]) end
 im=l[4]
 -- Small top-right freight box, a long port container, and wide lower storage tray.
 box(im,28,9,13,12,m[4]);rect(im,30,11,2,8,m[2]);rect(im,37,11,2,8,m[2]);rect(im,33,13,3,3,m[3]);put(im,33,13,m[6]);rect(im,34,14,2,1,w[1])
 rect(im,8,9,13,3,m[1]);rect(im,9,10,11,1,m[5]);rect(im,8,31,13,3,m[1]);rect(im,9,32,10,1,m[4]);rect(im,8,12,2,19,m[3]);rect(im,19,12,2,19,m[4])
 rect(im,23,22,18,3,m[1]);rect(im,24,23,16,1,m[5]);rect(im,23,33,18,3,m[1]);rect(im,24,34,16,1,m[4]);rect(im,23,25,2,8,m[3]);rect(im,39,25,2,8,m[3])
 -- Open loot basket remains visible even when main crates are sealed.
 box(im,17,36,16,7,m[3]);rect(im,19,37,12,4,m[1]);rect(im,20,38,4,2,w[1]);rect(im,20,38,4,1,w[2]);rect(im,25,38,4,2,m[4]);rect(im,25,38,4,1,m[6]);put(im,22,40,w[3])
 -- Clamp arms swing outward when inventory is accessible.
 local dx=active and -2 or 0
 rect(im,6+dx,18,3,7,m[1]);rect(im,7+dx,19,1,5,m[5]);rect(im,8+dx,19,3,2,m[1]);put(im,9+dx,19,m[4])
 local rx=active and 42 or 40
 rect(im,rx,27,3,7,m[1]);rect(im,rx+1,28,1,5,m[5]);rect(im,rx-2,31,3,2,m[1]);put(im,rx-1,31,m[4])
 rect(im,13,7,3,4,m[1]);rect(im,14,8,1,2,m[5]);rect(im,34,19,4,4,m[1]);rect(im,35,20,2,2,m[4])
 if broken then rect(im,40,29,3,5,0);put(im,40,30,m[4]);rect(im,35,20,3,3,0);put(im,35,20,m[5]);line(im,38,12,37,16,m[1]) end
 im=l[5]
 if not active and not broken then
  box(im,10,12,9,19,m[5]);rect(im,12,14,1,14,m[3]);rect(im,16,14,1,14,m[3]);rect(im,13,19,3,5,m[4]);line(im,11,25,17,27,m[2]);put(im,11,25,m[6]);put(im,17,27,m[6])
  box(im,25,25,14,8,m[5]);rect(im,27,27,9,1,m[3]);rect(im,27,30,9,1,m[3]);rect(im,32,26,2,6,m[4])
 elseif active then
  -- Sliding lids are parked along the outer rack, exposing valuable contents.
  box(im,6,12,4,19,m[4]);rect(im,7,15,1,12,m[5]);rect(im,7,18,1,4,r[2])
  box(im,25,20,14,5,m[4]);rect(im,27,21,9,1,m[5]);rect(im,32,21,2,3,m[2])
 else
  poly(im,{{10,12},{18,12},{18,17},{15,16},{13,19},{10,17}},m[4]);line(im,11,13,17,13,m[6]);put(im,16,15,m[1]);poly(im,{{11,25},{14,27},{17,26},{18,30},{10,31},{10,28}},m[3]);line(im,11,29,16,29,m[5])
  poly(im,{{25,25},{30,25},{31,28},{28,29},{27,32},{25,32}},m[4]);line(im,26,26,29,26,m[6]);poly(im,{{35,25},{38,25},{38,32},{33,32},{35,29}},m[3]);put(im,37,26,m[5])
 end
 im=l[6];rect(im,9,10,4,1,r[2]);rect(im,10,31,5,2,r[2]);put(im,10,31,r[3]);rect(im,29,10,3,2,r[2]);line(im,29,11,30,12,r[3]);rect(im,38,16,2,3,r[1]);rect(im,24,29,2,4,r[2]);put(im,24,29,r[3]);rect(im,18,40,2,2,r[2]);rect(im,30,40,2,2,r[1])
 im=l[7];put(im,14,8,active and r[3] or r[1]);rect(im,33,13,2,1,active and w[2] or r[1]);put(im,24,24,active and r[3] or r[1]);put(im,39,34,r[2])
 if broken then im=l[8];line(im,15,17,17,20,r[1]);line(im,17,20,16,24,r[2]);line(im,32,27,34,30,m[1]);rect(im,10,38,4,3,m[3]);rect(im,10,38,3,1,m[6]);rect(im,6,40,3,2,w[1]);rect(im,6,40,3,1,w[2]);rect(im,13,43,3,2,m[4]);put(im,13,43,m[6]);put(im,4,37,m[5]);put(im,36,42,m[4]);rect(im,38,38,3,2,m[3]);put(im,38,38,m[6]) end
 im=l[9];if active then put(im,12,15,w[3]);put(im,28,26,w[3]);put(im,18,22,r[3]);put(im,33,15,w[3]) elseif broken then put(im,41,27,w[2]);put(im,43,25,w[3]);line(im,41,28,42,29,w[2]) end
 return l
end
local units={
 {id='raider_outpost',label='OUTPOST',size=96,role='Raider command center with an offset communications mast, territorial pennant and reinforced entrance.',activeLabel='ALERT',moduleLayer='Command and Antenna',make=outpost,front='Entrance faces down / positive Y'},
 {id='raider_turret',label='TURRET',size=64,role='Stationary twin-autocannon defense with fixed anchor feet and external ammunition feed.',activeLabel='ARMED',moduleLayer='Weapon Assembly',make=turret,front='Gun direction is up / negative Y'},
 {id='raider_power_node',label='POWER NODE',size=64,role='Local defense generator with linear windings, cooling equipment and external insulated cable outlets.',activeLabel='ENERGIZED',moduleLayer='Generator and Output Modules',make=power,front='Infrastructure target; no firing direction'},
 {id='raider_storage',label='STORAGE',size=96,role='Stolen salvage cache with three mismatched container footprints, clamps and an open loot basket.',activeLabel='UNSEALED',moduleLayer='Freight and Clamps',make=storage,front='Loading access faces down / positive Y'}
}
local flats,allLayers={},{}
local manifest={generatorId='void-scrapper-raider-base-v1',name='RAIDER BASE STRUCTURES',faction='RAIDER',pixelScale=2,palette={metal=metalHex,factionRed=redHex,industrialHeatAndLoot=heatHex},structures={},
 referencePolicy='Seven approved Raider units and bosses were inspected for palette, materials and construction language. All four structure silhouettes are authored separately in Lua. No ReferenceOnly pixels or downscaled source silhouettes are used.',
 stateTimeline='Three tagged static poses per structure: normal, active, damaged. 200ms each for inspection, not a finished looping animation.',preview='raider_base_comparison.png'}
for _,unit in ipairs(units) do
 flats[unit.id]={};allLayers[unit.id]={};local names={};for i,name in ipairs(commonLayers) do names[i]=i==4 and unit.moduleLayer or name end
 local master=Sprite(unit.size,unit.size,ColorMode.RGB)
 for i,name in ipairs(names) do local layer=i==1 and master.layers[1] or master:newLayer();layer.name=name end
 local record={id=unit.id,label=unit.label,width=unit.size,height=unit.size,role=unit.role,front=unit.front,anchor={x=unit.size/2,y=unit.size/2},layers=names,master=unit.id..'.aseprite',sheet=unit.id..'_states.png',sheetLayout='Three horizontal cells at native size: normal, active, damaged; no padding or trimming.',assets={}}
 for index,state in ipairs(states) do
  local l=unit.make(state);allLayers[unit.id][state]={};if index>1 then master:newEmptyFrame(index) end
  local flat=Image(unit.size,unit.size,ColorMode.RGB)
  for i,im in ipairs(l) do local doubled=scale(im,2);allLayers[unit.id][state][i]=doubled;master:newCel(master.layers[i],index,doubled,Point(0,0));flat:drawImage(doubled,Point(0,0)) end
  master.frames[index].duration=0.2;flats[unit.id][state]=flat;flat:saveAs(outputDir..'/'..unit.id..'_'..state..'.png')
  record.assets[#record.assets+1]={id=state,label=state=='active' and unit.activeLabel or state:upper(),frame=index,png=unit.id..'_'..state..'.png'}
 end
 for index,state in ipairs(states) do local tag=master:newTag(index,index);tag.name=state end
 master:saveAs(outputDir..'/'..record.master);master:close()
 local strip=Image(unit.size*3,unit.size,ColorMode.RGB);for i,state in ipairs(states) do strip:drawImage(flats[unit.id][state],Point((i-1)*unit.size,0)) end;strip:saveAs(outputDir..'/'..record.sheet)
 manifest.structures[#manifest.structures+1]=record
end
local preview=Image(1000,1000,ColorMode.RGB);preview:clear(Color{r=18,g=24,b=32,a=255})
text(preview,'RAIDER BASE STRUCTURES',30,22,m[6],3);text(preview,'COMMAND / DEFEND / POWER / STORE',32,51,r[3],2)
local bgA,bgB=rgba('222D38'),rgba('273441')
for i,unit in ipairs(units) do
 local x=24+(i-1)*244;text(preview,unit.label,x+math.floor((220-#unit.label*8)/2),80,m[5],2)
 for row,state in ipairs(states) do
  local top=106+(row-1)*228
  for y=0,199 do for xx=0,219 do put(preview,x+xx,top+y,(math.floor(xx/12)+math.floor(y/12))%2==0 and bgA or bgB) end end
  local im=scale(flats[unit.id][state],2);preview:drawImage(im,Point(x+math.floor((220-im.width)/2),top+math.floor((200-im.height)/2)))
  local label=state=='active' and unit.activeLabel or state:upper();text(preview,label,x+math.floor((220-#label*8)/2),top+209,row==3 and r[3] or m[4],2)
 end
end
rect(preview,24,805,952,1,m[3]);text(preview,'NATIVE SIZE / NORMAL',30,823,m[4],2)
for i,unit in ipairs(units) do local x=24+(i-1)*244;preview:drawImage(flats[unit.id].normal,Point(x+math.floor((220-unit.size)/2),856));text(preview,unit.size==96 and '96 X 96' or '64 X 64',x+82,968,m[4],2) end
preview:saveAs(outputDir..'/raider_base_comparison.png')
if app.params.qaDir then
 local qa=Image(640,352,ColorMode.RGB);qa:clear(Color{r=18,g=24,b=32,a=255});rect(qa,0,176,640,176,m[6])
 for col,unit in ipairs(units) do for row,state in ipairs(states) do
  local small=Image(unit.size/2,unit.size/2,ColorMode.RGB);local im=flats[unit.id][state]
  for y=0,small.height-1 do for x=0,small.width-1 do put(small,x,y,im:getPixel(x*2,y*2)) end end
  local x=(col-1)*160+16+(48-small.width)/2;local y=18+(row-1)*52
  qa:drawImage(small,Point(x,y));qa:drawImage(small,Point(x,y+176));text(qa,state:upper(),(col-1)*160+72,y+18,m[5],1)
 end;text(qa,unit.label,(col-1)*160+8,5,m[6],1) end
 qa:saveAs(app.params.qaDir..'/readability_half_size.png')
 -- Solid-color masks provide a silhouette-only role comparison at native size.
 local silhouettes=Image(384,96,ColorMode.RGB);silhouettes:clear(Color{r=18,g=24,b=32,a=255})
 for i,unit in ipairs(units) do local im=flats[unit.id].normal;for it in im:pixels() do if pc.rgbaA(it())>0 then put(silhouettes,(i-1)*96+it.x+math.floor((96-unit.size)/2),it.y+math.floor((96-unit.size)/2),m[6]) end end end
 silhouettes:saveAs(app.params.qaDir..'/role_silhouettes.png')
end
local f=assert(io.open(outputDir..'/manifest.json','w'));f:write(json.encode(manifest));f:close()
local done=assert(io.open(outputDir..'/generation_complete.txt','w'));done:write('RAIDER BASE: four structures, three poses each, layered sources, transparent PNGs and comparison.\n');done:close()
print('RAIDER_BASE_GENERATED')
