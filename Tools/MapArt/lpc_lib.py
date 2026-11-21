from PIL import Image, ImageDraw
import os, json, math, random
T = os.environ['TEMP']
B = T + '/dl/lpc/x/LPC Base Assets/tiles/'
PROJ = 'C:/Users/user/Desktop/Data/Unity/Project/Paradox/'
OV = PROJ + 'Assets/Resources/MuseumOverlays/'
BOX = json.load(open(T + '/art/n/spr/boxes.json'))
_cache = {}
def sheet(n):
    if n not in _cache: _cache[n] = Image.open(B + n + '.png').convert('RGBA')
    return _cache[n]
def spr(sheetname, i):
    return sheet(sheetname).crop(tuple(BOX[sheetname][i]))
def tile(sheetname, tx, ty, w=1, h=1):
    return sheet(sheetname).crop((tx * 32, ty * 32, (tx + w) * 32, (ty + h) * 32))
def old(name):
    return Image.open(PROJ + 'Assets/Art/Museum/' + name).convert('RGBA')
def ov(name):
    im = Image.open(OV + name + '.png').convert('RGBA')
    return im.crop(im.getbbox()), im.getbbox()
def C(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)
def mix(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))
def put(im, s, x, y): im.alpha_composite(s, (int(x), int(y)))
def glow(im, cx, cy, rx, ry, color, strength):
    layer = Image.new('RGBA', im.size, (0, 0, 0, 0)); px = layer.load()
    for y in range(max(0, int(cy - ry)), min(im.size[1], int(cy + ry))):
        for x in range(max(0, int(cx - rx)), min(im.size[0], int(cx + rx))):
            d = math.hypot((x - cx) / rx, (y - cy) / ry)
            if d < 1: px[x, y] = (color[0], color[1], color[2], int(255 * strength * (1 - d) ** 2))
    im.alpha_composite(layer)
def noise(im, box, amt, rng, density=0.18):
    x0, y0, x1, y1 = box; px = im.load()
    for y in range(y0, y1):
        for x in range(x0, x1):
            if rng.random() < density:
                r, g, b, a = px[x, y]; k = rng.choice((-1, 1)) * rng.randint(2, amt)
                px[x, y] = (max(0, min(255, r + k)), max(0, min(255, g + k)), max(0, min(255, b + k)), a)
def wall_strip(im, y=0, x0=0, x1=512):
    w = spr('inside', 0)
    x = x0
    while x < x1:
        put(im, w, x, y); x += 96
    return w.size[1]
def vignette(im, k=0.8):
    vg = Image.new('RGBA', im.size, (0, 0, 0, 0)); p = vg.load()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            dd = math.hypot((x - 256) / 300, (y - 192) / 250)
            if dd > k: p[x, y] = (6, 6, 12, int(min(150, (dd - k) * 300)))
    im.alpha_composite(vg)
