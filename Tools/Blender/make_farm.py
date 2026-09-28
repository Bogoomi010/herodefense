"""
로우폴리 농장 크립 모델 + 걷기 애니메이션 생성 스크립트 — 원본 창작물 (docs/STORY_ZONES.md 농장 구역).
양(Sheep) · 닭(Chicken) · 소(Cow) · 돼지(Pig) · 성난 황소(Bull, 농장 보스)를 만들어 FBX로 내보낸다.

실행 방법
  MCP:       execute_blender_code 로 exec(open(이 파일).read())
  헤드리스:  blender --background --python Tools/Blender/make_farm.py -- <출력 폴더>

설계
  - 플랫 셰이딩 로우폴리, 단위 m, 발밑이 원점, 정면 -Y. Unity 임포트(Bake Axis Conversion) 뒤엔 머리가 -Z를 보므로
    프리팹을 만들 때 모델을 180° 돌려 +Z 정면으로 맞춘다(MobPrefabBuilder)
  - 몸통 머티리얼 이름은 "Body"/"Wool" — Unity에서 크립 색으로 칠하고 HP에 따라 어두워진다(EnemyView)
  - 걷기: 부품마다 담당 본(Body / Leg* / Head / Tail)을 정해 두고 합친다 → 가중치 칠 없이 부품이 통째로 본을 따른다.
    "Walk" 액션 24프레임(1초) 반복: 다리 앞뒤 흔들기(네발은 대각선 짝), 몸통 위아래 튕김, 머리 끄덕임, 꼬리 흔들기.
  - 씬 초기화는 read_factory_settings 대신 오브젝트·머티리얼·액션만 지운다(MCP 애드온이 꺼지지 않게)
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

WALK_FRAMES = 24  # 1초 (24fps)
HEAD_PARTS = ("Head", "Muzzle", "Ear", "Eye", "Nose", "Nostril", "Horn", "Comb", "Beak", "Wattle", "Snout")


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.actions):
        for d in list(coll):
            coll.remove(d)


def bone_of(part):
    """부품 이름 → 담당 본. Leg0/Hoof0 → Leg0, LegL/ToeL1 → LegL, 머리 부품 → Head, Tail* → Tail, 나머지 → Body"""
    for p in ("Leg", "Hoof", "Toe"):
        if part.startswith(p):
            return "Leg" + part[len(p)]
    if part.startswith(HEAD_PARTS):
        return "Head"
    if part.startswith("Tail"):
        return "Tail"
    return "Body"


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
        vg = o.vertex_groups.new(name=bone_of(name))
        vg.add(range(len(o.data.vertices)), 1.0, 'REPLACE')
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

    def torus(self, name, loc, major, minor, mat, rot):
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot)
        return self._finish(bpy.context.active_object, mat, name)

    def eyes(self, x, y, z, r, dark, shine):
        for sx in (-1, 1):
            s = 'L' if sx < 0 else 'R'
            self.sphere(f"Eye{s}", (sx * x, y, z), r, dark)
            self.sphere(f"EyeShine{s}", (sx * (x + r * 0.3), y - r * 0.8, z + r * 0.3), r * 0.32, shine)

    # ---------- 합치기 → 뼈대 → 걷기 → 내보내기 ----------

    def join_export(self, name, gait):
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

        arm = rig(obj, name)
        walk(arm, gait)

        out = f"{OUT_DIR}/{name}.fbx"
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=out, use_selection=True, object_types={'MESH', 'ARMATURE'},
            # 뼈대가 있어 bake_space_transform은 쓰지 않는다. 축 변환은 Unity가 Bake Axis Conversion으로 하고,
            # 이 경우 axis_forward 값과 상관없이 머리가 -Z로 오므로 앞뒤는 MobPrefabBuilder가 맞춘다
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            mesh_smooth_type='FACE', add_leaf_bones=False, armature_nodetype='NULL',
            bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
            bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
            path_mode='STRIP', embed_textures=False,
        )
        dims = tuple(round(d, 2) for d in obj.dimensions)
        print(f"[make_farm] {name}: verts={len(obj.data.vertices)} bones={len(arm.data.bones)} dims={dims} -> {out}")
        return obj


def group_bounds(obj):
    """본(버텍스 그룹)별 경계 상자: name → (min, max)"""
    names = {vg.index: vg.name for vg in obj.vertex_groups}
    out = {}
    for v in obj.data.vertices:
        for g in v.groups:
            n = names[g.group]
            lo, hi = out.get(n, (v.co.copy(), v.co.copy()))
            out[n] = (Vector(map(min, lo, v.co)), Vector(map(max, hi, v.co)))
    return out


def rig(obj, name):
    """Body(루트) + 다리마다 엉덩이에서 아래로 + 머리(목에서 앞으로) + 꼬리. 본의 X축을 월드 X에 맞춰 앞뒤 흔들기 = X축 회전."""
    b = group_bounds(obj)
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = bpy.context.active_object
    arm.name = f"{name}_Rig"
    eb = arm.data.edit_bones
    for bone in list(eb):
        eb.remove(bone)

    lo, hi = b["Body"]
    c = (lo + hi) / 2
    body = eb.new("Body")
    body.head, body.tail = (0, c.y, c.z), (0, c.y, c.z + 0.3)
    body.align_roll(Vector((0, -1, 0)))

    for n, (lo, hi) in b.items():
        if n.startswith("Leg"):
            e = eb.new(n)
            cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
            e.head, e.tail = (cx, cy, hi.z), (cx, cy, lo.z)
            e.align_roll(Vector((0, 1, 0)))
            e.parent = body
    if "Head" in b:
        lo, hi = b["Head"]
        e = eb.new("Head")
        cz = (lo.z + hi.z) / 2
        e.head, e.tail = (0, hi.y, cz - 0.05), (0, lo.y, cz)
        e.align_roll(Vector((0, 0, -1)))
        e.parent = body
    if "Tail" in b:
        lo, hi = b["Tail"]
        e = eb.new("Tail")
        e.head, e.tail = (0, lo.y, hi.z), (0, hi.y + 0.05, (lo.z + hi.z) / 2)
        e.align_roll(Vector((0, 0, 1)))
        e.parent = body
    bpy.ops.object.mode_set(mode='OBJECT')

    obj.parent = arm
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    mod.use_vertex_groups = True
    return arm


def walk(arm, gait):
    """걷기 한 주기(24프레임). gait: leg(도), bob(m), nod(도), wag(도), biped(두 다리)"""
    scene = bpy.context.scene
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, WALK_FRAMES
    arm.animation_data_create()
    arm.animation_data.action = bpy.data.actions.new("Walk")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    pbs = arm.pose.bones
    for pb in pbs:
        pb.rotation_mode = 'XYZ'
    # 다리 위상: 네발은 대각선 짝(앞왼·뒤오 / 앞오·뒤왼), 두발은 번갈아
    phase = {"Leg0": 0.0, "Leg3": 0.0, "Leg1": math.pi, "Leg2": math.pi, "LegL": 0.0, "LegR": math.pi}
    for f in range(1, WALK_FRAMES + 2, 2):
        t = (f - 1) / WALK_FRAMES * 2 * math.pi
        for pb in pbs:
            n = pb.name
            if n.startswith("Leg"):
                pb.rotation_euler = (math.radians(gait["leg"]) * math.sin(t + phase.get(n, 0.0)), 0, 0)
                pb.keyframe_insert("rotation_euler", frame=f)
            elif n == "Body":
                # 발을 디딜 때마다(한 주기에 두 번) 살짝 튕기고 좌우로 기우뚱
                pb.location = (0, gait["bob"] * abs(math.sin(t)), 0)
                pb.rotation_euler = (0, math.radians(3) * math.sin(t), 0)
                pb.keyframe_insert("location", frame=f)
                pb.keyframe_insert("rotation_euler", frame=f)
            elif n == "Head":
                pb.rotation_euler = (math.radians(gait["nod"]) * math.sin(2 * t), 0, 0)
                pb.keyframe_insert("rotation_euler", frame=f)
            elif n == "Tail":
                pb.rotation_euler = (0, 0, math.radians(gait["wag"]) * math.sin(t))
                pb.keyframe_insert("rotation_euler", frame=f)
    bpy.ops.object.mode_set(mode='OBJECT')


QUAD = {"leg": 26, "bob": 0.035, "nod": 5, "wag": 18}

# x = 좌우, y = 앞뒤(-Y 정면), z = 상하


def sheep():
    b = Builder(20260918)
    wool = b.mat("Wool", (0.93, 0.93, 0.95))
    skin = b.mat("Skin", (0.96, 0.88, 0.83))
    muzzle = b.mat("Muzzle", (0.88, 0.76, 0.72))
    ear = b.mat("EarInner", (0.93, 0.60, 0.60))
    dark = b.mat("Dark", (0.06, 0.06, 0.07), 0.8)
    hoof = b.mat("Hoof", (0.18, 0.18, 0.20))
    b.ico("Body", (0, 0.05, 0.66), (0.52, 0.68, 0.46), wool, subdiv=2, jitter=0.045)
    b.ico("Tail", (0, 0.74, 0.78), (0.11, 0.10, 0.10), wool, subdiv=1, jitter=0.02)
    b.box("Head", (0, -0.64, 0.84), (0.40, 0.40, 0.36), skin, bevel=0.09)
    b.box("Muzzle", (0, -0.84, 0.71), (0.28, 0.14, 0.17), muzzle, bevel=0.035)
    b.ico("HeadWool", (0, -0.58, 1.06), (0.34, 0.33, 0.18), wool, subdiv=1, jitter=0.03)
    b.eyes(0.16, -0.845, 0.90, 0.05, dark, skin)
    b.box("Nose", (0, -0.915, 0.73), (0.06, 0.03, 0.035), dark)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        rot = (0.25, sx * 0.95, sx * -0.25)
        b.ico(f"Ear{s}", (sx * 0.43, -0.60, 0.92), (0.19, 0.06, 0.11), skin, rot=rot)
        b.ico(f"EarIn{s}", (sx * 0.44, -0.635, 0.925), (0.13, 0.025, 0.07), ear, rot=rot)
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        x, y = sx * 0.27, sy * 0.30 + 0.05
        b.cyl(f"Leg{i}", (x, y, 0.28), 0.085, 0.36, skin)
        b.cyl(f"Hoof{i}", (x, y, 0.06), 0.092, 0.12, hoof)
    return b.join_export("Sheep", QUAD)


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
    # 닭: 두 발로 종종, 몸이 더 튀고 머리를 크게 까딱인다
    return b.join_export("Chicken", {"leg": 34, "bob": 0.05, "nod": 14, "wag": 8})


def cow():
    b = Builder(20260929)
    body = b.mat("Body", (0.96, 0.95, 0.92))
    spot = b.mat("Spot", (0.08, 0.07, 0.07))
    muzzle = b.mat("Muzzle", (0.93, 0.70, 0.70))
    horn = b.mat("Horn", (0.93, 0.88, 0.74), 0.6)
    dark = b.mat("Dark", (0.06, 0.06, 0.07), 0.8)
    hoof = b.mat("Hoof", (0.18, 0.18, 0.20))
    shine = b.mat("Shine", (0.95, 0.95, 0.95))
    b.box("Body", (0, 0.05, 1.0), (0.62, 1.25, 0.60), body, bevel=0.12)
    for i, (sx, y, z, sc) in enumerate(((1, 0.25, 1.10, 0.22), (-1, -0.15, 1.00, 0.18), (1, -0.30, 0.90, 0.14), (-1, 0.40, 1.12, 0.16))):
        b.ico(f"Spot{i}", (sx * 0.31, y, z), (0.03, sc, sc * 0.8), spot, subdiv=1)
    b.ico("UdderSpot", (0, 0.25, 0.68), (0.12, 0.10, 0.07), muzzle)
    head_z = 1.18
    b.box("Head", (0, -0.78, head_z), (0.44, 0.46, 0.44), body, bevel=0.08)
    b.box("Muzzle", (0, -1.03, head_z - 0.12), (0.40, 0.14, 0.26), muzzle, bevel=0.05)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.sphere(f"Nostril{s}", (sx * 0.08, -1.10, head_z - 0.10), 0.03, dark)
    b.eyes(0.17, -1.0, head_z + 0.08, 0.045, dark, shine)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.ico(f"Ear{s}", (sx * 0.30, -0.72, head_z + 0.10), (0.14, 0.04, 0.07), body, rot=(0, sx * 0.3, 0))
        b.cone(f"Horn{s}", (sx * 0.18, -0.78, head_z + 0.28), 0.045, 0.16, horn)
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        x, y = sx * 0.21, sy * 0.44 + 0.05
        b.cyl(f"Leg{i}", (x, y, 0.40), 0.10, 0.62, body)
        b.cyl(f"Hoof{i}", (x, y, 0.06), 0.105, 0.12, hoof)
    b.cyl("Tail", (0, 0.68, 0.95), 0.025, 0.55, body, rot=(math.radians(-15), 0, 0))
    b.ico("TailTip", (0, 0.72, 0.66), (0.05, 0.05, 0.09), spot)
    # 소: 무거워서 다리는 덜 흔들고 몸도 덜 튄다
    return b.join_export("Cow", {"leg": 20, "bob": 0.03, "nod": 4, "wag": 22})


def aim(d):
    """방향 벡터 → 원뿔(+Z 축)이 그쪽을 보게 하는 오일러 회전"""
    return Vector(d).normalized().to_track_quat('Z', 'Y').to_euler()


def bull():
    """
    성난 황소 (농장 구역 보스). 일반 크립과 실루엣부터 다르게:
    - 돌진 자세: 어깨 혹이 제일 높고 머리는 그보다 낮게 숙였다. 앞(가슴)이 크고 뒤(엉덩이)가 작은 쐐기 몸
    - 크게 휜 뿔(옆으로 뻗었다가 앞·위로) + 쇠 띠, 찌푸린 눈썹, 빛나는 붉은 눈("Glow"), 금 코뚜레
    - 쇠가시 목줄과 끊어진 사슬 — 우리를 부수고 나온 황소
    """
    b = Builder(20260928)
    body = b.mat("Body", (0.48, 0.20, 0.13))  # Unity에선 보스 색(MobDefs)으로 칠해진다
    mane = b.mat("Mane", (0.13, 0.07, 0.05))
    muzzle = b.mat("Muzzle", (0.62, 0.42, 0.36))
    horn = b.mat("Horn", (0.93, 0.87, 0.70), 0.6)
    iron = b.mat("Iron", (0.30, 0.31, 0.34), 0.45)
    leather = b.mat("Leather", (0.22, 0.13, 0.08))
    gold = b.mat("Ring", (0.90, 0.68, 0.18), 0.35)
    glow = b.mat("Glow", (1.0, 0.12, 0.05))
    dark = b.mat("Dark", (0.06, 0.06, 0.07), 0.8)
    hoof = b.mat("Hoof", (0.12, 0.12, 0.13))

    # 몸: 큰 가슴 + 작은 엉덩이 + 어깨 혹, 혹과 목덜미에 검은 갈기
    b.box("Body", (0, -0.25, 1.08), (0.84, 0.88, 0.80), body, bevel=0.16)
    b.box("BodyHind", (0, 0.42, 1.00), (0.64, 0.72, 0.62), body, bevel=0.13)
    b.ico("Hump", (0, -0.28, 1.50), (0.44, 0.44, 0.30), body, subdiv=1, jitter=0.03)
    b.ico("Mane", (0, -0.42, 1.60), (0.36, 0.36, 0.20), mane, subdiv=1, jitter=0.05)
    b.ico("ManeNeck", (0, -0.62, 1.36), (0.34, 0.26, 0.24), mane, subdiv=1, jitter=0.05)

    # 머리: 어깨보다 낮게 숙이고 앞으로 내민다
    hz = 0.98
    b.box("Head", (0, -0.98, hz), (0.52, 0.50, 0.48), body, bevel=0.09)
    b.ico("HeadTuft", (0, -1.02, hz + 0.26), (0.24, 0.20, 0.10), mane, subdiv=1, jitter=0.03)
    b.box("Muzzle", (0, -1.27, hz - 0.14), (0.46, 0.16, 0.28), muzzle, bevel=0.05)
    b.eyes(0.15, -1.22, hz + 0.05, 0.05, glow, glow)
    for sx in (-1, 1):
        s = 'L' if sx < 0 else 'R'
        b.box(f"HeadBrow{s}", (sx * 0.15, -1.245, hz + 0.12), (0.20, 0.06, 0.06), mane, rot=(0, -sx * 0.4, 0))
        b.sphere(f"Nostril{s}", (sx * 0.09, -1.355, hz - 0.12), 0.035, dark)
        b.ico(f"Ear{s}", (sx * 0.33, -0.86, hz + 0.02), (0.15, 0.05, 0.07), body, rot=(0, sx * 0.5, 0))
        # 뿔: 머리 옆으로 뻗은 뿌리 → 쇠 띠 → 앞·위로 휜 끝
        b.cone(f"Horn{s}0", (sx * 0.42, -0.98, hz + 0.18), 0.10, 0.34, horn, verts=8, rot=(0, sx * math.pi / 2, 0), r2=0.075)
        b.cyl(f"HornBand{s}", (sx * 0.56, -0.98, hz + 0.18), 0.085, 0.07, iron, verts=8, rot=(0, sx * math.pi / 2, 0))
        tip = Vector((sx * 0.35, -0.45, 0.85)).normalized()
        b.cone(f"Horn{s}1", Vector((sx * 0.59, -0.98, hz + 0.18)) + tip * 0.21, 0.075, 0.44, horn, verts=8, rot=aim(tip))
    b.torus("NoseRing", (0, -1.37, hz - 0.28), 0.10, 0.022, gold, (math.radians(90), 0, 0))

    # 쇠가시 목줄(목덜미 쪽으로 뒤로 기울임) + 아래에 끊어진 사슬
    c, tilt = Vector((0, -0.72, 1.08)), math.radians(65)
    up = Vector((0, math.cos(tilt), math.sin(tilt)))  # 목줄 고리 평면의 '위'
    b.torus("Collar", c, 0.43, 0.08, leather, (tilt, 0, 0))
    for i, a in enumerate(range(-100, 101, 40)):
        d = Vector((math.sin(math.radians(a)), 0, 0)) + up * math.cos(math.radians(a))
        b.cone(f"Spike{i}", c + d * 0.53, 0.055, 0.18, iron, verts=5, rot=aim(d))
    low = c - up * 0.47
    for i in range(3):
        b.torus(f"Chain{i}", low - Vector((0, 0.02, 0.08 + i * 0.12)), 0.065, 0.02, iron,
                (math.radians(90), 0, math.radians(90) * (i % 2)))

    # 다리: 앞다리가 더 굵다
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        front = sy < 0
        x, y, r = sx * (0.28 if front else 0.22), -0.42 if front else 0.54, 0.13 if front else 0.11
        b.cyl(f"Leg{i}", (x, y, 0.42), r, 0.68, body)
        b.cyl(f"Hoof{i}", (x, y, 0.06), r + 0.012, 0.12, hoof)
    b.cyl("Tail", (0, 0.82, 0.98), 0.03, 0.58, body, rot=(math.radians(-15), 0, 0))
    b.ico("TailTip", (0, 0.86, 0.68), (0.07, 0.07, 0.11), mane, subdiv=1, jitter=0.02)
    # 황소: 무겁게 쿵쿵, 머리를 크게 흔든다
    return b.join_export("Bull", {"leg": 18, "bob": 0.045, "nod": 10, "wag": 26})


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
    b.torus("Tail", (0, 0.72, 0.70), 0.06, 0.018, body, (0, math.radians(90), 0))
    # 돼지: 짧은 다리로 종종, 꼬리를 빨리 흔든다
    return b.join_export("Pig", {"leg": 30, "bob": 0.04, "nod": 5, "wag": 30})


def build_all():
    for make in (sheep, chicken, cow, pig, bull):
        clear_scene()
        make()


build_all()
