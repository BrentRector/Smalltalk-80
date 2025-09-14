using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Smalltalk80.ExtractClass
{
    /// <summary>
    /// Simple extractor that parses Smalltalk source files to extract class and method information.
    /// </summary>
    public class SimpleClassExtractor
    {
        private readonly string systemImagePath;
        private readonly Dictionary<string, ClassInfo> classes = new();
        private readonly Dictionary<string, List<MethodInfo>> classMethods = new();
        
        public SimpleClassExtractor(string systemImagePath)
        {
            this.systemImagePath = systemImagePath;
        }
        
        /// <summary>
        /// Parse the system image to extract class definitions.
        /// </summary>
        public void ParseSystemImage()
        {
            if (!File.Exists(systemImagePath))
            {
                throw new FileNotFoundException($"System image not found: {systemImagePath}");
            }
            
            var content = File.ReadAllText(systemImagePath);
            
            // Parse class definitions
            var classPattern = @"(\w+)\s+subclass:\s+#(\w+)\s+" +
                              @"instanceVariableNames:\s+'([^']*)'\s+" +
                              @"classVariableNames:\s+'([^']*)'\s+" +
                              @"poolDictionaries:\s+'([^']*)'\s+" +
                              @"category:\s+'([^']*)'";
            
            var classMatches = Regex.Matches(content, classPattern, RegexOptions.Multiline);
            
            foreach (Match match in classMatches)
            {
                var classInfo = new ClassInfo
                {
                    Superclass = match.Groups[1].Value,
                    Name = match.Groups[2].Value,
                    InstanceVariables = ParseVariableNames(match.Groups[3].Value),
                    ClassVariables = ParseVariableNames(match.Groups[4].Value),
                    PoolDictionaries = ParseVariableNames(match.Groups[5].Value),
                    Category = match.Groups[6].Value
                };
                
                classes[classInfo.Name] = classInfo;
                classMethods[classInfo.Name] = new List<MethodInfo>();
            }
        }
        
        /// <summary>
        /// Get class information by name.
        /// </summary>
        public ClassInfo? GetClass(string className)
        {
            return classes.TryGetValue(className, out var classInfo) ? classInfo : null;
        }
        
        /// <summary>
        /// Extract methods for a specific class from the system image.
        /// </summary>
        public List<MethodInfo> ExtractMethodsForClass(string systemImagePath, string className)
        {
            var methods = new List<MethodInfo>();
            var content = File.ReadAllText(systemImagePath);
            
            // Pattern for instance methods
            var instanceMethodPattern = $@"!{Regex.Escape(className)}\s+methodsFor:\s+'([^']*)'!(.*?)! !";
            var instanceMatches = Regex.Matches(content, instanceMethodPattern, RegexOptions.Singleline);
            
            foreach (Match match in instanceMatches)
            {
                var category = match.Groups[1].Value;
                var methodsBlock = match.Groups[2].Value;
                
                // Split methods by ! delimiter
                var methodSources = Regex.Split(methodsBlock, @"(?<=!)\s*\n(?=\w)");
                
                foreach (var methodSource in methodSources.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    var trimmedSource = methodSource.Trim();
                    if (trimmedSource.EndsWith("!"))
                    {
                        trimmedSource = trimmedSource.Substring(0, trimmedSource.Length - 1).Trim();
                    }
                    
                    if (!string.IsNullOrWhiteSpace(trimmedSource))
                    {
                        var selector = ExtractSelector(trimmedSource);
                        methods.Add(new MethodInfo
                        {
                            Selector = selector,
                            Source = trimmedSource,
                            IsClassMethod = false,
                            Category = category
                        });
                    }
                }
            }
            
            // Pattern for class methods
            var classMethodPattern = $@"!{Regex.Escape(className)}\s+class\s+methodsFor:\s+'([^']*)'!(.*?)! !";
            var classMatches = Regex.Matches(content, classMethodPattern, RegexOptions.Singleline);
            
            foreach (Match match in classMatches)
            {
                var category = match.Groups[1].Value;
                var methodsBlock = match.Groups[2].Value;
                
                // Split methods by ! delimiter
                var methodSources = Regex.Split(methodsBlock, @"(?<=!)\s*\n(?=\w)");
                
                foreach (var methodSource in methodSources.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    var trimmedSource = methodSource.Trim();
                    if (trimmedSource.EndsWith("!"))
                    {
                        trimmedSource = trimmedSource.Substring(0, trimmedSource.Length - 1).Trim();
                    }
                    
                    if (!string.IsNullOrWhiteSpace(trimmedSource))
                    {
                        var selector = ExtractSelector(trimmedSource);
                        methods.Add(new MethodInfo
                        {
                            Selector = selector,
                            Source = trimmedSource,
                            IsClassMethod = true,
                            Category = category
                        });
                    }
                }
            }
            
            return methods;
        }
        
        /// <summary>
        /// Parse variable names from a space-separated string.
        /// </summary>
        private List<string> ParseVariableNames(string varString)
        {
            if (string.IsNullOrWhiteSpace(varString))
                return new List<string>();
            
            return varString.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        
        /// <summary>
        /// Extract the selector from method source.
        /// </summary>
        private string ExtractSelector(string methodSource)
        {
            // Handle unary selectors (simple identifier)
            var unaryMatch = Regex.Match(methodSource, @"^(\w+)\s*(\n|$|\|)");
            if (unaryMatch.Success)
            {
                return unaryMatch.Groups[1].Value;
            }
            
            // Handle binary selectors
            var binaryMatch = Regex.Match(methodSource, @"^([+\-*/\\<>=~@%&|,]+)\s+\w+");
            if (binaryMatch.Success)
            {
                return binaryMatch.Groups[1].Value;
            }
            
            // Handle keyword selectors
            var keywordParts = new List<string>();
            var keywordPattern = @"(\w+:)";
            var keywordMatches = Regex.Matches(methodSource.Split('\n')[0], keywordPattern);
            
            foreach (Match match in keywordMatches)
            {
                keywordParts.Add(match.Groups[1].Value);
            }
            
            if (keywordParts.Count > 0)
            {
                return string.Join("", keywordParts);
            }
            
            // Default: take first word
            var firstWord = methodSource.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return firstWord ?? "unknown";
        }
    }
}