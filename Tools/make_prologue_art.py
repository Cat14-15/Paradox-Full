# 프롤로그 점심시간 실루엣 스프라이트(도트). 출력: Assets/Art/UI/Prologue
from PIL import Image, ImageDraw
import os
out = "Assets/Art/UI/Prologue"
os.makedirs(out, exist_ok=True)
K = (8, 6, 10, 255)

def person(name, hair=0, w=28, h=40):
    """앉은 사람 실루엣(머리+어깨). hair: 0 짧은머리, 1 긴머리, 2 묶음"""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cx = w // 2
    d.ellipse([cx - 7, 2, cx + 6, 17], fill=K)             # 머리
    if hair == 1: d.rectangle([cx - 8, 8, cx + 7, 26], fill=K)
    if hair == 2: d.ellipse([cx + 3, 0, cx + 11, 8], fill=K)
    d.rectangle([cx - 3, 16, cx + 2, 21], fill=K)           # 목
    d.rounded_rectangle([2, 21, w - 3, h], radius=7, fill=K)  # 어깨
    im.save(f"{out}/{name}.png")

person("sil_a", 0); person("sil_b", 1); person("sil_c", 2); person("sil_d", 0)

# 식탁(긴 판)
t = Image.new("RGBA", (16, 8), (0, 0, 0, 0)); d = ImageDraw.Draw(t)
d.rectangle([0, 0, 15, 3], fill=(30, 18, 14, 255)); d.rectangle([0, 4, 15, 7], fill=(14, 9, 8, 255))
t.save(f"{out}/table.png")

# 창문 빛 기둥(세로 그라데이션 흰색 알파)
g = Image.new("RGBA", (4, 64))
for y in range(64):
    a = int(200 * (1 - y / 63) ** 1.4)
    for x in range(4): g.putpixel((x, y), (255, 226, 160, a))
g.save(f"{out}/light.png")

# 대화창 판(9-slice용 작은 판)
p = Image.new("RGBA", (16, 16), (0, 0, 0, 0)); d = ImageDraw.Draw(p)
d.rectangle([0, 0, 15, 15], fill=(236, 200, 120, 255)); d.rectangle([2, 2, 13, 13], fill=(255, 255, 255, 255))
p.save(f"{out}/box.png")
print("ok")
