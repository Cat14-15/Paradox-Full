from lpc_lib import *
BIG = T + '/dl/pk_Big_32x32_Tileset/Big 32x32 Tileset/Separated Sprites/'
idx = json.load(open(T + '/art/n/bigidx.json'))
def bt(n):
    return Image.open(BIG + idx[str(n)].replace('\\', '/')).convert('RGBA')
rng = random.Random(9)
im = Image.new('RGBA', (512, 384), C('14151b'))
d = ImageDraw.Draw(im)
FT, FB = 96, 352
# ---- 바닥: 밝은 타일(937/1017 번갈아) + 따뜻한 톤
f0, f1 = bt(1017), bt(938)
for r in range(3, 11):
    for c in range(0, 16):
        put(im, f0 if (r + c) % 2 == 0 else f1, c * 32, r * 32)
warm = Image.new('RGBA', (512, 256), (200, 150, 90, 38)); im.alpha_composite(warm, (0, FT))
noise(im, (0, FT, 512, FB), 5, rng, 0.10)
# ---- 벽: 크림 벽돌 + 석재 걸레받이 (위 3줄), 아래 벽 1줄, 양옆
top = [bt(285), bt(325), bt(365)]
for r in range(3):
    for c in range(16): put(im, top[r], c * 32, r * 32)
for c in range(16): put(im, bt(365), c * 32, 352)
for r in range(3, 11):
    put(im, bt(325), 0, r * 32); put(im, bt(325), 480, r * 32)
for r in range(3, 11):
    put(im, bt(366), 0, r * 32) if False else None
# 바닥 가장자리 그림자
for i in range(10):
    s = Image.new('RGBA', (512, 1), (30, 20, 10, int(80 * (1 - i / 10)))); im.alpha_composite(s, (0, FT + i))
# ---- 액자(기존 그림 재활용) + 포스터
oldr = old('02_Ceramics_Gallery.png')
big_paint = oldr.crop((160, 44, 352, 96)); put(im, big_paint.resize((128, 34), Image.NEAREST), 192, 20)
for n, x in ((753, 60), (754, 108), (833, 372), (834, 420)):
    p = bt(n); put(im, p, x, 14)
# ---- 위쪽 가운데: 2층 계단 (큰 문틀)
arch = Image.new('RGBA', (128, 96), (0, 0, 0, 0)); ad = ImageDraw.Draw(arch)
ad.rectangle((0, 0, 127, 95), fill=C('0c0e12'), outline=C('b9a98a'), width=3)
for i in range(6):
    y = 88 - i * 13; wd = 104 - i * 12
    ad.rectangle((64 - wd // 2, y, 64 + wd // 2, y + 11), fill=mix(C('c9b48a'), C('3a3026'), i / 6))
    ad.line((64 - wd // 2, y, 64 + wd // 2, y), fill=C('efe0bc'))
put(im, arch, 192, 0)
glow(im, 256, 48, 64, 40, C('ffd78a'), 0.24)
# 층 표지
d.rectangle((226, 4, 286, 16), fill=C('1a1710'), outline=C('e6bf6a'))
# ---- 지하 계단 입구
b1 = Image.new('RGBA', (66, 46), (0, 0, 0, 0)); bd = ImageDraw.Draw(b1)
bd.rectangle((0, 0, 65, 45), fill=C('0a0b0f'), outline=C('8a8f97'), width=2)
for i in range(5):
    bd.rectangle((3 + i * 2, 5 + i * 7, 62 - i * 2, 9 + i * 7), fill=mix(C('9aa0a8'), C('0b0c10'), i / 5))
put(im, b1, 414, FT + 2)
# ---- 붉은 러너 카펫 (4칸 폭, 세로)
rug_c = [bt(1223), bt(1224)]
for r in range(3, 11):
    for c in (7, 8):
        put(im, bt(1223) if c == 7 else bt(1224), c * 32, r * 32)
# ---- 나침반(원래 맵 것)을 잘라 재활용
oc = old('00_Central_Hall.png').crop((160, 216, 354, 338))
put(im, oc, 256 - 97, 238 - 61)
# ---- 양옆 출입구: 나무 문틀 + 따뜻한 빛
def doorway(y0, y1, left):
    x0, x1 = (0, 32) if left else (480, 512)
    d.rectangle((x0, y0, x1 - 1, y1), fill=C('b9a98a'))
    for yy in range(y0, y1, 8): d.line((x0, yy, x1, yy), fill=C('9d8e72'))
    glow(im, 4 if left else 508, (y0 + y1) // 2, 36, 46, C('ffcf80'), 0.24)
    xx = 0 if left else 480
    d.rectangle((xx, y0 - 6, xx + 31, y0 - 1), fill=C('6b4630')); d.rectangle((xx, y1 + 1, xx + 31, y1 + 6), fill=C('6b4630'))
for (a, b) in ((140, 188), (236, 284)):
    doorway(a, b, True); doorway(a, b, False)
# ---- 화분 + 조명
for nm in ('00_Central_Hall__PlantsLeft', '00_Central_Hall__PlantsRight'):
    s, bb = ov(nm); put(im, s, bb[0], bb[1] - 14)
for x in (96, 160, 352, 416):
    glow(im, x, 40, 40, 46, C('ffc974'), 0.24)
# 아래 입구 유리문
d.rectangle((208, FB - 2, 304, 383), fill=C('0f1218'), outline=C('d8c9a8'), width=2)
d.rectangle((212, FB + 2, 300, 380), fill=C('1d3a4a')); d.line((256, FB - 2, 256, 383), fill=C('d8c9a8'))
vignette(im, 0.82)
im.save(T + '/art/n/out/F1_v3.png')
im.resize((1024, 768), Image.NEAREST).save(T + '/art/n/out/F1_v3_big.png')
print('ok')
