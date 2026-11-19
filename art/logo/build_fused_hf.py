"""Fuse Cinzel Decorative H and F on a shared stem and stretch that stem downward.

Usage: python build_fused_hf.py <font.ttf> <delta> <out.png> <out.svgpath.txt>
"""
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen, RecordingPen
from fontTools.pens.freetypePen import FreeTypePen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen

font_path, delta, out_png, out_path = sys.argv[1], float(sys.argv[2]), sys.argv[3], sys.argv[4]
font = TTFont(font_path)
gs = font.getGlyphSet()
cmap = font.getBestCmap()

F_SHIFT = 581 - 107   # F stem left edge onto the H right stem left edge
CUT_Y = 300           # below the H crossbar (325) and the F middle arm (320)


def record(ch):
    rp = DecomposingRecordingPen(gs)
    gs[cmap[ord(ch)]].draw(rp)
    return rp.value


def transform(ops, fn):
    out = []
    for op, args in ops:
        out.append((op, tuple(fn(p) for p in args)))
    return out


h = transform(record("H"), lambda p: (p[0], p[1] - delta) if (p[0] >= 450 and p[1] < CUT_Y) else p)
F_STEM_MAX_X = 400    # only the stem and its foot serif; the middle-arm terminal curl stays put
f = transform(record("F"), lambda p: (p[0] + F_SHIFT, p[1] - delta) if (p[1] < CUT_Y and p[0] < F_STEM_MAX_X) else (p[0] + F_SHIFT, p[1]))

combined = RecordingPen()
combined.value = h + f

bp = BoundsPen(None)
combined.replay(bp)
print("bounds", bp.bounds)

ft = FreeTypePen(None)
combined.replay(ft)
ft.image(width=900, height=700, contain=True).save(out_png)

svg = SVGPathPen(None)
combined.replay(svg)
with open(out_path, "w") as fh:
    fh.write(svg.getCommands())
print("saved", out_png, out_path)
