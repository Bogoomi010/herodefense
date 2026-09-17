"""
로우폴리 양(Sheep) 몹 모델 생성 스크립트 — 원본 창작물.

실행 방법
  헤드리스:  blender --background --python Tools/Blender/make_sheep.py -- <out.fbx>
  MCP:       execute_blender_code 로 이 파일 내용을 실행 (인자 없으면 기본 경로로 저장)

설계 (docs/PORTING_PLAN.md 5.2 스타일 가이드)
  - 각진 양털 덩어리(이코스피어 + 꼭짓점 흔들기, 플랫 셰이딩), 작은 팔각 머리, 분홍 귀 안쪽, 짧은 다리, 검은 발굽
  - 단위 m, 발밑이 원점, 정면 -Y (FBX 내보내기 후 Unity에서 +Z 정면)
  - 머티리얼: Wool(양털, Unity에서 계열 색으로 틴트) / Skin / EarInner / Dark / Hoof
"""
import math
import random
import sys

import bmesh
import bpy
from mathutils import Vector

random.seed(20260918)

# ---------- 씬 초기화 ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0


def make_material(name, rgb, roughness=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1.0)
    bsdf.inputs['Roughness'].default_value = roughness
    m.diffuse_color = (*rgb, 1.0)
    return m


M_WOOL = make_material("Wool", (0.93, 0.93, 0.95))
M_SKIN = make_material("Skin", (0.96, 0.88, 0.83))
M_MUZZLE = make_material("Muzzle", (0.88, 0.76, 0.72))
M_EAR = make_material("EarInner", (0.93, 0.60, 0.60))
M_DARK = make_material("Dark", (0.06, 0.06, 0.07), 0.8)
M_HOOF = make_material("Hoof", (0.18, 0.18, 0.20))

parts = []


def finish(obj, mat, name):
    obj.name = name
    obj.data.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def ico(name, loc, scale, mat, subdiv=1, jitter=0.0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    if jitter > 0:
        for v in o.data.vertices:
            v.co += Vector((random.uniform(-jitter, jitter) for _ in range(3)))
    return finish(o, mat, name)


def box(name, loc, size, mat, bevel=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = size
    o.rotation_euler = rot
    if bevel > 0:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=bevel, segments=1, affect='EDGES')
        bm.to_mesh(o.data)
        bm.free()
    return finish(o, mat, name)


def cyl(name, loc, radius, depth, mat, verts=6):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc)
    o = bpy.context.active_object
    return finish(o, mat, name)


def sphere(name, loc, radius, mat, seg=8, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=radius, location=loc)
    o = bpy.context.active_object
    return finish(o, mat, name)


# ---------- 몸통 (양털) ----------
# x = 좌우, y = 앞뒤(-Y 정면), z = 상하
ico("Body", (0, 0.05, 0.66), (0.52, 0.68, 0.46), M_WOOL, subdiv=2, jitter=0.045)
ico("Tail", (0, 0.74, 0.78), (0.11, 0.10, 0.10), M_WOOL, subdiv=1, jitter=0.02)

# ---------- 머리 ----------
HEAD = (0, -0.64, 0.84)
box("Head", HEAD, (0.40, 0.40, 0.36), M_SKIN, bevel=0.09)
box("Muzzle", (0, -0.84, 0.71), (0.28, 0.14, 0.17), M_MUZZLE, bevel=0.035)
ico("HeadWool", (0, -0.58, 1.06), (0.34, 0.33, 0.18), M_WOOL, subdiv=1, jitter=0.03)

# 눈 (검은 구 + 하이라이트)
for sx in (-1, 1):
    sphere(f"Eye{'L' if sx < 0 else 'R'}", (sx * 0.16, -0.845, 0.90), 0.05, M_DARK)
    sphere(f"EyeShine{'L' if sx < 0 else 'R'}", (sx * 0.175, -0.885, 0.915), 0.016, M_SKIN)

# 코
box("Nose", (0, -0.915, 0.73), (0.06, 0.03, 0.035), M_DARK)

# 귀 — 납작한 타원(이코스피어)을 바깥·아래로 눕힌다. 안쪽은 분홍
for sx in (-1, 1):
    side = 'L' if sx < 0 else 'R'
    rot = (0.25, sx * 0.95, sx * -0.25)
    for (name, loc, scale, mat) in (
        (f"Ear{side}", (sx * 0.43, -0.60, 0.92), (0.19, 0.06, 0.11), M_SKIN),
        (f"EarIn{side}", (sx * 0.44, -0.635, 0.925), (0.13, 0.025, 0.07), M_EAR),
    ):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0, location=loc)
        o = bpy.context.active_object
        o.scale = scale
        o.rotation_euler = rot
        finish(o, mat, name)

# ---------- 다리 + 발굽 ----------
for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
    x, y = sx * 0.27, sy * 0.30 + 0.05
    cyl(f"Leg{i}", (x, y, 0.28), 0.085, 0.36, M_SKIN)
    cyl(f"Hoof{i}", (x, y, 0.06), 0.092, 0.12, M_HOOF)

# ---------- 합치기 / 플랫 셰이딩 / 원점 ----------
for o in parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.object.join()
sheep = bpy.context.active_object
sheep.name = "Sheep"
sheep.data.name = "Sheep"
bpy.ops.object.shade_flat()
sheep.location = (0, 0, 0)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# 발밑을 z=0으로 맞춘다
min_z = min((sheep.matrix_world @ v.co).z for v in sheep.data.vertices)
for v in sheep.data.vertices:
    v.co.z -= min_z

dims = sheep.dimensions
print(f"[make_sheep] verts={len(sheep.data.vertices)} faces={len(sheep.data.polygons)} dims={tuple(round(d, 3) for d in dims)}")

# ---------- 내보내기 ----------
argv = sys.argv
out = argv[argv.index("--") + 1] if "--" in argv and argv.index("--") + 1 < len(argv) else "D:/Workspace/TowerDefense/Assets/_Project/Art/Models/Mobs/Sheep.fbx"
bpy.ops.object.select_all(action='DESELECT')
sheep.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=out,
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
print(f"[make_sheep] exported {out}")
