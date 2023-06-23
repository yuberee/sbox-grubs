namespace Grubs;

public class TerrainTextureParser
{
	public static async Task<bool> Load( string url )
	{
		var userImage = await Texture.LoadAsync( FileSystem.Mounted, "textures/ramona_comic.png" );
		if ( userImage is null )
			return false;

		var pixels = userImage.GetPixels();
		var sdfData = new byte[pixels.Length * 4];

		for ( int i = 0; i < pixels.Length; i++ )
		{
			if ( IsPixelAir( pixels[i] ) )
			{
				sdfData[i * 4] = 0;
				sdfData[i * 4 + 1] = 0;
				sdfData[i * 4 + 2] = 0;
				sdfData[i * 4 + 3] = 255;
			}
			else
			{
				sdfData[i * 4] = 255;
				sdfData[i * 4 + 1] = 255;
				sdfData[i * 4 + 2] = 255;
				sdfData[i * 4 + 3] = 255;
			}
		}

		var sdfTexture = Texture.Create( userImage.Width, userImage.Height, ImageFormat.BGRA8888 ).WithData( sdfData ).Finish();
		Log.Info( sdfTexture );
		return true;
	}

	private static bool IsPixelAir( Color32 color )
	{
		return color.a < 255;
	}

	[ConCmd.Client]
	public static void LoadUserImage( string url )
	{
		Load( url );
	}
}
