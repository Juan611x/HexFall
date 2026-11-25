"""Builds the air shield sphere: a 32 x 16 UV sphere (960 triangles, smooth shading).

The poles sit on Blender's Y axis, which Unity reads as its vertical Y: the FBX importer keeps the mesh's local
axes (the Z-up to Y-up conversion lands on the node rotation, which a MeshFilter ignores), so the sphere is built
with its poles on Blender Y, the same way as the current ring. That is the shield's spin axis.
The shader derives its wind veins from the object-space direction, not from the UVs, so the UVs are only a by-product. Radius 0.5 matches Unity's built-in Sphere.

Run headless from the repository root:

    blender -b --factory-startup --python art/vfx/wind_shield/build_air_shield_mesh.py

Output:
    game/Assets/_Project/VFX/WindShield/Models/AirShield.fbx
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "_Project", "VFX", "WindShield", "Models", "AirShield.fbx")

SEGMENTS = 32  # around the axis
RINGS = 16     # pole to pole -> 2 * SEGMENTS + (RINGS - 2) * SEGMENTS * 2 = 960 triangles
RADIUS = 0.5

bpy.ops.wm.read_factory_settings(use_empty=True)

bm = bmesh.new()
bm.loops.layers.uv.new("UVMap")  # create_uvsphere only fills an existing UV layer
bmesh.ops.create_uvsphere(bm, u_segments=SEGMENTS, v_segments=RINGS, radius=RADIUS, calc_uvs=True)
# create_uvsphere puts the poles on Z; turn them onto Y (x, y, z) -> (x, -z, y), a +90 degree rotation about X (the +Z pole lands on +Y)
bmesh.ops.rotate(bm, verts=bm.verts[:], cent=(0.0, 0.0, 0.0), matrix=Matrix.Rotation(math.radians(90.0), 3, "X"))
bmesh.ops.triangulate(bm, faces=bm.faces[:])
for face in bm.faces:
    face.smooth = True

mesh = bpy.data.meshes.new("AirShield")
bm.to_mesh(mesh)
bm.free()
obj = bpy.data.objects.new("AirShield", mesh)
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
print("AirShield triangles:", len(mesh.polygons), "->", OUT)
