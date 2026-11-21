from lpc_lib import *
rng = random.Random(5)
im = Image.new('RGBA', (512, 384), C('14151b'))
d = ImageDraw.Draw(im)
WH = 105          # 위쪽 벽 높이
FT, FB = 104, 346 # 바닥 위/아래
# ---- 나무 바닥 (판자 2종 번갈아 + 결)
plank = tile('inside', 0, 4)           # 32x32 나무 타일
for ty in range(FT, FB, 32):
    for tx in range(0, 512, 32):
        im.alpha_composite(plank, (tx, ty))
# 바닥 색 살짝 어둡게/따뜻하게
shade = Image.new('RGBA', (512, FB - FT), (40, 22, 10, 40)); im.alpha_composite(shade, (0, FT))
noise(im, (8, FT, 504, FB), 6, rng, 0.12)
# 하단 가림(바닥 아래 벽)
d.rectangle((0, FB, 511, 383), fill=C('1a2228'))
wall_strip(im, FB - 6 + 6, 0, 512) if False else None
# ---- 위쪽 벽
wall_strip(im, 0)
# 벽 아래 걸레받이 그림자
for i in range(8):
    ov_ = Image.new('RGBA', (512, 1), (8, 6, 4, int(90 * (1 - i / 8)))); im.alpha_composite(ov_, (0, FT + i))
# ---- 양옆 벽: 기둥 + 청록 벽면
side = Image.new('RGBA', (20, FB - FT), C('2b4d55'))
sd = ImageDraw.Draw(side)
sd.rectangle((0, 0, 19, FB - FT), fill=C('3e6e78')); sd.rectangle((16, 0, 19, FB - FT), fill=C('24424a')); sd.rectangle((0, 0, 2, FB - FT), fill=C('6a9aa2'))
put(im, side, 0, FT); put(im, side.transpose(Image.FLIP_LEFT_RIGHT), 492, FT)
# ---- 기둥(벽에 붙임)
for x in (28, 108, 392, 472):
    put(im, spr('inside', 2), x - 8, 0)
# ---- 캐비닛/책장 (벽 앞)
cab = spr('cabinets', 6)           # 160x69 장식장
put(im, cab, 40, WH - 69 + 2)
shelf = spr('cabinets', 0)         # 128x69 책장
put(im, shelf, 346, WH - 69 + 2)
# ---- 액자(기존 그림 재활용)
oldr = old('02_Ceramics_Gallery.png')
paint = oldr.crop((160, 44, 352, 96))   # 가로 풍경화
paint_s = oldr.crop((70, 54, 126, 92))
# ---- 2층 계단: 돌 아치 + 나무 계단
arch = Image.new('RGBA', (128, 105), (0, 0, 0, 0)); ad = ImageDraw.Draw(arch)
ad.rectangle((0, 0, 127, 104), fill=C('0c0e12'))
for i in range(0, 8):
    ad.rectangle((i, i, 127 - i, i + 1), fill=mix(C('d8c9a8'), C('6e6248'), i / 8))
    ad.rectangle((i, i, i + 1, 104), fill=mix(C('d8c9a8'), C('6e6248'), i / 8))
    ad.rectangle((126 - i, i, 127 - i, 104), fill=mix(C('d8c9a8'), C('6e6248'), i / 8))
steps = 6
for i in range(steps):
    y = 98 - i * 12; wd = 100 - i * 9
    ad.rectangle((64 - wd // 2, y, 64 + wd // 2, y + 10), fill=mix(C('b88a5a'), C('3a2616'), i / steps))
    ad.line((64 - wd // 2, y, 64 + wd // 2, y), fill=C('e3b98a'))
    ad.line((64 - wd // 2, y + 10, 64 + wd // 2, y + 10), fill=C('2a1a10'))
put(im, arch, 192, 0)
glow(im, 256, 52, 62, 46, C('ffd78a'), 0.20)
# ---- 횃불
tor = spr('castle_lightsources', 0)
for x in (182, 314):
    put(im, tor, x, 30); glow(im, x + 8, 44, 40, 44, C('ffbb5a'), 0.34)
for x in (140, 368):
    glow(im, x, FT + 40, 56, 26, C('ffcf80'), 0.10)
# ---- 오른쪽 위 지하 계단 입구
b1 = Image.new('RGBA', (66, 46), (0, 0, 0, 0)); bd = ImageDraw.Draw(b1)
bd.rectangle((0, 0, 65, 45), fill=C('0a0b0f'), outline=C('8a8f97'), width=2)
for i in range(5):
    bd.rectangle((3 + i * 2, 5 + i * 7, 62 - i * 2, 9 + i * 7), fill=mix(C('b88a5a'), C('0b0c10'), i / 5))
put(im, b1, 414, FT + 2)
for x in (414, 446, 476): d.rectangle((x, FT + 44, x + 2, FT + 50), fill=C('6a4026'))
d.rectangle((414, FT + 46, 478, FT + 49), fill=C('8a5a34'))
# ---- 붉은 카펫 러너
carpet = tile('castlefloors', 2, 7)
for ty in range(FT + 4, FB, 32):
    for x in (224, 256):
        im.alpha_composite(carpet, (x, ty))
d.rectangle((224, FT + 4, 225, FB), fill=C('f0b04a')); d.rectangle((286, FT + 4, 287, FB), fill=C('f0b04a'))
# ---- 나침반(카펫 위)
cx, cy = 256, 238
def ell(r, col, w=0):
    box = (cx - r, cy - int(r * 0.64), cx + r, cy + int(r * 0.64))
    d.ellipse(box, outline=col, width=w) if w else d.ellipse(box, fill=col)
TR, TRD = C('e6bf6a'), C('9a7438')
ell(88, C('5a2230')); ell(84, C('e8d9b5')); ell(76, C('9f2f41')); ell(72, C('e8d9b5')); ell(48, C('cdbd9c'))
ell(88, TR, 2); ell(48, TRD, 1)
for k in range(8):
    a = k * math.pi / 4; L = 82 if k % 2 == 0 else 52
    pts = [(cx + L * math.cos(a), cy + L * 0.64 * math.sin(a)), (cx + 8 * math.cos(a + math.pi / 2), cy + 8 * 0.64 * math.sin(a + math.pi / 2)), (cx, cy), (cx + 8 * math.cos(a - math.pi / 2), cy + 8 * 0.64 * math.sin(a - math.pi / 2))]
    d.polygon(pts, fill=TR if k % 2 == 0 else TRD)
d.ellipse((cx - 6, cy - 4, cx + 6, cy + 4), fill=C('9f2f41'), outline=TR)
# ---- 양옆 출입구(위·아래 2개씩)
def doorway(y0, y1, left):
    x0, x1 = (0, 24) if left else (488, 512)
    d.rectangle((x0, y0, x1, y1), fill=C('8a5a34'))
    for yy in range(y0, y1, 8): d.line((x0, yy, x1, yy), fill=C('6a4026'))
    glow(im, 4 if left else 508, (y0 + y1) // 2, 34, 44, C('ffcf80'), 0.22)
    xx = 0 if left else 488
    d.rectangle((xx, y0 - 5, xx + 23, y0 - 1), fill=C('d8c9a8')); d.rectangle((xx, y1 + 1, xx + 23, y1 + 5), fill=C('d8c9a8'))
for (a, b) in ((148, 190), (240, 282)):
    doorway(a, b, True); doorway(a, b, False)
# ---- 화분(기존 소품 재활용)
for nm in ('00_Central_Hall__PlantsLeft', '00_Central_Hall__PlantsRight'):
    s, bb = ov(nm); put(im, s, bb[0], bb[1] - 4)
# ---- 아래 출입문
d.rectangle((208, FB - 2, 304, 383), fill=C('0f1218'), outline=C('d8c9a8'), width=2)
d.rectangle((212, FB + 2, 300, 380), fill=C('1d3a4a')); d.line((256, FB - 2, 256, 383), fill=C('d8c9a8'))
vignette(im, 0.8)
im.save(T + '/art/n/out/F1_v2.png')
im.resize((1024, 768), Image.NEAREST).save(T + '/art/n/out/F1_v2_big.png')
print('ok')
