using System.Linq;
using System.Threading.Tasks;

namespace Editor;

/// <summary>
/// Double-click on a GameObject to enter Mesh Tool mode
/// </summary>
public static class DoubleClickMeshMode
{
	private static bool _wasDoubleClick = false;

	[EditorEvent.Frame]
	public static void OnFrame()
	{
		// Skip if disabled or playing
		if ( !MappingToolSettings.DoubleClickToMeshMode || Game.IsPlaying )
			return;

		var sceneView = SceneViewWidget.Current;
		if ( sceneView?.LastSelectedViewportWidget == null )
			return;

		// Get double-click state from the viewport's gizmo instance
		var gizmoInstance = sceneView.LastSelectedViewportWidget.GizmoInstance;
		if ( gizmoInstance == null )
			return;

		bool isDoubleClick = gizmoInstance.Input.DoubleClick;

		// Detect rising edge of double-click
		if ( isDoubleClick && !_wasDoubleClick )
		{
			_ = HandleDoubleClickAsync();
		}

		_wasDoubleClick = isDoubleClick;
	}

	[EditorEvent.Hotload]
	public static void OnHotload()
	{
		_wasDoubleClick = false;
	}

	private static async Task HandleDoubleClickAsync()
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return;

		// Check if we have a GameObject selected
		var selectedObjects = session.Selection.OfType<GameObject>().ToList();
		if ( selectedObjects.Count == 0 )
			return;

		// Check if any selected object has a MeshComponent (editable mesh)
		var hasMeshComponent = selectedObjects.Any( go => go.GetComponent<MeshComponent>() != null );

		if ( !hasMeshComponent )
			return;

		// Check if we're already in mesh tool
		var sceneView = SceneViewWidget.Current;
		var currentTool = sceneView?.Tools?.CurrentTool;
		if ( currentTool is Editor.MeshEditor.MeshTool )
			return;

		// Switch to mesh tool
		EditorToolManager.SetTool( "MeshTool" );
		Log.Info( "DoubleClick: Entered Mesh Tool mode" );

		// Wait for tool to initialize
		await Task.Delay( 50 );

		// Switch to MeshSelection (Object mode) instead of Primitive
		var meshTool = sceneView?.Tools?.CurrentTool as Editor.MeshEditor.MeshTool;
		if ( meshTool != null )
		{
			var tools = meshTool.Tools.ToList();
			var meshSelection = tools.FirstOrDefault( t => t.GetType().Name == "MeshSelection" );
			if ( meshSelection != null )
			{
				meshTool.CurrentTool = meshSelection;
				Log.Info( "DoubleClick: Switched to Object selection mode" );
			}
		}
	}
}
