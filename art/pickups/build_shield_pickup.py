"""Builds the shield refill pickup: a low-poly hexagonal shield with a gold rim and a raised rune.

The shape matches the HUD shield seals (pointy-top hex, gold frame, teal face, up-arrow rune).
The rune is on both sides because the pickup spins in place.

Run headless from the repository root:

    blender -b --factory-startup --python art/pickups/build_shield_pickup.py

Outputs:
    art/pickups/ShieldPickup.blend
    game/Assets/_Project/Art/Pickups/ShieldPickup.fbx
"""

import math
import os

import bmesh
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART_DIR = os.path.join(ROOT, "art", "pickups")
GAME_DIR = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "Pickups")
os.makedirs(GAME_DIR, exist_ok=True)

OUTER_RADIUS = 0.35
INNER_RADIUS = 0.28
RIM_DEPTH = 0.08
FACE_DEPTH = 0.05
RUNE_DEPTH = 0.05  # half-depth of the rune bars: they stand out of both faces
RUNE_WIDTH = 0.035

GOLD = (0.788, 0.592, 0.247, 1.0)
TEAL = (0.067, 0.369, 0.349, 1.0)
GLYPH = (0.6, 0.965, 0.894, 1.0)


def hex_points(radius):
    """Pointy-top hexagon in the XZ plane (Blender Z is up, the shield faces -Y)."""
    return [(radius * math.cos(math.radians(90 + 60 * i)), radius * math.sin(math.radians(90 + 60 * i)))
            for i in range(6)]


def prism(bm, points, depth, material_index):
    """Closed prism from a convex XZ outline, centred on Y = 0."""
    front = [bm.verts.new((x, -depth / 2, z)) for x, z in points]
    back = [bm.verts.new((x, depth / 2, z)) for x, z in points]
    faces = [bm.faces.new(front), bm.faces.new(list(reversed(back)))]
    n = len(points)
    for i in range(n):
        j = (i + 1) % n
        faces.append(bm.faces.new((front[j], front[i], back[i], back[j])))
    for f in faces:
        f.material_index = material_index


def ring(bm, outer, inner, depth, material_index):
    """Hexagonal frame between two outlines, centred on Y = 0."""
    of = [bm.verts.new((x, -depth / 2, z)) for x, z in outer]
    ob = [bm.verts.new((x, depth / 2, z)) for x, z in outer]
    inf = [bm.verts.new((x, -depth / 2, z)) for x, z in inner]
    inb = [bm.verts.new((x, depth / 2, z)) for x, z in inner]
    for i in range(6):
        j = (i + 1) % 6
        for quad in ((of[i], of[j], inf[j], inf[i]),
                     (ob[j], ob[i], inb[i], inb[j]),
                     (of[j], of[i], ob[i], ob[j]),
                     (inf[i], inf[j], inb[j], inb[i])):
            bm.faces.new(quad).material_index = material_index


def bar(bm, start, end, width, depth, material_index):
    """Flat box from start to end (XZ points) with the given width."""
    dx, dz = end[0] - start[0], end[1] - start[1]
    length = math.hypot(dx, dz)
    nx, nz = -dz / length * width / 2, dx / length * width / 2
    outline = [(start[0] + nx, start[1] + nz), (start[0] - nx, start[1] - nz),
               (end[0] - nx, end[1] - nz), (end[0] + nx, end[1] + nz)]
    prism(bm, outline, depth * 2, material_index)


def material(name, color, emission_strength):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.6
    if emission_strength > 0:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    mesh = bpy.data.meshes.new("ShieldPickup")
    bm = bmesh.new()
    ring(bm, hex_points(OUTER_RADIUS), hex_points(INNER_RADIUS), RIM_DEPTH, 0)
    prism(bm, hex_points(INNER_RADIUS), FACE_DEPTH, 1)
    # Up-arrow rune, like the HUD seal glyph.
    bar(bm, (0.0, -0.14), (0.0, 0.15), RUNE_WIDTH, RUNE_DEPTH, 2)
    bar(bm, (-0.1, 0.05), (0.012, 0.16), RUNE_WIDTH, RUNE_DEPTH, 2)
    bar(bm, (0.1, 0.05), (-0.012, 0.16), RUNE_WIDTH, RUNE_DEPTH, 2)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    for mat in (material("M_PickupShieldRim", GOLD, 0.0),
                material("M_PickupShieldFace", TEAL, 0.5),
                material("M_PickupShieldRune", GLYPH, 3.0)):
        mesh.materials.append(mat)

    ob = bpy.data.objects.new("ShieldPickup", mesh)
    bpy.context.scene.collection.objects.link(ob)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART_DIR, "ShieldPickup.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(GAME_DIR, "ShieldPickup.fbx"),
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="STRIP",
    )
    print("SHIELD_PICKUP_OK", len(mesh.vertices), len(mesh.polygons))


main()
