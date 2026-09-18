"""
로우폴리 나무 5종 생성 스크립트 — 원본 창작물.

실행 방법
  헤드리스:  blender --background --python Tools/Blender/make_trees.py -- <out_dir>
  MCP:       execute_blender_code 로 exec(open(path).read()) (인자 없으면 기본 경로로 저장)

스타일: 라임색 각진 잎 덩어리(이코스피어 + 꼭짓점 흔들기, 플랫 셰이딩) + 주황빛 갈색 테이퍼 줄기.
단위 m, 발밑 원점. 머티리얼: Leaf / Bark. 나무마다 FBX 1개 (Tree_Blob, Tree_Tall, Tree_Cone, Tree_Small, Tree_Round).
"""
import math
import random
import sys

import bpy
from mathutils import Vector

random.seed(20260919)

# ---------- 씬 정리 (MCP에서도 안전하도록 factory reset 대신 오브젝트만 삭제) ----------
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes):
    if m.users == 0:
        bpy.data.meshes.remove(m)


def make_material(name, rgb, roughness=0.9):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1.0)
    bsdf.inputs['Roughness'].default_value = roughness
    m.diffuse_color = (*rgb, 1.0)
    return m


M_LEAF = make_material("Leaf", (0.62, 0.84, 0.16))
M_BARK = make_material("Bark", (0.80, 0.47, 0.20))

parts = []


def finish(obj, mat, name):
    obj.name = name
    obj.data.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def leaf(loc, r, jitter=0.12, subdiv=1, scale=(1, 1, 1)):
    """각진 잎 덩어리"""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=r, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    for v in o.data.vertices:
        v.co += Vector((random.uniform(-jitter, jitter) * r for _ in range(3)))
    return finish(o, M_LEAF, "Leaf")


def limb(p0, p1, r0, r1, verts=7):
    """p0→p1 테이퍼 원기둥 (줄기·가지)"""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=1.0, depth=d.length, location=p0 + d / 2)
    o = bpy.context.active_object
    for v in o.data.vertices:
        r = r1 if v.co.z > 0 else r0
        v.co.x *= r
        v.co.y *= r
    o.rotation_euler = Vector((0, 0, 1)).rotation_difference(d).to_euler()
    return finish(o, M_BARK, "Limb")


def cluster(center, n, r, spread):
    """중심 주변에 잎 덩어리 n개"""
    leaf(center, r)
    for _ in range(n - 1):
        off = Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-0.6, 1))) * spread
        leaf(Vector(center) + off, r * random.uniform(0.55, 0.9))


def assemble(name):
    """parts를 하나로 합치고 플랫 셰이딩, 발밑 z=0"""
    global parts
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.join()
    t = bpy.context.active_object
    t.name = name
    t.data.name = name
    bpy.ops.object.shade_flat()
    t.location = (0, 0, 0)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    min_z = min(v.co.z for v in t.data.vertices)
    for v in t.data.vertices:
        v.co.z -= min_z
    bpy.ops.object.select_all(action='DESELECT')
    parts = []
    print(f"[make_trees] {name}: verts={len(t.data.vertices)} faces={len(t.data.polygons)} dims={tuple(round(d, 2) for d in t.dimensions)}")
    return t


# ---------- 1. 뭉게 나무 (큰 덩어리 4개, 살짝 굽은 줄기) ----------
def tree_blob():
    limb((0, 0, 0), (0.08, 0.04, 0.9), 0.16, 0.10)
    limb((0.08, 0.04, 0.85), (-0.05, 0.0, 1.5), 0.10, 0.05)
    for loc, r in (((0, 0, 1.75), 0.62), ((-0.45, 0.15, 1.95), 0.5), ((0.4, -0.1, 2.05), 0.48), ((0.05, 0.3, 2.4), 0.42)):
        leaf(loc, r, jitter=0.1)
    return assemble("Tree_Blob")


# ---------- 2. 키 큰 가지 나무 (긴 줄기 + 가지 끝 작은 덩어리 군집) ----------
def tree_tall():
    limb((0, 0, 0), (0.05, 0, 3.6), 0.2, 0.09)
    tips = []
    for z, ang, ln in ((1.9, 0.3, 1.1), (2.4, 2.4, 0.9), (2.9, 4.2, 1.0), (1.5, 3.6, 0.7)):
        p0 = Vector((0.03, 0, z))
        p1 = p0 + Vector((math.cos(ang), math.sin(ang), 0.55)) * ln
        limb(p0, p1, 0.08, 0.04, verts=6)
        tips.append(p1)
    for p in tips:
        cluster(p, 6, 0.28, 0.24)
    cluster((0.05, 0, 3.75), 7, 0.32, 0.28)
    return assemble("Tree_Tall")


# ---------- 3. 원뿔 나무 (아래로 갈수록 넓어지는 덩어리 층) ----------
def tree_cone():
    limb((0, 0, 0), (0, 0, 1.2), 0.14, 0.08)
    z = 1.0
    for ring_r, r, k in ((0.62, 0.42, 6), (0.5, 0.38, 5), (0.36, 0.34, 5), (0.2, 0.3, 4)):
        leaf((0, 0, z + 0.1), r * 1.05, jitter=0.08)
        for i in range(k):
            a = i / k * math.tau + random.uniform(-0.2, 0.2)
            leaf((math.cos(a) * ring_r, math.sin(a) * ring_r, z + random.uniform(-0.05, 0.1)), r, jitter=0.1)
        z += r * 1.15
    leaf((0, 0, z + 0.15), 0.26, jitter=0.1)
    return assemble("Tree_Cone")


# ---------- 4. 작은 나무 (덩어리 하나, 가는 줄기) ----------
def tree_small():
    limb((0, 0, 0), (0.03, 0, 0.7), 0.07, 0.04, verts=6)
    leaf((0, 0, 1.05), 0.5, jitter=0.12)
    return assemble("Tree_Small")


# ---------- 5. 둥근 나무 (큰 덩어리 하나, subdiv 2) ----------
def tree_round():
    limb((0, 0, 0), (-0.06, 0.02, 1.0), 0.13, 0.08)
    limb((-0.06, 0.02, 0.95), (0.05, 0, 1.6), 0.08, 0.04)
    leaf((0, 0, 2.1), 0.95, jitter=0.1, subdiv=2, scale=(1, 1, 0.9))
    return assemble("Tree_Round")


trees = [tree_blob(), tree_tall(), tree_cone(), tree_small(), tree_round()]

# ---------- 내보내기 (나무마다 FBX 1개, 원점에서) ----------
argv = sys.argv
out_dir = argv[argv.index("--") + 1] if "--" in argv and argv.index("--") + 1 < len(argv) else "D:/Workspace/TowerDefense/Assets/_Project/Art/Models/Env"
import os
os.makedirs(out_dir, exist_ok=True)
for t in trees:
    bpy.ops.object.select_all(action='DESELECT')
    t.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=f"{out_dir}/{t.name}.fbx",
        use_selection=True,
        object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        path_mode='STRIP',
        embed_textures=False,
    )
    print(f"[make_trees] exported {out_dir}/{t.name}.fbx")

# 보기용 배치 (내보내기 후)
for i, t in enumerate(trees):
    t.location.x = (i - 2) * 2.6
