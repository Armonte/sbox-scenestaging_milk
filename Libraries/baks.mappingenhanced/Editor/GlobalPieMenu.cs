using System;
using System.Linq;
using System.Threading.Tasks;
using static Editor.EditorEvent;

namespace Editor;

/// <summary>
/// Global pie menu helper for mesh editing modes
/// </summary>
public static class GlobalPieMenu
{
	static PieMenu _currentMenu;
	static bool _wasButtonPressed = false;
	static bool _wasMappingButtonPressed = false;

	[EditorEvent.Hotload]
	public static void ShowPieMenu()
	{
		Log.Info( "ShowPieMenu called" );
		// If menu is already open, close it
		if ( _currentMenu != null && _currentMenu.IsValid() )
		{
			_currentMenu.Close();
			_currentMenu = null;
			return;
		}

		_currentMenu = new PieMenu();
		_currentMenu.TriggerButton = MappingToolSettings.PieMenuButton;
		_currentMenu.Radius = MappingToolSettings.PieMenuSize;
		BuildDefaultPieMenu( _currentMenu );
		_currentMenu.OpenAtCursor();
	}

	[EditorEvent.Hotload]
	public static void ShowMappingPieMenu()
	{
		Log.Info( "ShowMappingPieMenu called" );

		// If menu is already open, close it
		if ( _currentMenu != null && _currentMenu.IsValid() )
		{
			_currentMenu.Close();
			_currentMenu = null;
			return;
		}

		_currentMenu = new PieMenu();
		_currentMenu.TriggerButton = MappingToolSettings.MappingPieMenuButton;
		_currentMenu.Radius = MappingToolSettings.MappingPieMenuSize;
		BuildMappingPieMenu( _currentMenu );
		_currentMenu.OpenAtCursor();
	}

	[EditorEvent.Frame]
	public static void OnFrame()
	{
		// Render Pie Menu
		var configuredButton = MappingToolSettings.PieMenuButton;
		bool isButtonPressed = Application.MouseButtons.HasFlag( configuredButton );

		bool modifierRequirementMet = true;
		if ( MappingToolSettings.PieMenuUseModifier )
		{
			var modifierKey = MappingToolSettings.PieMenuModifierKey;
			modifierRequirementMet = Application.IsKeyDown( modifierKey );
		}

		if ( isButtonPressed && !_wasButtonPressed && !Game.IsPlaying && modifierRequirementMet )
		{
			ShowPieMenu();
		}
		else if ( !isButtonPressed && _wasButtonPressed )
		{
			if ( _currentMenu != null && _currentMenu.IsValid() )
			{
				_currentMenu = null;
			}
		}

		_wasButtonPressed = isButtonPressed;

		// Mapping Pie Menu
		var mappingButton = MappingToolSettings.MappingPieMenuButton;
		bool isMappingButtonPressed = Application.MouseButtons.HasFlag( mappingButton );

		bool mappingModifierMet = true;
		if ( MappingToolSettings.MappingPieMenuUseModifier )
		{
			var mappingModifierKey = MappingToolSettings.MappingPieMenuModifierKey;
			mappingModifierMet = Application.IsKeyDown( mappingModifierKey );
		}

		if ( isMappingButtonPressed && !_wasMappingButtonPressed && !Game.IsPlaying && mappingModifierMet )
		{
			ShowMappingPieMenu();
		}
		else if ( !isMappingButtonPressed && _wasMappingButtonPressed )
		{
			if ( _currentMenu != null && _currentMenu.IsValid() )
			{
				_currentMenu = null;
			}
		}

		_wasMappingButtonPressed = isMappingButtonPressed;
	}

	static void BuildDefaultPieMenu( PieMenu menu )
	{
		// Get active scene viewport if available
		var viewport = GetActiveSceneViewport();

		if ( viewport != null )
		{
			// Scene viewport options
			menu.AddOption( "Normal View", "visibility", () =>
			{
				if ( viewport.State.RenderMode != SceneCameraDebugMode.Normal )
					viewport.State.RenderMode = SceneCameraDebugMode.Normal;
			} );

			menu.AddOption( "Full Bright", "lightbulb", () =>
			{
				viewport.State.RenderMode = viewport.State.RenderMode != SceneCameraDebugMode.FullBright
					? SceneCameraDebugMode.FullBright
					: SceneCameraDebugMode.Normal;
			} );

			menu.AddOption( "Diffuse", "wb_sunny", () =>
			{
				viewport.State.RenderMode = viewport.State.RenderMode != SceneCameraDebugMode.Diffuse
					? SceneCameraDebugMode.Diffuse
					: SceneCameraDebugMode.Normal;
			} );

			menu.AddOption( "Albedo", "texture", () =>
			{
				viewport.State.RenderMode = viewport.State.RenderMode != SceneCameraDebugMode.Albedo
					? SceneCameraDebugMode.Albedo
					: SceneCameraDebugMode.Normal;
			} );

			menu.AddOption( "AO", "blur_circular", () =>
			{
				viewport.State.RenderMode = viewport.State.RenderMode != SceneCameraDebugMode.AmbientOcclusion
					? SceneCameraDebugMode.AmbientOcclusion
					: SceneCameraDebugMode.Normal;
			} );

			menu.AddOption( "Post-Processing", "photo_filter", () =>
			{
				viewport.State.EnablePostProcessing = !viewport.State.EnablePostProcessing;
			} );

			menu.AddOption( "Align To Scene Camera", "three_d_rotation", () =>
			{
				if ( !SceneViewWidget.Current.Session.Scene.IsValid() )
					return;
				var sceneCam = SceneViewWidget.Current.Session.Scene.Scene.Camera;
				if ( !sceneCam.IsValid() )
					return;
				viewport.State.CameraPosition = sceneCam.WorldPosition;
				viewport.State.CameraRotation = sceneCam.WorldRotation;
			} );
		}
	}

	static void BuildMappingPieMenu( PieMenu menu )
	{
		// Object Mode (MeshSelection)
		menu.AddOption( "Mesh", "view_in_ar", () =>
		{
			_ = SwitchToModeAsync( "MeshSelection", "Object" );
		} );

		// Vertex Mode
		menu.AddOption( "Vertex", "push_pin", () =>
		{
			_ = SwitchToModeAsync( "VertexTool", "Vertex" );
		} );

		// Edge Mode
		menu.AddOption( "Edge", "timeline", () =>
		{
			_ = SwitchToModeAsync( "EdgeTool", "Edge" );
		} );

		// Face Mode
		menu.AddOption( "Face", "crop_square", () =>
		{
			_ = SwitchToModeAsync( "FaceTool", "Face" );
		} );
	}

	static async Task SwitchToModeAsync( string toolName, string displayName )
	{
		var meshTool = GetActiveMeshTool();
		bool toolWasActivated = false;

		// Activate mesh tool if not active
		if ( meshTool == null )
		{
			EditorToolManager.SetTool( "MeshTool" );
			toolWasActivated = true;

			// Wait a bit for tool initialization
			await Task.Delay( 50 );

			meshTool = GetActiveMeshTool();
			if ( meshTool == null )
			{
				Log.Warning( "Failed to activate Mesh Tool" );
				return;
			}
		}

		// Get the subtools
		var tools = meshTool.Tools.ToList();

		// If tools list is empty and we just activated, wait a bit longer
		if ( tools.Count == 0 && toolWasActivated )
		{
			await Task.Delay( 50 );
			tools = meshTool.Tools.ToList();
		}

		// Find the target tool
		var targetTool = tools.FirstOrDefault( t => t.GetType().Name == toolName );
		if ( targetTool != null )
		{
			meshTool.CurrentTool = targetTool;
		}
		else
		{
			Log.Warning( $"Could not find {toolName} in mesh tool subtools" );
		}
	}

	static SceneViewportWidget GetActiveSceneViewport()
	{
		// Try to find the currently focused scene viewport
		var focusedWidget = Application.FocusWidget;

		// Walk up the parent chain to find a SceneViewportWidget
		var widget = focusedWidget;
		while ( widget != null )
		{
			if ( widget is SceneViewportWidget viewport )
				return viewport;

			widget = widget.Parent;
		}

		return null;
	}

	static Editor.MeshEditor.MeshTool GetActiveMeshTool()
	{
		var sceneView = SceneViewWidget.Current;
		if ( sceneView?.Tools?.CurrentTool is Editor.MeshEditor.MeshTool meshTool )
		{
			return meshTool;
		}
		return null;
	}
}
