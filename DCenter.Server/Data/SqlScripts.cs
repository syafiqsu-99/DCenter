using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCenter.Server.Data;

// Views, table types and stored procedures live as .sql files under Data/Sql (embedded in the assembly)
// and reach the database only through migrations. A file used by an applied migration is never edited:
// change it by adding a .v2.sql file and a new migration that runs it.
public static partial class SqlScripts
{
    private static readonly Assembly Assembly = typeof(SqlScripts).Assembly;

    public static void Run(MigrationBuilder migrationBuilder, params string[] files)
    {
        foreach (var file in files)
            foreach (var batch in Batches(Read(file)))
                migrationBuilder.Sql(Wrap(batch));
    }

    // Every file in a folder whose name ends with the given version, e.g. Views/*.v1.sql.
    public static string[] Folder(string folder, string version)
        => Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix + folder + ".", StringComparison.Ordinal)
                        && n.EndsWith($".{version}.sql", StringComparison.Ordinal))
            .Select(n => folder + "/" + n[(ResourcePrefix.Length + folder.Length + 1)..])
            .Order(StringComparer.Ordinal)
            .ToArray();

    // The files in a folder for this version whose object name starts with one of the prefixes, so a
    // migration keeps running the same files after later stages add their own.
    public static string[] Matching(string folder, string version, params string[] prefixes)
        => Folder(folder, version).Where(f => prefixes.Any(p => ObjectName(f).StartsWith(p, StringComparison.Ordinal))).ToArray();

    public static string ObjectName(string file) => Path.GetFileName(file).Split('.')[0];

    public static string Read(string file)
    {
        var name = ResourcePrefix + file.Replace('/', '.');
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"SQL script {file} is not embedded. Check Data/Sql and the EmbeddedResource item in the csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // CREATE VIEW/PROCEDURE must start its batch, and the idempotent migration script puts every
    // statement inside IF ... BEGIN ... END, so each batch runs through dynamic SQL.
    internal static string Wrap(string batch) => $"EXEC(N'{batch.Replace("'", "''")}');";

    private const string ResourcePrefix = "DCenter.Server.Data.Sql.";

    private static IEnumerable<string> Batches(string script)
        => GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim());

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
