using System;
using System.IO;
using System.Threading;
using System.Net.Http;
using Newtonsoft.Json.Linq;

public class CPHInline
{
	private readonly string FGiphyURI = "https://api.giphy.com/v1/gifs/search?api_key={0}&q={1}&limit=1&offset=0&rating={2}&lang=en&bundle=messaging_non_clips";
	
	private static HttpClient FHttpClient;
	private string FGiphyAPIKey;

	private string FRating, FDefaultGif;	
	private int FPosX, FPosY, FSleepSeconds;
	
	public class TGiphyGif
	{
		public string Url { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }

		public override string ToString()
		{
			return $"URL: {Url}, Width: {Width}, Height: {Height}";
		}
	}
	
	//generic calls to override CPH calls to make formatting easier...
	private void LogInfo( string LogLine ){
		CPH.LogInfo( "[Giphy Action]" + LogLine );
	}
	private void LogInfo( string procedure, string LogLine ){
		LogInfo( string.Format( "({0}){1}", procedure, LogLine ) );
	}
	
	private void LogError( string LogLine ){
		CPH.LogError( "[Giphy Action]" + LogLine );
	}
	
	private void LogError( Exception ex ){
		LogError( string.Format( "({0}){1}", ex.Message, ex.StackTrace ) );
	}	
	
	private T GetGlobalVariable<T>( string variableName, bool isGlobal, T defaultVal ){
		var val = CPH.GetGlobalVar<T>( variableName, isGlobal );
		
		if( val == null || val.Equals( default( T ) ) )
			return defaultVal;
			
		return val;
	}
	
	//fully random thing to the a name of X size...
	private string GetRandomName(){
		const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		const int stringLength = 10;
		Random rand = new Random();
		char[] buffer = new char[ stringLength ];

		for (int i = 0; i < stringLength; i++)
		{
			buffer[i] = chars[ rand.Next( chars.Length ) ];
		}

		return new string(buffer);
	}
	
	//OBS stuff to work with items on the scene( getscene, createitem, getitemid, deleteitem .....)
	private string getSceneName(){
		try {
			var currentSceneResponse = CPH.ObsSendRaw( "GetCurrentProgramScene", "{}" );	
			LogInfo( "getSceneName", currentSceneResponse );
			var currentSceneJSON = JObject.Parse( currentSceneResponse );
			return currentSceneJSON[ "currentProgramSceneName" ]?.ToString();
		} catch ( Exception ex ){
			LogError( ex );
			return null;
		}
	}
	
	private int CreateNewSource( string SceneName, string SourceName, TGiphyGif GifData ){
		try {
			string fileUrl = "file:///" + GifData.Url.Replace("\\", "/");

			string createSourceInputJSON = $@"{{
												""sceneName"": ""{SceneName}"",
												""inputName"": ""{SourceName}"",
												""inputKind"": ""browser_source"",
												""inputSettings"": {{
													""url"": ""{fileUrl}"",
													""width"": {GifData.Width},
													""height"": {GifData.Height}
												}},
												""sceneItemEnabled"": true
											}}";
			string newSourceResponse = CPH.ObsSendRaw( "CreateInput", createSourceInputJSON );
			LogInfo( "CreateNewSource", newSourceResponse );
			var newSourceParsed = JObject.Parse( newSourceResponse );
			return newSourceParsed["sceneItemId"].ToObject<int>();
		}
		catch ( Exception ex ){
			LogError( ex );
			return -1;
		}
	}
	
	private int GetSourceID( string SceneName, string SourceName ){
		try {
			string getItemIdJson = $@"{{
										""sceneName"": ""{SceneName}"",
										""sourceName"": ""{SourceName}""
									}}";
			string getItemIdResponse = CPH.ObsSendRaw( "GetSceneItemId", getItemIdJson );
			var parsedId = JObject.Parse( getItemIdResponse );
			return parsedId["sceneItemId"].ToObject<int>();	
		} catch ( Exception ex ){
			LogError( ex );
			return -1;	
		}
	}
	
	private void PositionNewSource( string SceneName, int SourceID, TGiphyGif GifData ){
		try {
			string moveJson = $@"{{
									""sceneName"": ""{SceneName}"",
									""sceneItemId"": {SourceID},
									""sceneItemTransform"": {{
										""positionX"": {( FPosX - ( GifData.Width / 2 ) )},
										""positionY"": {( FPosY - ( GifData.Height / 2 ) )}
									}}
								}}";
			string NewSourceTransform = CPH.ObsSendRaw( "SetSceneItemTransform", moveJson );
		} catch ( Exception ex ){
			LogError( ex );
		}
	}
	
	private void RemoveNewSource( string SourceName ){
		try {
			string removeInputJson = $@"{{ ""inputName"": ""{SourceName}"" }}";
			CPH.ObsSendRaw("RemoveInput", removeInputJson);
			LogInfo( "Source Removed" );
		} catch ( Exception ex ){
			LogError( ex );
		}
	}
	
	private bool GetGiphyGif( string searchTerm, out TGiphyGif gifData )
	{
		gifData = null;

		try
		{
			// Build Giphy API request
			string uri = string.Format(FGiphyURI, FGiphyAPIKey, Uri.EscapeDataString(searchTerm), FRating);

			// Request GIF metadata from Giphy
			HttpResponseMessage response = FHttpClient.GetAsync(uri).GetAwaiter().GetResult();
			response.EnsureSuccessStatusCode();

			string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			var parsed = JObject.Parse(responseBody);

			// Check status
			bool gifStatus = parsed["meta"]?["status"]?.ToString() == "200";
			if (!gifStatus)
				return false;

			// Extract image info
			var image = parsed["data"]?[0]?["images"]?["fixed_height"];
			if (image == null)
				return false;

			string remoteUrl = image["url"]?.ToString(); // or use "url" for gif
			int width = int.TryParse(image["width"]?.ToString(), out int w) ? w : 320;
			int height = int.TryParse(image["height"]?.ToString(), out int h) ? h : 200;

			// Download the media file locally
			string localPath = DownloadMediaFile(remoteUrl);
			if (string.IsNullOrEmpty(localPath))
				return false;

			// Set gif data to point to local file
			gifData = new TGiphyGif
			{
				Url = localPath,
				Width = width,
				Height = height
			};

			LogInfo("GetGiphyGif", gifData.ToString());
			return true;
		}
		catch (Exception ex)
		{
			LogError(ex);
			return false;
		}
	}
	
	private string DownloadMediaFile( string url )
	{
		try
		{
			string outputDir = Path.GetTempPath();

			string extension = Path.GetExtension(url).Split('?')[0];
			if (string.IsNullOrEmpty(extension)) extension = ".gif";

			string fileName = $"giphy_{Guid.NewGuid()}{extension}";
			string fullPath = Path.Combine(outputDir, fileName);

			// Download with FHttpClient and stream to file
			using (var response = FHttpClient.GetAsync(url).GetAwaiter().GetResult())
			{
				response.EnsureSuccessStatusCode();
				using (var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
				using (var output = File.Create(fullPath))
				{
					input.CopyTo(output);
				}
			}

			LogInfo("DownloadMediaFile", $"Downloaded to: {fullPath}");
			return fullPath;
		}
		catch (Exception ex)
		{
			LogError( ex );
			return null;
		}
	}
	private void ClearInstance( TGiphyGif GifData, string ItemName )
	{
		RemoveNewSource( ItemName );
		try
		{
			if ( File.Exists( GifData.Url ) )
			{
				File.Delete( GifData.Url );
				LogInfo("DeleteMediaFile", $"Deleted: {GifData.Url}");
			}
		}
		catch (Exception ex)
		{
			LogError( ex );
		}
	}
	public void Init()
    {
        FHttpClient = new HttpClient();
        FGiphyAPIKey = GetGlobalVariable<string>( "Giph_API_Key", true, "" );
        FRating = GetGlobalVariable<string>( "Giph_rating", true, "pg-13" );
        FDefaultGif = GetGlobalVariable<string>( "Giph_defaut", true, "jim carey what is love" ); 
        FPosX = GetGlobalVariable<int>( "Giph_Position_x", true, 800 );
        FPosY = GetGlobalVariable<int>( "Giph_Position_y", true, 600 );
        FSleepSeconds = GetGlobalVariable<int>( "Giph_Sleep_S", true, 5 );
        //LogInfo( string.Format( "Has been loaded with default city: {0}, default Lat: {1}, default Lon: {2}", defaultCity, defaultLat, defaultLon ) );
    }

    public void Dispose()
    {
        FHttpClient.Dispose();
    }
    
	public bool Execute()
	{
        CPH.TryGetArg( "rawInput", out string rawInput );
		if( string.IsNullOrWhiteSpace( rawInput ) )
			rawInput = FDefaultGif;
		if( GetGiphyGif( rawInput, out TGiphyGif GifData ) ){
			string SourceName = GetRandomName();
			if( !CPH.ObsIsConnected() ){
				LogError( "OBS Is Not Connected - Check your OBS Setup" );
				return false;
			}
			string SceneName = getSceneName();
			
			if( string.IsNullOrWhiteSpace( SceneName ) ){
				LogError( "Was not able to get the current Scene Name" );
				return false;
			}
			
			//Create a source which SHOULD return ItemID ( sometimes fails???? )
			int ItemID = CreateNewSource( SceneName, SourceName, GifData );
			if( ItemID == -1 ){
				// a fall back if the first call did create the source, but failed to provide ID....
				ItemID = GetSourceID( SceneName, SourceName );
			}
			//if we still don't have an ID after the fallback - its all screwed up...
			if( ItemID == -1 ){
				LogError( "Could Not get ItemID" );
				return false;	
			}
			PositionNewSource( SceneName, ItemID, GifData );
			
			Thread.Sleep( FSleepSeconds * 1000 );
			
			ClearInstance( GifData, SourceName );
			// your main code goes here
			return true;
		}
		return false;
	}
}
