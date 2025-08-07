# Class Creation Dependencies Analysis

## Critical Path for Class Creation

When executing: `Object subclass: #MyClass instanceVariableNames: '' ...`

### Call Chain:
1. **Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:**
   - Calls: `self class name:inEnvironment:subclassOf:...`

2. **Class class>>name:inEnvironment:subclassOf:...**
   - Dependencies:
     - `newName first isUppercase` → Character>>isUppercase
     - `environ includesKey:` → Dictionary>>includesKey:
     - `environ at:` → Dictionary>>at:
     - `oldClass isKindOf: Behavior` → Object>>isKindOf:
     - `self newNamed:` → Class class>>newNamed:
     - `oldClass copy` → Object>>copy
     - `newClass subclassOf:oldClass:...` → ClassDescription>>subclassOf:...
     - `newClass declare:` → ClassDescription>>declare:
     - `newClass sharing:` → ClassDescription>>sharing:
     - `environ at: newName put: newClass` → Dictionary>>at:put:
     - `SystemOrganization classify:under:` → SystemOrganizer>>classify:under:
     - `newClass validateFrom:in:...` → ClassDescription>>validateFrom:...
     - `newClass superclass addSubclass:` → Behavior>>addSubclass:
     - `Smalltalk changes` → ChangeSet (optional)

3. **Class class>>newNamed:**
   - Dependencies:
     - `Metaclass subclassOf:` → Metaclass class>>subclassOf:
     - `MethodDictionary new` → MethodDictionary class>>new
     - `ClassOrganizer new` → ClassOrganizer class>>new

4. **Metaclass class>>subclassOf:**
   - Creates the metaclass for the new class
   - Needs Metaclass to be fully functional

## Essential Classes for Class Creation

### Phase 1: Core Bootstrap (Already Done)
- Object
- Behavior  
- ClassDescription
- Class
- Metaclass
- UndefinedObject (for nil)
- Boolean, True, False (for conditionals)
- SmallInteger (for arithmetic)
- Symbol (for selectors)
- String (for names)
- Array (for collections)
- BlockContext (for blocks)

### Phase 2: Class Creation Infrastructure (Needed)
Must be loaded via C# before we can create classes via Smalltalk:

1. **Character** - For `isUppercase` check
2. **Collection hierarchy up to Dictionary**:
   - Collection
   - Set
   - Dictionary
   - SystemDictionary (for Smalltalk global)
   - Association

3. **MethodDictionary** - Subclass of IdentityDictionary
   - IdentityDictionary
   - LookupKey

4. **ClassOrganizer** - For organizing methods
   - Organizer (superclass)

5. **SystemOrganizer** - For organizing classes (or we can stub this)

6. **Scanner** - For parsing variable names (maybe can avoid)

7. **WriteStream** - Used by various methods
   - Stream
   - PositionableStream  
   - WriteStream

8. **OrderedCollection** - Used internally by many methods

9. **Undeclared** - Global dictionary for undefined references (can stub)

### Phase 3: Everything Else
Once the above are loaded, we can create all other classes through Smalltalk:
- All remaining collections
- All remaining streams
- Files
- Graphics
- etc.

## Simplified Bootstrap Strategy

### Option A: Minimal Fake Class Creation
Create a simplified `Class>>subclass:...` that:
1. Creates a basic class object
2. Sets its superclass
3. Registers in Smalltalk dictionary
4. Skips all validation and organization

### Option B: Full Infrastructure
Load all Phase 2 classes via C#, then use real class creation.

### Option C: Hybrid
1. Load Phase 1 + Phase 2 via C#
2. Implement simplified class creation that works
3. Later replace with full version when all dependencies available

## Recommended Approach

**Three-Phase Bootstrap:**

1. **Phase 1 (C# Bootstrap)**: Core classes (Object through BlockContext)
2. **Phase 2 (C# Load)**: Infrastructure classes (Character through OrderedCollection)  
3. **Phase 3 (Smalltalk Load)**: Everything else via `subclass:`

The division point is after loading:
- MethodDictionary
- ClassOrganizer
- SystemOrganizer (or stub)
- Character
- OrderedCollection
- WriteStream

These ~15-20 additional classes enable full class creation.