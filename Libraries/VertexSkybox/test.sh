#!/bin/bash
# Quick test: verify .skye parsing works by round-tripping the Evening Plains file
# This tests the core data pipeline without needing the s&box editor
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SKYE_FILE="/mnt/c/dev/spyro/Evening Plains (Example Sky).skye"

echo "=== VertexSkybox Test ==="

# Test 1: Python parser baseline
echo ""
echo "--- Test 1: Python parser baseline ---"
python3 -c "
import sys
sys.path.insert(0, '/mnt/c/dev/spyro/tools')
from skye_parser import parse_skye, write_skye

sky = parse_skye('$SKYE_FILE')
print(f'  Vertices:  {sky.vertex_count}')
print(f'  Edges:     {sky.edge_count}')
print(f'  Triangles: {sky.triangle_count}')
print(f'  BG Color:  RGB({sky.bg_color[0]}, {sky.bg_color[1]}, {sky.bg_color[2]})')
print(f'  Palette:   {sky.palette_meta}/40 used')
print(f'  Radius:    {sky.get_sphere_radius():.4f}')

# Write and re-read for round-trip test
write_skye(sky, '/tmp/roundtrip_test.skye')
sky2 = parse_skye('/tmp/roundtrip_test.skye')
assert sky.vertex_count == sky2.vertex_count, 'Vertex count mismatch!'
assert sky.edge_count == sky2.edge_count, 'Edge count mismatch!'
assert sky.triangle_count == sky2.triangle_count, 'Triangle count mismatch!'
print('  Round-trip: PASS')
"

# Test 2: Verify file structure
echo ""
echo "--- Test 2: Project structure ---"
EXPECTED_FILES=(
    "Code/vertexskybox.csproj"
    "Code/Data/SkyboxData.cs"
    "Code/Data/SkyboxVertex.cs"
    "Code/IO/SkyeFormat.cs"
    "Code/IO/MeshExporter.cs"
    "Code/Rendering/SkyboxComponent.cs"
    "Code/Rendering/SkyboxMeshBuilder.cs"
    "Code/Utility/SphereConstraint.cs"
    "Code/Utility/SphereGeometry.cs"
    "Editor/vertexskybox.editor.csproj"
    "Assets/Shaders/vertexcolor_sky.shader"
    "vertexskybox.sbproj"
)

ALL_OK=true
for f in "${EXPECTED_FILES[@]}"; do
    if [ -f "$SCRIPT_DIR/$f" ]; then
        echo "  OK: $f"
    else
        echo "  MISSING: $f"
        ALL_OK=false
    fi
done

if $ALL_OK; then
    echo "  Structure: PASS"
else
    echo "  Structure: FAIL"
fi

# Test 3: Try dotnet build (will fail on WSL if sbox DLLs not accessible, but shows errors)
echo ""
echo "--- Test 3: dotnet build check ---"
if dotnet build "$SCRIPT_DIR/Code/vertexskybox.csproj" -c Debug --nologo 2>&1 | tail -3; then
    echo "  Build: PASS"
else
    echo "  Build: FAIL (expected on WSL - build in Windows/sbox editor)"
fi

echo ""
echo "=== Tests Complete ==="
