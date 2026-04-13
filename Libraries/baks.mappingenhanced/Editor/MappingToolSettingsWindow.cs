using Editor;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Editor.Label;

internal sealed class MappingToolSettingsWindow : BaseWindow
{
	private NavigationView Navigation { get; }

	[Menu( "Editor", "Mapping Tools/Settings", Icon = "settings" )]
	public static MappingToolSettingsWindow Open()
	{
		var window = new MappingToolSettingsWindow();
		window.Show();
		return window;
	}

	public MappingToolSettingsWindow()
	{
		SetModal( true, true );
		Size = new Vector2( 640, 420 );
		MinimumSize = Size;
		TranslucentBackground = true;
		NoSystemBackground = true;

		WindowTitle = "Mapping Tool Settings";
		SetWindowIcon( "settings" );

		Layout = Layout.Column();
		Layout.Margin = 4;
		Layout.Spacing = 4;

		Navigation = new NavigationView();
		Layout.Add( Navigation );

		BuildPages();
	}

	private void BuildPages()
	{
		Navigation.AddSectionHeader( "Mapping Tools" );

		Navigation.AddPage( "General", "tune", new PageGeneral( this ) );
		Navigation.AddPage( "Materials Gallery", "image", new PageMaterialBrowser( this ) );
		Navigation.AddPage( "Render Pie Menu", "menu", new PageRenderPieMenu( this ) );
		Navigation.AddPage( "Mapping Pie Menu", "edit", new PageMappingPieMenu( this ) );
		Navigation.AddPage( "Auto-Save", "save", new PageAutoSave( this ) );
	}
}

internal sealed class PageGeneral : Widget
{
	public PageGeneral( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 32;
		Layout.Spacing = 16;

		Layout.Add( new Subtitle( "General Settings" ) );
		Layout.Add( new Editor.Label( "General mapping workflow enhancements" ) { WordWrap = true } );

		var sheet = new Editor.ControlSheet();

		sheet.AddProperty( () => MappingToolSettings.DoubleClickToMeshMode );

		Layout.Add( sheet );

		Layout.AddStretchCell();
	}
}

internal sealed class PageMaterialBrowser : Widget
{
	private Layout _orgListLayout;

	public PageMaterialBrowser( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 32;
		Layout.Spacing = 16;

		Layout.Add( new Subtitle( "Materials Gallery" ) );

		var sheet = new Editor.ControlSheet();

		// Add default org property
		sheet.AddProperty( () => MappingToolSettings.DefaultOrganization );

		Layout.Add( sheet );

		// Organizations list section
		Layout.Add( new Editor.Label.Title( "Additional Organizations" ) );
		Layout.Add( new Editor.Label( "Add organizations to search for materials" ) { WordWrap = true } );

		// Scrollable container for organizations
		var scrollArea = new ScrollArea( this );
		scrollArea.MinimumHeight = 150;
		scrollArea.MaximumHeight = 200;
		Layout.Add( scrollArea );

		var container = new Widget( scrollArea );
		_orgListLayout = container.Layout = Layout.Column();
		_orgListLayout.Spacing = 4;
		scrollArea.Canvas = container;

		// Add organization controls
		var addRow = Layout.AddRow();
		addRow.Spacing = 8;

		var orgInput = new LineEdit();
		orgInput.PlaceholderText = "Enter organization name...";
		orgInput.MinimumWidth = 200;
		addRow.Add( orgInput, 1 );

		var addButton = new Editor.Button( "Add", "add" );
		addButton.Clicked += () =>
		{
			var org = orgInput.Text?.Trim();
			if ( !string.IsNullOrEmpty( org ) )
			{
				MappingToolSettings.AddOrganization( org );
				orgInput.Text = "";
				RefreshOrgList();
			}
		};
		addRow.Add( addButton );

		var resetButton = new Editor.Button( "Reset to Defaults" );
		resetButton.Clicked += () =>
		{
			MappingToolSettings.ResetMaterialGalleryDefaults();
			RefreshOrgList();
		};
		Layout.Add( resetButton );

		Layout.AddStretchCell();

		RefreshOrgList();
	}

	private void RefreshOrgList()
	{
		// Clear existing items
		_orgListLayout.Clear( true );

		var orgs = MappingToolSettings.AdditionalOrganizations.ToList();

		if ( orgs.Count == 0 )
		{
			var emptyLabel = new Editor.Label( "No additional organizations added" );
			emptyLabel.SetStyles( "color: rgba(255,255,255,0.4); font-style: italic;" );
			_orgListLayout.Add( emptyLabel );
			return;
		}

		foreach ( var org in orgs )
		{
			var row = _orgListLayout.AddRow();
			row.Spacing = 8;

			// Org name label
			var nameLabel = new Editor.Label( org );
			nameLabel.MinimumWidth = 150;
			row.Add( nameLabel, 1 );

			// Remove button
			var removeBtn = new Editor.Button.Primary( "", "close" );
			removeBtn.MinimumWidth = 32;
			removeBtn.MaximumWidth = 32;
			removeBtn.ToolTip = $"Remove {org}";
			removeBtn.Clicked += () =>
			{
				MappingToolSettings.RemoveOrganization( org );
				RefreshOrgList();
			};
			row.Add( removeBtn );
		}
	}
}

internal sealed class PageRenderPieMenu : Widget
{
	public PageRenderPieMenu( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 32;
		Layout.Spacing = 16;

		Layout.Add( new Subtitle( "Render Pie Menu" ) );
		Layout.Add( new Editor.Label( "Quick access to render modes and viewport settings" ) { WordWrap = true } );

		var sheet = new Editor.ControlSheet();

		// Automatically generate UI from the settings properties
		sheet.AddProperty( () => MappingToolSettings.PieMenuButton );
		sheet.AddProperty( () => MappingToolSettings.PieMenuUseModifier );
		sheet.AddProperty( () => MappingToolSettings.PieMenuModifierKey );
		sheet.AddProperty( () => MappingToolSettings.PieMenuSize );

		var resetButton = new Editor.Button( "Reset to Defaults" );
		resetButton.Clicked += () =>
		{
			MappingToolSettings.ResetPieMenuDefaults();
		};

		Layout.Add( sheet );
		Layout.Add( resetButton );
		Layout.AddStretchCell();
	}
}

internal sealed class PageMappingPieMenu : Widget
{
	public PageMappingPieMenu( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 32;
		Layout.Spacing = 16;

		Layout.Add( new Subtitle( "Mapping Pie Menu" ) );
		Layout.Add( new Editor.Label( "Quick access to mesh editing modes (Object, Vertex, Edge, Face)" ) { WordWrap = true } );

		var sheet = new Editor.ControlSheet();

		// Automatically generate UI from the settings properties
		sheet.AddProperty( () => MappingToolSettings.MappingPieMenuButton );
		sheet.AddProperty( () => MappingToolSettings.MappingPieMenuUseModifier );
		sheet.AddProperty( () => MappingToolSettings.MappingPieMenuModifierKey );
		sheet.AddProperty( () => MappingToolSettings.MappingPieMenuSize );

		var resetButton = new Editor.Button( "Reset to Defaults" );
		resetButton.Clicked += () =>
		{
			MappingToolSettings.ResetMappingPieMenuDefaults();
		};

		Layout.Add( sheet );
		Layout.Add( resetButton );
		Layout.AddStretchCell();
	}
}

// Add this new page class
internal sealed class PageAutoSave : Widget
{
	public PageAutoSave( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 32;
		Layout.Spacing = 16;

		Layout.Add( new Subtitle( "Auto-Save" ) );
		Layout.Add( new Editor.Label( "Automatically create backup saves at regular intervals" ) { WordWrap = true } );

		var sheet = new Editor.ControlSheet();

		sheet.AddProperty( () => MappingToolSettings.AutoSaveEnabled );
		sheet.AddProperty( () => MappingToolSettings.AutoSaveIntervalMinutes );
		sheet.AddProperty( () => MappingToolSettings.AutoSaveMaxBackups );
		sheet.AddProperty( () => MappingToolSettings.AutoSaveShowNotification );

		Layout.Add( sheet );

		// Force save button
		var saveNowButton = new Editor.Button( "Save Backup Now", "save" );
		saveNowButton.Clicked += () => AutoSave.ForceAutoSave();
		Layout.Add( saveNowButton );

		// Open folder button
		var openFolderButton = new Editor.Button( "Open Autosave Folder", "folder_open" );
		openFolderButton.Clicked += OpenAutoSaveFolder;
		Layout.Add( openFolderButton );

		var resetButton = new Editor.Button( "Reset to Defaults" );
		resetButton.Clicked += () => MappingToolSettings.ResetAutoSaveDefaults();
		Layout.Add( resetButton );

		Layout.AddStretchCell();
	}

	private void OpenAutoSaveFolder()
	{
		var session = SceneEditorSession.Active;
		if ( session?.Scene?.Source?.ResourcePath == null )
		{
			Log.Info( "No active scene" );
			return;
		}

		var sceneDirectory = Path.GetDirectoryName( session.Scene.Source.ResourcePath );
		var autoSaveFolder = Path.Combine( sceneDirectory, "autosave" );
		var fullPath = Editor.FileSystem.ProjectTemporary.GetFullPath( autoSaveFolder );

		// Create the folder if it doesn't exist
		if ( !Directory.Exists( fullPath ) )
		{
			Directory.CreateDirectory( fullPath );
		}

		System.Diagnostics.Process.Start( "explorer.exe", fullPath );
	}
}

/// <summary>
/// Settings for mapping tools including pie menu keybinds
/// </summary>
public static class MappingToolSettings
{
	private const string PreferencePrefix = "MappingTools.";

	// Render Pie Menu Settings
	[Title( "Mouse Button" )]
	[Description( "Mouse button to open the render pie menu" )]
	public static MouseButtons PieMenuButton
	{
		get => (MouseButtons)EditorCookie.Get( PreferencePrefix + "PieMenuButton", (int)MouseButtons.Forward );
		set => EditorCookie.Set( PreferencePrefix + "PieMenuButton", (int)value );
	}

	[Title( "Use Modifier Key" )]
	[Description( "Whether to require a modifier key to open the render pie menu" )]
	public static bool PieMenuUseModifier
	{
		get => EditorCookie.Get( PreferencePrefix + "PieMenuUseModifier", false );
		set => EditorCookie.Set( PreferencePrefix + "PieMenuUseModifier", value );
	}

	[Title( "Modifier Key" )]
	[Description( "Modifier key to open the render pie menu with" )]
	public static KeyCode PieMenuModifierKey
	{
		get => (KeyCode)EditorCookie.Get( PreferencePrefix + "PieMenuModifierKey", (int)KeyCode.Control );
		set => EditorCookie.Set( PreferencePrefix + "PieMenuModifierKey", (int)value );
	}

	[Title( "Menu Size" )]
	[Description( "Radius of the render pie menu in pixels" )]
	[Range( 100, 400 )]
	public static float PieMenuSize
	{
		get => EditorCookie.Get( PreferencePrefix + "PieMenuSize", 180f );
		set => EditorCookie.Set( PreferencePrefix + "PieMenuSize", value );
	}

	// Mapping Pie Menu Settings
	[Title( "Mouse Button" )]
	[Description( "Mouse button to open the mapping mode pie menu" )]
	public static MouseButtons MappingPieMenuButton
	{
		get => (MouseButtons)EditorCookie.Get( PreferencePrefix + "MappingPieMenuButton", (int)MouseButtons.Back );
		set => EditorCookie.Set( PreferencePrefix + "MappingPieMenuButton", (int)value );
	}

	[Title( "Use Modifier Key" )]
	[Description( "Whether to require a modifier key to open the mapping pie menu" )]
	public static bool MappingPieMenuUseModifier
	{
		get => EditorCookie.Get( PreferencePrefix + "MappingPieMenuUseModifier", false );
		set => EditorCookie.Set( PreferencePrefix + "MappingPieMenuUseModifier", value );
	}

	[Title( "Modifier Key" )]
	[Description( "Modifier key to open the mapping pie menu with" )]
	public static KeyCode MappingPieMenuModifierKey
	{
		get => (KeyCode)EditorCookie.Get( PreferencePrefix + "MappingPieMenuModifierKey", (int)KeyCode.Control );
		set => EditorCookie.Set( PreferencePrefix + "MappingPieMenuModifierKey", (int)value );
	}

	[Title( "Menu Size" )]
	[Description( "Radius of the mapping pie menu in pixels" )]
	[Range( 100, 400 )]
	public static float MappingPieMenuSize
	{
		get => EditorCookie.Get( PreferencePrefix + "MappingPieMenuSize", 180f );
		set => EditorCookie.Set( PreferencePrefix + "MappingPieMenuSize", value );
	}

	// Material Gallery Settings
	[Title( "Default Organization" )]
	[Description( "Primary organization to search for materials" )]
	public static string DefaultOrganization
	{
		get => EditorCookie.Get( PreferencePrefix + "DefaultOrganization", "facepunch" );
		set => EditorCookie.Set( PreferencePrefix + "DefaultOrganization", value );
	}

	public static IEnumerable<string> AdditionalOrganizations
	{
		get
		{
			var json = EditorCookie.Get( PreferencePrefix + "AdditionalOrganizations", "[]" );
			return Json.Deserialize<List<string>>( json ) ?? new List<string>();
		}
		set
		{
			var json = Json.Serialize( value );
			EditorCookie.Set( PreferencePrefix + "AdditionalOrganizations", json );
		}
	}

	public static void AddOrganization( string org )
	{
		var orgs = AdditionalOrganizations.ToList();
		if ( !orgs.Contains( org, StringComparer.OrdinalIgnoreCase ) )
		{
			orgs.Add( org );
			AdditionalOrganizations = orgs;
		}
	}

	public static void RemoveOrganization( string org )
	{
		var orgs = AdditionalOrganizations.ToList();
		orgs.RemoveAll( o => o.Equals( org, StringComparison.OrdinalIgnoreCase ) );
		AdditionalOrganizations = orgs;
	}

	public static void ResetPieMenuDefaults()
	{
		PieMenuButton = MouseButtons.Forward;
		PieMenuUseModifier = false;
		PieMenuModifierKey = KeyCode.Control;
		PieMenuSize = 180f;
	}

	public static void ResetMappingPieMenuDefaults()
	{
		MappingPieMenuButton = MouseButtons.Back;
		MappingPieMenuUseModifier = false;
		MappingPieMenuModifierKey = KeyCode.Control;
		MappingPieMenuSize = 180f;
	}

	public static void ResetMaterialGalleryDefaults()
	{
		DefaultOrganization = "facepunch";
		AdditionalOrganizations = new List<string>();
	}

	// Add these to MappingToolSettings class:

	// Auto-Save Settings
	[Title( "Enable Auto-Save" )]
	[Description( "Automatically save backups of your scene at regular intervals" )]
	public static bool AutoSaveEnabled
	{
		get => EditorCookie.Get( PreferencePrefix + "AutoSaveEnabled", true );
		set => EditorCookie.Set( PreferencePrefix + "AutoSaveEnabled", value );
	}

	[Title( "Interval (Minutes)" )]
	[Description( "How often to create a backup save" )]
	[Range( 1, 60 )]
	public static float AutoSaveIntervalMinutes
	{
		get => EditorCookie.Get( PreferencePrefix + "AutoSaveIntervalMinutes", 5f );
		set => EditorCookie.Set( PreferencePrefix + "AutoSaveIntervalMinutes", value );
	}

	[Title( "Maximum Backups" )]
	[Description( "Maximum number of backup files to keep per scene (0 = unlimited)" )]
	[Range( 0, 50 )]
	public static int AutoSaveMaxBackups
	{
		get => EditorCookie.Get( PreferencePrefix + "AutoSaveMaxBackups", 10 );
		set => EditorCookie.Set( PreferencePrefix + "AutoSaveMaxBackups", value );
	}

	[Title( "Show Notification" )]
	[Description( "Show a toast notification when auto-save completes" )]
	public static bool AutoSaveShowNotification
	{
		get => EditorCookie.Get( PreferencePrefix + "AutoSaveShowNotification", true );
		set => EditorCookie.Set( PreferencePrefix + "AutoSaveShowNotification", value );
	}

	// Double-Click Settings
	[Title( "Double-Click to Mesh Mode" )]
	[Description( "Double-click on a GameObject with MeshComponent to enter Mesh Tool mode" )]
	public static bool DoubleClickToMeshMode
	{
		get => EditorCookie.Get( PreferencePrefix + "DoubleClickToMeshMode", false );
		set => EditorCookie.Set( PreferencePrefix + "DoubleClickToMeshMode", value );
	}

	public static void ResetAutoSaveDefaults()
	{
		AutoSaveEnabled = true;
		AutoSaveIntervalMinutes = 5f;
		AutoSaveMaxBackups = 10;
		AutoSaveShowNotification = true;
	}
}
