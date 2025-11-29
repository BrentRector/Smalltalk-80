using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase4;

/// <summary>
/// Creates and populates ClassOrganizer objects for all classes during cold start.
/// ClassOrganizer stores method categories for browser display.
///
/// ClassOrganizer instance variables:
///   [0] globalComment    - String (unused)
///   [1] categoryArray    - Array of category name Symbols
///   [2] categoryStops    - Array of SmallInteger stop indices
///   [3] elementArray     - Array of selector Symbols
/// </summary>
internal class ClassOrganizationInstaller(
    ColdStartBundle bundle,
    ObjectGraphAllocator allocator,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    SymbolService symbolService,
    IDiagnosticHub? diagnostics = null)
{
    private readonly ColdStartBundle _bundle = bundle;
    private readonly ObjectGraphAllocator _allocator = allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId = localIdToObjectId;
    private readonly SymbolService _symbolService = symbolService;
    private readonly IDiagnosticHub? _diagnostics = diagnostics;

    // ClassOrganizer field indices
    private const int CLASSORGANIZER_GLOBALCOMMENT_INDEX = 0;
    private const int CLASSORGANIZER_CATEGORYARRAY_INDEX = 1;
    private const int CLASSORGANIZER_CATEGORYSTOPS_INDEX = 2;
    private const int CLASSORGANIZER_ELEMENTARRAY_INDEX = 3;

    // Class/Metaclass organization field index
    private const int CLASS_ORGANIZATION_INDEX = 5;

    /// <summary>
    /// Creates and installs ClassOrganizer objects for all classes.
    /// </summary>
    public void InstallOrganizations()
    {
        // Group methods by class and category
        var methodsByClass = new Dictionary<string, Dictionary<string, List<string>>>();

        foreach (var method in _bundle.Methods)
        {
            // Skip blocks (they don't have a home class or valid selector index)
            if (string.IsNullOrEmpty(method.HomeClassLocalId) || method.SelectorIndex < 0)
            {
                continue;
            }

            string classLocalId = method.HomeClassLocalId;
            string category = method.Category ?? "as yet unclassified";
            string selector = _bundle.Symbols[method.SelectorIndex];

            if (!methodsByClass.TryGetValue(classLocalId, out var categories))
            {
                categories = new Dictionary<string, List<string>>();
                methodsByClass[classLocalId] = categories;
            }

            if (!categories.TryGetValue(category, out var selectors))
            {
                selectors = new List<string>();
                categories[category] = selectors;
            }

            selectors.Add(selector);
        }

        // Create ClassOrganizer for each class
        int organizersCreated = 0;

        foreach (var kvp in methodsByClass)
        {
            string classLocalId = kvp.Key;
            var categories = kvp.Value;

            if (!_localIdToObjectId.TryGetValue(classLocalId, out SmalltalkOOP classOOP))
            {
                _diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Warning,
                    Category = "Phase4",
                    Message = $"Class not found for organization: {classLocalId}",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
                continue;
            }

            var organizerOOP = CreateClassOrganizer(categories);
            _allocator.SetInstanceVariable(classOOP, CLASS_ORGANIZATION_INDEX, organizerOOP);
            organizersCreated++;
        }

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase4",
            Message = $"Created {organizersCreated} ClassOrganizer objects",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }

    /// <summary>
    /// Creates a ClassOrganizer object from category data.
    /// </summary>
    private SmalltalkOOP CreateClassOrganizer(Dictionary<string, List<string>> categories)
    {
        // Sort categories alphabetically
        var sortedCategories = categories.Keys.OrderBy(c => c).ToList();

        // Build arrays
        var categoryNames = new List<string>();
        var categoryStops = new List<int>();
        var allSelectors = new List<string>();

        int currentStop = 0;
        foreach (var categoryName in sortedCategories)
        {
            var selectors = categories[categoryName];
            selectors.Sort(); // Sort selectors alphabetically

            categoryNames.Add(categoryName);
            allSelectors.AddRange(selectors);
            currentStop += selectors.Count;
            categoryStops.Add(currentStop);
        }

        // Allocate ClassOrganizer object
        var classOrganizerClassOOP = GlobalRegistry.LookupClassOOP("ClassOrganizer");
        var organizerOOP = _allocator.AllocateInstanceOf(classOrganizerClassOOP, 0);

        // Field [0]: globalComment - nil
        _allocator.SetInstanceVariable(organizerOOP, CLASSORGANIZER_GLOBALCOMMENT_INDEX, _allocator.GetNil());

        // Field [1]: categoryArray - Array of category name Symbols
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var categoryArrayOOP = _allocator.AllocateArray(arrayClassOOP, categoryNames.Count);
        for (int i = 0; i < categoryNames.Count; i++)
        {
            var symbolOOP = _symbolService.GetSymbolOOP(categoryNames[i]);
            _allocator.SetIndexedOOP(categoryArrayOOP, i, symbolOOP);
        }
        _allocator.SetInstanceVariable(organizerOOP, CLASSORGANIZER_CATEGORYARRAY_INDEX, categoryArrayOOP);

        // Field [2]: categoryStops - Array of SmallInteger stop indices
        var stopsArrayOOP = _allocator.AllocateArray(arrayClassOOP, categoryStops.Count);
        for (int i = 0; i < categoryStops.Count; i++)
        {
            _allocator.SetIndexedOOP(stopsArrayOOP, i, SmalltalkOOP.FromSmallInteger(categoryStops[i]));
        }
        _allocator.SetInstanceVariable(organizerOOP, CLASSORGANIZER_CATEGORYSTOPS_INDEX, stopsArrayOOP);

        // Field [3]: elementArray - Array of selector Symbols
        var elementArrayOOP = _allocator.AllocateArray(arrayClassOOP, allSelectors.Count);
        for (int i = 0; i < allSelectors.Count; i++)
        {
            var symbolOOP = _symbolService.GetSymbolOOP(allSelectors[i]);
            _allocator.SetIndexedOOP(elementArrayOOP, i, symbolOOP);
        }
        _allocator.SetInstanceVariable(organizerOOP, CLASSORGANIZER_ELEMENTARRAY_INDEX, elementArrayOOP);

        return organizerOOP;
    }
}
