"""Builds the main-menu arena (hexagonal stone platform, pillars, glowing runes).

Run headless from the repository root:

    blender -b --factory-startup --python art/environment/build_menu_arena.py -- \
        <floor_texture> <wall_texture>

Outputs:
    art/environment/MenuArena.blend
    art/environment/textures/*.png                      (128x128 PSX copies)
    game/Assets/_Project/Art/Environment/MenuArena.fbx
    game/Assets/_Project/Art/Environment/Textures/*.png
"""

import math
import os
import random
import shutil
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
FLOOR_SRC, WALL_SRC = ARGS[0], ARGS[1]

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART_DIR = os.path.join(ROOT, "art", "environment")
ART_TEX = os.path.join(ART_DIR, "textures")
GAME_DIR = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "Environment")
GAME_TEX = os.path.join(GAME_DIR, "Textures")
for d in (ART_TEX, GAME_TEX):
    os.makedirs(d, exist_ok=True)

TEXTURE_SIZE = 128
PLATFORM_RADIUS = 7.0
PLATFORM_HEIGHT = 0.5
STEP_RADIUS = 8.2
STEP_HEIGHT = 0.3
random.seed(7)


def psx_texture(src, name):
    """Downscale a source texture to a small PSX-sized PNG in both output folders."""
    img = bpy.data.images.load(src)
    img.scale(TEXTURE_SIZE, TEXTURE_SIZE)
    art_path = os.path.join(ART_TEX, name + ".png")
    img.filepath_raw = art_path
    img.file_format = "PNG"
    img.save()
    shutil.copyfile(art_path, os.path.join(GAME_TEX, name + ".png"))
    return bpy.data.images.load(art_path)


def material(name, image=None, emission=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 1.0
    if image is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Closest"
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if emission is not None:
        bsdf.inputs["Base Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    return mat


def box_uv(bm, scale):
    """Box-project UVs so textures tile every `scale` metres regardless of face orientation."""
    uv = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal
        axis = max(range(3), key=lambda i: abs(n[i]))
        for loop in face.loops:
            co = loop.vert.co
            if axis == 0:
                u, v = co.y, co.z
            elif axis == 1:
                u, v = co.x, co.z
            else:
                u, v = co.x, co.y
            loop[uv].uv = (u / scale, v / scale)


def mesh_object(name, bm, mat, uv_scale):
    box_uv(bm, uv_scale)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def hex_prism(radius, height, top_z, segments=6, rotation=math.pi / 6):
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segments,
        radius1=radius, radius2=radius, depth=height,
    )
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0),
                     matrix=Matrix.Rotation(rotation, 3, "Z"))
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, top_z - height / 2))
    return bm


def hex_ring(outer, inner, z, thickness=0.03):
    """Flat hexagonal ring lying on the floor (used for glowing runes)."""
    bm = bmesh.new()
    rot = math.pi / 6
    outer_v, inner_v = [], []
    for i in range(6):
        a = rot + i * math.pi / 3
        outer_v.append(bm.verts.new((outer * math.cos(a), outer * math.sin(a), z)))
        inner_v.append(bm.verts.new((inner * math.cos(a), inner * math.sin(a), z)))
    for i in range(6):
        j = (i + 1) % 6
        bm.faces.new((outer_v[i], outer_v[j], inner_v[j], inner_v[i]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thickness)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def pillar(height, broken):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=8,
                          radius1=0.5, radius2=0.42, depth=height)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, height / 2))
    top = [v for v in bm.verts if v.co.z > height - 0.01]
    if broken:
        # Jagged, tilted break at the top.
        for v in top:
            v.co.z -= random.uniform(0.0, 0.6)
            v.co.x += random.uniform(-0.05, 0.05)
    else:
        # Square capital block.
        cap = bmesh.ops.create_cube(bm, size=1.0)["verts"]
        bmesh.ops.scale(bm, verts=cap, vec=(1.25, 1.25, 0.3))
        bmesh.ops.translate(bm, verts=cap, vec=(0, 0, height + 0.15))
        base = bmesh.ops.create_cube(bm, size=1.0)["verts"]
        bmesh.ops.scale(bm, verts=base, vec=(1.3, 1.3, 0.35))
        bmesh.ops.translate(bm, verts=base, vec=(0, 0, 0.175))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def rubble(count, center, spread):
    bm = bmesh.new()
    for _ in range(count):
        verts = bmesh.ops.create_cube(bm, size=1.0)["verts"]
        s = random.uniform(0.2, 0.55)
        bmesh.ops.scale(bm, verts=verts, vec=(s, s * random.uniform(0.7, 1.3), s * 0.6))
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0),
                         matrix=Matrix.Rotation(random.uniform(0, math.pi), 3, "Z"))
        offset = Vector((center[0] + random.uniform(-spread, spread),
                         center[1] + random.uniform(-spread, spread), s * 0.3))
        bmesh.ops.translate(bm, verts=verts, vec=offset)
    return bm


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    floor_img = psx_texture(FLOOR_SRC, "T_StoneTiles")
    wall_img = psx_texture(WALL_SRC, "T_CastleBrick")
    m_floor = material("M_ArenaFloor", floor_img)
    m_wall = material("M_ArenaStone", wall_img)
    m_rune = material("M_ArenaRune", emission=(0.37, 0.92, 0.83))

    mesh_object("Platform", hex_prism(PLATFORM_RADIUS, PLATFORM_HEIGHT, 0.0), m_floor, 2.0)
    mesh_object("Step", hex_prism(STEP_RADIUS, STEP_HEIGHT, -PLATFORM_HEIGHT + 0.05), m_wall, 1.5)
    mesh_object("RuneOuter", hex_ring(5.2, 4.95, 0.0), m_rune, 1.0)
    mesh_object("RuneInner", hex_ring(1.45, 1.25, 0.0), m_rune, 1.0)

    heights = [5.0, 3.0, 5.0, 1.4, 4.4, 5.0]
    broken = [False, True, False, True, True, False]
    for i, (h, b) in enumerate(zip(heights, broken)):
        a = math.pi / 6 + i * math.pi / 3
        ob = mesh_object(f"Pillar_{i + 1:02d}", pillar(h, b), m_wall, 1.5)
        ob.location = (6.3 * math.cos(a), 6.3 * math.sin(a), 0.0)
        if b:
            mesh_object(f"Rubble_{i + 1:02d}",
                        rubble(5, (5.4 * math.cos(a), 5.4 * math.sin(a)), 0.7), m_wall, 1.0)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART_DIR, "MenuArena.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(GAME_DIR, "MenuArena.fbx"),
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="STRIP",
        use_mesh_modifiers=True,
    )
    print("ARENA_OK", [o.name for o in bpy.context.scene.objects])


main()
