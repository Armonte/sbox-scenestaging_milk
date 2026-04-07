#!/bin/bash
# Build script for VertexSkybox library
# Builds both the runtime (Code) and editor (Editor) assemblies
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SBOX_DIR="/mnt/c/Program Files (x86)/Steam/steamapps/common/sbox"
OUTPUT_DIR="$SBOX_DIR/.vs/output"

echo "=== VertexSkybox Build ==="
echo "Project: $SCRIPT_DIR"
echo "Output:  $OUTPUT_DIR"
echo ""

# Check sbox installation
if [ ! -d "$SBOX_DIR/bin/managed" ]; then
    echo "ERROR: s&box installation not found at $SBOX_DIR"
    echo "Check Steam installation path"
    exit 1
fi

# Build runtime library
echo "--- Building vertexskybox (runtime) ---"
dotnet build "$SCRIPT_DIR/Code/vertexskybox.csproj" -c Debug 2>&1 | tail -5
echo ""

# Build editor library
echo "--- Building vertexskybox.editor (editor) ---"
dotnet build "$SCRIPT_DIR/Editor/vertexskybox.editor.csproj" -c Debug 2>&1 | tail -5
echo ""

# Verify output
echo "--- Build Output ---"
for dll in vertexskybox.dll vertexskybox.editor.dll; do
    if [ -f "$OUTPUT_DIR/$dll" ]; then
        echo "  OK: $dll ($(stat -c%s "$OUTPUT_DIR/$dll") bytes)"
    else
        echo "  MISSING: $dll"
    fi
done

echo ""
echo "=== Build Complete ==="
