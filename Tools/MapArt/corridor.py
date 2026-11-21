from pa import *
import json, os
OUT = os.environ['TEMP'] + '/art/n/out/'
os.makedirs(OUT, exist_ok=True)
rng = random.Random(7)
im = Image.new('RGBA', (W, H), C('15161c'))
d = ImageDraw.Draw(im)
WALL0, WALL1 = C('2c3748'), C('3b4a5f')
TRIM = C('d8b46a'); TRIM_D = C('8f6c34')
WOOD = C('6b4630'); WOOD_L = C('8d6040'); WOOD_D = C('46291d')
M0, M1, MJ = C('e3d6bd'), C('d1c3a6'), C('a99a7c')
FL_T, FL_B = 116, 342

# ---------- 바닥 (대리석 체크)
for ty in range(FL_T, FL_B, 32):
    for tx in range(32, 480, 32):
        base = M0 if ((tx // 32 + (ty - FL_T) // 32) % 2 == 0) else M1
        d.rectangle((tx, ty, min(tx + 31, 479), min(ty + 31, FL_B - 1)), fill=base)
noise(im, (32, FL_T, 480, FL_B), 7, rng, 0.22)
for x in range(32, 480, 32): d.line((x, FL_T, x, FL_B - 1), fill=MJ)
for y in range(FL_T, FL_B, 32): d.line((32, y, 479, y), fill=MJ)
for _ in range(90):
    x = rng.randint(34, 470); y = rng.randint(FL_T + 2, FL_B - 6); l = rng.randint(6, 16)
    c = mix(M1, MJ, 0.35)
    for i in range(l):
        if 32 <= x + i < 480 and y + i // 3 < FL_B: im.putpixel((x + i, y + i // 3), c)

# ---------- 위쪽 벽
vgrad(im, (8, 8, 504, 24), C('efe0bd'), C('b99a62'))
d.rectangle((8, 24, 503, 26), fill=TRIM_D)
vgrad(im, (8, 27, 504, 92), WALL1, WALL0)
for x in range(40, 480, 64):
    d.rectangle((x, 34, x + 55, 86), outline=mix(WALL0, C('000000'), 0.25))
    d.line((x + 1, 35, x + 54, 35), fill=mix(WALL1, C('ffffff'), 0.12))
noise(im, (8, 27, 504, 92), 4, rng, 0.1)
d.rectangle((8, 92, 503, 94), fill=TRIM)
vgrad(im, (8, 95, 504, FL_T), WOOD_L, WOOD)
for x in range(40, 480, 64): d.rectangle((x, 99, x + 55, FL_T - 5), outline=WOOD_D)
d.rectangle((8, FL_T - 3, 503, FL_T - 1), fill=WOOD_D)

# ---------- 양옆 벽
for sx in (8, 480):
    vgrad(im, (sx, 26, sx + 24, FL_B + 2), WALL0, mix(WALL0, C('000000'), 0.3))
    d.rectangle((sx, 26, sx + 23, FL_B), outline=C('0f1015'))
    if sx == 8: d.rectangle((sx + 20, 26, sx + 23, FL_B), fill=WOOD_D)
    else: d.rectangle((sx, 26, sx + 3, FL_B), fill=WOOD_D)
# ---------- 아래 벽
vgrad(im, (8, FL_B, 504, 376), WALL0, C('1b2029'))
d.rectangle((8, FL_B, 503, FL_B + 3), fill=WOOD_D)
d.rectangle((8, FL_B + 3, 503, FL_B + 5), fill=TRIM_D)

# ---------- 기둥
def pillar(x):
    d.rectangle((x, 26, x + 19, FL_T), fill=C('c9bda3'))
    d.rectangle((x, 26, x + 4, FL_T), fill=C('efe4cc')); d.rectangle((x + 15, 26, x + 19, FL_T), fill=C('8d8068'))
    for yy in range(34, FL_T - 8, 6): d.line((x + 6, yy, x + 13, yy), fill=C('b3a68a'))
    d.rectangle((x - 3, 26, x + 22, 34), fill=TRIM); d.rectangle((x - 3, 34, x + 22, 36), fill=TRIM_D)
    d.rectangle((x - 3, FL_T - 8, x + 22, FL_T - 1), fill=C('b3a68a')); d.rectangle((x - 3, FL_T - 9, x + 22, FL_T - 8), fill=C('efe4cc'))
for x in (44, 148, 344, 448): pillar(x)

# ---------- 2층 계단 아치
d.rectangle((196, 30, 316, FL_T), fill=C('0d0f14'))
for i in range(0, 10):
    d.rectangle((196 + i, 30 + i, 316 - i, 31 + i), fill=mix(C('d8c9a8'), C('6e6248'), i / 10))
d.arc((196, 30, 316, 150), 180, 360, fill=TRIM, width=3)
vgrad(im, (206, 48, 306, FL_T), C('2a2417'), C('05060a'))
for i in range(6):
    y = FL_T - 4 - i * 9; wd = 96 - i * 8
    d.rectangle((256 - wd // 2, y, 256 + wd // 2, y + 7), fill=mix(C('d9c9a2'), C('3a3427'), i / 6))
    d.line((256 - wd // 2, y, 256 + wd // 2, y), fill=C('f4e8c8'))
glow(im, 256, 70, 70, 50, C('ffd78a'), 0.28)
d.rectangle((236, 36, 276, 48), fill=C('1a1710'), outline=TRIM)
pixel_text(d, 246, 38, "2F", TRIM)
d.rectangle((196, FL_T, 316, FL_T + 2), fill=TRIM_D); d.line((196, FL_T, 316, FL_T), fill=TRIM)

# ---------- 지하 계단 입구(오른쪽 위)
d.rectangle((416, FL_T + 2, 478, FL_T + 38), fill=C('0a0b0f'))
for i in range(5):
    y = FL_T + 4 + i * 6
    d.rectangle((418 + i * 2, y, 476 - i * 2, y + 4), fill=mix(C('6d7580'), C('0b0c10'), i / 5))
d.rectangle((414, FL_T, 480, FL_T + 3), fill=C('8a8f97')); d.rectangle((414, FL_T + 38, 480, FL_T + 41), fill=C('8a8f97'))
for x in (414, 446, 478): d.rectangle((x, FL_T + 38, x + 2, FL_T + 44), fill=C('50545c'))
d.rectangle((418, FL_T + 42, 476, FL_T + 44), fill=TRIM_D)
pixel_text(d, 434, FL_T + 46, "B1", C('9fb0c0'))

# ---------- 양옆 출입구
def doorway(y0, y1, left):
    x0, x1 = (0, 32) if left else (480, 512)
    d.rectangle((x0, y0, x1 - 1, y1), fill=C('b9a98a'))
    for yy in range(y0, y1, 32): d.line((x0, yy, x1, yy), fill=MJ)
    sx = 8 if left else 480
    d.rectangle((sx, y0 - 8, sx + 23, y0 - 1), fill=TRIM); d.rectangle((sx, y1 + 1, sx + 23, y1 + 8), fill=TRIM)
    d.rectangle((sx, y0 - 9, sx + 23, y0 - 8), fill=TRIM_D)
    glow(im, (8 if left else 504), (y0 + y1) // 2, 40, 48, C('ffcf80'), 0.22)
for (a, b) in ((150, 196), (238, 284)):
    doorway(a, b, True); doorway(a, b, False)

# ---------- 나침반
cx, cy = 256, 246
def ell(r, col, w=0):
    box = (cx - r, cy - int(r * 0.64), cx + r, cy + int(r * 0.64))
    if w: d.ellipse(box, outline=col, width=w)
    else: d.ellipse(box, fill=col)
ell(104, C('7a2f3a')); ell(100, C('e9dcc0')); ell(92, C('9f2f41')); ell(88, C('e9dcc0')); ell(60, C('cdbd9c'))
ell(104, TRIM, 2); ell(60, TRIM_D, 1)
for k in range(32):
    a = k * math.pi / 16; r0, r1 = (88, 92) if k % 2 else (84, 92)
    d.line((cx + r0 * math.cos(a), cy + r0 * 0.64 * math.sin(a), cx + r1 * math.cos(a), cy + r1 * 0.64 * math.sin(a)), fill=C('5a3a22'))
for k in range(8):
    a = k * math.pi / 4; L = 96 if k % 2 == 0 else 62
    pts = [(cx + L * math.cos(a), cy + L * 0.64 * math.sin(a)), (cx + 9 * math.cos(a + math.pi / 2), cy + 9 * 0.64 * math.sin(a + math.pi / 2)), (cx, cy), (cx + 9 * math.cos(a - math.pi / 2), cy + 9 * 0.64 * math.sin(a - math.pi / 2))]
    d.polygon(pts, fill=C('c9a35a') if k % 2 == 0 else C('8c6b34'))
d.ellipse((cx - 7, cy - 5, cx + 7, cy + 5), fill=C('9f2f41'), outline=TRIM)
pixel_text(d, cx - 3, cy - 86, "N", C('4a2a18'))

# ---------- 아래 입구
d.rectangle((226, FL_B - 44, 286, FL_B - 1), fill=C('7e2634'))
d.rectangle((226, FL_B - 44, 229, FL_B - 1), fill=TRIM); d.rectangle((283, FL_B - 44, 286, FL_B - 1), fill=TRIM)
for y in range(FL_B - 40, FL_B, 6): d.line((232, y, 280, y), fill=C('9c3646'))
d.rectangle((208, FL_B + 6, 304, FL_B + 34), fill=C('0f1218'), outline=TRIM, width=2)
vgrad(im, (212, FL_B + 9, 300, FL_B + 32), C('1d3a4a'), C('0d1a24'))
d.line((256, FL_B + 6, 256, FL_B + 34), fill=TRIM)
d.line((222, FL_B + 10, 236, FL_B + 30), fill=C('3b6a82')); d.line((270, FL_B + 10, 284, FL_B + 30), fill=C('3b6a82'))

# ---------- 조명
for x in (96, 200, 312, 416):
    d.rectangle((x - 3, 60, x + 3, 64), fill=TRIM_D); d.ellipse((x - 5, 64, x + 5, 76), fill=C('ffe3a3'), outline=TRIM)
    glow(im, x, 72, 46, 60, C('ffc974'), 0.30)
    glow(im, x, FL_T + 34, 60, 22, C('ffd68a'), 0.16)
for x in (80, 392):
    d.rectangle((x, 44, x + 40, 80), fill=TRIM_D); d.rectangle((x + 3, 47, x + 37, 77), fill=C('27394a'))
    d.polygon([(x + 3, 77), (x + 14, 62), (x + 22, 70), (x + 30, 56), (x + 37, 77)], fill=C('3f6d5a'))

# ---------- 가장자리 어둡게 + 비네트
for i in range(14):
    a = int(70 * (1 - i / 14) ** 2)
    ov = Image.new('RGBA', (448, 1), (8, 8, 12, a))
    for yy in (FL_T + i, FL_B - 1 - i): im.alpha_composite(ov, (32, yy))
    ov2 = Image.new('RGBA', (1, FL_B - FL_T), (8, 8, 12, a))
    im.alpha_composite(ov2, (32 + i, FL_T)); im.alpha_composite(ov2, (479 - i, FL_T))
vg = Image.new('RGBA', (W, H), (0, 0, 0, 0)); p = vg.load()
for y in range(H):
    for x in range(W):
        dd = math.hypot((x - 256) / 300, (y - 192) / 250)
        if dd > 0.75: p[x, y] = (6, 6, 10, int(min(170, (dd - 0.75) * 330)))
im.alpha_composite(vg)
im.save(OUT + 'F1_Corridor.png')
im.resize((1024, 768), Image.NEAREST).save(OUT + 'F1_big.png')
print('saved')
