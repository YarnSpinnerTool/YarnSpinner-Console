namespace YarnSpinnerConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Yarn.Compiler;

    public static class DefinitionsReader
    {
        private static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        public static List<Declaration> ReadFunctionDeclarations(IEnumerable<string> definitionsFiles)
        {
            var declarations = new List<Declaration>();
            var declaredNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var seenFiles = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in definitionsFiles)
            {
                var fullPath = Path.GetFullPath(file);
                if (!seenFiles.Add(fullPath))
                {
                    continue;
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
                {
                    Warn($"Couldn't read definitions file {fullPath}: {e.Message}");
                    continue;
                }

                using (document)
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        Warn($"Definitions file {fullPath} doesn't contain a JSON object, so it was skipped.");
                        continue;
                    }

                    if (!TryGetProperty(document.RootElement, "functions", out var functions) || functions.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (functions.ValueKind != JsonValueKind.Array)
                    {
                        Warn($"\"functions\" in {fullPath} isn't a list, so its functions were skipped.");
                        continue;
                    }

                    foreach (var function in functions.EnumerateArray())
                    {
                        var declaration = ReadFunction(function, fullPath);
                        if (declaration == null)
                        {
                            continue;
                        }

                        if (declaredNames.TryGetValue(declaration.Name, out var firstFile))
                        {
                            Warn($"Function {declaration.Name} in {fullPath} is already declared in {firstFile}, so the first declaration is used.");
                            continue;
                        }

                        declaredNames[declaration.Name] = fullPath;
                        declarations.Add(declaration);
                    }
                }
            }

            return declarations;
        }

        private static Declaration ReadFunction(JsonElement function, string file)
        {
            if (function.ValueKind != JsonValueKind.Object)
            {
                Warn($"A function entry in {file} isn't a JSON object, so it was skipped.");
                return null;
            }

            var name = GetString(function, "yarnName");
            if (string.IsNullOrWhiteSpace(name))
            {
                Warn($"A function in {file} has no yarnName, so it was skipped.");
                return null;
            }

            var returnTypeName = ReadReturnTypeName(function);
            if (string.IsNullOrWhiteSpace(returnTypeName))
            {
                Warn($"Function {name} in {file} has no return type, so its type will be worked out from how it's used.");
                return null;
            }

            var returnType = MapType(returnTypeName);
            if (returnType == null)
            {
                Warn($"Function {name} in {file} returns \"{returnTypeName}\", which ysc can't declare, so its type will be worked out from how it's used.");
                return null;
            }

            var typeBuilder = new FunctionTypeBuilder().WithReturnType(returnType);

            if (TryGetProperty(function, "parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
            {
                var parameterList = parameters.EnumerateArray().ToList();
                for (var i = 0; i < parameterList.Count; i++)
                {
                    var parameter = parameterList[i];
                    if (parameter.ValueKind != JsonValueKind.Object)
                    {
                        Warn($"A parameter of function {name} in {file} isn't a JSON object, so the function's type will be worked out from how it's used.");
                        return null;
                    }

                    if (TryGetProperty(parameter, "defaultValue", out var defaultValue) && defaultValue.ValueKind != JsonValueKind.Null)
                    {
                        return null;
                    }

                    var parameterType = MapType(GetString(parameter, "type")) ?? Yarn.Types.Any;
                    var isVariadic = GetBool(parameter, "isParamsArray");

                    if (isVariadic && i == parameterList.Count - 1)
                    {
                        typeBuilder = typeBuilder.WithVariadicParameterType(parameterType);
                    }
                    else
                    {
                        typeBuilder = typeBuilder.WithParameter(parameterType);
                    }
                }
            }

            var builder = new DeclarationBuilder()
                .WithName(name)
                .WithType(typeBuilder.FunctionType);

            var documentation = GetString(function, "documentation");
            if (!string.IsNullOrEmpty(documentation))
            {
                builder = builder.WithDescription(documentation);
            }

            return builder.Declaration;
        }

        private static string ReadReturnTypeName(JsonElement function)
        {
            if (TryGetProperty(function, "return", out var returnElement))
            {
                if (returnElement.ValueKind == JsonValueKind.Object)
                {
                    return GetString(returnElement, "type");
                }
                if (returnElement.ValueKind == JsonValueKind.String)
                {
                    return returnElement.GetString();
                }
            }
            return GetString(function, "returnType");
        }

        private static Yarn.IType MapType(string typeName)
        {
            switch (typeName?.Trim().ToLowerInvariant())
            {
                case "string":
                    return Yarn.Types.String;
                case "number":
                    return Yarn.Types.Number;
                case "bool":
                case "boolean":
                    return Yarn.Types.Boolean;
                case "any":
                    return Yarn.Types.Any;
                default:
                    return null;
            }
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(name, out value))
                {
                    return true;
                }
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        private static string GetString(JsonElement element, string name)
        {
            return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static bool GetBool(JsonElement element, string name)
        {
            return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.True;
        }

        private static void Warn(string text)
        {
            Console.ResetColor();
            Console.Error.Write("⚠️ WARNING: ");
            Console.Error.WriteLine(text);
        }
    }
}
