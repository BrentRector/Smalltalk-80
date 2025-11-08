# Configuring Persistent Smalltalk-80 Expert Agents

This guide explains how to configure persistent expert agents in Claude Code with extensive domain knowledge, specifically for Smalltalk-80 expertise.

## Overview

**Agents** in Claude Code are specialized AI assistants with domain expertise, custom tools, and focused responsibilities. They are:

- ✅ **Persistent across sessions** - Configured once, available forever
- ✅ **Automatically discovered** - Stored as files in `.claude/agents/`
- ✅ **Context-isolated** - Each agent has its own 200K token context window
- ✅ **Automatically invoked** - The main assistant delegates to them based on task descriptions
- ✅ **Independently configured** - Each can use different models, tools, and system prompts

## Agent File Structure

Agent files are markdown files stored in `.claude/agents/` with YAML frontmatter and a system prompt:

```markdown
---
name: agent-name
description: Detailed description of when this agent should be invoked...
tools: Task, Bash, Read, Write, Edit, Grep, Glob
model: claude-sonnet-4-5
color: blue
---

[Your agent's system prompt and expertise content goes here]
```

### Frontmatter Fields

| Field | Purpose | Example |
|-------|---------|---------|
| `name` | Unique identifier for the agent | `smalltalk-bluebook-expert` |
| `description` | When to invoke (with examples) | Detailed invocation criteria with `<example>` blocks |
| `tools` | Which tools the agent can use | `Task, Bash, Read, Write, Edit` |
| `model` | Which Claude model to use | `claude-sonnet-4-5` |
| `color` | UI color coding | `blue`, `yellow`, `green` |

### Description Field Best Practices

The `description` field determines when your agent is automatically invoked. Write it with:

1. **Clear use cases** - "Use this agent when you need X..."
2. **Concrete examples** - Include `<example>` blocks showing invocation patterns
3. **Domain boundaries** - Clarify what's in scope vs. out of scope
4. **Action triggers** - Specific verbs that should trigger invocation

**Example description with invocation examples:**

```markdown
description: Use this agent when you need authoritative answers about Smalltalk-80 language semantics, protocol specifications, and Blue Book canonical behavior. This agent provides detailed knowledge of the complete Smalltalk-80 class hierarchy, message protocols, and language semantics as defined in the original Blue Book specification.\n\nExamples:\n- <example>\n  Context: User needs to understand correct protocol for a Smalltalk class\n  user: "What messages should Collection implement?"\n  assistant: "Let me consult the smalltalk-bluebook-expert for the canonical protocol"\n  <commentary>Protocol questions require Blue Book expertise</commentary>\n  </example>\n- <example>\n  Context: User is implementing a Smalltalk class and needs to know correct behavior\n  user: "How should Number>>coerce:from: work?"\n  assistant: "I'll use the smalltalk-bluebook-expert to get the precise semantics"\n  <commentary>Semantic questions require authoritative Blue Book reference</commentary>\n  </example>
```

## Including Extensive Documentation

You have **three approaches** for including many pages of markdown documentation in your agent:

### Option 1: Direct Embedding (Best for <50KB)

**When to use:** Frequently-referenced material like bytecode tables, quick references, or core concepts.

**Approach:** Paste documentation directly into the agent markdown file after the frontmatter.

```markdown
---
name: smalltalk-bytecode-expert
description: Expert in Smalltalk-80 bytecode...
tools: Read, Write, Edit
model: claude-sonnet-4-5
---

You are an expert in Smalltalk-80 bytecode semantics.

## Complete Bytecode Reference

### Push Operations (0-119)

**Push Receiver Variable (0-15)**
- Bytecode: 0-15
- Operation: Push receiver's instance variable N onto stack
- Example: Bytecode 0 = push receiver's first instance variable

**Push Temporary Variable (16-31)**
- Bytecode: 16-31
- Operation: Push temporary variable (N-16) onto stack
- Example: Bytecode 16 = push first temporary variable

[... continue embedding reference material ...]
```

**Pros:**
- ✅ Instant access - no file I/O needed
- ✅ Always available in context
- ✅ Fast lookups

**Cons:**
- ❌ Large files consume context budget
- ❌ Less maintainable for frequently-updated docs
- ❌ All content loaded whether needed or not

### Option 2: File References (Best for Large Documentation)

**When to use:** Large reference documents (100KB+), infrequently-accessed specs, or frequently-updated documentation.

**Approach:** Reference documentation files and have the agent read them dynamically as needed.

```markdown
---
name: smalltalk-bluebook-expert
description: Expert in Smalltalk-80 Blue Book...
tools: Task, Bash, Read, Write, Edit
model: claude-sonnet-4-5
---

You are an expert in Smalltalk-80 with access to comprehensive reference documentation.

## Available Reference Documentation

**Part 4: The Smalltalk-80 Virtual Image**
- `docs/Bluebook - Chapter 26.md` (59KB) - Protocol for All Objects
- `docs/Bluebook - Chapter 27.md` (54KB) - Protocol for Numbers
- `docs/Bluebook - Chapter 28.md` (36KB) - Protocol for Collections
- `docs/Bluebook - Chapter 29.md` (27KB) - Protocol for Streams
- `docs/Bluebook - Chapter 30.md` (43KB) - Protocol for Classes

**Implementation Documentation**
- `docs/Interpreter-Architecture.md` (45KB) - VM architecture
- `docs/Formal Specification of the Primitive Methods.md` (31KB)

## Methodology

When answering questions:

1. **Identify Relevant Documentation**
   - Determine which chapter/file contains the answer
   - Use grep/glob if unsure of location

2. **Read Documentation First**
   ```
   Use Read tool: docs/Bluebook - Chapter 27.md
   ```

3. **Extract and Cite**
   - Quote relevant sections verbatim
   - Reference chapter and section numbers
   - Provide complete protocol specifications

4. **Answer Comprehensively**
   - Include method signatures
   - Explain semantics and edge cases
   - Note related protocols and patterns

**Example workflow:**
```
User asks: "What messages does Number implement for coercion?"
→ Read docs/Bluebook - Chapter 27.md
→ Search for "coercion" protocol
→ Extract complete method specifications
→ Provide answer with Blue Book citations
```
```

**Pros:**
- ✅ Unlimited documentation size
- ✅ Only loads what's needed (context-efficient)
- ✅ Easy to update docs separately
- ✅ Maintainable - edit docs without changing agent

**Cons:**
- ❌ Requires file I/O on each lookup
- ❌ Agent must know where to find information
- ❌ Slightly slower than embedded content

### Option 3: Hybrid Approach (Recommended)

**When to use:** Most real-world scenarios - combine embedded quick reference with on-demand detailed docs.

**Approach:** Embed frequently-needed information, reference detailed documentation.

```markdown
---
name: smalltalk-interpreter-expert
description: Expert in Smalltalk-80 VM implementation...
tools: Task, Bash, Read, Write, Edit
model: claude-sonnet-4-5
---

You are an expert in Smalltalk-80 interpreter implementation.

## Quick Reference - Embedded Content

### Critical Bytecode Ranges

| Range | Operation | Example |
|-------|-----------|---------|
| 0-15 | Push receiver variable | 0 = push receiver[0] |
| 16-31 | Push temporary variable | 16 = push temp[0] |
| 32-63 | Push literal constant | 32 = push literal[0] |
| 96-103 | Pop and store receiver variable | 96 = pop to receiver[0] |
| 104-111 | Pop and store temporary | 104 = pop to temp[0] |
| 128-175 | Send with inline arg count | Selector in following byte |

### Object Format Codes

- `0` - No fields (immediate integers)
- `1` - Fixed fields only (Point, Association)
- `2` - Indexable pointer fields (Array, MethodContext)
- `3` - Indexable byte fields (String, ByteArray)
- `4` - Indexable word fields (not used in Smalltalk-80)

## Detailed Documentation - On-Demand

For comprehensive specifications, read these files as needed:

**Core VM Documentation:**
- `docs/Interpreter-Architecture.md` - Complete VM architecture
- `docs/COLDSTART-PHASES-DETAILED.md` - Bootstrap process
- `docs/Formal Specification of the Primitive Methods.md` - Primitive semantics

**Blue Book Chapters:**
- `docs/Bluebook - Chapter 26.md` - Protocol for All Objects
- `docs/Bluebook - Chapter 27.md` - Protocol for Numbers
- `docs/Bluebook - Chapter 28.md` - Protocol for Collections

## Methodology

1. **Check embedded quick reference first** for common lookups
2. **Read detailed documentation** for complex questions
3. **Always cite sources** when referencing specifications
4. **Use Read tool** to access documentation dynamically
```

**Pros:**
- ✅ Best of both worlds - fast common lookups + complete coverage
- ✅ Context-efficient - only loads details when needed
- ✅ Maintainable - quick ref stable, detailed docs separate
- ✅ User-friendly - quick answers for common questions

**Cons:**
- ❌ Requires careful curation of what to embed
- ❌ Duplication risk if embedded content mirrors files

## Existing Smalltalk-80 Agents

This project currently has **three specialized Smalltalk-80 agents**:

### 1. smalltalk-interpreter-expert

**Purpose:** VM implementation, bytecode execution, C# interpreter code

**File:** `.claude/agents/smalltalk-interpreter-expert.md`

**Expertise:**
- Smalltalk-80 bytecode semantics and dispatch
- VM architecture (contexts, method lookup, primitives)
- C# 13 implementation patterns
- Bootstrap process and cold start phases
- Object memory and garbage collection

**When invoked:**
- Reviewing interpreter implementation code
- Implementing missing VM features
- Debugging bytecode execution issues
- Analyzing method dispatch mechanisms
- Bootstrap phase problems

**Key features:**
- Embedded bytecode reference table
- References to implementation docs
- C# coding standards
- Integration with existing codebase patterns

### 2. smalltalk-bluebook-expert

**Purpose:** Language semantics, protocols, canonical Smalltalk-80 behavior

**File:** `.claude/agents/smalltalk-bluebook-expert.md`

**Expertise:**
- Complete Smalltalk-80 class hierarchy
- Message protocols and semantics
- Blue Book canonical specifications
- Protocol compliance and correctness
- Language-level behavior (not implementation)

**When invoked:**
- Protocol specification questions
- Language semantics clarification
- Class hierarchy and inheritance questions
- Message signature questions
- Canonical behavior verification

**Key features:**
- References all 5 Blue Book chapters (26-30)
- Class hierarchy quick reference
- Methodology for citing sources
- Protocol documentation templates

### 3. csharp-13-expert

**Purpose:** Modern C# code generation and refactoring

**File:** `.claude/agents/csharp-13-expert.md`

**Expertise:**
- C# 13 language features
- Modern idiomatic C# patterns
- Code generation and refactoring
- Clean, maintainable C# architecture

**When invoked:**
- Generating new C# classes or methods
- Refactoring existing C# code
- Implementing C# interfaces
- Modernizing C# patterns

## How Agent Invocation Works

The main Claude Code assistant automatically delegates to specialist agents based on:

1. **Task description matching** - Keywords and patterns in your request
2. **Current context** - Files you're working with, recent conversation
3. **Agent descriptions** - The `description` field with examples
4. **Available tools** - What tools the task requires

**Example 1 - Automatic language expert invocation:**

```
You: "What messages should the Collection class implement according to the Blue Book?"

Claude internally:
- Recognizes "Blue Book" + "Collection" + "messages/protocol"
- Matches description of smalltalk-bluebook-expert
- Automatically invokes that agent

Agent workflow:
1. Read docs/Bluebook - Chapter 28.md
2. Extract Collection protocol section
3. List all required messages with signatures
4. Return comprehensive answer to main assistant

You see:
Complete protocol listing with Blue Book citations
```

**Example 2 - Automatic interpreter expert invocation:**

```
You: "Review my implementation of the pushTemporary bytecode handler"

Claude internally:
- Recognizes "bytecode handler" + "implementation"
- Matches description of smalltalk-interpreter-expert
- Automatically invokes that agent

Agent workflow:
1. Read the code file
2. Check against bytecode specification (embedded table)
3. Verify C# implementation patterns
4. Check edge cases and error handling
5. Return review with suggestions

You see:
Code review with Blue Book compliance check
```

## Agent Configuration Workflow

### Step 1: Create Agent File

Create a new markdown file in `.claude/agents/`:

```bash
touch .claude/agents/your-agent-name.md
```

### Step 2: Write Frontmatter

Add YAML frontmatter with agent metadata:

```yaml
---
name: your-agent-name
description: When to invoke this agent... (include examples)
tools: Task, Bash, Read, Write, Edit
model: claude-sonnet-4-5
color: blue
---
```

### Step 3: Write System Prompt

Write the agent's expertise and methodology:

```markdown
You are an expert in [domain].

## Your Expertise
[List key areas of knowledge]

## Available Documentation
[List reference files the agent can read]

## Methodology
[How the agent should approach questions]

1. Read relevant documentation
2. Apply specifications
3. Cite sources
4. Provide complete answers
```

### Step 4: Test Invocation

Ask a question that should trigger your agent:

```
"What is [specific question in agent's domain]?"
```

Verify:
- ✅ Agent was invoked automatically
- ✅ Agent read correct documentation
- ✅ Answer includes proper citations
- ✅ Answer is comprehensive and accurate

### Step 5: Iterate

Refine the agent based on usage:
- Update `description` field if invocation isn't triggering
- Add more embedded quick reference if common lookups are slow
- Reorganize documentation references for clarity
- Update methodology based on actual usage patterns

## Size Limits and Performance

### Agent File Size
- **Practical limit:** No hard limit, but recommended <500KB
- **Embedded content:** Keep <50KB for frequently-used tables
- **System prompt:** Typical 2-10KB works well

### Documentation Files
- **No limit** - Agent reads files dynamically with Read tool
- **Large files:** 100KB+ files work fine (Blue Book chapters are 27-59KB each)
- **Total documentation:** This project has 1.5MB+ docs, all accessible

### Context Windows
- **Per agent:** 200K tokens (~600KB text)
- **Main assistant:** Separate 200K token budget
- **Total:** Multiple agents can run concurrently, each with own budget

### Performance Considerations

**Fast approaches:**
- ✅ Embedded quick reference tables (instant lookup)
- ✅ Small documentation files (<100KB)
- ✅ Well-indexed file references

**Slower approaches:**
- ⚠️ Very large embedded content (loads on every invocation)
- ⚠️ Searching through many files
- ⚠️ Multiple Read operations for single question

**Optimization tips:**
1. Embed frequently-needed data (bytecode tables, constants)
2. Reference detailed specs (complete protocol listings)
3. Use clear file naming for easy lookup
4. Create index/table-of-contents for large doc sets

## Best Practices

### 1. Specialize Agents by Domain

**Good specialization:**
- ✅ Language semantics (one agent)
- ✅ VM implementation (one agent)
- ✅ Testing framework (one agent)

**Poor specialization:**
- ❌ Everything-Smalltalk agent (too broad)
- ❌ One agent per class (too narrow)

### 2. Write Descriptive Invocation Criteria

**Good description:**
```markdown
description: Use this agent when you need expert review and implementation
guidance for Smalltalk-80 interpreter code written in C#. This includes
reviewing interpreter implementations, suggesting code for missing functionality,
analyzing bytecode execution, validating method dispatch mechanisms, and ensuring
compliance with the Blue Book specification.\n\nExamples:\n- <example>...
```

**Poor description:**
```markdown
description: Smalltalk help
```

### 3. Balance Embedded vs. Referenced Content

**Embed:**
- Frequently-referenced tables (bytecode ranges)
- Critical constants (object format codes)
- Quick lookup data (class hierarchy overview)
- Methodology and patterns

**Reference:**
- Complete protocol specifications
- Detailed implementation guides
- Full chapter transcriptions
- Rarely-needed edge case documentation

### 4. Provide Clear Methodology

Help the agent know how to use its tools:

```markdown
## Methodology

When answering questions:

1. **Identify the question type**
   - Protocol specification → Read Blue Book chapter
   - Implementation issue → Read architecture docs
   - Quick lookup → Use embedded reference

2. **Gather information**
   - Use Read tool for documentation
   - Use Grep tool to search codebases
   - Cross-reference multiple sources

3. **Provide comprehensive answer**
   - Cite specific sources (chapter, section, line)
   - Include complete specifications
   - Explain edge cases
   - Show examples
```

### 5. Keep Agents Updated

As your project evolves:
- ✅ Update documentation references when files move
- ✅ Add new doc files to agent's knowledge base
- ✅ Refine invocation triggers based on usage
- ✅ Update embedded quick references when specs change

### 6. Version Control

Treat agent configurations as code:
- ✅ Commit agent files to git
- ✅ Document changes in commit messages
- ✅ Review agent updates in PRs
- ✅ Track which docs each agent references

## Example: Adding New Documentation

Let's say you have 5 new Blue Book chapters to add (Chapters 21-25):

### Step 1: Add Documentation Files

```bash
docs/
├── Bluebook - Chapter 21.md  (NEW - 45KB)
├── Bluebook - Chapter 22.md  (NEW - 67KB)
├── Bluebook - Chapter 23.md  (NEW - 34KB)
├── Bluebook - Chapter 24.md  (NEW - 52KB)
├── Bluebook - Chapter 25.md  (NEW - 41KB)
├── Bluebook - Chapter 26.md  (existing)
├── ...
```

### Step 2: Update Agent Configuration

Edit `.claude/agents/smalltalk-bluebook-expert.md`:

```markdown
## Your Knowledge Base

You have access to complete Blue Book reference documentation:

**Part 3: The Formal Specification** (NEW!)
- `docs/Bluebook - Chapter 21.md` - The Specification Language
- `docs/Bluebook - Chapter 22.md` - Formal Specification of the VM
- `docs/Bluebook - Chapter 23.md` - The Object Memory
- `docs/Bluebook - Chapter 24.md` - The Interpreter
- `docs/Bluebook - Chapter 25.md` - The Primitive Methods

**Part 4: The Smalltalk-80 Virtual Image**
- `docs/Bluebook - Chapter 26.md` - Protocol for All Objects
- `docs/Bluebook - Chapter 27.md` - Protocol for Numbers
- `docs/Bluebook - Chapter 28.md` - Protocol for Collections
- `docs/Bluebook - Chapter 29.md` - Protocol for Streams
- `docs/Bluebook - Chapter 30.md` - Protocol for Classes
```

### Step 3: Test New Coverage

Ask questions that require the new chapters:

```
"According to the formal specification, how does the VM handle method lookup?"
```

The agent should:
1. Recognize "formal specification" + "VM" + "method lookup"
2. Read `docs/Bluebook - Chapter 24.md` (The Interpreter)
3. Extract the method lookup specification
4. Provide answer with chapter 24 citations

### Step 4: Commit Changes

```bash
git add docs/Bluebook\ -\ Chapter\ 2*.md
git add .claude/agents/smalltalk-bluebook-expert.md
git commit -m "Add Blue Book Chapters 21-25 to bluebook-expert agent"
git push
```

## Troubleshooting

### Agent Not Being Invoked

**Problem:** You ask a question but the main assistant answers instead of delegating to your agent.

**Solutions:**

1. **Improve the description field:**
   - Add more specific keywords
   - Include concrete examples with `<example>` blocks
   - Make the domain boundaries clearer

2. **Be more explicit in your question:**
   ```
   ❌ "What does this method do?"
   ✅ "According to the Blue Book, what protocol does Number implement?"
   ```

3. **Check agent name conflicts:**
   - Ensure agent names are unique
   - No duplicate names in `.claude/agents/`

### Agent Can't Find Documentation

**Problem:** Agent says it can't find referenced documentation.

**Solutions:**

1. **Verify file paths:**
   ```bash
   ls -la docs/Bluebook*.md
   ```

2. **Use absolute paths from project root:**
   ```markdown
   ✅ `docs/Bluebook - Chapter 26.md`
   ❌ `./docs/Bluebook - Chapter 26.md`
   ❌ `../docs/Bluebook - Chapter 26.md`
   ```

3. **Check file permissions:**
   ```bash
   chmod 644 docs/*.md
   ```

### Answers Are Incomplete

**Problem:** Agent provides partial answers or misses key information.

**Solutions:**

1. **Improve agent methodology:**
   - Add step-by-step instructions
   - Specify what makes a "complete" answer
   - Include examples of good answers

2. **Structure documentation better:**
   - Add table of contents to large docs
   - Use clear section headers
   - Create index files for large doc sets

3. **Provide better prompts:**
   ```markdown
   ## Quality Standards

   Your answers must:
   - ✅ Include complete method signatures
   - ✅ Cite specific chapter and section
   - ✅ Cover edge cases and error conditions
   - ✅ Show example usage
   - ✅ Reference related protocols
   ```

## Advanced Techniques

### Cross-Agent Coordination

Agents can delegate to other agents for specialized subtasks:

```markdown
## Methodology

When you encounter C# implementation questions:
1. Delegate to the csharp-13-expert agent for code generation
2. Review the generated code against Smalltalk-80 semantics
3. Provide integrated answer combining both perspectives
```

### Dynamic Documentation Discovery

For large documentation sets, teach agents to search:

```markdown
## Finding Documentation

If you're unsure which file contains the answer:

1. **Use Grep to search:**
   ```
   grep -r "method lookup" docs/Bluebook*.md
   ```

2. **Check index files:**
   - Read `docs/Bluebook - Part 4 - Chapters 26 to 30.md` for overview
   - Follow references to specific chapters

3. **Search by pattern:**
   ```
   grep -l "coerce.*from" docs/Bluebook*.md
   ```
```

### Versioned Documentation

If you have multiple specification versions:

```markdown
## Available Specifications

**Smalltalk-80 (Blue Book) - Canonical:**
- `docs/bluebook/Chapter-26.md`
- `docs/bluebook/Chapter-27.md`

**Smalltalk-80 (Green Book) - Implementation:**
- `docs/greenbook/Chapter-1.md`
- `docs/greenbook/Chapter-2.md`

**Default:** Always use Blue Book unless user specifies otherwise.

**When user asks "how was this actually implemented":**
- Reference Green Book for implementation details
```

### Agent Composition Patterns

Create specialized agents that work together:

```
smalltalk-language-expert      (Blue Book semantics)
    ↓
smalltalk-interpreter-expert   (VM implementation)
    ↓
csharp-13-expert              (C# code generation)
```

Each handles its domain, creating a pipeline of expertise.

## Summary

### Quick Start Checklist

To create a new persistent Smalltalk-80 expert agent:

- [ ] Create file in `.claude/agents/your-agent-name.md`
- [ ] Add YAML frontmatter with name, description, tools, model
- [ ] Write system prompt with expertise areas
- [ ] List available documentation files
- [ ] Define clear methodology
- [ ] Include examples in description field
- [ ] Test invocation with relevant questions
- [ ] Commit to version control
- [ ] Document in project README

### Key Takeaways

1. **Agents are persistent** - Configure once, available forever
2. **Agents are automatic** - Invoked based on task description
3. **Agents are specialized** - One domain per agent works best
4. **Documentation can be huge** - Reference files, don't embed everything
5. **Hybrid approach wins** - Embed quick reference, reference detailed docs
6. **Test and iterate** - Refine based on actual usage patterns

### Your Smalltalk-80 Agent Ecosystem

You now have three specialized agents ready to use:

1. **smalltalk-bluebook-expert** - Language semantics and protocols
2. **smalltalk-interpreter-expert** - VM implementation and bytecode
3. **csharp-13-expert** - Modern C# code generation

All three are:
- ✅ Persistent across sessions
- ✅ Automatically invoked when relevant
- ✅ Backed by comprehensive documentation (1.5MB+)
- ✅ Context-isolated (separate 200K token budgets)
- ✅ Ready to use right now

Just start asking questions - Claude Code will automatically delegate to the right expert!
