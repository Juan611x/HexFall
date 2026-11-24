"""Builds the wind current ring: a thin band (256 triangles) around the vertical axis.

U runs around the circumference (0..1, the seam column is duplicated so U can wrap), V across the band (0 bottom,
1 top). The band bulges slightly in the middle so it hugs the sphere's curvature like a strip cut from the air
sphere. The band's axis is Blender's Y because the FBX importer keeps the mesh's local axes (the Z-up to Y-up
conversion lands on the node rotation, which a MeshFilter ignores), so Unity sees the axis on Y. Radius 0.5 matches AirShield.fbx; WindCurrents scales the ring per height and tilts it.

Run headless from the repository root:

    blender -b --factory-startup --python art/vfx/wind_shield/build_current_ring_mesh.py

Output:
    game/Assets/_Project/VFX/WindShield/Models/AirCurrentRing.fbx
"""

import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "_Project", "VFX", "WindShield", "Models", "AirCurrentRing.fbx")

SEGMENTS = 64      # around the axis: 64 * 2 rows * 2 triangles = 256 triangles
RADIUS = 0.5
HALF_HEIGHT = 0.08  # the band is 0.16 tall
# Bottom, middle and top row: the outer rows sit on the sphere surface at +-HALF_HEIGHT, the middle one on its equator.
ROW_RADIUS = (math.sqrt(RADIUS ** 2 - HALF_HEIGHT ** 2), RADIUS, math.sqrt(RADIUS ** 2 - HALF_HEIGHT ** 2))
ROW_Y = (-HALF_HEIGHT, 0.0, HALF_HEIGHT)

bpy.ops.wm.read_factory_settings(use_empty=True)

verts, uvs, faces = [], [], []
columns = SEGMENTS + 1
for row in range(3):
    for col in range(columns):
        angle = 2.0 * math.pi * col / SEGMENTS
        verts.append((ROW_RADIUS[row] * math.cos(angle), ROW_Y[row], ROW_RADIUS[row] * math.sin(angle)))
        uvs.append((col / SEGMENTS, row / 2.0))
for row in range(2):
    for col in range(SEGMENTS):
        a = row * columns + col
        b = a + 1
        c = a + columns + 1
        d = a + columns
        faces.append((a, b, c))
        faces.append((a, c, d))

mesh = bpy.data.meshes.new("AirCurrentRing")
mesh.from_pydata(verts, [], faces)
uv_layer = mesh.uv_layers.new(name="UVMap")
for loop in mesh.loops:
    uv_layer.data[loop.index].uv = uvs[loop.vertex_index]
mesh.update()

obj = bpy.data.objects.new("AirCurrentRing", mesh)
bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    mesh_smooth_type="FACE",
    path_mode="STRIP",
)
print("AirCurrentRing triangles:", len(mesh.polygons), "->", OUT)
