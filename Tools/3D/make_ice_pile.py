# -*- coding: utf-8 -*-
"""生成低/中/高三档冰块堆概念图（低模方块随机堆），供 Tripo 使用。

用法：
  & D:\Blender\blender.exe -b -P Tools\3D\make_ice_pile.py -- --level low --out <png> [--glb <glb>] [--size 1024]
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
OUT = arg("--out", "ice.png")
GLB = arg("--glb", None)
SIZE = int(arg("--size", "1024"))

CONF = {
    "low":  {"count": 12,  "layers": 1, "radius": 0.17, "height": 0.05, "seed": 11},
    "mid":  {"count": 55,  "layers": 3, "radius": 0.19, "height": 0.16, "seed": 22},
    "high": {"count": 130, "layers": 5, "radius": 0.20, "height": 0.30, "seed": 33},
}
cfg = CONF[LEVEL]
random.seed(cfg["seed"])

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def mat_ice():
    m = bpy.data.materials.new("ice")
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.45, 0.68, 0.88, 1)
    b.inputs["Roughness"].default_value = 0.22
    b.inputs["Metallic"].default_value = 0.0
    b.inputs["Specular IOR Level"].default_value = 0.6
    return m


ICE = mat_ice()

# 地面（接收阴影）
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, -0.001))
ground = bpy.context.object
gm = bpy.data.materials.new("ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (1, 1, 1, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
ground.data.materials.append(gm)

# 堆冰块
placed = 0
attempts = 0
while placed < cfg["count"] and attempts < cfg["count"] * 30:
    attempts += 1
    t = placed / max(1, cfg["count"] - 1)
    layer = min(cfg["layers"] - 1, int(t * cfg["layers"]))
    r_max = cfg["radius"] * (1.0 - 0.55 * layer / max(1, cfg["layers"] - 1))
    r = r_max * math.sqrt(random.random())
    a = random.random() * math.tau
    x, y = r * math.cos(a), r * math.sin(a)
    z = 0.025 + layer * (cfg["height"] / max(1, cfg["layers"])) + random.uniform(-0.008, 0.012)
    s = random.uniform(0.048, 0.075)
    bpy.ops.mesh.primitive_cube_add(size=s, location=(x, y, z),
                                    rotation=(random.uniform(-0.25, 0.25),
                                              random.uniform(-0.25, 0.25),
                                              random.random() * math.tau))
    ob = bpy.context.object
    ob.name = "ice_%03d" % placed
    mod = ob.modifiers.new("bev", "BEVEL")
    mod.width = 0.006
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


area("key", (0.7, -0.9, 1.1), 8, 1.2, (math.radians(45), 0, math.radians(35)))
area("fill", (-0.8, -0.7, 0.6), 3, 1.5, (math.radians(70), 0, math.radians(-40)))
area("rim", (0, 0.9, 0.9), 4, 1.0, (math.radians(-55), 0, 0))

world = bpy.data.worlds.new("W")
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (1, 1, 1, 1)
bg.inputs[1].default_value = 0.7
scene.world = world

# 相机：3/4 俯视
target = bpy.data.objects.new("target", None)
target.location = (0, 0, cfg["height"] * 0.35 + 0.03)
scene.collection.objects.link(target)
cam_data = bpy.data.cameras.new("cam")
cam_data.lens = 50
cam = bpy.data.objects.new("cam", cam_data)
cam.location = (0.46, -0.56, 0.44)
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
print("RENDERED", scene.render.filepath, "cubes", placed)

if GLB:
    bpy.ops.object.select_all(action="DESELECT")
    for o in scene.objects:
        if o.type == "MESH" and o.name.startswith("ice_"):
            o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(GLB), export_format="GLB",
                              use_selection=True, export_apply=True)
    print("EXPORTED", os.path.abspath(GLB))
