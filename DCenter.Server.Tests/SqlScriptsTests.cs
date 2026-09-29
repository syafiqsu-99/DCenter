using DCenter.Server.Data;
using DCenter.Server.Services;

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

    [Fact]
    public void Matching_PicksOnlyTheNamedObjects()
    {
        var files = SqlScripts.Matching("Procedures", "v1", "SP_DCenter_Wps_");

        Assert.Equal(["SP_DCenter_Wps_Delete", "SP_DCenter_Wps_FindByKey", "SP_DCenter_Wps_Get", "SP_DCenter_Wps_List", "SP_DCenter_Wps_Save"],
            files.Select(SqlScripts.ObjectName));
    }

    [Fact]
    public void Exec_NamesEveryParameterAndMarksOutputs()
    {
        var sql = StoredProcedures.Exec("SP_DCenter_Test", [Sql.Int("@Id", 3), Sql.OutputInt("@Count")]);

        Assert.Equal("EXEC dbo.SP_DCenter_Test @Id = @Id, @Count = @Count OUTPUT", sql);
        Assert.Equal("EXEC dbo.SP_DCenter_Test", StoredProcedures.Exec("SP_DCenter_Test", []));
    }
}
