from pa import *
import json, os
OUT = os.environ['TEMP'] + '/art/n/out/'
RES = 'C:/Users/user/Desktop/Data/Unity/Project/Paradox/Assets/Resources/MuseumOverlays/'
rng = random.Random(11)
im = Image.new('RGBA', (W, H), C('101116'))
d = ImageDraw.Draw(im)
WALL0, WALL1 = C('2a3446'), C('3d4d63')
TRIM = C('d8b46a'); TRIM_D = C('8f6c34')
M0, M1, MJ = C('e6dac2'), C('d3c5a8'), C('aa9b7d')
CX, CY = 256, 342          # 반원 중심(아래 변의 가운데)
RX, RY = 224, 226          # 바닥 반원 반지름

def inside(x, y, rx=RX, ry=RY):
    return y <= CY and ((x - CX) / rx) ** 2 + ((y - CY) / ry) ** 2 <= 1.0

# ---------- 벽 고리(바깥 → 안쪽)
for k in range(34, 0, -1):
    t = k / 34
    col = mix(WALL1, WALL0, t)
    d.ellipse((CX - RX - k - 2, CY - RY - k - 2, CX + RX + k + 2, CY + RY + k + 2), fill=col)
# 몰딩 라인
d.ellipse((CX - RX - 36, CY - RY - 36, CX + RX + 36, CY + RY + 36), outline=C('efe0bd'), width=3)
d.ellipse((CX - RX - 33, CY - RY - 33, CX + RX + 33, CY + RY + 33), outline=TRIM_D, width=2)
d.ellipse((CX - RX - 6, CY - RY - 6, CX + RX + 6, CY + RY + 6), outline=TRIM, width=3)
# 걸레받이(나무)
d.ellipse((CX - RX - 5, CY - RY - 5, CX + RX + 5, CY + RY + 5), outline=C('6b4630'), width=3)

# ---------- 바닥
px = im.load()
for y in range(100, CY):
    for x in range(20, 492):
        if inside(x, y):
            tx, ty = (x - 32) // 32, (y - 116) // 32
            px[x, y] = M0 if (tx + ty) % 2 == 0 else M1
noise(im, (20, 100, 492, CY), 7, rng, 0.22)
for x in range(32, 480, 32):
    for y in range(100, CY):
        if inside(x, y): px[x, y] = MJ
for y in range(116, CY, 32):
    for x in range(20, 492):
        if inside(x, y): px[x, y] = MJ
# 아래 벽 + 바닥 가장자리
vgrad(im, (8, CY, 504, 378), WALL0, C('171b24'))
d.rectangle((8, CY, 503, CY + 3), fill=C('46291d')); d.rectangle((8, CY + 3, 503, CY + 5), fill=TRIM_D)
# 가운데 큰 원형 문양(동상 둘레)
def ell(cx, cy, rx, ry, col, w=0):
    box = (cx - rx, cy - ry, cx + rx, cy + ry)
    d.ellipse(box, outline=col, width=w) if w else d.ellipse(box, fill=col)
ell(256, 226, 96, 62, C('7a2f3a')); ell(256, 226, 92, 59, C('e9dcc0')); ell(256, 226, 78, 50, C('9f2f41')); ell(256, 226, 74, 47, C('e3d3b2'))
ell(256, 226, 96, 62, TRIM, 2); ell(256, 226, 52, 33, TRIM_D, 1)
for k in range(24):
    a = k * math.pi / 12
    d.line((256 + 78 * math.cos(a), 226 + 50 * math.sin(a), 256 + 92 * math.cos(a), 226 + 59 * math.sin(a)), fill=C('9c7a40'))
# 레드 러너(아래 계단으로)
d.rectangle((232, 262, 280, CY - 1), fill=C('7e2634'))
d.rectangle((232, 262, 235, CY - 1), fill=TRIM); d.rectangle((277, 262, 280, CY - 1), fill=TRIM)
for y in range(266, CY, 6): d.line((238, y, 274, y), fill=C('9c3646'))

# ---------- 아래 계단(1층으로)
d.rectangle((216, CY + 6, 296, CY + 34), fill=C('0b0c10'), outline=TRIM, width=2)
for i in range(4):
    d.rectangle((220 + i * 3, CY + 9 + i * 6, 292 - i * 3, CY + 13 + i * 6), fill=mix(C('d9c9a2'), C('2b2619'), i / 4))
pixel_text(d, 248, CY + 18, "1F", C('f4e8c8')) if False else None

# ---------- 벽 창문(둥근 벽을 따라 배치) + 기둥
def on_ring(deg, r_off):
    a = math.radians(deg)
    return CX + (RX + r_off) * math.cos(a), CY - (RY + r_off) * math.sin(a), a
for deg in (160, 125, 90, 55, 20):
    x, y, a = on_ring(deg, -2 + 0)
    # 창: 벽 위에 작은 아치창
    wx, wy, _ = on_ring(deg, 18)
    d.ellipse((wx - 9, wy - 9, wx + 9, wy + 9), fill=C('1a2a3a'), outline=TRIM_D)
    d.ellipse((wx - 6, wy - 6, wx + 6, wy + 6), fill=C('7fb0c8'))
    glow(im, wx, wy, 34, 34, C('9fd4ee'), 0.22)
for deg in (170, 142, 108, 72, 38, 10):
    x, y, a = on_ring(deg, 4)
    d.rectangle((x - 6, y - 8, x + 6, y + 8), fill=C('c9bda3'), outline=C('8d8068'))
    d.rectangle((x - 8, y - 10, x + 8, y - 7), fill=TRIM)
# ---------- 문 4개
def door_diag(cx, cy, deg):
    a = math.radians(deg)
    ux, uy = math.cos(a), -math.sin(a)      # 문이 향하는 바깥 방향
    px_, py_ = -uy, ux
    L, Wd = 26, 22
    pts = [(cx + px_ * Wd - ux * 6, cy + py_ * Wd - uy * 6), (cx - px_ * Wd - ux * 6, cy - py_ * Wd - uy * 6),
           (cx - px_ * Wd + ux * L, cy - py_ * Wd + uy * L), (cx + px_ * Wd + ux * L, cy + py_ * Wd + uy * L)]
    d.polygon(pts, fill=C('0c0d11'), outline=TRIM)
    ins = [(cx + px_ * (Wd - 4) + ux * 2, cy + py_ * (Wd - 4) + uy * 2), (cx - px_ * (Wd - 4) + ux * 2, cy - py_ * (Wd - 4) + uy * 2),
           (cx - px_ * (Wd - 4) + ux * (L - 3), cy - py_ * (Wd - 4) + uy * (L - 3)), (cx + px_ * (Wd - 4) + ux * (L - 3), cy + py_ * (Wd - 4) + uy * (L - 3))]
    d.polygon(ins, fill=C('2b1d14'))
    glow(im, cx - ux * 10, cy - uy * 10, 36, 30, C('ffcf80'), 0.30)
def door_side(y0, y1, left):
    x0, x1 = (0, 46) if left else (466, 512)
    d.rectangle((x0, y0, x1, y1), fill=C('b9a98a'))
    for yy in range(y0, y1, 32): d.line((x0, yy, x1, yy), fill=MJ)
    xx = 28 if left else 484
    d.rectangle((xx - 10, y0 - 7, xx + 10, y0 - 1), fill=TRIM); d.rectangle((xx - 10, y1 + 1, xx + 10, y1 + 7), fill=TRIM)
    glow(im, 8 if left else 504, (y0 + y1) // 2, 36, 46, C('ffcf80'), 0.24)
door_diag(112, 190, 135)   # 기록 아카이브(왼쪽 위)
door_diag(400, 190, 45)    # 보존처리실(오른쪽 위)
door_side(250, 290, True)  # 자료 열람실
door_side(250, 290, False) # 기획전시실

# ---------- 동상(기존 동상 도트를 재활용해 새 받침대와 합침)
st = Image.open(RES + '00_Central_Hall__Statue.png').convert('RGBA').crop((218, 92, 294, 198))
# 새 받침대(3단)
for i, (w, h, c) in enumerate(((64, 10, C('4a4d55')), (52, 10, C('5d616b')), (40, 8, C('737884')))):
    pass
foot_y = 238
# 그림자
shade_ellipse(im, 256, foot_y + 2, 52, 14, 90)
d.rectangle((222, foot_y - 12, 290, foot_y), fill=C('2a2c33')); d.rectangle((222, foot_y - 12, 290, foot_y - 10), fill=C('5d616b'))
d.rectangle((230, foot_y - 22, 282, foot_y - 12), fill=C('4a4d55')); d.rectangle((230, foot_y - 22, 282, foot_y - 20), fill=C('7a7f8b'))
stat_img = st.copy()
im.alpha_composite(st, (256 - 38, foot_y - 22 - 106 + 14))
# ---------- 팀 집결 발판 4개
pads = []
for i, cx in enumerate((150, 214, 298, 362)):
    cy = 296 + (6 if i in (1, 2) else 0) - (8 if i in (0, 3) else 0)
    shade_ellipse(im, cx, cy + 4, 34, 12, 60)
    d.ellipse((cx - 26, cy - 13, cx + 26, cy + 13), fill=C('1b3a40'), outline=TRIM, width=2)
    d.ellipse((cx - 18, cy - 8, cx + 18, cy + 8), fill=C('2f7f86'), outline=C('9fe0dc'))
    d.ellipse((cx - 8, cy - 4, cx + 8, cy + 4), fill=C('b7f3ee'))
    glow(im, cx, cy, 44, 24, C('7fe8e0'), 0.35)
    pads.append((cx, cy))
# ---------- 조명 + 어둡게
glow(im, 256, 190, 190, 120, C('ffe0a0'), 0.16)
for k in range(10):
    a = int(90 * (1 - k / 10) ** 2)
    for y in range(100, CY):
        for x in range(20, 492):
            if inside(x, y) and not inside(x, y, RX - 2 - k, RY - 2 - k):
                r, g, b, aa = px[x, y]
                px[x, y] = (int(r * (1 - a / 255)), int(g * (1 - a / 255)), int(b * (1 - a / 255)), aa)
vg = Image.new('RGBA', (W, H), (0, 0, 0, 0)); p = vg.load()
for y in range(H):
    for x in range(W):
        dd = math.hypot((x - 256) / 300, (y - 192) / 250)
        if dd > 0.78: p[x, y] = (6, 6, 10, int(min(170, (dd - 0.78) * 330)))
im.alpha_composite(vg)
im.save(OUT + 'F2_StatueHall.png')
im.resize((1024, 768), Image.NEAREST).save(OUT + 'F2_big.png')
json.dump({'pads': pads}, open(OUT + 'F2_spec.json', 'w'))
print('saved')
