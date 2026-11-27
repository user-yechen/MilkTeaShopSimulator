# -*- coding: utf-8 -*-
"""多视角渲染 GLB（用于 TripoSR 输入 / 结果预览 / 对比图）。

用法：
  blender -b -P Tools/3D/render_views.py -- ^
    --model a.glb --out-dir Temp/views --views 0,120,240 --size 512 ^
    --pitch 15 --perspective --bg 0.82
  --bg transparent 时输出透明底（PNG RGBA）。
"""
import bpy
import sys
import os
import math
import argparse
from mathutils import Vector


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--model", required=True)
    p.add_argument("--out-dir", required=True)
    p.add_argument("--views", default="0,120,240")
    p.add_argument("--size", type=int, default=512)
    p.add_argument("--pitch", type=float, default=15.0)
    p.add_argument("--samples", type=int, default=48)
    p.add_argument("--perspective", action="store_true")
    p.add_argument("--bg", default="0.82", help="灰度值或 transparent")
    p.add_argument("--prefix", default="view")
    return p.parse_args(argv)


def bounds(objs):
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            for i in range(3):
                mins[i] = min(mins[i], w[i])
                maxs[i] = max(maxs[i], w[i])
    return mins, maxs


def add_area(name, loc, target, energy, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = (target - loc).normalized().to_track_quat('-Z', 'Y')


def main():
    a = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene

    bpy.ops.import_scene.gltf(filepath=os.path.abspath(a.model))
    meshes = [o for o in scene.objects if o.type == 'MESH']
    if not meshes:
        print("ERROR: no mesh")
        sys.exit(1)

    mins, maxs = bounds(meshes)
    center = (mins + maxs) * 0.5
    dims = maxs - mins
    max_dim = max(dims.x, dims.y, dims.z)

    transparent = a.bg.lower() == "transparent"

    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.85, 0.85, 0.85, 1)
    bg.inputs[1].default_value = 0.7
    scene.world = world

    size = max_dim
    add_area("Key", center + Vector((-size * 1.6, -size * 1.8, size * 1.6)), center, 260 * size * size, size * 2)
    add_area("Fill", center + Vector((size * 1.8, -size * 1.4, size * 0.8)), center, 90 * size * size, size * 2.5)
    add_area("Rim", center + Vector((size * 0.4, size * 2.0, size * 1.2)), center, 160 * size * size, size * 1.5)

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    if a.perspective:
        cam_data.type = 'PERSP'
        cam_data.lens = 60
    else:
        cam_data.type = 'ORTHO'
        cam_data.ortho_scale = max_dim * 1.35

    target = bpy.data.objects.new("Target", None)
    bpy.context.collection.objects.link(target)
    target.location = center
    con = cam.constraints.new('TRACK_TO')
    con.target = target
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'

    scene.render.engine = 'CYCLES'
    scene.cycles.samples = a.samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = a.size
    scene.render.resolution_y = a.size
    scene.render.film_transparent = transparent
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
    if not transparent:
        bg.inputs[0].default_value = (float(a.bg), float(a.bg), float(a.bg), 1)
        bg.inputs[1].default_value = 1.0

    os.makedirs(os.path.abspath(a.out_dir), exist_ok=True)
    dist = max_dim * 3.2
    pitch = math.radians(a.pitch)

    for yaw_deg in [float(v) for v in a.views.split(",") if v.strip()]:
        yaw = math.radians(yaw_deg)
        direction = Vector((
            math.sin(yaw) * math.cos(pitch),
            -math.cos(yaw) * math.cos(pitch),
            math.sin(pitch),
        )).normalized()
        cam.location = center + direction * dist
        out = os.path.join(os.path.abspath(a.out_dir), "%s_%03d.png" % (a.prefix, int(yaw_deg)))
        scene.render.filepath = out
        bpy.ops.render.render(write_still=True)
        print("RENDERED", out)


if __name__ == "__main__":
    main()
