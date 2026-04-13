using BspImport.Tools.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace BspImport.Tools;

class BspInspector : Window
{

	//[Menu( "Editor", "BSP Import/Inspect BSP (debug)", "map" )]
	public static void OpenInspector()
	{
		var tool = new BspInspector();
		tool.Show();
	}

	private Widget DataWidget;

	public BspInspector()
	{
		WindowTitle = "BSP Inspector (debug)";

		Canvas = new Widget( this );
		Canvas.Layout = Layout.Row();
		Canvas.Layout.Margin = 16;
		Canvas.Layout.Spacing = 4;
		this.MinimumHeight = 700;
		this.MinimumWidth = 650;

		// left column, for settings
		var leftCol = new Widget( Canvas );
		leftCol.Layout = Layout.Column();
		leftCol.FixedWidth = 300;
		leftCol.Layout.Margin = 16;
		leftCol.Layout.Spacing = 4;
		leftCol.SetStyles( "background-color: #222222" );
		Canvas.Layout.Add( leftCol );
		Canvas.Layout.AddSeparator( true );

		// setup control
		var newSettings = new InspectorSettings();

		var cookieString = "bsp-import.last-imported-bsp-debug";
		var settings = Game.Cookies.Get( cookieString, newSettings );

		if ( settings.FilePath != string.Empty )
		{
			TryDecompile( settings );
		}

		var settingsControl = new ControlSheet();
		var settingsSerialized = settings.GetSerialized();
		settingsSerialized.OnPropertyChanged += ( prop ) =>
		{
			if ( prop.Name == nameof( InspectorSettings.FilePath ) )
			{
				Log.Info( $"updated" );
				TryDecompile( settings );
			}

			Game.Cookies.Set( cookieString, settings );
			RebuildDataWidget( settings );
		};
		settingsControl.AddObject( settingsSerialized );

		leftCol.Layout.Add( settingsControl );
		leftCol.Layout.AddStretchCell();

		// right column, for info display
		DataWidget = new Widget( Canvas );
		DataWidget.Layout = Layout.Column();
		DataWidget.Layout.Margin = 16;
		DataWidget.Layout.Spacing = 4;
		DataWidget.Layout.AddStretchCell();
		DataWidget.SetStyles( "background-color: #222222" );
		Canvas.Layout.Add( DataWidget );

		RebuildDataWidget( settings ); // does nothing until a valid context is loaded

		this.Center();
	}

	private void RebuildDataWidget( InspectorSettings settings )
	{
		DataWidget.DestroyChildren();

		if ( Context == null )
			return;

		var contextControl = new ControlSheet();
		var serialized = Context.GetSerialized();

		Func<SerializedProperty, bool> filter = x => x.IsValid();

		if ( settings.Section == InspectorSection.Entities )
			filter += x => x.Name == nameof( ImportContext.Entities );

		if ( settings.Section == InspectorSection.TexDataStringData )
			filter += x => x.Name == nameof( ImportContext.TexDataStringData );

		contextControl.AddObject( serialized, filter );
		contextControl.IncludePropertyNames = true;
		DataWidget.Layout.Add( contextControl, 10 );

		DataWidget.Layout.AddStretchCell();
	}

	private ImportContext? Context { get; set; }

	private void TryDecompile( InspectorSettings settings )
	{
		var path = settings.FilePath;
		if ( !Editor.FileSystem.Content.FileExists( settings.FilePath ) )
			return;

		Log.Info( $"Decompile triggered {settings.FilePath}" );

		var data = Editor.FileSystem.Content.ReadAllBytes( path );
		var name = Path.GetFileName( path );

		Context = null;
		Context = new ImportContext( name, data.ToArray() );
		var decompiler = new MapDecompiler( Context );
		decompiler.GetFileInfo();
	}

}
