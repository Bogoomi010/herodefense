"""
AI 생성(Hitem3D 등) 고해상도 포탑 모델 → 게임용 가벼운 모델 + 구운 색 텍스처 → FBX.

원본은 면 약 200만 개·4K 텍스처라 그대로는 너무 무겁다. 면을 약 1.2%(2만 개대)로 줄이면 원본 UV가 뭉개지므로
새 UV를 펴고 원본 색을 새 텍스처(2048)에 구워(selected-to-active bake) 입힌다.

실행 (MCP): execute_blender_code 로
    SRC, NAME = r"<model.obj 경로>", "TowerBasic"; exec(open(이 파일).read())
결과: Assets/_Project/Art/Models/Towers/<NAME>.fbx, <NAME>_Albedo.png
       → Unity 메뉴 TowerDefense/Towers/Build Tower Prefabs (Resources/Towers/<NAME>.prefab)

규칙: 가장 긴 축을 높이로 세우고, 높이 HEIGHT(11m), 가로 중심·발밑이 원점 (docs/SCALE.md)
"""
import math
import time

import bpy
import numpy as np

OUT_DIR = "D:/Workspace/repo_herodefense/Assets/_Project/Art/Models/Towers"
HEIGHT = 11.0
RATIO = 0.012       # 면 줄이는 비율
TEX = 2048

SRC = globals().get("SRC")
NAME = globals().get("NAME", "TowerBasic")
# 무기(쇠뇌 등)가 몸통과 떨어진 조각이면 "Head"로 떼어 탑 중심에서 돌게 한다. HEAD_Z = 무기 바닥 높이(m), None이면 떼지 않는다.
# 기본 포탑: 흉벽 톱니 위 끝 약 8.0m, 지붕 처마 9.0m → 8.12m에 올려 걸리지 않고 돈다
HEAD_Z = globals().get("HEAD_Z")
HEAD_BACK = 0.4     # 무기 뒤끝을 탑 중심보다 이만큼 뒤에 둔다 (앞으로 길게 뻗어 보이게)
assert SRC, "SRC(원본 obj 경로)를 먼저 정하세요"


def split_head(lo):
    """
    ① 몸통 가운데(높이 30~45% 구간의 가로 중심)를 원점으로 옮긴다 (배너·무기·돌판 때문에 경계 상자 중심이 어긋나 있다)
    ② 떨어진 조각으로 나눠 가장 작은 위쪽 조각을 Head로. 원점 = 탑 중심의 회전축(무기 바닥 높이 HEAD_Z)
    무기가 가리키는 쪽이 탑의 정면이다 (Unity에서 +Z). 반환: [Body, Head]
    """
    me = lo.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mid = (co[:, 2] > 0.30 * HEIGHT) & (co[:, 2] < 0.45 * HEIGHT)
    cx = (co[mid, 0].min() + co[mid, 0].max()) / 2
    cy = (co[mid, 1].min() + co[mid, 1].max()) / 2
    co[:, 0] -= cx
    co[:, 1] -= cy
    me.vertices.foreach_set("co", co.ravel())
    me.update()

    bpy.ops.object.select_all(action='DESELECT')
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')
    parts = list(bpy.context.selected_objects)

    def zc(o):
        return sum(v.co.z for v in o.data.vertices) / len(o.data.vertices)
    body = max(parts, key=lambda o: len(o.data.vertices))
    tops = [o for o in parts if o is not body and zc(o) > 0.6 * HEIGHT]
    head = max(tops, key=lambda o: len(o.data.vertices))
    rest = [o for o in parts if o is not body and o is not head]
    if rest:  # 자잘한 조각은 몸통에 도로 붙인다
        bpy.ops.object.select_all(action='DESELECT')
        for o in rest + [body]:
            o.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()

    # 무기: 가리키는 방향(수평 무게중심 쪽) d를 따라 뒤끝이 중심 뒤 HEAD_BACK에 오게 옮기고, 바닥을 HEAD_Z로
    hm = head.data
    hc = np.empty(len(hm.vertices) * 3)
    hm.vertices.foreach_get("co", hc)
    hc = hc.reshape(-1, 3)
    d = hc[:, :2].mean(0)
    d /= np.linalg.norm(d)
    proj = hc[:, :2] @ d
    shift = -HEAD_BACK - proj.min()
    hc[:, 0] += d[0] * shift
    hc[:, 1] += d[1] * shift
    hc[:, 2] -= hc[:, 2].min()          # 바닥 = 0 (원점 높이 = HEAD_Z)
    hm.vertices.foreach_set("co", hc.ravel())
    hm.update()
    head.location = (0.0, 0.0, HEAD_Z)
    body.name = body.data.name = "Body"
    head.name = head.data.name = "Head"
    print(f"[import_hitem_tower] Head: {len(hm.vertices)} verts, 방향 {np.round(d, 2)}, 회전축 높이 {HEAD_Z}m")
    return [body, head]


def run():
    t = time.time()
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=SRC)
    hi = [o for o in bpy.data.objects if o not in before][0]
    hi.name = f"{NAME}_High"
    # OBJ 임포터가 오브젝트에 축 변환 회전(X 90°)을 붙여 넣으므로, 먼저 정점에 굳혀 로컬 = 월드로 만든다
    bpy.ops.object.select_all(action='DESELECT')
    hi.select_set(True)
    bpy.context.view_layer.objects.active = hi
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # 세우기: 가장 긴 축이 Z(높이)가 되게
    me = hi.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    ext = co.max(0) - co.min(0)
    tall = int(np.argmax(ext))
    if tall == 1:
        co = co[:, [0, 2, 1]] * np.array([1, -1, 1])   # Y가 높이 → X축 90° 회전
    elif tall == 0:
        co = co[:, [2, 1, 0]] * np.array([-1, 1, 1])   # X가 높이 → Y축 -90° 회전
    mn, mx = co.min(0), co.max(0)
    s = HEIGHT / (mx[2] - mn[2])
    co[:, 0] = (co[:, 0] - (mn[0] + mx[0]) / 2) * s
    co[:, 1] = (co[:, 1] - (mn[1] + mx[1]) / 2) * s
    co[:, 2] = (co[:, 2] - mn[2]) * s
    me.vertices.foreach_set("co", co.ravel())
    me.update()

    # 가벼운 사본: 면 줄이기 → 새 UV
    lo = hi.copy()
    lo.data = hi.data.copy()
    lo.name = lo.data.name = NAME
    bpy.context.scene.collection.objects.link(lo)
    bpy.ops.object.select_all(action='DESELECT')
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    mod = lo.modifiers.new("Decimate", 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = RATIO
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    while lo.data.uv_layers:
        lo.data.uv_layers.remove(lo.data.uv_layers[0])
    lo.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')

    # 굽기: 원본 색 → 새 텍스처
    img = bpy.data.images.new(f"{NAME}_Albedo", TEX, TEX, alpha=False)
    mat = bpy.data.materials.new(NAME)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    lo.data.materials.clear()
    lo.data.materials.append(mat)
    nt.nodes.active = tex
    scene = bpy.context.scene
    prev = scene.render.engine
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1
    bk = scene.render.bake
    bk.use_selected_to_active = True
    bk.cage_extrusion = 0.04
    bk.max_ray_distance = 0.15
    bk.margin = 6
    bpy.ops.object.select_all(action='DESELECT')
    hi.select_set(True)
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
    scene.render.engine = prev
    img.filepath_raw = f"{OUT_DIR}/{NAME}_Albedo.png"
    img.file_format = 'PNG'
    img.save()

    objs = [lo]
    if HEAD_Z is not None:
        objs = split_head(lo)

    # 내보내기 (축 변환을 모델에 굽는다)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=f"{OUT_DIR}/{NAME}.fbx", use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        path_mode='STRIP', embed_textures=False,
    )
    hi.hide_set(True)
    hi.hide_render = True
    print(f"[import_hitem_tower] {NAME}: faces {len(hi.data.polygons)} -> {len(lo.data.polygons)}, "
          f"dims={tuple(round(d, 2) for d in lo.dimensions)}, {round(time.time() - t, 1)}s")


run()
