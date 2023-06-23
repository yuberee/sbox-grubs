namespace Grubs;

[GameResource( "Terrain Set", "terrains", "", Icon = "terrain" )]
public class TerrainSet : GameResource
{
	public List<TerrainSetItem> Terrains { get; set; }
}

public struct TerrainSetItem
{
	public string Id { get; set; }
	public string Name { get; set; }

	[ResourceType( "png" )]
	public string Foreground { get; set; }

	[ResourceType( "png" )]
	public string Background { get; set; }
}
