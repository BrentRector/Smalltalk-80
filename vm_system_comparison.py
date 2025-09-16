#!/usr/bin/env python3
"""
VM System Comparison Tool for Smalltalk-80 Implementation

This tool provides comprehensive analysis and comparison between the VM bootstrap classes 
and the complete system image definitions. It helps validate the bootstrap process and 
identify discrepancies between the intended system and the actual VM implementation.

FEATURES:
- Parses bootstrap log files to extract VM state and loading information
- Compares bootstrap classes with system image classes
- Provides detailed mismatch analysis (missing methods, different bytecode, etc.)
- Generates comprehensive reports in both Markdown and JSON formats
- Includes statistics and summary information

ANALYSIS SCOPE:
1. Bootstrap log files (core_vm_dump.txt and vm_memory_dump.txt) for VM state information
2. Bootstrap JSON files (bootstrap.json) for actual VM class definitions  
3. System image JSON file (complete_system_final.json) as the reference standard

OUTPUT:
- Detailed Markdown report with human-readable analysis
- JSON export for programmatic analysis and integration
- Console summary with key statistics

USAGE:
    python vm_system_comparison.py                    # Generate both reports
    python vm_system_comparison.py --json-only       # JSON export only
    python vm_system_comparison.py --report-only     # Markdown report only
    python vm_system_comparison.py --verbose         # Detailed logging

The script is designed to be robust and handle parsing errors gracefully while providing
detailed diagnostic information about the Smalltalk-80 bootstrap process.
"""

import json
import re
import logging
from typing import Dict, List, Tuple, Optional, Set, Any
from dataclasses import dataclass, field
from pathlib import Path
import argparse

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

@dataclass
class MethodInfo:
    """Represents a compiled method with all its attributes"""
    selector: str
    bytecode: List[int] = field(default_factory=list)
    literals: List[Any] = field(default_factory=list)
    argument_count: int = 0
    temporary_count: int = 0
    primitive_index: int = 0
    source: str = ""
    
    def __hash__(self):
        # For set operations
        return hash((self.selector, tuple(self.bytecode), self.argument_count, self.temporary_count, self.primitive_index))
    
    def matches(self, other: 'MethodInfo') -> bool:
        """Check if this method exactly matches another method"""
        return (
            self.selector == other.selector and
            self.bytecode == other.bytecode and
            self.argument_count == other.argument_count and
            self.temporary_count == other.temporary_count and
            self.primitive_index == other.primitive_index
        )

@dataclass
class ClassInfo:
    """Represents a Smalltalk class with all its attributes"""
    name: str
    superclass: str = "nil"
    instance_variables: List[str] = field(default_factory=list)
    class_variables: List[str] = field(default_factory=list)
    pool_dictionaries: List[str] = field(default_factory=list)
    category: str = ""
    methods: Dict[str, MethodInfo] = field(default_factory=dict)
    class_methods: Dict[str, MethodInfo] = field(default_factory=dict)
    
    def method_count(self) -> int:
        """Total number of methods (instance + class)"""
        return len(self.methods) + len(self.class_methods)
    
    def matches_structure(self, other: 'ClassInfo') -> bool:
        """Check if class structure matches (not including methods)"""
        return (
            self.name == other.name and
            self.superclass == other.superclass and
            set(self.instance_variables) == set(other.instance_variables) and
            set(self.class_variables) == set(other.class_variables)
        )

class VMSystemComparator:
    """Main comparison engine"""
    
    def __init__(self, base_path: Path):
        self.base_path = base_path
        self.bootstrap_classes: Dict[str, ClassInfo] = {}
        self.system_classes: Dict[str, ClassInfo] = {}
        self.bootstrap_log_info: Dict[str, Any] = {}
        self.comparison_results: Dict[str, Any] = {}
        
    def parse_bootstrap_logs(self, core_dump_path: Path, vm_dump_path: Path) -> Dict[str, Any]:
        """Parse the bootstrap log files to extract VM state information"""
        logger.info("Parsing bootstrap log files...")
        
        log_info = {
            "bootstrap_phases": [],
            "created_classes": [],
            "failed_classes": [],
            "installed_methods": 0,
            "failed_methods": 0,
            "object_counts": {},
            "errors": []
        }
        
        # Pattern to match various log entries
        patterns = {
            "phase": re.compile(r'\[.*?\] (Phase \d+): (.+)'),
            "class_creation": re.compile(r'\[LOWLEVEL\] Creating class (\w+) < (\w+)'),
            "class_success": re.compile(r'\[LOWLEVEL\] ✓ Successfully created class (\w+)'),
            "class_failure": re.compile(r'\[LOWLEVEL\] \? Failed to create class (\w+): (.+)'),
            "method_install": re.compile(r'\[LOWLEVEL\] Installing (\d+) methods for (\w+)'),
            "method_failure": re.compile(r'\[LOWLEVEL\] \? Failed to install method (.+): (.+)'),
            "object_count": re.compile(r'with (\d+) objects'),
            "checkpoint": re.compile(r'Saved checkpoint for phase (\w+) with (\d+) objects')
        }
        
        for dump_file in [core_dump_path, vm_dump_path]:
            if not dump_file.exists():
                logger.warning(f"Log file not found: {dump_file}")
                continue
                
            try:
                # Try different encodings
                encodings = ['utf-8', 'utf-16', 'windows-1252', 'latin1']
                content = None
                
                for encoding in encodings:
                    try:
                        with open(dump_file, 'r', encoding=encoding) as f:
                            content = f.readlines()
                        break
                    except UnicodeDecodeError:
                        continue
                
                if content is None:
                    logger.warning(f"Could not decode {dump_file} with any encoding")
                    continue
                
                for line_num, line in enumerate(content, 1):
                    line = line.strip()
                    
                    # Check each pattern
                    for pattern_name, pattern in patterns.items():
                        match = pattern.search(line)
                        if match:
                                if pattern_name == "phase":
                                    log_info["bootstrap_phases"].append({
                                        "phase": match.group(1),
                                        "description": match.group(2),
                                        "line": line_num
                                    })
                                elif pattern_name == "class_creation":
                                    log_info["created_classes"].append({
                                        "name": match.group(1),
                                        "superclass": match.group(2),
                                        "line": line_num
                                    })
                                elif pattern_name == "class_failure":
                                    log_info["failed_classes"].append({
                                        "name": match.group(1),
                                        "reason": match.group(2),
                                        "line": line_num
                                    })
                                elif pattern_name == "method_install":
                                    count = int(match.group(1))
                                    class_name = match.group(2)
                                    log_info["installed_methods"] += count
                                elif pattern_name == "method_failure":
                                    log_info["failed_methods"] += 1
                                elif pattern_name == "checkpoint":
                                    phase = match.group(1)
                                    count = int(match.group(2))
                                    log_info["object_counts"][phase] = count
                                
            except Exception as e:
                error_msg = f"Error parsing {dump_file}: {str(e)}"
                logger.error(error_msg)
                log_info["errors"].append(error_msg)
        
        self.bootstrap_log_info = log_info
        logger.info(f"Parsed logs: {len(log_info['bootstrap_phases'])} phases, "
                   f"{len(log_info['created_classes'])} class attempts, "
                   f"{len(log_info['failed_classes'])} failures")
        
        return log_info
    
    def parse_bootstrap_json(self, bootstrap_path: Path) -> Dict[str, ClassInfo]:
        """Parse the bootstrap JSON file to get actual VM class definitions"""
        logger.info(f"Parsing bootstrap JSON: {bootstrap_path}")
        
        bootstrap_classes = {}
        
        try:
            with open(bootstrap_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            classes = data.get('classes', [])
            logger.info(f"Found {len(classes)} classes in bootstrap")
            
            for class_data in classes:
                class_info = self._parse_class_data(class_data, is_bootstrap=True)
                bootstrap_classes[class_info.name] = class_info
                
        except Exception as e:
            logger.error(f"Error parsing bootstrap JSON: {str(e)}")
            raise
        
        self.bootstrap_classes = bootstrap_classes
        return bootstrap_classes
    
    def parse_system_json(self, system_path: Path) -> Dict[str, ClassInfo]:
        """Parse the complete system JSON file"""
        logger.info(f"Parsing system JSON: {system_path}")
        
        system_classes = {}
        
        try:
            with open(system_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # Handle both formats: classes array or classes+methods arrays
            if 'classes' in data:
                classes = data['classes']
                methods = data.get('methods', [])
                
                # First, create all classes
                for class_data in classes:
                    class_info = self._parse_class_data(class_data, is_bootstrap=False)
                    system_classes[class_info.name] = class_info
                
                # Then add methods if they're in separate array
                if methods:
                    for method_data in methods:
                        class_name = method_data.get('class', '')
                        is_class_method = class_name.endswith(' class')
                        
                        # Remove ' class' suffix for class methods
                        if is_class_method:
                            class_name = class_name[:-6]
                        
                        if class_name in system_classes:
                            method_info = self._parse_method_data(method_data)
                            
                            if is_class_method:
                                system_classes[class_name].class_methods[method_info.selector] = method_info
                            else:
                                system_classes[class_name].methods[method_info.selector] = method_info
            
            logger.info(f"Found {len(system_classes)} classes in system image")
            
        except Exception as e:
            logger.error(f"Error parsing system JSON: {str(e)}")
            raise
        
        self.system_classes = system_classes
        return system_classes
    
    def _parse_class_data(self, class_data: Dict, is_bootstrap: bool = False) -> ClassInfo:
        """Parse a single class from JSON data"""
        class_info = ClassInfo(
            name=class_data.get('name', ''),
            superclass=class_data.get('superclass', 'nil'),
            instance_variables=class_data.get('instanceVariables', []),
            class_variables=class_data.get('classVariables', []),
            pool_dictionaries=class_data.get('poolDictionaries', []),
            category=class_data.get('category', '')
        )
        
        # Parse methods
        methods = class_data.get('methods', [])
        for method_data in methods:
            method_info = self._parse_method_data(method_data)
            class_info.methods[method_info.selector] = method_info
        
        # Parse class methods
        class_methods = class_data.get('classMethods', [])
        for method_data in class_methods:
            method_info = self._parse_method_data(method_data)
            class_info.class_methods[method_info.selector] = method_info
        
        return class_info
    
    def _parse_method_data(self, method_data: Dict) -> MethodInfo:
        """Parse a single method from JSON data"""
        # Handle bytecode - ensure it's a list of integers
        bytecode = method_data.get('bytecode', [])
        if isinstance(bytecode, str):
            # Handle hex string format like "40 88 68 7C"
            try:
                bytecode = [int(x, 16) for x in bytecode.split()]
            except ValueError:
                # Handle decimal string format
                try:
                    bytecode = [int(x) for x in bytecode.split()]
                except ValueError:
                    logger.warning(f"Could not parse bytecode: {bytecode}")
                    bytecode = []
        elif not isinstance(bytecode, list):
            bytecode = [bytecode] if bytecode is not None else []
        
        # Ensure all bytecode elements are integers
        try:
            bytecode = [int(b) for b in bytecode]
        except (ValueError, TypeError):
            logger.warning(f"Invalid bytecode format: {method_data.get('bytecode', [])}")
            bytecode = []
        
        return MethodInfo(
            selector=method_data.get('selector', ''),
            bytecode=bytecode,
            literals=method_data.get('literals', []),
            argument_count=method_data.get('numArgs', method_data.get('argumentCount', 0)),
            temporary_count=method_data.get('numTemps', method_data.get('temporaryCount', 0)),
            primitive_index=method_data.get('primitiveIndex', method_data.get('primitive', 0)),
            source=method_data.get('source', '')
        )
    
    def compare_classes(self) -> Dict[str, Any]:
        """Compare bootstrap classes against system classes"""
        logger.info("Comparing classes...")
        
        bootstrap_names = set(self.bootstrap_classes.keys())
        system_names = set(self.system_classes.keys())
        
        results = {
            "perfect_matches": [],
            "structural_matches": [],  # Same structure, different methods
            "mismatches": [],
            "bootstrap_only": list(bootstrap_names - system_names),
            "system_only": list(system_names - bootstrap_names),
            "statistics": {
                "bootstrap_classes": len(bootstrap_names),
                "system_classes": len(system_names),
                "common_classes": len(bootstrap_names & system_names),
                "perfect_match_count": 0,
                "structural_match_count": 0,
                "mismatch_count": 0
            }
        }
        
        # Compare common classes
        common_classes = bootstrap_names & system_names
        
        for class_name in sorted(common_classes):
            bootstrap_class = self.bootstrap_classes[class_name]
            system_class = self.system_classes[class_name]
            
            comparison = self._compare_single_class(bootstrap_class, system_class)
            
            if comparison["is_perfect_match"]:
                results["perfect_matches"].append(comparison)
                results["statistics"]["perfect_match_count"] += 1
            elif comparison["structure_matches"]:
                results["structural_matches"].append(comparison)
                results["statistics"]["structural_match_count"] += 1
            else:
                results["mismatches"].append(comparison)
                results["statistics"]["mismatch_count"] += 1
        
        self.comparison_results = results
        return results
    
    def _compare_single_class(self, bootstrap_class: ClassInfo, system_class: ClassInfo) -> Dict[str, Any]:
        """Compare a single class between bootstrap and system"""
        comparison = {
            "class_name": bootstrap_class.name,
            "structure_matches": bootstrap_class.matches_structure(system_class),
            "is_perfect_match": False,
            "differences": [],
            "method_comparison": {},
            "statistics": {
                "bootstrap_methods": bootstrap_class.method_count(),
                "system_methods": system_class.method_count(),
                "matching_methods": 0,
                "different_bytecode": 0,
                "bootstrap_only_methods": 0,
                "system_only_methods": 0
            }
        }
        
        # Check structural differences
        if bootstrap_class.superclass != system_class.superclass:
            comparison["differences"].append(
                f"Superclass: bootstrap='{bootstrap_class.superclass}' vs system='{system_class.superclass}'"
            )
        
        if set(bootstrap_class.instance_variables) != set(system_class.instance_variables):
            comparison["differences"].append(
                f"Instance variables: bootstrap={bootstrap_class.instance_variables} vs system={system_class.instance_variables}"
            )
        
        if set(bootstrap_class.class_variables) != set(system_class.class_variables):
            comparison["differences"].append(
                f"Class variables: bootstrap={bootstrap_class.class_variables} vs system={system_class.class_variables}"
            )
        
        # Compare methods
        method_comparison = self._compare_methods(
            bootstrap_class.methods, system_class.methods, "instance"
        )
        class_method_comparison = self._compare_methods(
            bootstrap_class.class_methods, system_class.class_methods, "class"
        )
        
        comparison["method_comparison"]["instance"] = method_comparison
        comparison["method_comparison"]["class"] = class_method_comparison
        
        # Update statistics
        comparison["statistics"]["matching_methods"] = (
            method_comparison["matching_count"] + class_method_comparison["matching_count"]
        )
        comparison["statistics"]["different_bytecode"] = (
            method_comparison["different_bytecode"] + class_method_comparison["different_bytecode"]
        )
        comparison["statistics"]["bootstrap_only_methods"] = (
            method_comparison["bootstrap_only"] + class_method_comparison["bootstrap_only"]
        )
        comparison["statistics"]["system_only_methods"] = (
            method_comparison["system_only"] + class_method_comparison["system_only"]
        )
        
        # Check if it's a perfect match
        comparison["is_perfect_match"] = (
            comparison["structure_matches"] and
            len(comparison["differences"]) == 0 and
            method_comparison["perfect_match"] and
            class_method_comparison["perfect_match"]
        )
        
        return comparison
    
    def _compare_methods(self, bootstrap_methods: Dict[str, MethodInfo], 
                        system_methods: Dict[str, MethodInfo], 
                        method_type: str) -> Dict[str, Any]:
        """Compare two sets of methods"""
        bootstrap_selectors = set(bootstrap_methods.keys())
        system_selectors = set(system_methods.keys())
        common_selectors = bootstrap_selectors & system_selectors
        
        result = {
            "method_type": method_type,
            "bootstrap_count": len(bootstrap_selectors),
            "system_count": len(system_selectors),
            "common_count": len(common_selectors),
            "bootstrap_only": len(bootstrap_selectors - system_selectors),
            "system_only": len(system_selectors - system_selectors),
            "matching_count": 0,
            "different_bytecode": 0,
            "perfect_match": False,
            "method_details": {},
            "bootstrap_only_methods": list(bootstrap_selectors - system_selectors),
            "system_only_methods": list(system_selectors - bootstrap_selectors)
        }
        
        matching_methods = 0
        different_bytecode = 0
        
        for selector in common_selectors:
            bootstrap_method = bootstrap_methods[selector]
            system_method = system_methods[selector]
            
            if bootstrap_method.matches(system_method):
                matching_methods += 1
                result["method_details"][selector] = "exact_match"
            else:
                different_bytecode += 1
                differences = []
                
                if bootstrap_method.bytecode != system_method.bytecode:
                    differences.append(f"bytecode: {bootstrap_method.bytecode} vs {system_method.bytecode}")
                if bootstrap_method.argument_count != system_method.argument_count:
                    differences.append(f"argument_count: {bootstrap_method.argument_count} vs {system_method.argument_count}")
                if bootstrap_method.temporary_count != system_method.temporary_count:
                    differences.append(f"temporary_count: {bootstrap_method.temporary_count} vs {system_method.temporary_count}")
                if bootstrap_method.primitive_index != system_method.primitive_index:
                    differences.append(f"primitive_index: {bootstrap_method.primitive_index} vs {system_method.primitive_index}")
                
                result["method_details"][selector] = differences
        
        result["matching_count"] = matching_methods
        result["different_bytecode"] = different_bytecode
        result["perfect_match"] = (
            len(bootstrap_selectors) == len(system_selectors) and
            len(common_selectors) == len(bootstrap_selectors) and
            matching_methods == len(common_selectors)
        )
        
        return result
    
    def export_results_json(self, output_path: Path):
        """Export comparison results as JSON for programmatic analysis"""
        logger.info(f"Exporting JSON results: {output_path}")
        
        export_data = {
            "timestamp": Path.cwd().name,
            "bootstrap_log_info": self.bootstrap_log_info,
            "comparison_results": self.comparison_results,
            "summary": {
                "bootstrap_classes": len(self.bootstrap_classes),
                "system_classes": len(self.system_classes),
                "perfect_matches": len(self.comparison_results.get('perfect_matches', [])),
                "structural_matches": len(self.comparison_results.get('structural_matches', [])),
                "mismatches": len(self.comparison_results.get('mismatches', [])),
                "bootstrap_only": len(self.comparison_results.get('bootstrap_only', [])),
                "system_only": len(self.comparison_results.get('system_only', []))
            }
        }
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(export_data, f, indent=2, default=str)

    def generate_report(self, output_path: Path):
        """Generate a detailed comparison report"""
        logger.info(f"Generating report: {output_path}")
        
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("# VM System Comparison Report\n\n")
            f.write(f"Generated at: {Path.cwd()}\n")
            f.write(f"Bootstrap JSON: {len(self.bootstrap_classes)} classes\n")
            f.write(f"System JSON: {len(self.system_classes)} classes\n\n")
            
            # Bootstrap log summary
            if self.bootstrap_log_info:
                f.write("## Bootstrap Log Analysis\n\n")
                log_info = self.bootstrap_log_info
                f.write(f"- Bootstrap phases: {len(log_info['bootstrap_phases'])}\n")
                f.write(f"- Class creation attempts: {len(log_info['created_classes'])}\n")
                f.write(f"- Failed class creations: {len(log_info['failed_classes'])}\n")
                f.write(f"- Methods installed: {log_info['installed_methods']}\n")
                f.write(f"- Failed method installations: {log_info['failed_methods']}\n")
                
                if log_info['object_counts']:
                    f.write(f"- Object counts by phase:\n")
                    for phase, count in log_info['object_counts'].items():
                        f.write(f"  - {phase}: {count} objects\n")
                
                if log_info['failed_classes']:
                    f.write("\n### Failed Class Creations\n\n")
                    for failed in log_info['failed_classes']:
                        f.write(f"- {failed['name']}: {failed['reason']}\n")
                
                f.write("\n")
            
            # Overall statistics
            if self.comparison_results:
                stats = self.comparison_results['statistics']
                f.write("## Overall Statistics\n\n")
                f.write(f"- Bootstrap classes: {stats['bootstrap_classes']}\n")
                f.write(f"- System classes: {stats['system_classes']}\n")
                f.write(f"- Common classes: {stats['common_classes']}\n")
                f.write(f"- Perfect matches: {stats['perfect_match_count']}\n")
                f.write(f"- Structural matches: {stats['structural_match_count']}\n")
                f.write(f"- Mismatches: {stats['mismatch_count']}\n")
                f.write(f"- Bootstrap-only classes: {len(self.comparison_results['bootstrap_only'])}\n")
                f.write(f"- System-only classes: {len(self.comparison_results['system_only'])}\n\n")
                
                # Coverage statistics
                total_common = stats['common_classes']
                if total_common > 0:
                    perfect_percentage = (stats['perfect_match_count'] / total_common) * 100
                    f.write(f"**Perfect Match Rate: {perfect_percentage:.1f}%**\n\n")
                
                # Perfect matches
                if self.comparison_results['perfect_matches']:
                    f.write("## Perfect Matches\n\n")
                    f.write("These classes match exactly between bootstrap and system:\n\n")
                    for match in self.comparison_results['perfect_matches']:
                        f.write(f"- **{match['class_name']}**: {match['statistics']['bootstrap_methods']} methods\n")
                    f.write("\n")
                
                # Structural matches
                if self.comparison_results['structural_matches']:
                    f.write("## Structural Matches (Different Methods)\n\n")
                    for match in self.comparison_results['structural_matches']:
                        f.write(f"### {match['class_name']}\n\n")
                        stats = match['statistics']
                        f.write(f"- Bootstrap methods: {stats['bootstrap_methods']}\n")
                        f.write(f"- System methods: {stats['system_methods']}\n")
                        f.write(f"- Matching methods: {stats['matching_methods']}\n")
                        f.write(f"- Different bytecode: {stats['different_bytecode']}\n")
                        f.write(f"- Bootstrap-only methods: {stats['bootstrap_only_methods']}\n")
                        f.write(f"- System-only methods: {stats['system_only_methods']}\n\n")
                
                # Mismatches
                if self.comparison_results['mismatches']:
                    f.write("## Class Mismatches\n\n")
                    for mismatch in self.comparison_results['mismatches']:
                        f.write(f"### {mismatch['class_name']}\n\n")
                        
                        if mismatch['differences']:
                            f.write("**Structural Differences:**\n")
                            for diff in mismatch['differences']:
                                f.write(f"- {diff}\n")
                            f.write("\n")
                        
                        # Method comparison details
                        for method_type in ['instance', 'class']:
                            if method_type in mismatch['method_comparison']:
                                method_comp = mismatch['method_comparison'][method_type]
                                if method_comp['bootstrap_count'] > 0 or method_comp['system_count'] > 0:
                                    f.write(f"**{method_type.title()} Methods:**\n")
                                    f.write(f"- Bootstrap: {method_comp['bootstrap_count']}, System: {method_comp['system_count']}\n")
                                    f.write(f"- Matching: {method_comp['matching_count']}, Different: {method_comp['different_bytecode']}\n")
                                    
                                    if method_comp['bootstrap_only_methods']:
                                        f.write(f"- Bootstrap-only: {', '.join(method_comp['bootstrap_only_methods'])}\n")
                                    if method_comp['system_only_methods']:
                                        f.write(f"- System-only: {', '.join(method_comp['system_only_methods'])}\n")
                                    
                                    # Show method differences for key methods
                                    method_details = method_comp.get('method_details', {})
                                    different_methods = [k for k, v in method_details.items() if isinstance(v, list)]
                                    if different_methods and len(different_methods) <= 10:  # Only show if not too many
                                        f.write(f"- Method differences:\n")
                                        for method_name in sorted(different_methods)[:5]:  # Show first 5
                                            f.write(f"  - `{method_name}`: {'; '.join(method_details[method_name])}\n")
                                        if len(different_methods) > 5:
                                            f.write(f"  - ... and {len(different_methods) - 5} more methods\n")
                                    
                                    f.write("\n")
                        
                        f.write("\n")
                
                # Classes only in one side
                if self.comparison_results['bootstrap_only']:
                    f.write("## Bootstrap-Only Classes\n\n")
                    for class_name in sorted(self.comparison_results['bootstrap_only']):
                        method_count = self.bootstrap_classes[class_name].method_count()
                        f.write(f"- **{class_name}**: {method_count} methods\n")
                    f.write("\n")
                
                if self.comparison_results['system_only']:
                    f.write("## System-Only Classes\n\n")
                    for class_name in sorted(self.comparison_results['system_only']):
                        method_count = self.system_classes[class_name].method_count()
                        f.write(f"- **{class_name}**: {method_count} methods\n")
                    f.write("\n")
    
    def run_comparison(self):
        """Run the complete comparison process"""
        logger.info("Starting VM system comparison...")
        
        # File paths
        core_dump = self.base_path / "core_vm_dump.txt"
        vm_dump = self.base_path / "vm_memory_dump.txt"
        bootstrap_json = self.base_path / "Bootstrap" / "bootstrap.json"
        system_json = self.base_path / "complete_system_final.json"
        report_path = self.base_path / "vm_system_comparison_report.md"
        json_path = self.base_path / "vm_system_comparison_results.json"
        
        try:
            # Parse all data sources
            self.parse_bootstrap_logs(core_dump, vm_dump)
            self.parse_bootstrap_json(bootstrap_json)
            self.parse_system_json(system_json)
            
            # Run comparison
            self.compare_classes()
            
            # Generate report
            self.generate_report(report_path)
            
            # Export JSON results
            self.export_results_json(json_path)
            
            # Print summary
            stats = self.comparison_results['statistics']
            logger.info("Comparison completed successfully!")
            logger.info(f"Results: {stats['perfect_match_count']} perfect matches, "
                       f"{stats['structural_match_count']} structural matches, "
                       f"{stats['mismatch_count']} mismatches")
            logger.info(f"Report saved to: {report_path}")
            logger.info(f"JSON results saved to: {json_path}")
            
            return True
            
        except Exception as e:
            logger.error(f"Comparison failed: {str(e)}")
            return False

def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="Compare VM bootstrap classes with system image")
    parser.add_argument("--path", type=Path, default=Path.cwd(), 
                       help="Base path to Smalltalk-80 directory")
    parser.add_argument("--verbose", "-v", action="store_true",
                       help="Enable verbose logging")
    parser.add_argument("--json-only", action="store_true",
                       help="Only export JSON results, skip markdown report")
    parser.add_argument("--report-only", action="store_true",
                       help="Only generate markdown report, skip JSON export")
    
    args = parser.parse_args()
    
    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)
    
    comparator = VMSystemComparator(args.path)
    
    # Override the run_comparison method behavior based on args
    if args.json_only or args.report_only:
        # Custom run with selective output
        logger.info("Starting VM system comparison...")
        
        # File paths
        core_dump = comparator.base_path / "core_vm_dump.txt"
        vm_dump = comparator.base_path / "vm_memory_dump.txt"
        bootstrap_json = comparator.base_path / "Bootstrap" / "bootstrap.json"
        system_json = comparator.base_path / "complete_system_final.json"
        report_path = comparator.base_path / "vm_system_comparison_report.md"
        json_path = comparator.base_path / "vm_system_comparison_results.json"
        
        try:
            # Parse all data sources
            comparator.parse_bootstrap_logs(core_dump, vm_dump)
            comparator.parse_bootstrap_json(bootstrap_json)
            comparator.parse_system_json(system_json)
            
            # Run comparison
            comparator.compare_classes()
            
            # Generate outputs based on arguments
            if args.json_only:
                comparator.export_results_json(json_path)
                logger.info(f"JSON results saved to: {json_path}")
            elif args.report_only:
                comparator.generate_report(report_path)
                logger.info(f"Report saved to: {report_path}")
            
            # Print summary
            stats = comparator.comparison_results['statistics']
            logger.info("Comparison completed successfully!")
            logger.info(f"Results: {stats['perfect_match_count']} perfect matches, "
                       f"{stats['structural_match_count']} structural matches, "
                       f"{stats['mismatch_count']} mismatches")
            
            success = True
            
        except Exception as e:
            logger.error(f"Comparison failed: {str(e)}")
            success = False
    else:
        success = comparator.run_comparison()
    
    return 0 if success else 1

if __name__ == "__main__":
    exit(main())