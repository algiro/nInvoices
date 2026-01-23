# Code Refactoring Patterns and Best Practices

## Overview

This document provides guidelines for applying clean code refactoring patterns when implementing new features or fixing bugs. These patterns ensure maintainability, testability, and adherence to SOLID principles.

## Pattern 1: Extract Strategy Pattern with Type-Based Polymorphism

### Context
When you have conditional logic based on type/classification that determines different behaviors, consider extracting the behavior into separate strategy classes.

### Example: Script vs Executable Process Discovery

**Before (Inline Conditional Logic):**
```csharp
public bool TryGetPID(ISSCProcessInfo processInfo, out int? pid)
{
    var extension = Path.GetExtension(processInfo.Path)?.ToLowerInvariant();
    var isScript = extension is ".cmd" or ".bat" or ".ps1";
    
    if (isScript)
    {
        var interpreterName = extension == ".ps1" ? "powershell" : "cmd";
        var processes = _processInfoProvider.GetProcessesByName(interpreterName);
        // ... matching logic
    }
    else
    {
        var processName = Path.GetFileNameWithoutExtension(processInfo.Path);
        var processes = _processInfoProvider.GetProcessesByName(processName);
        // ... matching logic
    }
}
```

**After (Strategy Pattern):**

1. **Define an enum to classify types:**
```csharp
public enum ExecutableType
{
    Exe,
    Script,
}
```

2. **Create extension methods for type detection:**
```csharp
public static class ISSCProcessInfoExtension
{
    public static bool IsAScriptFile(this ISSCProcessInfo processInfo)
    {
        var extension = Path.GetExtension(processInfo.Path)?.ToLowerInvariant();
        return extension == ".cmd" || extension == ".bat" || extension == ".ps1";
    }

    public static string GetInterpreterForScript(this ISSCProcessInfo processInfo)
    {
        var extension = Path.GetExtension(processInfo.Path)?.ToLowerInvariant();
        return extension switch
        {
            ".cmd" or ".bat" => "cmd",
            ".ps1" => "powershell",
            _ => throw new InvalidOperationException("Not a recognized script file.")
        };
    }
}
```

3. **Create strategy interface:**
```csharp
public interface IExecutableTypeProcess
{
    IEnumerable<ProcessRuntimeInfo> GetRunningProcesses(ISSCProcessInfo processInfo);
}
```

4. **Implement concrete strategies:**
```csharp
public sealed class ExeTypeProcess(IProcessInfoProvider processInfoProvider) : IExecutableTypeProcess
{
    public IEnumerable<ProcessRuntimeInfo> GetRunningProcesses(ISSCProcessInfo processInfo)
    {
        var processName = Path.GetFileNameWithoutExtension(processInfo.Path);
        if (string.IsNullOrWhiteSpace(processName))
            return [];

        return processInfoProvider.GetProcessesByName(processName);
    }
}

public sealed class ScriptTypeProcess(IProcessInfoProvider processInfoProvider) : IExecutableTypeProcess
{
    public IEnumerable<ProcessRuntimeInfo> GetRunningProcesses(ISSCProcessInfo processInfo)
    {
        return processInfoProvider.GetProcessesByName(processInfo.GetInterpreterForScript());
    }
}
```

5. **Create a coordinator class:**
```csharp
public sealed class ExecutableTypesProcess
{
    public ExecutableTypesProcess(IProcessInfoProvider processInfoProvider)
    {
        Exe = new ExeTypeProcess(processInfoProvider);
        Script = new ScriptTypeProcess(processInfoProvider);
    }

    public IEnumerable<ProcessRuntimeInfo> GetRunningProcesses(ISSCProcessInfo processInfo)
    {
        IExecutableTypeProcess executableTypeProcess = processInfo.IsAScriptFile() ? Script : Exe;
        return executableTypeProcess.GetRunningProcesses(processInfo);
    }

    private ExeTypeProcess Exe { get; }
    private ScriptTypeProcess Script { get; }
}
```

6. **Simplify the consumer:**
```csharp
public sealed class DefaultProcessDiscover : IProcessDiscover
{
    private readonly IProcessInfoProvider _processInfoProvider;
    private readonly ExecutableTypesProcess _executableTypesProcess;

    public DefaultProcessDiscover(IProcessInfoProvider processInfoProvider)
    {
        _processInfoProvider = processInfoProvider;
        _executableTypesProcess = new ExecutableTypesProcess(_processInfoProvider);
    }

    public IReadOnlyList<int> GetAllPIDs(ISSCProcessInfo processInfo)
    {
        if (processInfo is null)
            throw new ArgumentNullException(nameof(processInfo));

        var pids = new List<int>();
        var processes = _executableTypesProcess.GetRunningProcesses(processInfo);

        foreach (var process in processes)
        {
            if (IsMatchingProcess(process.CommandLine, processInfo.Path, processInfo.Arguments))
            {
                pids.Add(process.ProcessId);
            }
        }

        return pids;
    }
}
```

### Benefits
- **Single Responsibility**: Each strategy handles one type of executable
- **Open/Closed Principle**: Easy to add new executable types without modifying existing code
- **Testability**: Each strategy can be tested independently
- **Readability**: Clear separation of concerns

---

## Pattern 2: DRY - Eliminate Duplicate Code

### Context
When multiple methods have similar logic, extract the common behavior and reuse it.

### Example: TryGetPID calls GetAllPIDs

**Before:**
```csharp
public bool TryGetPID(ISSCProcessInfo processInfo, out int? pid)
{
    // Duplicate logic for getting processes
    var processes = GetProcesses(processInfo);
    foreach (var process in processes)
    {
        if (IsMatch(process))
        {
            pid = process.Id;
            return true;
        }
    }
    return false;
}

public IReadOnlyList<int> GetAllPIDs(ISSCProcessInfo processInfo)
{
    // Duplicate logic for getting processes
    var processes = GetProcesses(processInfo);
    var pids = new List<int>();
    foreach (var process in processes)
    {
        if (IsMatch(process))
        {
            pids.Add(process.Id);
        }
    }
    return pids;
}
```

**After:**
```csharp
public bool TryGetPID(ISSCProcessInfo processInfo, out int? pid)
{
    if (processInfo is null)
        throw new ArgumentNullException(nameof(processInfo));

    pid = null;

    var allPids = GetAllPIDs(processInfo);
    if (allPids.Count > 0)
    {
        pid = allPids[0];
        return true;
    }
    
    return false;
}

public IReadOnlyList<int> GetAllPIDs(ISSCProcessInfo processInfo)
{
    // Single source of truth for process discovery
    // TryGetPID delegates to this method
}
```

### Benefits
- **Maintainability**: Changes only need to be made in one place
- **Consistency**: Both methods use the same logic
- **Performance**: Can optimize in one location

---

## Pattern 3: Extract Domain Knowledge to Extension Methods

### Context
When you have type-checking or classification logic that's domain-specific, extract it to extension methods on the domain object.

### Example: Script Detection

**Before:**
```csharp
// Scattered throughout codebase
var extension = Path.GetExtension(processInfo.Path)?.ToLowerInvariant();
var isScript = extension is ".cmd" or ".bat" or ".ps1";
```

**After:**
```csharp
// In domain layer
public static class ISSCProcessInfoExtension
{
    public static bool IsAScriptFile(this ISSCProcessInfo processInfo)
    {
        var extension = Path.GetExtension(processInfo.Path)?.ToLowerInvariant();
        return extension == ".cmd" || extension == ".bat" || extension == ".ps1";
    }
}

// Usage
if (processInfo.IsAScriptFile())
{
    // Handle script
}
```

### Benefits
- **Discoverability**: Intellisense shows domain-relevant methods
- **Reusability**: Same logic available everywhere
- **Domain Clarity**: Speaks the language of the domain

---

## Pattern 4: Primary Constructor Usage (C# 12+)

### Context
Use primary constructors for simple classes with constructor injection.

### Example

**Before:**
```csharp
public sealed class ExeTypeProcess : IExecutableTypeProcess
{
    private readonly IProcessInfoProvider _processInfoProvider;

    public ExeTypeProcess(IProcessInfoProvider processInfoProvider)
    {
        _processInfoProvider = processInfoProvider;
    }

    public IEnumerable<ProcessRuntimeInfo> GetRunningProcesses(ISSCProcessInfo processInfo)
    {
        return _processInfoProvider.GetProcessesByName(processName);
    }
}
```

**After:**
```csharp
public sealed class ExeTypeProcess(IProcessInfoProvider processInfoProvider) : IExecutableTypeProcess
{
    public IEnumerable<ProcessRuntimeInfo> GetRunningProcesses(ISSCProcessInfo processInfo)
    {
        return processInfoProvider.GetProcessesByName(processName);
    }
}
```

### Benefits
- **Conciseness**: Less boilerplate code
- **Clarity**: Constructor parameters are immediately visible
- **Modern C#**: Uses latest language features

---

## Refactoring Checklist for AI Agents

When implementing a feature that involves conditional logic based on types:

### 1. Identify the Classification
- [ ] What types/categories exist? (e.g., Exe vs Script)
- [ ] Can this grow in the future? (new types)
- [ ] Is this a cross-cutting concern?

### 2. Extract Type Detection
- [ ] Create an enum for the classification if appropriate
- [ ] Extract detection logic to extension methods
- [ ] Make detection logic reusable across codebase

### 3. Apply Strategy Pattern
- [ ] Create a strategy interface (`IExecutableTypeProcess`)
- [ ] Implement concrete strategies for each type (`ExeTypeProcess`, `ScriptTypeProcess`)
- [ ] Create a coordinator/factory class (`ExecutableTypesProcess`)
- [ ] Use primary constructors for simple dependency injection

### 4. Eliminate Duplication
- [ ] Look for similar methods that can delegate to each other
- [ ] Extract common logic to a single method
- [ ] Make one method the "source of truth"

### 5. Update Tests
- [ ] Add tests for each strategy independently
- [ ] Add tests for the coordinator
- [ ] Ensure existing tests still pass

### 6. Documentation
- [ ] Add XML documentation to interfaces
- [ ] Document the "why" in class-level comments
- [ ] Update any architecture documentation

---

## When to Apply These Patterns

### Apply Strategy Pattern when:
- ✅ You have `if/else` or `switch` statements based on type
- ✅ The behavior difference is substantial (not just values)
- ✅ You expect to add more types in the future
- ✅ Each type needs different dependencies or initialization

### Don't apply Strategy Pattern when:
- ❌ There are only 2 simple cases that won't grow
- ❌ The difference is just configuration values
- ❌ The conditional logic is trivial (single line)

### Apply DRY (extract common method) when:
- ✅ Two or more methods have duplicate logic
- ✅ Changes to logic need to be consistent across methods
- ✅ One method is logically a subset of another

### Apply Extension Methods when:
- ✅ Logic is domain-specific and reusable
- ✅ You want to enhance domain objects without modifying them
- ✅ The operation is stateless or only depends on the object's data

---

## Example Workflow for AI Agent

When asked to "fix the script file detection issue":

1. **Initial Implementation** (get it working)
   - Add inline conditionals to check file extension
   - Get tests passing

2. **Refactoring Phase** (make it clean)
   - Extract type detection to extension method
   - Create enum if multiple types exist
   - Extract strategy pattern if complexity warrants it
   - Eliminate duplication
   - Update documentation

3. **Validation**
   - Run all tests
   - Check for code smells
   - Ensure SOLID principles are followed
   - Verify no performance regressions

---

## Key Principles

1. **Make it work, then make it right**: First implement the feature, then refactor
2. **SOLID over clever**: Prefer clear, maintainable code over clever one-liners
3. **Test-driven refactoring**: Ensure tests pass after each refactoring step
4. **Boy Scout Rule**: Leave the code better than you found it
5. **Domain-Driven Design**: Use domain language in code (e.g., `IsAScriptFile()`)

---

## Anti-Patterns to Avoid

### ❌ God Object
```csharp
// Don't put all logic in one massive class
public class ProcessManager
{
    // 500 lines of mixed responsibilities
}
```

### ❌ Primitive Obsession
```csharp
// Don't scatter magic strings everywhere
if (extension == ".cmd" || extension == ".bat" || extension == ".ps1")
```

### ❌ Duplicate Code
```csharp
// Don't repeat the same logic
public void Method1() { /* logic */ }
public void Method2() { /* same logic */ }
```

### ❌ Long Parameter List
```csharp
// Don't pass too many parameters
public void Process(string path, string args, bool isScript, string interpreter, ...)
```

---

## Summary

When refactoring code:
1. **Identify patterns** in the existing conditional logic
2. **Extract domain knowledge** to extension methods
3. **Apply Strategy Pattern** for type-based polymorphism
4. **Eliminate duplication** by having methods delegate to each other
5. **Use modern C# features** like primary constructors
6. **Maintain comprehensive tests** throughout the process

The goal is to create code that is **easy to understand, easy to test, and easy to extend**.
