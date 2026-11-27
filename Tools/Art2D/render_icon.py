# -*- coding: utf-8 -*-
"""3D 模型 → 2D 图标（Blender 无头渲染，透明底）。

用法：
  blender -b -P Tools/Art2D/render_icon.py -- ^
    --model "Assets/模型/奶茶杯_大.glb" --out "ArtSource/2D/renders/大奶茶杯.png" ^
    --size 512 --yaw 0 --pitch 8 --lens 85 --samples 64 --contact-shadow

参数：
  --yaw    水平角（0=正对镜头，90=侧面）
  --pitch  俯角（0=平视，90=俯视）
  --ortho  使用正交相机（更接近扁平图标），默认开启
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
    p.add_argument("--out", required=True)
    p.add_argument("--size", type=int, default=512)
    p.add_argument("--yaw", type=float, default=0.0)
    p.add_argument("--pitch", type=float, default=8.0)
    p.add_argument("--lens", type=float, default=85.0)
    p.add_argument("--samples", type=int, default=64)
    p.add_argument("--ortho", action="store_true", default=True)
    p.add_argument("--perspective", action="store_true")
    p.add_argument("--contact-shadow", action="store_true")
    p.add_argument("--solid", type=float, default=0.0,
                   help=">0 时把材质压成奶白半透明（0.85–0.95），去掉玻璃透射")
    p.add_argument("--bg", type=float, default=0.85, help="环境亮度（影响反射，不进出图）")
    return p.parse_args(argv)


def world_bounds(objs):
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
    return obj


def main():
    a = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene

    bpy.ops.import_scene.gltf(filepath=os.path.abspath(a.model))
    meshes = [o for o in scene.objects if o.type == 'MESH']
    if not meshes:
        print("ERROR: 模型没有网格")
        sys.exit(1)

    mins, maxs = world_bounds(meshes)
    center = (mins + maxs) * 0.5
    dims = maxs - mins
    max_dim = max(dims.x, dims.y, dims.z)

    # 相机
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    yaw = math.radians(a.yaw)
    pitch = math.radians(a.pitch)
    direction = Vector((
        math.sin(yaw) * math.cos(pitch),
        -math.cos(yaw) * math.cos(pitch),
        math.sin(pitch),
    )).normalized()

    dist = max_dim * 4.0
    cam.location = center + direction * dist
    target = bpy.data.objects.new("Target", None)
    bpy.context.collection.objects.link(target)
    target.location = center
    c = cam.constraints.new('TRACK_TO')
    c.target = target
    c.track_axis = 'TRACK_NEGATIVE_Z'
    c.up_axis = 'UP_Y'

    if a.perspective:
        cam_data.type = 'PERSP'
        cam_data.lens = a.lens
    else:
        cam_data.type = 'ORTHO'
        cam_data.ortho_scale = max_dim * 1.25

    # 材质实心化：把玻璃/透明材质压成奶白半透明，贴近现有图标
    if a.solid > 0:
        for mat in bpy.data.materials:
            if not mat.use_nodes:
                continue
            for n in mat.node_tree.nodes:
                if n.type != 'BSDF_PRINCIPLED':
                    continue
                for key in ('Transmission Weight', 'Transmission', 'Alpha'):
                    if key not in n.inputs:
                        continue
                    for link in list(n.inputs[key].links):
                        mat.node_tree.links.remove(link)
                    n.inputs[key].default_value = 0.0 if key != 'Alpha' else a.solid
                if 'Roughness' in n.inputs and not n.inputs['Roughness'].is_linked:
                    n.inputs['Roughness'].default_value = 0.35

    # 灯光：主光左上、补光右、轮廓光后
    size = max_dim
    add_area("Key", center + Vector((-size * 1.6, -size * 1.8, size * 1.6)), center, 260 * size * size, size * 2)
    add_area("Fill", center + Vector((size * 1.8, -size * 1.4, size * 0.8)), center, 90 * size * size, size * 2.5)
    add_area("Rim", center + Vector((size * 0.4, size * 2.0, size * 1.2)), center, 160 * size * size, size * 1.5)

    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (a.bg, a.bg, a.bg, 1)
    bg.inputs[1].default_value = 0.6
    scene.world = world

    scene.render.engine = 'CYCLES'
    scene.cycles.samples = a.samples
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.resolution_x = a.size
    scene.render.resolution_y = a.size
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    out = os.path.abspath(a.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("RENDERED", out)


if __name__ == "__main__":
    main()
