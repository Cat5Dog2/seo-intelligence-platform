using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeoIntelligence.Infrastructure.RakkoKeyword.Generated;

namespace ContractTests;

/// <summary>
/// 生成DTOの形状(プロパティ名・required制約・型・null許容性)をvendor OpenAPIスキーマと再帰的に照合する。
/// ルール:
/// 1. DTOに存在するプロパティは、対応するスキーマにも存在しなければならない(削除・改名の検知)。
/// 2. スキーマでrequiredのプロパティは、DTOにも存在しなければならない(必須項目欠落の検知)。
/// 3. スキーマのtype(type配列/anyOf/enum/const/items/additionalProperties含む)とCLR型は互換でなければならない。
///    CLR整数型はスキーマがintegerまたは全値が整数のenum/constの場合のみ許容する(小数混入の検知)。
/// 4. スキーマがnullを許容するプロパティは、CLR側(値型のNullable/参照型のnull注釈)もnull許容でなければならない。
/// 5. スキーマ上optionalのプロパティはDTOで省略できる(アプリが使わない項目の意図的な省略を許容)。
/// </summary>
public sealed class RakkoKeywordDtoShapeContractTests
{
    private const string GeneratedNamespace = "SeoIntelligence.Infrastructure.RakkoKeyword.Generated";

    [Fact]
    [Trait("Category", "Contract")]
    public async Task GeneratedDtoShapesMatchVendorOpenApiSchemas()
    {
        var specPath = Path.Combine(GetRepositoryRoot(), RakkoKeywordOpenApiMetadata.SourcePath);
        await using var specStream = File.OpenRead(specPath);
        using var document = await JsonDocument.ParseAsync(specStream);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var assembly = typeof(RakkoKeywordOpenApiMetadata).Assembly;
        var schemaNames = RakkoKeywordOpenApiMetadata.MvpSchemaNames
            .Concat(RakkoKeywordPhase2OpenApiMetadata.SchemaNames)
            .ToArray();
        var validator = new ShapeValidator(schemas);

        foreach (var schemaName in schemaNames)
        {
            var dtoType = assembly.GetType($"{GeneratedNamespace}.{schemaName}");
            if (dtoType is null)
            {
                validator.Failures.Add($"{schemaName}: generated DTO type was not found in {GeneratedNamespace}.");
                continue;
            }

            validator.Validate(schemas.GetProperty(schemaName), dtoType, schemaName);
        }

        Assert.True(validator.Failures.Count == 0, string.Join(Environment.NewLine, validator.Failures));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsScalarTypeChange()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"value":{"type":"number"}},"required":["value"]}""",
            typeof(NullableStringValueDto));

        Assert.Contains(failures, failure => failure.Contains(".value", StringComparison.Ordinal) && failure.Contains("incompatible", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsObjectToArrayChange()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"data":{"type":"array","items":{"type":"object","properties":{"x":{"type":"string"}}}}}}""",
            typeof(ObjectDataDto));

        Assert.Contains(failures, failure => failure.Contains(".data", StringComparison.Ordinal) && failure.Contains("incompatible", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsMissingNullabilityOnValueType()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"value":{"anyOf":[{"type":"number"},{"type":"null"}]}},"required":["value"]}""",
            typeof(NonNullableDecimalDto));

        Assert.Contains(failures, failure => failure.Contains(".value", StringComparison.Ordinal) && failure.Contains("non-nullable", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsMissingNullabilityOnReferenceType()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"value":{"anyOf":[{"type":"string"},{"type":"null"}]}},"required":["value"]}""",
            typeof(NonNullableStringDto));

        Assert.Contains(failures, failure => failure.Contains(".value", StringComparison.Ordinal) && failure.Contains("non-nullable", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsNumberSchemaForIntegerClrType()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"value":{"type":"number"}},"required":["value"]}""",
            typeof(IntValueDto));

        Assert.Contains(failures, failure => failure.Contains(".value", StringComparison.Ordinal) && failure.Contains("incompatible", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsFractionalEnumForIntegerClrType()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"value":{"type":"number","enum":[1.5,3]}},"required":["value"]}""",
            typeof(IntValueDto));

        Assert.Contains(failures, failure => failure.Contains(".value", StringComparison.Ordinal) && failure.Contains("incompatible", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorAllowsIntegralEnumForIntegerClrType()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"value":{"type":"number","enum":[30,40,50]}},"required":["value"]}""",
            typeof(IntValueDto));

        Assert.Empty(failures);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ValidatorDetectsDictionaryValueTypeChange()
    {
        var failures = ValidateFakeSchema(
            """{"type":"object","properties":{"values":{"type":"object","propertyNames":{"type":"string"},"additionalProperties":{"type":"number"}}}}""",
            typeof(StringDictionaryDto));

        Assert.Contains(failures, failure => failure.Contains(".values[*]", StringComparison.Ordinal) && failure.Contains("incompatible", StringComparison.Ordinal));
    }

    private static List<string> ValidateFakeSchema(string schemaJson, Type dtoType)
    {
        using var document = JsonDocument.Parse($$"""{"Root":{{schemaJson}}}""");
        var validator = new ShapeValidator(document.RootElement);
        validator.Validate(document.RootElement.GetProperty("Root"), dtoType, "Root");
        return validator.Failures;
    }

    private sealed class NullableStringValueDto
    {
        [JsonPropertyName("value")]
        public string? Value { get; init; }
    }

    private sealed class NonNullableStringDto
    {
        [JsonPropertyName("value")]
        public string Value { get; init; } = string.Empty;
    }

    private sealed class ObjectDataDto
    {
        [JsonPropertyName("data")]
        public NestedDto? Data { get; init; }
    }

    private sealed class NestedDto
    {
        [JsonPropertyName("x")]
        public string? X { get; init; }
    }

    private sealed class NonNullableDecimalDto
    {
        [JsonPropertyName("value")]
        public decimal Value { get; init; }
    }

    private sealed class IntValueDto
    {
        [JsonPropertyName("value")]
        public int Value { get; init; }
    }

    private sealed class StringDictionaryDto
    {
        [JsonPropertyName("values")]
        public Dictionary<string, string> Values { get; init; } = [];
    }

    private enum ClrKind
    {
        String,
        Boolean,
        Integer,
        Number,
        Object,
        Array
    }

    private sealed class ShapeValidator(JsonElement schemas)
    {
        private readonly NullabilityInfoContext nullabilityContext = new();

        public List<string> Failures { get; } = [];

        public void Validate(JsonElement rawSchema, Type clrType, string path)
            => Validate(rawSchema, clrType, nullability: null, path);

        private void Validate(JsonElement rawSchema, Type clrType, NullabilityInfo? nullability, string path)
        {
            var (schema, schemaAllowsNull) = Resolve(rawSchema);
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (underlying == typeof(JsonElement) || underlying == typeof(object))
            {
                return;
            }

            if (schemaAllowsNull && !ClrAllowsNull(clrType, nullability))
            {
                Failures.Add($"{path}: schema allows null but CLR type {clrType.Name} is declared non-nullable.");
            }

            var schemaKinds = GetSchemaKinds(schema);
            var clrKind = GetClrKind(underlying);
            if (schemaKinds.Count > 0 && clrKind is not null &&
                !schemaKinds.Overlaps(CompatibleSchemaKinds(clrKind.Value)))
            {
                Failures.Add($"{path}: schema type [{string.Join(",", schemaKinds)}] is incompatible with CLR type {underlying.Name}.");
                return;
            }

            if (IsDictionary(underlying))
            {
                var valueType = underlying.GetGenericArguments()[1];
                var valueNullability = nullability?.GenericTypeArguments.Length == 2
                    ? nullability.GenericTypeArguments[1]
                    : null;
                if (schema.ValueKind == JsonValueKind.Object &&
                    schema.TryGetProperty("additionalProperties", out var additionalProperties) &&
                    additionalProperties.ValueKind == JsonValueKind.Object)
                {
                    Validate(additionalProperties, valueType, valueNullability, $"{path}[*]");
                }
                else if (schema.ValueKind == JsonValueKind.Object &&
                    schema.TryGetProperty("properties", out var fixedProperties))
                {
                    foreach (var property in fixedProperties.EnumerateObject())
                    {
                        Validate(property.Value, valueType, valueNullability, $"{path}.{property.Name}");
                    }
                }

                return;
            }

            var elementType = GetEnumerableElementType(underlying);
            if (elementType is not null)
            {
                if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("items", out var items))
                {
                    var elementNullability = underlying.IsArray
                        ? nullability?.ElementType
                        : nullability?.GenericTypeArguments.FirstOrDefault();
                    Validate(items, elementType, elementNullability, $"{path}[]");
                }

                return;
            }

            if (IsScalar(underlying))
            {
                return;
            }

            if (schema.ValueKind != JsonValueKind.Object ||
                !schema.TryGetProperty("properties", out var schemaProperties))
            {
                return;
            }

            var clrProperties = underlying
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Property: property, Name: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                .Where(pair => pair.Name is not null)
                .ToDictionary(pair => pair.Name!, pair => pair.Property, StringComparer.Ordinal);

            foreach (var (jsonName, property) in clrProperties)
            {
                if (!schemaProperties.TryGetProperty(jsonName, out var propertySchema))
                {
                    Failures.Add($"{path}.{jsonName}: exists on {underlying.Name} but is missing from the vendor schema.");
                    continue;
                }

                Validate(propertySchema, property.PropertyType, nullabilityContext.Create(property), $"{path}.{jsonName}");
            }

            if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            {
                foreach (var requiredName in required.EnumerateArray())
                {
                    var name = requiredName.GetString();
                    if (name is not null && !clrProperties.ContainsKey(name))
                    {
                        Failures.Add($"{path}.{name}: required by the vendor schema but missing on {underlying.Name}.");
                    }
                }
            }
        }

        private static bool ClrAllowsNull(Type type, NullabilityInfo? nullability)
        {
            if (type.IsValueType)
            {
                return Nullable.GetUnderlyingType(type) is not null;
            }

            // 参照型はnull注釈(NRT)で判定する。注釈情報がない場合は許容する。
            return nullability is null || nullability.ReadState != NullabilityState.NotNull;
        }

        private (JsonElement Schema, bool AllowsNull) Resolve(JsonElement schema)
        {
            var allowsNull = false;
            while (schema.ValueKind == JsonValueKind.Object)
            {
                if (schema.TryGetProperty("$ref", out var reference))
                {
                    var referenceName = reference.GetString()!.Split('/')[^1];
                    schema = schemas.GetProperty(referenceName);
                    continue;
                }

                if (schema.TryGetProperty("nullable", out var nullable) && nullable.ValueKind == JsonValueKind.True)
                {
                    allowsNull = true;
                }

                if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
                {
                    var members = anyOf.EnumerateArray().ToArray();
                    var nonNull = members.Where(member => !IsNullType(member)).ToArray();
                    if (nonNull.Length < members.Length)
                    {
                        allowsNull = true;
                    }

                    if (nonNull.Length == 1)
                    {
                        schema = nonNull[0];
                        continue;
                    }
                }

                if (schema.TryGetProperty("type", out var typeElement) &&
                    typeElement.ValueKind == JsonValueKind.Array &&
                    typeElement.EnumerateArray().Any(entry => entry.GetString() == "null"))
                {
                    allowsNull = true;
                }

                break;
            }

            return (schema, allowsNull);
        }

        private HashSet<string> GetSchemaKinds(JsonElement schema)
        {
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            if (schema.ValueKind != JsonValueKind.Object)
            {
                return kinds;
            }

            if (schema.TryGetProperty("type", out var typeElement))
            {
                if (typeElement.ValueKind == JsonValueKind.String)
                {
                    AddKind(kinds, typeElement.GetString());
                }
                else if (typeElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in typeElement.EnumerateArray())
                    {
                        AddKind(kinds, entry.GetString());
                    }
                }

                // type: numberでも、enum/constの全値が整数ならintegerとして扱う
                // (小数が混ざる場合はnumberのまま、CLR整数型を不適合にする)。
                if (kinds.Contains("number") && TryClassifyNumericValues(schema, out var integralOnly))
                {
                    kinds.Remove("number");
                    kinds.Add(integralOnly ? "integer" : "number");
                }

                return kinds;
            }

            if (schema.TryGetProperty("properties", out _) || schema.TryGetProperty("additionalProperties", out _))
            {
                kinds.Add("object");
                return kinds;
            }

            if (schema.TryGetProperty("items", out _))
            {
                kinds.Add("array");
                return kinds;
            }

            if (schema.TryGetProperty("enum", out _) || schema.TryGetProperty("const", out _))
            {
                var sawFractionalNumber = false;
                var sawIntegralNumber = false;
                foreach (var value in EnumerateEnumAndConstValues(schema))
                {
                    if (value.ValueKind == JsonValueKind.Number)
                    {
                        if (value.TryGetInt64(out _))
                        {
                            sawIntegralNumber = true;
                        }
                        else
                        {
                            sawFractionalNumber = true;
                        }
                    }
                    else
                    {
                        AddKind(kinds, KindOfValue(value));
                    }
                }

                if (sawFractionalNumber)
                {
                    kinds.Add("number");
                }
                else if (sawIntegralNumber)
                {
                    kinds.Add("integer");
                }

                return kinds;
            }

            if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
            {
                foreach (var member in anyOf.EnumerateArray())
                {
                    var (resolved, _) = Resolve(member);
                    kinds.UnionWith(GetSchemaKinds(resolved));
                }

                // 整数と小数が混在するanyOfは、CLR整数型を許容しないためnumberへ寄せる。
                if (kinds.Contains("number"))
                {
                    kinds.Remove("integer");
                }
            }

            return kinds;
        }

        private static bool TryClassifyNumericValues(JsonElement schema, out bool integralOnly)
        {
            integralOnly = true;
            var found = false;
            foreach (var value in EnumerateEnumAndConstValues(schema))
            {
                if (value.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                found = true;
                if (!value.TryGetInt64(out _))
                {
                    integralOnly = false;
                }
            }

            return found;
        }

        private static IEnumerable<JsonElement> EnumerateEnumAndConstValues(JsonElement schema)
        {
            if (schema.TryGetProperty("enum", out var enumElement) && enumElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in enumElement.EnumerateArray())
                {
                    yield return entry;
                }
            }

            if (schema.TryGetProperty("const", out var constElement))
            {
                yield return constElement;
            }
        }

        private static void AddKind(HashSet<string> kinds, string? kind)
        {
            if (kind is not null && kind != "null")
            {
                kinds.Add(kind);
            }
        }

        private static string? KindOfValue(JsonElement value)
            => value.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                _ => null
            };

        private static ClrKind? GetClrKind(Type type)
        {
            if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) ||
                type.IsEnum)
            {
                return ClrKind.String;
            }

            if (type == typeof(bool))
            {
                return ClrKind.Boolean;
            }

            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort))
            {
                return ClrKind.Integer;
            }

            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            {
                return ClrKind.Number;
            }

            if (IsDictionary(type))
            {
                return ClrKind.Object;
            }

            if (type.IsArray || GetEnumerableElementType(type) is not null)
            {
                return ClrKind.Array;
            }

            return type.IsClass ? ClrKind.Object : null;
        }

        private static HashSet<string> CompatibleSchemaKinds(ClrKind kind)
            => kind switch
            {
                ClrKind.String => new HashSet<string>(StringComparer.Ordinal) { "string" },
                ClrKind.Boolean => new HashSet<string>(StringComparer.Ordinal) { "boolean" },
                // OpenAPIのnumberは小数を含み得るため、CLR整数型はintegerのみ許容する。
                ClrKind.Integer => new HashSet<string>(StringComparer.Ordinal) { "integer" },
                ClrKind.Number => new HashSet<string>(StringComparer.Ordinal) { "number", "integer" },
                ClrKind.Object => new HashSet<string>(StringComparer.Ordinal) { "object" },
                ClrKind.Array => new HashSet<string>(StringComparer.Ordinal) { "array" },
                _ => []
            };

        private static bool IsNullType(JsonElement schema)
            => schema.ValueKind == JsonValueKind.Object &&
                schema.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "null";

        private static bool IsScalar(Type type)
            => type == typeof(string)
                || type.IsPrimitive
                || type.IsEnum
                || type == typeof(decimal)
                || type == typeof(Guid)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(DateOnly);

        private static bool IsDictionary(Type type)
            => type.IsGenericType &&
                (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                    type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) ||
                    type.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        private static Type? GetEnumerableElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type == typeof(string) || IsDictionary(type))
            {
                return null;
            }

            return new[] { type }.Concat(type.GetInterfaces())
                .Where(candidate => candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(candidate => candidate.GetGenericArguments()[0])
                .FirstOrDefault();
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeoIntelligence.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
