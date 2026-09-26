"""
로우폴리 농장 크립 모델 생성 스크립트 — 원본 창작물 (docs/STORY_ZONES.md 농장 구역).
닭(Chicken) · 소(Cow) · 돼지(Pig) · 성난 황소(Bull, 농장 보스)를 만들어 FBX로 내보낸다.

실행 방법
  MCP:       execute_blender_code 로 exec(open(이 파일).read())
  헤드리스:  blender --background --python Tools/Blender/make_farm.py -- <출력 폴더>

설계 (make_sheep.py와 같은 규칙)
  - 플랫 셰이딩 로우폴리, 단위 m, 발밑이 원점, 정면 -Y (FBX 내보내기 후 Unity에서 +Z 정면)
  - 몸통 머티리얼 이름은 "Body" — Unity에서 크립 색으로 칠하고 HP에 따라 어두워진다(EnemyView)
  - 씬 초기화는 read_factory_settings 대신 오브젝트·머티리얼만 지운다(MCP 애드온이 꺼지지 않게)
"""
import math
import random
import sys

import bmesh
import bpy
from mathutils import Vector

OUT_DIR = "D:/Workspace/repo_herodefense/Assets/_Project/Art/Models/Mobs"
if "--" in sys.argv and sys.argv.index("--") + 1 < len(sys.argv):
    OUT_DIR = sys.argv[sys.argv.index("--") + 1]


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


class Builder:
    def __init__(self, seed):
        random.seed(seed)
        self.parts = []
        self.mats = {}

    def mat(self, name, rgb, roughness=0.9):
        if name in self.mats:
            return self.mats[name]
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        bsdf.inputs['Base Color'].default_value = (*rgb, 1.0)
        bsdf.inputs['Roughness'].default_value = roughness
        m.diffuse_color = (*rgb, 1.0)
        self.mats[name] = m
        return m

    def _finish(self, o, mat, name, rot=None):
        if rot is not None:
            o.rotation_euler = rot
        o.name = name
        o.data.name = name
        o.data.materials.clear()
        o.data.materials.append(mat)
        self.parts.append(o)
        return o

    def ico(self, name, loc, scale, mat, subdiv=1, jitter=0.0, rot=None):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=1.0, location=loc)
        o = bpy.context.active_object
        o.scale = scale
        if jitter > 0:
            for v in o.data.vertices:
                v.co += Vector([random.uniform(-jitter, jitter) for _ in range(3)])
        return self._finish(o, mat, name, rot)

    def box(self, name, loc, size, mat, bevel=0.0, rot=None):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
        o = bpy.context.active_object
        o.scale = size
        if bevel > 0:
            bm = bmesh.new()
            bm.from_mesh(o.data)
            bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=bevel, segments=1, affect='EDGES')
            bm.to_mesh(o.data)
            bm.free()
        return self._finish(o, mat, name, rot)

    def cyl(self, name, loc, radius, depth, mat, verts=6, rot=None):
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc)
        return self._finish(bpy.context.active_object, mat, name, rot)

    def cone(self, name, loc, r1, depth, mat, verts=6, rot=None, r2=0.0):
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc)
        return self._finish(bpy.context.active_object, mat, name, rot)

    def sphere(self, name, loc, radius, mat, seg=8, rings=6):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=radius, location=loc)
        return self._finish(bpy.context.active_object, mat, name)

    def eyes(self, x, y, z, r, dark, shine):
        for sx in (-1, 1):
            s = 'L' if sx < 0 else 'R'
            self.sphere(f"Eye{s}", (sx * x, y, z), r, dark)
            self.sphere(f"EyeShine{s}", (sx * (x + r * 0.3), y - r * 0.8, z + r * 0.3), r * 0.32, shine)

    def join_export(self, name):
        bpy.ops.object.select_all(action='DESELECT')
        for o in self.parts:
            o.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        bpy.ops.object.join()
        obj = bpy.context.active_object
        obj.name = name
        obj.data.name = name
        bpy.ops.object.shade_flat()
        obj.location = (0, 0, 0)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        min_z = min(v.co.z for v in obj.data.vertices)
        for v in obj.data.vertices:
            v.co.z -= min_z
        out = f"{OUT_DIR}/{name}.fbx"
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.ops.export_scene.fbx(
            filepath=out, use_selection=True, object_types={'MESH'},
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
            path_mode='STRIP', embed_textures=False,
        )
        dims = tuple(round(d, 2) for d in obj.dimensions)
        print(f"[make_farm] {name}: verts={len(obj.data.vertices)} dims={dims} -> {out}")
        return obj


# x = 좌우, y = 앞뒤(-Y 정면), z = 상하

def chicken():
    b = Builder(20260927)
    body = b.mat("Body", (0.97, 0.96, 0.93))
    comb = b.mat("Comb", (0.85, 0.12, 0.10))
    beak = b.mat("Beak", (0.98, 0.72, 0.15))
    dark = b.mat("Dark", (0.06, 0.06, 0.07), 0.8)
    b.ico("Body", (0, 0.05, 0.62), (0.34, 0.44, 0.34), body, subdiv=2, jitter=0.025)
    b.ico("Head", (0, -0.30, 1.02), (0.19, 0.20, 0.21), body, subdiv=2, jitter=0.01)
    b.ico("Neck", (0, -0.20, 0.84), (0.17, 0.17, 0.2), body, subdiv=1)
    for i, (y, z, h) in enumerate(((-0.40, 1.24, 0.10), (-0.30, 1.27, 0.12), (-0.20, 1.23, 0.09))):
        b.box(f"Comb{i}", (0, y, z), (0.05, 0.08, h), comb, bevel=0.015)
    b.cone("Beak", (0, -0.52, 1.0), 0.07, 0.16, beak, verts=4, rot=(math.radians(90), 0, 0))
    b.ico("Wattle", (0, -0.45, 0.88), (0.04, 0.03, 0.07), comb)
    b.eyes(0.12, -0.44, 1.07, 0.035, dark, body)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.ico(f"Wing{s}", (sx * 0.32, 0.08, 0.66), (0.07, 0.30, 0.20), body, subdiv=1, rot=(0.25, 0, 0))
    for i, (x, r) in enumerate(((-0.08, -0.25), (0.0, 0.0), (0.08, 0.25))):
        b.box(f"Tail{i}", (x, 0.46, 0.92), (0.06, 0.14, 0.34), body, bevel=0.02, rot=(math.radians(-25), r, 0))
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.cyl(f"Leg{s}", (sx * 0.12, 0.05, 0.16), 0.03, 0.32, beak)
        for j, a in enumerate((-0.5, 0.0, 0.5)):
            b.box(f"Toe{s}{j}", (sx * 0.12 + math.sin(a) * 0.06, -0.02 - math.cos(a) * 0.06, 0.015), (0.03, 0.12, 0.03), beak, rot=(0, 0, a))
    return b.join_export("Chicken")


def cow(name="Cow", bull=False):
    b = Builder(20260928 if bull else 20260929)
    body = b.mat("Body", (0.36, 0.20, 0.13) if bull else (0.96, 0.95, 0.92))
    spot = b.mat("Spot", (0.08, 0.07, 0.07))
    muzzle = b.mat("Muzzle", (0.55, 0.36, 0.33) if bull else (0.93, 0.70, 0.70))
    horn = b.mat("Horn", (0.93, 0.88, 0.74), 0.6)
    dark = b.mat("Dark", (0.06, 0.06, 0.07), 0.8)
    hoof = b.mat("Hoof", (0.18, 0.18, 0.20))
    shine = b.mat("Shine", (0.95, 0.95, 0.95))
    b.box("Body", (0, 0.05, 1.0), (0.62, 1.25, 0.60), body, bevel=0.12)
    if bull:
        b.ico("Hump", (0, -0.30, 1.28), (0.36, 0.36, 0.30), body, subdiv=1, jitter=0.02)
    else:
        for i, (sx, y, z, sc) in enumerate(((1, 0.25, 1.10, 0.22), (-1, -0.15, 1.00, 0.18), (1, -0.30, 0.90, 0.14), (-1, 0.40, 1.12, 0.16))):
            b.ico(f"Spot{i}", (sx * 0.31, y, z), (0.03, sc, sc * 0.8), spot, subdiv=1)
        b.ico("UdderSpot", (0, 0.25, 0.68), (0.12, 0.10, 0.07), muzzle)
    head_z = 1.18 if not bull else 1.10
    b.box("Head", (0, -0.78, head_z), (0.44, 0.46, 0.44), body, bevel=0.08)
    b.box("Muzzle", (0, -1.03, head_z - 0.12), (0.40, 0.14, 0.26), muzzle, bevel=0.05)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.sphere(f"Nostril{s}", (sx * 0.08, -1.10, head_z - 0.10), 0.03, dark)
    b.eyes(0.17, -1.0, head_z + 0.08, 0.045, b.mat("Eye", (0.55, 0.05, 0.05)) if bull else dark, shine)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.ico(f"Ear{s}", (sx * 0.30, -0.72, head_z + 0.10), (0.14, 0.04, 0.07), body, rot=(0, sx * 0.3, 0))
        if bull:
            b.cone(f"Horn{s}", (sx * 0.36, -0.78, head_z + 0.26), 0.07, 0.42, horn, rot=(0, sx * math.radians(-60), 0))
        else:
            b.cone(f"Horn{s}", (sx * 0.18, -0.78, head_z + 0.28), 0.045, 0.16, horn)
    if bull:
        bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.015, location=(0, -1.12, head_z - 0.2), rotation=(math.radians(90), 0, 0))
        b._finish(bpy.context.active_object, b.mat("Ring", (0.85, 0.66, 0.18), 0.35), "NoseRing")
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        x, y = sx * 0.21, sy * 0.44 + 0.05
        b.cyl(f"Leg{i}", (x, y, 0.40), 0.10, 0.62, body)
        b.cyl(f"Hoof{i}", (x, y, 0.06), 0.105, 0.12, hoof)
    b.cyl("Tail", (0, 0.68, 0.95), 0.025, 0.55, body, rot=(math.radians(-15), 0, 0))
    b.ico("TailTip", (0, 0.72, 0.66), (0.05, 0.05, 0.09), spot)
    return b.join_export(name)


def pig():
    b = Builder(20260930)
    body = b.mat("Body", (0.95, 0.66, 0.70))
    snout = b.mat("Snout", (0.88, 0.50, 0.56))
    dark = b.mat("Dark", (0.06, 0.06, 0.07), 0.8)
    hoof = b.mat("Hoof", (0.35, 0.25, 0.25))
    shine = b.mat("Shine", (0.97, 0.97, 0.97))
    b.ico("Body", (0, 0.05, 0.58), (0.46, 0.66, 0.42), body, subdiv=2, jitter=0.02)
    b.ico("Head", (0, -0.62, 0.70), (0.30, 0.28, 0.28), body, subdiv=2, jitter=0.01)
    b.cyl("Snout", (0, -0.90, 0.64), 0.13, 0.12, snout, verts=8, rot=(math.radians(90), 0, 0))
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.box(f"Nostril{s}", (sx * 0.045, -0.965, 0.64), (0.03, 0.02, 0.06), dark)
        b.cone(f"Ear{s}", (sx * 0.19, -0.58, 0.97), 0.10, 0.18, body, verts=3, rot=(math.radians(-25), sx * 0.35, 0))
    b.eyes(0.13, -0.84, 0.78, 0.035, dark, shine)
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        x, y = sx * 0.24, sy * 0.36 + 0.05
        b.cyl(f"Leg{i}", (x, y, 0.20), 0.085, 0.26, body)
        b.cyl(f"Hoof{i}", (x, y, 0.04), 0.09, 0.08, hoof)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.06, minor_radius=0.018, location=(0, 0.72, 0.70), rotation=(0, math.radians(90), 0))
    b._finish(bpy.context.active_object, body, "Tail")
    return b.join_export("Pig")


def build_all():
    for make in (chicken, cow, pig, lambda: cow("Bull", bull=True)):
        clear_scene()
        make()


build_all()
