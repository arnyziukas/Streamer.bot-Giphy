using System;
using System.IO;
using System.Threading;
using System.Net.Http;
using Newtonsoft.Json.Linq;

public enum PositionType { ptX, ptY }

public class CPHInline
{
    private readonly string FGiphyURI = "https://api.giphy.com/v1/gifs/search?api_key={0}&q={1}&limit=1&offset=0&rating={2}&lang=en&bundle=messaging_non_clips";

    private static HttpClient FHttpClient;
    private string FGiphyAPIKey;

    private string FRating, FDefaultGif;
    private int FPosX = -1, FPosY = -1, FMaxX = 1, FMaxY = 1, FSleepSeconds;
    private static Random FRandom = new Random();

    private Func<int> FGetXPosition, FGetYPosition;


    public class TGiphyGif
    {
        public string GifBase64 { get; set; }
        public string GiphyWatermark { get; set; }
        public string Url { get; set; } // Not used in this context, but can be useful for debugging or logging.
        private int _width, _height;
        private string _filePath;
        public int Width
        {
            get { return Math.Max(_width, 200); }
            set { _width = value; }
        }
        public int Height
        {
            get { return _height + 42; }
            set { _height = value; }
        }
        public string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_filePath))
                {
                    _filePath = Path.Combine(Path.GetTempPath(), $"giphy_{Guid.NewGuid():N}.html");
                }
                return _filePath;
            }
        }

        public string ToHtmlDocument()
        {
            string gifDataUrl = $"data:image/gif;base64,{GifBase64}";
            string gifWatermark = $"data:image/gif;base64,{GiphyWatermark}";
            string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ margin: 0; padding: 0; background: transparent; overflow: hidden; }}
        .container {{ position: relative; width: {Width}px; height: {Height}px; background-color: black; display: flex; flex-direction: column; justify-content: center; align-items: center; }}
        .gif-image {{ width: 100%; height: auto; object-fit: contain; flex-shrink: 0; }}
        .watermark-image {{ width: 200px; height: 42px; object-fit: contain; }}
    </style>
</head>
<body>
    <div class=""container"">
        <img src=""{gifDataUrl}"" class=""gif-image"" />
        <img src=""{gifWatermark}"" class=""watermark-image"">
    </div>
</body>
</html>";
            File.WriteAllText(FilePath, htmlContent);
            return FilePath;
        }

        public override string ToString()
        {
            return $"GIF URL: {Url}, Width: {Width}, Height: {Height}";
        }
    }

    //generic calls to override CPH calls to make formatting easier...
    private void LogDebug(string LogLine)
    {
        CPH.LogDebug("[Giphy Action]" + LogLine);
    }
    private void LogDebug(string procedure, string LogLine)
    {
        LogDebug(string.Format("({0}){1}", procedure, LogLine));
    }

    private void LogError(string LogLine)
    {
        CPH.LogError("[Giphy Action]" + LogLine);
    }

    private void LogError(Exception ex)
    {
        LogError(string.Format("({0}){1}", ex.Message, ex.StackTrace));
    }

    private T GetGlobalVariable<T>(string variableName, bool isGlobal, T defaultVal)
    {
        var val = CPH.GetGlobalVar<T>(variableName, isGlobal);

        if (val == null || val.Equals(default(T)))
            return defaultVal;

        return val;
    }

    //fully random thing to the a name of X size...
    private string GetRandomName()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const int stringLength = 10;
        Random rand = new Random();
        char[] buffer = new char[stringLength];

        for (int i = 0; i < stringLength; i++)
        {
            buffer[i] = chars[rand.Next(chars.Length)];
        }

        return new string(buffer);
    }

    //OBS stuff to work with items on the scene( getscene, createitem, getitemid, deleteitem .....)
    private string getSceneName()
    {
        try
        {
            var currentSceneResponse = CPH.ObsSendRaw("GetCurrentProgramScene", "{}");
            LogDebug("getSceneName", currentSceneResponse);
            var currentSceneJSON = JObject.Parse(currentSceneResponse);
            return currentSceneJSON["currentProgramSceneName"]?.ToString();
        }
        catch (Exception ex)
        {
            LogError(ex);
            return null;
        }
    }

    private int CreateNewSource(string SceneName, string SourceName, TGiphyGif GifData)
    {
        try
        {
            string htmlPath = GifData.ToHtmlDocument();
            string fileUrl = "file:///" + htmlPath.Replace("\\", "/");

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
            string newSourceResponse = CPH.ObsSendRaw("CreateInput", createSourceInputJSON);
            LogDebug("CreateNewSource", newSourceResponse);
            var newSourceParsed = JObject.Parse(newSourceResponse);
            return newSourceParsed["sceneItemId"].ToObject<int>();
        }
        catch (Exception ex)
        {
            LogError(ex);
            return -1;
        }
    }

    private int GetSourceID(string SceneName, string SourceName)
    {
        try
        {
            string getItemIdJson = $@"{{
										""sceneName"": ""{SceneName}"",
										""sourceName"": ""{SourceName}""
									}}";
            string getItemIdResponse = CPH.ObsSendRaw("GetSceneItemId", getItemIdJson);
            var parsedId = JObject.Parse(getItemIdResponse);
            return parsedId["sceneItemId"].ToObject<int>();
        }
        catch (Exception ex)
        {
            LogError(ex);
            return -1;
        }
    }

    private void PositionNewSource(string SceneName, int SourceID, TGiphyGif GifData)
    {
        try
        {
            string moveJson = $@"{{
									""sceneName"": ""{SceneName}"",
									""sceneItemId"": {SourceID},
									""sceneItemTransform"": {{
										""positionX"": {centerOnPosition(GifData.Width, PositionType.ptX)},
										""positionY"": {centerOnPosition(GifData.Height, PositionType.ptY)}
									}}
								}}";
            string NewSourceTransform = CPH.ObsSendRaw("SetSceneItemTransform", moveJson);
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    private void RemoveNewSource(string SourceName)
    {
        try
        {
            string removeInputJson = $@"{{ ""inputName"": ""{SourceName}"" }}";
            CPH.ObsSendRaw("RemoveInput", removeInputJson);
            LogDebug("Source Removed");
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    private bool GetGiphyGif(string searchTerm, out TGiphyGif gifData)
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
            string gifBase64 = ConvertGifUrlToBase64(remoteUrl);
            if (string.IsNullOrEmpty(gifBase64))
                return false;

            // Set gif data with Base64
            gifData = new TGiphyGif
            {
                GifBase64 = gifBase64,
                Width = width,
                Height = height,
                Url = remoteUrl,
                GiphyWatermark = GiphyWatermark
            };

            LogDebug("GetGiphyGif", gifData.ToString());
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return false;
        }
    }

    private void ClearInstance(TGiphyGif GifData, string ItemName)
    {
        RemoveNewSource(ItemName);
        try
        {
            if (File.Exists(GifData.FilePath))
            {
                File.Delete(GifData.FilePath);
                LogDebug("DeleteMediaFile", $"Deleted: {GifData.FilePath}");
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }
    private string ConvertGifUrlToBase64(string gifUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(gifUrl))
                return null;

            using (var response = FHttpClient.GetAsync(gifUrl).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                byte[] gifBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return Convert.ToBase64String(gifBytes);
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            return null;
        }
    }
    private int centerOnPosition(int size, PositionType positionType)
    {
        switch (positionType)
        {
            case PositionType.ptX:
                int centerX = FGetXPosition();
                int topLeftX = centerX - (size / 2);
                if (topLeftX < 0) return 0;
                else if (topLeftX + size > FMaxX) return FMaxX - size;
                else return topLeftX;
            case PositionType.ptY:
                int centerY = FGetYPosition();
                int topLeftY = centerY - (size / 2);
                if (topLeftY < 0) return 0;
                else if (topLeftY + size > FMaxY) return FMaxY - size;
                else return topLeftY;
            default:
                return 0;
        }
    }
    private void initializeOBS()
    {
        if (CPH.ObsIsConnected())
        {
            var videoSettingsJson = CPH.ObsSendRaw("GetVideoSettings", "{}");
            var videoSettings = JObject.Parse(videoSettingsJson);
            int canvasWidth = (int)videoSettings["baseWidth"];
            int canvasHeight = (int)videoSettings["baseHeight"];
            FMaxX = canvasWidth;
            FMaxY = canvasHeight;
        }
    }
    public void Init()
    {
        FHttpClient = new HttpClient();
        FGiphyAPIKey = GetGlobalVariable<string>("Giph_API_Key", true, "");
        FRating = GetGlobalVariable<string>("Giph_rating", true, "pg-13");
        FDefaultGif = GetGlobalVariable<string>("Giph_defaut", true, "Shrek is Love Shrek is Life");
        FPosX = GetGlobalVariable<int>("Giph_Position_x", true, -1);
        FPosY = GetGlobalVariable<int>("Giph_Position_y", true, -1);
        FSleepSeconds = GetGlobalVariable<int>("Giph_Sleep_S", true, 5);

        if (FPosX == -1)
            FGetXPosition = () => FRandom.Next(0, FMaxX); // Default width
        else
            FGetXPosition = () => FPosX;

        if (FPosY == -1)
            FGetYPosition = () => FRandom.Next(0, FMaxY); // Default height
        else
            FGetYPosition = () => FPosY;
        initializeOBS();
    }

    public void Dispose()
    {
        FHttpClient.Dispose();
    }

    public bool Execute()
    {
        //if we dont have OBS connected OR the API key is not set, no point in trying to do anything... nothing will work...
        if (!CPH.ObsIsConnected() || string.IsNullOrWhiteSpace(FGiphyAPIKey))
            return false;

        //in case we didnt have the OBS open when we compiled the code, fix the Maxes here....    
        if (FMaxX == 1 && FMaxY == 1)
            initializeOBS();

        CPH.TryGetArg("rawInput", out string rawInput);
        if (string.IsNullOrWhiteSpace(rawInput))
            rawInput = FDefaultGif;
        if (GetGiphyGif(rawInput, out TGiphyGif GifData))
        {
            string SourceName = GetRandomName();
            if (!CPH.ObsIsConnected())
            {
                LogError("OBS Is Not Connected - Check your OBS Setup");
                return false;
            }
            string SceneName = getSceneName();

            if (string.IsNullOrWhiteSpace(SceneName))
            {
                LogError("Was not able to get the current Scene Name");
                return false;
            }

            //Create a source which SHOULD return ItemID ( sometimes fails???? )
            int ItemID = CreateNewSource(SceneName, SourceName, GifData);
            if (ItemID == -1)
            {
                // a fall back if the first call did create the source, but failed to provide ID....
                ItemID = GetSourceID(SceneName, SourceName);
            }
            //if we still don't have an ID after the fallback - its all screwed up...
            if (ItemID == -1)
            {
                LogError("Could Not get ItemID");
                return false;
            }
            PositionNewSource(SceneName, ItemID, GifData);

            Thread.Sleep(FSleepSeconds * 1000);

            ClearInstance( GifData, SourceName );
            // your main code goes here
            return true;
        }
        return false;
    }

    private readonly string GiphyWatermark = "R0lGODlhyAAqANU/AG1ubQPR/3FhsPRpa09PT0+d2ZRam1XU+5fp+fKTd/7//puQWP74lDIyMqxVUf//rjG667GxsQwKDSYeT/vSiP7+zpgt9YeHh8v3/l6KuC2GqM7PzwCMU1cqcvm0gSFMZM7MelBJLwMnL1AnLAD+ren+/uzs68ZigbCzfP//546psQDOfNXUj0tGeW9EeM/PsS4TEiQiHnZtRhQWGikSMI7B0Dtmh3xBV9Pf436dqIc8QuLv9p+ekefmrAAAAP///yH/C1hNUCBEYXRhWE1QPD94cGFja2V0IGJlZ2luPSLvu78iIGlkPSJXNU0wTXBDZWhpSHpyZVN6TlRjemtjOWQiPz4gPHg6eG1wbWV0YSB4bWxuczp4PSJhZG9iZTpuczptZXRhLyIgeDp4bXB0az0iQWRvYmUgWE1QIENvcmUgNS42LWMwMTQgNzkuMTU2Nzk3LCAyMDE0LzA4LzIwLTA5OjUzOjAyICAgICAgICAiPiA8cmRmOlJERiB4bWxuczpyZGY9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkvMDIvMjItcmRmLXN5bnRheC1ucyMiPiA8cmRmOkRlc2NyaXB0aW9uIHJkZjphYm91dD0iIiB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIgeG1sbnM6c3RSZWY9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9zVHlwZS9SZXNvdXJjZVJlZiMiIHhtbG5zOnhtcD0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wLyIgeG1wTU06T3JpZ2luYWxEb2N1bWVudElEPSJ4bXAuZGlkOjBlMWViMjQ3LTVmZTItNGNjOS1hMDk5LWJmNTU0YmU2NjUxNyIgeG1wTU06RG9jdW1lbnRJRD0ieG1wLmRpZDo2MEQwNjk3RTk0NjAxMUU0QTFGM0QyRDdDRjA4QjAzNCIgeG1wTU06SW5zdGFuY2VJRD0ieG1wLmlpZDo2MEQwNjk3RDk0NjAxMUU0QTFGM0QyRDdDRjA4QjAzNCIgeG1wOkNyZWF0b3JUb29sPSJBZG9iZSBQaG90b3Nob3AgQ0MgMjAxNCAoTWFjaW50b3NoKSI+IDx4bXBNTTpEZXJpdmVkRnJvbSBzdFJlZjpp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"; 
}
