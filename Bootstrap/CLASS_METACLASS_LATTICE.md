# Phase 2 Class/Metaclass Lattice Diagram

## The Complete Lattice Structure

```
                                    ╔════════════════╗
                                    ║   Metaclass    ║◄──────┐
                                    ║────────────────║       │
                                    ║ thisClass      ║       │ (class pointer)
                                    ╚════════════════╝       │
                                             ▲                │
                                             │ superclass     │
                                    ╔════════════════╗       │
                                    ║ Metaclass class║───────┘
                                    ╚════════════════╝
                                             ▲
                                             │ superclass
                                    ╔════════════════╗
                                    ║  Class class   ║
                                    ╚════════════════╝
                                             ▲
                                             │ superclass
                                ╔═══════════════════════╗
                                ║ ClassDescription class║
                                ╚═══════════════════════╝
                                             ▲
                                             │ superclass
                                    ╔════════════════╗
                                    ║ Behavior class ║
                                    ╚════════════════╝
                                             ▲
                                             │ superclass
                                    ╔════════════════╗
                                    ║  Object class  ║
                                    ╚════════════════╝
                                             ▲
                                             │ superclass
                                    ╔════════════════╗
                                    ║ProtoObject class║
                                    ╚════════════════╝
                                             ▲
                                             │ superclass
                                    ╔════════════════╗
                                    ║     Class      ║
                                    ║────────────────║
                                    ║ name           ║
                                    ║ superclass     ║
                                    ║ methodDict     ║
                                    ║ format         ║
                                    ║ instanceVars   ║
                                    ╚════════════════╝
```

## Instance Side Hierarchy

```
                    ProtoObject
                         │
                         ▼
                      Object
                         │
                    ┌────┴────┐
                    ▼         ▼
                 Behavior   [other Object subclasses]
                    │
                    ▼
              ClassDescription
                    │
            ┌───────┴────────┐
            ▼                ▼
         Class           Metaclass
                         (thisClass)
```

## Metaclass Side (All metaclasses are instances of Metaclass)

```
    ╔═══════════════╗         ╔════════════════╗
    ║ ProtoObject   ║◄────────║ProtoObject class║
    ╚═══════════════╝ class   ╚════════════════╝
                               (instance of Metaclass)
    
    ╔═══════════════╗         ╔════════════════╗
    ║    Object     ║◄────────║  Object class  ║
    ╚═══════════════╝ class   ╚════════════════╝
                               (instance of Metaclass)
    
    ╔═══════════════╗         ╔════════════════╗
    ║   Behavior    ║◄────────║ Behavior class ║
    ╚═══════════════╝ class   ╚════════════════╝
                               (instance of Metaclass)
    
    ╔═══════════════╗         ╔═══════════════════╗
    ║ClassDescription║◄────────║ClassDescription class║
    ╚═══════════════╝ class   ╚═══════════════════╝
                               (instance of Metaclass)
    
    ╔═══════════════╗         ╔════════════════╗
    ║     Class     ║◄────────║  Class class   ║
    ╚═══════════════╝ class   ╚════════════════╝
                               (instance of Metaclass)
    
    ╔═══════════════╗         ╔════════════════╗
    ║   Metaclass   ║◄────────║ Metaclass class║
    ╚═══════════════╝ class   ╚════════════════╝
                               (instance of Metaclass!)
```

## Method Dictionary Wiring

Each class has its own method dictionary, installed via Phase 2 messages:

```
ProtoObject ──────► ProtoObjectMethodDict
                    { "==" → EqualsMethod }

Object ───────────► ObjectMethodDict
                    { "initialize" → InitializeMethod,
                      "class" → ClassMethod }

Behavior ─────────► BehaviorMethodDict
                    { "methodDict" → MethodDictGetter }

ClassDescription ─► ClassDescriptionMethodDict
                    { "format" → FormatGetter }

Class ────────────► ClassMethodDict
                    { "superclass" → SuperclassGetter,
                      "name" → NameGetter }

Metaclass ────────► MetaclassMethodDict
                    { "thisClass" → ThisClassGetter }
```

## Key Relationships

### 1. The Metaclass Loop
```
Metaclass class ──class──► Metaclass
    ▲                          │
    │                          │
    └────────thisClass─────────┘
```

### 2. The Class Hierarchy Root
```
ProtoObject class ──superclass──► Class
                                    │
                                    ▼
                              ClassDescription
                                    │
                                    ▼
                                 Behavior
                                    │
                                    ▼
                                  Object
                                    │
                                    ▼
                               ProtoObject
                                  (nil)
```

### 3. Instance Variables

```
Behavior
├── superclass
├── methodDict
└── format

ClassDescription (adds)
├── instanceVariables
└── organization

Class (adds)
├── name
├── classPool
└── sharedPools

Metaclass (adds)
└── thisClass
```

## Phase 2 Construction Sequence

1. **Build class hierarchy** via `subclass:instanceVariableNames:`
2. **Get method dictionaries** via `methodDict` message
3. **Create methods** via `CompiledMethod newWithSelector:bytecodes:`
4. **Install methods** via `at:put:` to method dictionaries
5. **Register in Smalltalk** via `Smalltalk at:put:`

## Critical Invariants

1. **Every object has a class**: Even ProtoObject has ProtoObject class
2. **Every class is an instance of a metaclass**: Regular classes → their metaclass
3. **Every metaclass is an instance of Metaclass**: Including Metaclass itself
4. **Metaclass class is an instance of Metaclass**: The loop closes here
5. **ProtoObject has no superclass**: It's the root (superclass = nil)
6. **ProtoObject class's superclass is Class**: Not ProtoObject!

## Method Resolution

When sending a message to an object:
1. Look in the object's class's methodDict
2. If not found, look in superclass's methodDict
3. Continue up the superclass chain to ProtoObject
4. If still not found, send `doesNotUnderstand:`

When sending a message to a class:
1. Look in the class's metaclass's methodDict
2. Follow the metaclass superclass chain
3. Eventually reach Class, then Behavior, then Object, then ProtoObject