using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SeoIntelligence.Infrastructure.Services;

internal static class TabularDataFile
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace ExtendedProperties = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
    private static readonly XNamespace CoreProperties = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";

    public static ImportedTable ReadCsv(string content)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRow.Add(current.ToString());
                    current.Clear();
                    break;
                case '\r':
                    if (index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        index++;
                    }

                    AddCsvRow(rows, currentRow, current);
                    break;
                case '\n':
                    AddCsvRow(rows, currentRow, current);
                    break;
                default:
                    current.Append(character);
                    break;
            }
        }

        AddCsvRow(rows, currentRow, current);
        return ToImportedTable(rows);
    }

    public static ImportedTable ReadXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var worksheetEntry = ResolveFirstWorksheetEntry(archive);
        if (worksheetEntry is null)
        {
            return new ImportedTable([], []);
        }

        using var worksheetStream = worksheetEntry.Open();
        var document = XDocument.Load(worksheetStream);
        var rows = new List<(int RowNumber, string[] Values)>();
        foreach (var row in document.Descendants(Spreadsheet + "row"))
        {
            var rowNumber = ReadInt(row.Attribute("r")?.Value) ?? rows.Count + 1;
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                var columnIndex = ReadColumnIndex(cell.Attribute("r")?.Value);
                if (!columnIndex.HasValue)
                {
                    columnIndex = values.Count + 1;
                }

                values[columnIndex.Value] = ReadCellValue(cell, sharedStrings);
            }

            if (values.Count == 0)
            {
                continue;
            }

            var width = values.Keys.Max();
            var rowValues = new string[width];
            foreach (var value in values)
            {
                rowValues[value.Key - 1] = value.Value;
            }

            rows.Add((rowNumber, rowValues));
        }

        return ToImportedTable(rows);
    }

    public static byte[] WriteXlsx(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        using var content = new MemoryStream();
        using (var archive = new ZipArchive(content, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", CreateContentTypesDocument());
            WriteEntry(archive, "_rels/.rels", CreateRootRelationshipsDocument());
            WriteEntry(archive, "docProps/app.xml", CreateAppPropertiesDocument());
            WriteEntry(archive, "docProps/core.xml", CreateCorePropertiesDocument());
            WriteEntry(archive, "xl/workbook.xml", CreateWorkbookDocument());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationshipsDocument());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", CreateWorksheetDocument(columns, rows));
        }

        return content.ToArray();
    }

    private static void AddCsvRow(List<string[]> rows, List<string> currentRow, StringBuilder current)
    {
        currentRow.Add(current.ToString());
        current.Clear();

        if (currentRow.Count == 1 && currentRow[0].Length == 0 && rows.Count == 0)
        {
            currentRow.Clear();
            return;
        }

        rows.Add(currentRow.ToArray());
        currentRow.Clear();
    }

    private static ImportedTable ToImportedTable(IReadOnlyList<string[]> rawRows)
    {
        var rows = rawRows
            .Select((values, index) => (RowNumber: index + 1, Values: values))
            .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToArray();
        return ToImportedTable(rows);
    }

    private static ImportedTable ToImportedTable(IReadOnlyList<(int RowNumber, string[] Values)> rawRows)
    {
        var headerRow = rawRows.FirstOrDefault(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)));
        if (headerRow.Values is null)
        {
            return new ImportedTable([], []);
        }

        var columns = headerRow.Values
            .Select(value => value.Trim())
            .ToArray();
        var dataRows = new List<ImportedRow>();

        foreach (var row in rawRows.Where(row => row.RowNumber > headerRow.RowNumber))
        {
            if (!row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                continue;
            }

            var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columns.Length; index++)
            {
                var column = columns[index];
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                cells[column] = index < row.Values.Length ? row.Values[index]?.Trim() : null;
            }

            dataRows.Add(new ImportedRow(row.RowNumber, cells));
        }

        return new ImportedTable(columns.Where(column => !string.IsNullOrWhiteSpace(column)).ToArray(), dataRows);
    }

    private static ZipArchiveEntry? ResolveFirstWorksheetEntry(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relationshipsEntry is null)
        {
            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        string? relationshipId;
        using (var workbookStream = workbookEntry.Open())
        {
            var workbook = XDocument.Load(workbookStream);
            relationshipId = workbook
                .Descendants(Spreadsheet + "sheet")
                .Select(element => element.Attribute(Relationships + "id")?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        using var relationshipsStream = relationshipsEntry.Open();
        var relationshipsDocument = XDocument.Load(relationshipsStream);
        var target = relationshipsDocument
            .Descendants(PackageRelationships + "Relationship")
            .Where(element => string.Equals(element.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal))
            .Select(element => element.Attribute("Target")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(target))
        {
            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
        if (!normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedTarget = $"xl/{normalizedTarget}";
        }

        return archive.GetEntry(normalizedTarget);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document
            .Descendants(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
        {
            var indexText = cell.Element(Spreadsheet + "v")?.Value;
            return int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                index >= 0 &&
                index < sharedStrings.Count
                    ? sharedStrings[index]
                    : string.Empty;
        }

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));
        }

        return cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
    }

    private static int? ReadColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return null;
        }

        var result = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }

            result = (result * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return result == 0 ? null : result;
    }

    private static int? ReadInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static XDocument CreateContentTypesDocument()
        => new(
            new XElement(
                ContentTypes + "Types",
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/docProps/core.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.core-properties+xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/docProps/app.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.extended-properties+xml"))));

    private static XDocument CreateRootRelationshipsDocument()
        => new(
            new XElement(
                PackageRelationships + "Relationships",
                new XAttribute("xmlns", "http://schemas.openxmlformats.org/package/2006/relationships"),
                new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml")),
                new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"),
                    new XAttribute("Target", "docProps/core.xml")),
                new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", "rId3"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"),
                    new XAttribute("Target", "docProps/app.xml"))));

    private static XDocument CreateWorkbookRelationshipsDocument()
        => new(
            new XElement(
                PackageRelationships + "Relationships",
                new XAttribute("xmlns", "http://schemas.openxmlformats.org/package/2006/relationships"),
                new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml"))));

    private static XDocument CreateWorkbookDocument()
        => new(
            new XElement(
                Spreadsheet + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", Relationships.NamespaceName),
                new XElement(
                    Spreadsheet + "sheets",
                    new XElement(
                        Spreadsheet + "sheet",
                        new XAttribute("name", "Data"),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(Relationships + "id", "rId1")))));

    private static XDocument CreateWorksheetDocument(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        var sheetRows = new List<XElement>
        {
            CreateWorksheetRow(1, columns)
        };

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            sheetRows.Add(CreateWorksheetRow(
                index + 2,
                columns.Select(column => row.TryGetValue(column, out var value) ? value : null)));
        }

        return new XDocument(
            new XElement(
                Spreadsheet + "worksheet",
                new XElement(Spreadsheet + "sheetData", sheetRows)));
    }

    private static XElement CreateWorksheetRow(int rowNumber, IEnumerable<string?> values)
    {
        var cells = values
            .Select((value, index) => new XElement(
                Spreadsheet + "c",
                new XAttribute("r", $"{ColumnName(index + 1)}{rowNumber}"),
                new XAttribute("t", "inlineStr"),
                new XElement(
                    Spreadsheet + "is",
                    new XElement(Spreadsheet + "t", value ?? string.Empty))))
            .ToArray();
        return new XElement(Spreadsheet + "row", new XAttribute("r", rowNumber), cells);
    }

    private static XDocument CreateAppPropertiesDocument()
        => new(
            new XElement(
                ExtendedProperties + "Properties",
                new XElement(ExtendedProperties + "Application", "SeoIntelligence")));

    private static XDocument CreateCorePropertiesDocument()
        => new(
            new XElement(
                CoreProperties + "coreProperties",
                new XElement(CoreProperties + "creator", "SeoIntelligence"),
                new XElement(CoreProperties + "created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))));

    private static void WriteEntry(ZipArchive archive, string entryName, XDocument document)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        document.Save(writer, SaveOptions.DisableFormatting);
    }

    private static string ColumnName(int columnIndex)
    {
        var value = columnIndex;
        var result = new StringBuilder();
        while (value > 0)
        {
            value--;
            result.Insert(0, (char)('A' + (value % 26)));
            value /= 26;
        }

        return result.ToString();
    }
}

internal sealed record ImportedTable(IReadOnlyList<string> Columns, IReadOnlyList<ImportedRow> Rows);

internal sealed record ImportedRow(int RowNumber, IReadOnlyDictionary<string, string?> Cells);
