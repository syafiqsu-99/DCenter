namespace DCenter.Server.Entities;

public class Welder
{
    public int Id { get; set; }
    public string WelderName { get; set; } = string.Empty;
    public string WelderNo { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
