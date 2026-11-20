"""Builds the health potion pickup: a low-poly round flask of crimson liquid with a glass neck,
a gold ring and a cork. The crimson matches the HUD health stroke.

Run headless from the repository root:

    blender -b --factory-startup --python art/pickups/build_health_potion.py

Outputs:
    art/pickups/HealthPotion.blend
    game/Assets/_Project/Art/Pickups/HealthPotion.fbx
"""

import os

import bmesh
import bpy
from mathutils import Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART_DIR = os.path.join(ROOT, "art", "pickups")
GAME_DIR = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "Pickups")
os.makedirs(GAME_DIR, exist_ok=True)

SEGMENTS = 8
BODY_RADIUS = 0.17
BODY_Z = -0.06

LIQUID = (0.745, 0.071, 0.235, 1.0)
GLASS = (0.98, 0.86, 0.88, 1.0)
GOLD = (0.788, 0.592, 0.247, 1.0)
CORK = (0.42, 0.27, 0.15, 1.0)


def add_part(bm, build, material_index):
    """Runs a bmesh primitive operator and tags only the faces it created."""
    before = set(bm.faces)
    build()
    for face in set(bm.faces) - before:
        face.material_index = material_index


def cylinder(bm, radius_bottom, radius_top, depth, z, material_index):
    add_part(bm, lambda: bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=SEGMENTS,
        radius1=radius_bottom, radius2=radius_top, depth=depth,
        matrix=Matrix.Translation((0.0, 0.0, z))), material_index)


def material(name, color, emission_strength):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.5
    if emission_strength > 0:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    mat.diffuse_color = color
    return mat


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    mesh = bpy.data.meshes.new("HealthPotion")
    bm = bmesh.new()
    add_part(bm, lambda: bmesh.ops.create_uvsphere(
        bm, u_segments=SEGMENTS, v_segments=6, radius=BODY_RADIUS,
        matrix=Matrix.Translation((0.0, 0.0, BODY_Z))), 0)
    top = BODY_Z + BODY_RADIUS
    cylinder(bm, 0.07, 0.055, 0.12, top + 0.04, 1)       # glass neck
    cylinder(bm, 0.08, 0.08, 0.025, top + 0.0, 2)         # gold ring where the neck meets the body
    cylinder(bm, 0.05, 0.06, 0.07, top + 0.13, 3)         # cork
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    for mat in (material("M_PotionLiquid", LIQUID, 0.6),
                material("M_PotionGlass", GLASS, 0.0),
                material("M_PotionGold", GOLD, 0.0),
                material("M_PotionCork", CORK, 0.0)):
        mesh.materials.append(mat)

    ob = bpy.data.objects.new("HealthPotion", mesh)
    bpy.context.scene.collection.objects.link(ob)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART_DIR, "HealthPotion.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(GAME_DIR, "HealthPotion.fbx"),
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="STRIP",
    )
    print("HEALTH_POTION_OK", len(mesh.vertices), len(mesh.polygons))


main()
