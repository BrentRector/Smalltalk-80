#!/usr/bin/env python3
"""
Comprehensive Validation Test for Smalltalk-80 VM
Validates all loaded classes against system image JSON structure
"""

import subprocess
import os
import json
import sys

def run_vm_with_memory_dump():
    """Run the VM and capture memory dump for validation"""
    print("=== Running VM with Memory Dump ===")
    
    # Change to the bin directory
    os.chdir("bin/Debug/net9.0")
    
    # Run interpreter with memory dump
    try:
        result = subprocess.run(
            ["./TestLowLevelClasses.exe"], 
            capture_output=True, 
            text=True, 
            timeout=30
        )
        
        print("VM Execution Output:")
        print(result.stdout)
        if result.stderr:
            print("VM Errors:")
            print(result.stderr)
            
        return result.returncode == 0
        
    except subprocess.TimeoutExpired:
        print("VM execution timed out")
        return False
    except Exception as e:
        print(f"Error running VM: {e}")
        return False
    finally:
        # Change back to original directory
        os.chdir("../../..")

def run_vm_comparison():
    """Run the VM comparison analysis"""
    print("\n=== Running VM System Comparison ===")
    
    try:
        result = subprocess.run(
            ["python", "vm_system_comparison.py"], 
            capture_output=True, 
            text=True
        )
        
        print("Comparison Output:")
        print(result.stdout)
        if result.stderr:
            print("Comparison Errors:")
            print(result.stderr)
            
        return result.returncode == 0
        
    except Exception as e:
        print(f"Error running comparison: {e}")
        return False

def analyze_validation_results():
    """Analyze the validation results from comparison"""
    print("\n=== Analyzing Validation Results ===")
    
    try:
        with open("vm_system_comparison_results.json", "r") as f:
            results = json.load(f)
        
        summary = results.get("summary", {})
        
        print(f"Validation Summary:")
        print(f"   - Bootstrap classes: {summary.get('bootstrap_classes', 0)}")
        print(f"   - System classes: {summary.get('system_classes', 0)}")
        print(f"   - Perfect matches: {summary.get('perfect_matches', 0)}")
        print(f"   - Structural matches: {summary.get('structural_matches', 0)}")
        print(f"   - Complete mismatches: {summary.get('mismatches', 0)}")
        
        # Calculate match rate
        total_bootstrap = summary.get('bootstrap_classes', 0)
        perfect = summary.get('perfect_matches', 0)
        structural = summary.get('structural_matches', 0)
        
        if total_bootstrap > 0:
            perfect_rate = (perfect / total_bootstrap) * 100
            structural_rate = ((perfect + structural) / total_bootstrap) * 100
            
            print(f"\nValidation Rates:")
            print(f"   - Perfect match rate: {perfect_rate:.1f}%")
            print(f"   - Structural match rate: {structural_rate:.1f}%")
        
        # Analyze mismatches
        comparison_results = results.get("comparison_results", {})
        mismatches = comparison_results.get("mismatches", [])
        
        if mismatches:
            print(f"\nClasses with structural issues ({len(mismatches)}):")
            for mismatch in mismatches:
                class_name = mismatch.get("class_name", "Unknown")
                differences = mismatch.get("differences", [])
                print(f"   - {class_name}: {len(differences)} difference(s)")
                for diff in differences[:2]:  # Show first 2 differences
                    print(f"     - {diff}")
                if len(differences) > 2:
                    print(f"     - ... and {len(differences) - 2} more")
        
        # Check for perfect matches
        perfect_matches = comparison_results.get("perfect_matches", [])
        if perfect_matches:
            print(f"\nPerfect matches ({len(perfect_matches)}):")
            for match in perfect_matches[:5]:  # Show first 5
                print(f"   - {match.get('class_name', 'Unknown')}")
            if len(perfect_matches) > 5:
                print(f"   - ... and {len(perfect_matches) - 5} more")
        
        return True
        
    except FileNotFoundError:
        print("ERROR: Validation results file not found")
        return False
    except Exception as e:
        print(f"ERROR: Error analyzing results: {e}")
        return False

def run_additional_validation_tests():
    """Run additional specific validation tests"""
    print("\n=== Running Additional Validation Tests ===")
    
    validation_tests = [
        ("TestBlueBookClassCreation.exe", "Blue Book class creation"),
        ("TestClassCreation.exe", "Basic class creation"),
        ("TestAssociation.exe", "Association class validation"),
    ]
    
    results = []
    os.chdir("bin/Debug/net9.0")
    
    try:
        for test_exe, description in validation_tests:
            print(f"\n🔍 Running {description}...")
            try:
                result = subprocess.run(
                    [f"./{test_exe}"], 
                    capture_output=True, 
                    text=True,
                    timeout=10
                )
                
                success = result.returncode == 0
                results.append((description, success))
                
                if success:
                    print(f"   ✅ {description}: PASSED")
                else:
                    print(f"   ❌ {description}: FAILED")
                    if result.stdout:
                        print(f"      Output: {result.stdout[:200]}...")
                    if result.stderr:
                        print(f"      Error: {result.stderr[:200]}...")
                        
            except subprocess.TimeoutExpired:
                print(f"   ⏰ {description}: TIMEOUT")
                results.append((description, False))
            except Exception as e:
                print(f"   ❌ {description}: ERROR - {e}")
                results.append((description, False))
    
    finally:
        os.chdir("../../..")
    
    return results

def main():
    """Main validation function"""
    print("Smalltalk-80 VM Comprehensive Validation")
    print("=" * 50)
    
    all_passed = True
    
    # Step 1: Run VM with memory dump
    vm_success = run_vm_with_memory_dump()
    if not vm_success:
        print("❌ VM execution failed")
        all_passed = False
    
    # Step 2: Run comparison analysis  
    comparison_success = run_vm_comparison()
    if not comparison_success:
        print("❌ Comparison analysis failed")
        all_passed = False
    
    # Step 3: Analyze results
    analysis_success = analyze_validation_results()
    if not analysis_success:
        print("❌ Results analysis failed")
        all_passed = False
    
    # Step 4: Additional validation tests
    additional_results = run_additional_validation_tests()
    additional_passed = all(success for _, success in additional_results)
    if not additional_passed:
        print("❌ Some additional validation tests failed")
        all_passed = False
    
    # Final summary
    print("\n" + "=" * 50)
    if all_passed:
        print("🎉 All validation tests PASSED!")
        print("✅ VM classes match system image structure")
    else:
        print("⚠️  Some validation tests FAILED")
        print("❌ Review output above for specific issues")
    
    return all_passed

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)