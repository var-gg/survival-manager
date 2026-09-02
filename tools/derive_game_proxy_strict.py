"""Create a strict-manifold game proxy from a textured GLB.

Run with Blender, for example:

    blender --background --python tools/derive_game_proxy_strict.py -- \
        INPUT.glb OUTPUT.glb 12000 2048

The source is only read. Attribute-split positions are welded before a single
bow-tie vertex is repaired, then the mesh is decimated and textures are packed
into the exported GLB.
"""

import hashlib
import json
import math
import re
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


WELD_DISTANCE = 1e-7
DEFAULT_FAN_SEPARATION = 2e-6


def parse_args():
    argv = sys.argv
    args = argv[argv.index("--") + 1 :] if "--" in argv else []
    if len(args) not in {4, 5}:
        raise SystemExit(
            "usage: blender --background --python derive_game_proxy_strict.py -- "
            "INPUT.glb OUTPUT.glb TARGET_TRIANGLES TEXTURE_RESOLUTION "
            "[FAN_SEPARATION]"
        )
    input_path = Path(args[0]).resolve()
    output_path = Path(args[1]).resolve()
    target_triangles = int(args[2])
    texture_resolution = int(args[3])
    fan_separation = float(args[4]) if len(args) == 5 else DEFAULT_FAN_SEPARATION
    return input_path, output_path, target_triangles, texture_resolution, fan_separation


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_name(value):
    return re.sub(r"[^A-Za-z0-9._-]+", "-", value).strip("-")


def triangle_count(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def face_fans(vertex):
    """Return face-connected fans around a BMVert.

    Faces are adjacent only when they meet across an edge incident to the
    vertex. A strict closed two-manifold vertex has exactly one such fan.
    """

    remaining = set(vertex.link_faces)
    fans = []
    while remaining:
        seed = remaining.pop()
        fan = {seed}
        stack = [seed]
        while stack:
            face = stack.pop()
            for edge in face.edges:
                if vertex not in edge.verts:
                    continue
                for adjacent in edge.link_faces:
                    if adjacent in remaining:
                        remaining.remove(adjacent)
                        fan.add(adjacent)
                        stack.append(adjacent)
        fans.append(fan)
    return fans


def topology_stats(bm):
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    boundary_edges = 0
    loose_edges = 0
    multi_face_edges = 0
    manifold_edges = 0
    for edge in bm.edges:
        linked_faces = len(edge.link_faces)
        if linked_faces == 0:
            loose_edges += 1
        elif linked_faces == 1:
            boundary_edges += 1
        elif linked_faces == 2:
            manifold_edges += 1
        else:
            multi_face_edges += 1

    strict_non_manifold = []
    for vertex in bm.verts:
        fans = face_fans(vertex)
        incident_face_counts = [len(edge.link_faces) for edge in vertex.link_edges]
        if (
            not vertex.link_faces
            or len(fans) != 1
            or any(count != 2 for count in incident_face_counts)
        ):
            strict_non_manifold.append(
                {
                    "index": vertex.index,
                    "position": [float(value) for value in vertex.co],
                    "face_count": len(vertex.link_faces),
                    "fan_face_counts": sorted(len(fan) for fan in fans),
                    "incident_edge_face_counts": sorted(incident_face_counts),
                }
            )

    parent = list(range(len(bm.verts)))
    sizes = [1] * len(bm.verts)

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(left, right):
        left_root = find(left)
        right_root = find(right)
        if left_root == right_root:
            return
        if sizes[left_root] < sizes[right_root]:
            left_root, right_root = right_root, left_root
        parent[right_root] = left_root
        sizes[left_root] += sizes[right_root]

    for edge in bm.edges:
        union(edge.verts[0].index, edge.verts[1].index)

    component_sizes = {}
    for vertex in bm.verts:
        root = find(vertex.index)
        component_sizes[root] = component_sizes.get(root, 0) + 1

    return {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "components": len(component_sizes),
        "component_vertex_counts": sorted(component_sizes.values(), reverse=True),
        "boundary_edges": boundary_edges,
        "loose_edges": loose_edges,
        "manifold_edges": manifold_edges,
        "multi_face_edges": multi_face_edges,
        "strict_non_manifold_vertex_count": len(strict_non_manifold),
        "strict_non_manifold_vertices": strict_non_manifold,
    }


def neighbor_centroid(vertex):
    neighbors = [edge.other_vert(vertex).co.copy() for edge in vertex.link_edges]
    if not neighbors:
        return vertex.co.copy()
    return sum(neighbors, Vector()) / len(neighbors)


def fan_average_normal(vertex):
    if not vertex.link_faces:
        return Vector()
    normal = sum((face.normal for face in vertex.link_faces), Vector())
    return normal.normalized() if normal.length > 1e-12 else Vector()


def split_single_bow_tie(bm, fan_separation):
    before = topology_stats(bm)
    candidates = before["strict_non_manifold_vertices"]
    if before["boundary_edges"] != 0 or before["loose_edges"] != 0:
        raise RuntimeError(
            "Source weld has boundary or loose edges; the bounded bow-tie repair is unsafe"
        )
    if before["multi_face_edges"] != 0:
        raise RuntimeError(
            "Source weld has edges with more than two faces; the bounded bow-tie repair is unsafe"
        )
    if len(candidates) != 1:
        raise RuntimeError(
            f"Expected exactly one strict non-manifold vertex, found {len(candidates)}"
        )

    candidate = candidates[0]
    vertex = bm.verts[candidate["index"]]
    fans = face_fans(vertex)
    if len(fans) != 2 or any(len(edge.link_faces) != 2 for edge in vertex.link_edges):
        raise RuntimeError(
            "The strict non-manifold vertex is not exactly two closed face fans"
        )

    original_position = vertex.co.copy()
    # One edge is enough to make BMesh detach the disconnected radial fan.
    # Passing every edge from a fan would rip that fan into separate faces.
    chosen_edge = next(
        edge
        for edge in vertex.link_edges
        if len(edge.link_faces) == 2
        and all(face in fans[0] for face in edge.link_faces)
    )
    separated = list(bmesh.utils.vert_separate(vertex, [chosen_edge]))
    if len(separated) != 2:
        raise RuntimeError(
            f"Bow-tie split produced {len(separated)} vertices instead of two"
        )

    bm.normal_update()
    centroid_delta = neighbor_centroid(separated[1]) - neighbor_centroid(separated[0])
    if centroid_delta.length > 1e-12:
        direction = centroid_delta.normalized()
        direction_source = "neighbor-centroid-delta"
    else:
        normal_delta = fan_average_normal(separated[1]) - fan_average_normal(separated[0])
        if normal_delta.length > 1e-12:
            direction = normal_delta.normalized()
            direction_source = "fan-normal-delta"
        else:
            direction = Vector((1.0, 0.0, 0.0))
            direction_source = "fixed-x-fallback"

    half_separation = fan_separation * 0.5
    separated[0].co = original_position - direction * half_separation
    separated[1].co = original_position + direction * half_separation
    bm.normal_update()
    bm.verts.index_update()
    bm.edges.index_update()
    bm.faces.index_update()

    after = topology_stats(bm)
    if (
        after["boundary_edges"] != 0
        or after["loose_edges"] != 0
        or after["multi_face_edges"] != 0
        or after["strict_non_manifold_vertex_count"] != 0
    ):
        raise RuntimeError("Bow-tie repair did not produce a strict closed two-manifold")

    return {
        "method": "split-disconnected-closed-face-fans-and-symmetric-splay",
        "source_vertex_index": candidate["index"],
        "source_position": [float(value) for value in original_position],
        "source_fan_face_counts": sorted(len(fan) for fan in fans),
        "separation": fan_separation,
        "direction": [float(value) for value in direction],
        "direction_source": direction_source,
        "result_positions": [
            [float(value) for value in separated_vertex.co]
            for separated_vertex in separated
        ],
        "topology_before": before,
        "topology_after": after,
    }


def strict_stats_for_mesh(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    stats = topology_stats(bm)
    bm.free()
    return stats


def main():
    input_path, output_path, target_triangles, texture_resolution, fan_separation = parse_args()

    if not input_path.is_file():
        raise FileNotFoundError(input_path)
    if output_path.exists():
        raise FileExistsError(f"Refusing to overwrite existing output: {output_path}")
    if output_path.suffix.lower() != ".glb":
        raise ValueError("OUTPUT must use the .glb extension")
    if target_triangles <= 0:
        raise ValueError("TARGET_TRIANGLES must be positive")
    if texture_resolution <= 0:
        raise ValueError("TEXTURE_RESOLUTION must be positive")
    if not math.isfinite(fan_separation) or fan_separation <= WELD_DISTANCE * 2:
        raise ValueError(
            f"FAN_SEPARATION must be finite and greater than {WELD_DISTANCE * 2:g}"
        )

    source_hash_before = sha256(input_path)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(input_path))
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(mesh_objects) != 1:
        raise RuntimeError(f"Expected one mesh object, found {len(mesh_objects)}")

    obj = mesh_objects[0]
    mesh = obj.data
    source_triangles = triangle_count(mesh)
    source_uv_layers = [layer.name for layer in mesh.uv_layers]
    if not source_uv_layers:
        raise RuntimeError("Source mesh has no UV layer")
    if target_triangles >= source_triangles:
        raise ValueError("TARGET_TRIANGLES must be lower than the source triangle count")

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=WELD_DISTANCE)
    bm.normal_update()
    bm.verts.index_update()
    bm.edges.index_update()
    bm.faces.index_update()
    repair = split_single_bow_tie(bm, fan_separation)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    if [layer.name for layer in mesh.uv_layers] != source_uv_layers:
        raise RuntimeError("UV layer inventory changed during weld or bow-tie repair")

    pre_decimate_triangles = triangle_count(mesh)
    ratio = target_triangles / pre_decimate_triangles
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new(name="StrictGameProxyDecimate", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    result_triangles = triangle_count(mesh)
    if result_triangles != target_triangles:
        raise RuntimeError(
            f"Decimation produced {result_triangles} triangles; expected {target_triangles}"
        )
    if not mesh.uv_layers:
        raise RuntimeError("UV data was lost during decimation")

    post_decimate_topology = strict_stats_for_mesh(mesh)
    if (
        post_decimate_topology["boundary_edges"] != 0
        or post_decimate_topology["loose_edges"] != 0
        or post_decimate_topology["multi_face_edges"] != 0
        or post_decimate_topology["strict_non_manifold_vertex_count"] != 0
    ):
        raise RuntimeError(
            "Decimation regressed strict manifold topology: "
            + json.dumps(post_decimate_topology, separators=(",", ":"))
        )

    output_path.parent.mkdir(parents=True, exist_ok=False)
    texture_outputs = []
    image_reports = []
    for image in list(bpy.data.images):
        if image.name in {"Render Result", "Viewer Node"}:
            continue
        try:
            _ = image.pixels[0]
        except (IndexError, RuntimeError):
            continue
        source_size = [int(image.size[0]), int(image.size[1])]
        image.scale(texture_resolution, texture_resolution)
        texture_path = output_path.parent / (
            f"{safe_name(Path(image.name).stem)}-{texture_resolution}.jpg"
        )
        image.filepath_raw = str(texture_path)
        image.file_format = "JPEG"
        image.save()
        encoded = texture_path.read_bytes()
        image.pack(data=encoded, data_len=len(encoded))
        texture_outputs.append(str(texture_path))
        image_reports.append(
            {
                "name": image.name,
                "source_size": source_size,
                "result_size": [int(image.size[0]), int(image.size[1])],
                "external_path": str(texture_path),
                "external_sha256": sha256(texture_path),
            }
        )

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_animations=False,
        export_image_format="AUTO",
        export_tangents=True,
    )

    source_hash_after = sha256(input_path)
    if source_hash_after != source_hash_before:
        raise RuntimeError("Source GLB hash changed during derivation")

    manifest = {
        "schema": "p09.ai3d_local_derivation.strict-manifold.v1",
        "source": {
            "path": str(input_path),
            "sha256_before": source_hash_before,
            "sha256_after": source_hash_after,
        },
        "output": {
            "path": str(output_path),
            "bytes": output_path.stat().st_size,
            "sha256": sha256(output_path),
        },
        "operation": "weld-repair-bow-tie-decimate-and-downscale-textures",
        "source_triangles": source_triangles,
        "pre_decimate_triangles": pre_decimate_triangles,
        "requested_target_triangles": target_triangles,
        "result_triangles": result_triangles,
        "decimate_ratio": ratio,
        "weld_distance": WELD_DISTANCE,
        "repair": repair,
        "post_decimate_topology": post_decimate_topology,
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "texture_resolution": texture_resolution,
        "textures": image_reports,
        "texture_outputs": texture_outputs,
        "export_tangents": True,
        "notes": [
            "The provider GLB was only read and its SHA-256 stayed unchanged.",
            "Only the two disconnected closed face fans at the inherited bow-tie were split.",
            "The two new fan vertices were splayed symmetrically before decimation.",
            "Per-loop UV data was retained across the position weld and repair.",
            "This remains an automated decimation proxy, not handcrafted retopology.",
        ],
    }
    manifest_path = output_path.with_suffix(".derivation.json")
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    print("DERIVATION_MANIFEST=" + str(manifest_path))
    print("DERIVATION_SUMMARY=" + json.dumps(manifest, separators=(",", ":")))


if __name__ == "__main__":
    main()
