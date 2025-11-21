#!/bin/bash
#
# Analyzes test coverage for Smalltalk-80 HeapManager
# Scans HeapManager implementation files and test file to identify:
# - Which HeapManager methods are implemented
# - Which methods have tests
# - Coverage percentage by component
# - Untested methods
#

# Parse arguments
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
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
HEAP_MANAGER_DIR="$ROOT_DIR/Smalltalk80.Interpreter/Core/HeapManager"
TESTS_FILE="$ROOT_DIR/Smalltalk80.Interpreter.Tests/HeapManager/SmalltalkHeapManagerTests.cs"

echo -e "${CYAN}===== Smalltalk-80 HeapManager Coverage Analysis =====${NC}"
echo ""

# Find all public methods in HeapManager components
declare -A component_methods
declare -A component_counts
total_methods=0

for file in "$HEAP_MANAGER_DIR"/*.cs; do
    filename=$(basename "$file")

    # Skip utility/data classes
    if [[ "$filename" == *"Interface"* ]] || \
       [[ "$filename" == "ObjectFormat.cs" ]] || \
       [[ "$filename" == "ObjectTableBlock.cs" ]] || \
       [[ "$filename" == "GarbageCollectionResult.cs" ]] || \
       [[ "$filename" == "HeapStatistics.cs" ]]; then
        continue
    fi

    component="${filename%.cs}"

    # Find public methods (excluding constructors and properties)
    methods=$(grep -oP 'public\s+(?:virtual\s+)?(?:override\s+)?\w+(?:<[^>]+>)?\s+\K\w+(?=\s*\()' "$file" | \
              grep -v "^get_" | grep -v "^set_" | grep -v "^$component$" || true)

    if [[ -n "$methods" ]]; then
        count=$(echo "$methods" | wc -l)
        component_methods["$component"]="$methods"
        component_counts["$component"]=$count
        total_methods=$((total_methods + count))
    fi
done

# Find all test methods
declare -a test_methods
total_tests=0

if [[ -f "$TESTS_FILE" ]]; then
    test_methods=($(grep -oP '\[Fact\]\s+public\s+void\s+\K\w+(?=\s*\()' "$TESTS_FILE"))
    total_tests=${#test_methods[@]}
fi

# Analyze coverage by component
echo -e "${YELLOW}Coverage by Component:${NC}"
echo ""

total_implemented=0
total_tested=0

for component in $(echo "${!component_methods[@]}" | tr ' ' '\n' | sort); do
    methods="${component_methods[$component]}"
    implemented="${component_counts[$component]}"

    # Count tested methods
    tested=0
    while IFS= read -r method; do
        if [[ -n "$method" ]]; then
            # Check if any test starts with this method name
            for test in "${test_methods[@]}"; do
                if [[ "$test" =~ ^${method}_ ]]; then
                    tested=$((tested + 1))
                    break
                fi
            done
        fi
    done <<< "$methods"

    total_implemented=$((total_implemented + implemented))
    total_tested=$((total_tested + tested))

    if [[ $implemented -gt 0 ]]; then
        coverage=$(awk "BEGIN {printf \"%.1f\", ($tested / $implemented) * 100}")
    else
        coverage=0.0
    fi

    # Determine color
    if (( $(echo "$coverage >= 90" | bc -l) )); then
        color=$GREEN
        status="✓"
    elif (( $(echo "$coverage >= 70" | bc -l) )); then
        color=$YELLOW
        status="○"
    else
        color=$RED
        status="○"
    fi

    if (( $(echo "$coverage == 100" | bc -l) )); then
        status="✓"
    fi

    echo -e "  ${color}${status}${NC} ${component}: ${CYAN}${tested}${NC}/${CYAN}${implemented}${NC} methods (${color}${coverage}%${NC} coverage)"
done

echo ""
echo -e "${CYAN}===== Summary =====${NC}"
echo ""
echo -e "  Total Methods Implemented: ${CYAN}${total_implemented}${NC}"
echo -e "  Total Methods with Tests: ${CYAN}${total_tested}${NC}"
echo -e "  Total Test Methods: ${CYAN}${total_tests}${NC}"

if [[ $total_implemented -gt 0 ]]; then
    overall_coverage=$(awk "BEGIN {printf \"%.1f\", ($total_tested / $total_implemented) * 100}")
else
    overall_coverage=0.0
fi

# Determine color for overall coverage
if (( $(echo "$overall_coverage >= 90" | bc -l) )); then
    coverage_color=$GREEN
elif (( $(echo "$overall_coverage >= 70" | bc -l) )); then
    coverage_color=$YELLOW
else
    coverage_color=$RED
fi

echo -e "  Overall Method Coverage: ${coverage_color}${overall_coverage}%${NC}"
echo ""

# Show detailed breakdown if requested
if [[ "$DETAILED" == true ]]; then
    echo -e "${CYAN}===== Detailed Breakdown =====${NC}"
    echo ""

    for component in $(echo "${!component_methods[@]}" | tr ' ' '\n' | sort); do
        echo -e "${YELLOW}Component: ${component}${NC}"
        echo "Implemented Methods:"

        methods="${component_methods[$component]}"
        while IFS= read -r method; do
            if [[ -n "$method" ]]; then
                # Check if method has test
                has_test=false
                for test in "${test_methods[@]}"; do
                    if [[ "$test" =~ ^${method}_ ]]; then
                        has_test=true
                        break
                    fi
                done

                if [[ "$has_test" == true ]]; then
                    echo -e "  ${GREEN}✓${NC} $method"
                else
                    echo -e "  ${GRAY}○${NC} $method"
                fi
            fi
        done <<< "$methods"
        echo ""
    done
fi

# Show untested methods if requested
if [[ "$SHOW_UNTESTED" == true ]]; then
    echo -e "${YELLOW}===== Untested Methods =====${NC}"
    echo ""

    has_untested=false

    for component in $(echo "${!component_methods[@]}" | tr ' ' '\n' | sort); do
        methods="${component_methods[$component]}"
        untested=()

        while IFS= read -r method; do
            if [[ -n "$method" ]]; then
                # Check if method has test
                has_test=false
                for test in "${test_methods[@]}"; do
                    if [[ "$test" =~ ^${method}_ ]]; then
                        has_test=true
                        break
                    fi
                done

                if [[ "$has_test" == false ]]; then
                    untested+=("$method")
                fi
            fi
        done <<< "$methods"

        if [[ ${#untested[@]} -gt 0 ]]; then
            has_untested=true
            echo -e "${RED}${component}:${NC}"
            for method in "${untested[@]}"; do
                echo -e "  ${RED}⚠${NC} $method"
            done
            echo ""
        fi
    done

    if [[ "$has_untested" == false ]]; then
        echo -e "${GREEN}All methods have test coverage! ✓${NC}"
        echo ""
    fi
fi

# Show test summary
echo -e "${CYAN}===== Test Summary =====${NC}"
echo ""
echo -e "Test File: ${GRAY}Smalltalk80.Interpreter.Tests/HeapManager/SmalltalkHeapManagerTests.cs${NC}"
echo -e "Total [Fact] Tests: ${CYAN}${total_tests}${NC}"
echo ""
echo -e "${YELLOW}To run HeapManager tests:${NC}"
echo -e "  ${GRAY}dotnet test --filter ${CYAN}\"Category=HeapManager\"${NC}"
echo ""

exit 0
