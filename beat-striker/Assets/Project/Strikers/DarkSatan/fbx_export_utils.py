"""FBX export without embedding the author's local .blend path."""
import bpy
from io_scene_fbx import export_fbx_bin


def export_fbx(**kwargs):
    original_compound = export_fbx_bin.elem_props_compound

    def compound_without_native_path(element, compound_name, *args, **options):
        setter = original_compound(element, compound_name, *args, **options)

        def set_property(property_type, name, value, *extra, **property_options):
            if compound_name == b'Original' and name == b'ApplicationNativeFile':
                value = ''
            return setter(property_type, name, value, *extra, **property_options)

        return set_property

    export_fbx_bin.elem_props_compound = compound_without_native_path
    try:
        return bpy.ops.export_scene.fbx(**kwargs)
    finally:
        export_fbx_bin.elem_props_compound = original_compound
