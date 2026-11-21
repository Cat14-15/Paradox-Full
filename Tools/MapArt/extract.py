import numpy as np, os, json
from PIL import Image, ImageDraw
T = os.environ['TEMP']
B = T + '/dl/lpc/x/LPC Base Assets/tiles/'
OUT = T + '/art/n/spr/'
os.makedirs(OUT, exist_ok=True)

def components(im, gap=1):
    a = np.array(im)[:, :, 3] > 0
    H, W = a.shape
    lab = np.zeros((H, W), int); n = 0; boxes = []
    for y in range(H):
        for x in range(W):
            if a[y, x] and lab[y, x] == 0:
                n += 1; st = [(y, x)]; lab[y, x] = n
                x0 = x1 = x; y0 = y1 = y
                while st:
                    cy, cx = st.pop()
                    x0 = min(x0, cx); x1 = max(x1, cx); y0 = min(y0, cy); y1 = max(y1, cy)
                    for dy in range(-gap, gap + 1):
                        for dx in range(-gap, gap + 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < H and 0 <= nx < W and a[ny, nx] and lab[ny, nx] == 0:
                                lab[ny, nx] = n; st.append((ny, nx))
                boxes.append((x0, y0, x1 + 1, y1 + 1))
    return boxes

cat = {}
for name in ('inside', 'castlewalls', 'cabinets', 'castle_lightsources', 'castlefloors', 'signs', 'stairs', 'cementstair', 'chests', 'barrel', 'house'):
    try:
        im = Image.open(B + name + '.png').convert('RGBA')
    except Exception as e:
        continue
    boxes = [b for b in components(im, 1) if (b[2] - b[0]) * (b[3] - b[1]) >= 64]
    cat[name] = boxes
    sheet = Image.new('RGBA', (1400, 1400), (60, 60, 72, 255)); d = ImageDraw.Draw(sheet)
    x = y = 8; rowh = 0
    for i, b in enumerate(boxes):
        s = im.crop(b); w, h = s.size
        if x + w + 8 > 1400: x = 8; y += rowh + 22; rowh = 0
        sheet.alpha_composite(s, (x, y + 12)); d.text((x, y), str(i), fill=(255, 255, 0, 255))
        x += max(w, 20) + 10; rowh = max(rowh, h + 12)
    sheet.crop((0, 0, 1400, min(1400, y + rowh + 30))).save(T + f'/art/n/spr/sheet_{name}.png')
    print(name, len(boxes))
json.dump(cat, open(OUT + 'boxes.json', 'w'))
