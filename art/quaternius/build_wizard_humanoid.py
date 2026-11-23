"""Builds a Humanoid-ready copy of the Quaternius wizard for the third-person player.

The Quaternius rig drives each leg with an IK constraint towards a foot bone that hangs from
`Root`. Unity's Humanoid avatar needs every foot to descend from its lower leg, so this script
drops the two leg IK constraints, re-parents `Foot.L`/`Foot.R` under `LowerLeg.L`/`LowerLeg.R`
(keeping their rest transforms, so skin weights and the mesh are unchanged) and exports mesh
plus armature without animation.

The wizard's own combat actions are exported too, one file per action on the same rig, so Unity
can import them as Humanoid clips. Without the leg IK their legs are not usable; they are meant
for upper-body layers, with locomotion coming from the Universal Animation Library. (One action
per export with its slot assigned: Blender 4.5's "all actions" bake writes motionless takes.)

`art/quaternius/Wizard.blend` is only read, never saved. The menu keeps using the original
`Wizard.fbx`.

Run headless from the repository root:

    blender -b art/quaternius/Wizard.blend --factory-startup --python art/quaternius/build_wizard_humanoid.py

Output:
    game/Assets/_Project/Art/Characters/Wizard/WizardHumanoid.fbx
    game/Assets/_Project/Art/Characters/Wizard/WizardHumanoid@<Action>.fbx (one per COMBAT_ACTIONS)
"""

import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "Characters", "Wizard")
OUT_FILE = os.path.join(OUT_DIR, "WizardHumanoid.fbx")
COMBAT_ACTIONS = ("Staff_Attack", "Spell1", "Spell2", "RecieveHit")

ARMATURE = "CharacterArmature"
FEET = {"Foot.L": "LowerLeg.L", "Foot.R": "LowerLeg.R"}


def rest_pose(arm):
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


def drop_leg_ik(arm):
    for leg in FEET.values():
        pb = arm.pose.bones[leg]
        for con in [c for c in pb.constraints if c.type == "IK"]:
            pb.constraints.remove(con)


def reparent_feet(arm):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    bones = arm.data.edit_bones
    for foot, leg in FEET.items():
        bones[foot].use_connect = False
        bones[foot].parent = bones[leg]
    # PoleTarget.L/R stay: the mesh carries vertex groups for them, and extra bones under Root
    # do not affect the Humanoid mapping.
    bpy.ops.object.mode_set(mode="OBJECT")


def main():
    arm = bpy.data.objects[ARMATURE]
    rest_pose(arm)
    drop_leg_ik(arm)
    reparent_feet(arm)

    for obj in bpy.data.objects:
        if obj.animation_data:
            obj.animation_data_clear()

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=OUT_FILE,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        use_mesh_modifiers=True,
        path_mode="STRIP",
    )
    for foot, leg in FEET.items():
        print(f"{foot} -> {arm.data.bones[foot].parent.name}")
    print(f"Exported {OUT_FILE}")

    export_actions(arm)


def export_actions(arm):
    # The meshes go along so each file keeps the bind pose and the same hierarchy as the model;
    # without them Unity takes the first animated frame as the avatar's reference pose.
    data = arm.animation_data or arm.animation_data_create()
    scene = bpy.context.scene
    for name in COMBAT_ACTIONS:
        action = bpy.data.actions[name]
        data.action = action
        if action.slots:
            data.action_slot = action.slots[0]
        scene.frame_start, scene.frame_end = (int(f) for f in action.frame_range)
        path = os.path.join(OUT_DIR, f"WizardHumanoid@{name}.fbx")
        bpy.ops.export_scene.fbx(
            filepath=path,
            object_types={"ARMATURE", "MESH"},
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            bake_anim=True,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_force_startend_keying=True,
            path_mode="STRIP",
        )
        print(f"Exported {path}")


if __name__ == "__main__":
    main()
