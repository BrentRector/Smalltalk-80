#!/usr/bin/env python3
"""
Test to understand the OOP assignment issue in bootstrap.
"""

def simulate_oop_assignment():
    """Simulate what happens during Object and ObjectClass creation."""
    
    print("Simulating OOP Assignment during bootstrap...\n")
    
    next_oop = 0
    objects = {}
    
    def new_object(name, field_count):
        nonlocal next_oop
        oop = next_oop
        next_oop += 1
        objects[oop] = {"name": name, "fields": [None] * field_count}
        print(f"  Created {name} -> OOP {oop} (shifted: {oop << 1})")
        return oop
    
    # Create Object class (8 fields)
    print("Creating Object class...")
    object_oop = new_object("Object", 8)
    
    # Create ObjectClass metaclass
    print("\nCreating ObjectClass metaclass...")
    # Note: PrimitiveNewMetaclass creates TWO objects:
    # 1. The metaclass itself (6 fields)
    # 2. An empty method dictionary (8 fields)
    print("  Note: PrimitiveNewMetaclass creates a method dictionary too!")
    method_dict_oop = new_object("  MethodDict for ObjectClass", 8)
    objectclass_oop = new_object("ObjectClass", 6)
    
    print(f"\nOOP Summary:")
    print(f"  Object: OOP {object_oop}")
    print(f"  ObjectClass's MethodDict: OOP {method_dict_oop}")
    print(f"  ObjectClass: OOP {objectclass_oop}")
    
    print(f"\nThis explains why ObjectClass gets OOP {objectclass_oop} instead of 1!")
    print("The method dictionary created in PrimitiveNewMetaclass consumes an OOP.")
    
    # But wait, let's check if this is the real issue...
    print("\n" + "="*60)
    print("But this doesn't explain why Object and ObjectClass both have OOP 0!")
    print("Let me check the actual bootstrap log output...")
    
if __name__ == "__main__":
    simulate_oop_assignment()