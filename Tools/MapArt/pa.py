from PIL import Image, ImageDraw, ImageFilter, ImageChops
import random, math
W,H=512,384
def C(h,a=255):
    h=h.lstrip('#');return (int(h[0:2],16),int(h[2:4],16),int(h[4:6],16),a)
def mix(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(4))
def vgrad(im,box,c0,c1):
    x0,y0,x1,y1=box;d=ImageDraw.Draw(im)
    for y in range(y0,y1):
        t=(y-y0)/max(1,y1-y0-1);d.line((x0,y,x1-1,y),fill=mix(c0,c1,t))
def noise(im,box,amt,rng,density=0.18):
    x0,y0,x1,y1=box
    px=im.load()
    for y in range(y0,y1):
        for x in range(x0,x1):
            if rng.random()<density:
                r,g,b,a=px[x,y]
                k=rng.choice((-1,1))*rng.randint(2,amt)
                px[x,y]=(max(0,min(255,r+k)),max(0,min(255,g+k)),max(0,min(255,b+k)),a)
def glow(im,cx,cy,rx,ry,color,strength):
    """부드러운 빛 웅덩이(가산)"""
    layer=Image.new('RGBA',im.size,(0,0,0,0))
    px=layer.load()
    for y in range(max(0,int(cy-ry)),min(im.size[1],int(cy+ry))):
        for x in range(max(0,int(cx-rx)),min(im.size[0],int(cx+rx))):
            d=math.hypot((x-cx)/rx,(y-cy)/ry)
            if d<1:
                a=int(255*strength*(1-d)**2)
                px[x,y]=(color[0],color[1],color[2],a)
    im.alpha_composite(layer)
def shade_ellipse(im,cx,cy,rx,ry,a=70):
    glow(im,cx,cy,rx,ry,(10,8,12),a/255)
def pixel_text(d,x,y,s,col,scale=1):
    F={'2':["111","001","111","100","111"],'F':["111","100","111","100","100"],'B':["110","101","110","101","110"],'1':["010","110","010","010","111"],'N':["101","111","111","101","101"]}
    cx=x
    for ch in s:
        g=F[ch]
        for r,row in enumerate(g):
            for c,v in enumerate(row):
                if v=='1': d.rectangle((cx+c*scale,y+r*scale,cx+c*scale+scale-1,y+r*scale+scale-1),fill=col)
        cx+=4*scale
def column(im,x,y0,y1,w,light,mid,dark,cap=gold if False else None):
    d=ImageDraw.Draw(im)
    d.rectangle((x,y0,x+w-1,y1),fill=mid)
    d.rectangle((x,y0,x+1,y1),fill=light)
    d.rectangle((x+w-3,y0,x+w-1,y1),fill=dark)
