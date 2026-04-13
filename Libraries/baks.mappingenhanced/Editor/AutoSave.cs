using System;
using System.IO;
using System.Threading.Tasks;

namespace Editor;

/// <summary>
/// Automatic backup save system that creates periodic backups of the current scene
/// </summary>
public static class AutoSave
{
	private static RealTimeSince _timeSinceLastSave = 0;
	private static bool _initialized = false;

	[EditorEvent.Frame]
	public static void OnFrame()
	{
		// Skip if disabled or playing
		if ( !MappingToolSettings.AutoSaveEnabled || Game.IsPlaying )
			return;

		// Initialize timer on first frame
		if ( !_initialized )
		{
			_timeSinceLastSave = 0;
			_initialized = true;
			return;
		}

		// Check if it's time to save
		float intervalMinutes = MappingToolSettings.AutoSaveIntervalMinutes;
		if ( _timeSinceLastSave < intervalMinutes * 60f )
			return;

		// Reset timer and save
		_timeSinceLastSave = 0;
		PerformAutoSave();
	}

	[EditorEvent.Hotload]
	public static void OnHotload()
	{
		_initialized = false;
	}

	/// <summary>
	/// Gets the autosave folder path for the current scene
	/// </summary>
	public static string GetAutoSaveFolderPath()
	{
		var session = SceneEditorSession.Active;
		if ( session?.Scene?.Source?.ResourcePath == null )
			return null;

		var originalPath = session.Scene.Source.ResourcePath;
		var sceneName = Path.GetFileNameWithoutExtension( originalPath );
		var sceneDirectory = Path.GetDirectoryName( originalPath );
		var autoSaveFolder = Path.Combine( sceneDirectory, "autosave" );

		return FileSystem.ProjectTemporary.GetFullPath( autoSaveFolder );
	}

	public static void PerformAutoSave()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session?.Scene == null )
			{
				Log.Info( "AutoSave: No active scene to save" );
				return;
			}

			var scene = session.Scene;

			// Get the original scene path
			var originalPath = scene.Source?.ResourcePath;
			if ( string.IsNullOrEmpty( originalPath ) )
			{
				Log.Info( "AutoSave: Scene has no source path (unsaved scene)" );
				return;
			}

			// Get the scene name without extension
			var sceneName = Path.GetFileNameWithoutExtension( originalPath );
			var sceneDirectory = Path.GetDirectoryName( originalPath );

			// Create autosave folder path
			var autoSaveFolder = Path.Combine( sceneDirectory, "autosave" );

			// Ensure the autosave directory exists
			var fullAutoSavePath = FileSystem.ProjectTemporary.GetFullPath( autoSaveFolder );
			if ( !Directory.Exists( fullAutoSavePath ) )
			{
				Directory.CreateDirectory( fullAutoSavePath );
				Log.Info( $"AutoSave: Created autosave folder at {autoSaveFolder}" );
			}

			// Generate timestamp
			var timestamp = DateTime.Now.ToString( "yyyy-MM-dd_HH-mm-ss" );
			var autoSaveFileName = $"{sceneName}_{timestamp}.scene";
			var autoSavePath = Path.Combine( autoSaveFolder, autoSaveFileName );

			// Serialize the scene
			var json = scene.Serialize();

			// Write to file
			FileSystem.ProjectTemporary.WriteAllText( autoSavePath, json.ToString() );
			CleanupOldBackups( fullAutoSavePath, sceneName );

			Log.Info( $"AutoSave: Saved backup to {fullAutoSavePath}" );

			// Show toast notification if enabled
			if ( MappingToolSettings.AutoSaveShowNotification )
			{
				_ = Popup( sceneName, timestamp );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"AutoSave: Failed to save - {ex.Message}" );
		}
	}

	static async Task Popup( string title, string subtitle )
	{
		using var progress = Application.Editor.ProgressSection();
		progress.Title = $"Auto-saved: {title}";
		progress.Subtitle = $"Backup: {subtitle}";

		await Task.Delay( 2000 );
	}

	private static void CleanupOldBackups( string fullAutoSavePath, string sceneName )
	{
		try
		{
			int maxBackups = MappingToolSettings.AutoSaveMaxBackups;
			if ( maxBackups <= 0 ) return; // No limit

			if ( !Directory.Exists( fullAutoSavePath ) ) return;

			// Get all backup files for this scene, sorted by creation time (oldest first)
			var backupFiles = Directory.GetFiles( fullAutoSavePath, $"{sceneName}_*.scene" )
				.Select( f => new FileInfo( f ) )
				.OrderBy( f => f.CreationTime )
				.ToList();

			// Delete oldest files if we exceed the limit
			while ( backupFiles.Count > maxBackups )
			{
				var oldest = backupFiles[0];
				oldest.Delete();
				backupFiles.RemoveAt( 0 );
				Log.Info( $"AutoSave: Deleted old backup {oldest.Name}" );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"AutoSave: Failed to cleanup old backups - {ex.Message}" );
		}
	}

	/// <summary>
	/// Force an immediate autosave
	/// </summary>
	[Menu( "Editor", "Mapping Tools/Force Auto-Save", Icon = "save" )]
	public static void ForceAutoSave()
	{
		PerformAutoSave();
	}
}
