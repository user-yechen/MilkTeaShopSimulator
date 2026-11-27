# -*- coding: utf-8 -*-
"""生成「双头蒸汽机」概念图（低模 + 白底 3/4 视角），供 TripoSR 使用；可选导出 GLB。

用法：
  & D:\Blender\blender.exe -b -P Tools\3D\make_heater_concept.py -- --out <png> [--glb <glb>] [--size 1024]
"""
import bpy
import math
import os
import sys


def arg(name, default=None):
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) else default
    return default


OUT = arg("--out", "heater_concept.png")
GLB = arg("--glb", None)
SIZE = int(arg("--size", "1024"))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def mat(name, color, metallic=0.0, rough=0.5, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.5
    return m


def cube(name, loc, dims, material, rot=(0, 0, 0), bevel=0.005):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = o.modifiers.new("bev", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(material)
    return o


def cyl(name, loc, radius, depth, material, rot=(0, 0, 0), verts=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                        location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.materials.append(material)
    return o


STEEL = mat("steel", (0.30, 0.31, 0.33), metallic=0.0, rough=0.6)
DARK = mat("dark", (0.03, 0.03, 0.035), metallic=0.2, rough=0.45)
PANEL = mat("panel", (0.02, 0.02, 0.025), metallic=0.1, rough=0.35)
RED = mat("red", (0.70, 0.05, 0.05), metallic=0.1, rough=0.45)
SCREEN = mat("screen", (0.05, 0.25, 0.3), metallic=0.0, rough=0.2,
             emission=(0.1, 0.7, 0.8))
TRAY = mat("tray", (0.06, 0.06, 0.065), metallic=0.4, rough=0.5)
PIPE = mat("pipe", (0.16, 0.16, 0.18), metallic=0.6, rough=0.4)

# 机身
cube("body", (0, 0, 0.25), (0.30, 0.34, 0.50), STEEL, bevel=0.008)

# 前面板（贴在正面，略倾斜）
cube("panel", (0, -0.168, 0.435), (0.28, 0.030, 0.150), PANEL,
     rot=(math.radians(-10), 0, 0), bevel=0.004)
# 屏幕
cube("screen", (0, -0.187, 0.462), (0.080, 0.006, 0.045), SCREEN,
     rot=(math.radians(-10), 0, 0), bevel=0.002)
# 按键
for i in range(5):
    x = -0.080 + i * 0.040
    cyl("btn_%d" % i, (x, -0.187, 0.405), 0.0075, 0.007, DARK,
        rot=(math.radians(80), 0, 0), verts=16)

# 蒸汽杆 + 红色手柄
for side, x in ((-1, -0.070), (1, 0.070)):
    cyl("handle_%d" % side, (x, -0.180, 0.372), 0.014, 0.034, RED,
        rot=(math.radians(80), 0, 0), verts=20)
    cyl("wand_%d" % side, (x, -0.196, 0.265), 0.012, 0.200, PIPE,
        rot=(math.radians(6), 0, 0), verts=16)
    cyl("nozzle_%d" % side, (x, -0.199, 0.160), 0.006, 0.026, PIPE,
        rot=(math.radians(6), 0, 0), verts=12)

# 接水盘
cube("tray_base", (0, -0.085, 0.030), (0.30, 0.20, 0.040), TRAY, bevel=0.003)
cube("tray_top", (0, -0.085, 0.042), (0.27, 0.16, 0.006), DARK, bevel=0.001)

# 支脚
for sx in (-1, 1):
    for sy in (-1, 1):
        cyl("foot_%d_%d" % (sx, sy), (sx * 0.12, sy * 0.13, 0.012), 0.012, 0.024,
            DARK, verts=16)

# 侧面提手
cube("side_handle", (0.152, 0.02, 0.40), (0.012, 0.10, 0.028), DARK, bevel=0.003)


def area(name, loc, energy, size, rot):
    d = bpy.data.lights.new(name, "AREA")
    d.energy = energy
    d.size = size
    o = bpy.data.objects.new(name, d)
    o.location = loc
    o.rotation_euler = rot
    scene.collection.objects.link(o)


area("key", (0.8, -1.0, 1.1), 5, 1.2, (math.radians(52), 0, math.radians(38)))
area("fill", (-0.9, -0.9, 0.7), 2, 1.5, (math.radians(75), 0, math.radians(-45)))
area("rim", (0, 1.1, 0.9), 3, 1.0, (math.radians(-50), 0, 0))

world = bpy.data.worlds.new("W")
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (1, 1, 1, 1)
bg.inputs[1].default_value = 1.0
scene.world = world

# 相机：Track To 对准机身
target = bpy.data.objects.new("target", None)
target.location = (0, 0, 0.27)
scene.collection.objects.link(target)

cam_data = bpy.data.cameras.new("cam")
cam_data.lens = 50
cam = bpy.data.objects.new("cam", cam_data)
cam.location = (0.58, -0.88, 0.58)
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
print("RENDERED", scene.render.filepath)

if GLB:
    bpy.ops.object.select_all(action="DESELECT")
    for o in scene.objects:
        if o.type == "MESH":
            o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(GLB), export_format="GLB",
                              use_selection=True, export_apply=True)
    print("EXPORTED", os.path.abspath(GLB))
