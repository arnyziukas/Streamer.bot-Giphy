# Streamer.bot Giphy Integration

A powerful Streamer.bot action that fetches GIFs from Giphy API and displays them as overlay sources in OBS Studio with automatic positioning, custom sizing, and built-in watermarking.

## 🎯 Features

- **Search Giphy**: Search for GIFs using text commands from chat or other triggers
- **Automatic OBS Integration**: Creates browser sources in OBS with the fetched GIF
- **Smart Positioning**: Configurable positioning (fixed coordinates or random placement)
- **Custom Sizing**: Dynamic GIF sizing with minimum width enforcement and watermark space
- **Built-in Watermarking**: Automatic Giphy watermark overlay for compliance
- **Temporary Files**: Automatic cleanup of generated HTML files
- **Fallback Support**: Default GIF when search terms don't return results
- **Content Rating Control**: Configurable content rating filter (G, PG, PG-13, R)

## 📋 Requirements

### Software Dependencies
- **Streamer.bot** (latest version recommended)
- **OBS Studio** with **obs-websocket** plugin
- **Internet connection** for Giphy API access

### API Requirements
- **Giphy API Key** (free from [Giphy Developers](https://developers.giphy.com/))

## 🚀 Setup Instructions

### 1. Get Giphy API Key
1. Visit [Giphy Developers](https://developers.giphy.com/)
2. Create a free account or sign in
3. Create a new app to get your API key
4. Copy the API key for later use

### 2. Configure Global Variables in Streamer.bot
Set up these global variables in Streamer.bot:

| Variable Name | Type | Description | Example Value |
|---------------|------|-------------|---------------|
| `Giph_API_Key` | String | Your Giphy API key | `your_api_key_here` |
| `Giph_rating` | String | Content rating filter | `pg-13` |
| `Giph_defaut` | String | Default search term when no input | `funny cat` |
| `Giph_Position_x` | Integer | Fixed X position (-1 for random) | `-1` |
| `Giph_Position_y` | Integer | Fixed Y position (-1 for random) | `-1` |
| `Giph_Sleep_S` | Integer | Display duration in seconds | `5` |

### 3. Install the Action
1. Copy the code from `code.cs`
2. Create a new C# action in Streamer.bot
3. Paste the code and compile
4. Configure the action trigger (e.g., chat command `!gif`)

### 4. OBS Setup
1. Ensure OBS Studio is running
2. Enable obs-websocket plugin
3. Connect Streamer.bot to OBS
4. The action will automatically create browser sources as needed

## ⚙️ Configuration Options

### Content Rating Levels
- `g` - General audiences
- `pg` - Parental guidance suggested  
- `pg-13` - Parents strongly cautioned
- `r` - Restricted

### Position Settings
- Set `Giph_Position_x` and `Giph_Position_y` to `-1` for random positioning
- Set specific coordinates for fixed positioning
- The system automatically centers GIFs on the specified coordinates

### Size Behavior
- **Minimum Width**: 200px (automatically enforced)
- **Height Adjustment**: Original height + 42px (for watermark space)
- **Aspect Ratio**: Maintained from original GIF

## 🎮 Usage

### Basic Usage
1. Trigger the action (e.g., type `!gif` in chat)
2. The system will use the default search term
3. GIF appears in OBS for the configured duration
4. Files are automatically cleaned up

### With Search Terms
If your trigger supports arguments:
1. Use command with search term: `!gif dancing cat`
2. The action searches Giphy for "dancing cat"
3. Displays the first result found

### Example Triggers
- **Chat Command**: `!gif [search_term]`
- **Channel Points**: Redeem to show random GIF
- **Follows/Subs**: Automatic celebration GIFs
- **Hotkeys**: Manual GIF display

## 🔧 Technical Details

### File Management
- Creates temporary HTML files in system temp directory
- Unique filenames using GUID to prevent conflicts
- Automatic cleanup after display duration
- Base64 encoded GIFs for offline display

### HTML Structure
- Responsive container with black background
- GIF displays at top with proper scaling
- Watermark positioned at bottom center
- Fixed 200x42px watermark dimensions

### Error Handling
- Graceful fallback for API failures
- Logs all operations for debugging
- Validates OBS connection before execution
- Handles missing search results

### Performance Features
- Static HttpClient for connection reuse
- Efficient Base64 encoding
- Minimal memory footprint
- Fast HTML generation

## 🐛 Troubleshooting

### Common Issues

**GIFs not appearing in OBS:**
- Check OBS connection in Streamer.bot
- Verify obs-websocket is enabled
- Ensure OBS scene is active

**API errors:**
- Verify Giphy API key is correct
- Check internet connection
- Confirm API key hasn't exceeded rate limits

**Positioning issues:**
- Check canvas size detection
- Verify coordinate values are within bounds
- Test with random positioning first

**No search results:**
- Try different search terms
- Check content rating restrictions
- Verify default GIF setting

### Debug Information
- Check Streamer.bot logs for detailed error messages
- All operations are logged with `[Giphy Action]` prefix
- API responses and OBS commands are logged for debugging

## 📝 Global Variables Reference

```
Giph_API_Key=your_giphy_api_key_here
Giph_rating=pg-13
Giph_defaut=funny meme
Giph_Position_x=-1
Giph_Position_y=-1
Giph_Sleep_S=5
```

## 🔄 Changelog

### Current Version
- ✅ Custom property getters for Width/Height
- ✅ Unique FilePath generation with GUID
- ✅ HTML file saving with composite layout
- ✅ Centered watermark positioning (200x42px)
- ✅ Automatic cleanup and error handling
- ✅ Base64 embedded content for reliability

## 📄 License

This project is provided as-is for use with Streamer.bot. Please ensure compliance with Giphy's terms of service when using their API.

## 🤝 Contributing

Feel free to submit issues, feature requests, or improvements to enhance this Giphy integration for the Streamer.bot community.

---

**Note**: This action requires a valid Giphy API key and active OBS connection to function properly. Always test in a controlled environment before using in live streams.
