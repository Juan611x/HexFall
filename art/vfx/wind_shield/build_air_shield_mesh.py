"""Builds the low-poly air shield sphere: a faceted UV sphere with flat shading.

U wraps around the vertical (Y) axis with the seam duplicated, V runs pole to pole, so a small
point-filtered texture maps cleanly. Radius 0.5 matches Unity's built-in Sphere.

Run headless from the repository root:

    blender -b --factory-startup --python art/vfx/wind_shield/build_air_shield_mesh.py

Output:
    game/Assets/_Project/VFX/WindShield/Models/AirShield.fbx
"""

import math
import os

import bmesh
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "game", "Assets", "_Project", "VFX", "WindShield", "Models", "AirShield.fbx")

SEGMENTS = 10  # around the axis
RINGS = 6      # pole to pole -> 2 * SEGMENTS + (RINGS - 2) * SEGMENTS * 2 = 100 triangles
RADIUS = 0.5

bpy.ops.wm.read_factory_settings(use_empty=True)

bm = bmesh.new()
bm.loops.layers.uv.new("UVMap")  # create_uvsphere only fills an existing UV layer
bmesh.ops.create_uvsphere(bm, u_segments=SEGMENTS, v_segments=RINGS, radius=RADIUS, calc_uvs=True)
bmesh.ops.triangulate(bm, faces=bm.faces[:])
for face in bm.faces:
    face.smooth = False

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
