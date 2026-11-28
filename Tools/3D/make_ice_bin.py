# -*- coding: utf-8 -*-
"""按前制作台凹槽尺寸（2.82 × 4.32 × 0.40）生成冰块堆，低/中/高三档。

用法：
  & D:\Blender\blender.exe -b -P Tools\3D\make_ice_bin.py -- --level mid --out <png> [--glb <glb>] [--size 1024]

尺寸来源：D:\Blender\前制作台.blend 的 Cube.001（x ±1.41, y -9.22~-4.90, z 0.178~0.576），
即 2.82 × 4.32 × 0.398 的矩形凹槽；冰块堆按此长宽、按档位填不同高度。
"""
import bpy
import math
import os
import random
import sys


def arg(name, default=None):
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) else default
    return default


LEVEL = arg("--level", "mid")
OUT = arg("--out", "ice_bin.png")
GLB = arg("--glb", None)
SIZE = int(arg("--size", "1024"))

BIN_X, BIN_Y, BIN_Z = 2.718, 3.339, 0.868
MARGIN = 0.98

CONF = {
    "low":  {"count": 20,  "fill": 0.22, "size": (0.34, 0.46), "seed": 101},
    "mid":  {"count": 70,  "fill": 0.50, "size": (0.34, 0.46), "seed": 202},
    "high": {"count": 150, "fill": 0.85, "size": (0.32, 0.44), "seed": 303},
}
cfg = CONF[LEVEL]
random.seed(cfg["seed"])

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def ice_mat():
    m = bpy.data.materials.new("ice")
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.42, 0.66, 0.88, 1)
    b.inputs["Roughness"].default_value = 0.18
    b.inputs["Metallic"].default_value = 0.0
    return m


ICE = ice_mat()

# 地面
bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, -0.002))
gm = bpy.data.materials.new("ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (1, 1, 1, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
bpy.context.object.data.materials.append(gm)

half_x = BIN_X * 0.5 * MARGIN
half_y = BIN_Y * 0.5 * MARGIN
top = BIN_Z * cfg["fill"]

placed = 0
tries = 0
while placed < cfg["count"] and tries < cfg["count"] * 40:
    tries += 1
    s = random.uniform(*cfg["size"])
    # 高度按层分布：越靠上越收拢，形成自然堆
    t = placed / max(1, cfg["count"] - 1)
    z = 0.02 + top * (t ** 0.8) + random.uniform(-0.02, 0.02)
    # 上层向中心收
    shrink = 1.0 - 0.45 * (z / max(top, 1e-6))
    lim_x = max(0.05, half_x * shrink * 0.92 - s * 0.5)
    lim_y = max(0.05, half_y * shrink * 0.92 - s * 0.5)
    x = random.uniform(-lim_x, lim_x)
    y = random.uniform(-lim_y, lim_y)
    bpy.ops.mesh.primitive_cube_add(size=s, location=(x, y, z),
                                    rotation=(random.uniform(-0.2, 0.2),
                                              random.uniform(-0.2, 0.2),
                                              random.random() * math.tau))
    ob = bpy.context.object
    ob.name = "ice_%03d" % placed
    mod = ob.modifiers.new("bev", "BEVEL")
    mod.width = min(0.03, s * 0.08)
    mod.segments = 2
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.materials.append(ICE)
    placed += 1

# 灯光
def area(name, loc, energy, size, rot):
    d = bpy.data.lights.new(name, "AREA")
    d.energy = energy
    d.size = size
    o = bpy.data.objects.new(name, d)
    o.location = loc
    o.rotation_euler = rot
    scene.collection.objects.link(o)


area("key", (1.6, -2.2, 3.2), 18, 3.0, (math.radians(45), 0, math.radians(35)))
area("fill", (-2.0, -1.6, 1.6), 6, 3.0, (math.radians(70), 0, math.radians(-40)))
area("rim", (0, 2.2, 2.0), 10, 2.0, (math.radians(-55), 0, 0))

world = bpy.data.worlds.new("W")
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (1, 1, 1, 1)
bg.inputs[1].default_value = 0.9
scene.world = world

# 相机：3/4 俯视，覆盖 4.3 长的堆
target = bpy.data.objects.new("target", None)
target.location = (0, 0, top * 0.35)
scene.collection.objects.link(target)
cam_data = bpy.data.cameras.new("cam")
cam_data.lens = 55
cam = bpy.data.objects.new("cam", cam_data)
cam.location = (3.4, -4.4, 3.6)
scene.collection.objects.link(cam)
scene.camera = cam
con = cam.constraints.new("TRACK_TO")
con.target = target
con.track_axis = "TRACK_NEGATIVE_Z"
con.up_axis = "UP_Y"

scene.render.resolution_x = SIZE
scene.render.resolution_y = SIZE
scene.render.film_transparent = False
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = os.path.abspath(OUT)
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except Exception:
    scene.render.engine = "BLENDER_EEVEE"
try:
    scene.eevee.taa_render_samples = 32
except Exception:
    pass
scene.view_settings.view_transform = "Standard"

os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
bpy.ops.render.render(write_still=True)
print("RENDERED", scene.render.filepath, "cubes", placed, "fill", top)

if GLB:
    bpy.ops.object.select_all(action="DESELECT")
    for o in scene.objects:
        if o.type == "MESH" and o.name.startswith("ice_"):
            o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(GLB), export_format="GLB",
                              use_selection=True, export_apply=True)
    print("EXPORTED", os.path.abspath(GLB))
