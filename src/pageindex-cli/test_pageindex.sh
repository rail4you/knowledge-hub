#!/usr/bin/env bash
# test_pageindex.sh — Verify PageIndex CLI works end-to-end
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CLI="$SCRIPT_DIR/pageindex_cli.py"
PAGEINDEX_DIR="$SCRIPT_DIR/PageIndex"
TEST_DOCX="/tmp/pageindex_test.docx"
TEST_OUTPUT="/tmp/test_docx_index.json"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

pass() { echo -e "${GREEN}[PASS]${NC} $1"; }
fail() { echo -e "${RED}[FAIL]${NC} $1"; }
info() { echo -e "${YELLOW}[INFO]${NC} $1"; }

errors=0

# ── 1. Prerequisite checks ──
info "Checking prerequisites..."

if [ ! -d "$PAGEINDEX_DIR" ]; then
    fail "PageIndex/ directory not found. Run: git clone https://github.com/Vectifyai/PageIndex.git"
    ((errors++))
else
    pass "PageIndex/ directory exists"
fi

if ! command -v python3 &>/dev/null; then
    fail "python3 not found in PATH"
    ((errors++))
else
    pass "python3 found: $(python3 --version)"
fi

for mod in litellm docx; do
    if python3 -c "import $mod" 2>/dev/null; then
        pass "Python module '$mod' available"
    else
        fail "Python module '$mod' not installed"
        ((errors++))
    fi
done

# Get API key: env var or appsettings.json
API_KEY="${QWEN_API_KEY:-}"
if [ -z "$API_KEY" ]; then
    SETTINGS="$SCRIPT_DIR/../../src/KnowledgeHub.HttpApi.Host/appsettings.json"
    if [ -f "$SETTINGS" ]; then
        API_KEY=$(python3 -c "import json; d=json.load(open('$SETTINGS')); print(d.get('Qwen',{}).get('ApiKey',''))" 2>/dev/null || true)
    fi
fi

if [ -z "$API_KEY" ]; then
    fail "No QWEN_API_KEY set and not found in appsettings.json"
    ((errors++))
else
    pass "API key available (length: ${#API_KEY})"
fi

if [ "$errors" -gt 0 ]; then
    fail "Prerequisites failed ($errors errors). Fix them and re-run."
    exit 1
fi

# ── 2. Generate test DOCX ──
info "Generating test DOCX..."
python3 -c "
from docx import Document

doc = Document()
doc.add_heading('PageIndex CLI Test Document', level=1)
doc.add_paragraph('This is a test document for verifying the PageIndex CLI tool integration.')

doc.add_heading('Chapter 1: Introduction', level=2)
doc.add_paragraph('This chapter introduces the basic concepts of document indexing.')
doc.add_paragraph('Document indexing allows structured navigation through large documents.')

doc.add_heading('Chapter 2: Architecture', level=2)
doc.add_paragraph('The architecture consists of multiple layers: domain, application, and infrastructure.')
doc.add_paragraph('Each layer has specific responsibilities and follows DDD principles.')

doc.add_heading('Chapter 3: Implementation', level=2)
doc.add_paragraph('Implementation involves creating entities, services, and background jobs.')
doc.add_paragraph('The PageIndex CLI converts documents into a structured JSON tree.')

doc.save('$TEST_DOCX')
print('Test DOCX saved to $TEST_DOCX')
"

if [ -f "$TEST_DOCX" ]; then
    pass "Test DOCX created ($(stat -f%z "$TEST_DOCX" 2>/dev/null || stat -c%s "$TEST_DOCX") bytes)"
else
    fail "Failed to create test DOCX"
    exit 1
fi

# ── 3. Execute PageIndex CLI ──
info "Running PageIndex CLI..."
cd "$SCRIPT_DIR"

if python3 "$CLI" "$TEST_DOCX" --qwen-api-key "$API_KEY" --qwen-model qwen-long -o "$TEST_OUTPUT"; then
    pass "PageIndex CLI exited with code 0"
else
    exit_code=$?
    fail "PageIndex CLI exited with code $exit_code"
    exit $exit_code
fi

# ── 4. Validate output JSON ──
info "Validating output JSON..."

if [ ! -f "$TEST_OUTPUT" ]; then
    fail "Output file not found: $TEST_OUTPUT"
    exit 1
fi
pass "Output file exists: $TEST_OUTPUT"

python3 -c "
import json, sys

with open('$TEST_OUTPUT', 'r') as f:
    data = json.load(f)

errors = 0

if 'structure' not in data:
    print('[FAIL] Missing top-level \"structure\" key')
    errors += 1
else:
    structure = data['structure']
    if not isinstance(structure, list):
        print('[FAIL] \"structure\" is not a list')
        errors += 1
    elif len(structure) == 0:
        print('[FAIL] \"structure\" is empty (expected at least 1 node)')
        errors += 1
    else:
        print(f'[PASS] structure has {len(structure)} top-level node(s)')

# Check for node titles
def check_nodes(nodes, path='root'):
    for i, node in enumerate(nodes):
        p = f'{path}[{i}]'
        if 'title' in node:
            print(f'[PASS] Node {p}: title=\"{node[\"title\"]}\"')
        else:
            print(f'[WARN] Node {p}: no title')

check_nodes(data.get('structure', []))

if errors > 0:
    sys.exit(1)

print(f'File size: {len(json.dumps(data))} bytes')
"

if [ $? -eq 0 ]; then
    pass "JSON validation passed"
else
    fail "JSON validation failed"
    exit 1
fi

# ── 5. Summary ──
echo ""
echo "========================================"
pass "All tests passed!"
echo "  Output: $TEST_OUTPUT"
echo "========================================"
