# -*- coding: utf-8 -*-
"""Blender 无头减面：把 GLB/FBX 减到指定三角面数并导出 GLB。

用法：
  blender -b -P Tools/3D/decimate_to_budget.py -- --in raw.glb --tris 1200 --out final.glb
"""
import bpy
import sys
import os
import argparse

def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--in", dest="src", required=True)
    p.add_argument("--tris", type=int, required=True)
    p.add_argument("--out", dest="dst", required=True)
    return p.parse_args(argv)

def main():
    a = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = os.path.abspath(a.src)
    ext = os.path.splitext(src)[1].lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=src)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=src)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=src)
    else:
        print("ERROR: 不支持的格式 " + ext)
        sys.exit(1)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print("ERROR: 没有网格")
        sys.exit(1)

    before = sum(len(o.data.polygons) for o in meshes)
    ratio = min(1.0, a.tris / max(before, 1))

    for o in meshes:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        mod = o.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)

    after = sum(len(o.data.polygons) for o in meshes)
    os.makedirs(os.path.dirname(os.path.abspath(a.dst)), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=os.path.abspath(a.dst),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    print("DECIMATE %d -> %d tris, out=%s" % (before, after, a.dst))

if __name__ == "__main__":
    main()
