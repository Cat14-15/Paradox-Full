"""도트 글꼴 아틀라스 생성기.
굴림(gulim.ttc)의 내장 16px 비트맵 글꼴로 KS X 1001 한글 2350자 + ASCII + 기호를 16x16 칸 아틀라스(atlas.png)로 만들고,
글자별 가로 폭을 glyphs.txt 에 적는다(한 줄: 글자<TAB>폭). 유니티 PixelText 가 이 둘을 읽는다.
다른 도트 폰트(.ttf)로 바꿀 땐 FONT 경로와 인덱스만 바꿔 다시 실행하면 된다(라이선스가 자유로운 폰트 권장).
실행: python Tools/make_pixel_font.py
"""
from PIL import Image, ImageDraw, ImageFont
import os, math
FONT, INDEX, SIZE = "C:/Windows/Fonts/gulim.ttc", 0, 16
CELL = 16
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "PixelFont")
chars = [chr(c) for c in range(0x20, 0x7F)]
chars += list("·…★☆▶◀▲▼←→↑↓※♥♡○●■□「」『』~ㆍ×÷±°℃")
for lead in range(0xB0, 0xC9):
    for trail in range(0xA1, 0xFF):
        try: chars.append(bytes([lead, trail]).decode("euc-kr"))
        except Exception: pass
seen, uniq = set(), []
for c in chars:
    if c not in seen: seen.add(c); uniq.append(c)
chars = uniq
chars += [chr(c) for c in range(0x3131, 0x3164) if chr(c) not in set(chars)]   # ㅋ ㅎ ㅠ 같은 자모
font = ImageFont.truetype(FONT, SIZE, index=INDEX, layout_engine=ImageFont.Layout.BASIC)
cols = 64; rows = math.ceil(len(chars) / cols)
atlas = Image.new("RGBA", (cols * CELL, rows * CELL), (255, 255, 255, 0))
lines = []
for i, ch in enumerate(chars):
    cell = Image.new("L", (CELL * 2, CELL * 2), 0)
    ImageDraw.Draw(cell).fontmode = "1"
    d = ImageDraw.Draw(cell); d.fontmode = "1"; d.text((0, 0), ch, font=font, fill=255)
    bb = cell.getbbox()
    is_hangul = 0xAC00 <= ord(ch) <= 0xD7A3 or 0x3131 <= ord(ch) <= 0x3163
    if ch == " ":
        adv, shift = 4, 0
    elif is_hangul:
        adv, shift = 15, 0                      # 한글은 폭을 1칸 줄여 글자 사이를 붙인다
    elif bb:
        shift = bb[0]                           # 영문·숫자·기호는 왼쪽 여백을 없애고 글자 폭만큼만 차지(비례폭)
        adv = max(bb[2] - bb[0] + 1, 2)
        if ord(ch) >= 0x80: adv = max(adv + 2, 6)
    else:
        adv, shift = 8, 0
    px = atlas.load(); ox, oy = (i % cols) * CELL, (i // cols) * CELL
    for y in range(CELL):
        for x in range(CELL):
            if x + shift < CELL * 2 and cell.getpixel((x + shift, y)) > 0: px[ox + x, oy + y] = (255, 255, 255, 255)
    lines.append(ch + "\t" + str(adv))
os.makedirs(OUT, exist_ok=True)
atlas.save(os.path.join(OUT, "atlas.png"))
open(os.path.join(OUT, "glyphs.txt"), "w", encoding="utf-8").write("\n".join(lines))
print(len(chars), "glyphs", atlas.size)
