using DCenter.Server.Services;

namespace DCenter.Server.Tests;

public class StoredProceduresTests
{
    [Fact]
    public void Exec_NamesEveryParameterAndMarksOutputs()
    {
        var sql = StoredProcedures.Exec("SP_Test", [Sql.Int("@Id", 3), Sql.OutputInt("@Count")]);

        Assert.Equal("EXEC dcenter.SP_Test @Id = @Id, @Count = @Count OUTPUT", sql);
        Assert.Equal("EXEC dcenter.SP_Test", StoredProcedures.Exec("SP_Test", []));
    }

    [Fact]
    public void MissingMessage_NamesTheProcedureAndTheScriptToRun()
    {
        var message = StoredProcedures.MissingMessage("SP_Welder_List");

        Assert.Contains("dcenter.SP_Welder_List", message);
        Assert.Contains("Sql/DCenter/DCenter_StoredProcedures.sql", message);
    }
}
