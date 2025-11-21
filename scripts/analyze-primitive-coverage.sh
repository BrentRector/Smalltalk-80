#!/bin/bash
# Analyzes test coverage for Smalltalk-80 primitives
# Usage: ./scripts/analyze-primitive-coverage.sh [--detailed] [--show-untested]

set -e

DETAILED=false
SHOW_UNTESTED=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --detailed|-d)
            DETAILED=true
            shift
            ;;
        --show-untested|-u)
            SHOW_UNTESTED=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--detailed] [--show-untested]"
            exit 1
            ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
PRIMITIVES_DIR="$ROOT_DIR/Smalltalk80.Interpreter/Core/VM/Primitives"
TESTS_DIR="$ROOT_DIR/Smalltalk80.Interpreter.Tests/Primitives"

echo -e "${CYAN}===== Smalltalk-80 Primitive Coverage Analysis =====${NC}\n"

# Count primitives by category
declare -A primitive_counts
declare -A test_counts
total_primitives=0
total_tests=0

# Scan primitive implementations
for file in "$PRIMITIVES_DIR"/*.cs; do
    filename=$(basename "$file")

    # Skip interface and utility files
    if [[ "$filename" =~ Interface || "$filename" =~ PrimitiveRegistry || "$filename" =~ PlatformBridge ]]; then
        continue
    fi

    category="${filename%Primitives.cs}"

    # Count classes that implement IPrimitive
    count=$(grep -c "public.*class.*:.*IPrimitive" "$file" 2>/dev/null || echo 0)

    if [ "$count" -gt 0 ]; then
        primitive_counts["$category"]=$count
        total_primitives=$((total_primitives + count))
    fi
done

# Scan test files
for file in "$TESTS_DIR"/*Tests.cs; do
    if [ ! -f "$file" ]; then
        continue
    fi

    filename=$(basename "$file")
    category="${filename%PrimitivesTests.cs}"

    # Count test methods (marked with [Fact])
    count=$(grep -c "^\s*\[Fact\]" "$file" 2>/dev/null || echo 0)

    if [ "$count" -gt 0 ]; then
        test_counts["$category"]=$count
        total_tests=$((total_tests + count))
    fi
done

# Display coverage by category
echo -e "${YELLOW}Coverage by Category:${NC}\n"

for category in $(echo "${!primitive_counts[@]}" | tr ' ' '\n' | sort); do
    implemented=${primitive_counts[$category]}
    tested=${test_counts[$category]:-0}

    if [ "$implemented" -gt 0 ]; then
        coverage=$(awk "BEGIN {printf \"%.1f\", ($tested / $implemented) * 100}")
    else
        coverage="0.0"
    fi

    # Determine color based on coverage
    if (( $(echo "$coverage >= 90" | bc -l) )); then
        COLOR=$GREEN
        status="✓"
    elif (( $(echo "$coverage >= 70" | bc -l) )); then
        COLOR=$YELLOW
        status="○"
    else
        COLOR=$RED
        status="○"
    fi

    echo -e "  ${COLOR}${status}${NC} ${category}: ${CYAN}${tested}${NC}/${CYAN}${implemented}${NC} primitives (${COLOR}${coverage}%${NC} coverage)"
done

# Summary
echo -e "\n${CYAN}===== Summary =====${NC}\n"
echo -e "  Total Primitives Implemented: ${CYAN}${total_primitives}${NC}"
echo -e "  Total Test Methods: ${CYAN}${total_tests}${NC}"

if [ "$total_primitives" -gt 0 ]; then
    overall_coverage=$(awk "BEGIN {printf \"%.1f\", ($total_tests / $total_primitives) * 100}")
else
    overall_coverage="0.0"
fi

if (( $(echo "$overall_coverage >= 90" | bc -l) )); then
    COLOR=$GREEN
elif (( $(echo "$overall_coverage >= 70" | bc -l) )); then
    COLOR=$YELLOW
else
    COLOR=$RED
fi

echo -e "  Overall Test Coverage: ${COLOR}${overall_coverage}%${NC}\n"

# Detailed breakdown
if [ "$DETAILED" = true ]; then
    echo -e "${CYAN}===== Detailed Breakdown =====${NC}\n"

    for category in $(echo "${!primitive_counts[@]}" | tr ' ' '\n' | sort); do
        echo -e "${YELLOW}Category: ${category}${NC}"
        echo "Implemented Primitives:"

        file="$PRIMITIVES_DIR/${category}Primitives.cs"
        if [ -f "$file" ]; then
            grep -oP 'public\s+(?:sealed\s+)?class\s+\K\w+(?=\s*:\s*IPrimitive)' "$file" | sort | while read -r prim; do
                echo -e "  ${GRAY}- $prim${NC}"
            done
        fi
        echo ""
    done
fi

# Show untested primitives
if [ "$SHOW_UNTESTED" = true ]; then
    echo -e "${YELLOW}===== Untested Primitives =====${NC}\n"

    has_untested=false

    for category in $(echo "${!primitive_counts[@]}" | tr ' ' '\n' | sort); do
        implemented=${primitive_counts[$category]}
        tested=${test_counts[$category]:-0}

        # Simple heuristic: if we have fewer tests than primitives, show warning
        if [ "$tested" -lt "$implemented" ]; then
            has_untested=true
            echo -e "${RED}${category}:${NC}"
            echo -e "  ${RED}⚠${NC}  Expected ${implemented} primitives, but only ${tested} tests found"
            echo ""
        fi
    done

    if [ "$has_untested" = false ]; then
        echo -e "${GREEN}All primitives have test coverage! ✓${NC}\n"
    fi
fi

exit 0
