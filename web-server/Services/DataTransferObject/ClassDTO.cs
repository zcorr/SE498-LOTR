namespace web_server.Services;

public class ClassDTO
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Desc { get; set; } = string.Empty;
	public int[]? Racialids { get; set; }
}