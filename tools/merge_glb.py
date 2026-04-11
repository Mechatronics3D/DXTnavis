"""
merge_glb.py - Merge many simple GLB files into a single GLTF/GLB scene.

Optimized for DXTnavis exports where each .glb is typically a single mesh,
single node, and single binary buffer. This avoids the much slower
decode/re-encode path through trimesh when combining tens of thousands of
small mesh files.

Usage:
    pixi run merge-glb <input_dir> <output_path> [--embed]
"""

import base64
import sys
from pathlib import Path


def _pad4(length: int) -> int:
    return (4 - (length % 4)) % 4


def _rebase_attributes(attributes, accessor_offset: int):
    updated = attributes.__class__()
    for key, value in vars(attributes).items():
        if value is not None:
            setattr(updated, key, value + accessor_offset)
    return updated


def merge_glb_files(input_dir: str, output_path: str, embed: bool = False):
    try:
        from pygltflib import (
            GLTF2,
            Accessor,
            Asset,
            Attributes,
            Buffer,
            BufferView,
            Mesh,
            Node,
            Primitive,
            Scene,
        )
    except ImportError:
        print("Error: pygltflib not installed. Run 'pixi install' first.")
        sys.exit(1)

    input_path = Path(input_dir)
    out_path = Path(output_path)

    if not input_path.is_dir():
        print(f"Error: {input_dir} is not a directory")
        sys.exit(1)

    glb_files = sorted(input_path.glob("*.glb"))
    if not glb_files:
        print(f"Error: no .glb files found in {input_dir}")
        sys.exit(1)

    print(f"Found {len(glb_files)} GLB files in {input_dir}")
    print(f"Output: {output_path}")
    print()

    merged_bin = bytearray()
    merged = GLTF2(
        asset=Asset(version="2.0", generator="DXTnavis merge_glb.py"),
        scenes=[Scene(nodes=[])],
        scene=0,
        nodes=[],
        meshes=[],
        accessors=[],
        bufferViews=[],
        buffers=[],
        materials=[],
        images=[],
        textures=[],
        samplers=[],
        skins=[],
        cameras=[],
        animations=[],
    )
    loaded = 0
    skipped = 0

    for i, glb_file in enumerate(glb_files):
        try:
            gltf = GLTF2().load(str(glb_file))
            if len(gltf.buffers or []) != 1:
                skipped += 1
                print(f"  [SKIP] {glb_file.name}: expected 1 buffer, found {len(gltf.buffers or [])}")
                continue

            bin_blob = gltf.binary_blob() or b""
            if not bin_blob:
                skipped += 1
                print(f"  [SKIP] {glb_file.name}: missing binary payload")
                continue

            buffer_offset = len(merged_bin)
            merged_bin.extend(bin_blob)
            merged_bin.extend(b"\x00" * _pad4(len(merged_bin)))

            mesh_offset = len(merged.meshes)
            node_offset = len(merged.nodes)
            accessor_offset = len(merged.accessors)
            bufferview_offset = len(merged.bufferViews)
            material_offset = len(merged.materials)
            image_offset = len(merged.images)
            texture_offset = len(merged.textures)
            sampler_offset = len(merged.samplers)

            for sampler in gltf.samplers or []:
                merged.samplers.append(sampler)

            for image in gltf.images or []:
                if image.bufferView is not None:
                    image.bufferView += bufferview_offset
                merged.images.append(image)

            for texture in gltf.textures or []:
                if texture.sampler is not None:
                    texture.sampler += sampler_offset
                if texture.source is not None:
                    texture.source += image_offset
                merged.textures.append(texture)

            for material in gltf.materials or []:
                pbr = material.pbrMetallicRoughness
                if pbr is not None:
                    if pbr.baseColorTexture is not None and pbr.baseColorTexture.index is not None:
                        pbr.baseColorTexture.index += texture_offset
                    if pbr.metallicRoughnessTexture is not None and pbr.metallicRoughnessTexture.index is not None:
                        pbr.metallicRoughnessTexture.index += texture_offset
                if material.normalTexture is not None and material.normalTexture.index is not None:
                    material.normalTexture.index += texture_offset
                if material.occlusionTexture is not None and material.occlusionTexture.index is not None:
                    material.occlusionTexture.index += texture_offset
                if material.emissiveTexture is not None and material.emissiveTexture.index is not None:
                    material.emissiveTexture.index += texture_offset
                merged.materials.append(material)

            for buffer_view in gltf.bufferViews or []:
                buffer_view.buffer = 0
                buffer_view.byteOffset = (buffer_view.byteOffset or 0) + buffer_offset
                merged.bufferViews.append(buffer_view)

            for accessor in gltf.accessors or []:
                if accessor.bufferView is not None:
                    accessor.bufferView += bufferview_offset
                merged.accessors.append(accessor)

            for mesh in gltf.meshes or []:
                for primitive in mesh.primitives or []:
                    primitive.attributes = _rebase_attributes(primitive.attributes, accessor_offset)
                    if primitive.indices is not None:
                        primitive.indices += accessor_offset
                    if primitive.material is not None:
                        primitive.material += material_offset
                    if primitive.targets:
                        primitive.targets = [
                            _rebase_attributes(target, accessor_offset) for target in primitive.targets
                        ]
                merged.meshes.append(mesh)

            for node in gltf.nodes or []:
                if node.mesh is not None:
                    node.mesh += mesh_offset
                if node.children:
                    node.children = [child + node_offset for child in node.children]
                if not node.name:
                    node.name = glb_file.stem
                merged.nodes.append(node)

            scene_index = gltf.scene if gltf.scene is not None else 0
            if gltf.scenes and scene_index < len(gltf.scenes):
                root_nodes = gltf.scenes[scene_index].nodes or []
            else:
                root_nodes = list(range(len(gltf.nodes or [])))

            if not root_nodes and gltf.nodes:
                root_nodes = [0]

            merged.scenes[0].nodes.extend(node_offset + idx for idx in root_nodes)
            loaded += 1
        except Exception as e:
            skipped += 1
            print(f"  [SKIP] {glb_file.name}: {e}")

        if (i + 1) % 100 == 0 or i == len(glb_files) - 1:
            print(f"  Progress: {i + 1}/{len(glb_files)} ({loaded} loaded, {skipped} skipped)")

    if loaded == 0:
        print("Error: no meshes loaded successfully")
        sys.exit(1)

    print()
    print(f"Merging {loaded} meshes into {out_path.name}...")

    # Export based on extension
    ext = out_path.suffix.lower()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    merged.buffers = [Buffer(byteLength=len(merged_bin))]
    merged.set_binary_blob(bytes(merged_bin))

    if ext == ".glb":
        merged.save_binary(str(out_path))
    elif ext == ".gltf":
        if embed:
            encoded = base64.b64encode(bytes(merged_bin)).decode("ascii")
            merged.buffers[0].uri = f"data:application/octet-stream;base64,{encoded}"
            merged.save_json(str(out_path))
        else:
            bin_path = out_path.with_suffix(".bin")
            merged.buffers[0].uri = bin_path.name
            merged.save_json(str(out_path))
            bin_path.write_bytes(bytes(merged_bin))
    else:
        print(f"Error: unsupported output format '{ext}'. Use .gltf or .glb")
        sys.exit(1)

    file_size = out_path.stat().st_size
    if file_size < 1024 * 1024:
        size_str = f"{file_size / 1024:.1f} KB"
    else:
        size_str = f"{file_size / (1024 * 1024):.1f} MB"

    print(f"Done! {loaded} meshes -> {out_path.name} ({size_str})")


if __name__ == "__main__":
    args = sys.argv[1:]
    embed = False
    if "--embed" in args:
        embed = True
        args.remove("--embed")

    if len(args) < 2:
        print("Usage: python merge_glb.py <input_dir> <output_path> [--embed]")
        print()
        print("  input_dir   Directory containing .glb files")
        print("  output_path Output file path (.gltf or .glb)")
        print("  --embed     For .gltf output, embed binary data in the file")
        print()
        print("Examples:")
        print("  python merge_glb.py ../export/mesh scene.gltf --embed")
        print("  python merge_glb.py C:/exports/mesh C:/output/combined.glb")
        sys.exit(1)

    merge_glb_files(args[0], args[1], embed=embed)
