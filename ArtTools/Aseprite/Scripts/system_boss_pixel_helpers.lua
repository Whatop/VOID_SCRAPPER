local pc=app.pixelColor
local function put(im,x,y,p) assert(x>=0 and y>=0 and x<im.width and y<im.height,'Pixel outside canvas');im:drawPixel(x,y,p) end
local function rect(im,x,y,w,h,p) for yy=y,y+h-1 do for xx=x,x+w-1 do put(im,xx,yy,p) end end end
local function line(im,x0,y0,x1,y1,p)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0);local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1;local err=dx+dy
 while true do put(im,x0,y0,p);if x0==x1 and y0==y1 then break end;local e=2*err;if e>=dy then err=err+dy;x0=x0+sx end;if e<=dx then err=err+dx;y0=y0+sy end end
end
local function poly(im,points,p)
 local minY,maxY=im.height,-1;for _,q in ipairs(points) do minY=math.min(minY,q[2]);maxY=math.max(maxY,q[2]) end
 for y=minY,maxY do local nodes={};local j=#points
  for i=1,#points do local a,b=points[i],points[j];if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then nodes[#nodes+1]=a[1]+(y-a[2])/(b[2]-a[2])*(b[1]-a[1]) end;j=i end
  table.sort(nodes);for n=1,#nodes-1,2 do for x=math.ceil(nodes[n]),math.floor(nodes[n+1]) do put(im,x,y,p) end end
 end
 for i=1,#points do local a,b=points[i],points[i%#points+1];line(im,a[1],a[2],b[1],b[2],p) end
end
local function pairRect(im,x,y,w,h,p) rect(im,x,y,w,h,p);rect(im,im.width-x-w,y,w,h,p) end
local function pairLine(im,x0,y0,x1,y1,p) line(im,x0,y0,x1,y1,p);line(im,im.width-1-x0,y0,im.width-1-x1,y1,p) end
local function pairPoly(im,points,p) poly(im,points,p);local q={};for _,v in ipairs(points) do q[#q+1]={im.width-1-v[1],v[2]} end;poly(im,q,p) end
local function oct(im,x,y,w,h,c,p) poly(im,{{x+c,y},{x+w-1-c,y},{x+w-1,y+c},{x+w-1,y+h-1-c},{x+w-1-c,y+h-1},{x+c,y+h-1},{x,y+h-1-c},{x,y+c}},p) end
local function diamond(im,cx,cy,r,p)
 for y=math.floor(cy-r),math.ceil(cy+r) do for x=math.floor(cx-r),math.ceil(cx+r) do if math.abs(x-cx)+math.abs(y-cy)<=r then put(im,x,y,p) end end end
end
local function mirror(im) local result=Image(im.width,im.height,ColorMode.RGB);for it in im:pixels() do put(result,im.width-1-it.x,it.y,it()) end;return result end
local function scale(im,n) local result=Image(im.width*n,im.height*n,ColorMode.RGB);for it in im:pixels() do if pc.rgbaA(it())>0 then rect(result,it.x*n,it.y*n,n,n,it()) end end;return result end
local font={A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},Q={'010','101','101','111','011'},R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},['2']={'110','001','010','100','111'},['3']={'110','001','010','001','110'},['4']={'101','101','111','001','001'},['6']={'011','100','110','101','010'},['8']={'010','101','010','101','010'},[' ']={'000','000','000','000','000'},['/']={'001','001','010','100','100'}}
local function text(im,label,x,y,color,n)
 for i=1,#label do local glyph=assert(font[label:sub(i,i)],'Font glyph missing: '..label:sub(i,i));for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then rect(im,x+(i-1)*4*n+(xx-1)*n,y+(yy-1)*n,n,n,color) end end end end
end

return {put=put,rect=rect,line=line,poly=poly,pairRect=pairRect,pairLine=pairLine,pairPoly=pairPoly,oct=oct,diamond=diamond,mirror=mirror,scale=scale,text=text}
