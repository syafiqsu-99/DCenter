using DCenter.Server.Data;

namespace DCenter.Server.Tests;

public class SqlScriptsTests
{
    [Fact]
    public void Folder_ListsEveryEmbeddedViewWithTheVDCenterPrefix()
    {
        var views = SqlScripts.Folder("Views", "v1");

        Assert.Equal(20, views.Length);
        Assert.All(views, v => Assert.StartsWith("V_DCenter_", SqlScripts.ObjectName(v)));
        Assert.All(views, v => Assert.Contains($"CREATE OR ALTER VIEW dbo.{SqlScripts.ObjectName(v)}", SqlScripts.Read(v)));
    }

    [Fact]
    public void Wrap_RunsTheBatchAsDynamicSqlWithQuotesDoubled()
        => Assert.Equal("EXEC(N'SELECT N''a''''b'' AS x');", SqlScripts.Wrap("SELECT N'a''b' AS x"));
}
